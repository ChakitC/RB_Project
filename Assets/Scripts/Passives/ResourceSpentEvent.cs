public readonly struct ResourceSpentEvent
{
    public readonly float Amount;
    public readonly string Reason;
    public readonly ulong FactId;
    public ResourceSpentEvent(float amount, string reason, ulong factId)
    { Amount = amount; Reason = reason; FactId = factId; }
}
