#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Character discovery and authoring setup are separate from the timeline's draft lifecycle.
public static class SkillHitboxCharacterSetup
{
    public static GameObject ResolveModel(GameObject selected)
    {
        if (selected == null) return null;
        var animator = selected.GetComponentInParent<Animator>(true);
        if (animator != null) return animator.gameObject;
        var models = selected.GetComponentsInChildren<Animator>(true);
        return models.Length == 1 ? models[0].gameObject : null;
    }

    public static List<CharacterStats> FindModelStats(GameObject model)
    {
        var result = new List<CharacterStats>();
        if (model == null) return result;
        var prefab = EditorUtility.IsPersistent(model) ? model : PrefabUtility.GetCorrespondingObjectFromSource(model);
        var avatar = model.GetComponent<Animator>()?.avatar;
        var candidates = AssetDatabase.FindAssets("t:CharacterStats")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterStats>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(stats => stats != null).ToList();
        if (prefab != null) result.AddRange(candidates.Where(stats => stats.CharacterPrefab == prefab || stats.CharacterPrefabBasement == prefab));
        if (result.Count == 0 && avatar != null) result.AddRange(candidates.Where(stats => stats.characterAvatar == avatar));
        return result.OrderBy(stats => stats.name).ToList();
    }

    public static SetAnimationVfxData PrepareModel(GameObject model)
    {
        if (model == null || EditorUtility.IsPersistent(model) || Application.isPlaying) return null;
        var tool = model.GetComponent<SetAnimationVfxData>() ?? Undo.AddComponent<SetAnimationVfxData>(model);
        var so = new SerializedObject(tool);
        if (so.FindProperty("characterRoot").objectReferenceValue == null)
        { so.FindProperty("characterRoot").objectReferenceValue = model.transform; so.ApplyModifiedProperties(); }
        return tool;
    }
    public sealed class Attack
    {
        public string Category, Label, EntryId;
        public ScriptableObject Source;
        public SkillGemDefinition Skill;
    }

    public static CharacteContext ResolveCharacter(GameObject selected)
    {
        if (selected == null) return null;
        var owner = CharacterContextModuleLookup.ResolveContext(selected);
        if (owner != null) return owner;
        // A prefab wrapper may contain its context. Never pick an arbitrary party member.
        for (var node = selected.transform; node != null; node = node.parent)
        {
            var children = node.GetComponentsInChildren<CharacteContext>(true);
            if (children.Length == 1) return children[0];
            if (children.Length > 1) return null;
        }
        return null;
    }

    public static SetAnimationVfxData FindAuthoring(CharacteContext ctx)
    {
        if (ctx == null) return null;
        return ctx.GetComponentsInChildren<SetAnimationVfxData>(true)
            .Concat(ctx.GetComponentsInParent<SetAnimationVfxData>(true))
            .FirstOrDefault(tool => ResolveCharacter(tool.gameObject) == ctx);
    }

    public static SetAnimationVfxData Prepare(CharacteContext ctx)
    {
        if (ctx == null || EditorUtility.IsPersistent(ctx) || Application.isPlaying) return null;
        ctx.ResolveReferences();
        var tool = FindAuthoring(ctx);
        if (tool == null) tool = Undo.AddComponent<SetAnimationVfxData>(ctx.gameObject);
        var so = new SerializedObject(tool);
        var root = so.FindProperty("characterRoot");
        if (root.objectReferenceValue == null)
        {
            root.objectReferenceValue = ctx.transform;
            so.ApplyModifiedProperties();
        }
        return tool;
    }

    public static List<Attack> CollectAttacks(CharacteContext ctx, CharacterStats modelStats = null)
    {
        var result = new List<Attack>();
        if (ctx == null && modelStats == null) return result;
        var stats = ctx != null ? ctx.baseStats : modelStats;
        var seenSkills = new HashSet<SkillGemDefinition>();
        void AddSkill(SkillGemDefinition skill, string label)
        {
            if (skill == null || !seenSkills.Add(skill)) return;
            string title = string.IsNullOrWhiteSpace(label) ? skill.SkillDefinitionDisplayName : label;
            if (string.IsNullOrWhiteSpace(title)) title = skill.name;
            result.Add(new Attack { Category = "Skills", Label = title, Source = skill, EntryId = "main", Skill = skill });
        }
        void AddCombo(SkillGemDefinition combo, string category)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.MeleeStepCount; i++)
            {
                var step = combo.GetMeleeStep(i);
                if (step.executionSkill == null) continue;
                string clip = step.executionSkill.skillClip?.Clip != null ? step.executionSkill.skillClip.Clip.name : "No animation";
                bool hasId = !string.IsNullOrWhiteSpace(step.EntryId);
                result.Add(new Attack { Category = category, Label = $"Step {i + 1} — {clip}",
                    Source = combo.IsCombo && hasId ? (ScriptableObject)combo : step.executionSkill,
                    EntryId = hasId ? step.EntryId : "main", Skill = step.executionSkill });
                seenSkills.Add(step.executionSkill);
            }
        }
        void AddSlot(CharacterSkillLoadoutSlot slot)
        {
            if (slot?.options == null) return;
            foreach (var option in slot.options)
                if (option != null) AddSkill(option.ActiveSkillAsset, option.ResolvedDisplayName);
        }
        AddCombo(stats != null ? stats.animProfile?.lightMeleeSkill ?? stats.animProfile?.meleeSkill : null, "Light");
        AddCombo(stats != null ? stats.animProfile?.heavyMeleeSkill ?? stats.animProfile?.meleeSkill : null, "Heavy");
        if (stats != null)
        {
            if (stats.skillSlots != null) foreach (var slot in stats.skillSlots) AddSlot(slot);
            AddSlot(stats.helperCommandSlot);
            if (stats.helperProcSlots != null)
                foreach (var slot in stats.helperProcSlots)
                    if (slot?.options != null)
                        foreach (var option in slot.options)
                            if (option != null) AddSkill(option.ExecutionSkill, option.ResolvedDisplayName);
            AddSkill(stats.chainAttackSkill, "Chain Attack");
            AddSkill(stats.partyComboSkill != null ? stats.partyComboSkill.executionSkill : null, "Party Combo");
        }
        // Inspect serialized enemy/legacy slots without calling runtime loadout-building getters.
        var manager = ctx != null ? ctx.SkillManager : null;
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            var slots = so.FindProperty("autonomousSlots");
            for (int i = 0; slots != null && i < slots.arraySize; i++)
                AddSkill(slots.GetArrayElementAtIndex(i).FindPropertyRelative("skillAsset").objectReferenceValue as SkillGemDefinition, null);
            AddSkill(so.FindProperty("chainAttackSkill")?.FindPropertyRelative("skillAsset")?.objectReferenceValue as SkillGemDefinition, "Chain Attack");
        }
        return result;
    }
}
#endif
