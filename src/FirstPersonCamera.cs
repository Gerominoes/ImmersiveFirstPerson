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
    private static readonly System.Reflection.FieldInfo? CameraField =
        AccessTools.Field(typeof(GameCamera), "m_camera");

    private static Camera? _lastCamera;
    private static float _originalFov;
    private static float _originalNearClip;
    private static bool _savedOriginals;

    private static Vector3 _smoothPosition;
    private static bool _hasSmoothPosition;

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
            ResetSmoothing();
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

        ResetSmoothing();
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
        Transform eye = player.m_eye != null
            ? player.m_eye
            : player.transform;

        Vector3 desiredPosition = eye.position;
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

        if (IsCrouchingOrSneaking(player))
            desiredPosition += Vector3.up * Plugin.CrouchVerticalOffset.Value;

        ApplyTransform(gameCamera, camera, desiredPosition, vanillaCameraRotation);

        if (Plugin.UseCustomFov.Value)
            camera.fieldOfView = Mathf.Clamp(Plugin.Fov.Value, 40f, 120f);

        camera.nearClipPlane = Mathf.Clamp(Plugin.NearClip.Value, 0.005f, 0.5f);
    }

    private static bool IsCrouchingOrSneaking(Player player)
    {
        return player.InDodge() == false && player.IsCrouching();
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
            ResetSmoothing();
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

    private static void ResetSmoothing()
    {
        _hasSmoothPosition = false;
        _smoothPosition = Vector3.zero;
    }
}
