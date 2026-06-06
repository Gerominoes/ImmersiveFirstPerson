using HarmonyLib;

namespace ImmersiveFirstPerson;

[HarmonyPatch(typeof(Player))]
[HarmonyPatch("Update")]
internal static class PlayerUpdatePatch
{
    private static void Postfix(Player __instance)
    {
        FirstPersonState.Update(__instance);
        HeadVisibilityController.Update(__instance);
    }
}
