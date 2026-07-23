using System;
using System.Reflection;
using HarmonyLib;

namespace VoiceOutputDeviceChanger.Interop.Game;

internal static class GameReflection
{
    public static Type StartOfRoundType { get; } = RequireType("StartOfRound");

    public static Type MenuManagerType { get; } = RequireType("MenuManager");

    public static Type QuickMenuManagerType { get; } = RequireType("QuickMenuManager");

    public static Type SettingsOptionType { get; } = RequireType("SettingsOption");

    public static FieldInfo AllPlayerScriptsField { get; } = RequireField(StartOfRoundType, "allPlayerScripts");

    public static FieldInfo LocalPlayerControllerField { get; } = RequireField(StartOfRoundType, "localPlayerController");

    public static FieldInfo IsPlayerControlledField { get; } = RequireField(RequireType("GameNetcodeStuff.PlayerControllerB"), "isPlayerControlled");

    public static FieldInfo IsPlayerDeadField { get; } = RequireField(RequireType("GameNetcodeStuff.PlayerControllerB"), "isPlayerDead");

    public static FieldInfo CurrentVoiceSourceField { get; } = RequireField(RequireType("GameNetcodeStuff.PlayerControllerB"), "currentVoiceChatAudioSource");

    public static FieldInfo SettingsOptionTypeField { get; } = RequireField(SettingsOptionType, "optionType");

    public static FieldInfo SettingsTextElementField { get; } = RequireField(SettingsOptionType, "textElement");

    public static FieldInfo MenuIsInitSceneField { get; } = RequireField(MenuManagerType, "isInitScene");

    public static MethodInfo StartOfRoundRefreshMethod { get; } = RequireMethod(StartOfRoundType, "RefreshPlayerVoicePlaybackObjects");

    public static MethodInfo MenuManagerStartMethod { get; } = RequireMethod(MenuManagerType, "Start");

    public static MethodInfo QuickMenuManagerStartMethod { get; } = RequireMethod(QuickMenuManagerType, "Start");

    public static MethodInfo SettingsOptionIntMethod { get; } = RequireMethod(SettingsOptionType, "SetSettingsOptionInt");

    private static Type RequireType(string name)
    {
        return AccessTools.TypeByName(name) ?? throw new TypeLoadException($"Lethal Company v81 type '{name}' was not found.");
    }

    private static FieldInfo RequireField(Type type, string name)
    {
        return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
    }

    private static MethodInfo RequireMethod(Type type, string name)
    {
        return AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
    }
}
