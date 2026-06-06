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
    private static Quaternion _smoothRotation = Quaternion.identity;
    private static bool _hasSmoothTransform;

    private static float _rawPitch;
    private static float _rawYaw;
    private static bool _rawLookInitialized;

    internal static bool HasRawLook => Plugin.UseRawMouseLook.Value && _rawLookInitialized;
    internal static float CurrentYaw => _rawYaw;

    internal static Quaternion CurrentYawRotation => Quaternion.Euler(0f, _rawYaw, 0f);

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
            ResetRawLook();
            return;
        }

        ApplyFirstPersonCamera(gameCamera, camera, player);

        LocalPlayerVisibilityOverride.ForceVisible(player);
        BodyVisibilityController.Update(player);
        HeadVisibilityController.Update(player);
    }

    internal static void RestoreLastCamera()
    {
        if (_lastCamera != null)
            RestoreCamera(_lastCamera);

        ResetSmoothing();
        ResetRawLook();
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

    private static void ApplyFirstPersonCamera(GameCamera gameCamera, Camera camera, Player player)
    {
        Transform eye = player.m_eye != null
            ? player.m_eye
            : player.transform;

        Vector3 desiredPosition = eye.position;
        desiredPosition += eye.up * Plugin.CameraVerticalOffset.Value;
        desiredPosition += eye.forward * Plugin.CameraForwardOffset.Value;

        Quaternion desiredRotation = GetDesiredRotation(eye, player);

        ApplyTransform(gameCamera, camera, desiredPosition, desiredRotation);

        if (Plugin.UseCustomFov.Value)
            camera.fieldOfView = Mathf.Clamp(Plugin.Fov.Value, 40f, 120f);

        camera.nearClipPlane = Mathf.Clamp(Plugin.NearClip.Value, 0.005f, 0.5f);
    }

    private static Quaternion GetDesiredRotation(Transform eye, Player player)
    {
        if (!Plugin.UseRawMouseLook.Value)
            return eye.rotation;

        if (!_rawLookInitialized)
        {
            Vector3 euler = eye.rotation.eulerAngles;
            _rawPitch = NormalizePitch(euler.x);
            _rawYaw = euler.y;
            _rawLookInitialized = true;
        }

        float sensitivity = Mathf.Clamp(Plugin.RawMouseSensitivity.Value, 0.05f, 10f);
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        _rawYaw = Mathf.Repeat(_rawYaw + mouseX, 360f);
        _rawPitch = Mathf.Clamp(_rawPitch - mouseY, -89f, 89f);

        Quaternion yawRotation = Quaternion.Euler(0f, _rawYaw, 0f);
        player.transform.rotation = yawRotation;

        return Quaternion.Euler(_rawPitch, _rawYaw, 0f);
    }

    private static float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
            pitch -= 360f;

        return Mathf.Clamp(pitch, -89f, 89f);
    }

    private static void ApplyTransform(GameCamera gameCamera, Camera camera, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        Vector3 finalPosition = desiredPosition;
        Quaternion finalRotation = desiredRotation;

        if (Plugin.SmoothCamera.Value)
        {
            if (!_hasSmoothTransform)
            {
                _smoothPosition = gameCamera.transform.position;
                _smoothRotation = gameCamera.transform.rotation;
                _hasSmoothTransform = true;
            }

            float speed = Mathf.Max(0f, Plugin.CameraSmoothing.Value);
            float lerp = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, lerp);
            _smoothRotation = Quaternion.Slerp(_smoothRotation, desiredRotation, lerp);

            finalPosition = _smoothPosition;
            finalRotation = _smoothRotation;
        }
        else
        {
            ResetSmoothing();
        }

        gameCamera.transform.position = finalPosition;
        gameCamera.transform.rotation = finalRotation;

        if (camera.transform != gameCamera.transform)
        {
            camera.transform.position = finalPosition;
            camera.transform.rotation = finalRotation;
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
        _hasSmoothTransform = false;
        _smoothPosition = Vector3.zero;
        _smoothRotation = Quaternion.identity;
    }

    private static void ResetRawLook()
    {
        _rawLookInitialized = false;
        _rawPitch = 0f;
        _rawYaw = 0f;
    }
}
