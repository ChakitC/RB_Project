#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SummonPrefabValidator
{
    static readonly string[] ForbiddenComponentNames =
    {
        "PlayerContext", "AllyContext", "EnemyContext", "CharacterEquipment", "AccessoryLoadout",
        "CharacterActiveSkillProgress", "CharacterContextPartyLoader", "LevelSystem", "EnemyLevelSystem",
        "PlayerInput", "PlayerInputHandler", "PlayerInventory", "PlayerMovementCC", "PlayerUIContext",
        "Interactor", "AllyHelperManager", "FieldAllyManager", "PartyFormationController",
        "PartyFormationFollowRuntime", "PartyCommandController", "InterruptionCommandController",
        "ChainAttackCoordinator", "ChainAttackProcController", "FieldAllyMember",
        "ThirdPersonAimController"
    };

    public static bool Validate(SummonContext context, out string error)
    {
        error = string.Empty;
        if (context == null)
        {
            error = "SummonContext is missing.";
            return false;
        }

        return Validate(context.transform.root.gameObject, context.Mobility, out error);
    }

    public static bool Validate(GameObject prefab, SummonMobility mobility, out string error)
    {
        var issues = new List<string>();
        if (prefab == null)
        {
            issues.Add("Prefab is missing.");
        }
        else
        {
            SummonContext context = prefab.GetComponentInChildren<SummonContext>(true);
            SummonedEntityRuntime runtime = prefab.GetComponentInChildren<SummonedEntityRuntime>(true);
            if (context == null)
                issues.Add("Required component missing: SummonContext.");
            else if (context.transform != prefab.transform)
                issues.Add("SummonContext must be on the summon prefab root.");
            if (runtime == null)
                issues.Add("Required component missing: SummonedEntityRuntime.");
            if (prefab.GetComponentInChildren<SummonHealthSystem>(true) == null)
                issues.Add("Required component missing: SummonHealthSystem.");
            if (prefab.GetComponentInChildren<StateHub>(true) == null)
                issues.Add("Required component missing: StateHub.");
            if (prefab.GetComponentInChildren<StatsHub>(true) == null)
                issues.Add("Required component missing: StatsHub.");
            if (prefab.GetComponentInChildren<AITargetInfo>(true) == null)
                issues.Add("Required component missing: AITargetInfo.");
            if (prefab.GetComponentInChildren<CombatEventBus>(true) == null)
                issues.Add("Required component missing: CombatEventBus.");

            if (runtime != null)
                ValidateRoots(prefab.transform, runtime, issues);

            if (mobility == SummonMobility.Mobile)
            {
                if (prefab.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) == null)
                    issues.Add("Mobile summon requires NavMeshAgent.");
                if (prefab.GetComponentInChildren<AgentMoveDriver>(true) == null)
                    issues.Add("Mobile summon requires AgentMoveDriver.");
            }
            else if (!CharacterPlacementProbeUtility.TryGetFootprint(prefab, mobility, out _, out string footprintError))
            {
                issues.Add(footprintError);
            }

            ValidateForbiddenComponents(prefab, issues);
            ValidateRecursiveSummonReferences(prefab, issues);
        }

        error = string.Join("\n", issues);
        return issues.Count == 0;
    }

    static void ValidateRoots(Transform prefabRoot, SummonedEntityRuntime runtime, List<string> issues)
    {
        SerializedObject serialized = new SerializedObject(runtime);
        SerializedProperty gameplay = serialized.FindProperty("gameplayRoot");
        SerializedProperty presentation = serialized.FindProperty("presentationRoot");
        GameObject gameplayRoot = gameplay?.objectReferenceValue as GameObject;
        Transform presentationRoot = presentation?.objectReferenceValue as Transform;

        if (gameplayRoot == null)
            issues.Add("SummonedEntityRuntime requires a Gameplay Root.");
        if (presentationRoot == null)
            issues.Add("SummonedEntityRuntime requires a Presentation Root.");
        if (gameplayRoot != null && presentationRoot != null)
        {
            if (!IsSameOrChild(gameplayRoot.transform, prefabRoot))
                issues.Add("Gameplay Root must be a descendant of the summon prefab root.");
            if (!IsSameOrChild(presentationRoot, prefabRoot))
                issues.Add("Presentation Root must be a descendant of the summon prefab root.");
            if (presentationRoot == gameplayRoot.transform || presentationRoot.IsChildOf(gameplayRoot.transform))
                issues.Add("Gameplay Root and Presentation Root must be separate hierarchy roots.");
        }
    }

    static bool IsSameOrChild(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }

    static void ValidateForbiddenComponents(GameObject prefab, List<string> issues)
    {
        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            for (int j = 0; j < ForbiddenComponentNames.Length; j++)
            {
                if (!string.Equals(behaviour.GetType().Name, ForbiddenComponentNames[j], StringComparison.Ordinal))
                    continue;

                issues.Add($"Forbidden component present: {ForbiddenComponentNames[j]} ({behaviour.name}).");
                break;
            }
        }
    }

    static void ValidateRecursiveSummonReferences(GameObject prefab, List<string> issues)
    {
        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        var visitedSkills = new HashSet<SkillGemDefinition>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                    iterator.objectReferenceValue is not SkillGemDefinition skill ||
                    !visitedSkills.Add(skill))
                    continue;

                if (ContainsRecursiveSummon(skill.payload, new HashSet<SkillPayloadDef>()))
                    issues.Add($"Recursive summon reference found through skill '{skill.SkillDefinitionId}'.");
            }
        }
    }

    static bool ContainsRecursiveSummon(SkillPayloadDef payload, HashSet<SkillPayloadDef> visited)
    {
        if (payload == null || !visited.Add(payload))
            return false;

        if (payload is SummonSkillPayloadDef summon)
            return summon.SummonPrefab != null && summon.SummonPrefab.GetComponentInChildren<SummonContext>(true) != null;

        if (payload is not CompositeSkillPayloadDef composite)
            return false;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is PayloadStep payloadStep &&
                ContainsRecursiveSummon(payloadStep.Payload, visited))
                return true;
        }

        return false;
    }

    [MenuItem("Tools/RB/Summoning/Validate All Summon Prefabs")]
    public static void ValidateAllPrefabs()
    {
        int summonCount = 0;
        int errorCount = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SummonContext context = prefab != null ? prefab.GetComponentInChildren<SummonContext>(true) : null;
            if (context == null)
                continue;

            summonCount++;
            if (!Validate(prefab, context.Mobility, out string error))
            {
                errorCount++;
                Debug.LogError($"[SummonPrefabValidator] {path}:\n{error}", prefab);
            }
        }

        Debug.Log($"[SummonPrefabValidator] Checked {summonCount} summon prefab(s); {errorCount} invalid.");
    }
}
#endif
