using System;

[Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "ComboExecutionProvenance")]
public readonly struct FollowUpExecutionProvenance
{
    public readonly ulong SessionId;
    public readonly ulong FollowUpWindowId;
    public readonly ulong ExecutionId;
    public readonly string FollowUpSkillId;
    public readonly float ChainExpiresAt;

    public FollowUpExecutionProvenance(
        ulong sessionId,
        ulong followUpWindowId,
        ulong executionId,
        string followUpSkillId,
        float chainExpiresAt)
    {
        SessionId = sessionId;
        FollowUpWindowId = followUpWindowId;
        ExecutionId = executionId;
        FollowUpSkillId = followUpSkillId;
        ChainExpiresAt = chainExpiresAt;
    }

    public bool IsValid =>
        SessionId != 0 &&
        FollowUpWindowId != 0 &&
        ExecutionId != 0 &&
        !string.IsNullOrWhiteSpace(FollowUpSkillId) &&
        ChainExpiresAt > 0f;
}
