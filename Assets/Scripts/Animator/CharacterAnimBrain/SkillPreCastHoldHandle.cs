public readonly struct SkillPreCastHoldHandle
{
    public readonly int RequestId;
    public readonly int HoldId;
    public bool IsValid => RequestId > 0 && HoldId > 0;

    public SkillPreCastHoldHandle(int requestId, int holdId)
    {
        RequestId = requestId;
        HoldId = holdId;
    }
}
