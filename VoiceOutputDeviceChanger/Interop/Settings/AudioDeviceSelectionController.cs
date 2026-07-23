using System;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using VoiceOutputDeviceChanger.Interop.WindowsAudio;

namespace VoiceOutputDeviceChanger.Interop.Settings;

internal sealed class AudioDeviceSelectionController : IDisposable
{
    private readonly ConfigEntry<string> _configuredEndpointId;
    private readonly ManualLogSource _logger;
    private readonly VoiceAudioRouter _router;
    private bool _disposed;

    public AudioDeviceSelectionController(
        ManualLogSource logger,
        ConfigEntry<string> configuredEndpointId,
        VoiceAudioRouter router)
    {
        _logger = logger;
        _configuredEndpointId = configuredEndpointId;
        _router = router;
        _configuredEndpointId.SettingChanged += OnSettingChanged;
    }

    public string ApplyConfiguredSelection()
    {
        return ResolveSelection(cycle: false);
    }

    public string CycleSelection()
    {
        return ResolveSelection(cycle: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _configuredEndpointId.SettingChanged -= OnSettingChanged;
    }

    private string ResolveSelection(bool cycle)
    {
        if (_disposed)
        {
            return "REMOTE VOICE OUTPUT\nMOD IS SHUTTING DOWN";
        }

        try
        {
            AudioEndpointSnapshot snapshot = AudioEndpointService.EnumerateActiveRenderEndpoints();
            string configuredId = _configuredEndpointId.Value ?? string.Empty;
            int configuredIndex = FindEndpoint(snapshot, configuredId);

            if (cycle)
            {
                int nextIndex = configuredIndex < -1 ? snapshot.Endpoints.Count : configuredIndex + 1;
                string nextId = nextIndex < 0 || nextIndex >= snapshot.Endpoints.Count
                    ? string.Empty
                    : snapshot.Endpoints[nextIndex].Id;
                _configuredEndpointId.Value = nextId;
                configuredId = nextId;
                configuredIndex = FindEndpoint(snapshot, configuredId);
            }

            if (string.IsNullOrEmpty(configuredId))
            {
                _router.SelectEndpoint(string.Empty);
                return $"REMOTE VOICE OUTPUT\nSYSTEM DEFAULT: {GetDefaultName(snapshot)}";
            }

            if (configuredIndex >= 0)
            {
                AudioEndpoint endpoint = snapshot.Endpoints[configuredIndex];
                _router.SelectEndpoint(endpoint.Id);
                return $"REMOTE VOICE OUTPUT\n{Shorten(endpoint.Name)}";
            }

            _router.SelectEndpoint(string.Empty);
            return $"REMOTE VOICE OUTPUT\nMISSING DEVICE; DEFAULT: {GetDefaultName(snapshot)}";
        }
        catch (Exception exception)
        {
            try
            {
                _router.Suspend();
            }
            catch (ObjectDisposedException)
            {
                // Shutdown already prevents alternate routing from muting Unity output.
            }

            TryLog(LogLevel.Error, $"Audio endpoint enumeration failed; Unity output remains enabled. {exception.GetType().Name}: {exception.Message}");
            return "REMOTE VOICE OUTPUT\nUNAVAILABLE; UNITY OUTPUT ACTIVE";
        }
    }

    private static int FindEndpoint(AudioEndpointSnapshot snapshot, string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
        {
            return -1;
        }

        for (int index = 0; index < snapshot.Endpoints.Count; index++)
        {
            if (string.Equals(snapshot.Endpoints[index].Id, endpointId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -2;
    }

    private static string GetDefaultName(AudioEndpointSnapshot snapshot)
    {
        int index = FindEndpoint(snapshot, snapshot.DefaultEndpointId);
        return index >= 0 ? Shorten(snapshot.Endpoints[index].Name) : "CURRENT WINDOWS DEVICE";
    }

    private static string Shorten(string value)
    {
        const int maximumLength = 48;
        string displayName = SanitizeDisplayName(value);
        return displayName.Length <= maximumLength ? displayName : displayName.Substring(0, maximumLength - 1) + "…";
    }

    private static string SanitizeDisplayName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (character == '<')
            {
                builder.Append('‹');
            }
            else if (character == '>')
            {
                builder.Append('›');
            }
            else if (char.IsControl(character))
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        string sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? "UNNAMED DEVICE" : sanitized;
    }

    private void OnSettingChanged(object sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ApplyConfiguredSelection();
    }

    private void TryLog(LogLevel level, string message)
    {
        try
        {
            _logger.Log(level, message);
        }
        catch
        {
            // Diagnostics must not turn device loss into a game callback failure.
        }
    }
}
