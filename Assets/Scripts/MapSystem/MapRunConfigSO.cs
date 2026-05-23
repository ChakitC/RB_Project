using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WeightedMapNodeType
{
    [Tooltip("ชนิด node ที่จะถูกสุ่มเลือก")]
    public MapNodeType type = MapNodeType.Combat;

    [Tooltip("น้ำหนักสุ่ม ยิ่งมากยิ่งมีโอกาสถูกเลือกมาก")]
    [Min(0f)] public float weight = 1f;
}

[CreateAssetMenu(menuName = "Game/Map/Run Config")]
public class MapRunConfigSO : ScriptableObject
{
    [Header("Seed")]
    [Tooltip("ถ้าเปิด จะสุ่ม seed ใหม่ทุกครั้งที่เริ่ม run")]
    [SerializeField] private bool randomizeSeed = true;

    [Tooltip("seed คงที่สำหรับทดสอบ map เดิมซ้ำ ใช้เมื่อปิด Randomize Seed")]
    [SerializeField] private int seed;

    [Header("Shape")]
    [Tooltip("จำนวน node บนเส้นหลักตั้งแต่ Start ถึง Boss")]
    [SerializeField, Min(4)] private int criticalPathNodeCount = 6;

    [Tooltip("จำนวนทางแยกเสริมขั้นต่ำต่อ run")]
    [SerializeField, Min(0)] private int minBranchCount = 1;

    [Tooltip("จำนวนทางแยกเสริมสูงสุดต่อ run")]
    [SerializeField, Min(0)] private int maxBranchCount = 3;

    [Tooltip("จำนวนประตูออกสูงสุดที่ node หนึ่ง node มีได้")]
    [SerializeField, Range(1, 4)] private int maxOutgoingPerNode = 3;

    [Tooltip("บังคับให้มีห้องน้ำเงินก่อนถึง Boss บนเส้นหลัก")]
    [SerializeField] private bool forceBlueBeforeBoss = true;

    [Tooltip("กฎช่วยลดการเจอห้องแดงติดกันนานเกินไป")]
    [SerializeField] private MapPitySystem pitySystem = new();

    [Header("Node Weights")]
    [Tooltip("น้ำหนักสุ่มชนิดห้องบนเส้นหลักช่วงกลาง run")]
    [SerializeField] private WeightedMapNodeType[] mainPathWeights =
    {
        new() { type = MapNodeType.Combat, weight = 6f },
        new() { type = MapNodeType.Elite, weight = 1f },
        new() { type = MapNodeType.Ambush, weight = 1f }
    };

    [Tooltip("น้ำหนักสุ่มชนิดห้องน้ำเงิน เช่น Reward, Shop, Heal, Upgrade")]
    [SerializeField] private WeightedMapNodeType[] blueWeights =
    {
        new() { type = MapNodeType.Reward, weight = 4f },
        new() { type = MapNodeType.Shop, weight = 1f },
        new() { type = MapNodeType.Heal, weight = 1f },
        new() { type = MapNodeType.Upgrade, weight = 1f }
    };

    [Tooltip("น้ำหนักสุ่มชนิดห้องปลายทางตัน ทางตันควรมีรางวัลหรือความคุ้มค่าเสมอ")]
    [SerializeField] private WeightedMapNodeType[] branchDeadEndWeights =
    {
        new() { type = MapNodeType.Reward, weight = 5f },
        new() { type = MapNodeType.Elite, weight = 2f },
        new() { type = MapNodeType.Event, weight = 1f }
    };

    [Header("Content Pools")]
    [Tooltip("รายการ room definition ที่ generator ใช้เลือก prefab ห้องตามชนิด node")]
    [SerializeField] private RoomDefinitionSO[] roomDefinitions;

    [Tooltip("รายการ encounter definition ที่ generator ใช้เลือกศัตรูและ wave ตามชนิด node")]
    [SerializeField] private EncounterDefinitionSO[] encounterDefinitions;

    public bool RandomizeSeed => randomizeSeed;
    public int Seed => seed;
    public int CriticalPathNodeCount => Mathf.Max(4, criticalPathNodeCount);
    public int MinBranchCount => Mathf.Max(0, minBranchCount);
    public int MaxBranchCount => Mathf.Max(MinBranchCount, maxBranchCount);
    public int MaxOutgoingPerNode => Mathf.Clamp(maxOutgoingPerNode, 1, 4);
    public bool ForceBlueBeforeBoss => forceBlueBeforeBoss;
    public MapPitySystem PitySystem => pitySystem ?? new MapPitySystem();
    public WeightedMapNodeType[] MainPathWeights => mainPathWeights;
    public WeightedMapNodeType[] BlueWeights => blueWeights;
    public WeightedMapNodeType[] BranchDeadEndWeights => branchDeadEndWeights;
    public RoomDefinitionSO[] RoomDefinitions => roomDefinitions;
    public EncounterDefinitionSO[] EncounterDefinitions => encounterDefinitions;
}

