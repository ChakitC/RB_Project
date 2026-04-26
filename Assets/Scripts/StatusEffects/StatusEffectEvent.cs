using UnityEngine;

public enum StatusEffectEventType
{
    AppliedNew,
    Refreshed,
    StackChanged,
    Removed,
    Ticked
}

public readonly struct StatusEffectEvent
{
    public readonly StatusEffectController Controller;
    public readonly StatusEffectEventType EventType;
    public readonly StatusEffectInstance Instance;
    public readonly StatusEffectDef Definition;
    public readonly GameObject Source;
    public readonly int OldStacks;
    public readonly int NewStacks;

    public StatusEffectEvent(
        StatusEffectController controller,
        StatusEffectEventType eventType,
        StatusEffectInstance instance,
        StatusEffectDef definition,
        GameObject source,
        int oldStacks,
        int newStacks)
    {
        Controller = controller;
        EventType = eventType;
        Instance = instance;
        Definition = definition;
        Source = source;
        OldStacks = oldStacks;
        NewStacks = newStacks;
    }
}
