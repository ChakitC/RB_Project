using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Skill/Skill Hit Box Data")]
public sealed class SkillHitBoxData : ScriptableObject
{
    [SerializeField] private List<HitBoxGroupData> groups = new List<HitBoxGroupData>();

    [FormerlySerializedAs("hitBoxDatas")]
    [SerializeField, HideInInspector] private List<LegacyHitBoxData> legacyHitBoxDatas = new List<LegacyHitBoxData>();

    [FormerlySerializedAs("hitBoxCollider")]
    [SerializeField, HideInInspector] private Collider legacyHitBoxCollider;

    public IReadOnlyList<HitBoxGroupData> Groups => groups != null ? groups : Array.Empty<HitBoxGroupData>();

    public enum HitBoxType
    {
        Box,
        Capsule,
        Sphere
    }

    [Serializable]
    public sealed class HitBoxGroupData
    {
        [SerializeField] private string groupKey = "Group01";
        [SerializeField] private List<HitBoxShapeData> shapes = new List<HitBoxShapeData>();

        public string GroupKey
        {
            get => string.IsNullOrWhiteSpace(groupKey) ? string.Empty : groupKey.Trim();
            set => groupKey = value;
        }

        public List<HitBoxShapeData> Shapes => shapes ?? (shapes = new List<HitBoxShapeData>());

        public void EnsureDefaults()
        {
            if (shapes == null)
                shapes = new List<HitBoxShapeData>();
        }
    }

    [Serializable]
    public sealed class HitBoxShapeData
    {
        [SerializeField] private string shapeName = "HitBox";
        [SerializeField] private HitBoxType type = HitBoxType.Capsule;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float height = 1f;
        [SerializeField, Range(0, 2)] private int direction = 1;

        public string ShapeName
        {
            get => string.IsNullOrWhiteSpace(shapeName) ? "HitBox" : shapeName.Trim();
            set => shapeName = value;
        }

        public HitBoxType Type
        {
            get => type;
            set => type = value;
        }

        public Vector3 LocalPosition
        {
            get => localPosition;
            set => localPosition = value;
        }

        public Vector3 LocalEulerAngles
        {
            get => localEulerAngles;
            set => localEulerAngles = value;
        }

        public Vector3 LocalScale
        {
            get => localScale;
            set => localScale = value;
        }

        public Vector3 Center
        {
            get => center;
            set => center = value;
        }

        public Vector3 Size
        {
            get => size;
            set => size = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        public float Height
        {
            get => height;
            set => height = value;
        }

        public int Direction
        {
            get => direction;
            set => direction = value;
        }

        public void EnsureDefaults()
        {
            if (Mathf.Approximately(localScale.x, 0f) &&
                Mathf.Approximately(localScale.y, 0f) &&
                Mathf.Approximately(localScale.z, 0f))
            {
                localScale = Vector3.one;
            }
        }
    }

    [Serializable]
    sealed class LegacyHitBoxData
    {
        public HitBoxType type;
        public Vector3 center;
        public Vector3 size;
        public float radius;
        public float height;
        public int direction;
    }

    void OnValidate()
    {
        EnsureDefaults();
        MigrateLegacyDataIfNeeded();
    }

    public void ReplaceGroups(List<HitBoxGroupData> newGroups)
    {
        groups = newGroups ?? new List<HitBoxGroupData>();
        EnsureDefaults();
        MigrateLegacyDataIfNeeded();
    }

    public int CollectValidationIssues(List<string> issues)
    {
        int issueCount = 0;

        if (groups == null || groups.Count == 0)
        {
            AddIssue(issues, "SkillHitBoxData has no hitbox groups configured.", ref issueCount);
            return issueCount;
        }

        HashSet<string> groupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < groups.Count; i++)
        {
            HitBoxGroupData group = groups[i];
            if (group == null)
            {
                AddIssue(issues, $"Hitbox group at index {i} is null.", ref issueCount);
                continue;
            }

            group.EnsureDefaults();
            string groupKey = group.GroupKey;
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                AddIssue(issues, $"Hitbox group at index {i} has an empty group key.", ref issueCount);
            }
            else if (!groupKeys.Add(groupKey))
            {
                AddIssue(issues, $"Duplicate hitbox group key '{groupKey}' detected.", ref issueCount);
            }

            List<HitBoxShapeData> shapes = group.Shapes;
            if (shapes.Count == 0)
            {
                AddIssue(issues, $"Hitbox group '{FormatGroupLabel(groupKey, i)}' has no shapes.", ref issueCount);
                continue;
            }

