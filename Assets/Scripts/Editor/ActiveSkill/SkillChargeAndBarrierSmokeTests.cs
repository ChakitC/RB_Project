#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the cast transaction, the charge pool, the summon snapshot, and barrier blocking.
/// These are pure-logic checks; placement, physics, and prefab wiring stay Play Mode work.
/// </summary>
public sealed class SkillChargeAndBarrierSmokeTests
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

    // ---- Charge pool --------------------------------------------------------------------------

    [Test]
    public void SingleChargePoolBehavesLikeAPlainCooldown()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);

        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
        Assert.That(charges.TryConsume(0f, 12f), Is.True);
        Assert.That(charges.AvailableCharges, Is.EqualTo(0));

        charges.Refresh(1, 11.9f);
        Assert.That(charges.HasCharge, Is.False);

        charges.Refresh(1, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
    }

    [Test]
    public void ChargesRechargeSequentiallyNotInParallel()
    {
        var charges = new SkillChargeState();
        charges.Refresh(2, 0f);

        Assert.That(charges.TryConsume(0f, 12f), Is.True);
        Assert.That(charges.TryConsume(0f, 12f), Is.True);
        Assert.That(charges.AvailableCharges, Is.EqualTo(0));

        charges.Refresh(2, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1), "First charge returns at 12s.");

        charges.Refresh(2, 23.9f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1), "Second charge must not return early.");

        charges.Refresh(2, 24f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(2), "Second charge returns at 24s.");
    }

    [Test]
    public void UnlockingAChargeMakesItAvailableImmediately()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);
        charges.TryConsume(0f, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(0));

        charges.Refresh(2, 1f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1), "The newly unlocked charge is usable at once.");
        Assert.That(charges.MaxCharges, Is.EqualTo(2));
    }

    [Test]
    public void LosingAChargeClampsWithoutRestartingTheRunningRecharge()
    {
        var charges = new SkillChargeState();
        charges.Refresh(2, 0f);
        charges.TryConsume(0f, 12f);
        charges.TryConsume(0f, 12f);

        // Max drops back to 1 while two segments are queued: keep the oldest, drop the newest.
        charges.Refresh(1, 1f);
        Assert.That(charges.MaxCharges, Is.EqualTo(1));
        Assert.That(charges.RechargingCount, Is.EqualTo(1));

        charges.Refresh(1, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1),
            "The surviving segment keeps its original 12s deadline.");
    }

    [Test]
    public void ZeroCooldownNeverSpendsACharge()
    {
        var charges = new SkillChargeState();
        charges.Refresh(1, 0f);

        Assert.That(charges.TryConsume(0f, 0f), Is.True);
        Assert.That(charges.AvailableCharges, Is.EqualTo(1));
    }

    [Test]
    public void MaxChargesFlowsFromDefinitionAndUpgradeModifier()
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.charges";
        def.baseMaxCharges = 1;
        def.payload = Track(ScriptableObject.CreateInstance<SucceedingProbePayload>());

        var instance = new SkillInstance { def = def };
        Assert.That(instance.GetFinalStats(null).maxCharges, Is.EqualTo(1));

        var snapshot = new SkillUpgradeStatSnapshot();
        snapshot.AddNode(new SkillUpgradeNodeData
        {
            nodeId = "part_a",
            statModifiers = new List<StatModifier>
            {
                new StatModifier { stat = StatType.MaxCharges, add = 1f, mul = 1f },
            },
        });

        instance.upgradeSnapshot = snapshot;
        Assert.That(instance.GetFinalStats(null).maxCharges, Is.EqualTo(2));
    }

    [Test]
    public void TwoSlotsOnTheSameSkillShareOnePool()
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.shared";
        def.baseManaCost = 0f;
        def.baseCooldown = 12f;
        def.baseMaxCharges = 1;
        def.payload = Track(ScriptableObject.CreateInstance<SucceedingProbePayload>());

        var shared = new SkillChargeState();
        var slotA = new SkillInstance { def = def };
        var slotB = new SkillInstance { def = def };
        slotA.BindCharges(shared);
        slotB.BindCharges(shared);

        Assert.That(slotA.HasBoundCharges, Is.True);
        Assert.That(slotB.HasBoundCharges, Is.True);

        var user = new FakeSkillUser(100f);
        Assert.That(slotB.CanCast(user), Is.True);

        Assert.That(slotA.Cast(user, null, 0, out _), Is.True);

        Assert.That(slotB.CanCast(user), Is.False,
            "Spending the charge from slot A must make slot B unavailable too.");
        Assert.That(slotB.TryGetChargeStatus(user, out SkillChargeStatus statusB), Is.True);
        Assert.That(statusB.Available, Is.EqualTo(0));
        Assert.That(shared.AvailableCharges, Is.EqualTo(0),
            "Exactly one charge must be spent — no double deduction.");
    }

    [Test]
    public void ChargeStatusReadsFullBeforeTheSkillHasEverBeenCast()
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.neverCast";
        def.baseCooldown = 12f;
        def.baseMaxCharges = 2;
        def.payload = Track(ScriptableObject.CreateInstance<SucceedingProbePayload>());

        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.TryGetChargeStatus(new FakeSkillUser(100f), out SkillChargeStatus status), Is.True);
        Assert.That(status.Available, Is.EqualTo(2));
        Assert.That(status.Max, Is.EqualTo(2), "A never-cast skill reports a full pool, not 'unknown'.");
    }

    [Test]
    public void ResetToFullRefillsAPoolAsOnLoadWould()
    {
        var charges = new SkillChargeState();
        charges.Refresh(2, 0f);
        charges.TryConsume(0f, 12f);
        charges.TryConsume(0f, 12f);
        Assert.That(charges.AvailableCharges, Is.EqualTo(0));

        charges.ResetToFull();
        charges.Refresh(2, 1f);

        Assert.That(charges.AvailableCharges, Is.EqualTo(2));
        Assert.That(charges.RechargingCount, Is.EqualTo(0), "Reset must clear pending recharges.");
    }

    // ---- Cast transaction ---------------------------------------------------------------------

    [Test]
    public void FailedExecutionCostsNoEnergyAndNoCharge()
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.failing";
        def.baseManaCost = 25f;
        def.baseCooldown = 12f;
        def.baseMaxCharges = 1;
        def.payload = Track(ScriptableObject.CreateInstance<FailingProbePayload>());

        var instance = new SkillInstance { def = def };
        var user = new FakeSkillUser(100f);

        Assert.That(instance.Cast(user, null, 0, out SkillExecutionResult result), Is.False);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.PlacementBlocked));
        Assert.That(result.PublicMessage, Is.EqualTo(SkillExecutionResult.PlacementFailureMessage));
        Assert.That(FailingProbePayload.ExecutionCount, Is.EqualTo(1));

        Assert.That(user.currentEnagy, Is.EqualTo(100f), "A refused cast must not spend energy.");
        Assert.That(instance.CanCast(user), Is.True, "A refused cast must not spend a charge.");
    }

    [Test]
    public void SuccessfulExecutionCommitsEnergyAndCharge()
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.succeeding";
        def.baseManaCost = 25f;
        def.baseCooldown = 12f;
        def.baseMaxCharges = 1;
        def.payload = Track(ScriptableObject.CreateInstance<SucceedingProbePayload>());

        var instance = new SkillInstance { def = def };
        var user = new FakeSkillUser(100f);

        Assert.That(instance.Cast(user, null, 0, out SkillExecutionResult result), Is.True);
        Assert.That(result.Success, Is.True);
        Assert.That(user.currentEnagy, Is.EqualTo(75f));
        Assert.That(instance.CanCast(user), Is.False, "The charge is now recharging.");
    }

    [Test]
    public void BlockedMinionPreCastCostsNothingWhileOtherSkillsStillPay()
    {
        SkillGemDefinition summonSkill = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        summonSkill.skillId = "test.blocked.minion";
        summonSkill.tags = SkillTag.Minion | SkillTag.Ranged;

        SkillGemDefinition normalSkill = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        normalSkill.skillId = "test.blocked.normal";
        normalSkill.tags = SkillTag.Spell;

        Assert.That(InvokeIsSummonSkill(summonSkill), Is.True,
            "A Minion-tagged skill is treated as a summon for the blocked pre-cast exemption.");
        Assert.That(InvokeIsSummonSkill(normalSkill), Is.False,
            "Non-Minion skills keep the existing blocked pre-cast cooldown rule.");
        Assert.That(InvokeIsSummonSkill(null), Is.False);
    }

    static bool InvokeIsSummonSkill(SkillGemDefinition skillDef)
    {
        System.Reflection.MethodInfo method = typeof(SkillCastOrchestrator).GetMethod(
            "IsSummonSkill",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.That(method, Is.Not.Null, "SkillCastOrchestrator.IsSummonSkill is missing.");
        return (bool)method.Invoke(null, new object[] { skillDef });
    }

    [Test]
    public void CompositeSucceedsWhenAnyEnabledStepSucceeds()
    {
        CompositeSkillPayloadDef composite = Track(ScriptableObject.CreateInstance<CompositeSkillPayloadDef>());

        var failingStep = new PayloadStep();
        failingStep.SetPayload(Track(ScriptableObject.CreateInstance<FailingProbePayload>()));
        var succeedingStep = new PayloadStep();
        succeedingStep.SetPayload(Track(ScriptableObject.CreateInstance<SucceedingProbePayload>()));

        composite.AddStep(failingStep);
        composite.AddStep(succeedingStep);

        SkillCastContext context = CreateContext(composite);
        Assert.That(composite.ExecuteWithResult(context).Success, Is.True);
        Assert.That(FailingProbePayload.ExecutionCount, Is.EqualTo(1),
            "A failing step must not stop the steps after it.");
        Assert.That(SucceedingProbePayload.ExecutionCount, Is.EqualTo(1));
    }

    [Test]
    public void CompositeFailsWhenEveryEnabledStepFails()
    {
        CompositeSkillPayloadDef composite = Track(ScriptableObject.CreateInstance<CompositeSkillPayloadDef>());
        var step = new PayloadStep();
        step.SetPayload(Track(ScriptableObject.CreateInstance<FailingProbePayload>()));
        composite.AddStep(step);

        SkillExecutionResult result = composite.ExecuteWithResult(CreateContext(composite));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.PlacementBlocked));
    }

    [Test]
    public void CompositeWithNoEnabledStepReportsNoEffect()
    {
        CompositeSkillPayloadDef composite = Track(ScriptableObject.CreateInstance<CompositeSkillPayloadDef>());
        var gatedStep = new PayloadStep { RequiredUpgradeId = "never.granted" };
        gatedStep.SetPayload(Track(ScriptableObject.CreateInstance<SucceedingProbePayload>()));
        composite.AddStep(gatedStep);

        SkillExecutionResult result = composite.ExecuteWithResult(CreateContext(composite));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.NoEffect));
        Assert.That(SucceedingProbePayload.ExecutionCount, Is.EqualTo(0));
    }

    // ---- Summon snapshot ----------------------------------------------------------------------

    [Test]
    public void SummonCarriesMaxHealthAndUpgradeIdsFromTheCast()
    {
        GameObject ownerObject = Track(new GameObject("Owner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();

        GameObject summonObject = Track(new GameObject("Summon"));
        summonObject.AddComponent<SummonContext>();
        SummonedEntityRuntime runtime = summonObject.AddComponent<SummonedEntityRuntime>();

        runtime.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon.snapshot",
            Mobility = SummonMobility.Stationary,
            Lifetime = 10f,
            DespawnDelay = 0.25f,
            MaxHealth = 617.5f,
            UpgradeIds = new List<string> { "feno.skill.minigunterret.part_b.armor_piercing_rounds" },
        }, null);

        Assert.That(runtime.MaxHealth, Is.EqualTo(617.5f));
        Assert.That(runtime.HasUpgrade("feno.skill.minigunterret.part_b.armor_piercing_rounds"), Is.True);
        Assert.That(runtime.HasUpgrade("feno.skill.minigunterret.part_c.barrier"), Is.False);
        Assert.That(runtime.Owner, Is.SameAs(owner));

        var modifiers = new List<RuntimeStatModifier>();
        runtime.AppendStatModifiers(modifiers);
        Assert.That(modifiers.Exists(m => m.StatType == StatType.MaxHP && Mathf.Approximately(m.Value, 617.5f)), Is.True,
            "The snapshotted max HP must reach the summon's StatsHub as a modifier.");
    }

    [Test]
    public void ExecutionStateCarriesSpawnedSummonsToLaterSteps()
    {
        GameObject ownerObject = Track(new GameObject("Owner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();

        GameObject summonObject = Track(new GameObject("Summon"));
        summonObject.AddComponent<SummonContext>();
        SummonedEntityRuntime runtime = summonObject.AddComponent<SummonedEntityRuntime>();
        runtime.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon.state",
            Mobility = SummonMobility.Stationary,
            Lifetime = 10f,
        }, null);

        var state = new SkillCastExecutionState();
        Assert.That(state.HasSpawnedSummons, Is.False);

        state.RegisterSpawnedSummon(runtime);
        state.RegisterSpawnedSummon(runtime);

        Assert.That(state.SpawnedSummons.Count, Is.EqualTo(1), "Registration is idempotent.");
        Assert.That(state.SpawnedSummons[0], Is.SameAs(runtime));
    }

    // ---- Barrier ------------------------------------------------------------------------------

    [Test]
    public void BarrierBlocksHostilesAndIgnoresFriendlies()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();
        BarrierRuntime barrier = CreateBarrier(owner, BarrierAnchorMode.CastPosition, anchorSummon: null);

        GameObject enemyObject = Track(new GameObject("Enemy"));
        enemyObject.AddComponent<EnemyContext>();

        Assert.That(barrier.BlocksProjectileFrom(enemyObject), Is.True);
        Assert.That(barrier.BlocksProjectileFrom(ownerObject), Is.False, "Friendly fire passes through.");
        Assert.That(barrier.BlocksProjectileFrom(Track(new GameObject("Unknown"))), Is.False,
            "A shooter with no CharacteContext passes through rather than being blocked.");
    }

    [Test]
    public void UnknownFactionsAreNeverHostile()
    {
        GameObject playerObject = Track(new GameObject("Player"));
        PlayerContext player = playerObject.AddComponent<PlayerContext>();

        GameObject enemyObject = Track(new GameObject("Enemy"));
        EnemyContext enemy = enemyObject.AddComponent<EnemyContext>();

        Assert.That(BarrierFactionUtility.AreHostile(player, enemy), Is.True);
        Assert.That(BarrierFactionUtility.AreHostile(player, player), Is.False);
        Assert.That(BarrierFactionUtility.AreHostile(player, null), Is.False);
        Assert.That(BarrierFactionUtility.AreHostile(null, enemy), Is.False);

        // Auto / Generic / Neutral have no side, so they are neither hostile nor friendly.
        Assert.That(BarrierFactionUtility.AreHostile(player, CreateIdentityContext(AITargetIdentity.Auto)), Is.False);
        Assert.That(BarrierFactionUtility.AreHostile(player, CreateIdentityContext(AITargetIdentity.Generic)), Is.False);
        Assert.That(BarrierFactionUtility.AreHostile(player, CreateIdentityContext(AITargetIdentity.Neutral)), Is.False);
        Assert.That(BarrierFactionUtility.AreFriendly(player, CreateIdentityContext(AITargetIdentity.Generic)), Is.False);
    }

    [Test]
    public void CasterBarrierEndsWhenTheCasterDies()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();
        BarrierRuntime barrier = CreateBarrier(owner, BarrierAnchorMode.Caster, anchorSummon: null);

        BarrierEndReason? reason = null;
        barrier.Ended += data => reason = data.Reason;

        // No HealthSystem resolves, so the caster cannot be judged dead and the barrier survives.
        barrier.TickLifetime(0f);
        Assert.That(barrier.IsBarrierActive, Is.True);

        // Destroying the owner makes the caster unresolvable, which ends the barrier.
        Object.DestroyImmediate(ownerObject);
        barrier.TickLifetime(0f);

        Assert.That(reason, Is.EqualTo(BarrierEndReason.AnchorLost));
    }

    [Test]
    public void SummonBarrierEndsWhenItsSummonStops()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();

        GameObject summonObject = Track(new GameObject("Summon"));
        summonObject.AddComponent<SummonContext>();
        SummonedEntityRuntime summon = summonObject.AddComponent<SummonedEntityRuntime>();
        summon.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon.barrier",
            Mobility = SummonMobility.Stationary,
            Lifetime = 10f,
            DespawnDelay = 1f,
        }, null);

        BarrierRuntime barrier = CreateBarrier(owner, BarrierAnchorMode.SpawnedEntitiesFromCurrentCast, summon);

        BarrierEndReason? reason = null;
        barrier.Ended += data => reason = data.Reason;

        barrier.TickLifetime(0f);
        Assert.That(barrier.IsBarrierActive, Is.True);

        summon.BeginDespawn(SummonDespawnReason.Killed);
        barrier.TickLifetime(0f);

        Assert.That(reason, Is.EqualTo(BarrierEndReason.AnchorLost));
    }

    [Test]
    public void CastPositionBarrierSurvivesWithoutAnyAnchor()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();
        BarrierRuntime barrier = CreateBarrier(owner, BarrierAnchorMode.CastPosition, anchorSummon: null);

        BarrierEndReason? reason = null;
        barrier.Ended += data => reason = data.Reason;

        for (int i = 0; i < 5; i++)
            barrier.TickLifetime(0f);

        Assert.That(barrier.IsBarrierActive, Is.True);
        Assert.That(reason, Is.Null, "A cast-position barrier has no anchor to lose.");
    }

    [Test]
    public void BarrierAbsorbsUntilItBreaksAndDoesNotRegenerate()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();

        GameObject barrierObject = Track(new GameObject("Barrier", typeof(SphereCollider)));
        BarrierRuntime barrier = barrierObject.AddComponent<BarrierRuntime>();
        barrier.Initialize(new BarrierSpawnRequest
        {
            Owner = owner,
            FallbackPosition = Vector3.zero,
            Radius = 3f,
            Lifetime = 10f,
            MaxHealth = 100f,
        });

        BarrierEndReason? endReason = null;
        barrier.Ended += data => endReason = data.Reason;

        barrier.AbsorbProjectile(40f, Vector3.zero, Vector3.up);
        Assert.That(barrier.CurrentHealth, Is.EqualTo(60f));
        Assert.That(barrier.IsBarrierActive, Is.True);
        Assert.That(endReason, Is.Null);

        // Overkill: the breaking shot is fully consumed, no overflow escapes.
        barrier.AbsorbProjectile(500f, Vector3.zero, Vector3.up);
        Assert.That(barrier.CurrentHealth, Is.EqualTo(0f));
        Assert.That(barrier.IsBarrierActive, Is.False);
        Assert.That(endReason, Is.EqualTo(BarrierEndReason.Broken));
    }

    [Test]
    public void BarrierPayloadRejectsMissingPrefab()
    {
        BarrierSkillPayloadDef payload = Track(ScriptableObject.CreateInstance<BarrierSkillPayloadDef>());
        var issues = new List<string>();
        payload.CollectValidationIssues(issues);

        Assert.That(issues.Exists(issue => issue.Contains("barrier prefab")), Is.True);
        Assert.That(issues.Exists(issue => issue.Contains("zero HP")), Is.False,
            "The default 0.75 anchor share already resolves to non-zero HP.");
    }

    [Test]
    public void BarrierPayloadRejectsZeroHealthAuthoring()
    {
        BarrierSkillPayloadDef payload = Track(ScriptableObject.CreateInstance<BarrierSkillPayloadDef>());
        var serialized = new UnityEditor.SerializedObject(payload);
        serialized.FindProperty("baseHealth").floatValue = 0f;
        serialized.FindProperty("anchorMaxHealthShare").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var issues = new List<string>();
        payload.CollectValidationIssues(issues);

        Assert.That(issues.Exists(issue => issue.Contains("zero HP")), Is.True,
            "A payload with no base health and no anchor share must be reported.");
    }

    [Test]
    public void BarrierVfxPresenterDetachesAndCleansUpOnEnd()
    {
        GameObject ownerObject = Track(new GameObject("BarrierOwner"));
        PlayerContext owner = ownerObject.AddComponent<PlayerContext>();

        GameObject barrierObject = Track(new GameObject("Barrier", typeof(SphereCollider)));
        BarrierRuntime barrier = barrierObject.AddComponent<BarrierRuntime>();

        var presentation = new GameObject("Presentation");
        presentation.transform.SetParent(barrierObject.transform, false);
        BarrierVfxPresenter presenter = presentation.AddComponent<BarrierVfxPresenter>();

        // Edit Mode never runs Awake, so wire the presenter the way Awake would at runtime.
        presenter.Bind(barrier);

        barrier.Initialize(new BarrierSpawnRequest
        {
            Owner = owner,
            AnchorMode = BarrierAnchorMode.CastPosition,
            FallbackPosition = Vector3.zero,
            Radius = 3f,
            Lifetime = 10f,
            MaxHealth = 100f,
        });

        Assert.That(presenter.transform.parent, Is.SameAs(barrierObject.transform));

        barrier.AbsorbProjectile(500f, Vector3.zero, Vector3.up);

        // The presentation must survive the runtime root so the break can play out.
        Assert.That(presenter == null, Is.False, "Presentation must not die with the runtime root.");
        Assert.That(presenter.transform.parent, Is.Null, "Presentation must detach before cleanup.");

        // It owns its own teardown, so nothing is orphaned.
        Object.DestroyImmediate(presentation);
    }

    // ---- Helpers ------------------------------------------------------------------------------

    BarrierRuntime CreateBarrier(
        CharacteContext owner,
        BarrierAnchorMode anchorMode,
        SummonedEntityRuntime anchorSummon)
    {
        GameObject barrierObject = Track(new GameObject("Barrier", typeof(SphereCollider)));
        BarrierRuntime barrier = barrierObject.AddComponent<BarrierRuntime>();

        Assert.That(barrier.Initialize(new BarrierSpawnRequest
        {
            Owner = owner,
            AnchorMode = anchorMode,
            Anchor = anchorMode == BarrierAnchorMode.Caster
                ? owner.transform
                : anchorSummon != null ? anchorSummon.transform : null,
            AnchorSummon = anchorSummon,
            FallbackPosition = Vector3.zero,
            Radius = 3f,
            Lifetime = 10f,
            MaxHealth = 100f,
        }), Is.True);

        return barrier;
    }

    CharacteContext CreateIdentityContext(AITargetIdentity identity)
    {
        GameObject go = Track(new GameObject($"Identity_{identity}"));
        IdentityProbeContext context = go.AddComponent<IdentityProbeContext>();
        context.SetIdentity(identity);
        return context;
    }

    static SkillCastContext CreateContext(SkillPayloadDef payload)
    {
        SkillGemDefinition def = ScriptableObject.CreateInstance<SkillGemDefinition>();
        def.skillId = "test.context";
        def.payload = payload;

        var context = new SkillCastContext(null, def, new FinalSkillStats { maxCharges = 1 });
        Object.DestroyImmediate(def);
        return context;
    }

    T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    sealed class FakeSkillUser : ISkillUser
    {
        public FakeSkillUser(float energy) => Energy = energy;

        public float Energy { get; private set; }
        public Transform CastOrigin => null;
        public Transform AimTransform => null;
        public Vector3 AimDirection => Vector3.forward;
        public float currentEnagy => Energy;
        public StatsHub StatsHub => null;

        public void SpendEnagy(float amount) => Energy -= amount;
    }
}

public sealed class FailingProbePayload : SkillPayloadDef
{
    public static int ExecutionCount;

    public override void Execute(SkillCastContext context) => ExecuteWithResult(context);

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        ExecutionCount++;
        return SkillExecutionResult.Failed(
            SkillExecutionFailureReason.PlacementBlocked,
            "Probe payload always refuses.");
    }
}

public sealed class SucceedingProbePayload : SkillPayloadDef
{
    public static int ExecutionCount;

    public override void Execute(SkillCastContext context)
    {
        ExecutionCount++;
    }
}

/// <summary>Test-only context whose TargetIdentity can be forced to any value.</summary>
public sealed class IdentityProbeContext : CharacteContext
{
    AITargetIdentity identity = AITargetIdentity.Generic;

    public override AITargetIdentity TargetIdentity => identity;

    public void SetIdentity(AITargetIdentity value) => identity = value;
}
#endif
