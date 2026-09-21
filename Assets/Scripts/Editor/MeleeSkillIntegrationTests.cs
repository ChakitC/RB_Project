#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MeleeSkillIntegrationTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> objects = new();

    // Runs the same assertions without the Test Runner's scene save/switch prompt.
    public static string RunSmokeChecks()
    {
        var results = new List<string>();
        foreach (var method in typeof(MeleeSkillIntegrationTests).GetMethods())
        {
            var cases = method.GetCustomAttributes<TestCaseAttribute>();
            foreach (var testCase in cases) Run(method, testCase.Arguments);
            if (method.GetCustomAttribute<TestAttribute>() != null) Run(method, Array.Empty<object>());
        }
        string report = string.Join("\n", results);
        System.IO.File.WriteAllText("../BuildArtifacts/melee-skill-smoke.txt", report);
        return report;

        void Run(MethodInfo method, object[] arguments)
        {
            var fixture = new MeleeSkillIntegrationTests();
            string name = method.Name + "(" + string.Join(",", arguments) + ")";
            try { method.Invoke(fixture, arguments); results.Add("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name + ": " + (ex.InnerException ?? ex)); }
            finally { fixture.Cleanup(); }
        }
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test]
    public void ComboSkillBlockBindsHitboxesAndInterruptClearsTheCombo()
    {
        var context = MakeRig(false);
        var combo = MakeCombo(2);
        var skill = combo.ComboSteps[0].executionSkill;
        var profile = new SkillDefensiveBlockSettings();
        profile.windowStartNormalized = 0f; profile.windowEndNormalized = .95f;
        skill.defensiveBlock = profile;
        context.baseStats.animProfile.lightMeleeSkill = combo;
        var block = context.gameObject.AddComponent<DefensiveBlockAttack>();
        Call(block, "Awake"); Call(block, "OnEnable"); context.ResolveReferences();
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        context.GetComponent<AnimancerComponent>().Evaluate(.1f);
        Assert.That(block.skill, Is.SameAs(skill));
        Assert.That(block.WindowOpen, Is.True);
        var runtime = context.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        Assert.That(runtime, Is.Not.Null);
        Assert.That(typeof(SkillHitboxSequenceRuntime).GetField("_defensiveBlock", Hidden).GetValue(runtime), Is.SameAs(block));
        context.MeleeController.PressMelee(MeleeType.Light);
        Call(block, "StopOwnedSkill");
        Assert.That(context.MeleeController.IsComboActive, Is.False);
        Assert.That(context.stateHub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Ready));
        Assert.That(block.WindowOpen, Is.False);
        // A new request without a profile must not inherit the previous window.
        skill.defensiveBlock = null;
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        context.GetComponent<AnimancerComponent>().Evaluate(.1f);
        Assert.That(block.WindowOpen, Is.False);
    }

    [Test]
    public void ContactMeleeInterceptsAtCloseRangeWithoutTimedApproach()
    {
        var caster = MakeRig(false); var combo = MakeCombo(1);
        var skill = combo.ComboSteps[0].executionSkill;
        skill.defensiveBlock = new SkillDefensiveBlockSettings { windowEndNormalized = .95f };
        var payload = (PrefabHitboxSkillPayloadDef)skill.payload;
        payload.HitboxLayout.Groups[0].Shapes[0].Size = Vector3.one * 2f;
        caster.transform.position = new Vector3(0, 0, 2f);
        caster.transform.rotation = Quaternion.LookRotation(Vector3.back);
        caster.baseStats.animProfile.lightMeleeSkill = combo;
        var attack = caster.gameObject.AddComponent<DefensiveBlockAttack>();
        Call(attack, "Awake"); Call(attack, "OnEnable"); caster.ResolveReferences();

        var receiver = MakeRig(false);
        var settings = Track(ScriptableObject.CreateInstance<DefensiveBlockActorProfile>());
        settings.animation = Track(ScriptableObject.CreateInstance<BlockAnimationProfile>());
        settings.animation.beginClip = MakeClip(); settings.animation.impactClip = MakeClip();
        settings.hitLagDuration = 0; settings.cameraEnabled = false;
        receiver.baseStats.defensiveBlock = settings;
        var guard = receiver.gameObject.AddComponent<DefensiveBlockController>(); Call(guard, "Awake");
        var player = Track(new GameObject("Protected Player")).AddComponent<PlayerContext>();
        player.HealthSystem = player.gameObject.AddComponent<HealthSystem>();
        player.HealthSystem.CTX = player;
        player.HealthSystem.maximumHealth = player.HealthSystem.currentHealth = 100;
        player.transform.position = Vector3.back * 2;

        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        caster.GetComponent<AnimancerComponent>().Evaluate(.1f);
        caster.GetComponent<AnimancerComponent>().Evaluate(.01f); // Dispatch the evaluated HitStart event.
        Assert.That(attack.CanApproachGuard(player, guard), Is.True);
        Assert.That(attack.IsTimedApproach, Is.False);
        // The same close attack must not bypass timed movement planning when opted in.
        skill.defensiveBlock.mode = DefensiveBlockMode.TimedApproach;
        Assert.That(attack.CanApproachGuard(player, guard), Is.False);
        skill.defensiveBlock.mode = DefensiveBlockMode.Contact;

        // Stage an accepted guard reservation, then exercise actual runtime interception.
        int request = attack.RequestId;
        Set(attack, "acceptedWindow", 0); Set(attack, "defender", guard); Set(attack, "protectedPlayer", player);
        Set(guard, "attack", attack); Set(guard, "player", player); Set(guard, "request", request);
        Set(guard, "life", receiver.LifeGeneration); Set(guard, "playerLife", player.LifeGeneration);
        Set(guard, "sessionDefinition", receiver.baseStats); Set(guard, "active", true); Set(guard, "arrived", true);
        Assert.That(receiver.AnimDriver.TryBeginBlock(request, settings.animation), Is.True);
        Assert.That(guard.IsReadyFor(attack, request), Is.True);
        Vector3 lunge = Vector3.back * 3f;
        Assert.That(attack.ConstrainContactRootMotion(caster.transform.position, lunge).z,
            Is.EqualTo(-.55f).Within(.001f), "Ready Contact guard must stop the root before it passes the guard.");
        Set(guard, "arrived", false);
        Assert.That(attack.ConstrainContactRootMotion(caster.transform.position, lunge), Is.EqualTo(lunge),
            "A pending warp cannot hold the attacker.");
        Set(guard, "arrived", true);
        skill.defensiveBlock.mode = DefensiveBlockMode.TimedApproach;
        Assert.That(attack.ConstrainContactRootMotion(caster.transform.position, lunge), Is.EqualTo(lunge));
        skill.defensiveBlock.mode = DefensiveBlockMode.Contact;
        Physics.SyncTransforms();
        var runtime = caster.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        Assert.That(attack.WindowOpen, Is.True, "Contact window remains open before interception.");
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(0));
        Assert.That(runtime.TryGetActiveBounds(out _), Is.True);
        Assert.That(Vector3.Dot(caster.transform.forward, guard.transform.forward), Is.LessThan(-.25f));
        Assert.That(attack.TryIntercept(runtime), Is.True, attack.LastProbe);
        Assert.That(attack.SuccessCount, Is.EqualTo(1));
        Assert.That(caster.MeleeController.IsComboActive, Is.False);
        Assert.That(player.HealthSystem.currentHealth, Is.EqualTo(100));
        Assert.That(attack.TryIntercept(runtime), Is.False, "A consumed contact cannot resolve twice.");
        Assert.That(attack.ConstrainContactRootMotion(caster.transform.position, lunge), Is.EqualTo(lunge),
            "Resolving the guard must release its root motion constraint.");
    }

    [Test]
    public void ComboSkillUsesStableIdsAndRejectsNestedCombos()
    {
        var combo = MakeCombo(2);
        Assert.That(combo.ValidateMelee(out _), Is.True);
        var first = combo.ComboSteps[0]; var second = combo.ComboSteps[1];
        Set(combo, "comboSteps", new List<SkillComboStep> { second, first });
        Assert.That(combo.TryGetComboStep(first.EntryId, out var selected, out int index), Is.True);
        Assert.That(index, Is.EqualTo(1)); Assert.That(selected.executionSkill, Is.SameAs(first.executionSkill));
        combo.SetComboChainWindow(first.EntryId, new Vector2(.2f, .4f));
        Assert.That(combo.ComboSteps[0].chainWindowN, Is.EqualTo(second.chainWindowN));
        Assert.That(combo.ComboSteps[1].chainWindowN, Is.EqualTo(new Vector2(.2f, .4f)));
        Set(combo, "comboSteps", new List<SkillComboStep> { new SkillComboStep(combo, "nested", Vector2.zero, false) });
        Assert.That(combo.ValidateMelee(out _), Is.False);
    }

    [Test]
    public void ContinuingBlockSuppressesOnlyTheOwnedStepAndComboCanAdvance()
    {
        var context = MakeRig(false); var combo = MakeCombo(2);
        var first = combo.ComboSteps[0].executionSkill;
        var profile = new SkillDefensiveBlockSettings();
        profile.windows = new[] { new DefensiveBlockWindow { startNormalized = 0f, endNormalized = .95f,
            hitboxSteps = new[] { 0 }, onSuccess = DefensiveBlockOutcome.ContinueSkill } };
        first.defensiveBlock = profile; context.baseStats.animProfile.lightMeleeSkill = combo;
        var block = context.gameObject.AddComponent<DefensiveBlockAttack>();
        Call(block, "Awake"); Call(block, "OnEnable"); context.ResolveReferences();
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var animancer = context.GetComponent<AnimancerComponent>(); animancer.Evaluate(.1f);
        int request = block.RequestId;
        Set(block, "acceptedWindow", 0); Call(block, "SuppressAcceptedWindow");
        Assert.That(context.MeleeController.IsComboActive, Is.True);
        Assert.That(block.OwnsCurrentSkill, Is.True);
        Assert.That(block.WindowOpen, Is.False);
        context.MeleeController.PressMelee(MeleeType.Light);
        animancer.Evaluate(.4f); animancer.Evaluate(.01f);
        Assert.That(context.AnimBrain.CurrentMeleeStepIndex, Is.EqualTo(1));
        Assert.That(context.MeleeController.IsComboActive, Is.True);
        Assert.That(block.Matches(request), Is.False, "Old Block requests must not affect the new combo step.");
    }

    [Test]
    public void BasicMeleeNeverTouchesEnergyOrSharedCharges()
    {
        var skill = MakeSkill();
        skill.baseManaCost = 500;
        skill.baseMaxCharges = 5;
        skill.baseCooldown = 20;
        var user = Track(new GameObject("user")).AddComponent<ProbeSkillUserBehaviour>();
        var pool = new SkillChargeState();
        pool.Refresh(1, Time.time);
        pool.TryReserve(1001, Time.time);
        var instance = new SkillInstance { def = skill };
        instance.BindCharges(pool);
        Assert.That(instance.TryReserveCast(user, SkillCastCostPolicy.Normal, true, out var reservation,
            SkillExecutionKind.BasicMelee), Is.True);
        Assert.That(pool.AvailableCharges, Is.Zero, "A basic attack must not resize/refresh the shared pool.");
        reservation.Commit();
        Assert.That(pool.AvailableCharges, Is.Zero);
        Assert.That(pool.RechargingCount, Is.Zero);
        Assert.That(pool.ReleaseReservation(1001), Is.True, "It must not consume another cast's reservation.");
        Assert.That(user.currentEnagy, Is.EqualTo(100));
    }

    [Test]
    public void MeleeTagAloneDoesNotMakeAnActiveSkillFree()
    {
        var skill = MakeSkill();
        skill.tags = SkillTag.Melee;
        skill.baseManaCost = 500;
        var user = Track(new GameObject("user")).AddComponent<ProbeSkillUserBehaviour>();
        Assert.That(new SkillInstance { def = skill }.TryReserveCast(user, SkillCastCostPolicy.Normal, true, out _), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlayerPressAdvancesComboAndReusesHost(bool nestedModules)
    {
        var context = MakeRig(nestedModules);
        var melee = context.MeleeController;
        var combo = MakeCombo(2);
        context.baseStats.animProfile.lightMeleeSkill = combo;
        context.stateHub.RequestMeleePress(MeleeType.Light);
        Assert.That(melee.IsComboActive, Is.True);
        Assert.That(context.AnimBrain.IsMeleePlaybackActive, Is.True);
        Assert.That(context.AnimBrain.IsSkillPlaybackActive, Is.False);
        Assert.That(context.stateHub.CanUseSkill(), Is.False);
        var firstRuntime = melee.GetComponentInChildren<SkillHitboxSequenceRuntime>(true);
        Assert.That(firstRuntime, Is.Not.Null);
        int first = RequestId(context.AnimBrain);
        context.stateHub.RequestMeleePress(MeleeType.Light);
        Call(context.AnimBrain, "RaiseMeleeChainWindow", first, true);
        Assert.That(context.AnimBrain.CurrentMeleeStepIndex, Is.EqualTo(1));
        Assert.That(RequestId(context.AnimBrain), Is.Not.EqualTo(first));
        Assert.That(melee.GetComponentsInChildren<SkillHitboxSequenceRuntime>(true).Length, Is.EqualTo(2));
        Assert.That(melee.GetComponentInChildren<SkillHitboxSequenceRuntime>(true), Is.SameAs(firstRuntime));
        int second = RequestId(context.AnimBrain);
        context.stateHub.RequestMeleePress(MeleeType.Light);
        Call(context.AnimBrain, "RaiseMeleeChainWindow", second, true);
        Assert.That(melee.GetComponentsInChildren<SkillHitboxSequenceRuntime>(true).Length, Is.EqualTo(2), "Repeating a payload must reuse its collider host.");
        Call(context.AnimBrain, "RaiseMeleeChainWindow", first, true);
        Assert.That(context.AnimBrain.CurrentMeleeStepIndex, Is.EqualTo(1), "Old request callbacks must not advance a new step.");
        melee.InterruptMelee();
        Assert.That(melee.IsComboActive, Is.False);
        Assert.That(context.stateHub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Ready));
        Assert.That(context.AnimBrain.IsMeleePlaybackActive, Is.False);
        Assert.That(context.GetComponentInChildren<SkillHitboxGroup>().Colliders[0].enabled, Is.False);
    }

    [Test]
    public void FrameZeroHitboxAndNormalCompletionUseTheSharedAnimationTimeline()
    {
        var context = MakeRig(false);
        context.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = context.MeleeController.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        var animancer = context.GetComponent<AnimancerComponent>();
        // Animancer dispatches evaluated events on its next graph update.
        animancer.Evaluate(.01f);
        animancer.Evaluate(.01f);
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(0));
        animancer.Evaluate(.3f);
        animancer.Evaluate(.01f);
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(-1));
        animancer.Evaluate(1f);
        animancer.Evaluate(.01f);
        Assert.That(context.MeleeController.IsComboActive, Is.False);
        Assert.That(context.stateHub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Ready));
        Assert.That(context.GetComponentInChildren<SkillHitboxGroup>().Colliders[0].enabled, Is.False);
    }

    [Test]
    public void MissingExecutionSkillFailsWithoutEnteringMeleeState()
    {
        var context = MakeRig(false);
        var combo = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        combo.comboEnabled = true; Set(combo, "comboSteps", new List<SkillComboStep> { new SkillComboStep() });
        context.baseStats.animProfile.heavyMeleeSkill = combo;
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Heavy), Is.False);
        Assert.That(context.stateHub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Ready));
    }

    [TestCase("death")]
    [TestCase("downed")]
    [TestCase("disable")]
    public void InterruptedActorsCloseTheHitWindowImmediately(string reason)
    {
        var context = MakeRig(false);
        context.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = context.MeleeController.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        int request = RequestId(context.AnimBrain);
        Call(runtime, "OnSkillTimelineEventRaised", request, CombatTimelineEventName.HitStart);
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(0));
        if (reason == "death") context.AnimBrain.PlayDead();
        else if (reason == "downed") context.AnimBrain.SetDowned(true);
        else Call(context.MeleeController, "OnDisable");
        Assert.That(context.MeleeController.IsComboActive, Is.False);
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(-1));
        Assert.That(context.GetComponentInChildren<SkillHitboxGroup>().Colliders[0].enabled, Is.False);
        Call(runtime, "OnSkillTimelineEventRaised", request, CombatTimelineEventName.HitStart);
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(-1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HitWindowsApplyLiveMeleeDamageOnceAndPublishMeleeMetadata(bool nestedModules)
    {
        var caster = MakeRig(nestedModules);
        var target = MakeRig(!nestedModules);
        target.transform.position = Vector3.right * 100f;
        caster.baseStats.Damage = 20;
        caster.baseStats.critRate = 0;
        caster.StatsHub.MarkDirty();
        target.HealthSystem.maximumHealth = target.HealthSystem.currentHealth = 1000;
        var collider = target.gameObject.AddComponent<BoxCollider>();
        var combo = MakeCombo(1, twoWindows: true);
        caster.baseStats.animProfile.lightMeleeSkill = combo;
        var facts = new List<PassiveEventContext>();
        caster.CombatEventBus.EventPublished += facts.Add;
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = caster.MeleeController.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        int request = RequestId(caster.AnimBrain);
        float expected = caster.StatsHub.GetSkillBaseDamage();
        Assert.That(expected, Is.GreaterThan(0f));
        Call(runtime, "OnSkillTimelineEventRaised", request, CombatTimelineEventName.HitStart);
        Call(runtime, "ProcessContact", collider);
        Call(runtime, "ProcessContact", collider);
        Assert.That(target.HealthSystem.currentHealth, Is.EqualTo(1000 - expected).Within(.001f));
        Assert.That(facts.Count, Is.EqualTo(1));
        Assert.That(facts[0].Metadata.SourceKind, Is.EqualTo(CombatSourceKind.Melee));
        string firstAttack = facts[0].AttackId;
        Call(runtime, "OnSkillTimelineEventRaised", request, CombatTimelineEventName.HitEnd);
        caster.baseStats.Damage = 40;
        caster.StatsHub.MarkDirty();
        float secondDamage = caster.StatsHub.GetSkillBaseDamage();
        Call(runtime, "OnSkillTimelineEventRaised", request, CombatTimelineEventName.HitStart);
        Call(runtime, "ProcessContact", collider);
        Assert.That(target.HealthSystem.currentHealth, Is.EqualTo(1000 - expected - secondDamage).Within(.001f));
        Assert.That(facts.Count, Is.EqualTo(2));
        Assert.That(facts[1].AttackId, Is.Not.EqualTo(firstAttack));
        caster.MeleeController.InterruptMelee();
        Call(runtime, "ProcessContact", collider);
        Assert.That(facts.Count, Is.EqualTo(2), "Interrupted hitboxes must stop immediately.");
    }

    [Test]
    public void SkillOnlyBlockDoesNotBlockBasicMelee()
    {
        var context = MakeRig(false);
        context.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Set(context.stateHub, "statusEffectControlBlocks", ControlBlockFlags.Skill);
        Assert.That(context.stateHub.CanUseSkill(), Is.False);
        Assert.That(context.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
    }

    [Test]
    public void ComboBufferExpiryAndLastStepRepeatKeepTheirRules()
    {
        var combo = MakeCombo(2);
        Type type = typeof(MeleeController).Assembly.GetType("MeleeComboSession");
        var session = Activator.CreateInstance(type, true);
        CallPublic(session, "Start", combo);
        Assert.That(CallPublic(session, "QueuePress").ToString(), Is.EqualTo("Continue"));
        Assert.That(CallPublic(session, "NotifyChainWindowOpened").ToString(), Is.EqualTo("Advance"));
        Assert.That(CallPublic(session, "QueuePress").ToString(), Is.EqualTo("Continue"));
        Assert.That(CallPublic(session, "NotifyChainWindowOpened").ToString(), Is.EqualTo("Advance"));
        Assert.That(type.GetProperty("CurrentStepIndex").GetValue(session), Is.EqualTo(1));
        CallPublic(session, "Start", combo);
        CallPublic(session, "NotifyChainWindowClosed");
        CallPublic(session, "QueuePress");
        Assert.That(CallPublic(session, "NotifyStepCompleted").ToString(), Is.EqualTo("Complete"));
    }

    [Test]
    public void BoneMotionSamplesNewContactsAndFiltersAttackColliders()
    {
        var caster = MakeRig(true);
        var target = MakeRig(false);
        target.transform.position = new Vector3(100, 0, 0);
        var hurtbox = target.gameObject.AddComponent<BoxCollider>();
        caster.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = caster.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        var group = caster.GetComponentInChildren<SkillHitboxGroup>();
        var hand = caster.transform.Find("Hand");
        Assert.That(group.transform.parent, Is.SameAs(hand));
        Call(runtime, "OnSkillTimelineEventRaised", RequestId(caster.AnimBrain), CombatTimelineEventName.HitStart);
        Assert.That(target.HealthSystem.currentHealth, Is.EqualTo(1000));
        hand.position = target.transform.position;
        hand.rotation = Quaternion.Euler(20, 45, 10);
        hand.localScale = new Vector3(2, 3, 4);
        Physics.SyncTransforms();
        Call(runtime, "LateUpdate");
        float remaining = target.HealthSystem.currentHealth;
        Assert.That(remaining, Is.LessThan(1000), "Motion after HitStart must be sampled.");
        Assert.That(Vector3.Distance(group.Colliders[0].transform.lossyScale, hand.lossyScale), Is.LessThan(.001f));
        Call(runtime, "LateUpdate");
        Assert.That(target.HealthSystem.currentHealth, Is.EqualTo(remaining), "One target per hit window.");
        Assert.That(runtime.CanDamageCollider(group.Colliders[0]), Is.False);
        var attack = new GameObject("OtherAttack"); attack.transform.SetParent(target.transform, false);
        var attackCollider = attack.AddComponent<BoxCollider>(); attackCollider.isTrigger = true;
        attack.AddComponent<SkillHitboxGroup>().Configure("Other", new[] { attackCollider });
        Assert.That(runtime.CanDamageCollider(attackCollider), Is.False);
        Set(caster.MeleeController, "targetMask", (LayerMask)0);
        Assert.That(runtime.CanDamageCollider(hurtbox), Is.False);
    }

    [Test]
    public void ModelAnimatorWinsOverTheRootPlaceholderAnimator()
    {
        var caster = MakeRig(false);
        var container = new GameObject("VisualModel"); container.transform.SetParent(caster.transform, false);
        var model = new GameObject("Model"); model.transform.SetParent(container.transform, false);
        model.AddComponent<Animator>();
        var hand = new GameObject("Hand"); hand.transform.SetParent(model.transform, false);
        caster.Visual = caster.gameObject.AddComponent<CharacterVisualController>();
        Set(caster.Visual, "modelRoot", container.transform);
        caster.Visual.animator = caster.GetComponent<Animator>();
        caster.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var group = caster.GetComponentInChildren<SkillHitboxGroup>();
        Assert.That(group.transform.parent, Is.SameAs(hand.transform));
    }

    [Test]
    public void CachedLayoutRebindsAfterAnimatorReplacementAndCleansDetachedGroups()
    {
        var caster = MakeRig(false);
        caster.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = caster.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        var group = caster.GetComponentInChildren<SkillHitboxGroup>();
        caster.MeleeController.InterruptMelee();
        var model = new GameObject("ReplacementModel"); model.transform.SetParent(caster.transform, false);
        var animator = model.AddComponent<Animator>();
        var hand = new GameObject("Hand"); hand.transform.SetParent(model.transform, false);
        caster.GetComponent<AnimancerComponent>().Animator = animator;
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        Assert.That(caster.GetComponentInChildren<SkillHitboxSequenceRuntime>(), Is.SameAs(runtime));
        Assert.That(group.transform.parent, Is.SameAs(hand.transform));
        caster.MeleeController.InterruptMelee();
        // These fixtures run without entering Play Mode, so invoke MonoBehaviour teardown explicitly.
        Call(runtime, "OnDestroy");
        Object.DestroyImmediate(runtime.gameObject);
        Assert.That(group == null, Is.True, "Runtime owns groups even when they are parented to model bones.");
    }

    [Test]
    public void DestroyedBoneClosesWindowAndCachedLayoutCanBeRebuilt()
    {
        var caster = MakeRig(false);
        caster.baseStats.animProfile.lightMeleeSkill = MakeCombo(1);
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        var runtime = caster.GetComponentInChildren<SkillHitboxSequenceRuntime>();
        Call(runtime, "OnSkillTimelineEventRaised", RequestId(caster.AnimBrain), CombatTimelineEventName.HitStart);
        Object.DestroyImmediate(caster.transform.Find("Hand").gameObject);
        Call(runtime, "LateUpdate");
        Assert.That(runtime.ActiveStepIndex, Is.EqualTo(-1));
        caster.MeleeController.InterruptMelee();
        var hand = new GameObject("Hand"); hand.transform.SetParent(caster.transform, false);
        Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
        Assert.That(caster.GetComponentInChildren<SkillHitboxGroup>(), Is.Not.Null);
        Assert.That(caster.GetComponentsInChildren<SkillHitboxSequenceRuntime>().Length, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingBoneRefusesExecutionWithoutLeavingColliders(bool basic)
    {
        var caster = MakeRig(false);
        var skill = MakeSkill();
        var payload = (PrefabHitboxSkillPayloadDef)skill.payload;
        payload.HitboxLayout.Groups[0].AnchorPath = "MissingBone";
        var user = caster.GetComponentInChildren<ProbeSkillUserBehaviour>();
        var context = new SkillCastContext(user, skill, default, caster.AnimBrain, 123,
            executionKind: basic ? SkillExecutionKind.BasicMelee : SkillExecutionKind.StandardSkill);
        var result = payload.ExecuteWithResult(context);
        Assert.That(result.Success, Is.False);
        Assert.That(caster.GetComponentsInChildren<SkillHitboxGroup>().Length, Is.Zero);
        Assert.That(user.currentEnagy, Is.EqualTo(100));
    }

    [Test]
    public void BoneLayoutRoundTripsThroughTheSceneAuthoringTool()
    {
        var caster = MakeRig(false);
        var skill = MakeSkill();
        var payload = (PrefabHitboxSkillPayloadDef)skill.payload;
        var hand = caster.transform.Find("Hand");
        hand.localPosition = new Vector3(3, 4, 5);
        hand.localRotation = Quaternion.Euler(0, 50, 10);
        hand.localScale = new Vector3(2, 3, 4);
        var shape = payload.HitboxLayout.Groups[0].Shapes[0];
        shape.LocalPosition = new Vector3(.2f, .4f, .6f);
        shape.Size = new Vector3(1, 2, 3);
        var tool = caster.gameObject.AddComponent<SetSkillHitBoxData>();
        Set(tool, "skill", skill);
        Call(tool, "LoadLayoutFromData");
        var group = hand.GetComponentInChildren<SkillHitboxGroup>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group.transform.parent, Is.SameAs(hand));
        Call(tool, "SaveLayoutFromSource");
        var saved = payload.HitboxLayout.Groups[0];
        Assert.That(saved.Anchor, Is.EqualTo(SkillHitboxLayoutData.AnchorSpace.AnimatorRoot));
        Assert.That(saved.AnchorPath, Is.EqualTo("Hand"));
        Assert.That(Vector3.Distance(saved.Shapes[0].LocalPosition, shape.LocalPosition), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(saved.Shapes[0].LocalScale, Vector3.one), Is.LessThan(.001f));
        Assert.That(saved.Shapes[0].Size, Is.EqualTo(shape.Size));
        var clone = new SkillHitboxLayoutData(); clone.CopyFrom(payload.HitboxLayout.Groups);
        Assert.That(clone.Groups[0].AnchorPath, Is.EqualTo("Hand"));
        Assert.That(clone.Groups[0].Shapes[0], Is.Not.SameAs(saved.Shapes[0]));
    }

    IdentityProbeContext MakeRig(bool nested)
    {
        var root = Track(new GameObject("MeleeTestRig"));
        var context = root.AddComponent<IdentityProbeContext>();
        context.baseStats = Track(ScriptableObject.CreateInstance<CharacterStats>());
        context.baseStats.Damage = 10;
        context.baseStats.maxHP = 1000;
        var module = root;
        if (nested) { module = new GameObject("GamePlayStats_System"); module.transform.SetParent(root.transform, false); }
        context.StatsHub = module.AddComponent<StatsHub>();
        Call(context.StatsHub, "Awake");
        context.CombatEventBus = module.AddComponent<CombatEventBus>();
        context.HealthSystem = module.AddComponent<HealthSystem>();
        context.HealthSystem.CTX = context;
        context.HealthSystem.maximumHealth = context.HealthSystem.currentHealth = 1000;
        context.stateHub = root.AddComponent<StateHub>();
        Call(context.stateHub, "Awake");
        var animator = root.AddComponent<Animator>();
        var animancer = root.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        context.AnimBrain = root.AddComponent<CharacterAnimBrain>();
        Set(context.AnimBrain, "animancer", animancer);
        var profile = Track(ScriptableObject.CreateInstance<CharacterAnimProfileSO>());
        context.baseStats.animProfile = profile;
        profile.locomotionDirectionalClips.idle = MakeClip();
        profile.locomotionDirectionalClips.forward = MakeClip();
        profile.locomotionDirectionalClips.backward = MakeClip();
        profile.locomotionDirectionalClips.left = MakeClip();
        profile.locomotionDirectionalClips.right = MakeClip();
        profile.crawlMixer = profile.ResolveLocomotionMixer();
        profile.crawling = new ClipTransition { Clip = MakeClip() };
        profile.dead = new ClipTransition { Clip = MakeClip() };
        context.AnimBrain.SetAnimProfileOverride(profile);
        Call(context.AnimBrain, "Update");
        context.AnimDriver = root.AddComponent<CharacterAnimDriver>();
        Call(context.AnimDriver, "Awake");
        module.AddComponent<ProbeSkillUserBehaviour>();
        context.SkillManager = module.AddComponent<CharacterSkillManager>();
        var bone = new GameObject("Hand"); bone.transform.SetParent(root.transform, false);
        context.MeleeController = root.AddComponent<MeleeController>();
        Call(context.MeleeController, "Awake");
        Call(context.MeleeController, "OnEnable");
        context.ResolveReferences();
        return context;
    }

    SkillGemDefinition MakeSkill(bool twoWindows = false)
    {
        var skill = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        skill.name = skill.displayName = "Test Melee";
        skill.skillId = "test.melee";
        skill.tags = SkillTag.Melee;
        skill.baseDamage = skill.baseStaggerPower = skill.baseCritChance = 0f;
        skill.damageCoefficient = 1f;
        skill.baseManaCost = skill.baseCooldown = skill.baseCastTime = skill.castPointNormalized = 0f;
        skill.skillClip = new ClipTransition { Clip = MakeClip() };
        AddEvent(skill.skillClip, 0f, CombatTimelineEventName.HitStart);
        AddEvent(skill.skillClip, .25f, CombatTimelineEventName.HitEnd);
        if (twoWindows)
        {
            AddEvent(skill.skillClip, .4f, CombatTimelineEventName.HitStart);
            AddEvent(skill.skillClip, .6f, CombatTimelineEventName.HitEnd);
        }
        var payload = Track(ScriptableObject.CreateInstance<PrefabHitboxSkillPayloadDef>());
        payload.name = "Melee Hitboxes";
        skill.payload = payload;
        var data = new SerializedObject(payload);
        data.FindProperty("anchorMode").enumValueIndex = (int)PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CasterRoot;
        var steps = data.FindProperty("steps"); steps.arraySize = twoWindows ? 2 : 1;
        for (int i = 0; i < steps.arraySize; i++)
        {
            var hit = steps.GetArrayElementAtIndex(i);
            var keys = hit.FindPropertyRelative("groupKeys"); keys.arraySize = 1;
            keys.GetArrayElementAtIndex(0).stringValue = "Strike";
            hit.FindPropertyRelative("damageMultiplier").floatValue = 1f;
            hit.FindPropertyRelative("hitPolicy").enumValueIndex = (int)PrefabHitboxSkillPayloadDef.HitPolicy.OncePerStep;
            hit.FindPropertyRelative("clearHitCacheOnEnter").boolValue = true;
            hit.FindPropertyRelative("overrideKnockback").boolValue = false;
            hit.FindPropertyRelative("knockbackDistance").floatValue = 0f;
            hit.FindPropertyRelative("knockbackDuration").floatValue = 0f;
            hit.FindPropertyRelative("knockbackInterruptsActions").boolValue = false;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        ConfigureTestLayout(skill);
        return skill;
    }
    SkillGemDefinition MakeCombo(int count, bool twoWindows = false)
    {
        var combo = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        var steps = new List<SkillComboStep>();
        for (int i = 0; i < count; i++)
        {
            var skill = MakeSkill(twoWindows);
            skill.skillId = "test.melee." + i;
            skill.name = skill.displayName = "Step " + i;
            // Damage-number pooling is Play Mode presentation, outside these combat assertions.
            Set(skill.payload, "showDamageNumbers", false);
            steps.Add(new SkillComboStep(skill, "step-" + i, new Vector2(.35f, .85f), true));
        }
        combo.comboEnabled = true; Set(combo, "comboSteps", steps);
        return combo;
    }
    static void ConfigureTestLayout(SkillGemDefinition skill)
    {
        var group = new SkillHitboxLayoutData.HitBoxGroupData { GroupKey = "Strike",
            Anchor = SkillHitboxLayoutData.AnchorSpace.AnimatorRoot, AnchorPath = "Hand" };
        group.Shapes.Add(new SkillHitboxLayoutData.HitBoxShapeData { Type = SkillHitboxLayoutData.HitBoxType.Box });
        ((PrefabHitboxSkillPayloadDef)skill.payload).ReplaceHitboxLayoutGroups(new List<SkillHitboxLayoutData.HitBoxGroupData> { group });
    }

    AnimationClip MakeClip()
    {
        var clip = Track(new AnimationClip());
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0,0,1,0));
        return clip;
    }
    static void AddEvent(ClipTransition clip, float time, CombatTimelineEventName name)
    {
        var nameAsset = StringAsset.Find(CombatTimelineEventNames.ToStringReference(name), out _);
        Assert.That(nameAsset, Is.Not.Null, "The project must contain the combat timeline event asset.");
        clip.SerializedEvents ??= new AnimancerEvent.Sequence.Serializable();
        clip.SerializedEvents.AddEvent(time, name: nameAsset);
    }
    static int RequestId(CharacterAnimBrain brain)
    {
        object channel = typeof(CharacterAnimBrain).GetField("_skillChannel", Hidden).GetValue(brain);
        return (int)channel.GetType().GetProperty("RequestId").GetValue(channel);
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    static object CallPublic(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    T Track<T>(T value) where T : Object { objects.Add(value); return value; }
}
#endif
