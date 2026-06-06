using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class HeadVisibilityController
{
    private static readonly Dictionary<Renderer, bool> OriginalRendererStates = new();
    private static readonly Dictionary<Transform, Vector3> OriginalBoneScales = new();
    private static readonly List<Renderer?> RenderersToRemove = new();
    private static readonly List<Transform?> BonesToRemove = new();

    private static Player? _cachedPlayer;
    private static bool _active;
    private static float _nextRefreshTime;

    private static readonly string[] HeadKeywords = { "head" };
    private static readonly string[] HairKeywords = { "hair" };
    private static readonly string[] FaceKeywords = { "face", "jaw", "eye" };
    private static readonly string[] HelmetKeywords = { "helmet", "helm" };
    private static readonly string[] ShoulderKeywords = { "shoulder" };
    private static readonly string[] BackItemKeywords = { "back", "cape", "cloak" };

    internal static void Update(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
            return;

        bool shouldHide = Plugin.EnableMod.Value && FirstPersonState.Active && HasAnyVisibilityRuleEnabled();
        Apply(player, shouldHide);
    }

    internal static void ForceVisible()
    {
        RestoreRendererStates();
        RestoreBoneScales();
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
            OriginalBoneScales.Clear();
            _active = true;
            _nextRefreshTime = 0f;
        }

        ApplyBoneMode(player);

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRendererCache(player);
            _nextRefreshTime = Time.unscaledTime + 0.5f;
        }

        HideCachedRenderers();
    }

    private static bool HasAnyVisibilityRuleEnabled()
    {
        return Plugin.HideHead.Value || Plugin.HideHair.Value || Plugin.HideFace.Value || Plugin.HideHelmet.Value || Plugin.HideShoulderPads.Value || Plugin.HideBackItems.Value;
    }

    private static void ApplyBoneMode(Player player)
    {
        if (Plugin.HeadHideModeConfig.Value == HeadHideModeOption.BoneShrink && Plugin.HideHead.Value)
        {
            ShrinkHeadBones(player);
            return;
        }

        RestoreBoneScales();
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

            if (ShouldHideRenderer(renderer))
                desiredRenderers.Add(renderer);
        }

        RenderersToRemove.Clear();

        foreach (KeyValuePair<Renderer, bool> entry in OriginalRendererStates)
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
                OriginalRendererStates.Add(renderer, renderer.enabled);
        }
    }

    private static bool ShouldHideRenderer(Renderer renderer)
    {
        string descriptor = BuildRendererDescriptor(renderer);

        if (Plugin.HeadHideModeConfig.Value == HeadHideModeOption.RendererDisable && Plugin.HideHead.Value && ContainsAny(descriptor, HeadKeywords))
            return true;

        if (Plugin.HideHair.Value && ContainsAny(descriptor, HairKeywords))
            return true;

        if (Plugin.HideFace.Value && ContainsAny(descriptor, FaceKeywords))
            return true;

        if (Plugin.HideHelmet.Value && ContainsAny(descriptor, HelmetKeywords))
            return true;

        if (Plugin.HideShoulderPads.Value && ContainsAny(descriptor, ShoulderKeywords))
            return true;

        if (Plugin.HideBackItems.Value && ContainsAny(descriptor, BackItemKeywords))
            return true;

        return false;
    }

    private static string BuildRendererDescriptor(Renderer renderer)
    {
        string path = renderer.transform != null ? RendererScanner.GetPath(renderer.transform) : string.Empty;
        return $"{renderer.name} {path}".ToLowerInvariant();
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

    private static void HideCachedRenderers()
    {
        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (renderer.enabled)
                renderer.enabled = false;
        }
    }

    private static void ShrinkHeadBones(Player player)
    {
        Transform[] transforms = player.GetComponentsInChildren<Transform>(true);

        foreach (Transform transform in transforms)
        {
            if (transform == null || !IsHeadBone(transform))
                continue;

            if (!OriginalBoneScales.ContainsKey(transform))
            {
                OriginalBoneScales.Add(transform, transform.localScale);
                Plugin.DebugLog($"Shrinking head bone: {RendererScanner.GetPath(transform)}");
            }

            transform.localScale = Vector3.one * 0.001f;
        }

        RemoveDestroyedBones();
    }

    private static bool IsHeadBone(Transform transform)
    {
        return transform.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void RestoreRendererStates()
    {
        if (OriginalRendererStates.Count == 0)
            return;

        foreach (KeyValuePair<Renderer, bool> entry in OriginalRendererStates)
        {
            Renderer renderer = entry.Key;

            if (renderer == null)
                continue;

            renderer.enabled = entry.Value;
        }

        OriginalRendererStates.Clear();
        RenderersToRemove.Clear();
        _nextRefreshTime = 0f;
    }

    private static void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (OriginalRendererStates.TryGetValue(renderer, out bool originalState))
            renderer.enabled = originalState;
    }

    private static void RestoreBoneScales()
    {
        if (OriginalBoneScales.Count == 0)
            return;

        foreach (KeyValuePair<Transform, Vector3> entry in OriginalBoneScales)
        {
            Transform transform = entry.Key;

            if (transform == null)
                continue;

            transform.localScale = entry.Value;
        }

        OriginalBoneScales.Clear();
        BonesToRemove.Clear();
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

    private static void RemoveDestroyedBones()
    {
        BonesToRemove.Clear();

        foreach (Transform transform in OriginalBoneScales.Keys)
        {
            if (transform == null)
                BonesToRemove.Add(transform);
        }

        foreach (Transform? transform in BonesToRemove)
        {
            if (transform is not null)
                OriginalBoneScales.Remove(transform);
        }

        BonesToRemove.Clear();
    }

    private static void ResetCache()
    {
        _cachedPlayer = null;
        _active = false;
        _nextRefreshTime = 0f;
        OriginalRendererStates.Clear();
        OriginalBoneScales.Clear();
        RenderersToRemove.Clear();
        BonesToRemove.Clear();
    }
}
