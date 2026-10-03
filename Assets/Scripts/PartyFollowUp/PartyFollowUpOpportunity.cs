using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboOpportunityState")]
public enum PartyFollowUpOpportunityState
{
    Offered = 0,
    Starting = 1,
    Executing = 2,
    Committed = 3,
    Finished = 4,
    Expired = 5,
    Cancelled = 6,
    Failed = 7,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboRejectReason")]
public enum PartyFollowUpRejectReason
{
    None = 0,
    Disabled = 1,
    NoOffer = 2,
    StaleOffer = 3,
    Expired = 4,
    Paused = 5,
    Busy = 6,
    OwnerInvalid = 7,
    TargetInvalid = 8,
    PlacementFailed = 9,
    CastRejected = 10,
    PayloadFailed = 11,
    SessionInvalid = 12,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboOpportunity")]
public sealed class PartyFollowUpOpportunity
{
    public int OfferId;
    public PartyFollowUpSkillDef Definition;
    public ChainActorRole OwnerRole;
    public CharacteContext OwnerContext;
    public SkillTargetHandle Target;
    public float OfferedAt;
    public float ExpiresAt;
    public float FollowUpChainExpiresAt;
    public ulong SessionId;
    public ulong FollowUpWindowId;
    public ulong ExecutionId;
    public PassiveEventContext TriggerContext;
    public PartyFollowUpOpportunityState State;
    public PartyFollowUpRejectReason EndReason;

    public ulong FactId => TriggerContext.FactId;
    public ulong ChainId => TriggerContext.ChainId;
    public int Depth => TriggerContext.Depth;
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboOfferViewData")]
public readonly struct PartyFollowUpOfferViewData
{
    public readonly int OfferId;
    public readonly ChainActorRole OwnerRole;
    public readonly PartyFollowUpSkillDef Definition;
    public readonly SkillTargetHandle Target;
    public readonly float ExpiresAt;
    public readonly PartyFollowUpOpportunityState State;
    public readonly PartyFollowUpRejectReason Reason;

    public PartyFollowUpOfferViewData(PartyFollowUpOpportunity offer)
    {
        OfferId = offer != null ? offer.OfferId : 0;
        OwnerRole = offer != null ? offer.OwnerRole : ChainActorRole.None;
        Definition = offer != null ? offer.Definition : null;
        Target = offer != null ? offer.Target : SkillTargetHandle.None;
        ExpiresAt = offer != null ? offer.ExpiresAt : 0f;
        State = offer != null ? offer.State : PartyFollowUpOpportunityState.Cancelled;
        Reason = offer != null ? offer.EndReason : PartyFollowUpRejectReason.NoOffer;
    }
}
