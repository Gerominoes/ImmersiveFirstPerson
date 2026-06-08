using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("valheim.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.geronimo.valheim.immersivefirstperson";
    public const string PluginName = "Immersive First Person";
    public const string PluginVersion = "1.3.0";

    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<bool> EnableMod = null!;
    internal static ConfigEntry<KeyCode> ToggleFirstPersonKey = null!;
    internal static ConfigEntry<bool> DefaultToFirstPerson = null!;

    internal static ConfigEntry<bool> OverrideForcedThirdPerson = null!;
    internal static ConfigEntry<bool> LockCameraWhileAttached = null!;
    internal static ConfigEntry<float> AttachedCameraExtraVerticalOffset = null!;
    internal static ConfigEntry<float> AttachedCameraExtraForwardOffset = null!;
    internal static ConfigEntry<float> AttachedCameraMaxYaw = null!;
    internal static ConfigEntry<float> AttachedCameraMaxPitch = null!;

    internal static ConfigEntry<bool> UseHeadTrackedAnchor = null!;
    internal static ConfigEntry<float> CameraVerticalOffset = null!;
    internal static ConfigEntry<float> CameraForwardOffset = null!;
    internal static ConfigEntry<float> DownLookExtraForwardOffset = null!;
    internal static ConfigEntry<float> DownLookExtraVerticalOffset = null!;
    internal static ConfigEntry<float> CrouchVerticalOffset = null!;
    internal static ConfigEntry<float> NearClip = null!;
    internal static ConfigEntry<bool> UseCustomFov = null!;
    internal static ConfigEntry<float> Fov = null!;
    internal static ConfigEntry<float> HeadBobAmount = null!;
    internal static ConfigEntry<bool> LockBodyToCamera = null!;
    internal static ConfigEntry<float> BodyRotationFollowSpeed = null!;

    internal static ConfigEntry<float> FirstPersonShadowDistance = null!;
    internal static ConfigEntry<int> FirstPersonShadowCascades = null!;
    internal static ConfigEntry<bool> UseOcclusionCulling = null!;
    internal static ConfigEntry<bool> DisableCameraEffects = null!;

    internal static ConfigEntry<bool> HideHead = null!;
    internal static ConfigEntry<bool> ForceBodyVisible = null!;
    internal static ConfigEntry<float> VisibilityRefreshInterval = null!;

    internal static ConfigEntry<bool> EnableDebugLogs = null!;
    internal static ConfigEntry<bool> LogRendererNames = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;

        // General settings.
        EnableMod = Config.Bind("General", "EnableMod", true, "Enable Immersive First Person.");

        // Input settings.
        ToggleFirstPersonKey = Config.Bind("Input", "ToggleFirstPersonKey", KeyCode.F6, "Press this key to toggle first-person mode.");
        DefaultToFirstPerson = Config.Bind("Input", "DefaultToFirstPerson", false, "Start in first-person mode when the local player is ready.");

        // Camera override settings.
        OverrideForcedThirdPerson = Config.Bind("Camera Overrides", "OverrideForcedThirdPerson", true, "Keep first-person mode active during gameplay interactions that normally force third person, such as inventory, crafting, ships, hold fast, and attached states.");
        LockCameraWhileAttached = Config.Bind("Camera Overrides", "LockCameraWhileAttached", true, "Lock the first-person camera to a captured head-level body offset while attached to seats, ships, hold-fast points, and similar attach points. Reduces attachment jitter and rubberbanding.");
        AttachedCameraExtraVerticalOffset = Config.Bind("Camera Overrides", "AttachedCameraExtraVerticalOffset", 0f, "Extra vertical offset added to the captured head-level camera position while attached.");
        AttachedCameraExtraForwardOffset = Config.Bind("Camera Overrides", "AttachedCameraExtraForwardOffset", 0.08f, "Extra forward offset added to the captured head-level camera position while attached.");
        AttachedCameraMaxYaw = Config.Bind("Camera Overrides", "AttachedCameraMaxYaw", 80f, new ConfigDescription("Maximum left/right camera yaw from the attached body direction.", new AcceptableValueRange<float>(20f, 180f)));
        AttachedCameraMaxPitch = Config.Bind("Camera Overrides", "AttachedCameraMaxPitch", 55f, new ConfigDescription("Maximum up/down camera pitch while attached.", new AcceptableValueRange<float>(20f, 89f)));

        // Camera placement settings.
        UseHeadTrackedAnchor = Config.Bind("Camera", "UseHeadTrackedAnchor", true, "Anchor the camera to the animated head bone when found. Falls back to the player eye transform.");
        CameraVerticalOffset = Config.Bind("Camera", "CameraVerticalOffset", 0.04f, "Vertical offset from the selected camera anchor.");
        CameraForwardOffset = Config.Bind("Camera", "CameraForwardOffset", 0.16f, "Forward offset from the selected camera anchor to keep the torso out of the view.");
        DownLookExtraForwardOffset = Config.Bind("Camera", "DownLookExtraForwardOffset", 0.16f, "Extra forward offset applied gradually when looking down.");
        DownLookExtraVerticalOffset = Config.Bind("Camera", "DownLookExtraVerticalOffset", 0.06f, "Extra upward offset applied gradually when looking down.");
        CrouchVerticalOffset = Config.Bind("Camera", "CrouchVerticalOffset", -0.45f, "Additional vertical camera offset while crouching or sneaking when head tracking is unavailable or head bob is reduced.");
        NearClip = Config.Bind("Camera", "NearClip", 0.02f, "Near clipping plane while first-person mode is active.");
        UseCustomFov = Config.Bind("Camera", "UseCustomFov", true, "Use the configured first-person FOV.");
        Fov = Config.Bind("Camera", "FOV", 75f, "Field of view while first-person mode is active.");
        HeadBobAmount = Config.Bind("Camera Motion", "HeadBobAmount", 0.5f, new ConfigDescription("Controls how much fast animation-based head motion affects the first-person camera. 0 keeps only filtered head tracking. 1 uses full tracked head motion.", new AcceptableValueRange<float>(0f, 1f)));
        LockBodyToCamera = Config.Bind("Camera", "LockBodyToCamera", true, "Rotate the local player body yaw to match vanilla camera yaw while first-person mode is active.");
        BodyRotationFollowSpeed = Config.Bind("Camera", "BodyRotationFollowSpeed", 0f, "How quickly the body rotates to the camera yaw. Set to 0 for instant body lock.");

        // Graphics optimization settings.
        FirstPersonShadowDistance = Config.Bind("Graphics", "FirstPersonShadowDistance", 30f, new ConfigDescription("Maximum shadow draw distance while first-person mode is active. Set to -1 to keep the game's current shadow distance.", new AcceptableValueRange<float>(-1f, 500f)));
        FirstPersonShadowCascades = Config.Bind("Graphics", "FirstPersonShadowCascades", 0, new ConfigDescription("Maximum shadow cascade count while first-person mode is active. Set to -1 to keep the game's current cascade count. Values are normalized to 0, 2, or 4.", new AcceptableValueRange<int>(-1, 4)));
        UseOcclusionCulling = Config.Bind("Graphics", "UseOcclusionCulling", true, "Enable camera occlusion culling while first-person mode is active.");
        DisableCameraEffects = Config.Bind("Graphics", "DisableCameraEffects", false, "Disable known camera post-processing components while first-person mode is active, then restore them when leaving first person.");

        // Visibility settings.
        HideHead = Config.Bind("Visibility", "HideHead", false, "Hide the local player's head and head-slot equipment from the first-person camera while preserving shadows.");
        ForceBodyVisible = Config.Bind("Visibility", "ForceBodyVisible", true, "Force the local player body and held items visible while first-person mode is active.");
        VisibilityRefreshInterval = Config.Bind("Visibility", "VisibilityRefreshInterval", 1f, new ConfigDescription("Seconds between head-slot and head-bone refresh scans while HideHead is enabled. Lower values detect equipment changes sooner and cost more CPU.", new AcceptableValueRange<float>(0.1f, 10f)));

        // Debug settings.
        EnableDebugLogs = Config.Bind("Debug", "EnableDebugLogs", false, "Enable extra logs for camera, state, visibility, and cleanup behavior.");
        LogRendererNames = Config.Bind("Debug", "LogRendererNames", false, "Log local player renderer names, paths, materials, and enabled states once when first-person mode activates.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        LogOptimizationPolicy();
    }

    private static void LogOptimizationPolicy()
    {
        // Startup policy logging makes game logs enough to verify immersion-safe settings.
        Log.LogInfo($"Optimization policy: view distance unchanged; LOD unchanged; shadowDistanceCap={FirstPersonShadowDistance.Value}; shadowCascadesCap={FirstPersonShadowCascades.Value}; occlusionCulling={UseOcclusionCulling.Value}; disableCameraEffects={DisableCameraEffects.Value}; visibilityRefreshInterval={VisibilityRefreshInterval.Value}s.");
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
