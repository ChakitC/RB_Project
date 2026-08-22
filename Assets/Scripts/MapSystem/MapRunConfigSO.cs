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
    [Header("Profiles")]
    [Tooltip("รูปร่างแผนที่และน้ำหนักสุ่มของด่านนี้ ต้องใส่เสมอ")]
    [SerializeField] private MapGenerationProfileSO generationProfile;

    [Tooltip("ชุด room และ encounter ที่ด่านนี้ใช้ ต้องใส่เสมอ")]
    [SerializeField] private MapContentPoolSO contentPool;

    [Tooltip("ความคืบหน้าและ XP ของด่าน ต้องใส่เมื่อเป็น Test Stage")]
    [SerializeField] private StageProgressionProfileSO progressionProfile;

    [Header("Stage Identity")]
    [Tooltip("รหัสด่านที่ใช้เก็บ save progress ห้ามเปลี่ยนหลังปล่อยบิลด์แล้ว")]
    [SerializeField] private string stageId;

    [Tooltip("รหัสเดิมของด่านนี้ ใช้ย้าย save progress เมื่อเปลี่ยน Stage Id")]
    [SerializeField] private string[] legacyStageIds;

    [Tooltip("ชื่อด่านที่แสดงบนบอร์ด")]
    [SerializeField] private string stageDisplayName;

    // Identity is the only tuning this asset owns. Everything else lives on a profile, so two
    // stages set in the same place can share one, and a value has exactly one home.
    //
    // A missing profile is a content error, not a runtime fallback: MapRunConfigValidator refuses
    // the config and StartRun bails before generating. The defaults below only keep the properties
    // safe to read while that error is being reported.

    public MapGenerationProfileSO GenerationProfile => generationProfile;
    public MapContentPoolSO ContentPool => contentPool;
    public StageProgressionProfileSO ProgressionProfile => progressionProfile;

    // --- Shape ---------------------------------------------------------------------------

    public bool RandomizeSeed => generationProfile == null || generationProfile.RandomizeSeed;
    public int Seed => generationProfile != null ? generationProfile.Seed : 0;
    public int CriticalPathNodeCount => generationProfile != null ? generationProfile.CriticalPathNodeCount : 2;
    public int MinBranchCount => generationProfile != null ? generationProfile.MinBranchCount : 0;
    public int MaxBranchCount => generationProfile != null ? generationProfile.MaxBranchCount : 0;
    public int MaxOutgoingPerNode => generationProfile != null ? generationProfile.MaxOutgoingPerNode : 1;
    public bool ForceBlueBeforeBoss => generationProfile != null && generationProfile.ForceBlueBeforeBoss;
    public MapPitySystem PitySystem => generationProfile != null ? generationProfile.PitySystem : new MapPitySystem();

    public WeightedMapNodeType[] MainPathWeights => generationProfile != null
        ? generationProfile.MainPathWeights
        : Array.Empty<WeightedMapNodeType>();

    public WeightedMapNodeType[] BlueWeights => generationProfile != null
        ? generationProfile.BlueWeights
        : Array.Empty<WeightedMapNodeType>();

    public WeightedMapNodeType[] BranchDeadEndWeights => generationProfile != null
        ? generationProfile.BranchDeadEndWeights
        : Array.Empty<WeightedMapNodeType>();

    // --- Content -------------------------------------------------------------------------

    public RoomDefinitionSO[] RoomDefinitions => contentPool != null
        ? contentPool.RoomDefinitions
        : Array.Empty<RoomDefinitionSO>();

    public EncounterDefinitionSO[] EncounterDefinitions => contentPool != null
        ? contentPool.EncounterDefinitions
        : Array.Empty<EncounterDefinitionSO>();

    // --- Stage identity and progression ---------------------------------------------------

    /// <summary>
    /// The key stage progress is saved under. It stays on the config rather than on a shared
    /// profile because it is per-stage identity, and it must be unique and immutable once shipped.
    /// </summary>
    public string StageId => stageId != null ? stageId.Trim() : string.Empty;

    /// <summary>
    /// Ids this stage was saved under before. When Stage Id has to change, the old id belongs here
    /// so saved progress is adopted rather than lost.
    /// </summary>
    public string[] LegacyStageIds => legacyStageIds;

    public string StageDisplayName => string.IsNullOrWhiteSpace(stageDisplayName) ? name : stageDisplayName;
    public bool IsTestStage => !string.IsNullOrWhiteSpace(StageId);

    public LevelTableSO LevelTable => progressionProfile != null ? progressionProfile.LevelTable : null;
    public int StartLevel => progressionProfile != null ? progressionProfile.StartLevel : 1;
    public int TargetLevel => progressionProfile != null ? progressionProfile.TargetLevel : StartLevel + 1;
    public int TargetRunCount => progressionProfile != null ? progressionProfile.TargetRunCount : 1;
    public int[] EnemyLevelTiers => progressionProfile != null ? progressionProfile.EnemyLevelTiers : null;
    public float RegularEnemyXpShare => progressionProfile != null ? progressionProfile.RegularEnemyXpShare : 0f;
    public float BossXpShare => progressionProfile != null ? progressionProfile.BossXpShare : 0f;
    public float CompletionXpShare => Mathf.Max(0f, 1f - RegularEnemyXpShare - BossXpShare);
    public GameObject StageExitPrefab => progressionProfile != null ? progressionProfile.StageExitPrefab : null;

    public int GetEnemyLevel(int stageProgressCount)
    {
        int[] tiers = EnemyLevelTiers;
        if (tiers == null || tiers.Length == 0)
            return StartLevel;

        int index = Mathf.Clamp(stageProgressCount, 0, tiers.Length - 1);
        return Mathf.Max(1, tiers[index]);
    }

    public int GetXpBudgetPerRun()
    {
        LevelTableSO table = LevelTable;
        if (table == null)
            return 0;

        long startXp = table.GetTotalXpToReach(StartLevel);
        long targetXp = table.GetTotalXpToReach(TargetLevel);
        long rangeXp = Math.Max(0L, targetXp - startXp);
        return Mathf.Max(0, Mathf.RoundToInt((float)rangeXp / TargetRunCount));
    }
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
        ValidateProfiles(config, errors);
        ValidateStageProgression(config, errors);
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

    /// <summary>
    /// Tuning lives on profiles, so a missing profile means the config has no shape or no content
    /// at all. Reporting it first keeps the rest of the errors from being noise about empty lists.
    /// </summary>
    static void ValidateProfiles(MapRunConfigSO config, List<string> errors)
    {
        if (config.GenerationProfile == null)
            errors.Add("Generation Profile is missing, so the run has no map shape or node weights.");

        if (config.ContentPool == null)
            errors.Add("Content Pool is missing, so the run has no rooms or encounters.");

        if (config.IsTestStage && config.ProgressionProfile == null)
            errors.Add("A Test Stage requires a Stage Progression Profile for its levels, run count, XP split, and Stage Exit prefab.");
    }

    static void ValidateStageProgression(MapRunConfigSO config, List<string> errors)
    {
        if (!config.IsTestStage)
            return;

        if (config.LevelTable == null)
            errors.Add("Test Stage requires a LevelTable.");
        if (config.TargetLevel > (config.LevelTable != null ? config.LevelTable.MaxLevel : int.MaxValue))
            errors.Add($"TargetLevel {config.TargetLevel} exceeds the LevelTable max level.");
        if (config.EnemyLevelTiers == null || config.EnemyLevelTiers.Length != config.TargetRunCount)
            errors.Add($"EnemyLevelTiers must contain exactly TargetRunCount ({config.TargetRunCount}) entries.");
        if (config.StageExitPrefab == null)
            errors.Add("Test Stage requires a StageExitPrefab.");
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
