using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class LocalPlayerRendererOwnership
{
    internal static bool IsLocalPlayerRenderer(Player player, Renderer renderer)
    {
        if (renderer == null || renderer.transform == null)
            return false;

        if (!IsLocalPlayerTransform(player, renderer.transform))
            return false;

        // World-object owners are rejected unless their transform is inside the local player hierarchy.
        return !HasForeignWorldOwner(player, renderer.transform);
    }

    internal static bool IsLocalPlayerTransform(Player player, Transform transform)
    {
        return player != null &&
               player == Player.m_localPlayer &&
               player.transform != null &&
               transform != null &&
               (transform == player.transform || transform.IsChildOf(player.transform));
    }

    private static bool HasForeignWorldOwner(Player player, Transform transform)
    {
        // These components identify world foliage, terrain props, destructibles, and networked scene objects.
        return HasForeignOwner<Pickable>(player, transform) ||
               HasForeignOwner<Plant>(player, transform) ||
               HasForeignOwner<TreeBase>(player, transform) ||
               HasForeignOwner<Destructible>(player, transform) ||
               HasForeignOwner<MineRock>(player, transform) ||
               HasForeignOwner<MineRock5>(player, transform) ||
               HasForeignOwner<WearNTear>(player, transform) ||
               HasForeignOwner<LODGroup>(player, transform) ||
               HasForeignOwner<ZNetView>(player, transform);
    }

    private static bool HasForeignOwner<T>(Player player, Transform transform) where T : Component
    {
        T owner = transform.GetComponentInParent<T>();
        return owner != null && !IsLocalPlayerTransform(player, owner.transform);
    }
}
