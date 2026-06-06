using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class FirstPersonState
{
    internal static bool Active { get; private set; }

    private static Player? _cachedPlayer;
    private static bool _defaultApplied;

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        if (_cachedPlayer != null && _cachedPlayer != player)
        {
            ForceInactive();
            _defaultApplied = false;
        }

        _cachedPlayer = player;

        if (!Plugin.EnableMod.Value)
        {
            SetActive(false);
            return;
        }

        if (!IsSafePlayerState(player))
        {
            SetActive(false);
            return;
        }

        if (!_defaultApplied)
        {
            _defaultApplied = true;

            if (Plugin.DefaultToFirstPerson.Value)
                SetActive(true);
        }

        if (CanReadToggleInput() && Input.GetKeyDown(Plugin.ToggleFirstPersonKey.Value))
            SetActive(!Active);

        if (Active && Plugin.LogRendererNames.Value)
            RendererScanner.LogRenderersOnce(player);
    }

    internal static bool ShouldApplyCamera(Player player)
    {
        return Plugin.EnableMod.Value &&
               Active &&
               player != null &&
               player == Player.m_localPlayer &&
               IsSafePlayerState(player) &&
               CanOverrideCurrentCameraContext();
    }

    internal static void ForceInactive()
    {
        SetActive(false);
        _cachedPlayer = null;
        _defaultApplied = false;
    }

    private static void SetActive(bool active)
    {
        if (Active == active)
            return;

        Active = active;

        if (!active)
        {
            HeadVisibilityController.ForceVisible();
            FirstPersonCamera.RestoreLastCamera();
            RendererScanner.ResetLogState();
        }

        Plugin.Log.LogInfo(active
            ? "Immersive first person active."
            : "Immersive first person inactive.");
    }

    private static bool CanReadToggleInput()
    {
        return !IsInputBlockingUiVisible();
    }

    private static bool CanOverrideCurrentCameraContext()
    {
        if (!Plugin.OverrideForcedThirdPerson.Value)
            return !IsAnyBlockingUiVisible() && !IsPlayerAttached();

        if (IsHardCameraBlockVisible())
            return false;

        return true;
    }

    private static bool IsAnyBlockingUiVisible()
    {
        return IsInputBlockingUiVisible() || IsHardCameraBlockVisible();
    }

    private static bool IsInputBlockingUiVisible()
    {
        if (InventoryGui.IsVisible())
            return true;

        return false;
    }

    private static bool IsHardCameraBlockVisible()
    {
        if (Menu.IsVisible())
            return true;

        if (Minimap.IsOpen())
            return true;

        return false;
    }

    private static bool IsSafePlayerState(Player player)
    {
        if (player.IsDead())
            return false;

        return true;
    }

    private static bool IsPlayerAttached()
    {
        Player? player = Player.m_localPlayer;
        return player != null && player.IsAttached();
    }
}
