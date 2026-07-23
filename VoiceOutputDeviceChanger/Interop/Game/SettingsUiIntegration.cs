using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VoiceOutputDeviceChanger.Interop.Settings;

namespace VoiceOutputDeviceChanger.Interop.Game;

internal static class SettingsUiIntegration
{
    private const int MicrophoneDeviceOption = 5;
    private const int DontTellOption = 19;
    private const string RemoteVoiceOutputName = "VoiceOutputDeviceChanger.RemoteVoiceOutput";

    public static void Inject(Component menuRoot, AudioDeviceSelectionController controller)
    {
        Component[] options = menuRoot.GetComponentsInChildren(GameReflection.SettingsOptionType, true);
        foreach (Component option in options)
        {
            if (!string.Equals(option.gameObject.name, "ChooseDevice", StringComparison.Ordinal) ||
                Convert.ToInt32(GameReflection.SettingsOptionTypeField.GetValue(option), CultureInfo.InvariantCulture) != MicrophoneDeviceOption)
            {
                continue;
            }

            Transform parent = option.transform.parent;
            if (parent.Find(RemoteVoiceOutputName) is not null)
            {
                continue;
            }

            bool wasActive = option.gameObject.activeSelf;
            GameObject clone = UnityEngine.Object.Instantiate(option.gameObject, parent, false);
            try
            {
                clone.SetActive(false);

                Component clonedOption = clone.GetComponent(GameReflection.SettingsOptionType);
                Type enumType = GameReflection.SettingsOptionTypeField.FieldType;
                GameReflection.SettingsOptionTypeField.SetValue(clonedOption, Enum.ToObject(enumType, DontTellOption));

                if (clone.transform is RectTransform rectangle)
                {
                    rectangle.anchoredPosition += new Vector2(0f, -52f);
                }

                object? textElement = GameReflection.SettingsTextElementField.GetValue(clonedOption);
                VoiceOutputDeviceOption marker = clone.AddComponent<VoiceOutputDeviceOption>();
                marker.Initialize(controller, textElement);
                clone.SetActive(wasActive);
                clone.name = RemoteVoiceOutputName;
            }
            catch
            {
                UnityEngine.Object.Destroy(clone);
                throw;
            }
        }
    }
}

[HarmonyPatch]
internal static class MenuManagerStartPatch
{
    private static MethodInfo TargetMethod()
    {
        return GameReflection.MenuManagerStartMethod;
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        IntegrationContext.RunGuarded(
            "Main-menu output selector injection",
            () =>
            {
                bool isInitScene = (bool)(GameReflection.MenuIsInitSceneField.GetValue(__instance) ?? false);
                if (!isInitScene &&
                    __instance is Component component &&
                    IntegrationContext.TryGetSelectionController(out AudioDeviceSelectionController? controller) &&
                    controller is not null)
                {
                    SettingsUiIntegration.Inject(component, controller);
                }
            });
    }
}

[HarmonyPatch]
internal static class QuickMenuManagerStartPatch
{
    private static MethodInfo TargetMethod()
    {
        return GameReflection.QuickMenuManagerStartMethod;
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        IntegrationContext.RunGuarded(
            "In-game output selector injection",
            () =>
            {
                if (__instance is Component component &&
                    IntegrationContext.TryGetSelectionController(out AudioDeviceSelectionController? controller) &&
                    controller is not null)
                {
                    SettingsUiIntegration.Inject(component, controller);
                }
            });
    }
}

[HarmonyPatch]
internal static class SettingsOptionIntPatch
{
    private static MethodInfo TargetMethod()
    {
        return GameReflection.SettingsOptionIntMethod;
    }

    [HarmonyPrefix]
    private static bool Prefix(object __instance)
    {
        if (__instance is not Component component)
        {
            return true;
        }

        VoiceOutputDeviceOption? marker = component.GetComponent<VoiceOutputDeviceOption>();
        if (marker is null)
        {
            return true;
        }

        IntegrationContext.RunGuarded("Remote voice output selection", marker.Cycle);
        return false;
    }
}
