using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

[HarmonyPatch(typeof(Player))]
[HarmonyPatch("SetControls")]
internal static class FirstPersonMovementPatch
{
    private static void Prefix(Player __instance, ref Vector3 movedir)
    {
        if (__instance == null || __instance != Player.m_localPlayer)
            return;

        if (!Plugin.EnableMod.Value || !FirstPersonState.Active)
            return;

        if (!Plugin.UseRawMouseLook.Value || !FirstPersonCamera.HasRawLook)
            return;

        if (movedir.sqrMagnitude <= 0.0001f)
            return;

        Vector3 localMove = Vector3.ClampMagnitude(movedir, 1f);
        Vector3 cameraRelativeMove = FirstPersonCamera.CurrentYawRotation * localMove;
        cameraRelativeMove.y = movedir.y;

        movedir = Vector3.ClampMagnitude(cameraRelativeMove, 1f);
    }
}
