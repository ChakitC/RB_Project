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
        var issues = new List<SkillValidationIssue>();
        CollectValidationIssues(issues, SkillExecutionKind.BasicMelee);
        return TryGetValidationError(issues, out reason);
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
