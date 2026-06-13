using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveFirstPerson;

internal static class HelmetShadowController
{
    private const float MaxHelmetRendererExtent = 1.35f;
    private const float MinHelmetRendererHeight = 0.85f;
    private const float MaxHelmetRendererHeight = 2.45f;
    private const float MaxHelmetHorizontalDistance = 0.9f;

    private static readonly FieldInfo? HelmetItemInstanceField =
        AccessTools.Field(typeof(VisEquipment), "m_helmetItemInstance");

    private static readonly Dictionary<Renderer, RendererState> OriginalRendererStates = new();
    private static readonly Dictionary<VisEquipment, HashSet<Renderer>> HelmetChangeSnapshots = new();
    private static readonly HashSet<Renderer> DesiredRenderers = new();
    private static readonly HashSet<Renderer> ObservedHelmetRenderers = new();
    private static readonly HashSet<Renderer> WarnedUnsafeRenderers = new();
    private static readonly HashSet<string> HelmetPrefabFingerprints = new();
    private static readonly List<Renderer?> RenderersToRemove = new();
    private static readonly List<VisEquipment?> SnapshotsToRemove = new();

    private static Player? _cachedPlayer;
    private static bool _active;
    private static bool _dirty = true;
    private static float _nextRefreshTime;

    private readonly struct RendererState
    {
        internal readonly bool Enabled;
        internal readonly ShadowCastingMode ShadowCastingMode;
        internal readonly bool ReceiveShadows;
        internal readonly int Layer;
        internal readonly Material[] SharedMaterials;

        internal RendererState(Renderer renderer)
        {
            Enabled = renderer.enabled;
            ShadowCastingMode = renderer.shadowCastingMode;
            ReceiveShadows = renderer.receiveShadows;
            Layer = renderer.gameObject.layer;
            SharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
        }
    }

    private static readonly string[] FullBodyKeywords =
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

    private static readonly string[] HeadSlotKeywords =
    {
        "head",
        "helmet",
        "helm",
        "hat",
        "hood",
        "hair",
        "beard",
        "circlet",
        "crown",
        "headgear"
    };

    internal static void RequestRefresh()
    {
        _dirty = true;
        _nextRefreshTime = 0f;
    }

    internal static void Update(Player player, bool shouldHide)
    {
        if (!shouldHide || player == null || player != Player.m_localPlayer)
        {
            Restore();
            return;
        }

        if (_cachedPlayer != null && _cachedPlayer != player)
            Restore();

        _cachedPlayer = player;

        if (!_active)
        {
            // Active state starts with a clean renderer cache for exact restoration.
            OriginalRendererStates.Clear();
            DesiredRenderers.Clear();
            _active = true;
            _dirty = true;
            _nextRefreshTime = 0f;
        }

        if (_dirty || Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRendererCache(player);
            _dirty = false;
            _nextRefreshTime = Time.unscaledTime + Mathf.Clamp(Plugin.VisibilityRefreshInterval.Value, 0.1f, 10f);
        }

        ApplyShadowsOnly();
    }

    internal static void Restore()
    {
        RestoreRendererStates();
        DesiredRenderers.Clear();
        ObservedHelmetRenderers.Clear();
        HelmetChangeSnapshots.Clear();
        WarnedUnsafeRenderers.Clear();
        HelmetPrefabFingerprints.Clear();
        RenderersToRemove.Clear();
        SnapshotsToRemove.Clear();
        _cachedPlayer = null;
        _active = false;
        _dirty = true;
        _nextRefreshTime = 0f;
    }

    internal static bool IsManagedRenderer(Renderer renderer)
    {
        return renderer != null &&
               (DesiredRenderers.Contains(renderer) || OriginalRendererStates.ContainsKey(renderer));
    }

    internal static void CopyManagedRenderers(List<Renderer> target)
    {
        target.Clear();
        RemoveDestroyedRenderers();

        // The head shrink fallback uses this list only for transform compensation.
        foreach (Renderer renderer in DesiredRenderers)
        {
            if (renderer != null)
                target.Add(renderer);
        }
    }

    internal static void CaptureHelmetChangeStart(VisEquipment visEquipment)
    {
        if (!ShouldObserveHelmetChange(visEquipment))
            return;

        // Prefix snapshots let us detect helmet renderers that were added outside the normal helmet joint.
        HashSet<Renderer> snapshot = new(visEquipment.GetComponentsInChildren<Renderer>(true));
        HelmetChangeSnapshots[visEquipment] = snapshot;
    }

    internal static void CaptureHelmetChangeEnd(VisEquipment visEquipment, bool changed)
    {
        if (visEquipment == null)
            return;

        if (!HelmetChangeSnapshots.TryGetValue(visEquipment, out HashSet<Renderer> beforeRenderers))
            return;

        HelmetChangeSnapshots.Remove(visEquipment);

        if (!changed || !PlayerEquipmentAccess.IsLocalPlayerVisEquipment(visEquipment))
        {
            RequestRefresh();
            return;
        }

        // Renderers that appear during SetHelmetEquipped are strong helmet-only candidates.
        Renderer[] afterRenderers = visEquipment.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in afterRenderers)
        {
            if (renderer == null || beforeRenderers.Contains(renderer))
                continue;

            if (IsSafeHelmetRenderer(visEquipment, renderer, true, "helmet equip delta"))
                ObservedHelmetRenderers.Add(renderer);
        }

