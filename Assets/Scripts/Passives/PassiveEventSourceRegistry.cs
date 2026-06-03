using System;

public static class PassiveEventSourceRegistry
{
    public static Type GetSourceType(PassiveEventSourceKind kind)
    {
        return kind switch
        {
            PassiveEventSourceKind.MovementDistance => typeof(MovementDistanceEventSource),
            _ => null
        };
    }
}
