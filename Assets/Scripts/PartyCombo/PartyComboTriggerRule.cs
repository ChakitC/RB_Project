using System;
using UnityEngine;

public enum PartyComboTriggerKind
{
    None = 0,
    ComboSkillCommitted = 1,
    TargetHasStatus = 2,
    EnteredBreak = 3,
    FinalStrike = 100,
    ArtsReaction = 101,
    InflictionCountReached = 102,
}

public enum PartyComboSourceRelation
{
    ControlledActor = 0,
    Self = 1,
    OtherPartyMember = 2,
    AnyPartyMember = 3,
}

[Serializable]
public sealed class PartyComboTriggerRule
{
    public PartyComboTriggerKind kind;
    public PartyComboSourceRelation sourceRelation = PartyComboSourceRelation.OtherPartyMember;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.Any;
    public StatusEffectDef requiredStatus;
    public string requiredStatusTag;
    [Min(1)] public int requiredStacks = 1;
    public string eventSourceId;
    public bool allowSameComboSkill;
}
