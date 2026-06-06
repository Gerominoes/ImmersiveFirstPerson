using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveFirstPerson;

internal static class HeadVisibilityController
{
    private const float HeadSlotClipRadius = 0.55f;
    private const float MaxHeadSlotExtent = 1.15f;
    private const float MinHeadSlotHeight = 0.9f;

    private readonly struct RendererState
    {
        internal readonly bool Enabled;
        internal readonly ShadowCastingMode ShadowCastingMode;

        internal RendererState(Renderer renderer)
        {
            Enabled = renderer.enabled;
            ShadowCastingMode = renderer.shadowCastingMode;
        }
    }

    private static readonly Dictionary<Renderer, RendererState> OriginalRendererStates = new();
    private static readonly List<Renderer?> RenderersToRemove = new();

    private static Player? _cachedPlayer;
    private static bool _active;
    private static float _nextRefreshTime;

    private static readonly string[] HeadSlotKeywords =
    {
        "head",
        "hair",
        "beard",
        "face",
        "jaw",
        "eye",
        "brow",
        "mouth",
        "nose",
        "teeth",
        "helmet",
        "helm",
        "hat",
        "hood",
        "circlet",
        "crown",
        "headgear",
        "padded"
    };

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
        "cultivator",
        "fishing",
        "itemstand"
    };

    private static readonly string[] BodyKeywords =
    {
        "body",
        "torso",
        "chest",
        "arm",
        "leg",
        "foot",
        "feet",
        "hand",
        "player",
        "character",
        "human",
        "male",
        "female",
        "skin"
    };

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        bool shouldHide = Plugin.EnableMod.Value && FirstPersonState.Active && Plugin.HideHead.Value;
        Apply(player, shouldHide);
    }

    internal static void ForceVisible()
    {
        RestoreRendererStates();
        ResetCache();
    }

    private static void Apply(Player player, bool shouldHide)
    {
        if (_cachedPlayer != null && _cachedPlayer != player)
            ForceVisible();

        _cachedPlayer = player;

        if (!shouldHide)
        {
            ForceVisible();
            return;
        }

        if (!_active)
        {
            OriginalRendererStates.Clear();
            _active = true;
            _nextRefreshTime = 0f;
        }

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRendererCache(player);
            _nextRefreshTime = Time.unscaledTime + 0.15f;
        }

        HideCachedRenderersShadowsOnly();
    }

    private static void RefreshRendererCache(Player player)
    {
        RemoveDestroyedRenderers();

        HashSet<Renderer> desiredRenderers = new();
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (ShouldHideHeadSlotRenderer(player, renderer))
                desiredRenderers.Add(renderer);
        }

        RenderersToRemove.Clear();

        foreach (KeyValuePair<Renderer, RendererState> entry in OriginalRendererStates)
        {
            Renderer renderer = entry.Key;

            if (renderer == null || !desiredRenderers.Contains(renderer))
                RenderersToRemove.Add(renderer);
        }

        foreach (Renderer? renderer in RenderersToRemove)
        {
            if (renderer is not null)
            {
                RestoreRenderer(renderer);
                OriginalRendererStates.Remove(renderer);
            }
        }

        RenderersToRemove.Clear();

        foreach (Renderer renderer in desiredRenderers)
        {
            if (!OriginalRendererStates.ContainsKey(renderer))
            {
                OriginalRendererStates.Add(renderer, new RendererState(renderer));
                Plugin.DebugLog($"First-person head visibility matched renderer: {BuildRendererDescriptor(renderer)} | bounds={renderer.bounds.size}");
            }
        }
    }

    private static bool ShouldHideHeadSlotRenderer(Player player, Renderer renderer)
    {
        string descriptor = BuildRendererDescriptor(renderer);

        if (LooksLikeHeldItem(descriptor))
            return false;

        if (LooksLikeFullBodyRenderer(renderer, descriptor))
            return false;

        if (ContainsAny(descriptor, HeadSlotKeywords))
            return true;

        return IsSmallRendererNearHeadOrCamera(player, renderer);
    }

    private static bool IsSmallRendererNearHeadOrCamera(Player player, Renderer renderer)
    {
        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        float largestExtent = Mathf.Max(size.x, size.y, size.z);

        if (largestExtent > MaxHeadSlotExtent)
            return false;

        float localY = player.transform.InverseTransformPoint(bounds.center).y;

        if (localY < MinHeadSlotHeight)
            return false;

        Camera? camera = Camera.main;

        if (camera == null)
            return false;

        Vector3 closestPoint = bounds.ClosestPoint(camera.transform.position);
        return Vector3.Distance(closestPoint, camera.transform.position) <= HeadSlotClipRadius;
    }

    private static string BuildRendererDescriptor(Renderer renderer)
    {
        StringBuilder builder = new();

        builder.Append(renderer.name).Append(' ');

        if (renderer.transform != null)
            builder.Append(RendererScanner.GetPath(renderer.transform)).Append(' ');

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            builder.Append(skinnedMeshRenderer.sharedMesh.name).Append(' ');

        Material[] materials = renderer.sharedMaterials;

        if (materials != null)
        {
            foreach (Material material in materials)
            {
                if (material != null)
                    builder.Append(material.name).Append(' ');
            }
        }

        return builder.ToString().ToLowerInvariant();
    }

    private static bool LooksLikeHeldItem(string descriptor)
    {
        return ContainsAny(descriptor, HeldItemKeywords);
    }

    private static bool LooksLikeFullBodyRenderer(Renderer renderer, string descriptor)
    {
        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        float largestExtent = Mathf.Max(size.x, size.y, size.z);
        bool bodyName = ContainsAny(descriptor, BodyKeywords) && !ContainsAny(descriptor, HeadSlotKeywords);
        bool bodyScale = size.y > 1.2f && largestExtent > 1.4f;

        return bodyName || bodyScale;
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

    private static void HideCachedRenderersShadowsOnly()
    {
        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private static void RestoreRendererStates()
    {
        if (OriginalRendererStates.Count == 0)
            return;

        foreach (KeyValuePair<Renderer, RendererState> entry in OriginalRendererStates)
        {
            Renderer renderer = entry.Key;

            if (renderer == null)
                continue;

            renderer.enabled = entry.Value.Enabled;
            renderer.shadowCastingMode = entry.Value.ShadowCastingMode;
        }

        OriginalRendererStates.Clear();
        RenderersToRemove.Clear();
        _nextRefreshTime = 0f;
    }

    private static void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (OriginalRendererStates.TryGetValue(renderer, out RendererState originalState))
        {
            renderer.enabled = originalState.Enabled;
            renderer.shadowCastingMode = originalState.ShadowCastingMode;
        }
    }

    private static void RemoveDestroyedRenderers()
    {
        RenderersToRemove.Clear();

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                RenderersToRemove.Add(renderer);
        }

        foreach (Renderer? renderer in RenderersToRemove)
        {
            if (renderer is not null)
                OriginalRendererStates.Remove(renderer);
        }

        RenderersToRemove.Clear();
    }

    private static void ResetCache()
    {
        _cachedPlayer = null;
        _active = false;
        _nextRefreshTime = 0f;
        OriginalRendererStates.Clear();
        RenderersToRemove.Clear();
    }
}