public static class MapRunConfigValidator
{
    public static bool Validate(MapRunConfigSO config, out string error)
    {
        error = string.Empty;
        if (config == null)
        {
            error = "Map run config is missing.";
            return false;
        }

        var errors = new List<string>();
        ValidateRoomDefinitions(config, errors);
        ValidateRequiredRoomType(config, MapNodeType.Start, "Start node", 1, errors);
        ValidateRequiredRoomType(config, MapNodeType.Boss, "Boss node", 1, errors);
        ValidateFallbackRoomType(config, config.PitySystem.ForcedBlueType, "PitySystem forced blue type", 2, errors);
        ValidateWeightedNodeTypes(config, "MainPathWeights", config.MainPathWeights, IsMainPathTypeAllowed, 2, errors);
        ValidateWeightedNodeTypes(config, "BlueWeights", config.BlueWeights, MapPitySystem.IsBlueNodeType, 2, errors);
        ValidateWeightedNodeTypes(config, "BranchDeadEndWeights", config.BranchDeadEndWeights, MapPitySystem.IsDeadEndRewardType, 1, errors);
        ValidateWeightedFallbackRoomType(config, "MainPathWeights", config.MainPathWeights, IsMainPathTypeAllowed, MapNodeType.Combat, 2, errors);
        if (config.ForceBlueBeforeBoss)
            ValidateWeightedFallbackRoomType(config, "BlueWeights", config.BlueWeights, MapPitySystem.IsBlueNodeType, MapNodeType.Reward, 2, errors);
        if (config.MaxBranchCount > 0)
            ValidateWeightedFallbackRoomType(config, "BranchDeadEndWeights", config.BranchDeadEndWeights, MapPitySystem.IsDeadEndRewardType, MapNodeType.Reward, 1, errors);
        ValidateBranchCapacity(config, errors);

        if (errors.Count == 0)
            return true;

        error = string.Join("\n", errors);
        return false;
    }

    static void ValidateRoomDefinitions(MapRunConfigSO config, List<string> errors)
    {
        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null || definitions.Length == 0)
        {
            errors.Add("RoomDefinitions is empty.");
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition == null)
            {
                errors.Add($"RoomDefinitions[{i}] is empty.");
            }
        }
    }

    static void ValidateRequiredRoomType(MapRunConfigSO config, MapNodeType type, string label, int requiredExitCount, List<string> errors)
    {
        if (!HasEnabledRoomDefinitionForType(config, type, requiredExitCount))
            errors.Add($"{label} requires an enabled room definition for {type} with at least {requiredExitCount} exits.");
    }

    static void ValidateFallbackRoomType(MapRunConfigSO config, MapNodeType type, string label, int requiredExitCount, List<string> errors)
    {
        if (!HasEnabledRoomDefinitionForType(config, type, requiredExitCount))
            errors.Add($"{label} requires an enabled room definition for {type} with at least {requiredExitCount} exits.");
    }

    static void ValidateWeightedFallbackRoomType(
        MapRunConfigSO config,
        string fieldName,
        WeightedMapNodeType[] weights,
        Func<MapNodeType, bool> isAllowedType,
        MapNodeType fallback,
        int requiredExitCount,
        List<string> errors)
    {
        if (HasUsableWeightedNodeType(config, weights, isAllowedType, requiredExitCount))
            return;

        if (!HasEnabledRoomDefinitionForType(config, fallback, requiredExitCount))
            errors.Add($"{fieldName} has no usable weighted entries, and fallback {fallback} has no enabled room definition with at least {requiredExitCount} exits.");
    }

    static void ValidateWeightedNodeTypes(
        MapRunConfigSO config,
        string fieldName,
        WeightedMapNodeType[] weights,
        Func<MapNodeType, bool> isAllowedType,
        int requiredExitCount,
        List<string> errors)
    {
        if (weights == null)
            return;

        for (int i = 0; i < weights.Length; i++)
        {
            WeightedMapNodeType entry = weights[i];
            if (entry == null || entry.weight <= 0f)
                continue;

            if (!isAllowedType(entry.type))
            {
                errors.Add($"{fieldName}[{i}] uses invalid node type {entry.type}.");
                continue;
            }

            if (!HasEnabledRoomDefinitionForType(config, entry.type, requiredExitCount))
                errors.Add($"{fieldName}[{i}] uses {entry.type}, but no enabled room definition supports it with at least {requiredExitCount} exits.");
        }
    }

    static void ValidateBranchCapacity(MapRunConfigSO config, List<string> errors)
    {
        int parentCount = Mathf.Max(0, config.CriticalPathNodeCount - 2);
        int branchSlotsPerParent = Mathf.Max(0, config.MaxOutgoingPerNode - 1);
        int availableBranchSlots = parentCount * branchSlotsPerParent;

        if (config.MinBranchCount > availableBranchSlots)
        {
            errors.Add(
                $"MinBranchCount {config.MinBranchCount} is higher than available branch slots {availableBranchSlots}. " +
                "Increase MaxOutgoingPerNode or CriticalPathNodeCount.");
        }
    }

    static bool IsMainPathTypeAllowed(MapNodeType type)
    {
        return type != MapNodeType.Start && type != MapNodeType.Boss;
    }

    static bool HasUsableWeightedNodeType(
        MapRunConfigSO config,
        WeightedMapNodeType[] weights,
        Func<MapNodeType, bool> isAllowedType,
        int requiredExitCount)
    {
        if (weights == null)
            return false;

        for (int i = 0; i < weights.Length; i++)
        {
            WeightedMapNodeType entry = weights[i];
            if (entry != null &&
                entry.weight > 0f &&
                isAllowedType(entry.type) &&
                HasEnabledRoomDefinitionForType(config, entry.type, requiredExitCount))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasEnabledRoomDefinitionForType(MapRunConfigSO config, MapNodeType type, int requiredExitCount)
    {
        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null)
            return false;

        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition != null &&
                definition.Weight > 0f &&
                definition.NodeType == type &&
                definition.MaxExitCount >= requiredExitCount &&
                definition.RoomPrefab != null)
            {
                return true;
            }
        }

        return false;
    }
}
