using UnityEngine;

public enum PartyComboOpportunityState
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

public enum PartyComboRejectReason
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

public sealed class PartyComboOpportunity
{
    public int OfferId;
    public PartyComboSkillDef Definition;
    public ChainActorRole OwnerRole;
    public CharacteContext OwnerContext;
    public SkillTargetHandle Target;
    public float OfferedAt;
    public float ExpiresAt;
    public float ComboChainExpiresAt;
    public ulong SessionId;
    public ulong ComboWindowId;
    public ulong ExecutionId;
    public PassiveEventContext TriggerContext;
    public PartyComboOpportunityState State;
    public PartyComboRejectReason EndReason;

    public ulong FactId => TriggerContext.FactId;
    public ulong ChainId => TriggerContext.ChainId;
    public int Depth => TriggerContext.Depth;
}

public readonly struct PartyComboOfferViewData
{
    public readonly int OfferId;
    public readonly ChainActorRole OwnerRole;
    public readonly PartyComboSkillDef Definition;
    public readonly SkillTargetHandle Target;
    public readonly float ExpiresAt;
    public readonly PartyComboOpportunityState State;
    public readonly PartyComboRejectReason Reason;

    public PartyComboOfferViewData(PartyComboOpportunity offer)
    {
        OfferId = offer != null ? offer.OfferId : 0;
        OwnerRole = offer != null ? offer.OwnerRole : ChainActorRole.None;
        Definition = offer != null ? offer.Definition : null;
        Target = offer != null ? offer.Target : SkillTargetHandle.None;
        ExpiresAt = offer != null ? offer.ExpiresAt : 0f;
        State = offer != null ? offer.State : PartyComboOpportunityState.Cancelled;
        Reason = offer != null ? offer.EndReason : PartyComboRejectReason.NoOffer;
    }
}
