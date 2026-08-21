#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Covers the cast transaction end to end: resources are reserved when a cast starts, the stats it
/// was priced with are frozen for its whole life, and every exit path settles that reservation
/// exactly once.
///
/// These are pure-logic checks. The animated path — animation acceptance, cast-point timing,
/// interruption — needs a live Animancer driver and stays a Play Mode check; what is verified here
/// is the settlement rule each of those paths ends up calling.
/// </summary>
public sealed class SkillCastTransactionSmokeTests
{
    readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
        FailingProbePayload.ExecutionCount = 0;
        SucceedingProbePayload.ExecutionCount = 0;
    }

    // ---- Charge reservations --------------------------------------------------------------------

    [Test]
    public void ReservingTakesTheChargeImmediatelyAndReleasingGivesItBack()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);

        Assert.That(charges.TryReserve(1, 0f), Is.True);
        Assert.That(charges.AvailableCharges, Is.EqualTo(0),
            "A reserved charge must leave the pool right away, not at the cast point.");
        Assert.That(charges.RechargingCount, Is.EqualTo(0), "Reserving must not start a cooldown.");

        Assert.That(charges.ReleaseReservation(1), Is.True);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
        Assert.That(charges.RechargingCount, Is.EqualTo(0));
    }

    [Test]
    public void ReserveCommitAndReleaseAreAllIdempotent()
    {
        var charges = new SkillChargeState();
        charges.Refresh(2, 0f);

        Assert.That(charges.TryReserve(7, 0f), Is.True);
        Assert.That(charges.TryReserve(7, 0f), Is.True, "Re-reserving one token must not take a second charge.");
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));

        Assert.That(charges.CommitReservation(7, 0f, 12f), Is.True);
        Assert.That(charges.CommitReservation(7, 0f, 12f), Is.False, "A settled token commits once.");
        Assert.That(charges.ReleaseReservation(7), Is.False, "A committed token cannot then be refunded.");
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
        Assert.That(charges.RechargingCount, Is.EqualTo(1));

        Assert.That(charges.ReleaseReservation(99), Is.False, "An unknown token is a no-op.");
    }

    [Test]
    public void ReservedChargesRechargeSequentiallyOnceCommitted()
    {
        var charges = new SkillChargeState();
        charges.Refresh(2, 0f);

        Assert.That(charges.TryReserve(1, 0f), Is.True);
        Assert.That(charges.TryReserve(2, 0f), Is.True);
        charges.CommitReservation(1, 0f, 12f);
        charges.CommitReservation(2, 0f, 12f);

        charges.Refresh(2, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1), "The second charge queues behind the first.");

        charges.Refresh(2, 24f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(2));
    }

    [Test]
    public void AnEmptyPoolRefusesAReservation()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);

        Assert.That(charges.TryReserve(1, 0f), Is.True);
        Assert.That(charges.TryReserve(2, 0f), Is.False, "A reserved charge is not available to a second cast.");
    }

    [Test]
    public void ResetToFullDropsOutstandingReservations()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);
        charges.TryReserve(1, 0f);

        charges.ResetToFull();

        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
        Assert.That(charges.ReleaseReservation(1), Is.False,
            "A dropped reservation must not be able to push the refilled pool above its maximum.");
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
    }

    // ---- Energy reservations --------------------------------------------------------------------

    [Test]
    public void ReservedEnergyLeavesTheSpendablePoolBeforeItIsSpent()
    {
        SkillUserSystem user = CreateEnergyUser(100f);

        Assert.That(user.TryReserveEnergy(1, 30f), Is.True);
        Assert.That(user.CurrentEnergy, Is.EqualTo(70f), "Reserved energy is not spendable.");
        Assert.That(user.StoredEnergy, Is.EqualTo(100f), "But it has not actually been spent yet.");

        user.CommitEnergyReservation(1);
        Assert.That(user.StoredEnergy, Is.EqualTo(70f));
        Assert.That(user.CurrentEnergy, Is.EqualTo(70f));
    }

    [Test]
    public void ReleasingAnEnergyReservationRefundsTheWholeAmount()
    {
        SkillUserSystem user = CreateEnergyUser(100f);

        Assert.That(user.TryReserveEnergy(1, 30f), Is.True);
        Assert.That(user.ReleaseEnergyReservation(1), Is.True);

        Assert.That(user.CurrentEnergy, Is.EqualTo(100f));
        Assert.That(user.ReleaseEnergyReservation(1), Is.False, "A settled token refunds once.");
        Assert.That(user.CurrentEnergy, Is.EqualTo(100f));
    }

    [Test]
    public void TwoCastsWindingUpCannotSpendTheSameEnergy()
    {
        SkillUserSystem user = CreateEnergyUser(100f);

        Assert.That(user.TryReserveEnergy(1, 60f), Is.True);
        Assert.That(user.TryReserveEnergy(2, 60f), Is.False,
            "The second cast sees only what the first one left behind.");
        Assert.That(user.TryReserveEnergy(2, 40f), Is.True);
        Assert.That(user.CurrentEnergy, Is.EqualTo(0f));
    }

    // ---- SkillInstance reservations ---------------------------------------------------------------

    [Test]
    public void AReservedCastHoldsItsChargeForTheWholeWindUp()
    {
        SkillGemDefinition def = CreateSkill("test.reserve", manaCost: 0f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());
        var user = new ProbeSkillUser(100f);

        Assert.That(instance.TryReserveCast(user, false, true, out SkillCastReservation reservation), Is.True);
        Assert.That(instance.CanCast(user), Is.False,
            "A second press during the wind-up must not find the charge the first cast is holding.");
        Assert.That(instance.TryGetChargeStatus(user, out SkillChargeStatus status), Is.True);
        Assert.That(status.Available, Is.EqualTo(0), "The HUD shows the skill as unavailable while it winds up.");

        reservation.Release();
        Assert.That(instance.CanCast(user), Is.True, "An abandoned cast gives the charge straight back.");
    }

    [Test]
    public void ABlockedPreCastBurnsTheCooldownButRefundsTheEnergy()
    {
        SkillUserSystem user = CreateEnergyUser(100f);
        SkillGemDefinition def = CreateSkill("test.blocked", manaCost: 25f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.TryReserveCast(user, false, true, out SkillCastReservation reservation), Is.True);
        Assert.That(user.CurrentEnergy, Is.EqualTo(75f));

        reservation.CommitChargeOnly();

        Assert.That(user.CurrentEnergy, Is.EqualTo(100f), "A blocked pre-cast keeps its energy.");
        Assert.That(instance.CanCast(user), Is.False, "But it still burns the cooldown.");
    }

    [Test]
    public void AnInterruptedPreCastCostsNothingAtAll()
    {
        SkillUserSystem user = CreateEnergyUser(100f);
        SkillGemDefinition def = CreateSkill("test.interrupted", manaCost: 25f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.TryReserveCast(user, false, true, out SkillCastReservation reservation), Is.True);
        reservation.Release();

        Assert.That(user.CurrentEnergy, Is.EqualTo(100f));
        Assert.That(instance.CanCast(user), Is.True);
    }

    [Test]
    public void StampCooldownFalseRefundsTheChargeEvenOnCommit()
    {
        SkillGemDefinition def = CreateSkill("test.noStamp", manaCost: 0f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());
        var user = new ProbeSkillUser(100f);

        Assert.That(instance.TryReserveCast(user, true, false, out SkillCastReservation reservation), Is.True);
        reservation.Commit();

        Assert.That(instance.CanCast(user), Is.True,
            "Interruption-style casts opt out of the cooldown entirely.");
    }

    [Test]
    public void TheStatsSnapshotIsFrozenWhenTheCastIsReserved()
    {
        SkillGemDefinition def = CreateSkill("test.snapshot", manaCost: 25f, cooldown: 12f);
        def.baseDamage = 100f;

        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());
        var user = new ProbeSkillUser(100f);

        Assert.That(instance.TryReserveCast(user, false, true, out SkillCastReservation reservation), Is.True);
        Assert.That(reservation.Stats.damage, Is.EqualTo(100f));

        // A buff landing mid wind-up rewrites what a *future* cast would cost, never this one.
        var snapshot = new SkillUpgradeStatSnapshot();
        snapshot.AddNode(new SkillUpgradeNodeData
        {
            nodeId = "mid_windup_buff",
            statModifiers = new List<StatModifier>
            {
                new StatModifier { stat = StatType.Damage, add = 500f, mul = 1f },
            },
        });
        instance.upgradeSnapshot = snapshot;

        Assert.That(reservation.Stats.damage, Is.EqualTo(100f),
            "A cast already in flight keeps the stats it was priced with.");
        Assert.That(instance.GetFinalStats(user).damage, Is.EqualTo(600f),
            "The next cast does pick the buff up.");
    }

    [Test]
    public void AFailedPayloadRollsBackEverythingItReserved()
    {
        SkillUserSystem user = CreateEnergyUser(100f);
        SkillGemDefinition def = CreateSkill("test.rollback", manaCost: 25f, cooldown: 12f);
        def.payload = Track(ScriptableObject.CreateInstance<FailingProbePayload>());

        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.Cast(user, null, 0, out SkillExecutionResult result), Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.PlacementBlocked));
        Assert.That(user.CurrentEnergy, Is.EqualTo(100f), "A refused cast refunds its energy.");
        Assert.That(user.StoredEnergy, Is.EqualTo(100f));
        Assert.That(instance.CanCast(user), Is.True, "And its charge.");
    }

    [Test]
    public void ASuccessfulPayloadCommitsEnergyAndCooldownTogether()
    {
        SkillUserSystem user = CreateEnergyUser(100f);
        SkillGemDefinition def = CreateSkill("test.commit", manaCost: 25f, cooldown: 12f);

        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.Cast(user, null, 0, out _), Is.True);
        Assert.That(user.StoredEnergy, Is.EqualTo(75f));
        Assert.That(user.CurrentEnergy, Is.EqualTo(75f));
        Assert.That(instance.CanCast(user), Is.False);
    }

    // ---- Orchestrator ------------------------------------------------------------------------------

    [Test]
    public void TheImmediateCastPathRaisesCastReleasedBeforeRunningThePayload()
    {
        var owner = Track(new GameObject("orchestrator_owner"));
        var orchestrator = new SkillCastOrchestrator(owner.transform);

        SkillGemDefinition def = CreateSkill("test.orchestrator.ok", manaCost: 0f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(orchestrator.GetOrCreateCharges(def));
        var user = new ProbeSkillUser(100f);

        var order = new List<string>();
        orchestrator.CastStarted += _ => order.Add("started");
        orchestrator.CastReleased += _ => order.Add("released");
        orchestrator.CastExecutionFailed += (_, __) => order.Add("failed");

        SkillCastStartResult result = orchestrator.TryStartCast(new SkillCastRequest(
            instance,
            user,
            animationDriver: null,
            onStarted: () => order.Add("onStarted"),
            useAnimationDriver: false,
            debugSource: "test"));

        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.ImmediateSuccess));
        Assert.That(order, Is.EqualTo(new[] { "onStarted", "released" }),
            "CastReleased means 'reached the cast point' and fires before the payload runs.");
        Assert.That(SucceedingProbePayload.ExecutionCount, Is.EqualTo(1));
        Assert.That(instance.CanCast(user), Is.False, "A successful immediate cast commits its charge.");
    }

    [Test]
    public void APayloadThatProducesNothingReachesTheCastPointAndThenRollsBack()
    {
        var owner = Track(new GameObject("orchestrator_owner"));
        var orchestrator = new SkillCastOrchestrator(owner.transform);

        SkillGemDefinition def = CreateSkill("test.orchestrator.fail", manaCost: 0f, cooldown: 12f);
        def.payload = Track(ScriptableObject.CreateInstance<FailingProbePayload>());
        var instance = new SkillInstance { def = def };
        instance.BindCharges(orchestrator.GetOrCreateCharges(def));
        var user = new ProbeSkillUser(100f);

        var order = new List<string>();
        orchestrator.CastReleased += _ => order.Add("released");
        orchestrator.CastExecutionFailed += (_, __) => order.Add("failed");

        SkillCastStartResult result = orchestrator.TryStartCast(new SkillCastRequest(
            instance,
            user,
            animationDriver: null,
            useAnimationDriver: false,
            debugSource: "test"));

        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.Rejected));
        Assert.That(order, Is.EqualTo(new[] { "released", "failed" }),
            "The cast point still happened; the refusal is reported after it.");
        Assert.That(instance.CanCast(user), Is.True, "A payload that produced nothing costs nothing.");
    }

    [Test]
    public void ARejectedStartNeverRunsOnStartedAndKeepsTheChargeAvailable()
    {
        var owner = Track(new GameObject("orchestrator_owner"));
        var orchestrator = new SkillCastOrchestrator(owner.transform);

        SkillGemDefinition def = CreateSkill("test.orchestrator.noFallback", manaCost: 0f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(orchestrator.GetOrCreateCharges(def));
        var user = new ProbeSkillUser(100f);

        bool onStartedRan = false;

        // useAnimationDriver with no driver and no immediate fallback: nothing can accept this cast.
        SkillCastStartResult result = orchestrator.TryStartCast(new SkillCastRequest(
            instance,
            user,
            animationDriver: null,
            onStarted: () => onStartedRan = true,
            useAnimationDriver: true,
            allowImmediateFallback: false,
            debugSource: "test"));

        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.Rejected));
        Assert.That(onStartedRan, Is.False,
            "OnStarted has caster side effects, so it must not run for a cast nothing accepted.");
        Assert.That(SucceedingProbePayload.ExecutionCount, Is.EqualTo(0));
        Assert.That(instance.CanCast(user), Is.True, "The reservation was rolled back.");
    }

    [Test]
    public void ACastRefusedByCanProceedCostsNothing()
    {
        var owner = Track(new GameObject("orchestrator_owner"));
        var orchestrator = new SkillCastOrchestrator(owner.transform);

        SkillGemDefinition def = CreateSkill("test.orchestrator.canProceed", manaCost: 0f, cooldown: 12f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(orchestrator.GetOrCreateCharges(def));
        var user = new ProbeSkillUser(100f);

        SkillCastStartResult result = orchestrator.TryStartCast(new SkillCastRequest(
            instance,
            user,
            animationDriver: null,
            canProceed: () => false,
            useAnimationDriver: false,
            debugSource: "test"));

        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.Rejected));
        Assert.That(instance.CanCast(user), Is.True);
    }

    // ---- Payload results ----------------------------------------------------------------------------

    [Test]
    public void AnAreaHealThatFindsNobodyStillSucceedsAndStillCosts()
    {
        var payload = Track(ScriptableObject.CreateInstance<HealAreaSkillPayloadDef>());
        SetHealTargetToAllies(payload);
        SkillCastContext context = CreateCasterContext(payload, new FinalSkillStats
        {
            maxCharges = 1,
            areaRadius = 10f,
            healPower = 25f,
        });

        SkillExecutionResult result = payload.ExecuteWithResult(context);

        Assert.That(result.Success, Is.True,
            "Missing is a gameplay outcome the player owns, not a refusal the game refunds.");
    }

    [Test]
    public void AnAreaHealWithNoRadiusIsBrokenConfigurationAndRefunds()
    {
        var payload = Track(ScriptableObject.CreateInstance<HealAreaSkillPayloadDef>());
        SetHealTargetToAllies(payload);
        SkillCastContext context = CreateCasterContext(payload, new FinalSkillStats { maxCharges = 1 });

        SkillExecutionResult result = payload.ExecuteWithResult(context);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.MissingAuthoringData));
    }

    [Test]
    public void EveryPayloadWithoutARuntimeContextFailsInsteadOfSilentlyCostingTheCast()
    {
        AssertMissingContext(Track(ScriptableObject.CreateInstance<HealAreaSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<MorphSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<TauntSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<ProjectileSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<PrefabHitboxSkillPayloadDef>()));
        AssertMissingContext(Track(ScriptableObject.CreateInstance<SpawnPickupSkillPayloadDef>()));
    }

    /// <summary>The heal mode is authoring data, so it is set the way the inspector would set it.</summary>
    static void SetHealTargetToAllies(HealAreaSkillPayloadDef payload)
    {
        var serialized = new SerializedObject(payload);
        serialized.FindProperty("target").enumValueIndex = (int)HealTargetMode.Allies;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssertMissingContext(SkillPayloadDef payload)
    {
        SkillExecutionResult result = payload.ExecuteWithResult(null);
        Assert.That(result.Success, Is.False, $"{payload.GetType().Name} must refuse a null cast context.");
    }

    // ---- Faction rule -------------------------------------------------------------------------------

    [Test]
    public void TheSharedFactionRuleDecidesWhoCanBeTaunted()
    {
        IdentityProbeContext player = CreateIdentityContext(AITargetIdentity.Player);
        IdentityProbeContext companion = CreateIdentityContext(AITargetIdentity.Companion);
        IdentityProbeContext enemy = CreateIdentityContext(AITargetIdentity.Enemy);

        Assert.That(CharacterFactionUtility.AreHostile(player, enemy), Is.True);
        Assert.That(CharacterFactionUtility.AreHostile(companion, enemy), Is.True);
        Assert.That(CharacterFactionUtility.AreHostile(enemy, player), Is.True);

        Assert.That(CharacterFactionUtility.AreHostile(player, companion), Is.False, "Friendly fire is not a taunt.");
        Assert.That(CharacterFactionUtility.AreHostile(player, player), Is.False);
        Assert.That(CharacterFactionUtility.AreHostile(enemy, enemy), Is.False);

        foreach (AITargetIdentity sideless in new[]
                 {
                     AITargetIdentity.Auto,
                     AITargetIdentity.Generic,
                     AITargetIdentity.Neutral,
                 })
        {
            IdentityProbeContext neutral = CreateIdentityContext(sideless);
            Assert.That(CharacterFactionUtility.AreHostile(player, neutral), Is.False,
                $"{sideless} has no side, so it is never taunted.");
            Assert.That(CharacterFactionUtility.AreFriendly(player, neutral), Is.False,
                $"{sideless} is not friendly either — the two checks are not inverses.");
        }

        Assert.That(CharacterFactionUtility.AreHostile(player, null), Is.False);
        Assert.That(CharacterFactionUtility.AreHostile(null, enemy), Is.False);
    }

    [Test]
    public void TheBarrierWrapperStillAnswersTheSameAsTheSharedRule()
    {
        IdentityProbeContext player = CreateIdentityContext(AITargetIdentity.Player);
        IdentityProbeContext enemy = CreateIdentityContext(AITargetIdentity.Enemy);

        Assert.That(BarrierFactionUtility.AreHostile(player, enemy),
            Is.EqualTo(CharacterFactionUtility.AreHostile(player, enemy)));
        Assert.That(BarrierFactionUtility.AreFriendly(player, player),
            Is.EqualTo(CharacterFactionUtility.AreFriendly(player, player)));
    }

    // ---- Context-driven peer module lookup -----------------------------------------------------------

    [Test]
    public void PeerModulesResolveThroughContextForEveryPrefabLayout()
    {
        AssertBusResolves(ModuleLayout.OnContextRoot);
        AssertBusResolves(ModuleLayout.OnChildBranch);
        AssertBusResolves(ModuleLayout.OnParentAboveContext);
        AssertBusResolves(ModuleLayout.OnSiblingBranch);
    }

    enum ModuleLayout
    {
        OnContextRoot,
        OnChildBranch,
        OnParentAboveContext,
        OnSiblingBranch,
    }

    void AssertBusResolves(ModuleLayout layout)
    {
        var root = Track(new GameObject($"actor_{layout}"));
        var contextHost = root;

        if (layout == ModuleLayout.OnParentAboveContext)
        {
            contextHost = new GameObject("context_below_root");
            contextHost.transform.SetParent(root.transform, false);
        }

        IdentityProbeContext context = contextHost.AddComponent<IdentityProbeContext>();
        context.SetIdentity(AITargetIdentity.Player);

        GameObject busHost;
        switch (layout)
        {
            case ModuleLayout.OnChildBranch:
                busHost = new GameObject("GamePlayStats_System");
                busHost.transform.SetParent(contextHost.transform, false);
                break;
            case ModuleLayout.OnParentAboveContext:
                busHost = root;
                break;
            case ModuleLayout.OnSiblingBranch:
                busHost = new GameObject("Combat_System");
                busHost.transform.SetParent(root.transform, false);
                break;
            default:
                busHost = contextHost;
                break;
        }

        busHost.AddComponent<CombatEventBus>();

        var userHost = new GameObject("Skill_System");
        userHost.transform.SetParent(contextHost.transform, false);

        CombatEventBus resolved = CharacterContextModuleLookup.ResolveCombatEventBus(userHost);

        Assert.That(resolved, Is.Not.Null,
            $"A one-direction GetComponent would have missed the {layout} layout and silently dropped every combat event.");
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    SkillGemDefinition CreateSkill(string id, float manaCost, float cooldown)
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = id;
        def.baseManaCost = manaCost;
        def.baseCooldown = cooldown;
        def.baseMaxCharges = 1;
        def.payload = Track(ScriptableObject.CreateInstance<SucceedingProbePayload>());
        return def;
    }

    /// <summary>
    /// A real <see cref="SkillUserSystem"/> so the energy reservation path is exercised rather than
    /// the plain-ISkillUser fallback.
    /// </summary>
    SkillUserSystem CreateEnergyUser(float energy)
    {
        var host = Track(new GameObject("energy_user"));
        IdentityProbeContext context = host.AddComponent<IdentityProbeContext>();
        context.SetIdentity(AITargetIdentity.Player);

        var stats = Track(ScriptableObject.CreateInstance<CharacterStats>());
        stats.Enagy = energy;
        context.baseStats = stats;

        return host.AddComponent<SkillUserSystem>();
    }

    IdentityProbeContext CreateIdentityContext(AITargetIdentity identity)
    {
        var host = Track(new GameObject($"identity_{identity}"));
        IdentityProbeContext context = host.AddComponent<IdentityProbeContext>();
        context.SetIdentity(identity);
        return context;
    }

    /// <summary>Cast context whose caster is a real component, so CasterRoot/CasterContext resolve.</summary>
    SkillCastContext CreateCasterContext(SkillPayloadDef payload, FinalSkillStats stats)
    {
        var host = Track(new GameObject("caster"));
        IdentityProbeContext context = host.AddComponent<IdentityProbeContext>();
        context.SetIdentity(AITargetIdentity.Player);

        var user = host.AddComponent<ProbeSkillUserBehaviour>();

        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.casterContext";
        def.payload = payload;

        return new SkillCastContext(user, def, stats);
    }

    T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    /// <summary>Plain ISkillUser: no reservation support, so it exercises the spend-at-commit fallback.</summary>
    sealed class ProbeSkillUser : ISkillUser
    {
        public ProbeSkillUser(float energy) => Energy = energy;

        public float Energy { get; private set; }
        public Transform CastOrigin => null;
        public Transform AimTransform => null;
        public Vector3 AimDirection => Vector3.forward;
        public float currentEnagy => Energy;
        public StatsHub StatsHub => null;

        public void SpendEnagy(float amount) => Energy -= amount;
    }
}

/// <summary>Component-based ISkillUser so SkillCastContext can resolve a caster object and context.</summary>
public sealed class ProbeSkillUserBehaviour : MonoBehaviour, ISkillUser
{
    public Transform CastOrigin => transform;
    public Transform AimTransform => transform;
    public Vector3 AimDirection => transform.forward;
    public float currentEnagy => 100f;
    public StatsHub StatsHub => null;

    public void SpendEnagy(float amount)
    {
    }
}
#endif
