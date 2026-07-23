using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using VoiceOutputDeviceChanger.Interop.Game;
using VoiceOutputDeviceChanger.Interop.Settings;
using VoiceOutputDeviceChanger.Interop.WindowsAudio;

namespace VoiceOutputDeviceChanger;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Lethal Company.exe")]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Unity owns MonoBehaviour lifetime; OnDestroy performs the deterministic rollback.")]
public sealed class Plugin : BaseUnityPlugin
{
    private Harmony? _harmony;
    private AudioDeviceSelectionController? _selectionController;
    private VoiceAudioRouter? _router;

    private void Awake()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Logger.LogError("Voice Output Device Changer requires Windows Core Audio and has been disabled on this platform.");
            return;
        }

        try
        {
            ConfigEntry<string> endpointId = Config.Bind(
                "General",
                "RemoteVoiceOutputEndpointId",
                string.Empty,
                "Windows audio endpoint ID used for remote player voices. Empty selects the current multimedia default device.");

            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
            {
                sampleRate = 48000;
                Logger.LogWarning("Unity did not report an output sample rate; falling back to 48000 Hz for alternate voice output.");
            }

            _router = new VoiceAudioRouter(Logger, sampleRate, string.Empty);
            _selectionController = new AudioDeviceSelectionController(Logger, endpointId, _router);
            _selectionController.ApplyConfiguredSelection();
            IntegrationContext.Initialize(Logger, _router, _selectionController);

            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded for Lethal Company v81.");
        }
        catch (Exception exception)
        {
            IntegrationContext.Clear();
            _harmony?.UnpatchSelf();
            _selectionController?.Dispose();
            _router?.Dispose();
            _harmony = null;
            _selectionController = null;
            _router = null;
            Logger.LogError($"Voice Output Device Changer could not initialize and was disabled: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        IntegrationContext.Clear();
        _harmony?.UnpatchSelf();
        _selectionController?.Dispose();
        _router?.Dispose();
        _harmony = null;
        _selectionController = null;
        _router = null;
    }
}
