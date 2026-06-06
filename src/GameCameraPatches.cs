using HarmonyLib;
using UnityEngine;

namespace ImmersiveBuildCamera;

[HarmonyPatch(typeof(GameCamera))]
[HarmonyPatch("UpdateCamera")]
internal static class GameCameraUpdatePatch
{
    private static readonly System.Reflection.FieldInfo? CameraField =
        AccessTools.Field(typeof(GameCamera), "m_camera");

    private static float _originalFov;
    private static float _originalNearClip;
    private static bool _savedOriginals;

    private static Vector3 _smoothPosition;
    private static Quaternion _smoothRotation = Quaternion.identity;
    private static bool _hasSmoothTransform;

    private static int _cachedCollisionMask = -1;

    private static void Postfix(GameCamera __instance)
    {
        if (__instance == null)
            return;

        Camera? camera = GetCamera(__instance) ?? Camera.main;

        if (camera == null)
            return;

        if (!_savedOriginals)
        {
            _originalFov = camera.fieldOfView;
            _originalNearClip = camera.nearClipPlane;
            _savedOriginals = true;
        }

        if (!BuildCameraState.Active)
        {
            RestoreCamera(camera);
            ResetSmoothing();
            return;
        }

        ApplyImmersiveBuildCamera(__instance, camera);
    }

    private static Camera? GetCamera(GameCamera gameCamera)
    {
        if (CameraField == null)
            return null;

        return CameraField.GetValue(gameCamera) as Camera;
    }

    private static void ApplyImmersiveBuildCamera(GameCamera gameCamera, Camera camera)
    {
        Player? player = Player.m_localPlayer;

        if (player == null)
            return;

        Transform eye = player.m_eye != null
            ? player.m_eye
            : player.transform;

        Vector3 anchorPosition = eye.position;
        Vector3 desiredPosition = anchorPosition;
        Quaternion desiredRotation = eye.rotation;

        float cameraDistance = Mathf.Max(0f, BuildCameraDistance.Current);

        if (cameraDistance > 0.001f)
            desiredPosition -= eye.forward * cameraDistance;

        int shoulderDirection = BuildCameraState.GetShoulderDirection();

        if (shoulderDirection != 0)
        {
            float sideOffset = Plugin.ShoulderOffsetX.Value * shoulderDirection;

            desiredPosition += eye.right * sideOffset;
            desiredPosition += eye.up * Plugin.ShoulderOffsetY.Value;
            desiredPosition -= eye.forward * Plugin.ShoulderDistance.Value;

            desiredPosition = ResolveCameraCollision(anchorPosition, desiredPosition);
        }
        else if (cameraDistance > 0.001f)
        {
            desiredPosition = ResolveCameraCollision(anchorPosition, desiredPosition);
        }

        ApplySmoothedTransform(gameCamera, desiredPosition, desiredRotation);

        camera.fieldOfView = Plugin.BuildFov.Value;
        camera.nearClipPlane = Plugin.NearClip.Value;
    }

    private static void ApplySmoothedTransform(GameCamera gameCamera, Vector3 desiredPosition, Quaternion desiredRotation)
    {
        if (!_hasSmoothTransform)
        {
            _smoothPosition = gameCamera.transform.position;
            _smoothRotation = gameCamera.transform.rotation;
            _hasSmoothTransform = true;
        }

        float speed = Mathf.Max(0f, Plugin.CameraTransitionSpeed.Value);
        float lerp = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

        _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, lerp);
        _smoothRotation = Quaternion.Slerp(_smoothRotation, desiredRotation, lerp);

        gameCamera.transform.position = _smoothPosition;
        gameCamera.transform.rotation = _smoothRotation;
    }

    private static Vector3 ResolveCameraCollision(Vector3 anchorPosition, Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - anchorPosition;
        float distance = offset.magnitude;

        if (distance <= 0.001f)
            return desiredPosition;

        Vector3 direction = offset / distance;

        bool hitSomething = Physics.SphereCast(
            anchorPosition,
            Mathf.Max(0.01f, Plugin.CollisionRadius.Value),
            direction,
            out RaycastHit hit,
            distance,
            GetCollisionMask(),
            QueryTriggerInteraction.Ignore
        );

        if (!hitSomething)
            return desiredPosition;

        float safeDistance = Mathf.Max(0f, hit.distance - Plugin.CollisionRadius.Value);
        return anchorPosition + direction * safeDistance;
    }

    private static int GetCollisionMask()
    {
        if (_cachedCollisionMask != -1)
            return _cachedCollisionMask;

        int mask = LayerMask.GetMask(
            "Default",
            "static_solid",
            "terrain",
            "piece",
            "piece_nonsolid"
        );

        if (mask == 0)
        {
            mask = Physics.DefaultRaycastLayers;
            Plugin.Log.LogWarning("Could not resolve Valheim-specific collision layers. Falling back to Physics.DefaultRaycastLayers.");
        }

        _cachedCollisionMask = mask;
        return _cachedCollisionMask;
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
}
