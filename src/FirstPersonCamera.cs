using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

[HarmonyPatch(typeof(GameCamera))]
[HarmonyPatch("UpdateCamera")]
internal static class GameCameraUpdatePatch
{
    private static void Postfix(GameCamera __instance)
    {
        FirstPersonCamera.Update(__instance);
    }
}

internal static class FirstPersonCamera
{
    private const float HeadBobFilterSpeed = 3f;

    private static readonly string[] CameraEffectTypeNameFragments =
    {
        "PostProcessLayer",
        "PostProcessVolume",
        "Bloom",
        "DepthOfField",
        "MotionBlur",
        "ScreenSpaceAmbientOcclusion",
        "SunShafts",
        "Vignette"
    };

    private static readonly FieldInfo? CameraField =
        AccessTools.Field(typeof(GameCamera), "m_camera");

    private static readonly MethodInfo? IsCrouchingMethod =
        FindInstanceMethod(typeof(Player), "IsCrouching") ??
        FindInstanceMethod(typeof(Character), "IsCrouching");

    private static readonly MethodInfo? InSneakMethod =
        FindInstanceMethod(typeof(Player), "InSneak") ??
        FindInstanceMethod(typeof(Character), "InSneak");

    private static readonly FieldInfo? CrouchField =
        AccessTools.Field(typeof(Player), "m_crouchToggled") ??
        AccessTools.Field(typeof(Player), "m_crouch") ??
        AccessTools.Field(typeof(Character), "m_crouch");

    // Camera defaults are captured on first-person entry and restored on exit.
    private static Camera? _lastCamera;
    private static Camera? _savedCamera;
    private static Player? _cachedAnchorPlayer;
    private static Transform? _cachedHeadAnchor;
    private static float _originalFov;
    private static float _originalNearClip;
    private static bool _originalUseOcclusionCulling;
    private static bool _savedOriginals;
    private static bool _loggedRenderingState;

    // Shadow defaults are global Unity settings, so they must be restored explicitly.
    private static float _originalShadowDistance;
    private static int _originalShadowCascades;

    // Camera effect components are restored to their previous enabled state.
    private static readonly Dictionary<Behaviour, bool> CameraEffectStates = new();

    // Head tracking state smooths high-frequency head animation.
    private static Player? _filteredHeadPlayer;
    private static Vector3 _filteredLocalHeadPosition;
    private static bool _hasFilteredHeadPosition;

    // Attached camera state keeps seats, ships, and hold-fast points stable.
    private static Player? _attachedCameraPlayer;
    private static Vector3 _attachedLocalCameraPosition;
    private static bool _hasAttachedCameraPosition;

    internal static void Update(GameCamera gameCamera)
    {
        if (gameCamera == null)
            return;

        Camera? camera = GetCamera(gameCamera) ?? Camera.main;

        if (camera == null)
            return;

        _lastCamera = camera;

        Player? player = Player.m_localPlayer;

        if (player == null || !FirstPersonState.ShouldApplyCamera(player))
        {
            RestoreCamera(camera);
            ResetHeadBobFilter();
            ResetAttachedCameraLock();
            RestoreLocalVisibilityForSuppressedCamera();
            return;
        }

        Quaternion vanillaCameraRotation = gameCamera.transform.rotation;
        bool lockAttachedCamera = ShouldLockAttachedCamera(player);

        if (!lockAttachedCamera)
            ResetAttachedCameraLock();

        if (Plugin.LockBodyToCamera.Value && !lockAttachedCamera)
            LockBodyYawToCamera(player, vanillaCameraRotation);

        SaveOriginalCameraValues(camera);
        ApplyFirstPersonCamera(gameCamera, camera, player, vanillaCameraRotation, lockAttachedCamera);

        LocalPlayerVisibilityOverride.ForceVisible(player);
        BodyVisibilityController.Update(player);
        HeadVisibilityController.Update(player);
    }

    internal static void RestoreLastCamera()
    {
        if (_lastCamera != null)
            RestoreCamera(_lastCamera);

        ResetHeadBobFilter();
        ResetAttachedCameraLock();
        ResetAnchorCache();
    }

