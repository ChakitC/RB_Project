using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class SetSkillHitBoxData : MonoBehaviour
{
    const string LoadLayoutUndoLabel = "Load Skill HitBox Layout";
    const string CreateTemplateUndoLabel = "Create Skill HitBox Template";

    [SerializeField] private Transform sourceHitboxRoot;
    [SerializeField] private SkillHitBoxData skillHitBoxData;
    [SerializeField] private bool includeInactiveObjects = true;
    [SerializeField, MinValue(1), LabelText("Template Group Count")]
    [PropertyTooltip("Number of SkillHitboxGroup templates to create when pressing Create Source Template.")]
    private int templateGroupCount = 1;

    [Button("Create Source Template")]
    [PropertyTooltip("Create editable SkillHitboxGroup templates with default capsule colliders under Source Hitbox Root.")]
    private void CreateSourceTemplate()
    {
#if UNITY_EDITOR
        if (!TryGetEditableRoot(out Transform root))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(CreateTemplateUndoLabel);

        List<GameObject> createdObjects = new List<GameObject>();

        try
        {
            int groupCount = Mathf.Max(1, templateGroupCount);
            int layer = root.gameObject.layer;
            GameObject firstGroupObject = null;

            for (int i = 0; i < groupCount; i++)
            {
                string groupKey = GetNextTemplateGroupKey(root);
                GameObject groupObject = CreateTemplateGroup(root, groupKey, layer, createdObjects);

                if (firstGroupObject == null)
                    firstGroupObject = groupObject;
            }

            MarkHierarchyDirty(root);
            if (firstGroupObject != null)
            {
                Selection.activeObject = firstGroupObject;
                EditorGUIUtility.PingObject(firstGroupObject);
            }

            Debug.Log(
                $"Created {groupCount} SkillHitBox template group(s) under '{root.name}'.",
                root);
        }
        catch (Exception ex)
        {
            CleanupCreatedObjects(createdObjects);
            MarkHierarchyDirty(root);
            Debug.LogError($"Failed to create SkillHitBox template: {ex.Message}", this);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
#else
        Debug.LogWarning("CreateSourceTemplate is only available in the Unity Editor.", this);
#endif
    }

    static GameObject CreateTemplateGroup(
        Transform root,
        string groupKey,
        int layer,
        List<GameObject> createdObjects)
    {
        GameObject groupObject = CreateChildObject(
            root,
            groupKey,
            layer,
            createdObjects,
            CreateTemplateUndoLabel);

        SkillHitboxGroup group = groupObject.AddComponent<SkillHitboxGroup>();

        GameObject shapeObject = CreateChildObject(
            groupObject.transform,
            "HitBox01",
            layer,
            createdObjects,
            CreateTemplateUndoLabel);

        CapsuleCollider collider = shapeObject.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.enabled = false;
        collider.radius = 0.5f;
        collider.height = 1f;
        collider.direction = 1;

        group.Configure(groupKey, new[] { collider });
        return groupObject;
    }

    [Button("Load Layout From Data")]
    [PropertyTooltip("โหลด hit box จาก SkillHitBoxData ออกมาเป็น GameObject และ Collider ใต้ Source Hitbox Root เพื่อปรับตำแหน่ง ขนาด และรูปทรงใน scene")]
    private void LoadLayoutFromData()
    {
#if UNITY_EDITOR
        if (skillHitBoxData == null)
        {
            Debug.LogWarning("SkillHitBoxData is not assigned.", this);
            return;
        }

        if (!TryGetEditableRoot(out Transform root))
            return;

        IReadOnlyList<SkillHitBoxData.HitBoxGroupData> sourceGroups = skillHitBoxData.Groups;
        if (sourceGroups == null || sourceGroups.Count == 0)
        {
            Debug.LogWarning($"SkillHitBoxData '{skillHitBoxData.name}' has no groups to load.", skillHitBoxData);
            return;
        }

        if (!HasLoadableGroups(sourceGroups))
        {
            Debug.LogWarning($"SkillHitBoxData '{skillHitBoxData.name}' has no loadable hitbox shapes.", skillHitBoxData);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(LoadLayoutUndoLabel);

        List<GameObject> createdObjects = new List<GameObject>();

        try
        {
            int removedGroupCount = RemoveExistingSourceGroups(root);
            int createdGroupCount = 0;
            int createdColliderCount = 0;
            int layer = root.gameObject.layer;

            for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
            {
                SkillHitBoxData.HitBoxGroupData sourceGroup = sourceGroups[groupIndex];
                if (sourceGroup == null)
                {
                    Debug.LogWarning(
                        $"Skipping hitbox group at index {groupIndex} because it is null.",
                        skillHitBoxData);
                    continue;
                }

                sourceGroup.EnsureDefaults();
                string groupKey = sourceGroup.GroupKey;
                if (string.IsNullOrWhiteSpace(groupKey))
                {
                    Debug.LogWarning(
                        $"Skipping hitbox group at index {groupIndex} because the group key is empty.",
                        skillHitBoxData);
                    continue;
                }

                List<SkillHitBoxData.HitBoxShapeData> sourceShapes = sourceGroup.Shapes;
                if (sourceShapes == null || sourceShapes.Count == 0)
                {
                    Debug.LogWarning(
                        $"Skipping hitbox group '{groupKey}' because it has no shapes.",
                        skillHitBoxData);
                    continue;
                }

                GameObject groupObject = CreateChildObject(root, groupKey, layer, createdObjects);
                SkillHitboxGroup loadedGroup = groupObject.AddComponent<SkillHitboxGroup>();
                List<Collider> groupColliders = new List<Collider>(sourceShapes.Count);

                for (int shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
                {
                    SkillHitBoxData.HitBoxShapeData sourceShape = sourceShapes[shapeIndex];
                    if (sourceShape == null)
                    {
                        Debug.LogWarning(
                            $"Skipping hitbox shape at index {shapeIndex} in group '{groupKey}' because it is null.",
                            skillHitBoxData);
                        continue;
                    }

                    sourceShape.EnsureDefaults();

                    string shapeName = string.IsNullOrWhiteSpace(sourceShape.ShapeName)
                        ? $"HitBox{shapeIndex + 1:D2}"
                        : sourceShape.ShapeName;

                    GameObject shapeObject = CreateChildObject(groupObject.transform, shapeName, layer, createdObjects);
                    shapeObject.transform.localPosition = sourceShape.LocalPosition;
                    shapeObject.transform.localRotation = Quaternion.Euler(sourceShape.LocalEulerAngles);
                    shapeObject.transform.localScale = sourceShape.LocalScale;

                    if (!TryAddColliderFromShapeData(shapeObject, sourceShape, out Collider createdCollider, out string errorMessage))
                    {
                        createdObjects.Remove(shapeObject);
                        Undo.DestroyObjectImmediate(shapeObject);
                        Debug.LogWarning(
                            $"Skipping hitbox shape '{shapeName}' in group '{groupKey}': {errorMessage}",
                            skillHitBoxData);
                        continue;
                    }

                    createdCollider.isTrigger = true;
                    createdCollider.enabled = false;
                    groupColliders.Add(createdCollider);
                    createdColliderCount++;
                }

                if (groupColliders.Count == 0)
                {
                    createdObjects.Remove(groupObject);
                    Undo.DestroyObjectImmediate(groupObject);
                    Debug.LogWarning(
                        $"Skipping hitbox group '{groupKey}' because no colliders could be created.",
                        skillHitBoxData);
                    continue;
                }

                loadedGroup.Configure(groupKey, groupColliders);
                createdGroupCount++;
            }

            MarkHierarchyDirty(root);

            if (createdGroupCount == 0)
            {
                Debug.LogWarning(
                    $"Failed to load '{skillHitBoxData.name}' because no editable hitbox groups could be created.",
                    skillHitBoxData);
                return;
            }

            Debug.Log(
                $"Loaded SkillHitBoxData '{skillHitBoxData.name}' into '{root.name}': " +
                $"{createdGroupCount} groups / {createdColliderCount} colliders " +
                $"(removed {removedGroupCount} existing groups).",
                root);
        }
        catch (Exception ex)
        {
            CleanupCreatedObjects(createdObjects);
            MarkHierarchyDirty(root);
            Debug.LogError(
                $"Failed to load SkillHitBoxData '{skillHitBoxData.name}' into the scene: {ex.Message}",
                this);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
#else
        Debug.LogWarning("LoadLayoutFromData is only available in the Unity Editor.", this);
#endif
    }

    [Button("Save Layout From Source")]
    [PropertyTooltip("อ่าน SkillHitboxGroup และ Collider ใต้ Source Hitbox Root กลับไปบันทึกเป็น SkillHitBoxData หลังจากปรับแต่งใน scene เสร็จ")]
    private void SaveLayoutFromSource()
    {
        if (skillHitBoxData == null)
        {
            Debug.LogWarning("SkillHitBoxData is not assigned.", this);
            return;
        }

        Transform root = GetSourceRoot();
        SkillHitboxGroup[] sourceGroups = root.GetComponentsInChildren<SkillHitboxGroup>(includeInactiveObjects);
        if (sourceGroups == null || sourceGroups.Length == 0)
        {
            Debug.LogWarning($"No SkillHitboxGroup found under '{root.name}'.", this);
            return;
        }

        List<SkillHitBoxData.HitBoxGroupData> rebuiltGroups = new List<SkillHitBoxData.HitBoxGroupData>();
        int extractedColliderCount = 0;

        for (int i = 0; i < sourceGroups.Length; i++)
        {
            SkillHitboxGroup sourceGroup = sourceGroups[i];
            if (sourceGroup == null)
                continue;

            SkillHitBoxData.HitBoxGroupData groupData = new SkillHitBoxData.HitBoxGroupData
            {
                GroupKey = sourceGroup.GroupKey
            };

            IReadOnlyList<Collider> colliders = sourceGroup.Colliders;
            for (int colliderIndex = 0; colliderIndex < colliders.Count; colliderIndex++)
            {
                Collider collider = colliders[colliderIndex];
                if (!TryCreateShapeData(root, collider, out SkillHitBoxData.HitBoxShapeData shapeData))
                    continue;

                groupData.Shapes.Add(shapeData);
                extractedColliderCount++;
            }

            if (groupData.Shapes.Count == 0)
            {
                Debug.LogWarning(
                    $"Skipping group '{sourceGroup.GroupKey}' because no supported collider could be exported.",
                    sourceGroup);
                continue;
            }

            rebuiltGroups.Add(groupData);
        }

        if (rebuiltGroups.Count == 0)
        {
            Debug.LogWarning("No hitbox layout could be exported into SkillHitBoxData.", this);
            return;
        }

        skillHitBoxData.ReplaceGroups(rebuiltGroups);

        List<string> validationIssues = new List<string>();
        int issueCount = skillHitBoxData.CollectValidationIssues(validationIssues);
        MarkAssetDirty(skillHitBoxData);

        if (issueCount > 0)
        {
            Debug.LogWarning(
                $"Saved SkillHitBoxData from '{root.name}', but found {issueCount} validation issue(s).\n- " +
                string.Join("\n- ", validationIssues),
                skillHitBoxData);
            return;
        }

        Debug.Log(
            $"Saved SkillHitBoxData from '{root.name}': {rebuiltGroups.Count} groups / {extractedColliderCount} colliders.",
            skillHitBoxData);
    }

    [Button("Validate Current Data")]
    [PropertyTooltip("ตรวจสอบว่า SkillHitBoxData ปัจจุบันมี group key, shape และค่าของ collider ครบและถูกต้องหรือไม่")]
    private void ValidateCurrentData()
    {
        if (skillHitBoxData == null)
        {
            Debug.LogWarning("SkillHitBoxData is not assigned.", this);
            return;
        }

        List<string> validationIssues = new List<string>();
        int issueCount = skillHitBoxData.CollectValidationIssues(validationIssues);
        if (issueCount == 0)
        {
            Debug.Log($"SkillHitBoxData '{skillHitBoxData.name}' passed validation.", skillHitBoxData);
            return;
        }

        Debug.LogWarning(
            $"SkillHitBoxData '{skillHitBoxData.name}' has {issueCount} validation issue(s).\n- " +
            string.Join("\n- ", validationIssues),
            skillHitBoxData);
    }

    static bool TryCreateShapeData(
        Transform root,
        Collider collider,
        out SkillHitBoxData.HitBoxShapeData shapeData)
    {
        shapeData = null;

        if (root == null || collider == null)
            return false;

        Transform colliderTransform = collider.transform;
        shapeData = new SkillHitBoxData.HitBoxShapeData
        {
            ShapeName = colliderTransform.name,
            LocalPosition = root.InverseTransformPoint(colliderTransform.position),
            LocalEulerAngles = (Quaternion.Inverse(root.rotation) * colliderTransform.rotation).eulerAngles,
            LocalScale = CalculateScaleRelativeToRoot(root, colliderTransform)
        };

        if (collider is BoxCollider box)
        {
            shapeData.Type = SkillHitBoxData.HitBoxType.Box;
            shapeData.Center = box.center;
            shapeData.Size = box.size;
            shapeData.Radius = 0f;
            shapeData.Height = 0f;
            shapeData.Direction = 1;
            return true;
        }

        if (collider is CapsuleCollider capsule)
        {
            shapeData.Type = SkillHitBoxData.HitBoxType.Capsule;
            shapeData.Center = capsule.center;
            shapeData.Size = Vector3.zero;
            shapeData.Radius = capsule.radius;
            shapeData.Height = capsule.height;
            shapeData.Direction = capsule.direction;
            return true;
        }

        if (collider is SphereCollider sphere)
        {
            shapeData.Type = SkillHitBoxData.HitBoxType.Sphere;
            shapeData.Center = sphere.center;
            shapeData.Size = Vector3.zero;
            shapeData.Radius = sphere.radius;
            shapeData.Height = 0f;
            shapeData.Direction = 1;
            return true;
        }

        Debug.LogWarning($"Collider type '{collider.GetType().Name}' is not supported.", collider);
        shapeData = null;
        return false;
    }

    static bool HasLoadableGroups(IReadOnlyList<SkillHitBoxData.HitBoxGroupData> sourceGroups)
    {
        if (sourceGroups == null || sourceGroups.Count == 0)
            return false;

        for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
        {
            SkillHitBoxData.HitBoxGroupData sourceGroup = sourceGroups[groupIndex];
            if (sourceGroup == null)
                continue;

            string groupKey = sourceGroup.GroupKey;
            if (string.IsNullOrWhiteSpace(groupKey))
                continue;

            List<SkillHitBoxData.HitBoxShapeData> sourceShapes = sourceGroup.Shapes;
            if (sourceShapes == null || sourceShapes.Count == 0)
                continue;

            for (int shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
            {
                if (sourceShapes[shapeIndex] != null)
                    return true;
            }
        }

        return false;
    }

    static bool TryAddColliderFromShapeData(
        GameObject shapeObject,
        SkillHitBoxData.HitBoxShapeData sourceShape,
        out Collider createdCollider,
        out string errorMessage)
    {
        createdCollider = null;
        errorMessage = null;

        if (shapeObject == null || sourceShape == null)
        {
            errorMessage = "shape input is null";
            return false;
        }

        switch (sourceShape.Type)
        {
            case SkillHitBoxData.HitBoxType.Box:
                BoxCollider box = shapeObject.AddComponent<BoxCollider>();
                box.center = sourceShape.Center;
                box.size = sourceShape.Size;
                createdCollider = box;
                return true;

            case SkillHitBoxData.HitBoxType.Capsule:
                CapsuleCollider capsule = shapeObject.AddComponent<CapsuleCollider>();
                capsule.center = sourceShape.Center;
                capsule.radius = sourceShape.Radius;
                capsule.height = sourceShape.Height;
                capsule.direction = sourceShape.Direction;
                createdCollider = capsule;
                return true;

            case SkillHitBoxData.HitBoxType.Sphere:
                SphereCollider sphere = shapeObject.AddComponent<SphereCollider>();
                sphere.center = sourceShape.Center;
                sphere.radius = sourceShape.Radius;
                createdCollider = sphere;
                return true;

            default:
                errorMessage = $"unsupported shape type '{sourceShape.Type}'";
                return false;
        }
    }

    static GameObject CreateChildObject(
        Transform parent,
        string objectName,
        int layer,
        List<GameObject> createdObjects,
        string undoLabel = LoadLayoutUndoLabel)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObject.layer = layer;
        createdObject.transform.SetParent(parent, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(createdObject, undoLabel);
#endif
        createdObjects?.Add(createdObject);
        return createdObject;
    }

    static void CleanupCreatedObjects(List<GameObject> createdObjects)
    {
        if (createdObjects == null)
            return;

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            GameObject createdObject = createdObjects[i];
            if (createdObject == null)
                continue;

            UnityEngine.Object.DestroyImmediate(createdObject);
        }
    }

    Transform GetSourceRoot()
    {
        return sourceHitboxRoot != null ? sourceHitboxRoot : transform;
    }

    bool TryGetEditableRoot(out Transform root)
    {
        root = GetSourceRoot();
        if (root == null)
        {
            Debug.LogWarning("Could not resolve a source hitbox root.", this);
            return false;
        }

        if (root.GetComponent<SkillHitboxGroup>() != null)
        {
            Debug.LogWarning(
                $"Source Hitbox Root '{root.name}' must be a container, not a SkillHitboxGroup itself.",
                root);
            return false;
        }

        return true;
    }

    static Vector3 CalculateScaleRelativeToRoot(Transform root, Transform target)
    {
        Vector3 rootScale = root.lossyScale;
        Vector3 targetScale = target.lossyScale;

        return new Vector3(
            SafeDivide(targetScale.x, rootScale.x),
            SafeDivide(targetScale.y, rootScale.y),
            SafeDivide(targetScale.z, rootScale.z));
    }

    static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Abs(denominator) < 0.0001f ? numerator : numerator / denominator;
    }

    static int RemoveExistingSourceGroups(Transform root)
    {
#if UNITY_EDITOR
        if (root == null)
            return 0;

        SkillHitboxGroup[] existingGroups = root.GetComponentsInChildren<SkillHitboxGroup>(true);
        int removedGroupCount = 0;

        for (int i = existingGroups.Length - 1; i >= 0; i--)
        {
            SkillHitboxGroup existingGroup = existingGroups[i];
            if (existingGroup == null || existingGroup.transform == root)
                continue;

            Undo.DestroyObjectImmediate(existingGroup.gameObject);
            removedGroupCount++;
        }

        return removedGroupCount;
#else
        return 0;
#endif
    }

    static string GetNextTemplateGroupKey(Transform root)
    {
        const string groupPrefix = "Group";

        HashSet<string> usedGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root != null)
        {
            SkillHitboxGroup[] existingGroups = root.GetComponentsInChildren<SkillHitboxGroup>(true);
            for (int i = 0; i < existingGroups.Length; i++)
            {
                SkillHitboxGroup existingGroup = existingGroups[i];
                if (existingGroup == null)
                    continue;

                string existingKey = existingGroup.GroupKey;
                if (!string.IsNullOrWhiteSpace(existingKey))
                    usedGroupKeys.Add(existingKey.Trim());
            }
        }

        for (int i = 1; i < 1000; i++)
        {
            string candidate = $"{groupPrefix}{i:D2}";
            if (!usedGroupKeys.Contains(candidate))
                return candidate;
        }

        return $"{groupPrefix}{usedGroupKeys.Count + 1:D2}";
    }

    static void MarkHierarchyDirty(Transform root)
    {
#if UNITY_EDITOR
        if (root == null)
            return;

        EditorUtility.SetDirty(root.gameObject);

        if (root.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
#endif
    }

    static void MarkAssetDirty(SkillHitBoxData data)
    {
#if UNITY_EDITOR
        if (data == null)
            return;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
#endif
    }
}
