using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VoiceOutputDeviceChanger.Interop.WindowsAudio;

namespace VoiceOutputDeviceChanger.Interop.Game;

internal static class VoicePlaybackIntegration
{
    public static void Refresh(object startOfRound, VoiceAudioRouter router)
    {
        object? localPlayer = GameReflection.LocalPlayerControllerField.GetValue(startOfRound);
        if (localPlayer is null)
        {
            return;
        }

        if (GameReflection.AllPlayerScriptsField.GetValue(startOfRound) is not Array players)
        {
            return;
        }

        var routedSources = new HashSet<AudioSource>();
        foreach (object? player in players)
        {
            if (player is null || IsSameUnityObject(player, localPlayer))
            {
                continue;
            }

            bool isControlled = (bool)(GameReflection.IsPlayerControlledField.GetValue(player) ?? false);
            bool isDead = (bool)(GameReflection.IsPlayerDeadField.GetValue(player) ?? false);
            if (!isControlled && !isDead)
            {
                continue;
            }

            if (GameReflection.CurrentVoiceSourceField.GetValue(player) is not AudioSource source || source == null)
            {
                continue;
            }

            routedSources.Add(source);
            VoiceCaptureFilter filter = source.GetComponent<VoiceCaptureFilter>() ?? source.gameObject.AddComponent<VoiceCaptureFilter>();
            filter.Initialize(router);
        }

        VoiceCaptureFilter[] filters = UnityEngine.Object.FindObjectsOfType<VoiceCaptureFilter>(true);
        foreach (VoiceCaptureFilter filter in filters)
        {
            AudioSource? source = filter.GetComponent<AudioSource>();
            if (source == null || !routedSources.Contains(source))
            {
                filter.Deactivate();
            }
        }
    }

    private static bool IsSameUnityObject(object left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is UnityEngine.Object leftObject &&
               right is UnityEngine.Object rightObject &&
               leftObject == rightObject;
    }
}

[HarmonyPatch]
internal static class RefreshPlayerVoicePlaybackObjectsPatch
{
    private static MethodInfo TargetMethod()
    {
        return GameReflection.StartOfRoundRefreshMethod;
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        IntegrationContext.RunGuarded(
            "Remote voice source attachment",
            () =>
            {
                if (IntegrationContext.TryGetRouter(out VoiceAudioRouter? router) && router is not null)
                {
                    VoicePlaybackIntegration.Refresh(__instance, router);
                }
            });
    }
}
