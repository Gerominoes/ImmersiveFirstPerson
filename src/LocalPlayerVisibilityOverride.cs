using System;
using System.Reflection;
using HarmonyLib;

namespace ImmersiveFirstPerson;

internal static class LocalPlayerVisibilityOverride
{
    private static readonly MethodInfo? PlayerSetVisibleMethod =
        FindInstanceMethod(typeof(Player), "SetVisible") ??
        FindInstanceMethod(typeof(Character), "SetVisible");

    private static readonly FieldInfo? VisEquipmentField =
        AccessTools.Field(typeof(Humanoid), "m_visEquipment") ??
        AccessTools.Field(typeof(Character), "m_visEquipment");

    private static Type? _cachedVisEquipmentType;
    private static MethodInfo? _cachedVisEquipmentSetVisibleMethod;

    internal static void ForceVisible(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        if (!Plugin.EnableMod.Value || !FirstPersonState.Active || !Plugin.ForceBodyVisible.Value)
            return;

        TrySetPlayerVisible(player);
        TrySetVisEquipmentVisible(player);
    }

    private static void TrySetPlayerVisible(Player player)
    {
        if (PlayerSetVisibleMethod == null)
            return;

        try
        {
            PlayerSetVisibleMethod.Invoke(player, new object[] { true });
        }
        catch (Exception ex)
        {
            Plugin.DebugLog($"SetVisible override failed: {ex.GetType().Name}");
        }
    }

    private static void TrySetVisEquipmentVisible(Player player)
    {
        if (VisEquipmentField == null)
            return;

        object? visEquipment = VisEquipmentField.GetValue(player);

        if (visEquipment == null)
            return;

        Type type = visEquipment.GetType();

        if (_cachedVisEquipmentType != type)
        {
            _cachedVisEquipmentType = type;
            _cachedVisEquipmentSetVisibleMethod = FindInstanceMethod(type, "SetVisible");
        }

        if (_cachedVisEquipmentSetVisibleMethod == null)
            return;

        try
        {
            _cachedVisEquipmentSetVisibleMethod.Invoke(visEquipment, new object[] { true });
        }
        catch (Exception ex)
        {
            Plugin.DebugLog($"VisEquipment visibility override failed: {ex.GetType().Name}");
        }
    }

    private static MethodInfo? FindInstanceMethod(Type type, string name)
    {
        // Optional Valheim methods vary by version, so missing methods should not warn.
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
