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

    private static readonly FieldInfo? CameraField =
        AccessTools.Field(typeof(GameCamera), "m_camera");

    private static readonly MethodInfo? IsCrouchingMethod =
        AccessTools.Method(typeof(Player), "IsCrouching") ??
        AccessTools.Method(typeof(Character), "IsCrouching");

    private static readonly MethodInfo? InSneakMethod =
        AccessTools.Method(typeof(Player), "InSneak") ??
        AccessTools.Method(typeof(Character), "InSneak");

    private static readonly FieldInfo? CrouchField =
        AccessTools.Field(typeof(Player), "m_crouchToggled") ??
        AccessTools.Field(typeof(Player), "m_crouch") ??
        AccessTools.Field(typeof(Character), "m_crouch");

    private static Camera? _lastCamera;
    private static Player? _cachedAnchorPlayer;
    private static Transform? _cachedHeadAnchor;
    private static float _originalFov;
    private static float _originalNearClip;
    private static bool _savedOriginals;

    private static Vector3 _smoothPosition;
    private static bool _hasSmoothPosition;

    private static Player? _filteredHeadPlayer;
    private static Vector3 _filteredLocalHeadPosition;
    private static bool _hasFilteredHeadPosition;

    internal static void Update(GameCamera gameCamera)
    {
        if (gameCamera == null)
            return;

        Camera? camera = GetCamera(gameCamera) ?? Camera.main;

        if (camera == null)
            return;

        _lastCamera = camera;
        SaveOriginalCameraValues(camera);

        Player? player = Player.m_localPlayer;

        if (player == null || !FirstPersonState.ShouldApplyCamera(player))
        {
            RestoreCamera(camera);
            ResetPositionSmoothing();
            ResetHeadBobFilter();
            RestoreLocalVisibilityForSuppressedCamera();
            return;
        }

        Quaternion vanillaCameraRotation = gameCamera.transform.rotation;

        if (Plugin.LockBodyToCamera.Value)
            LockBodyYawToCamera(player, vanillaCameraRotation);

        ApplyFirstPersonCamera(gameCamera, camera, player, vanillaCameraRotation);

        LocalPlayerVisibilityOverride.ForceVisible(player);
        BodyVisibilityController.Update(player);
        HeadVisibilityController.Update(player);
    }

    internal static void RestoreLastCamera()
    {
        if (_lastCamera != null)
            RestoreCamera(_lastCamera);

        ResetPositionSmoothing();
        ResetHeadBobFilter();
        ResetAnchorCache();
    }

    private static Camera? GetCamera(GameCamera gameCamera)
    {
        if (CameraField == null)
            return null;

        return CameraField.GetValue(gameCamera) as Camera;
    }

    private static void SaveOriginalCameraValues(Camera camera)
    {
        if (_savedOriginals)
            return;

        _originalFov = camera.fieldOfView;
        _originalNearClip = camera.nearClipPlane;
        _savedOriginals = true;
    }

    private static void RestoreLocalVisibilityForSuppressedCamera()
    {
        if (!FirstPersonState.Active)
            return;

        BodyVisibilityController.Reset();
        HeadVisibilityController.ForceVisible();
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

    private static void ApplyFirstPersonCamera(GameCamera gameCamera, Camera camera, Player player, Quaternion vanillaCameraRotation)
    {
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

        ApplyTransform(gameCamera, camera, desiredPosition, vanillaCameraRotation);

        if (Plugin.UseCustomFov.Value)
            camera.fieldOfView = Mathf.Clamp(Plugin.Fov.Value, 40f, 120f);

        camera.nearClipPlane = Mathf.Clamp(Plugin.NearClip.Value, 0.005f, 0.5f);
    }

    private static Vector3 GetHeadBobScaledAnchorPosition(Player player, Transform anchor, bool hasHeadAnchor)
    {
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

    private static void ApplyTransform(GameCamera gameCamera, Camera camera, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        Vector3 finalPosition = desiredPosition;

        if (Plugin.SmoothCamera.Value)
        {
            if (!_hasSmoothPosition)
            {
                _smoothPosition = gameCamera.transform.position;
                _hasSmoothPosition = true;
            }

            float speed = Mathf.Max(0f, Plugin.CameraSmoothing.Value);
            float lerp = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, lerp);
            finalPosition = _smoothPosition;
        }
        else
        {
            ResetPositionSmoothing();
        }

        gameCamera.transform.position = finalPosition;
        gameCamera.transform.rotation = desiredRotation;

        if (camera.transform != gameCamera.transform)
        {
            camera.transform.position = finalPosition;
            camera.transform.rotation = desiredRotation;
        }
    }

    private static void RestoreCamera(Camera camera)
    {
        if (!_savedOriginals)
            return;

        camera.fieldOfView = _originalFov;
        camera.nearClipPlane = _originalNearClip;
    }

    private static void ResetPositionSmoothing()
    {
        _hasSmoothPosition = false;
        _smoothPosition = Vector3.zero;
    }

    private static void ResetHeadBobFilter()
    {
        _filteredHeadPlayer = null;
        _hasFilteredHeadPosition = false;
        _filteredLocalHeadPosition = Vector3.zero;
    }

    private static void ResetAnchorCache()
    {
        _cachedAnchorPlayer = null;
        _cachedHeadAnchor = null;
    }
}
