using HarmonyLib;
using UnityEngine;

namespace ImmersiveBuildCamera;

internal static class PrecisionMovementPatches
{
    internal static void Apply(Harmony harmony)
    {
        System.Reflection.MethodInfo? original =
            AccessTools.Method(typeof(Player), "SetControls");

        System.Reflection.MethodInfo? prefix =
            AccessTools.Method(typeof(PrecisionMovementPatches), nameof(PrefixSetControls));

        if (original == null)
        {
            Plugin.Log.LogWarning("Could not find Player.SetControls. Precision movement patch skipped.");
            return;
        }

        if (prefix == null)
        {
            Plugin.Log.LogWarning("Could not find precision movement prefix.");
            return;
        }

        harmony.Patch(original, prefix: new HarmonyMethod(prefix));
        Plugin.Log.LogInfo("Patched Player.SetControls for precision movement.");
    }

    private static void PrefixSetControls(Player __instance, ref Vector3 movedir, ref bool run, ref bool autoRun)
    {
        if (!Plugin.EnablePrecisionMovement.Value)
            return;

        if (!BuildCameraState.Active)
            return;

        if (!BuildCameraState.PrecisionMovementActive)
            return;

        if (__instance != Player.m_localPlayer)
            return;

        float multiplier = Mathf.Clamp(
            Plugin.PrecisionMoveMultiplier.Value,
            0.05f,
            1f
        );

        movedir *= multiplier;
        run = false;
        autoRun = false;
    }
}