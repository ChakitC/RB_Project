#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the shared Helper rig contract. The rig is created once and reused for every loaded
/// Helper character, so missing runtime modules must be caught on the prefab rather than after the
/// first party registration callback.
/// </summary>
public static class HelperPrefabValidator
{
    public static bool Validate(GameObject prefab, out string error)
    {
        var issues = new List<string>();
        if (prefab == null)
        {
            issues.Add("Helper prefab is missing.");
        }
        else
        {
            AllyContext context = prefab.GetComponentInChildren<AllyContext>(true);
            CharacterSkillManager skillManager = prefab.GetComponentInChildren<CharacterSkillManager>(true);
            CharacterActiveSkillProgress progress =
                prefab.GetComponentInChildren<CharacterActiveSkillProgress>(true);
            CharacterAnimDriver animDriver = prefab.GetComponentInChildren<CharacterAnimDriver>(true);
            CharacterAnimBrain animBrain = prefab.GetComponentInChildren<CharacterAnimBrain>(true);

            Require(context, "AllyContext", issues);
            Require(skillManager, "CharacterSkillManager", issues);
            Require(progress, "CharacterActiveSkillProgress", issues);
            Require(animDriver, "CharacterAnimDriver", issues);
            Require(animBrain, "CharacterAnimBrain", issues);

            if (context != null)
            {
                context.ResolveReferences();
                if (context.SkillManager == null || context.SkillManager != skillManager)
                    issues.Add("AllyContext.SkillManager is not bound to the prefab's CharacterSkillManager.");
                if (context.ActiveSkillProgress == null || context.ActiveSkillProgress != progress)
                    issues.Add("AllyContext.ActiveSkillProgress is not bound to the prefab's progress component.");
                if (context.AnimDriver == null || context.AnimDriver != animDriver)
                    issues.Add("AllyContext.AnimDriver is not bound to the prefab's CharacterAnimDriver.");
                if (context.AnimBrain == null || context.AnimBrain != animBrain)
                    issues.Add("AllyContext.AnimBrain is not bound to the prefab's CharacterAnimBrain.");
            }
        }

        error = string.Join("\n", issues);
        return issues.Count == 0;
    }

    static void Require(UnityEngine.Object component, string name, List<string> issues)
    {
        if (component == null)
            issues.Add($"Required Helper component missing: {name}.");
    }

    [MenuItem("Tools/RB/AI/Validate Helper Rig")]
    public static void ValidateConfiguredHelperRig()
    {
        const string path = "Assets/Prefab/Player/Ally_Helper.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!Validate(prefab, out string error))
        {
            Debug.LogError($"[HelperPrefabValidator] {path}:\n{error}", prefab);
            return;
        }

        Debug.Log($"[HelperPrefabValidator] Validated {path}.", prefab);
    }
}
#endif
