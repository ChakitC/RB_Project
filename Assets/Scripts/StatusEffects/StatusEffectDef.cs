using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Status Effect")]
public sealed class StatusEffectDef : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public Sprite icon;
    public StatusEffectCategory category = StatusEffectCategory.Neutral;

    [Header("Lifetime")]
    [Min(0f)] public float duration = 5f;
    [Min(0)] public int maxStacks = 1;
    public StackMode stackMode = StackMode.RefreshDuration;

    [Header("Tick")]
    [Min(0f)] public float tickInterval;
    public float tickDamage;

    [Header("Gameplay")]
    public ControlBlockFlags controlBlocks = ControlBlockFlags.None;
    public bool pushStunnedState;
    public List<string> tags = new();
    public List<StatusEffectModifier> modifiers = new();

    [Header("Triggered Stacking")]
    public List<StatusEffectTriggerRule> triggerRules = new();

    public bool IsPermanent => duration <= 0f;
    public bool HasTick => tickInterval > 0f && !Mathf.Approximately(tickDamage, 0f);
    public int ClampedMaxStacks => maxStacks <= 0 ? int.MaxValue : maxStacks;
}
