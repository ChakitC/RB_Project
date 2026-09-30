using System;
using System.Collections.Generic;

public partial class SkillGemDefinition
{
    // Preserve serialized assets while making the two execution shapes explicit to consumers.
    public bool TryGetExecution(out SkillPayloadDef execution)
    {
        execution = IsCombo ? null : payload;
        return execution != null;
    }

    public bool TryGetCombo(out IReadOnlyList<SkillComboStep> steps)
    {
        steps = IsCombo ? comboSteps ?? (IReadOnlyList<SkillComboStep>)Array.Empty<SkillComboStep>() : null;
        return IsCombo;
    }

    public float GetExecutionCastPointNormalized(SkillExecutionKind kind) =>
        kind == SkillExecutionKind.BasicMelee ? 0f : GetCastPointNormalized();

    public bool IsTimelineListenerReadyAt(SkillExecutionKind kind, float normalizedTime) =>
        float.IsFinite(normalizedTime) && (kind == SkillExecutionKind.BasicMelee
            ? normalizedTime >= 0f
            : normalizedTime > GetExecutionCastPointNormalized(kind));

    // The root payload remains the serialized backing store. Child payloads never own these policies.
    public SkillHelperFacingMode HelperFacingMode => TryGetExecution(out var execution)
        ? execution.HelperFacingMode : SkillHelperFacingMode.KeepCurrentFacing;

    public bool TryGetTargetPlacement(out SkillTargetPlacementSpec placement)
    {
        placement = default;
        return TryGetExecution(out var execution) && execution.TryGetTargetPlacement(out placement);
    }
}
