using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    public static MapGraph Generate(MapRunConfigSO config)
    {
        int seed = ResolveSeed(config);
        return Generate(config, seed);
    }

    public static MapGraph Generate(MapRunConfigSO config, int seed)
    {
        UnityEngine.Random.State previousState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(seed);

        try
        {
            MapGraph graph = new MapGraph();
            graph.SetResolvedSeed(seed);
            int criticalCount = config != null ? config.CriticalPathNodeCount : 6;
            int bossIndex = criticalCount - 1;
            int forcedBlueIndex = Mathf.Max(1, bossIndex - 1);
            int redStreak = 0;

            var criticalNodes = new List<MapNode>();
            for (int i = 0; i < criticalCount; i++)
            {
                MapNodeType type = ResolveCriticalPathType(config, i, bossIndex, forcedBlueIndex, redStreak);
                string id = type == MapNodeType.Start ? "start" : type == MapNodeType.Boss ? "boss" : $"main_{i:00}";
                var node = new MapNode(id, type, i, true);
                graph.AddNode(node);
                criticalNodes.Add(node);

                redStreak = MapPitySystem.IsRedNodeType(type) ? redStreak + 1 : 0;
            }

            for (int i = 0; i < criticalNodes.Count - 1; i++)
                graph.AddEdge(criticalNodes[i].Id, criticalNodes[i + 1].Id);

            AddBranches(graph, criticalNodes, config);
            AssignExitDirections(graph, config);
            AssignDefinitions(graph, config);

            graph.StartNode?.Reveal();
            graph.StartNode?.Visit();
            graph.RevealOutgoing(graph.StartNode);

            return graph;
        }
        finally
        {
            UnityEngine.Random.state = previousState;
        }
    }

    static int ResolveSeed(MapRunConfigSO config)
    {
        if (config == null || config.RandomizeSeed)
            return Environment.TickCount;

        return config.Seed;
    }

    static MapNodeType ResolveCriticalPathType(
        MapRunConfigSO config,
        int index,
        int bossIndex,
        int forcedBlueIndex,
        int redStreak)
    {
        if (index == 0)
            return MapNodeType.Start;

        if (index == bossIndex)
            return MapNodeType.Boss;

        if (config != null && config.ForceBlueBeforeBoss && index == forcedBlueIndex)
            return ChooseWeighted(config.BlueWeights, MapNodeType.Reward, candidateType => IsUsableBlueNode(config, candidateType, 2));

        MapPitySystem pity = config != null ? config.PitySystem : new MapPitySystem();
        if (pity.ShouldForceBlue(redStreak))
        {
            MapNodeType forcedBlueType = pity.ForcedBlueType;
            if (IsUsableBlueNode(config, forcedBlueType, 2))
                return forcedBlueType;

            return ChooseWeighted(config != null ? config.BlueWeights : null, MapNodeType.Reward, candidateType => IsUsableBlueNode(config, candidateType, 2));
        }

        return ChooseWeighted(config != null ? config.MainPathWeights : null, MapNodeType.Combat, candidateType => IsUsableMainPathNode(config, candidateType, 2));
    }

    static void AddBranches(MapGraph graph, List<MapNode> criticalNodes, MapRunConfigSO config)
    {
        if (criticalNodes == null || criticalNodes.Count <= 2)
            return;

        int minBranch = config != null ? config.MinBranchCount : 1;
        int maxBranch = config != null ? config.MaxBranchCount : 2;
        int branchCount = UnityEngine.Random.Range(minBranch, maxBranch + 1);
        int maxOutgoing = config != null ? config.MaxOutgoingPerNode : 3;
        int created = 0;
        List<MapNode> candidates = BuildBranchParentCandidates(criticalNodes, maxOutgoing, config);

        while (created < branchCount && candidates.Count > 0)
        {
            int candidateIndex = UnityEngine.Random.Range(0, candidates.Count);
            MapNode parent = candidates[candidateIndex];

            int depth = parent.Depth + 1;
            MapNodeType type = ChooseWeighted(config != null ? config.BranchDeadEndWeights : null, MapNodeType.Reward, candidateType => IsUsableDeadEndNode(config, candidateType));
            string branchId = $"branch_{created:00}_{parent.Id}";
            var branch = new MapNode(branchId, type, depth, false);
            branch.SetDeadEndReward(MapPitySystem.IsDeadEndRewardType(type));

            graph.AddNode(branch);
            graph.AddEdge(parent.Id, branch.Id);
            created++;

            if (parent.OutgoingIds.Count >= maxOutgoing || !CanSupportAdditionalOutgoing(config, parent))
                candidates.RemoveAt(candidateIndex);
        }

        if (created < minBranch)
            Debug.LogWarning($"[MapGenerator] Created {created} branches, below MinBranchCount {minBranch}. Check CriticalPathNodeCount and MaxOutgoingPerNode.");
    }

    static List<MapNode> BuildBranchParentCandidates(List<MapNode> criticalNodes, int maxOutgoing, MapRunConfigSO config)
    {
        var candidates = new List<MapNode>();
        if (criticalNodes == null)
            return candidates;

        for (int i = 0; i < criticalNodes.Count - 1; i++)
        {
            MapNode node = criticalNodes[i];
            if (node != null &&
                node.Type != MapNodeType.Start &&
                node.Type != MapNodeType.Boss &&
                node.OutgoingIds.Count < maxOutgoing &&
                CanSupportAdditionalOutgoing(config, node))
            {
                candidates.Add(node);
            }
        }

        return candidates;
    }

    static bool CanSupportAdditionalOutgoing(MapRunConfigSO config, MapNode node)
    {
        if (config == null || node == null)
            return true;

        int returnExitCount = node.Type != MapNodeType.Start && node.Type != MapNodeType.Boss ? node.IncomingIds.Count : 0;
        int requiredExitCount = node.OutgoingIds.Count + 1 + returnExitCount;
        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null || definitions.Length == 0)
            return true;

        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition != null &&
                definition.Weight > 0f &&
                definition.RoomPrefab != null &&
                definition.NodeType == node.Type &&
                definition.MaxExitCount >= requiredExitCount)
            {
                return true;
            }
        }

        return false;
    }

    static void AssignDefinitions(MapGraph graph, MapRunConfigSO config)
    {
        if (graph == null)
            return;

        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            RoomDefinitionSO roomDefinition = ChooseRoomDefinition(config, graph, node, out int roomRotationSteps);
            node.SetRoomDefinition(roomDefinition, roomRotationSteps);
            node.SetEncounterDefinition(ChooseEncounterDefinition(config, node.Type));

            if (!node.HasDeadEndReward && node.OutgoingIds.Count == 0 && node.Type != MapNodeType.Boss)
                node.SetDeadEndReward(MapPitySystem.IsDeadEndRewardType(node.Type) || (node.RoomDefinition != null && node.RoomDefinition.HasClearReward));
        }
    }

    static void AssignExitDirections(MapGraph graph, MapRunConfigSO config)
    {
        if (graph == null)
            return;

        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            MapNode node = graph.Nodes[i];
            if (node == null)
                continue;

            node.SetOutgoingDirections(ChooseExitMask(graph, node, config));
        }
    }

    static RoomExitMask ChooseExitMask(MapGraph graph, MapNode node, MapRunConfigSO config)
    {
        if (node == null)
            return RoomExitMask.None;

        int outgoingCount = node.OutgoingIds.Count;
        if (outgoingCount <= 0)
            return RoomExitMask.None;

        RoomExitMask reservedMask = graph != null ? graph.GetReturnExitMask(node) : RoomExitMask.None;
        RoomExitMask preferredMask;

        if (node.Type == MapNodeType.Start && outgoingCount == 1)
        {
            preferredMask = RandomSingleExitMask();
        }
        else if (outgoingCount == 1)
        {
            preferredMask = BuildAvailableExitMask(1, reservedMask, RoomExitMask.Up);
        }
        else if (outgoingCount == 2)
        {
            if (reservedMask == RoomExitMask.None && MapPitySystem.IsBlueNodeType(node.Type))
            {
                preferredMask = RoomExitMask.Left | RoomExitMask.Right;
            }
            else
            {
                RoomExitMask preferredDirections = UnityEngine.Random.value < 0.5f
                    ? RoomExitMask.Up | RoomExitMask.Right
                    : RoomExitMask.Up | RoomExitMask.Left;

                preferredMask = BuildAvailableExitMask(2, reservedMask, preferredDirections);
            }
        }
        else if (outgoingCount == 3)
        {
            preferredMask = BuildAvailableExitMask(3, reservedMask, RoomExitMask.Left | RoomExitMask.Right | RoomExitMask.Up);
        }
        else
        {
            preferredMask = BuildAvailableExitMask(4, reservedMask, RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left);
        }

        return ChooseSupportedOutgoingMask(config, node.Type, outgoingCount, reservedMask, preferredMask);
    }

    static RoomExitMask ChooseSupportedOutgoingMask(
        MapRunConfigSO config,
        MapNodeType type,
        int outgoingCount,
        RoomExitMask reservedMask,
        RoomExitMask preferredMask)
    {
        if (HasSupportingRoomDefinition(config, type, reservedMask | preferredMask))
            return preferredMask;

        for (int value = 1; value <= (int)(RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left); value++)
        {
            RoomExitMask candidateMask = (RoomExitMask)value;
            if (RoomExitDirectionUtility.Count(candidateMask) != outgoingCount ||
                (candidateMask & reservedMask) != 0)
            {
                continue;
            }

            if (HasSupportingRoomDefinition(config, type, reservedMask | candidateMask))
                return candidateMask;
        }

        return preferredMask;
    }

    static bool HasSupportingRoomDefinition(MapRunConfigSO config, MapNodeType type, RoomExitMask requiredMask)
    {
        RoomDefinitionSO[] definitions = config != null ? config.RoomDefinitions : null;
        if (definitions == null || definitions.Length == 0)
            return true;

        int requiredExitCount = RoomExitDirectionUtility.Count(requiredMask);
        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition != null &&
                definition.Weight > 0f &&
                definition.RoomPrefab != null &&
                definition.NodeType == type &&
                definition.MaxExitCount >= requiredExitCount &&
                definition.TryGetRotationForExitMask(requiredMask, false, out _))
            {
                return true;
            }
        }

        return false;
    }

    static RoomExitMask RandomSingleExitMask()
    {
        return RoomExitDirectionUtility.ToMask((RoomExitDirection)UnityEngine.Random.Range(0, 4));
    }

    static RoomExitMask BuildAvailableExitMask(int requiredCount, RoomExitMask reservedMask, RoomExitMask preferredMask)
    {
        RoomExitMask mask = RoomExitMask.None;
        AddPreferredDirections(ref mask, preferredMask, reservedMask, requiredCount);
        AddDirectionIfAvailable(ref mask, RoomExitDirection.Up, reservedMask, requiredCount);
        AddDirectionIfAvailable(ref mask, RoomExitDirection.Right, reservedMask, requiredCount);
        AddDirectionIfAvailable(ref mask, RoomExitDirection.Left, reservedMask, requiredCount);
        AddDirectionIfAvailable(ref mask, RoomExitDirection.Down, reservedMask, requiredCount);
        return mask;
    }

    static void AddPreferredDirections(ref RoomExitMask mask, RoomExitMask preferredMask, RoomExitMask reservedMask, int requiredCount)
    {
        for (int i = 0; i < 4; i++)
            AddDirectionIfAvailable(ref mask, (RoomExitDirection)i, reservedMask, requiredCount, preferredMask);
    }

    static void AddDirectionIfAvailable(
        ref RoomExitMask mask,
        RoomExitDirection direction,
        RoomExitMask reservedMask,
        int requiredCount,
        RoomExitMask preferredMask = RoomExitMask.Up | RoomExitMask.Right | RoomExitMask.Down | RoomExitMask.Left)
    {
        if (RoomExitDirectionUtility.Count(mask) >= requiredCount)
            return;

        RoomExitMask directionMask = RoomExitDirectionUtility.ToMask(direction);
        if ((preferredMask & directionMask) == 0 ||
            (reservedMask & directionMask) != 0 ||
            (mask & directionMask) != 0)
        {
            return;
        }

        mask |= directionMask;
    }

    static RoomDefinitionSO ChooseRoomDefinition(MapRunConfigSO config, MapGraph graph, MapNode node, out int rotationSteps)
    {
        rotationSteps = 0;
        RoomDefinitionSO[] definitions = config != null ? config.RoomDefinitions : null;
        if (definitions == null || definitions.Length == 0)
            return null;

        MapNodeType type = node != null ? node.Type : MapNodeType.Combat;
        int requiredExitCount = node != null ? node.OutgoingIds.Count : 0;
        RoomExitMask requiredMask = node != null ? node.OutgoingExitMask : RoomExitMask.None;
        if (graph != null)
        {
            requiredExitCount = graph.GetRequiredExitCount(node);
            requiredMask = graph.GetRequiredExitMask(node);
        }

        RoomDefinitionSO exactMatch = ChooseWeightedRoomDefinition(definitions, type, requiredExitCount, requiredMask, true, out rotationSteps);
        if (exactMatch != null)
            return exactMatch;

        return ChooseWeightedRoomDefinition(definitions, type, requiredExitCount, requiredMask, false, out rotationSteps);
    }

    static RoomDefinitionSO ChooseWeightedRoomDefinition(
        RoomDefinitionSO[] definitions,
        MapNodeType type,
        int requiredExitCount,
        RoomExitMask requiredMask,
        bool exactMaskOnly,
        out int rotationSteps)
    {
        rotationSteps = 0;
        float total = 0f;
        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition == null || definition.Weight <= 0f ||
                !CanUseRoomDefinition(definition, type, requiredExitCount, requiredMask, exactMaskOnly, out _))
                continue;

            total += definition.Weight;
        }

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition == null || definition.Weight <= 0f ||
                !CanUseRoomDefinition(definition, type, requiredExitCount, requiredMask, exactMaskOnly, out int candidateRotationSteps))
                continue;

            cumulative += definition.Weight;
            if (roll <= cumulative)
            {
                rotationSteps = candidateRotationSteps;
                return definition;
            }
        }

        return FindFirstRoomDefinition(definitions, type, requiredExitCount, requiredMask, exactMaskOnly, out rotationSteps);
    }

    static bool CanUseRoomDefinition(
        RoomDefinitionSO definition,
        MapNodeType type,
        int requiredExitCount,
        RoomExitMask requiredMask,
        bool exactMaskOnly,
        out int rotationSteps)
    {
        rotationSteps = 0;
        if (definition == null ||
            definition.RoomPrefab == null ||
            definition.NodeType != type ||
            definition.MaxExitCount < requiredExitCount)
        {
            return false;
        }

        return definition.TryGetRotationForExitMask(requiredMask, exactMaskOnly, out rotationSteps);
    }

    static RoomDefinitionSO FindFirstRoomDefinition(
        RoomDefinitionSO[] definitions,
        MapNodeType type,
        int requiredExitCount,
        RoomExitMask requiredMask,
        bool exactMaskOnly,
        out int rotationSteps)
    {
        rotationSteps = 0;
        for (int i = 0; i < definitions.Length; i++)
        {
            RoomDefinitionSO definition = definitions[i];
            if (definition != null && definition.Weight > 0f &&
                CanUseRoomDefinition(definition, type, requiredExitCount, requiredMask, exactMaskOnly, out rotationSteps))
                return definition;
        }

        return null;
    }

    static EncounterDefinitionSO ChooseEncounterDefinition(MapRunConfigSO config, MapNodeType type)
    {
        EncounterDefinitionSO[] definitions = config != null ? config.EncounterDefinitions : null;
        if (definitions == null || definitions.Length == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < definitions.Length; i++)
        {
            EncounterDefinitionSO definition = definitions[i];
            if (definition == null || definition.Weight <= 0f || definition.NodeType != type)
                continue;

            total += definition.Weight;
        }

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < definitions.Length; i++)
        {
            EncounterDefinitionSO definition = definitions[i];
            if (definition == null || definition.Weight <= 0f || definition.NodeType != type)
                continue;

            cumulative += definition.Weight;
            if (roll <= cumulative)
                return definition;
        }

        return FindFirstEncounterDefinition(definitions, type);
    }

    static EncounterDefinitionSO FindFirstEncounterDefinition(EncounterDefinitionSO[] definitions, MapNodeType type)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            EncounterDefinitionSO definition = definitions[i];
            if (definition != null && definition.Weight > 0f && definition.NodeType == type)
                return definition;
        }

        return null;
    }

    static MapNodeType ChooseWeighted(WeightedMapNodeType[] weights, MapNodeType fallback, Func<MapNodeType, bool> canUseType = null)
    {
        if (weights == null || weights.Length == 0)
            return fallback;

        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            WeightedMapNodeType entry = weights[i];
            if (IsUsableWeightedEntry(entry, canUseType))
                total += entry.weight;
        }

        if (total <= 0f)
            return fallback;

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            WeightedMapNodeType entry = weights[i];
            if (!IsUsableWeightedEntry(entry, canUseType))
                continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
                return entry.type;
        }

        return fallback;
    }

    static bool IsUsableWeightedEntry(WeightedMapNodeType entry, Func<MapNodeType, bool> canUseType)
    {
        return entry != null &&
               entry.weight > 0f &&
               (canUseType == null || canUseType(entry.type));
    }

    static bool IsUsableMainPathNode(MapRunConfigSO config, MapNodeType type, int requiredExitCount)
    {
        return type != MapNodeType.Start &&
               type != MapNodeType.Boss &&
               HasEnabledRoomDefinitionForType(config, type, requiredExitCount);
    }

    static bool IsUsableBlueNode(MapRunConfigSO config, MapNodeType type, int requiredExitCount)
    {
        return MapPitySystem.IsBlueNodeType(type) &&
               HasEnabledRoomDefinitionForType(config, type, requiredExitCount);
    }

    static bool IsUsableDeadEndNode(MapRunConfigSO config, MapNodeType type)
    {
        return MapPitySystem.IsDeadEndRewardType(type) &&
               HasEnabledRoomDefinitionForType(config, type, 1);
    }

    static bool HasEnabledRoomDefinitionForType(MapRunConfigSO config, MapNodeType type, int requiredExitCount)
    {
        if (config == null)
            return true;

        RoomDefinitionSO[] definitions = config.RoomDefinitions;
        if (definitions == null || definitions.Length == 0)
            return true;

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
