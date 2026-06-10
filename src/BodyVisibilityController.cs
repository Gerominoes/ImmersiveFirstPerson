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

    // Held item markers keep weapon parts out of the head-only exclusion path.
    private static readonly string[] HeldItemKeywords =
    {
        "hand",
        "right",
        "left",
        "weapon",
        "sword",
        "axe",
        "mace",
        "hammer",
        "club",
        "knife",
        "bow",
        "arrow",
        "shield",
        "torch",
        "tool",
        "pickaxe",
        "hatchet",
        "battleaxe",
        "sledge",
        "spear",
        "atgeir",
        "crossbow",
        "buckler",
        "staff",
        "wand",
        "cultivator",
        "fishing",
        "itemstand"
    };

    private static readonly string[] HeadKeywords =
    {
        "head",
        "hair",
        "face",
        "jaw",
        "eye",
        "helmet",
        "helm"
    };

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

        if (ContainsAny(descriptor, HeldItemKeywords))
            return false;

        return ContainsAnyHeadKeyword(descriptor);
    }

    private static bool ContainsAny(string value, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool ContainsAnyHeadKeyword(string value)
    {
        foreach (string keyword in HeadKeywords)
        {
            if (ContainsKeywordTokenOrSafePrefix(value, keyword))
                return true;
        }

        return false;
    }

    private static bool ContainsKeywordTokenOrSafePrefix(string value, string keyword)
    {
        int index = value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            bool startsAtToken = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
            int end = index + keyword.Length;
            bool endsAtToken = end >= value.Length || !char.IsLetterOrDigit(value[end]);

            if (startsAtToken && endsAtToken)
                return true;

            if (startsAtToken && keyword.Length >= 5)
                return true;

            index = value.IndexOf(keyword, end, StringComparison.OrdinalIgnoreCase);
        }

        return false;
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
