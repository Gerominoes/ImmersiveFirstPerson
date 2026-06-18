using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class DodgeDirectionController
{
    private const float DodgeQueueBodyLockSuppressSeconds = 0.75f;

    private static Player? _suppressedPlayer;
    private static float _suppressBodyLockUntil;

    internal static void PrepareDodge(Player player, ref Vector3 dodgeDir)
    {
        if (!ShouldHandleFirstPersonDodge(player))
            return;

        if (Plugin.DodgeWhereYouLook.Value)
        {
            // Camera-direction dodge mode intentionally ignores held movement input.
            if (TryGetFlatLookDirection(player, out Vector3 lookDirection))
                dodgeDir = lookDirection;

            return;
        }

        // Vanilla-direction dodge mode keeps body yaw unlocked while the queued dodge starts.
        _suppressedPlayer = player;
        _suppressBodyLockUntil = Time.unscaledTime + DodgeQueueBodyLockSuppressSeconds;
    }

    internal static bool ShouldSkipBodyYawLock(Player player)
    {
        if (!ShouldHandleFirstPersonDodge(player) || Plugin.DodgeWhereYouLook.Value)
            return false;

        if (player.InDodge())
            return true;

        if (_suppressedPlayer == player && Time.unscaledTime <= _suppressBodyLockUntil)
            return true;

        if (_suppressedPlayer == player)
            ClearSuppression();

        return false;
    }

    private static bool ShouldHandleFirstPersonDodge(Player player)
    {
        return player != null &&
               player == Player.m_localPlayer &&
               Plugin.EnableMod.Value &&
               FirstPersonState.ShouldApplyCamera(player);
    }

    private static bool TryGetFlatLookDirection(Player player, out Vector3 lookDirection)
    {
        // The player eye tracks vanilla mouse look, which is the same basis used by the camera.
        lookDirection = player.GetLookDir();
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.0001f && GameCamera.instance != null)
        {
            lookDirection = GameCamera.instance.transform.forward;
            lookDirection.y = 0f;
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
            return false;

        lookDirection.Normalize();
        return true;
    }

    private static void ClearSuppression()
    {
        _suppressedPlayer = null;
        _suppressBodyLockUntil = 0f;
    }
}

[HarmonyPatch(typeof(Player), "Dodge")]
internal static class PlayerDodgeDirectionPatch
{
    private static void Prefix(Player __instance, ref Vector3 dodgeDir)
    {
        // This patch runs after vanilla has selected the directional input vector.
        DodgeDirectionController.PrepareDodge(__instance, ref dodgeDir);
    }
}