        RequestRefresh();
    }

    private static bool ShouldObserveHelmetChange(VisEquipment visEquipment)
    {
        return visEquipment != null &&
               FirstPersonState.Active &&
               PlayerEquipmentAccess.IsLocalPlayerVisEquipment(visEquipment);
    }

    private static void RefreshRendererCache(Player player)
    {
        RemoveDestroyedRenderers();
        RemoveDestroyedObservedRenderers();
        RemoveDeadSnapshots();
        DesiredRenderers.Clear();

        if (!PlayerEquipmentAccess.TryGetVisEquipment(player, out VisEquipment visEquipment))
            return;

        AddHelmetInstanceRenderers(visEquipment);
        AddObservedHelmetRenderers(visEquipment);
        AddHelmetJointRenderers(visEquipment);
        AddPrefabMatchedHelmetRenderers(player, visEquipment);
        RestoreNoLongerDesiredRenderers();
        CaptureNewDesiredRendererStates();
    }

    private static void AddHelmetInstanceRenderers(VisEquipment visEquipment)
    {
        if (HelmetItemInstanceField == null)
            return;

        object? helmetValue = HelmetItemInstanceField.GetValue(visEquipment);

        if (helmetValue is not GameObject helmetInstance)
            return;

        foreach (Renderer renderer in helmetInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (IsSafeHelmetRenderer(visEquipment, renderer, true, "helmet item instance"))
                DesiredRenderers.Add(renderer);
        }
    }

    private static void AddObservedHelmetRenderers(VisEquipment visEquipment)
    {
        foreach (Renderer renderer in ObservedHelmetRenderers)
        {
            if (IsSafeHelmetRenderer(visEquipment, renderer, true, "observed helmet renderer"))
                DesiredRenderers.Add(renderer);
        }
    }

    private static void AddHelmetJointRenderers(VisEquipment visEquipment)
    {
        if (visEquipment.m_helmet == null)
            return;

        foreach (Renderer renderer in visEquipment.m_helmet.GetComponentsInChildren<Renderer>(true))
        {
            if (IsSafeHelmetRenderer(visEquipment, renderer, true, "helmet joint hierarchy"))
                DesiredRenderers.Add(renderer);
        }
    }

    private static void AddPrefabMatchedHelmetRenderers(Player player, VisEquipment visEquipment)
    {
        int helmetHash = PlayerEquipmentAccess.GetHelmetHash(visEquipment);

        if (helmetHash == 0)
            return;

        BuildHelmetPrefabFingerprints(helmetHash);

        if (HelmetPrefabFingerprints.Count == 0)
            return;

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || DesiredRenderers.Contains(renderer))
                continue;

            if (!HelmetPrefabFingerprints.Contains(BuildRendererFingerprint(renderer)))
                continue;

            if (!IsRendererNearPlayerHead(player, renderer))
                continue;

            if (IsSafeHelmetRenderer(visEquipment, renderer, false, "helmet prefab fingerprint"))
                DesiredRenderers.Add(renderer);
        }
    }

    private static void BuildHelmetPrefabFingerprints(int helmetHash)
    {
        HelmetPrefabFingerprints.Clear();
        ObjectDB objectDB = ObjectDB.instance;

        if (objectDB == null)
            return;

        GameObject prefab = objectDB.GetItemPrefab(helmetHash);

        if (prefab == null)
            return;

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            string fingerprint = BuildRendererFingerprint(renderer);

            if (!string.IsNullOrEmpty(fingerprint))
                HelmetPrefabFingerprints.Add(fingerprint);
        }
    }

    private static bool IsSafeHelmetRenderer(VisEquipment visEquipment, Renderer renderer, bool confirmedHelmetSource, string source)
    {
        if (renderer == null || !IsRendererOwnedByLocalPlayer(renderer))
            return false;

        if (visEquipment.m_bodyModel != null && renderer == visEquipment.m_bodyModel)
        {
            WarnUnsafeRenderer(renderer, source, "it is the player body renderer");
            return false;
        }

        string descriptor = BuildRendererDescriptor(renderer);

        if (LooksLikeFullBodyRenderer(renderer, descriptor))
        {
            WarnUnsafeRenderer(renderer, source, "it appears to contain full body geometry");
            return false;
        }

        if (!confirmedHelmetSource && !IsRendererNearPlayerHead(Player.m_localPlayer, renderer))
            return false;

        return true;
    }

    private static bool IsRendererOwnedByLocalPlayer(Renderer renderer)
    {
        Player? player = Player.m_localPlayer;

        return player != null &&
               renderer != null &&
               renderer.transform != null &&
               player.transform != null &&
               (renderer.transform == player.transform || renderer.transform.IsChildOf(player.transform));
    }

    private static bool IsRendererNearPlayerHead(Player player, Renderer renderer)
    {
        if (player == null || renderer == null)
            return false;

        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        float largestExtent = Mathf.Max(size.x, size.y, size.z);

        if (largestExtent > MaxHelmetRendererExtent)
            return false;

        Vector3 localCenter = player.transform.InverseTransformPoint(bounds.center);

        if (localCenter.y < MinHelmetRendererHeight || localCenter.y > MaxHelmetRendererHeight)
            return false;

        Vector2 localHorizontal = new(localCenter.x, localCenter.z);
        return localHorizontal.magnitude <= MaxHelmetHorizontalDistance;
    }

    private static bool LooksLikeFullBodyRenderer(Renderer renderer, string descriptor)
    {
        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        float largestExtent = Mathf.Max(size.x, size.y, size.z);
        bool bodyName = ContainsAny(descriptor, FullBodyKeywords);
        bool headSlotName = ContainsAny(descriptor, HeadSlotKeywords);
        bool bodyScale = size.y > 1.2f && largestExtent > 1.5f;

        return (bodyName && !headSlotName) || bodyScale;
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

        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null)
                builder.Append(material.name).Append(' ');
        }

        return builder.ToString().ToLowerInvariant();
    }

    private static string BuildRendererFingerprint(Renderer renderer)
    {
        if (renderer == null)
            return string.Empty;

        StringBuilder builder = new();

        // Mesh plus material names identify runtime clones across renamed GameObjects.
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            builder.Append(skinnedMeshRenderer.sharedMesh.name);
        else if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            builder.Append(meshFilter.sharedMesh.name);

        builder.Append('|');

        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null)
                builder.Append(material.name).Append(';');
        }

        return builder.ToString();
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

    private static void WarnUnsafeRenderer(Renderer renderer, string source, string reason)
    {
        if (renderer == null || WarnedUnsafeRenderers.Contains(renderer))
            return;

        WarnedUnsafeRenderers.Add(renderer);
        Plugin.Log.LogWarning($"Skipped helmet ShadowsOnly renderer from {source} because {reason}: {BuildRendererDescriptor(renderer)}");
    }

    private static void RestoreNoLongerDesiredRenderers()
    {
        RenderersToRemove.Clear();

        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
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
    }

    private static void CaptureNewDesiredRendererStates()
    {
        foreach (Renderer renderer in DesiredRenderers)
        {
            if (renderer == null || OriginalRendererStates.ContainsKey(renderer))
                continue;

            OriginalRendererStates.Add(renderer, new RendererState(renderer));
            Plugin.HeadHidingDebugLog($"Helmet ShadowsOnly matched renderer: {BuildRendererDescriptor(renderer)}");
        }
    }

    private static void ApplyShadowsOnly()
    {
        foreach (Renderer renderer in OriginalRendererStates.Keys)
        {
            if (renderer == null)
                continue;

            if (!IsRendererOwnedByLocalPlayer(renderer))
                continue;

            if (!renderer.enabled)
                renderer.enabled = true;

            if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
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

            RestoreRenderer(renderer);
        }

        OriginalRendererStates.Clear();
    }

    private static void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (!OriginalRendererStates.TryGetValue(renderer, out RendererState originalState))
            return;

        renderer.enabled = originalState.Enabled;
        renderer.shadowCastingMode = originalState.ShadowCastingMode;
        renderer.receiveShadows = originalState.ReceiveShadows;
        renderer.sharedMaterials = originalState.SharedMaterials;

        if (renderer.gameObject != null)
            renderer.gameObject.layer = originalState.Layer;
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

        foreach (Renderer renderer in DesiredRenderers)
        {
            if (renderer == null)
                RenderersToRemove.Add(renderer);
        }

        foreach (Renderer? renderer in RenderersToRemove)
        {
            if (renderer is not null)
                DesiredRenderers.Remove(renderer);
        }

        RenderersToRemove.Clear();
    }

    private static void RemoveDestroyedObservedRenderers()
    {
        RenderersToRemove.Clear();

        foreach (Renderer renderer in ObservedHelmetRenderers)
        {
            if (renderer == null)
                RenderersToRemove.Add(renderer);
        }

        foreach (Renderer? renderer in RenderersToRemove)
        {
            if (renderer is not null)
                ObservedHelmetRenderers.Remove(renderer);
        }

        RenderersToRemove.Clear();
    }

    private static void RemoveDeadSnapshots()
    {
        SnapshotsToRemove.Clear();

        foreach (VisEquipment visEquipment in HelmetChangeSnapshots.Keys)
        {
            if (visEquipment == null)
                SnapshotsToRemove.Add(visEquipment);
        }

        foreach (VisEquipment? visEquipment in SnapshotsToRemove)
        {
            if (visEquipment is not null)
                HelmetChangeSnapshots.Remove(visEquipment);
        }

        SnapshotsToRemove.Clear();
    }
}
