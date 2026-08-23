#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the targeted-delivery contract: how a cast carries the character it was aimed at, what
/// happens to that lock when the target stops existing, and which cost policy a metered assist
/// runs under.
///
/// Pure-logic checks. The flight itself, the DeliveryRelease marker, and the arrival effects need
/// a live animation driver and stay Play Mode work; what is pinned down here is every decision
/// those paths depend on.
/// </summary>
public sealed class TargetedDeliverySmokeTests
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
    }

    // ---- Target handle ---------------------------------------------------------------------------

    [Test]
    public void ACastWithNoTargetIsDistinctFromACastWhoseTargetDied()
    {
        Assert.That(SkillTargetHandle.None.WasAssigned, Is.False,
            "No target at all is broken wiring and must be able to refund the cast.");

        IdentityProbeContext target = CreateCharacter("victim", maxHealth: 100f, currentHealth: 100f);
        var handle = new SkillTargetHandle(target);

        Assert.That(handle.WasAssigned, Is.True);
        Assert.That(handle.TryResolveLiveContext(out _), Is.True);

        Object.DestroyImmediate(target.gameObject);

        Assert.That(handle.WasAssigned, Is.True,
            "A target that was locked and then destroyed must NOT read as 'no target' - it still costs.");
        Assert.That(handle.TryResolveLiveContext(out CharacteContext resolved), Is.False);
        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void TheDeliveryPointSurvivesTheTargetBeingDestroyed()
    {
        IdentityProbeContext target = CreateCharacter("mover", maxHealth: 100f, currentHealth: 100f);
        target.transform.position = new Vector3(5f, 0f, 7f);

        var handle = new SkillTargetHandle(target);
        Vector3 whileAlive = handle.ResolveDeliveryPoint();

        Assert.That(whileAlive.y, Is.GreaterThan(0f), "The delivery point sits above the character, not at their feet.");

        Object.DestroyImmediate(target.gameObject);
        Vector3 afterDeath = handle.ResolveDeliveryPoint();

        Assert.That(afterDeath, Is.EqualTo(whileAlive),
            "A delivery whose target vanished still has somewhere to land instead of snapping to the origin.");
    }

    [Test]
    public void TheDeliveryPointTracksALivingTarget()
    {
        IdentityProbeContext target = CreateCharacter("walker", maxHealth: 100f, currentHealth: 100f);
        var handle = new SkillTargetHandle(target);

        Vector3 before = handle.ResolveDeliveryPoint();
        target.transform.position += new Vector3(10f, 0f, 0f);
        Vector3 after = handle.ResolveDeliveryPoint();

        Assert.That(after.x - before.x, Is.EqualTo(10f).Within(0.001f),
            "The delivery must follow a target that is still moving.");
    }

    [Test]
    public void ADeadOrDownedTargetStillResolvesButReceivesNothing()
    {
        IdentityProbeContext target = CreateCharacter("downed", maxHealth: 100f, currentHealth: 0f);
        var handle = new SkillTargetHandle(target);

        Assert.That(handle.TryResolveLiveContext(out _), Is.True,
            "The reference still exists, so the delivery still has somewhere to fly.");
        Assert.That(handle.TryResolveEffectTarget(out _), Is.False,
            "But nothing lands on a target who is not alive.");
    }

    [Test]
    public void ArrivalClearanceIsChosenByTheCallerNotTheLock()
    {
        IdentityProbeContext target = CreateCharacter("clearance", maxHealth: 100f, currentHealth: 100f);
        var handle = new SkillTargetHandle(target);

        float bare = handle.ResolveDeliveryPoint(0f).y;
        float lifted = handle.ResolveDeliveryPoint(1.25f).y;

        Assert.That(lifted - bare, Is.EqualTo(1.25f).Within(0.001f));
    }

    // ---- Cost policy -----------------------------------------------------------------------------

    [Test]
    public void TheLegacyBooleanKeepsItsExactOldMeaning()
    {
        Assert.That(SkillCastCostPolicies.FromLegacyFlag(false), Is.EqualTo(SkillCastCostPolicy.Normal));
        Assert.That(SkillCastCostPolicies.FromLegacyFlag(true), Is.EqualTo(SkillCastCostPolicy.IgnoreEnergyAndCharge));

        Assert.That(SkillCastCostPolicy.IgnoreEnergyAndCharge.IgnoresCharge(), Is.True,
            "Guaranteed interruptions must never be refused for an empty pool.");
        Assert.That(SkillCastCostPolicy.IgnoreEnergyRespectCharge.IgnoresCharge(), Is.False,
            "A metered assist stays on its own cooldown.");
        Assert.That(SkillCastCostPolicy.Normal.IgnoresEnergy(), Is.False);
    }

    [Test]
    public void AnAssistPaysNoEnergyButStillNeedsACharge()
    {
        SkillUserSystem user = CreateEnergyUser(0f);
        SkillGemDefinition def = CreateSkill("test.assist", manaCost: 40f, cooldown: 30f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(
            instance.TryReserveCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, true, out SkillCastReservation first),
            Is.True,
            "An empty energy bar must not block an assist that costs no energy.");

        first.Commit();
        Assert.That(user.CurrentEnergy, Is.EqualTo(0f), "And committing must not take energy it never reserved.");

        Assert.That(
            instance.TryReserveCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, true, out _),
            Is.False,
            "The second attempt inside the cooldown must be refused - this is the whole point of the policy.");
    }

    [Test]
    public void AnInterruptionStyleCastStillIgnoresAnEmptyPool()
    {
        SkillUserSystem user = CreateEnergyUser(0f);
        SkillGemDefinition def = CreateSkill("test.interrupt", manaCost: 40f, cooldown: 30f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(instance.TryReserveCast(user, true, true, out SkillCastReservation first), Is.True);
        first.Commit();

        Assert.That(instance.TryReserveCast(user, true, true, out SkillCastReservation second), Is.True,
            "Legacy ignoreResourceCosts callers must keep casting through an empty pool.");
        second.Release();
    }

    [Test]
    public void FailingBeforeCommitRefundsTheChargeAndCommittingKeepsIt()
    {
        SkillUserSystem user = CreateEnergyUser(0f);
        SkillGemDefinition def = CreateSkill("test.settle", manaCost: 0f, cooldown: 30f);
        var instance = new SkillInstance { def = def };
        instance.BindCharges(new SkillChargeState());

        Assert.That(
            instance.TryReserveCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, true, out SkillCastReservation aborted),
            Is.True);
        aborted.Release();

        Assert.That(instance.CanCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, out _), Is.True,
            "A cast that never reached its cast point costs nothing.");

        Assert.That(
            instance.TryReserveCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, true, out SkillCastReservation committed),
            Is.True);
        committed.Commit();

        Assert.That(instance.CanCast(user, SkillCastCostPolicy.IgnoreEnergyRespectCharge, out _), Is.False,
            "Once committed, losing the target later must not hand the cooldown back.");
    }

    [Test]
    public void ACastRequestCarriesItsPolicyAndItsLock()
    {
        IdentityProbeContext target = CreateCharacter("request_target", maxHealth: 100f, currentHealth: 50f);
        var handle = new SkillTargetHandle(target);

        var legacy = new SkillCastRequest(null, null, ignoreResourceCosts: true);
        Assert.That(legacy.CostPolicy, Is.EqualTo(SkillCastCostPolicy.IgnoreEnergyAndCharge));
        Assert.That(legacy.IgnoreResourceCosts, Is.True);
        Assert.That(legacy.PrimaryTarget.WasAssigned, Is.False,
            "A request with no target must never read as targeted.");

        var targeted = new SkillCastRequest(
            null, null, primaryTarget: handle, costPolicy: SkillCastCostPolicy.IgnoreEnergyRespectCharge);

        Assert.That(targeted.CostPolicy, Is.EqualTo(SkillCastCostPolicy.IgnoreEnergyRespectCharge));
        Assert.That(targeted.IgnoreResourceCosts, Is.False,
            "The legacy read must not report a metered assist as free of everything.");
        Assert.That(targeted.PrimaryTarget, Is.SameAs(handle), "The lock is passed through, never copied or re-resolved.");
    }

    // ---- Payload ---------------------------------------------------------------------------------

    [Test]
    public void ADeliveryWithNoTargetFailsTheCastInsteadOfBurningACooldown()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();
        SkillCastContext context = CreateCastContext(payload, primaryTarget: null);

        SkillExecutionResult result = payload.ExecuteWithResult(context);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Reason, Is.EqualTo(SkillExecutionFailureReason.MissingRuntimeContext),
            "No target is a wiring failure, so the transaction must roll back.");
    }

    [Test]
    public void ADeliveryDeclaresTheReleaseMarkerItCannotWorkWithout()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();

        Assert.That(payload.RequiresSkillTimelineEvents, Is.True);

        var events = new List<CombatTimelineEventName>();
        payload.CollectTimelineEventNames(events);

        Assert.That(events, Contains.Item(CombatTimelineEventName.DeliveryRelease));
        Assert.That(CombatTimelineEventNames.ToAnimancerEventName(CombatTimelineEventName.DeliveryRelease),
            Is.EqualTo("DeliveryRelease"),
            "The marker name must match what the clip raises.");
    }

    [Test]
    public void APhysicalDeliveryPrefabIsAnAuthoringError()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();

        var physical = Track(new GameObject("physical_can"));
        physical.AddComponent<Rigidbody>();
        physical.AddComponent<SphereCollider>();
        SetPrivate(payload, "deliveryPrefab", physical);

        var issues = new List<string>();
        payload.CollectValidationIssues(issues);

        Assert.That(issues.Exists(i => i.Contains("Rigidbody")), Is.True,
            "A delivery moved by the runtime must not also be moved by physics.");
        Assert.That(issues.Exists(i => i.Contains("Collider")), Is.True,
            "A delivery must not collide with anything on the way to its target.");
    }

    [Test]
    public void ImpossibleFlightSettingsAreAuthoringErrors()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();
        SetPrivate(payload, "speed", 0f);
        SetPrivate(payload, "minFlightDuration", 2f);
        SetPrivate(payload, "maxFlightDuration", 1f);

        var issues = new List<string>();
        payload.CollectValidationIssues(issues);

        Assert.That(issues.Exists(i => i.Contains("speed")), Is.True);
        Assert.That(issues.Exists(i => i.Contains("min flight duration is greater")), Is.True);
    }

    [Test]
    public void APercentHealScalesWithTheTargetsOwnMaxHealth()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();
        SetPrivate(payload, "healMode", DeliveryHealMode.PercentMaxHealth);
        SetPrivate(payload, "healMaxHealthFraction", 0.25f);

        IdentityProbeContext small = CreateCharacter("small", maxHealth: 200f, currentHealth: 10f);
        IdentityProbeContext large = CreateCharacter("large", maxHealth: 1000f, currentHealth: 10f);

        Assert.That(payload.ResolveHealAmount(small.HealthSystem, null), Is.EqualTo(50f).Within(0.001f));
        Assert.That(payload.ResolveHealAmount(large.HealthSystem, null), Is.EqualTo(250f).Within(0.001f),
            "A percent heal must read the recipient's max HP, not the caster's.");
    }

    [Test]
    public void AFlatHealOfZeroFallsBackToTheSkillsHealPower()
    {
        TargetedDeliverySkillPayloadDef payload = CreateDeliveryPayload();
        SetPrivate(payload, "healMode", DeliveryHealMode.Flat);

        IdentityProbeContext target = CreateCharacter("flat", maxHealth: 100f, currentHealth: 10f);
        var stats = new FinalSkillStats { healPower = 33f };

        Assert.That(payload.ResolveHealAmount(target.HealthSystem, stats), Is.EqualTo(33f).Within(0.001f));
    }

    // ---- Helper proc eligibility -----------------------------------------------------------------

    [Test]
    public void TheHelperItselfIsNeverEligibleToReceiveItsOwnAssist()
    {
        SkillHelperDef def = Track(ScriptableObject.CreateInstance<SkillHelperDef>());
        def.triggerMode = SkillHelperTriggerMode.PartyHealthThreshold;

        Assert.That(def.IsPartyHealthTrigger, Is.True);
        Assert.That(def.IsRoleEligible(ChainActorRole.Helper), Is.False,
            "The helper is the one delivering the assist, so it cannot also be the recipient.");
        Assert.That(def.IsRoleEligible(ChainActorRole.None), Is.False);
        Assert.That(def.IsRoleEligible(ChainActorRole.Player), Is.True);
        Assert.That(def.IsRoleEligible(ChainActorRole.PartySlot1), Is.True);
        Assert.That(def.IsRoleEligible(ChainActorRole.PartySlot2), Is.True);
    }

    [Test]
    public void AnEmptyEligibleRoleListFallsBackToThePartyRatherThanBlockingEverything()
    {
        SkillHelperDef def = Track(ScriptableObject.CreateInstance<SkillHelperDef>());
        def.eligibleRoles = new ChainActorRole[0];

        Assert.That(def.IsRoleEligible(ChainActorRole.Player), Is.True,
            "An unconfigured list must not silently disable the trigger.");
        Assert.That(def.IsRoleEligible(ChainActorRole.Helper), Is.False);
    }

    // ---- Height resolution ------------------------------------------------------------------------

    [Test]
    public void TheOverheadPointIgnoresTriggerVolumes()
    {
        IdentityProbeContext target = CreateCharacter("sensor_owner", maxHealth: 100f, currentHealth: 100f);

        // Detection ranges and hitboxes are triggers in this project and routinely dwarf the
        // character. Measuring one would put the delivery point metres above the actual head.
        var sensor = Track(new GameObject("detection_range"));
        sensor.transform.SetParent(target.transform, false);
        SphereCollider trigger = sensor.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 25f;

        float top = CharacterTargetHeightUtility.ResolveOverheadPoint(target).y;

        Assert.That(top, Is.LessThan(5f),
            "A 25m detection sphere must not be mistaken for the character's height.");
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    SkillGemDefinition CreateSkill(string id, float manaCost, float cooldown)
    {
        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = id;
        def.baseManaCost = manaCost;
        def.baseCooldown = cooldown;
        def.baseMaxCharges = 1;
        def.payload = CreateDeliveryPayload();
        return def;
    }

    TargetedDeliverySkillPayloadDef CreateDeliveryPayload()
    {
        TargetedDeliverySkillPayloadDef payload =
            Track(ScriptableObject.CreateInstance<TargetedDeliverySkillPayloadDef>());

        SetPrivate(payload, "deliveryPrefab", Track(new GameObject("probe_can")));
        return payload;
    }

    SkillCastContext CreateCastContext(SkillPayloadDef payload, SkillTargetHandle primaryTarget)
    {
        var host = Track(new GameObject("delivery_caster"));
        IdentityProbeContext context = host.AddComponent<IdentityProbeContext>();
        context.SetIdentity(AITargetIdentity.Companion);

        var user = host.AddComponent<ProbeSkillUserBehaviour>();

        SkillGemDefinition def = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        def.skillId = "test.delivery";
        def.payload = payload;

        return new SkillCastContext(user, def, new FinalSkillStats(), primaryTarget: primaryTarget);
    }

    IdentityProbeContext CreateCharacter(string name, float maxHealth, float currentHealth)
    {
        var host = Track(new GameObject(name));
        IdentityProbeContext context = host.AddComponent<IdentityProbeContext>();
        context.SetIdentity(AITargetIdentity.Companion);

        HealthSystem health = host.AddComponent<HealthSystem>();
        health.maximumHealth = maxHealth;
        health.currentHealth = currentHealth;
        context.HealthSystem = health;

        // A plain solid collider so the overhead point has real bounds to measure.
        CapsuleCollider body = host.AddComponent<CapsuleCollider>();
        body.height = 2f;
        body.center = new Vector3(0f, 1f, 0f);

        return context;
    }

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

    static void SetPrivate(Object target, string fieldName, object value)
    {
        var serialized = new UnityEditor.SerializedObject(target);
        UnityEditor.SerializedProperty property = serialized.FindProperty(fieldName);

        Assert.That(property, Is.Not.Null, $"Field '{fieldName}' is missing from {target.GetType().Name}.");

        switch (value)
        {
            case float f:
                property.floatValue = f;
                break;
            case int i:
                property.intValue = i;
                break;
            case System.Enum e:
                property.enumValueIndex = System.Convert.ToInt32(e);
                break;
            case Object o:
                property.objectReferenceValue = o;
                break;
            default:
                Assert.Fail($"Unsupported test value type for '{fieldName}'.");
                break;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }
}
#endif
