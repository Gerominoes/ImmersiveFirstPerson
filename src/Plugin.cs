using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveBuildCamera;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("valheim.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.geronimo.valheim.immersivebuildcamera";
    public const string PluginName = "Immersive Build Camera";
    public const string PluginVersion = "1.1.0";

    internal static ManualLogSource Log = null!;

    internal static ConfigEntry<KeyCode> ToggleCameraKey = null!;
    internal static ConfigEntry<KeyCode> TogglePrecisionMovementKey = null!;
    internal static ConfigEntry<KeyCode> LeftShoulderKey = null!;
    internal static ConfigEntry<KeyCode> RightShoulderKey = null!;
    internal static ConfigEntry<KeyCode> CameraDistanceCloserKey = null!;
    internal static ConfigEntry<KeyCode> CameraDistanceFartherKey = null!;

    internal static ConfigEntry<float> BuildFov = null!;
    internal static ConfigEntry<float> NearClip = null!;
    internal static ConfigEntry<float> CameraTransitionSpeed = null!;

    internal static ConfigEntry<bool> EnableScrollDistanceAdjust = null!;
    internal static ConfigEntry<float> DefaultBuildCameraDistance = null!;
    internal static ConfigEntry<float> MinBuildCameraDistance = null!;
    internal static ConfigEntry<float> MaxBuildCameraDistance = null!;
    internal static ConfigEntry<float> ScrollDistanceStep = null!;
    internal static ConfigEntry<bool> RememberScrollDistance = null!;

    internal static ConfigEntry<float> ShoulderOffsetX = null!;
    internal static ConfigEntry<float> ShoulderOffsetY = null!;
    internal static ConfigEntry<float> ShoulderDistance = null!;
    internal static ConfigEntry<float> CollisionRadius = null!;
    internal static ConfigEntry<bool> ToggleShoulderPeek = null!;

    internal static ConfigEntry<bool> EnablePrecisionMovement = null!;
    internal static ConfigEntry<bool> PrecisionMovementDefaultOn = null!;
    internal static ConfigEntry<float> PrecisionMoveMultiplier = null!;

    internal static ConfigEntry<bool> HideLocalPlayerWhenImmersive = null!;

    internal static ConfigEntry<bool> EnableDebugLogs = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;

        ToggleCameraKey = Config.Bind(
            "Input",
            "ToggleCameraKey",
            KeyCode.LeftAlt,
            "Press this while using a build tool to toggle immersive build camera."
        );

        TogglePrecisionMovementKey = Config.Bind(
            "Input",
            "TogglePrecisionMovementKey",
            KeyCode.LeftControl,
            "Press this while immersive build camera is active to toggle slow precision movement."
        );

        LeftShoulderKey = Config.Bind(
            "Input",
            "LeftShoulderKey",
            KeyCode.Q,
            "Hold or press this while immersive build camera is active to peek left, depending on ToggleShoulderPeek."
        );

        RightShoulderKey = Config.Bind(
            "Input",
            "RightShoulderKey",
            KeyCode.E,
            "Hold or press this while immersive build camera is active to peek right, depending on ToggleShoulderPeek."
        );

        CameraDistanceCloserKey = Config.Bind(
            "Input",
            "CameraDistanceCloserKey",
            KeyCode.PageUp,
            "Press this while immersive build camera is active to move the camera closer."
        );

        CameraDistanceFartherKey = Config.Bind(
            "Input",
            "CameraDistanceFartherKey",
            KeyCode.PageDown,
            "Press this while immersive build camera is active to move the camera farther away."
        );

        BuildFov = Config.Bind(
            "Camera",
            "BuildFov",
            68f,
            "Field of view while immersive build camera is active."
        );

        NearClip = Config.Bind(
            "Camera",
            "NearClip",
            0.04f,
            "Near clipping plane while immersive build camera is active."
        );

        CameraTransitionSpeed = Config.Bind(
            "Camera",
            "CameraTransitionSpeed",
            12f,
            "How quickly the immersive build camera moves toward its target. Set to 0 for instant movement."
        );

        EnableScrollDistanceAdjust = Config.Bind(
            "Camera Distance",
            "EnableScrollDistanceAdjust",
            false,
            "Legacy opt-in. If true, mouse wheel also adjusts camera distance, but this can conflict with Valheim build piece rotation."
        );

        DefaultBuildCameraDistance = Config.Bind(
            "Camera Distance",
            "DefaultBuildCameraDistance",
            0f,
            "Starting backward camera distance from the player's eye when immersive build camera turns on."
        );

        MinBuildCameraDistance = Config.Bind(
            "Camera Distance",
            "MinBuildCameraDistance",
            0f,
            "Closest allowed camera distance."
        );

        MaxBuildCameraDistance = Config.Bind(
            "Camera Distance",
            "MaxBuildCameraDistance",
            1.25f,
            "Farthest allowed camera distance."
        );

        ScrollDistanceStep = Config.Bind(
            "Camera Distance",
            "ScrollDistanceStep",
            0.05f,
            "Distance changed per camera distance key press or mouse wheel notch."
        );

        RememberScrollDistance = Config.Bind(
            "Camera Distance",
            "RememberScrollDistance",
            false,
            "If true, keeps the adjusted camera distance between immersive build camera sessions."
        );

        ShoulderOffsetX = Config.Bind(
            "Shoulder Peek",
            "ShoulderOffsetX",
            0.90f,
            "Horizontal shoulder offset. Higher values make shoulder peek more useful but more likely to hit collision."
        );

        ShoulderOffsetY = Config.Bind(
            "Shoulder Peek",
            "ShoulderOffsetY",
            0.06f,
            "Vertical shoulder offset."
        );

        ShoulderDistance = Config.Bind(
            "Shoulder Peek",
            "ShoulderDistance",
            0.55f,
            "Backward shoulder camera distance."
        );

        CollisionRadius = Config.Bind(
            "Shoulder Peek",
            "CollisionRadius",
            0.10f,
            "Sphere radius used to prevent shoulder peek camera clipping into objects."
        );

        ToggleShoulderPeek = Config.Bind(
            "Shoulder Peek",
            "ToggleShoulderPeek",
            false,
            "If false, shoulder peek keys must be held. If true, shoulder peek keys toggle left, right, or centered."
        );

        EnablePrecisionMovement = Config.Bind(
            "Movement",
            "EnablePrecisionMovement",
            true,
            "Allow slow precision movement while immersive build camera is active."
        );

        PrecisionMovementDefaultOn = Config.Bind(
            "Movement",
            "PrecisionMovementDefaultOn",
            true,
            "Whether slow precision movement starts enabled whenever immersive build camera is toggled on."
        );

        PrecisionMoveMultiplier = Config.Bind(
            "Movement",
            "PrecisionMoveMultiplier",
            0.35f,
            "Movement input multiplier when precision movement is enabled. Lower means slower."
        );

        HideLocalPlayerWhenImmersive = Config.Bind(
            "Local Visibility",
            "HideLocalPlayerWhenImmersive",
            true,
            "Hide only the local player's renderers while immersive build camera is active and shoulder peek is not being used."
        );

        EnableDebugLogs = Config.Bind(
            "Debug",
            "EnableDebugLogs",
            false,
            "Enable extra logging for camera, shoulder peek, precision movement, visibility, and cleanup state changes."
        );

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        PrecisionMovementPatches.Apply(_harmony);

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    internal static void DebugLog(string message)
    {
        if (EnableDebugLogs?.Value == true)
            Log.LogInfo($"[Debug] {message}");
    }

    private void OnDestroy()
    {
        BuildCameraState.ForceInactive();
        PlayerRendererVisibility.ForceVisible();
        _harmony?.UnpatchSelf();
    }
}