            for (int shapeIndex = 0; shapeIndex < shapes.Count; shapeIndex++)
            {
                HitBoxShapeData shape = shapes[shapeIndex];
                if (shape == null)
                {
                    AddIssue(
                        issues,
                        $"Hitbox shape {shapeIndex} in group '{FormatGroupLabel(groupKey, i)}' is null.",
                        ref issueCount);
                    continue;
                }

                shape.EnsureDefaults();

                if (HasNearZeroAxis(shape.LocalScale))
                {
                    AddIssue(
                        issues,
                        $"Hitbox shape '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' has a near-zero local scale.",
                        ref issueCount);
                }

                switch (shape.Type)
                {
                    case HitBoxType.Box:
                        if (shape.Size.x <= 0f || shape.Size.y <= 0f || shape.Size.z <= 0f)
                        {
                            AddIssue(
                                issues,
                                $"Box hitbox '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' must have a positive size on every axis.",
                                ref issueCount);
                        }
                        break;

                    case HitBoxType.Capsule:
                        if (shape.Radius <= 0f)
                        {
                            AddIssue(
                                issues,
                                $"Capsule hitbox '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' must have a positive radius.",
                                ref issueCount);
                        }

                        if (shape.Height <= 0f)
                        {
                            AddIssue(
                                issues,
                                $"Capsule hitbox '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' must have a positive height.",
                                ref issueCount);
                        }

                        if (shape.Direction < 0 || shape.Direction > 2)
                        {
                            AddIssue(
                                issues,
                                $"Capsule hitbox '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' has an invalid direction '{shape.Direction}'.",
                                ref issueCount);
                        }
                        break;

                    case HitBoxType.Sphere:
                        if (shape.Radius <= 0f)
                        {
                            AddIssue(
                                issues,
                                $"Sphere hitbox '{shape.ShapeName}' in group '{FormatGroupLabel(groupKey, i)}' must have a positive radius.",
                                ref issueCount);
                        }
                        break;
                }
            }
        }

        return issueCount;
    }

    void EnsureDefaults()
    {
        if (groups == null)
            groups = new List<HitBoxGroupData>();

        for (int i = 0; i < groups.Count; i++)
        {
            HitBoxGroupData group = groups[i];
            if (group == null)
                continue;

            group.EnsureDefaults();

            List<HitBoxShapeData> shapes = group.Shapes;
            for (int j = 0; j < shapes.Count; j++)
            {
                HitBoxShapeData shape = shapes[j];
                if (shape == null)
                    continue;

                shape.EnsureDefaults();
            }
        }
    }

    void MigrateLegacyDataIfNeeded()
    {
        if (legacyHitBoxCollider != null &&
            (legacyHitBoxDatas == null || legacyHitBoxDatas.Count == 0))
        {
            legacyHitBoxCollider = null;
        }

        if ((groups != null && groups.Count > 0) ||
            legacyHitBoxDatas == null ||
            legacyHitBoxDatas.Count == 0)
        {
            return;
        }

        HitBoxGroupData migratedGroup = new HitBoxGroupData
        {
            GroupKey = "LegacyGroup01"
        };

        for (int i = 0; i < legacyHitBoxDatas.Count; i++)
        {
            LegacyHitBoxData legacyData = legacyHitBoxDatas[i];
            if (legacyData == null)
                continue;

            migratedGroup.Shapes.Add(new HitBoxShapeData
            {
                ShapeName = $"LegacyHitBox{i + 1}",
                Type = legacyData.type,
                LocalPosition = Vector3.zero,
                LocalEulerAngles = Vector3.zero,
                LocalScale = Vector3.one,
                Center = legacyData.center,
                Size = legacyData.size,
                Radius = legacyData.radius,
                Height = legacyData.height,
                Direction = legacyData.direction
            });
        }

        if (migratedGroup.Shapes.Count > 0)
        {
            groups.Add(migratedGroup);
            legacyHitBoxDatas.Clear();
            legacyHitBoxCollider = null;
        }
    }

    static void AddIssue(List<string> issues, string message, ref int issueCount)
    {
        issueCount++;
        issues?.Add(message);
    }

    static string FormatGroupLabel(string groupKey, int groupIndex)
    {
        return string.IsNullOrWhiteSpace(groupKey)
            ? $"Group#{groupIndex + 1}"
            : groupKey;
    }

    static bool HasNearZeroAxis(Vector3 value)
    {
        return Mathf.Abs(value.x) < 0.0001f ||
               Mathf.Abs(value.y) < 0.0001f ||
               Mathf.Abs(value.z) < 0.0001f;
    }
}
