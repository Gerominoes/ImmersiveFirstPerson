using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal enum HeadHideModeOption
{
    RendererDisable,
    BoneShrink
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("valheim.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.geronimo.valheim.immersivefirstperson";
    public const string PluginName = "Immersive First Person";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> EnableMod = null!;
    internal static ConfigEntry<KeyCode> ToggleFirstPersonKey = null!;
    internal static ConfigEntry<bool> DefaultToFirstPerson = null!;

    internal static ConfigEntry<float> CameraVerticalOffset = null!;
    internal static ConfigEntry<float> CameraForwardOffset = null!;
    internal static ConfigEntry<float> NearClip = null!;
    internal static ConfigEntry<bool> UseCustomFov = null!;
    internal static ConfigEntry<float> Fov = null!;
    internal static ConfigEntry<bool> SmoothCamera = null!;
    internal static ConfigEntry<float> CameraSmoothing = null!;
    internal static ConfigEntry<bool> LockBodyToCamera = null!;
    internal static ConfigEntry<float> BodyRotationFollowSpeed = null!;

    internal static ConfigEntry<bool> HideHead = null!;
    internal static ConfigEntry<bool> HideHair = null!;
    internal static ConfigEntry<bool> HideFace = null!;
    internal static ConfigEntry<bool> HideHelmet = null!;
    internal static ConfigEntry<bool> HideShoulderPads = null!;
    internal static ConfigEntry<bool> HideBackItems = null!;
    internal static ConfigEntry<bool> ForceBodyVisible = null!;
    internal static ConfigEntry<HeadHideModeOption> HeadHideModeConfig = null!;

    internal static ConfigEntry<bool> EnableDebugLogs = null!;
    internal static ConfigEntry<bool> LogRendererNames = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;

        EnableMod = Config.Bind("General", "EnableMod", true, "Enable Immersive First Person.");
        ToggleFirstPersonKey = Config.Bind("Input", "ToggleFirstPersonKey", KeyCode.F6, "Press this key to toggle first-person mode.");
        DefaultToFirstPerson = Config.Bind("Input", "DefaultToFirstPerson", false, "Start in first-person mode when the local player is ready.");

        CameraVerticalOffset = Config.Bind("Camera", "CameraVerticalOffset", 0.08f, "Vertical offset from the player's eye transform.");
        CameraForwardOffset = Config.Bind("Camera", "CameraForwardOffset", 0.05f, "Forward offset from the player's eye transform.");
        NearClip = Config.Bind("Camera", "NearClip", 0.03f, "Near clipping plane while first-person mode is active.");
        UseCustomFov = Config.Bind("Camera", "UseCustomFov", true, "Use the configured first-person FOV.");
        Fov = Config.Bind("Camera", "FOV", 75f, "Field of view while first-person mode is active.");
        SmoothCamera = Config.Bind("Camera", "SmoothCamera", false, "Optional extra smoothing for camera position. Disabled by default so vanilla mouse behavior is preserved.");
        CameraSmoothing = Config.Bind("Camera", "CameraSmoothing", 18f, "How quickly the camera moves toward the first-person target if SmoothCamera is enabled.");
        LockBodyToCamera = Config.Bind("Camera", "LockBodyToCamera", true, "Rotate the local player body yaw to match vanilla camera yaw while first-person mode is active.");
        BodyRotationFollowSpeed = Config.Bind("Camera", "BodyRotationFollowSpeed", 0f, "How quickly the body rotates to the camera yaw. Set to 0 for instant body lock.");

        HideHead = Config.Bind("Visibility", "HideHead", true, "Hide the local player's head while first-person mode is active.");
        HideHair = Config.Bind("Visibility", "HideHair", true, "Hide the local player's hair while first-person mode is active.");
        HideFace = Config.Bind("Visibility", "HideFace", true, "Hide face-related local player renderers while first-person mode is active.");
        HideHelmet = Config.Bind("Visibility", "HideHelmet", true, "Hide the local player's helmet while first-person mode is active.");
        HideShoulderPads = Config.Bind("Visibility", "HideShoulderPads", false, "Hide shoulder-related renderers if armor clips into the camera.");
        HideBackItems = Config.Bind("Visibility", "HideBackItems", false, "Hide back, cape, and cloak renderers if they clip into the camera.");
        ForceBodyVisible = Config.Bind("Visibility", "ForceBodyVisible", true, "Force the local player and non-head renderers visible while first-person mode is active.");
        HeadHideModeConfig = Config.Bind("Visibility", "HeadHideMode", HeadHideModeOption.BoneShrink, "How the local player's head is hidden. RendererDisable hides matched renderers. BoneShrink scales matched head bones down.");

        EnableDebugLogs = Config.Bind("Debug", "EnableDebugLogs", false, "Enable extra logs for camera, state, visibility, and cleanup behavior.");
        LogRendererNames = Config.Bind("Debug", "LogRendererNames", false, "Log local player renderer names, paths, materials, and enabled states once when first-person mode activates.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    internal static void DebugLog(string message)
    {
        if (EnableDebugLogs?.Value == true)
            Log.LogInfo($"[Debug] {message}");
    }

    private void OnDestroy()
    {
        FirstPersonState.ForceInactive();
        BodyVisibilityController.Reset();
        HeadVisibilityController.ForceVisible();
        FirstPersonCamera.RestoreLastCamera();
        _harmony?.UnpatchSelf();
    }
}
