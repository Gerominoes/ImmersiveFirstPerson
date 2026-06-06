using HarmonyLib;

namespace ImmersiveBuildCamera;

[HarmonyPatch(typeof(Player))]
[HarmonyPatch("Update")]
internal static class PlayerUpdatePatch
{
    private static void Postfix(Player __instance)
    {
        BuildCameraState.Update(__instance);
        PlayerRendererVisibility.Update(__instance);
    }
}