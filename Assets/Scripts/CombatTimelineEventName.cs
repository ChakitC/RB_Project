using Animancer;

public enum CombatTimelineEventName
{
    None = 0,

    HitStart = 10,
    HitEnd = 11,
    FootStep = 12,
    SpawnEffect = 13,
    ShakeCamera = 14,

    PreCastOpen = 200,
    PreCastClose = 201,
}

public static class CombatTimelineEventNames
{
    public static bool IsValid(CombatTimelineEventName eventName)
    {
        return eventName != CombatTimelineEventName.None;
    }

    public static StringReference ToStringReference(CombatTimelineEventName eventName)
    {
        string animancerEventName = ToAnimancerEventName(eventName);
        return string.IsNullOrWhiteSpace(animancerEventName)
            ? null
            : StringReference.Get(animancerEventName);
    }

    public static string ToAnimancerEventName(CombatTimelineEventName eventName)
    {
        switch (eventName)
        {
            case CombatTimelineEventName.HitStart:
                return "HitStart";
            case CombatTimelineEventName.HitEnd:
                return "HitEnd";
            case CombatTimelineEventName.FootStep:
                return "FootStep";
            case CombatTimelineEventName.SpawnEffect:
                return "SpawnEffect";
            case CombatTimelineEventName.ShakeCamera:
                return "ShakeCamera";
            case CombatTimelineEventName.PreCastOpen:
                return "PreCastOpen";
            case CombatTimelineEventName.PreCastClose:
                return "PreCastClose";
        }

        return eventName == CombatTimelineEventName.None ? null : eventName.ToString();
    }

    public static void AddUnique(
        System.Collections.Generic.List<CombatTimelineEventName> eventNames,
        CombatTimelineEventName eventName)
    {
        if (eventNames == null || !IsValid(eventName) || eventNames.Contains(eventName))
            return;

        eventNames.Add(eventName);
    }

}
