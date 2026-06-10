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
    private const float MaxHeadSlotHeight = 2.4f;
    private const float MaxHeadSlotHorizontalDistance = 0.75f;
    private const float HeadShrinkScale = 0.001f;
    private const float HeadSlotCompensationScale = 1f / HeadShrinkScale;
    private static readonly Vector3 HeadShrinkVector = Vector3.one * HeadShrinkScale;

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

    private readonly struct TransformState
    {
        internal readonly Vector3 LocalPosition;
        internal readonly Vector3 LocalScale;

        internal TransformState(Transform transform)
        {
            LocalPosition = transform.localPosition;
            LocalScale = transform.localScale;
        }
    }

    private static readonly Dictionary<Renderer, RendererState> OriginalRendererStates = new();
    private static readonly Dictionary<Transform, Vector3> OriginalBoneScales = new();
    private static readonly Dictionary<Transform, TransformState> OriginalHeadSlotStates = new();
    private static readonly HashSet<Renderer> DesiredRenderers = new();
    private static readonly List<Transform> CachedHeadBones = new();
    private static readonly List<Renderer?> RenderersToRemove = new();
    private static readonly List<Transform?> BonesToRemove = new();
    private static readonly List<Transform?> HeadSlotTransformsToRemove = new();

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

    // Head-slot hierarchy markers are stronger than mesh or material names.
    private static readonly string[] HeadSlotHierarchyKeywords =
    {
        "attach_helmet",
        "attach_helm",
        "attach_head",
        "helmet",
        "helm",
        "hair",
        "beard",
        "headgear"
    };

    private static readonly string[] HeadBoneRejectKeywords =
    {
        "helmet",
        "helm",
        "hat",
        "hood",
        "hair",
        "beard",
        "circlet",
        "crown",
        "headgear",
        "padded"
    };

    // Equipment skeletons may share head bones with shoulder-linked meshes.
    private static readonly string[] EquipmentSkeletonKeywords =
    {
        "attach_skin",
        "attach_armor",
        "attach_helmet",
        "attach_helm",
        "attach_shoulder",
        "attach_back"
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
        if (!IsLocalPlayer(player))
        {
            ForceVisible();
            return;
        }

        bool shouldHide = Plugin.EnableMod.Value && FirstPersonState.Active && Plugin.HideHead.Value;
        Apply(player, shouldHide);
    }

    internal static void ForceVisible()
    {
        RestoreHeadSlotStates();
        RestoreBoneScales();
        RestoreRendererStates();
        ResetCache();
    }

    private static void Apply(Player player, bool shouldHide)
    {
        if (!IsLocalPlayer(player))
        {
            ForceVisible();
            return;
        }

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
            // Start with empty caches so equipment and skeleton state are captured fresh.
            OriginalRendererStates.Clear();
            OriginalBoneScales.Clear();
            OriginalHeadSlotStates.Clear();
            DesiredRenderers.Clear();
            CachedHeadBones.Clear();
            _active = true;
            _nextRefreshTime = 0f;
        }

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRendererCache(player);
            RefreshHeadBoneCache(player);
            _nextRefreshTime = Time.unscaledTime + Mathf.Clamp(Plugin.VisibilityRefreshInterval.Value, 0.1f, 10f);
        }

        ApplyCachedHeadBoneScale();
        CompensateHeadSlotTransforms();
        HideCachedRenderersShadowsOnly();
    }

    private static void RefreshRendererCache(Player player)
    {
        // Refresh against unmodified attachment transforms so stale compensation cannot affect matching.
        RestoreHeadSlotStates();
        RemoveDestroyedRenderers();
        RemoveDestroyedHeadSlotTransforms();

        DesiredRenderers.Clear();
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!IsRendererOwnedByPlayer(player, renderer))
                continue;

            if (ShouldHideHeadSlotRenderer(player, renderer))
                DesiredRenderers.Add(renderer);
        }

        RenderersToRemove.Clear();

        foreach (KeyValuePair<Renderer, RendererState> entry in OriginalRendererStates)
        {
            Renderer renderer = entry.Key;

            if (renderer == null || !DesiredRenderers.Contains(renderer))
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

        foreach (Renderer renderer in DesiredRenderers)
        {
            if (!OriginalRendererStates.ContainsKey(renderer))
            {
                OriginalRendererStates.Add(renderer, new RendererState(renderer));
                Plugin.DebugLog($"First-person head-slot visibility matched renderer: {BuildRendererDescriptor(renderer)} | bounds={renderer.bounds.size}");
            }
        }
    }

    private static bool ShouldHideHeadSlotRenderer(Player player, Renderer renderer)
    {
        if (!IsRendererOwnedByPlayer(player, renderer))
            return false;

        string descriptor = BuildRendererDescriptor(renderer);

        if (LooksLikeHeldItem(descriptor))
            return false;

        if (LooksLikeFullBodyRenderer(renderer, descriptor))
            return false;

        bool hasHeadSlotKeyword = ContainsAnyHeadSlotKeyword(descriptor);
        bool hasHeadSlotHierarchy = LooksLikeHeadSlotHierarchy(renderer, descriptor);
        bool isNearHeadOrCamera = IsSmallRendererNearHeadOrCamera(player, renderer);

        if (hasHeadSlotHierarchy && (hasHeadSlotKeyword || isNearHeadOrCamera))
            return true;

        return hasHeadSlotKeyword && isNearHeadOrCamera;
    }

    private static bool IsSmallRendererNearHeadOrCamera(Player player, Renderer renderer)
    {
        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        float largestExtent = Mathf.Max(size.x, size.y, size.z);

        if (largestExtent > MaxHeadSlotExtent)
            return false;

        Vector3 localCenter = player.transform.InverseTransformPoint(bounds.center);
        float localY = localCenter.y;

        if (localY < MinHeadSlotHeight || localY > MaxHeadSlotHeight)
            return false;

        Vector2 localHorizontal = new(localCenter.x, localCenter.z);

        if (localHorizontal.magnitude > MaxHeadSlotHorizontalDistance)
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

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            if (skinnedMeshRenderer.sharedMesh != null)
                builder.Append(skinnedMeshRenderer.sharedMesh.name).Append(' ');

            if (skinnedMeshRenderer.rootBone != null)
                builder.Append(RendererScanner.GetPath(skinnedMeshRenderer.rootBone)).Append(' ');
        }

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

    private static bool LooksLikeHeadSlotHierarchy(Renderer renderer, string descriptor)
    {
        if (renderer == null)
            return false;

        if (ContainsAny(descriptor, HeadSlotHierarchyKeywords))
            return true;

        if (IsTransformUnderLikelyHeadBone(renderer.transform, false))
            return true;

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            if (IsTransformUnderLikelyHeadBone(skinnedMeshRenderer.rootBone, true))
                return true;

            Transform[] bones = skinnedMeshRenderer.bones;

            if (bones != null)
            {
                foreach (Transform bone in bones)
                {
                    if (IsTransformUnderLikelyHeadBone(bone, true))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsTransformUnderLikelyHeadBone(Transform? transform, bool includeSelf)
    {
        // Walking ancestors lets child attachments inherit head-bone context.
        Transform? current = includeSelf ? transform : transform?.parent;

        while (current != null)
        {
            if (IsLikelyCharacterHeadBoneName(current.name))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool IsLikelyCharacterHeadBoneName(string name)
    {
        if (name.Equals("head", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bip_head", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bip01 head", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bip01head", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bip001 head", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("bip001head", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Length > 4 &&
               name.StartsWith("head", StringComparison.OrdinalIgnoreCase) &&
               char.IsDigit(name[4]);
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
        bool bodyName = ContainsAny(descriptor, BodyKeywords) && !ContainsAnyHeadSlotKeyword(descriptor);
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

    private static bool ContainsAnyHeadSlotKeyword(string value)
    {
        foreach (string keyword in HeadSlotKeywords)
        {
            if (ContainsKeywordTokenOrSafePrefix(value, keyword))
                return true;
        }

        return false;
    }

    private static bool ContainsAnyHeadBoneRejectKeyword(string value)
    {
        foreach (string keyword in HeadBoneRejectKeywords)
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

    private static void HideCachedRenderersShadowsOnly()
    {
        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (!IsCachedLocalRenderer(renderer))
                continue;

            if (!renderer.enabled)
                renderer.enabled = true;

            if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private static void RefreshHeadBoneCache(Player player)
    {
        RemoveDestroyedBones();

        Transform[] transforms = player.GetComponentsInChildren<Transform>(true);

        foreach (Transform transform in transforms)
        {
            if (transform == null || !IsHeadBone(transform))
                continue;

            if (!IsTransformOwnedByPlayer(player, transform))
                continue;

            if (!OriginalBoneScales.ContainsKey(transform))
            {
                OriginalBoneScales.Add(transform, transform.localScale);
                CachedHeadBones.Add(transform);
                Plugin.DebugLog($"Shrinking local head bone for first-person view: {RendererScanner.GetPath(transform)}");
            }
        }
    }

    private static void ApplyCachedHeadBoneScale()
    {
        RemoveDestroyedBones();

        foreach (Transform transform in CachedHeadBones)
        {
            if (transform == null)
                continue;

            if (!IsCachedLocalTransform(transform))
                continue;

            if (transform.localScale != HeadShrinkVector)
                transform.localScale = HeadShrinkVector;
        }
    }

    private static bool IsHeadBone(Transform transform)
    {
        string descriptor = $"{transform.name} {RendererScanner.GetPath(transform)}".ToLowerInvariant();

        if (ContainsAny(descriptor, EquipmentSkeletonKeywords))
            return false;

        if (ContainsAny(descriptor, HeldItemKeywords))
            return false;

        if (ContainsAnyHeadBoneRejectKeyword(descriptor))
            return false;

        string name = transform.name;
        return IsLikelyCharacterHeadBoneName(name) ||
               ContainsKeywordTokenOrSafePrefix(name, "head");
    }

    private static void CompensateHeadSlotTransforms()
    {
        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (!IsCachedLocalRenderer(renderer))
                continue;

            CompensateTransformIfUnderShrunkHead(renderer.transform);

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                if (skinnedMeshRenderer.rootBone != null)
                    CompensateTransformIfUnderShrunkHead(skinnedMeshRenderer.rootBone);
            }
        }

        RemoveDestroyedHeadSlotTransforms();
    }

    private static void CompensateTransformIfUnderShrunkHead(Transform transform)
    {
        if (!IsCachedLocalTransform(transform))
            return;

        Transform? compensationRoot = FindCompensationRootUnderShrunkHead(transform);

        if (compensationRoot == null)
            return;

        CompensateHeadSlotTransform(compensationRoot, true, "position and scale");
    }

    private static void CompensateHeadSlotTransform(Transform transform, bool compensatePosition, string mode)
    {
        if (!IsCachedLocalTransform(transform))
            return;

        if (!OriginalHeadSlotStates.ContainsKey(transform))
        {
            OriginalHeadSlotStates.Add(transform, new TransformState(transform));
            Plugin.DebugLog($"Compensating head-slot transform {mode}: {RendererScanner.GetPath(transform)}");
        }

        // Position is only expanded for attachments located below the shrunken head bone.
        TransformState originalState = OriginalHeadSlotStates[transform];
        Vector3 compensatedPosition = compensatePosition
            ? originalState.LocalPosition * HeadSlotCompensationScale
            : originalState.LocalPosition;
        Vector3 compensatedScale = originalState.LocalScale * HeadSlotCompensationScale;

        if (transform.localPosition != compensatedPosition)
            transform.localPosition = compensatedPosition;

        if (transform.localScale != compensatedScale)
            transform.localScale = compensatedScale;
    }

    private static Transform? FindCompensationRootUnderShrunkHead(Transform transform)
    {
        if (transform == null || !IsCachedLocalTransform(transform) || OriginalBoneScales.ContainsKey(transform))
            return null;

        Transform current = transform;

        while (current.parent != null)
        {
            if (OriginalBoneScales.ContainsKey(current.parent))
                return current;

            current = current.parent;
        }

        return null;
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
        CachedHeadBones.Clear();
        BonesToRemove.Clear();
    }

    private static void RestoreHeadSlotStates()
    {
        if (OriginalHeadSlotStates.Count == 0)
            return;

        foreach (KeyValuePair<Transform, TransformState> entry in OriginalHeadSlotStates)
        {
            Transform transform = entry.Key;

            if (transform == null)
                continue;

            transform.localPosition = entry.Value.LocalPosition;
            transform.localScale = entry.Value.LocalScale;
        }

        OriginalHeadSlotStates.Clear();
        HeadSlotTransformsToRemove.Clear();
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

        for (int i = CachedHeadBones.Count - 1; i >= 0; i--)
        {
            Transform transform = CachedHeadBones[i];

            if (transform != null)
                continue;

            CachedHeadBones.RemoveAt(i);

            if (transform is not null)
                OriginalBoneScales.Remove(transform);
        }

        foreach (Transform transform in OriginalBoneScales.Keys)
        {
            if (transform == null)
                BonesToRemove.Add(transform);
        }

        foreach (Transform? transform in BonesToRemove)
        {
            if (transform is not null)
            {
                OriginalBoneScales.Remove(transform);
                CachedHeadBones.Remove(transform);
            }
        }

        BonesToRemove.Clear();
    }

    private static void RemoveDestroyedHeadSlotTransforms()
    {
        HeadSlotTransformsToRemove.Clear();

        foreach (Transform transform in OriginalHeadSlotStates.Keys)
        {
            if (transform == null)
                HeadSlotTransformsToRemove.Add(transform);
        }

        foreach (Transform? transform in HeadSlotTransformsToRemove)
        {
            if (transform is not null)
                OriginalHeadSlotStates.Remove(transform);
        }

        HeadSlotTransformsToRemove.Clear();
    }

    private static bool IsLocalPlayer(Player player)
    {
        return player != null && player == Player.m_localPlayer;
    }

    private static bool IsRendererOwnedByPlayer(Player player, Renderer renderer)
    {
        return renderer != null && IsTransformOwnedByPlayer(player, renderer.transform);
    }

    private static bool IsTransformOwnedByPlayer(Player player, Transform transform)
    {
        // Multiplayer safety: head hiding must only mutate the local player's hierarchy.
        return IsLocalPlayer(player) &&
               player.transform != null &&
               transform != null &&
               (transform == player.transform || transform.IsChildOf(player.transform));
    }

    private static bool IsCachedLocalRenderer(Renderer renderer)
    {
        return _cachedPlayer != null && IsRendererOwnedByPlayer(_cachedPlayer, renderer);
    }

    private static bool IsCachedLocalTransform(Transform transform)
    {
        return _cachedPlayer != null && IsTransformOwnedByPlayer(_cachedPlayer, transform);
    }

    private static void ResetCache()
    {
        _cachedPlayer = null;
        _active = false;
        _nextRefreshTime = 0f;
        OriginalRendererStates.Clear();
        OriginalBoneScales.Clear();
        OriginalHeadSlotStates.Clear();
        DesiredRenderers.Clear();
        CachedHeadBones.Clear();
        RenderersToRemove.Clear();
        BonesToRemove.Clear();
        HeadSlotTransformsToRemove.Clear();
    }
}
