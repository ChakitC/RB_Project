#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SummonContext))]
public sealed class SummonContextEditor : Editor
{
    static readonly string[] HiddenInheritedFields =
    {
        "baseStats", "currentWeapon", "CharacterLoad", "Equipment", "AccessoryLoadout",
        "WeaponSystem", "AnimBrain", "AnimDriver", "PairOffsetApplier", "MeleeController",
        "AimRig", "UIManager", "StatusEffects", "levelSystem", "StaminaSystem", "DashSystem",
        "KnockbackMotor", "PassiveController", "ActiveSkillProgress", "EnegySystem", "Interactor",
        "SkillManager", "moveInput", "lookInput"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenInheritedFields);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate Summon Prefab"))
        {
            if (SummonPrefabValidator.Validate(target as SummonContext, out string error))
                Debug.Log($"[SummonPrefabValidator] '{target.name}' is valid.", target);
            else
                Debug.LogError($"[SummonPrefabValidator] '{target.name}' is invalid:\n{error}", target);
        }
    }
}
#endif
