using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class BodyVisibilityController
{
    private static readonly Dictionary<Renderer, bool> OriginalRendererStates = new();
    private static readonly List<Renderer?> DeadRenderers = new();
    private static Player? _cachedPlayer;
    private static bool _captured;

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        if (!Plugin.EnableMod.Value || !FirstPersonState.Active || !Plugin.ForceBodyVisible.Value)
        {
            Reset();
            return;
        }

        if (_cachedPlayer != null && _cachedPlayer != player)
            Reset();

        _cachedPlayer = player;

        if (!_captured)
            CaptureVisibleBodyRenderers(player);

        ForceCapturedBodyRenderersVisible();
    }

    internal static void Reset()
    {
        OriginalRendererStates.Clear();
        DeadRenderers.Clear();
        _cachedPlayer = null;
        _captured = false;
    }

    private static void CaptureVisibleBodyRenderers(Player player)
    {
        OriginalRendererStates.Clear();
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            if (LooksLikeHeadRenderer(renderer))
                continue;

            OriginalRendererStates[renderer] = true;
        }

        _captured = true;
        Plugin.DebugLog($"Captured {OriginalRendererStates.Count} visible body renderers for first-person body restore.");
    }

    private static void ForceCapturedBodyRenderersVisible()
    {
        RemoveDestroyedRenderers();

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (!renderer.enabled)
                renderer.enabled = true;
        }
    }

    private static bool LooksLikeHeadRenderer(Renderer renderer)
    {
        string path = renderer.transform != null ? RendererScanner.GetPath(renderer.transform) : string.Empty;
        string descriptor = $"{renderer.name} {path}".ToLowerInvariant();

        return Contains(descriptor, "head") ||
               Contains(descriptor, "hair") ||
               Contains(descriptor, "face") ||
               Contains(descriptor, "jaw") ||
               Contains(descriptor, "eye") ||
               Contains(descriptor, "helmet") ||
               Contains(descriptor, "helm");
    }

    private static bool Contains(string value, string keyword)
    {
        return value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void RemoveDestroyedRenderers()
    {
        DeadRenderers.Clear();

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                DeadRenderers.Add(renderer);
        }

        foreach (Renderer? renderer in DeadRenderers)
        {
            if (renderer is not null)
                OriginalRendererStates.Remove(renderer);
        }

        DeadRenderers.Clear();
    }
}
