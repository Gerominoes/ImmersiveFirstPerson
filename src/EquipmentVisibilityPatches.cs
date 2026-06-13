using HarmonyLib;

namespace ImmersiveFirstPerson;

[HarmonyPatch(typeof(VisEquipment), "SetHelmetEquipped")]
internal static class VisEquipmentSetHelmetEquippedPatch
{
    private static void Prefix(VisEquipment __instance)
    {
        // The prefix captures renderers before Valheim replaces helmet visuals.
        HelmetShadowController.CaptureHelmetChangeStart(__instance);
    }

    private static void Postfix(VisEquipment __instance, bool __result)
    {
        // The postfix records newly-added helmet renderers and refreshes body hiding.
        HelmetShadowController.CaptureHelmetChangeEnd(__instance, __result);

        if (__result && PlayerEquipmentAccess.IsLocalPlayerVisEquipment(__instance))
            HeadVisibilityController.RequestRefresh();
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
internal static class VisEquipmentSetChestEquippedPatch
{
    private static void Postfix(VisEquipment __instance, bool __result)
    {
        // Chest changes can rebuild the body mesh that Blacksmith modifies.
        if (__result && PlayerEquipmentAccess.IsLocalPlayerVisEquipment(__instance))
            HeadVisibilityController.RequestRefresh();
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
internal static class VisEquipmentSetShoulderEquippedPatch
{
    private static void Postfix(VisEquipment __instance, bool __result)
    {
        // Shoulder visuals can add skinned renderers near the head, so refresh the cache.
        if (__result && PlayerEquipmentAccess.IsLocalPlayerVisEquipment(__instance))
            HeadVisibilityController.RequestRefresh();
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
internal static class VisEquipmentSetLegEquippedPatch
{
    private static void Postfix(VisEquipment __instance, bool __result)
    {
        // Leg changes can rebuild the body mesh that Blacksmith modifies.
        if (__result && PlayerEquipmentAccess.IsLocalPlayerVisEquipment(__instance))
            HeadVisibilityController.RequestRefresh();
    }
}

[HarmonyPatch(typeof(VisEquipment), "SetModel")]
internal static class VisEquipmentSetModelPatch
{
    private static void Postfix(VisEquipment __instance)
    {
        // Model changes swap the underlying player mesh and invalidate body-part validation.
        if (PlayerEquipmentAccess.IsLocalPlayerVisEquipment(__instance))
            HeadVisibilityController.RequestRefresh();
    }
}

[HarmonyPatch(typeof(Player), "OnDestroy")]
internal static class PlayerOnDestroyVisibilityPatch
{
    private static void Prefix(Player __instance)
    {
        // Local player despawn must restore renderer, mesh, and Blacksmith dictionary state first.
        if (__instance != null && __instance == Player.m_localPlayer)
            FirstPersonState.ForceInactive();
    }
}
