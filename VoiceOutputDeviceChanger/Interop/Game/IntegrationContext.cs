using System;
using BepInEx.Logging;
using VoiceOutputDeviceChanger.Interop.Settings;
using VoiceOutputDeviceChanger.Interop.WindowsAudio;

namespace VoiceOutputDeviceChanger.Interop.Game;

internal static class IntegrationContext
{
    private static ManualLogSource? _logger;
    private static VoiceAudioRouter? _router;
    private static AudioDeviceSelectionController? _selectionController;

    public static void Initialize(
        ManualLogSource logger,
        VoiceAudioRouter router,
        AudioDeviceSelectionController selectionController)
    {
        _logger = logger;
        _router = router;
        _selectionController = selectionController;
    }

    public static void Clear()
    {
        _selectionController = null;
        _router = null;
        _logger = null;
    }

    public static bool TryGetRouter(out VoiceAudioRouter? router)
    {
        router = _router;
        return router is not null;
    }

    public static bool TryGetSelectionController(out AudioDeviceSelectionController? controller)
    {
        controller = _selectionController;
        return controller is not null;
    }

    public static void RunGuarded(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            TryLog(LogLevel.Error, $"{operation} failed without interrupting the game: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void TryLog(LogLevel level, string message)
    {
        try
        {
            _logger?.Log(level, message);
        }
        catch
        {
            // Diagnostics are secondary to preserving the game callback.
        }
    }
}
