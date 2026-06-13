using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlacksmithTools;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class BlacksmithHeadHider
{
    private const string BodyPartControllerTypeName = "BlacksmithTools.BodyPartController";

    private static readonly List<int> EquippedHashes = new();
    private static readonly int[] HeadBoneIndexes = Util.BodyPartToBoneIndexes(BodypartSystem.bodyPart.Head);
    private static readonly Dictionary<string, BodypartEntrySnapshot> EntrySnapshots = new();

    private static Type? _bodyPartControllerType;
    private static MethodInfo? _setupMethod;
    private static MethodInfo? _fullUpdateMethod;
    private static VisEquipment? _activeVisEquipment;
    private static string? _activeKey;
    private static bool _active;
    private static bool _dirty = true;

    private sealed class BodypartEntrySnapshot
    {
        internal bool HadParts { get; set; }
        internal bool HadBones { get; set; }
        internal List<BodypartSystem.bodyPart>? Parts { get; set; }
        internal List<int>? Bones { get; set; }
    }

    internal static void RequestRefresh()
    {
        _dirty = true;
    }

    internal static bool TryApply(Player player, out string failureReason)
    {
        failureReason = string.Empty;

        // Blacksmith can only update the local player's visual equipment controller.
        if (!PlayerEquipmentAccess.TryGetVisEquipment(player, out VisEquipment visEquipment))
            return Fail("local VisEquipment was not found", out failureReason);

        if (_activeVisEquipment != null && _activeVisEquipment != visEquipment)
            Restore();

        if (!CanUseBlacksmith(visEquipment, out failureReason))
            return false;

        if (!TrySelectBodypartRuleKey(visEquipment, out string ruleKey, out failureReason))
            return false;

        if (_active && !_dirty && _activeVisEquipment == visEquipment && string.Equals(_activeKey, ruleKey, StringComparison.Ordinal))
            return true;

        // Re-keying removes the previous temporary rule before registering the new equipped item.
        if (_activeKey != null && !string.Equals(_activeKey, ruleKey, StringComparison.Ordinal))
        {
            RestoreBodypartEntries();
            _active = false;
            _activeKey = null;
        }

        RegisterHeadRule(ruleKey);

        if (!EnsureController(visEquipment, out Component? controller, out failureReason))
        {
            RestoreAfterFailedApply(visEquipment);
            return false;
        }

        if (controller == null || !TriggerBodyControllerUpdate(visEquipment, controller, out failureReason))
        {
            RestoreAfterFailedApply(visEquipment);
            return false;
        }

        if (!ValidateHeadHidden(visEquipment, out failureReason))
        {
            RestoreAfterFailedApply(visEquipment);
            return false;
        }

        _activeVisEquipment = visEquipment;
        _activeKey = ruleKey;
        _active = true;
        _dirty = false;
        Plugin.HeadHidingDebugLog($"Blacksmith head hide active using rule key '{ruleKey}'.");
        return true;
    }

    internal static void Restore()
    {
        VisEquipment? visEquipment = _activeVisEquipment;

        // Dictionary entries are restored even if the Unity objects were destroyed.
        RestoreBodypartEntries();

        if (visEquipment != null && TryGetController(visEquipment, out Component? controller) && controller != null)
        {
            if (TriggerBodyControllerUpdate(visEquipment, controller, out string reason))
                Plugin.HeadHidingDebugLog("Restored Blacksmith body mesh after first-person exit.");
            else
                Plugin.HeadHidingDebugLog($"Blacksmith restore update failed: {reason}.");
        }

        _activeVisEquipment = null;
        _activeKey = null;
        _active = false;
        _dirty = true;
    }

    private static bool CanUseBlacksmith(VisEquipment visEquipment, out string failureReason)
    {
        failureReason = string.Empty;

        // Blacksmith's own body hiding setting must be enabled because its controller is optional.
        if (Main.bodyHidingEnabled == null || !Main.bodyHidingEnabled.Value)
            return Fail("Blacksmith bodypart hiding is disabled", out failureReason);

        if (visEquipment.m_bodyModel == null)
            return Fail("VisEquipment has no body model renderer", out failureReason);

        Mesh mesh = visEquipment.m_bodyModel.sharedMesh;

        if (mesh == null)
            return Fail("body model has no shared mesh", out failureReason);

        if (!mesh.isReadable)
            return Fail("body model mesh is unreadable", out failureReason);

        return true;
    }

    private static bool TrySelectBodypartRuleKey(VisEquipment visEquipment, out string ruleKey, out string failureReason)
    {
        ruleKey = string.Empty;
        failureReason = string.Empty;

        // A helmet key is preferred because Blacksmith applies rules by equipped prefab hash.
        int helmetHash = PlayerEquipmentAccess.GetHelmetHash(visEquipment);

        if (helmetHash != 0 && PlayerEquipmentAccess.TryGetPrefabName(helmetHash, out ruleKey))
            return true;

        PlayerEquipmentAccess.GetEquippedHashes(visEquipment, EquippedHashes);

        foreach (int itemHash in EquippedHashes)
        {
            if (itemHash == 0)
                continue;

            if (PlayerEquipmentAccess.TryGetPrefabName(itemHash, out ruleKey))
                return true;
        }

        return Fail("no stable equipped item prefab name was available for Blacksmith body hiding", out failureReason);
    }

    private static void RegisterHeadRule(string ruleKey)
    {
        CaptureEntrySnapshot(ruleKey);

        // The public part dictionary keeps the runtime rule readable to Blacksmith's conversion path.
        List<BodypartSystem.bodyPart> parts = BodypartSystem.bodypartSettings.TryGetValue(ruleKey, out List<BodypartSystem.bodyPart> existingParts)
            ? new List<BodypartSystem.bodyPart>(existingParts)
            : new List<BodypartSystem.bodyPart>();

        if (!parts.Contains(BodypartSystem.bodyPart.Head))
            parts.Add(BodypartSystem.bodyPart.Head);

        BodypartSystem.bodypartSettings[ruleKey] = parts;

        // The bone dictionary is populated directly so internal updates do not depend on a later config conversion pass.
        List<int> bones = BodypartSystem.bodypartSettingsAsBones.TryGetValue(ruleKey, out List<int> existingBones)
            ? new List<int>(existingBones)
            : new List<int>();

        foreach (int headBoneIndex in HeadBoneIndexes)
        {
            if (!bones.Contains(headBoneIndex))
                bones.Add(headBoneIndex);
        }

        BodypartSystem.bodypartSettingsAsBones[ruleKey] = bones;
        Plugin.HeadHidingDebugLog($"Registered temporary Blacksmith Head rule for '{ruleKey}'.");
    }

    private static void CaptureEntrySnapshot(string ruleKey)
    {
        if (EntrySnapshots.ContainsKey(ruleKey))
            return;

        // Exact snapshots let first-person cleanup restore pre-existing Blacksmith config entries.
        bool hadParts = BodypartSystem.bodypartSettings.TryGetValue(ruleKey, out List<BodypartSystem.bodyPart> existingParts);
        bool hadBones = BodypartSystem.bodypartSettingsAsBones.TryGetValue(ruleKey, out List<int> existingBones);

        EntrySnapshots[ruleKey] = new BodypartEntrySnapshot
        {
            HadParts = hadParts,
            HadBones = hadBones,
            Parts = hadParts ? new List<BodypartSystem.bodyPart>(existingParts) : null,
            Bones = hadBones ? new List<int>(existingBones) : null
        };
    }

    private static bool EnsureController(VisEquipment visEquipment, out Component? controller, out string failureReason)
    {
        controller = null;
        failureReason = string.Empty;

        if (!TryResolveBodyPartControllerType(out Type controllerType, out failureReason))
            return false;

        controller = visEquipment.GetComponent(controllerType);

        if (controller != null)
            return true;

        if (!CanUseBlacksmith(visEquipment, out failureReason))
            return false;

        try
        {
            // Blacksmith's controller is internal, so setup uses reflection only when the component is missing.
            controller = visEquipment.gameObject.AddComponent(controllerType);
            MethodInfo? setupMethod = GetSetupMethod(controllerType);

            if (setupMethod == null)
                return Fail("Blacksmith BodyPartController.Setup method was not found", out failureReason);

            setupMethod.Invoke(controller, new object[] { visEquipment });
            Plugin.HeadHidingDebugLog("Attached missing Blacksmith BodyPartController to local VisEquipment.");
            return true;
        }
        catch (Exception ex)
        {
            return Fail($"Blacksmith BodyPartController setup failed: {ex.GetType().Name}", out failureReason);
        }
    }

    private static bool TryGetController(VisEquipment visEquipment, out Component? controller)
    {
        controller = null;

        if (!TryResolveBodyPartControllerType(out Type controllerType, out _))
            return false;

        controller = visEquipment != null ? visEquipment.GetComponent(controllerType) : null;
        return controller != null;
    }

    private static bool TriggerBodyControllerUpdate(VisEquipment visEquipment, Component controller, out string failureReason)
    {
        failureReason = string.Empty;

        try
        {
            // FullUpdate is public on the internal controller and rebuilds the active model entry.
            MethodInfo? fullUpdateMethod = GetFullUpdateMethod(controller.GetType());

            if (fullUpdateMethod == null)
                return Fail("Blacksmith BodyPartController.FullUpdate method was not found", out failureReason);

            fullUpdateMethod.Invoke(controller, Array.Empty<object>());
            ApplyCurrentModelMesh(visEquipment);
            return true;
        }
        catch (Exception ex)
        {
            return Fail($"Blacksmith BodyPartController update failed: {ex.GetType().Name}", out failureReason);
        }
    }

    private static void ApplyCurrentModelMesh(VisEquipment visEquipment)
    {
        if (visEquipment == null || visEquipment.m_bodyModel == null || visEquipment.m_models == null)
            return;

        // Valheim assigns m_models meshes to m_bodyModel during visual updates, so runtime rebuilds must mirror that.
        int modelIndex = visEquipment.GetModelIndex();

        if (modelIndex < 0 || modelIndex >= visEquipment.m_models.Length)
            return;

        Mesh mesh = visEquipment.m_models[modelIndex].m_mesh;

        if (mesh != null && visEquipment.m_bodyModel.sharedMesh != mesh)
            visEquipment.m_bodyModel.sharedMesh = mesh;
    }

    private static bool ValidateHeadHidden(VisEquipment visEquipment, out string failureReason)
    {
        failureReason = string.Empty;

        if (visEquipment.m_bodyModel == null)
            return Fail("body model renderer disappeared during Blacksmith validation", out failureReason);

        Mesh mesh = visEquipment.m_bodyModel.sharedMesh;

        if (mesh == null)
            return Fail("body model mesh disappeared during Blacksmith validation", out failureReason);

        if (!mesh.isReadable)
            return Fail("body model mesh became unreadable during Blacksmith validation", out failureReason);

        if (mesh.boneWeights == null || mesh.boneWeights.Length == 0)
            return Fail("body model mesh has no bone weights for Blacksmith validation", out failureReason);

        if (MeshContainsDominantHeadTriangles(mesh))
            return Fail("Blacksmith update left dominant head-weighted body triangles visible", out failureReason);

        return true;
    }

    private static bool MeshContainsDominantHeadTriangles(Mesh mesh)
    {
        BoneWeight[] boneWeights = mesh.boneWeights;

        // The validation mirrors Blacksmith's own triangle removal criteria without creating a new mesh.
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            int[] triangles = mesh.GetTriangles(subMeshIndex);

            for (int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
            {
                if (TriangleWouldBeRemovedByBlacksmith(boneWeights, triangles, triangleIndex))
                    return true;
            }
        }

        return false;
    }

    private static bool TriangleWouldBeRemovedByBlacksmith(BoneWeight[] boneWeights, int[] triangles, int triangleIndex)
    {
        int matchedDominantVertices = 0;

        // Blacksmith inspects the first two vertices and removes the triangle on the first dominant head match.
        for (int vertexOffset = 0; vertexOffset < 2; vertexOffset++)
        {
            int vertexIndex = triangles[triangleIndex + vertexOffset];

            if (vertexIndex < 0 || vertexIndex >= boneWeights.Length)
                continue;

            if (HasDominantHeadBone(boneWeights[vertexIndex]) && ++matchedDominantVertices == 1)
                return true;
        }

        return false;
    }

    private static bool HasDominantHeadBone(BoneWeight boneWeight)
    {
        float dominantWeight = Mathf.Max(boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3);

        if (dominantWeight <= 0f)
            return false;

        // A head bone must carry at least 90 percent of the dominant weight to match Blacksmith's hide rule.
        for (int boneSlot = 0; boneSlot < 4; boneSlot++)
        {
            int boneIndex = GetBoneIndex(boneWeight, boneSlot);

            if (!HeadBoneIndexes.Contains(boneIndex))
                continue;

            float weight = GetBoneWeight(boneWeight, boneSlot);

            if (weight / dominantWeight > 0.9f)
                return true;
        }

        return false;
    }

    private static int GetBoneIndex(BoneWeight boneWeight, int boneSlot)
    {
        return boneSlot switch
        {
            0 => boneWeight.boneIndex0,
            1 => boneWeight.boneIndex1,
            2 => boneWeight.boneIndex2,
            3 => boneWeight.boneIndex3,
            _ => -1
        };
    }

    private static float GetBoneWeight(BoneWeight boneWeight, int boneSlot)
    {
        return boneSlot switch
        {
            0 => boneWeight.weight0,
            1 => boneWeight.weight1,
            2 => boneWeight.weight2,
            3 => boneWeight.weight3,
            _ => 0f
        };
    }

    private static void RestoreAfterFailedApply(VisEquipment visEquipment)
    {
        RestoreBodypartEntries();

        if (TryGetController(visEquipment, out Component? controller) && controller != null)
            TriggerBodyControllerUpdate(visEquipment, controller, out _);

        _activeVisEquipment = null;
        _activeKey = null;
        _active = false;
        _dirty = true;
    }

    private static void RestoreBodypartEntries()
    {
        foreach (KeyValuePair<string, BodypartEntrySnapshot> entry in EntrySnapshots)
        {
            string ruleKey = entry.Key;
            BodypartEntrySnapshot snapshot = entry.Value;

            if (snapshot.HadParts && snapshot.Parts != null)
                BodypartSystem.bodypartSettings[ruleKey] = new List<BodypartSystem.bodyPart>(snapshot.Parts);
            else
                BodypartSystem.bodypartSettings.Remove(ruleKey);

            if (snapshot.HadBones && snapshot.Bones != null)
                BodypartSystem.bodypartSettingsAsBones[ruleKey] = new List<int>(snapshot.Bones);
            else
                BodypartSystem.bodypartSettingsAsBones.Remove(ruleKey);
        }

        EntrySnapshots.Clear();
    }

    private static bool TryResolveBodyPartControllerType(out Type controllerType, out string failureReason)
    {
        failureReason = string.Empty;
        _bodyPartControllerType ??= typeof(BodypartSystem).Assembly.GetType(BodyPartControllerTypeName, false);
        controllerType = _bodyPartControllerType!;

        if (controllerType == null)
            return Fail("Blacksmith BodyPartController type was not found", out failureReason);

        return true;
    }

    private static MethodInfo? GetSetupMethod(Type controllerType)
    {
        _setupMethod ??= controllerType.GetMethod("Setup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return _setupMethod;
    }

    private static MethodInfo? GetFullUpdateMethod(Type controllerType)
    {
        _fullUpdateMethod ??= controllerType.GetMethod("FullUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return _fullUpdateMethod;
    }

    private static bool Fail(string reason, out string failureReason)
    {
        failureReason = reason;
        Plugin.HeadHidingDebugLog($"Blacksmith head hiding unavailable: {reason}.");
        return false;
    }
}
