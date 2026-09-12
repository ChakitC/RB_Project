using System;

[Serializable]
public readonly struct ComboExecutionProvenance
{
    public readonly ulong SessionId;
    public readonly ulong ComboWindowId;
    public readonly ulong ExecutionId;
    public readonly string ComboSkillId;
    public readonly float ChainExpiresAt;

    public ComboExecutionProvenance(
        ulong sessionId,
        ulong comboWindowId,
        ulong executionId,
        string comboSkillId,
        float chainExpiresAt)
    {
        SessionId = sessionId;
        ComboWindowId = comboWindowId;
        ExecutionId = executionId;
        ComboSkillId = comboSkillId;
        ChainExpiresAt = chainExpiresAt;
    }

    public bool IsValid =>
        SessionId != 0 &&
        ComboWindowId != 0 &&
        ExecutionId != 0 &&
        !string.IsNullOrWhiteSpace(ComboSkillId) &&
        ChainExpiresAt > 0f;
}
