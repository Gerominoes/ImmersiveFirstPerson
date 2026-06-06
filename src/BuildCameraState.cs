using HarmonyLib;
using UnityEngine;

namespace ImmersiveBuildCamera;

internal static class BuildCameraState
{
    internal static bool Active { get; private set; }
    internal static bool PrecisionMovementActive { get; private set; }

    internal static bool ShoulderPeekActive => Active && GetShoulderDirection() != 0;

    private static int _toggledShoulderDirection;

    private static readonly System.Reflection.FieldInfo? RightItemField =
        AccessTools.Field(typeof(Humanoid), "m_rightItem");

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        bool canUseCamera = CanUseImmersiveCamera(player);

        if (!canUseCamera)
        {
            SetActive(false);
            return;
        }

        if (Input.GetKeyDown(Plugin.ToggleCameraKey.Value))
        {
            SetActive(!Active);
        }

        if (!Active)
            return;

        UpdateShoulderPeekState();
        BuildCameraDistance.UpdateFromInput();

        if (Plugin.EnablePrecisionMovement.Value &&
            Input.GetKeyDown(Plugin.TogglePrecisionMovementKey.Value))
        {
            SetPrecisionMovement(!PrecisionMovementActive);
        }
    }

    internal static void ForceInactive()
    {
        SetActive(false);
    }

    internal static int GetShoulderDirection()
    {
        if (!Active)
            return 0;

        if (Plugin.ToggleShoulderPeek.Value)
            return _toggledShoulderDirection;

        return GetHeldShoulderDirection();
    }

    private static void UpdateShoulderPeekState()
    {
        if (!Plugin.ToggleShoulderPeek.Value)
        {
            _toggledShoulderDirection = 0;
            return;
        }

        bool leftPressed = Input.GetKeyDown(Plugin.LeftShoulderKey.Value);
        bool rightPressed = Input.GetKeyDown(Plugin.RightShoulderKey.Value);

        if (leftPressed && rightPressed)
        {
            SetShoulderDirection(0);
            return;
        }

        if (leftPressed)
        {
            ToggleShoulderDirection(-1);
            return;
        }

        if (rightPressed)
        {
            ToggleShoulderDirection(1);
        }
    }

    private static void ToggleShoulderDirection(int direction)
    {
        if (_toggledShoulderDirection == direction)
        {
            SetShoulderDirection(0);
            return;
        }

        SetShoulderDirection(direction);
    }

    private static void SetShoulderDirection(int direction)
    {
        if (_toggledShoulderDirection == direction)
            return;

        _toggledShoulderDirection = direction;

        if (direction < 0)
            Plugin.DebugLog("Shoulder peek set to left.");
        else if (direction > 0)
            Plugin.DebugLog("Shoulder peek set to right.");
        else
            Plugin.DebugLog("Shoulder peek centered.");
    }

    private static int GetHeldShoulderDirection()
    {
        bool left = Input.GetKey(Plugin.LeftShoulderKey.Value);
        bool right = Input.GetKey(Plugin.RightShoulderKey.Value);

        if (left && !right)
            return -1;

        if (right && !left)
            return 1;

        return 0;
    }

    private static void SetActive(bool active)
    {
        if (Active == active)
            return;

        Active = active;

        if (active)
        {
            BuildCameraDistance.ResetForSession();

            PrecisionMovementActive =
                Plugin.EnablePrecisionMovement.Value &&
                Plugin.PrecisionMovementDefaultOn.Value;
        }
        else
        {
            PrecisionMovementActive = false;
            SetShoulderDirection(0);
            PlayerRendererVisibility.ForceVisible();
        }

        Plugin.Log.LogInfo(active
            ? $"Immersive build camera active. Precision movement: {(PrecisionMovementActive ? "on" : "off")}."
            : "Immersive build camera inactive.");
    }

    private static void SetPrecisionMovement(bool active)
    {
        if (PrecisionMovementActive == active)
            return;

        PrecisionMovementActive = active;

        Plugin.Log.LogInfo(active
            ? "Precision movement active."
            : "Precision movement inactive.");
    }

    private static bool CanUseImmersiveCamera(Player player)
    {
        if (!IsSafePlayerState(player))
            return false;

        if (!HasBuildTool(player))
            return false;

        return true;
    }

    private static bool HasBuildTool(Player player)
    {
        if (RightItemField == null)
        {
            Plugin.Log.LogWarning("Could not find Humanoid.m_rightItem.");
            return false;
        }

        ItemDrop.ItemData? rightItem =
            RightItemField.GetValue(player) as ItemDrop.ItemData;

        if (rightItem == null)
            return false;

        if (rightItem.m_shared == null)
            return false;

        return rightItem.m_shared.m_buildPieces != null;
    }

    private static bool IsSafePlayerState(Player player)
    {
        if (player.IsDead())
            return false;

        if (player.IsAttached())
            return false;

        if (player.IsSwimming())
            return false;

        if (InventoryGui.IsVisible())
            return false;

        if (Menu.IsVisible())
            return false;

        if (Minimap.IsOpen())
            return false;

        return true;
    }
}
