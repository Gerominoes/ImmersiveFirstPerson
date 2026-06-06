using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ImmersiveBuildCamera;

internal static class PlayerRendererVisibility
{
    private static readonly Dictionary<Renderer, bool> OriginalRendererStates = new();
    private static readonly List<Renderer> DeadRenderers = new();

    private static readonly System.Reflection.FieldInfo? PlacementGhostField =
        AccessTools.Field(typeof(Player), "m_placementGhost");

    private static Player? _cachedPlayer;
    private static bool _hidden;
    private static float _nextRefreshTime;

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        bool shouldHide =
            Plugin.HideLocalPlayerWhenImmersive.Value &&
            BuildCameraState.Active &&
            !BuildCameraState.ShoulderPeekActive;

        Apply(player, shouldHide);
    }

    internal static void ForceVisible()
    {
        RestoreRendererStates();
        ResetCache();
    }

    private static void Apply(Player player, bool shouldHide)
    {
        if (player == null)
        {
            ForceVisible();
            return;
        }

        if (_cachedPlayer != null && _cachedPlayer != player)
        {
            ForceVisible();
        }

        _cachedPlayer = player;

        if (shouldHide)
        {
            HidePlayerRenderers(player);
        }
        else
        {
            RestoreRendererStates();
        }
    }

    private static void HidePlayerRenderers(Player player)
    {
        if (!_hidden)
        {
            OriginalRendererStates.Clear();
            _hidden = true;
            _nextRefreshTime = 0f;
        }

        if (OriginalRendererStates.Count == 0 || Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRendererCache(player);
            _nextRefreshTime = Time.unscaledTime + 0.25f;
        }

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (renderer.enabled)
                renderer.enabled = false;
        }
    }

    private static void RefreshRendererCache(Player player)
    {
        RemoveDestroyedRenderers();

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (ShouldSkipRenderer(player, renderer))
                continue;

            if (!OriginalRendererStates.ContainsKey(renderer))
                OriginalRendererStates.Add(renderer, renderer.enabled);
        }
    }

    private static bool ShouldSkipRenderer(Player player, Renderer renderer)
    {
        GameObject? placementGhost = GetPlacementGhost(player);

        if (placementGhost != null &&
            renderer.transform != null &&
            renderer.transform.IsChildOf(placementGhost.transform))
        {
            return true;
        }

        return false;
    }

    private static GameObject? GetPlacementGhost(Player player)
    {
        if (PlacementGhostField == null)
            return null;

        return PlacementGhostField.GetValue(player) as GameObject;
    }

    private static void RestoreRendererStates()
    {
        if (!_hidden && OriginalRendererStates.Count == 0)
            return;

        foreach (KeyValuePair<Renderer, bool> entry in OriginalRendererStates)
        {
            Renderer renderer = entry.Key;

            if (renderer == null)
                continue;

            renderer.enabled = entry.Value;
        }

        OriginalRendererStates.Clear();
        DeadRenderers.Clear();

        _hidden = false;
        _nextRefreshTime = 0f;
    }

    private static void RemoveDestroyedRenderers()
    {
        DeadRenderers.Clear();

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                DeadRenderers.Add(renderer!);
        }

        foreach (Renderer renderer in DeadRenderers)
        {
            OriginalRendererStates.Remove(renderer);
        }

        DeadRenderers.Clear();
    }

    private static void ResetCache()
    {
        _cachedPlayer = null;
        _hidden = false;
        _nextRefreshTime = 0f;
        OriginalRendererStates.Clear();
        DeadRenderers.Clear();
    }
}