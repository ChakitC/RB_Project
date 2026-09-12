using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PartyComboOpportunityController : MonoBehaviour
{
    enum SettlementState
    {
        Pending,
        Committed,
        Rejected,
    }

    readonly struct DedupeKey : IEquatable<DedupeKey>
    {
        readonly ulong factId;
        readonly string comboId;
        readonly ChainActorRole role;

        public DedupeKey(ulong factId, string comboId, ChainActorRole role)
        {
            this.factId = factId;
            this.comboId = comboId;
            this.role = role;
        }

        public bool Equals(DedupeKey other) =>
            factId == other.factId && role == other.role &&
            string.Equals(comboId, other.comboId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is DedupeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(factId, comboId, (int)role);
    }

    readonly struct UseKey : IEquatable<UseKey>
    {
        readonly ulong windowId;
        readonly string comboId;
        readonly ChainActorRole role;

        public UseKey(ulong windowId, string comboId, ChainActorRole role)
        {
            this.windowId = windowId;
            this.comboId = comboId;
            this.role = role;
        }

        public ulong WindowId => windowId;

        public bool Equals(UseKey other) =>
            windowId == other.windowId && role == other.role &&
            string.Equals(comboId, other.comboId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is UseKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(windowId, comboId, (int)role);
    }

    sealed class WindowState
    {
        public ulong WindowId;
        public float ExpiresAt;
    }

    sealed class Settlement
    {
        public SettlementState State;
        public float ExpiresAt;
    }

    sealed class Candidate
    {
        public PartyRuntimeActor Owner;
        public PartyRuntimeActor EventActor;
        public PartyComboSkillDef Definition;
        public PassiveEventContext Context;
        public ulong WindowId;
        public float WindowExpiresAt;
        public float ReceivedAt;
        public DedupeKey Dedupe;
    }

    static long nextSessionId;
    static long nextWindowId;
    static long nextExecutionId;

    [SerializeField] PartyComboFeatureFlags featureFlags;
    [SerializeField] PartyComboSkillExecutor executor;

    readonly PartyCombatEventRouter router = new PartyCombatEventRouter();
    readonly Dictionary<ChainActorRole, PartyComboOpportunity> offers =
        new Dictionary<ChainActorRole, PartyComboOpportunity>();
    readonly Dictionary<ulong, WindowState> windowsByChain = new Dictionary<ulong, WindowState>();
    readonly Dictionary<DedupeKey, ulong> dedupeWindows = new Dictionary<DedupeKey, ulong>();
    readonly HashSet<UseKey> inFlight = new HashSet<UseKey>();
    readonly HashSet<UseKey> used = new HashSet<UseKey>();
    readonly Dictionary<ulong, Settlement> settlements = new Dictionary<ulong, Settlement>();
    readonly Dictionary<ulong, List<Candidate>> bufferedCandidates =
        new Dictionary<ulong, List<Candidate>>();
    readonly List<PartyComboOpportunity> offerScratch = new List<PartyComboOpportunity>(4);
    readonly List<ulong> idScratch = new List<ulong>(16);
    readonly List<DedupeKey> dedupeScratch = new List<DedupeKey>(16);
    readonly List<UseKey> useScratch = new List<UseKey>(16);

    PartyRuntime party;
    ulong sessionId;
    int nextOfferId = 1;
    float comboClock;

    public event Action<PartyComboOfferViewData> OfferChanged;
    public float ComboClockNow => comboClock;
    public ulong PartyBindingVersion => sessionId;
    public bool IsBound => party != null && sessionId != 0;
    public bool RuntimeFeatureEnabled => featureFlags != null && featureFlags.runtimeEnabled;

    void Awake()
    {
        if (executor == null)
            executor = GetComponent<PartyComboSkillExecutor>();
    }

    void OnEnable()
    {
        router.EventRouted += OnEventRouted;
    }

    void OnDisable()
    {
        router.EventRouted -= OnEventRouted;
        UnbindParty();
    }

    void Update()
    {
        if (!IsBound)
            return;

        if (!GlobalTimeScaleManager.Instance.IsPaused)
            comboClock += Time.unscaledDeltaTime;

        ExpireOffers();
        CleanupExpiredState();
        CancelForInvalidControlledActor();
    }

    public bool BindParty(PartyRuntime runtime)
    {
        UnbindParty();
        PartyComboFeatureGate.Bind(featureFlags);
        if (runtime == null || featureFlags == null || !featureFlags.runtimeEnabled)
            return false;

        party = runtime;
        sessionId = NextId(ref nextSessionId);
        comboClock = 0f;
        router.Bind(runtime);
        if (executor == null)
            executor = GetComponent<PartyComboSkillExecutor>();
        executor?.BindParty(runtime, sessionId);
        return executor != null;
    }

    public void UnbindParty()
    {
        sessionId = 0;
        router.Unbind();
        CancelAllOffers(PartyComboRejectReason.SessionInvalid);
        executor?.UnbindParty();
        party = null;
        windowsByChain.Clear();
        dedupeWindows.Clear();
        inFlight.Clear();
        used.Clear();
        settlements.Clear();
        bufferedCandidates.Clear();
        PartyComboFeatureGate.Unbind(featureFlags);
    }

    public bool TryGetOffer(ChainActorRole role, out PartyComboOfferViewData view)
    {
        if (offers.TryGetValue(role, out PartyComboOpportunity offer) &&
            offer != null && offer.State == PartyComboOpportunityState.Offered)
        {
            view = new PartyComboOfferViewData(offer);
            return true;
        }

        view = default;
        return false;
    }

    public bool TryGetComboChargeStatus(
        ChainActorRole role,
        out PartyComboSkillDef definition,
        out SkillChargeStatus status)
    {
        definition = null;
        status = default;

        PartyRuntimeActor actor = party?.GetActor(role);
        CharacteContext actorContext = actor?.Context;
        if (actorContext == null || actorContext.baseStats == null || actorContext.SkillManager == null)
            return false;

        definition = actorContext.baseStats.partyComboSkill;
        return definition != null &&
               actorContext.SkillManager.TryGetPartyComboChargeStatus(definition, out status);
    }

    public bool TryConsumeOffer(
        ChainActorRole role,
        int expectedOfferId,
        out PartyComboRejectReason reason)
    {
        reason = PartyComboRejectReason.None;
        if (!IsBound || featureFlags == null || !featureFlags.runtimeEnabled || executor == null)
            return Reject(PartyComboRejectReason.Disabled, out reason);
        if (!offers.TryGetValue(role, out PartyComboOpportunity offer) || offer == null)
            return Reject(PartyComboRejectReason.NoOffer, out reason);
        if (offer.OfferId != expectedOfferId)
            return Reject(PartyComboRejectReason.StaleOffer, out reason);
        if (offer.State != PartyComboOpportunityState.Offered)
            return Reject(PartyComboRejectReason.Busy, out reason);
        if (comboClock >= offer.ExpiresAt || comboClock >= offer.ComboChainExpiresAt)
        {
            EndOffer(offer, PartyComboOpportunityState.Expired, PartyComboRejectReason.Expired);
            return Reject(PartyComboRejectReason.Expired, out reason);
        }
        if (GlobalTimeScaleManager.Instance.IsPaused)
            return Reject(PartyComboRejectReason.Paused, out reason);
        if (!IsOfferEligible(offer, requireCharge: true, out reason))
        {
            EndOffer(offer, PartyComboOpportunityState.Cancelled, reason);
            return false;
        }

        var useKey = new UseKey(offer.ComboWindowId, offer.Definition.RuntimeId, offer.OwnerRole);
        if (used.Contains(useKey) || !inFlight.Add(useKey))
            return Reject(PartyComboRejectReason.Busy, out reason);

        offer.State = PartyComboOpportunityState.Starting;
        Notify(offer);

        ulong executionId = NextId(ref nextExecutionId);
        offer.ExecutionId = executionId;
        var provenance = new ComboExecutionProvenance(
            sessionId,
            offer.ComboWindowId,
            executionId,
            offer.Definition.RuntimeId,
            offer.ComboChainExpiresAt);
        settlements[executionId] = new Settlement
        {
            State = SettlementState.Pending,
            ExpiresAt = offer.ComboChainExpiresAt,
        };

        PartyComboStartKind start = executor.TryStart(
            offer,
            provenance,
            info => OnExecutionCommitted(offer, useKey, provenance, info),
            failure => OnExecutionFailed(offer, useKey, executionId, failure));

        if (start == PartyComboStartKind.Accepted)
        {
            if (offer.State == PartyComboOpportunityState.Starting)
            {
                offer.State = PartyComboOpportunityState.Executing;
                Notify(offer);
            }
            return true;
        }

        settlements[executionId].State = SettlementState.Rejected;
        DiscardBuffered(executionId);
        inFlight.Remove(useKey);

        if (start == PartyComboStartKind.Busy || start == PartyComboStartKind.Paused)
        {
            if (IsCurrentOffer(offer) && comboClock < offer.ExpiresAt && IsOfferEligible(offer, false, out _))
            {
                offer.State = PartyComboOpportunityState.Offered;
                Notify(offer);
            }
            reason = start == PartyComboStartKind.Paused
                ? PartyComboRejectReason.Paused
                : PartyComboRejectReason.Busy;
            return false;
        }

        reason = start == PartyComboStartKind.PlacementFailed
            ? PartyComboRejectReason.PlacementFailed
            : PartyComboRejectReason.CastRejected;
        EndOffer(offer, PartyComboOpportunityState.Failed, reason);
        return false;
    }

    void OnEventRouted(PartyRuntimeActor eventActor, PassiveEventContext context)
    {
        if (!IsBound || !featureFlags.runtimeEnabled || context.FactId == 0 || context.ChainId == 0)
            return;
        if (context.Depth >= featureFlags.maxComboDepth)
            return;

        if (!TryResolveWindow(context, out ulong windowId, out float windowExpiresAt))
            return;

        ComboExecutionProvenance provenance = context.ComboProvenance;
        if (provenance.IsValid)
        {
            if (!settlements.TryGetValue(provenance.ExecutionId, out Settlement settlement))
                return;
            if (settlement.State == SettlementState.Rejected)
                return;

            if (settlement.State == SettlementState.Pending)
            {
                EvaluateAndBuffer(eventActor, context, windowId, windowExpiresAt, provenance.ExecutionId);
                return;
            }
        }

        EvaluateAndDeliver(eventActor, context, windowId, windowExpiresAt);
    }

    bool TryResolveWindow(in PassiveEventContext context, out ulong windowId, out float expiresAt)
    {
        ComboExecutionProvenance provenance = context.ComboProvenance;
        if (provenance.IsValid)
        {
            windowId = provenance.ComboWindowId;
            expiresAt = provenance.ChainExpiresAt;
            return provenance.SessionId == sessionId && comboClock < expiresAt;
        }

        if (windowsByChain.TryGetValue(context.ChainId, out WindowState existing) &&
            comboClock < existing.ExpiresAt)
        {
            windowId = existing.WindowId;
            expiresAt = existing.ExpiresAt;
            return true;
        }

        float lifetime = Mathf.Max(0.1f, featureFlags.maxComboChainLifetimeSeconds);
        var created = new WindowState
        {
            WindowId = NextId(ref nextWindowId),
            ExpiresAt = comboClock + lifetime,
        };
        windowsByChain[context.ChainId] = created;
        windowId = created.WindowId;
        expiresAt = created.ExpiresAt;
        return true;
    }

    void EvaluateAndBuffer(
        PartyRuntimeActor eventActor,
        in PassiveEventContext context,
        ulong windowId,
        float windowExpiresAt,
        ulong executionId)
    {
        List<Candidate> matches = BuildCandidates(eventActor, context, windowId, windowExpiresAt);
        if (matches.Count == 0)
            return;

        if (!bufferedCandidates.TryGetValue(executionId, out List<Candidate> buffer))
        {
            buffer = new List<Candidate>();
            bufferedCandidates.Add(executionId, buffer);
        }
        buffer.AddRange(matches);
    }

    void EvaluateAndDeliver(
        PartyRuntimeActor eventActor,
        in PassiveEventContext context,
        ulong windowId,
        float windowExpiresAt)
    {
        List<Candidate> matches = BuildCandidates(eventActor, context, windowId, windowExpiresAt);
        for (int i = 0; i < matches.Count; i++)
            Deliver(matches[i]);
    }

    List<Candidate> BuildCandidates(
        PartyRuntimeActor eventActor,
        in PassiveEventContext context,
        ulong windowId,
        float windowExpiresAt)
    {
        var matches = new List<Candidate>(party.Actors.Count);
        for (int i = 0; i < party.Actors.Count; i++)
        {
            PartyRuntimeActor owner = party.Actors[i];
            if (owner == null || owner.Role == ChainActorRole.Player || owner.Role == ChainActorRole.Helper)
                continue;

            PartyComboSkillDef definition = owner.Context != null && owner.Context.baseStats != null
                ? owner.Context.baseStats.partyComboSkill
                : null;
            if (definition == null || !PartyComboTriggerEvaluator.Matches(definition, party, owner, eventActor, context))
                continue;

            var useKey = new UseKey(windowId, definition.RuntimeId, owner.Role);
            if (used.Contains(useKey) || inFlight.Contains(useKey))
                continue;

            var dedupe = new DedupeKey(context.FactId, definition.RuntimeId, owner.Role);
            if (dedupeWindows.ContainsKey(dedupe))
                continue;

            if (!CanCreateOffer(owner, definition, context, out _))
                continue;

            dedupeWindows.Add(dedupe, windowId);
            matches.Add(new Candidate
            {
                Owner = owner,
                EventActor = eventActor,
                Definition = definition,
                Context = context,
                WindowId = windowId,
                WindowExpiresAt = windowExpiresAt,
                ReceivedAt = comboClock,
                Dedupe = dedupe,
            });
        }
        return matches;
    }

    void Deliver(Candidate candidate)
    {
        if (candidate == null || comboClock >= candidate.WindowExpiresAt)
            return;
        if (!CanCreateOffer(candidate.Owner, candidate.Definition, candidate.Context, out SkillTargetHandle target))
            return;

        if (offers.TryGetValue(candidate.Owner.Role, out PartyComboOpportunity existing))
        {
            if (TryRefresh(existing, candidate, target))
                return;
            if (candidate.Definition.refreshPolicy != PartyComboOfferRefreshPolicy.Replace ||
                existing.State != PartyComboOpportunityState.Offered)
            {
                return;
            }
            EndOffer(existing, PartyComboOpportunityState.Cancelled, PartyComboRejectReason.StaleOffer);
        }

        float duration = Mathf.Max(0.1f, candidate.Definition.offerDurationSeconds);
        float maximumLifetime = Mathf.Max(duration, candidate.Definition.maxOfferLifetimeSeconds);
        var offer = new PartyComboOpportunity
        {
            OfferId = NextOfferId(),
            Definition = candidate.Definition,
            OwnerRole = candidate.Owner.Role,
            OwnerContext = candidate.Owner.Context,
            Target = target,
            OfferedAt = candidate.ReceivedAt,
            ExpiresAt = Mathf.Min(candidate.ReceivedAt + duration, candidate.ReceivedAt + maximumLifetime, candidate.WindowExpiresAt),
            ComboChainExpiresAt = candidate.WindowExpiresAt,
            SessionId = sessionId,
            ComboWindowId = candidate.WindowId,
            TriggerContext = candidate.Context,
            State = PartyComboOpportunityState.Offered,
        };
        offers[candidate.Owner.Role] = offer;
        Notify(offer);
    }

    bool TryRefresh(
        PartyComboOpportunity offer,
        Candidate candidate,
        SkillTargetHandle target)
    {
        if (offer == null || offer.State != PartyComboOpportunityState.Offered ||
            offer.Definition != candidate.Definition || offer.ComboWindowId != candidate.WindowId ||
            candidate.Definition.refreshPolicy != PartyComboOfferRefreshPolicy.RefreshSameTarget ||
            offer.Target.OriginalInstanceId != target.OriginalInstanceId)
        {
            return false;
        }

        float maximum = offer.OfferedAt + Mathf.Max(
            offer.Definition.offerDurationSeconds,
            offer.Definition.maxOfferLifetimeSeconds);
        offer.ExpiresAt = Mathf.Min(
            comboClock + Mathf.Max(0.1f, offer.Definition.offerDurationSeconds),
            maximum,
            offer.ComboChainExpiresAt);
        Notify(offer);
        return true;
    }

    bool CanCreateOffer(
        PartyRuntimeActor owner,
        PartyComboSkillDef definition,
        in PassiveEventContext context,
        out SkillTargetHandle target)
    {
        target = ResolveTarget(definition, owner, context);
        if (definition.executionSkill == null || definition.executionProfile == null || owner?.Context == null)
            return false;

        owner.Context.ResolveReferences();
        if (definition.requireOwnerAlive &&
            (owner.Context.HealthSystem == null || !owner.Context.HealthSystem.IsAlive))
        {
            return false;
        }

        if (definition.ownerBusyPolicy == PartyComboOwnerBusyPolicy.RejectWhenBusy &&
            owner.FieldMember != null &&
            (owner.FieldMember.IsReserved || owner.FieldMember.HasActiveSequenceExecution))
        {
            return false;
        }

        if (definition.requireTargetAlive &&
            (target == null || !target.TryResolveAliveTarget(out _, out _)))
        {
            return false;
        }

        CharacterSkillManager manager = owner.Context.SkillManager;
        return manager != null &&
               manager.TryGetPartyComboChargeStatus(definition, out SkillChargeStatus charge) &&
               charge.HasCharge;
    }

    SkillTargetHandle ResolveTarget(
        PartyComboSkillDef definition,
        PartyRuntimeActor owner,
        in PassiveEventContext context)
    {
        GameObject targetObject = definition.targetPolicy switch
        {
            PartyComboTargetPolicy.Self => owner?.Context != null ? owner.Context.gameObject : null,
            PartyComboTargetPolicy.EventActor => context.Actor,
            _ => context.Target,
        };
        CharacteContext target = targetObject != null
            ? CharacterContextModuleLookup.ResolveContext(targetObject)
            : null;
        if (target != null)
            return SkillTargetHandle.For(target);

        // Map_TestAI deliberately uses a lightweight damageable instead of a full character
        // prefab. Keep this exception explicit so normal character targeting remains context-first.
        ChainAttackTestTarget testTarget = targetObject != null
            ? targetObject.GetComponentInParent<ChainAttackTestTarget>()
            : null;
        return SkillTargetHandle.ForNonCharacterDamageable(testTarget);
    }

    bool IsOfferEligible(
        PartyComboOpportunity offer,
        bool requireCharge,
        out PartyComboRejectReason reason)
    {
        if (offer == null || offer.SessionId != sessionId || offer.OwnerContext == null)
            return Reject(PartyComboRejectReason.SessionInvalid, out reason);
        offer.OwnerContext.ResolveReferences();
        if (offer.OwnerContext.HealthSystem == null || !offer.OwnerContext.HealthSystem.IsAlive)
            return Reject(PartyComboRejectReason.OwnerInvalid, out reason);
        if (offer.Definition.requireTargetAlive &&
            (offer.Target == null || !offer.Target.TryResolveAliveTarget(out _, out _)))
        {
            return Reject(PartyComboRejectReason.TargetInvalid, out reason);
        }
        if (requireCharge &&
            (!offer.OwnerContext.SkillManager.TryGetPartyComboChargeStatus(
                 offer.Definition,
                 out SkillChargeStatus charge) || !charge.HasCharge))
        {
            return Reject(PartyComboRejectReason.Busy, out reason);
        }

        reason = PartyComboRejectReason.None;
        return true;
    }

    void OnExecutionCommitted(
        PartyComboOpportunity offer,
        UseKey useKey,
        ComboExecutionProvenance provenance,
        ActiveSkillCastInfo info)
    {
        if (!IsCurrentOffer(offer) || offer.SessionId != sessionId || offer.ExecutionId != provenance.ExecutionId)
            return;
        if (!settlements.TryGetValue(provenance.ExecutionId, out Settlement settlement) ||
            settlement.State != SettlementState.Pending)
        {
            return;
        }

        settlement.State = SettlementState.Committed;
        inFlight.Remove(useKey);
        used.Add(useKey);
        offer.State = PartyComboOpportunityState.Committed;
        Notify(offer);

        CombatEventBus bus = offer.OwnerContext != null ? offer.OwnerContext.CombatEventBus : null;
        if (bus != null && comboClock < offer.ComboChainExpiresAt)
        {
            var metadata = new CombatEventMetadata(
                sourceKind: CombatSourceKind.Skill,
                comboSkillId: offer.Definition.RuntimeId,
                comboOwnerRole: offer.OwnerRole);
            PassiveEventContext committed = bus.CreateChildContext(
                offer.TriggerContext,
                PassiveEventType.ComboSkillCommitted,
                offer.OwnerContext.gameObject,
                ResolveTargetObject(offer.Target),
                $"party-combo:{offer.Definition.RuntimeId}",
                value: 1f,
                origin: PassiveEventOrigin.System,
                metadata: metadata,
                actor: offer.OwnerContext.gameObject,
                comboProvenance: provenance);
            bus.Publish(committed);
        }

        FlushBuffered(provenance.ExecutionId);
        if (IsCurrentOffer(offer))
            offers.Remove(offer.OwnerRole);
    }

    void OnExecutionFailed(
        PartyComboOpportunity offer,
        UseKey useKey,
        ulong executionId,
        PartyComboRejectReason failure)
    {
        if (settlements.TryGetValue(executionId, out Settlement settlement) &&
            settlement.State == SettlementState.Pending)
        {
            settlement.State = SettlementState.Rejected;
        }
        DiscardBuffered(executionId);
        inFlight.Remove(useKey);
        if (IsCurrentOffer(offer))
            EndOffer(offer, PartyComboOpportunityState.Failed, failure);
    }

    void FlushBuffered(ulong executionId)
    {
        if (!bufferedCandidates.TryGetValue(executionId, out List<Candidate> candidates))
            return;

        bufferedCandidates.Remove(executionId);
        for (int i = 0; i < candidates.Count; i++)
            Deliver(candidates[i]);
    }

    void DiscardBuffered(ulong executionId)
    {
        bufferedCandidates.Remove(executionId);
    }

    void ExpireOffers()
    {
        if (offers.Count == 0)
            return;
        offerScratch.Clear();
        foreach (PartyComboOpportunity offer in offers.Values)
            offerScratch.Add(offer);
        for (int i = 0; i < offerScratch.Count; i++)
        {
            PartyComboOpportunity offer = offerScratch[i];
            if (offer != null && offer.State == PartyComboOpportunityState.Offered &&
                (comboClock >= offer.ExpiresAt || comboClock >= offer.ComboChainExpiresAt))
            {
                EndOffer(offer, PartyComboOpportunityState.Expired, PartyComboRejectReason.Expired);
            }
        }
    }

    void CleanupExpiredState()
    {
        if (windowsByChain.Count > 0)
        {
            idScratch.Clear();
            foreach (KeyValuePair<ulong, WindowState> pair in windowsByChain)
            {
                if (comboClock >= pair.Value.ExpiresAt)
                    idScratch.Add(pair.Key);
            }
            for (int i = 0; i < idScratch.Count; i++)
                windowsByChain.Remove(idScratch[i]);
        }

        if (dedupeWindows.Count > 0)
        {
            dedupeScratch.Clear();
            foreach (KeyValuePair<DedupeKey, ulong> pair in dedupeWindows)
            {
                if (!IsWindowActive(pair.Value))
                    dedupeScratch.Add(pair.Key);
            }
            for (int i = 0; i < dedupeScratch.Count; i++)
                dedupeWindows.Remove(dedupeScratch[i]);
        }

        if (settlements.Count > 0)
        {
            idScratch.Clear();
            foreach (KeyValuePair<ulong, Settlement> pair in settlements)
            {
                if (comboClock >= pair.Value.ExpiresAt && !bufferedCandidates.ContainsKey(pair.Key))
                    idScratch.Add(pair.Key);
            }
            for (int i = 0; i < idScratch.Count; i++)
                settlements.Remove(idScratch[i]);
        }

        CollectExpiredUses(used);
        for (int i = 0; i < useScratch.Count; i++)
            used.Remove(useScratch[i]);
        CollectExpiredUses(inFlight);
        for (int i = 0; i < useScratch.Count; i++)
            inFlight.Remove(useScratch[i]);
    }

    void CollectExpiredUses(HashSet<UseKey> source)
    {
        useScratch.Clear();
        foreach (UseKey key in source)
        {
            if (!IsWindowActive(key.WindowId))
                useScratch.Add(key);
        }
    }

    bool IsWindowActive(ulong windowId)
    {
        foreach (WindowState window in windowsByChain.Values)
        {
            if (window.WindowId == windowId && comboClock < window.ExpiresAt)
                return true;
        }
        foreach (PartyComboOpportunity offer in offers.Values)
        {
            if (offer.ComboWindowId == windowId && comboClock < offer.ComboChainExpiresAt)
                return true;
        }
        return false;
    }

    void CancelForInvalidControlledActor()
    {
        PartyRuntimeActor controlled = party?.GetActor(ChainActorRole.Player);
        CharacteContext context = controlled?.Context;
        context?.ResolveReferences();
        if (context == null || context.HealthSystem == null || !context.HealthSystem.IsAlive)
        {
            CancelAllOffers(PartyComboRejectReason.OwnerInvalid);
            executor?.CancelAll();
            RejectUnsettledExecutions();
        }
    }

    void RejectUnsettledExecutions()
    {
        foreach (Settlement settlement in settlements.Values)
        {
            if (settlement.State == SettlementState.Pending)
                settlement.State = SettlementState.Rejected;
        }

        bufferedCandidates.Clear();
        inFlight.Clear();
    }

    void CancelAllOffers(PartyComboRejectReason reason)
    {
        if (offers.Count == 0)
            return;
        offerScratch.Clear();
        foreach (PartyComboOpportunity offer in offers.Values)
            offerScratch.Add(offer);
        for (int i = 0; i < offerScratch.Count; i++)
            EndOffer(offerScratch[i], PartyComboOpportunityState.Cancelled, reason);
        offers.Clear();
    }

    void EndOffer(
        PartyComboOpportunity offer,
        PartyComboOpportunityState state,
        PartyComboRejectReason reason)
    {
        if (offer == null)
            return;
        offer.State = state;
        offer.EndReason = reason;
        Notify(offer);
        if (offers.TryGetValue(offer.OwnerRole, out PartyComboOpportunity current) &&
            ReferenceEquals(current, offer))
        {
            offers.Remove(offer.OwnerRole);
        }
    }

    bool IsCurrentOffer(PartyComboOpportunity offer)
    {
        return offer != null &&
               offers.TryGetValue(offer.OwnerRole, out PartyComboOpportunity current) &&
               ReferenceEquals(current, offer);
    }

    void Notify(PartyComboOpportunity offer)
    {
        if (featureFlags != null && featureFlags.debugLogging && offer != null)
        {
            Debug.Log(
                $"[PartyCombo] combo={offer.Definition?.RuntimeId ?? "<none>"} " +
                $"offer={offer.OfferId} execution={offer.ExecutionId} role={offer.OwnerRole} " +
                $"fact={offer.TriggerContext.FactId} target={ResolveTargetObject(offer.Target)?.name ?? "<none>"} " +
                $"chain={offer.TriggerContext.ChainId} depth={offer.TriggerContext.Depth} " +
                $"state={offer.State} reason={offer.EndReason}",
                this);
        }
        OfferChanged?.Invoke(new PartyComboOfferViewData(offer));
    }

    int NextOfferId()
    {
        int id = nextOfferId++;
        if (id <= 0)
        {
            nextOfferId = 2;
            id = 1;
        }
        return id;
    }

    static ulong NextId(ref long counter)
    {
        return unchecked((ulong)Interlocked.Increment(ref counter));
    }

    static bool Reject(PartyComboRejectReason value, out PartyComboRejectReason reason)
    {
        reason = value;
        return false;
    }

    static GameObject ResolveTargetObject(SkillTargetHandle target)
    {
        return target != null && target.TryResolveLiveContext(out CharacteContext context)
            ? context.gameObject
            : null;
    }
}
