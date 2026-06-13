using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillHitboxLayoutData
{
    [SerializeField] private List<HitBoxGroupData> groups = new List<HitBoxGroupData>();

    public IReadOnlyList<HitBoxGroupData> Groups => groups != null ? groups : Array.Empty<HitBoxGroupData>();
    public bool HasGroups => groups != null && groups.Count > 0;

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

    public void ReplaceGroups(List<HitBoxGroupData> newGroups)
    {
        groups = newGroups ?? new List<HitBoxGroupData>();
        EnsureDefaults();
    }

    public void CopyFrom(IReadOnlyList<HitBoxGroupData> sourceGroups)
    {
        groups = CloneGroups(sourceGroups);
        EnsureDefaults();
    }

    public int CollectValidationIssues(List<string> issues)
    {
        int issueCount = 0;

        if (groups == null || groups.Count == 0)
        {
            AddIssue(issues, "Hitbox layout has no groups configured.", ref issueCount);
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

    public void EnsureDefaults()
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
                shapes[j]?.EnsureDefaults();
        }
    }

    private static List<HitBoxGroupData> CloneGroups(IReadOnlyList<HitBoxGroupData> sourceGroups)
    {
        var clonedGroups = new List<HitBoxGroupData>();
        if (sourceGroups == null)
            return clonedGroups;

        for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
        {
            HitBoxGroupData sourceGroup = sourceGroups[groupIndex];
            if (sourceGroup == null)
            {
                clonedGroups.Add(null);
                continue;
            }

            var clonedGroup = new HitBoxGroupData
            {
                GroupKey = sourceGroup.GroupKey
            };

            List<HitBoxShapeData> sourceShapes = sourceGroup.Shapes;
            for (int shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
            {
                HitBoxShapeData sourceShape = sourceShapes[shapeIndex];
                if (sourceShape == null)
                {
                    clonedGroup.Shapes.Add(null);
                    continue;
                }

                clonedGroup.Shapes.Add(new HitBoxShapeData
                {
                    ShapeName = sourceShape.ShapeName,
                    Type = sourceShape.Type,
                    LocalPosition = sourceShape.LocalPosition,
                    LocalEulerAngles = sourceShape.LocalEulerAngles,
                    LocalScale = sourceShape.LocalScale,
                    Center = sourceShape.Center,
                    Size = sourceShape.Size,
                    Radius = sourceShape.Radius,
                    Height = sourceShape.Height,
                    Direction = sourceShape.Direction
                });
            }

            clonedGroups.Add(clonedGroup);
        }

        return clonedGroups;
    }

    private static void AddIssue(List<string> issues, string message, ref int issueCount)
    {
        issueCount++;
        issues?.Add(message);
    }

    private static string FormatGroupLabel(string groupKey, int groupIndex)
    {
        return string.IsNullOrWhiteSpace(groupKey)
            ? $"Group#{groupIndex + 1}"
            : groupKey;
    }

    private static bool HasNearZeroAxis(Vector3 value)
    {
        return Mathf.Abs(value.x) < 0.0001f ||
               Mathf.Abs(value.y) < 0.0001f ||
               Mathf.Abs(value.z) < 0.0001f;
    }
}
