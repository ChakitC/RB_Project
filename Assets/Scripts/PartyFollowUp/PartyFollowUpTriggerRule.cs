using System;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboTriggerKind")]
public enum PartyFollowUpTriggerKind
{
    None = 0,
    FollowUpSkillCommitted = 1,
    TargetHasStatus = 2,
    EnteredBreak = 3,
    FinalStrike = 100,
    ArtsReaction = 101,
    InflictionCountReached = 102,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboSourceRelation")]
public enum PartyFollowUpSourceRelation
{
    ControlledActor = 0,
    Self = 1,
    OtherPartyMember = 2,
    AnyPartyMember = 3,
}

[Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboTriggerRule")]
public sealed class PartyFollowUpTriggerRule
{
    public PartyFollowUpTriggerKind kind;
    public PartyFollowUpSourceRelation sourceRelation = PartyFollowUpSourceRelation.OtherPartyMember;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.Any;
    public StatusEffectDef requiredStatus;
    public string requiredStatusTag;
    [Min(1)] public int requiredStacks = 1;
    public string eventSourceId;
    [UnityEngine.Serialization.FormerlySerializedAs("allowSameComboSkill")]
    public bool allowSameFollowUpSkill;
}
