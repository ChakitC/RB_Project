using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public partial class SkillGemDefinition
{
    [FoldoutGroup("Combo"), LabelText("Enable Combo")]
    public bool comboEnabled;
    [FoldoutGroup("Combo"), ShowIf(nameof(comboEnabled))]
    [SerializeField] List<SkillComboStep> comboSteps = new();
    public bool IsCombo => comboEnabled;
    public IReadOnlyList<SkillComboStep> ComboSteps => comboSteps;
    public int MeleeStepCount => IsCombo ? comboSteps?.Count ?? 0 : 1;
    public SkillComboStep GetMeleeStep(int index) => IsCombo ? comboSteps[index] :
        new SkillComboStep(this, "main", Vector2.zero, false);

    public bool TryGetComboStep(string id, out SkillComboStep step, out int index)
    {
        if (IsCombo && comboSteps != null && !string.IsNullOrWhiteSpace(id))
            for (int i = 0; i < comboSteps.Count; i++)
                if (comboSteps[i].EntryId == id) { step = comboSteps[i]; index = i; return true; }
        step = default; index = -1; return false;
    }

    public bool SetComboChainWindow(string id, Vector2 value)
    {
        if (!TryGetComboStep(id, out var step, out int index)) return false;
        float start = Mathf.Clamp01(value.x), end = Mathf.Clamp01(value.y);
        step.chainWindowN = new Vector2(Mathf.Min(start, end), Mathf.Max(start, end));
        comboSteps[index] = step;
        return true;
    }

    public bool ValidateMelee(out string reason)
    {
        if (MeleeStepCount == 0) { reason = "Combo has no steps."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < MeleeStepCount; i++)
        {
            var step = GetMeleeStep(i);
            var skill = step.executionSkill;
            if (skill == null || skill.IsCombo || skill.skillClip == null || !skill.skillClip.IsValid || skill.payload == null)
            { reason = $"Step {i + 1} needs a non-combo Skill with animation and payload."; return false; }
            if (string.IsNullOrWhiteSpace(step.EntryId) || !ids.Add(step.EntryId))
            { reason = $"Step {i + 1} has a missing or duplicate ID."; return false; }
            if (!float.IsFinite(step.chainWindowN.x) || !float.IsFinite(step.chainWindowN.y) ||
                step.chainWindowN.x < 0 || step.chainWindowN.y > 1 || step.chainWindowN.x > step.chainWindowN.y)
            { reason = $"Step {i + 1} has an invalid Chain Window."; return false; }
        }
        reason = null; return true;
    }

#if UNITY_EDITOR
    [FoldoutGroup("Combo"), ShowIf(nameof(comboEnabled)), Button("Assign Missing Step IDs")]
    public void EnsureComboStepIds()
    {
        var ids = new HashSet<string>();
        if (comboSteps == null) return;
        UnityEditor.Undo.RecordObject(this, "Assign Combo Step IDs");
        bool changed = false;
        for (int i = 0; i < comboSteps.Count; i++)
        {
            var step = comboSteps[i];
            if (!string.IsNullOrWhiteSpace(step.EntryId) && ids.Add(step.EntryId)) continue;
            string id = Guid.NewGuid().ToString("N"); ids.Add(id);
            comboSteps[i] = new SkillComboStep(step.executionSkill, id, step.chainWindowN, step.dropBufferOnWindowExpire);
            changed = true;
        }
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
