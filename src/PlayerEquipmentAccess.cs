using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class PlayerEquipmentAccess
{
    private static readonly FieldInfo? VisEquipmentField =
        AccessTools.Field(typeof(Humanoid), "m_visEquipment") ??
        AccessTools.Field(typeof(Character), "m_visEquipment");

    private static readonly FieldInfo? HelmetHashField =
        AccessTools.Field(typeof(VisEquipment), "m_currentHelmetItemHash");

    private static readonly FieldInfo?[] EquippedHashFields =
    {
        AccessTools.Field(typeof(VisEquipment), "m_currentHelmetItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentChestItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentLegItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentShoulderItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentUtilityItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentTrinketItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentLeftItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentRightItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentLeftBackItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentRightBackItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentBeardItemHash"),
        AccessTools.Field(typeof(VisEquipment), "m_currentHairItemHash")
    };

    internal static bool TryGetVisEquipment(Player player, out VisEquipment visEquipment)
    {
        visEquipment = null!;

        // Local player equipment is stored on Humanoid in current Valheim builds.
        if (player == null || VisEquipmentField == null)
            return false;

        object? value = VisEquipmentField.GetValue(player);

        if (value is not VisEquipment foundVisEquipment)
            return false;

        visEquipment = foundVisEquipment;
        return true;
    }

    internal static bool IsLocalPlayerVisEquipment(VisEquipment visEquipment)
    {
        if (visEquipment == null)
            return false;

        // VisEquipment usually lives on the same object as Player, with parent lookup as a modded fallback.
        Player? player = visEquipment.GetComponent<Player>();

        if (player == null)
            player = visEquipment.GetComponentInParent<Player>();

        return player != null && player == Player.m_localPlayer;
    }

    internal static int GetHelmetHash(VisEquipment visEquipment)
    {
        if (visEquipment == null || HelmetHashField == null)
            return 0;

        object? value = HelmetHashField.GetValue(visEquipment);
        return value is int hash ? hash : 0;
    }

    internal static void GetEquippedHashes(VisEquipment visEquipment, List<int> hashes)
    {
        hashes.Clear();

        if (visEquipment == null)
            return;

        // Hash order prefers helmet, then stable worn or held items for Blacksmith trigger keys.
        foreach (FieldInfo? field in EquippedHashFields)
        {
            if (field == null)
                continue;

            object? value = field.GetValue(visEquipment);

            if (value is int hash && hash != 0 && !hashes.Contains(hash))
                hashes.Add(hash);
        }
    }

    internal static bool TryGetPrefabName(int itemHash, out string prefabName)
    {
        prefabName = string.Empty;

        // Blacksmith matches configured names by stable hash against equipped item hashes.
        ObjectDB objectDB = ObjectDB.instance;

        if (objectDB == null || itemHash == 0)
            return false;

        GameObject prefab = objectDB.GetItemPrefab(itemHash);

        if (prefab == null)
            return false;

        prefabName = prefab.name;
        return !string.IsNullOrEmpty(prefabName);
    }
}