    private static Camera? GetCamera(GameCamera gameCamera)
    {
        if (CameraField == null)
            return null;

        return CameraField.GetValue(gameCamera) as Camera;
    }

    private static MethodInfo? FindInstanceMethod(Type type, string name)
    {
        // Optional Valheim methods vary by version, so missing methods should not warn.
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void SaveOriginalCameraValues(Camera camera)
    {
        if (_savedOriginals && _savedCamera == camera)
            return;

        if (_savedOriginals && _savedCamera != null)
            RestoreCamera(_savedCamera);

        // Capture camera state that the first-person renderer overrides.
        _savedCamera = camera;
        _originalFov = camera.fieldOfView;
        _originalNearClip = camera.nearClipPlane;
        _originalUseOcclusionCulling = camera.useOcclusionCulling;

        // Capture global shadow state changed for first-person performance.
        _originalShadowDistance = QualitySettings.shadowDistance;
        _originalShadowCascades = QualitySettings.shadowCascades;

        _savedOriginals = true;
        LogRenderingState();
    }

    private static void LogRenderingState()
    {
        if (_loggedRenderingState)
            return;

        _loggedRenderingState = true;

        // Runtime logging confirms the first-person path preserves scene range and LOD.
        Plugin.Log.LogInfo($"First-person rendering state: view distance unchanged; LOD unchanged; originalNearClip={_originalNearClip:0.###}; requestedNearClip={Plugin.NearClip.Value:0.###}; originalShadowDistance={_originalShadowDistance:0.###}; shadowDistanceCap={Plugin.FirstPersonShadowDistance.Value:0.###}; originalShadowCascades={_originalShadowCascades}; shadowCascadesCap={Plugin.FirstPersonShadowCascades.Value}; originalOcclusionCulling={_originalUseOcclusionCulling}; requestedOcclusionCulling={Plugin.UseOcclusionCulling.Value}; cameraEffectsDisabled={Plugin.DisableCameraEffects.Value}.");
    }

    private static void RestoreLocalVisibilityForSuppressedCamera()
    {
        if (!FirstPersonState.Active)
            return;

        BodyVisibilityController.Reset();
        HeadVisibilityController.ForceVisible();
    }

    private static bool ShouldLockAttachedCamera(Player player)
    {
        return Plugin.LockCameraWhileAttached.Value && player.IsAttached();
    }

    private static void LockBodyYawToCamera(Player player, Quaternion cameraRotation)
    {
        Vector3 forward = cameraRotation * Vector3.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        float speed = Mathf.Max(0f, Plugin.BodyRotationFollowSpeed.Value);

        player.transform.rotation = speed <= 0f
            ? targetRotation
            : Quaternion.Slerp(player.transform.rotation, targetRotation, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    private static void ApplyFirstPersonCamera(GameCamera gameCamera, Camera camera, Player player, Quaternion vanillaCameraRotation, bool lockAttachedCamera)
    {
        if (lockAttachedCamera)
        {
            ApplyAttachedCamera(gameCamera, camera, player, vanillaCameraRotation);
            return;
        }

        Transform anchor = GetCameraAnchor(player);
        bool hasHeadAnchor = anchor != null && Plugin.UseHeadTrackedAnchor.Value && anchor != player.m_eye;
        bool isCrouchingOrSneaking = IsCrouchingOrSneaking(player);

        Vector3 desiredPosition = GetHeadBobScaledAnchorPosition(player, anchor, hasHeadAnchor);
        desiredPosition += Vector3.up * Plugin.CameraVerticalOffset.Value;

        Vector3 flatForward = vanillaCameraRotation * Vector3.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude > 0.0001f)
            desiredPosition += flatForward.normalized * Plugin.CameraForwardOffset.Value;

        float downLookAmount = Mathf.Clamp01(Vector3.Dot(vanillaCameraRotation * Vector3.forward, Vector3.down));

        if (downLookAmount > 0f)
        {
            if (flatForward.sqrMagnitude > 0.0001f)
                desiredPosition += flatForward.normalized * Plugin.DownLookExtraForwardOffset.Value * downLookAmount;

            desiredPosition += Vector3.up * Plugin.DownLookExtraVerticalOffset.Value * downLookAmount;
        }

        if (!hasHeadAnchor && isCrouchingOrSneaking)
            desiredPosition += Vector3.up * Plugin.CrouchVerticalOffset.Value;

        ApplyCameraState(gameCamera, camera, desiredPosition, vanillaCameraRotation);
    }

    private static void ApplyAttachedCamera(GameCamera gameCamera, Camera camera, Player player, Quaternion vanillaCameraRotation)
    {
        ResetHeadBobFilter();

        Quaternion bodyRotation = player.transform.rotation;
        Quaternion limitedRotation = GetLimitedAttachedCameraRotation(bodyRotation, vanillaCameraRotation);
        Vector3 desiredPosition = GetAttachedCameraPosition(player, bodyRotation);

        ApplyCameraState(gameCamera, camera, desiredPosition, limitedRotation);
    }

    private static Vector3 GetAttachedCameraPosition(Player player, Quaternion bodyRotation)
    {
        if (_attachedCameraPlayer != player)
            ResetAttachedCameraLock();

        if (!_hasAttachedCameraPosition)
        {
            _attachedCameraPlayer = player;
            _attachedLocalCameraPosition = CaptureAttachedLocalCameraPosition(player, bodyRotation);
            _hasAttachedCameraPosition = true;
            Plugin.DebugLog($"Captured attached camera local position: {_attachedLocalCameraPosition}");
        }

        Vector3 localOffset = _attachedLocalCameraPosition;
        localOffset += Vector3.up * Plugin.AttachedCameraExtraVerticalOffset.Value;
        localOffset += Vector3.forward * Plugin.AttachedCameraExtraForwardOffset.Value;
        return player.transform.TransformPoint(localOffset);
    }

    private static Vector3 CaptureAttachedLocalCameraPosition(Player player, Quaternion bodyRotation)
    {
        Transform anchor = GetCameraAnchor(player);
        Vector3 worldPosition = anchor != null ? anchor.position : GetFallbackEyePosition(player);
        Vector3 flatForward = bodyRotation * Vector3.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude > 0.0001f)
            worldPosition += flatForward.normalized * Plugin.CameraForwardOffset.Value;

        worldPosition += Vector3.up * Plugin.CameraVerticalOffset.Value;
        return player.transform.InverseTransformPoint(worldPosition);
    }

    private static Vector3 GetFallbackEyePosition(Player player)
    {
        if (player.m_eye != null)
            return player.m_eye.position;

        return player.transform.position + Vector3.up * 1.6f;
    }

    private static Quaternion GetLimitedAttachedCameraRotation(Quaternion bodyRotation, Quaternion vanillaCameraRotation)
    {
        Quaternion localLook = Quaternion.Inverse(bodyRotation) * vanillaCameraRotation;
        Vector3 localEuler = NormalizeEuler(localLook.eulerAngles);
        float maxYaw = Mathf.Clamp(Plugin.AttachedCameraMaxYaw.Value, 0f, 180f);
        float maxPitch = Mathf.Clamp(Plugin.AttachedCameraMaxPitch.Value, 1f, 89f);
        float yaw = Mathf.Clamp(localEuler.y, -maxYaw, maxYaw);
        float pitch = Mathf.Clamp(localEuler.x, -maxPitch, maxPitch);

        return bodyRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
            angle -= 360f;
        else if (angle < -180f)
            angle += 360f;

        return angle;
    }

    private static Vector3 GetHeadBobScaledAnchorPosition(Player player, Transform? anchor, bool hasHeadAnchor)
    {
        if (anchor == null)
        {
            ResetHeadBobFilter();
            return GetFallbackEyePosition(player);
        }

        if (!hasHeadAnchor)
        {
            ResetHeadBobFilter();
            return anchor.position;
        }

        float headBobAmount = Mathf.Clamp01(Plugin.HeadBobAmount.Value);
        Vector3 animatedLocalHeadPosition = player.transform.InverseTransformPoint(anchor.position);

        if (_filteredHeadPlayer != player)
            ResetHeadBobFilter();

        if (!_hasFilteredHeadPosition)
        {
            _filteredHeadPlayer = player;
            _filteredLocalHeadPosition = animatedLocalHeadPosition;
            _hasFilteredHeadPosition = true;
        }

        float lerp = 1f - Mathf.Exp(-HeadBobFilterSpeed * Time.unscaledDeltaTime);
        _filteredLocalHeadPosition = Vector3.Lerp(_filteredLocalHeadPosition, animatedLocalHeadPosition, lerp);

        if (headBobAmount >= 0.999f)
            return anchor.position;

        Vector3 fastLocalHeadMotion = animatedLocalHeadPosition - _filteredLocalHeadPosition;
        Vector3 finalLocalHeadPosition = _filteredLocalHeadPosition + fastLocalHeadMotion * headBobAmount;
        return player.transform.TransformPoint(finalLocalHeadPosition);
    }

    private static Transform GetCameraAnchor(Player player)
    {
        if (!Plugin.UseHeadTrackedAnchor.Value)
            return player.m_eye != null ? player.m_eye : player.transform;

        if (_cachedAnchorPlayer == player && _cachedHeadAnchor != null)
            return _cachedHeadAnchor;

        _cachedAnchorPlayer = player;
        _cachedHeadAnchor = FindBestHeadAnchor(player);

        if (_cachedHeadAnchor != null)
        {
            Plugin.DebugLog($"Using head-tracked camera anchor: {RendererScanner.GetPath(_cachedHeadAnchor)}");
            return _cachedHeadAnchor;
        }

        return player.m_eye != null ? player.m_eye : player.transform;
    }

    private static Transform? FindBestHeadAnchor(Player player)
    {
        Transform[] transforms = player.GetComponentsInChildren<Transform>(true);
        Transform? best = null;
        int bestScore = int.MinValue;

        foreach (Transform transform in transforms)
        {
            if (transform == null)
                continue;

            int score = ScoreHeadAnchor(transform);

            if (score > bestScore)
            {
                bestScore = score;
                best = transform;
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static int ScoreHeadAnchor(Transform transform)
    {
        string name = transform.name.ToLowerInvariant();
        string path = RendererScanner.GetPath(transform).ToLowerInvariant();
        string descriptor = name + " " + path;

        if (!descriptor.Contains("head"))
            return int.MinValue;

        if (descriptor.Contains("helmet") || descriptor.Contains("helm") || descriptor.Contains("hair") || descriptor.Contains("beard"))
            return int.MinValue;

        int score = 10;

        if (name == "head")
            score += 100;

        if (name.Contains("head"))
            score += 40;

        if (path.Contains("neck"))
            score += 10;

        if (transform.GetComponent<Renderer>() != null)
            score -= 50;

        return score;
    }

    private static bool IsCrouchingOrSneaking(Player player)
    {
        if (TryInvokeBool(player, IsCrouchingMethod, out bool isCrouching) && isCrouching)
            return true;

        if (TryInvokeBool(player, InSneakMethod, out bool inSneak) && inSneak)
            return true;

        if (CrouchField != null && CrouchField.GetValue(player) is bool crouchFieldValue)
            return crouchFieldValue;

        return false;
    }

    private static bool TryInvokeBool(Player player, MethodInfo? method, out bool value)
    {
        value = false;

        if (method == null)
            return false;

        object? result = method.Invoke(player, null);

        if (result is not bool boolResult)
            return false;

        value = boolResult;
        return true;
    }

    private static void ApplyCameraState(GameCamera gameCamera, Camera camera, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        gameCamera.transform.position = desiredPosition;
        gameCamera.transform.rotation = desiredRotation;

        if (camera.transform != gameCamera.transform)
        {
            camera.transform.position = desiredPosition;
            camera.transform.rotation = desiredRotation;
        }

        if (Plugin.UseCustomFov.Value)
            camera.fieldOfView = Mathf.Clamp(Plugin.Fov.Value, 40f, 120f);
        else
            camera.fieldOfView = _originalFov;

        ApplyFirstPersonRendering(camera);
    }

    private static void ApplyFirstPersonRendering(Camera camera)
    {
        // Keep the near clip tight without changing first-person view distance.
        camera.nearClipPlane = Mathf.Clamp(Plugin.NearClip.Value, 0.005f, 0.5f);
        camera.useOcclusionCulling = Plugin.UseOcclusionCulling.Value;

        // Shadow settings reduce lighting cost without changing visible object distance.
        ApplyShadowOverrides();

        // Camera effects are optional because some players prefer the visual tradeoff.
        ApplyCameraEffectOverrides(camera);
    }

    private static void ApplyShadowOverrides()
    {
        if (Plugin.FirstPersonShadowDistance.Value >= 0f)
        {
            float requestedShadowDistance = Mathf.Max(0f, Plugin.FirstPersonShadowDistance.Value);
            QualitySettings.shadowDistance = Mathf.Min(_originalShadowDistance, requestedShadowDistance);
        }

        if (Plugin.FirstPersonShadowCascades.Value >= 0)
        {
            int requestedShadowCascades = NormalizeShadowCascades(Plugin.FirstPersonShadowCascades.Value);
            QualitySettings.shadowCascades = Mathf.Min(_originalShadowCascades, requestedShadowCascades);
        }
    }

    private static int NormalizeShadowCascades(int value)
    {
        if (value <= 0)
            return 0;

        if (value <= 2)
            return 2;

        return 4;
    }

    private static void ApplyCameraEffectOverrides(Camera camera)
    {
        if (!Plugin.DisableCameraEffects.Value)
        {
            RestoreCameraEffectStates();
            return;
        }

        foreach (Behaviour component in camera.GetComponents<Behaviour>())
        {
            if (component == null || !IsCameraEffectComponent(component))
                continue;

            if (!CameraEffectStates.ContainsKey(component))
                CameraEffectStates.Add(component, component.enabled);

            component.enabled = false;
        }
    }

    private static bool IsCameraEffectComponent(Behaviour component)
    {
        Type type = component.GetType();
        string typeName = type.FullName ?? type.Name;

        if (string.Equals(typeName, "UnityEngine.Rendering.Volume", StringComparison.Ordinal))
            return true;

        foreach (string fragment in CameraEffectTypeNameFragments)
        {
            if (typeName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static void RestoreCamera(Camera camera)
    {
        if (!_savedOriginals)
            return;

        Camera? targetCamera = _savedCamera != null ? _savedCamera : camera;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = _originalFov;
            targetCamera.nearClipPlane = _originalNearClip;
            targetCamera.useOcclusionCulling = _originalUseOcclusionCulling;
        }

        QualitySettings.shadowDistance = _originalShadowDistance;
        QualitySettings.shadowCascades = _originalShadowCascades;
        RestoreCameraEffectStates();

        _savedCamera = null;
        _savedOriginals = false;
    }

    private static void RestoreCameraEffectStates()
    {
        foreach (KeyValuePair<Behaviour, bool> effectState in CameraEffectStates)
        {
            if (effectState.Key != null)
                effectState.Key.enabled = effectState.Value;
        }

        CameraEffectStates.Clear();
    }

    private static void ResetHeadBobFilter()
    {
        _filteredHeadPlayer = null;
        _hasFilteredHeadPosition = false;
        _filteredLocalHeadPosition = Vector3.zero;
    }

    private static void ResetAttachedCameraLock()
    {
        _attachedCameraPlayer = null;
        _hasAttachedCameraPosition = false;
        _attachedLocalCameraPosition = Vector3.zero;
    }

    private static void ResetAnchorCache()
    {
        _cachedAnchorPlayer = null;
        _cachedHeadAnchor = null;
    }
}
