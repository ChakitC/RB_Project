using Animancer;

public enum CombatTimelineEventName
{
    None = 0,

    HitStart = 10,
    HitEnd = 11,
    FootStep = 12,
    SpawnEffect = 13,
    ShakeCamera = 14,
    HitLag = 15,

    TauntApply = 30,

    /// <summary>The frame a carried object leaves the caster's hand and starts travelling.</summary>
    DeliveryRelease = 31,

    Vfx = 20,

    PreCastOpen = 200,
    PreCastClose = 201,

    CutsceneSkillStart = 210,
    CutsceneSkillEnd   = 211,
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
            case CombatTimelineEventName.HitLag:
                return "HitLag";
            case CombatTimelineEventName.TauntApply:
                return "TauntApply";
            case CombatTimelineEventName.DeliveryRelease:
                return "DeliveryRelease";
            case CombatTimelineEventName.Vfx:
                return "Vfx";
            case CombatTimelineEventName.PreCastOpen:
                return "PreCastOpen";
            case CombatTimelineEventName.PreCastClose:
                return "PreCastClose";
            case CombatTimelineEventName.CutsceneSkillStart:
                return "CutsceneSkillStart";
            case CombatTimelineEventName.CutsceneSkillEnd:
                return "CutsceneSkillEnd";
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
