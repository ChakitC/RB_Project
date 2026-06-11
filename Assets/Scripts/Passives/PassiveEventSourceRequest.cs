public readonly struct PassiveEventSourceRequest
{
    public PassiveEventSourceRequest(
        PassiveEventSourceKind kind,
        PassiveEventType eventType,
        string eventSourceId,
        float floatValue,
        int intValue)
    {
        Kind = kind;
        EventType = eventType;
        EventSourceId = eventSourceId;
        FloatValue = floatValue;
        IntValue = intValue;
    }

    public PassiveEventSourceKind Kind { get; }
    public PassiveEventType EventType { get; }
    public string EventSourceId { get; }
    public float FloatValue { get; }
    public int IntValue { get; }

    public bool IsValid =>
        Kind != PassiveEventSourceKind.None &&
        EventType != PassiveEventType.None &&
        !string.IsNullOrWhiteSpace(EventSourceId);
}
