using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PassiveCompositionTestFixture
{
    readonly List<Object> owned = new();
    readonly List<CharacteContext> actors = new();
    T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
    public void Setup() => PassiveDispatch.Reset();
    public void Cleanup()
    {
        foreach (var actor in actors) if (actor != null && actor.PassiveController != null) Call(actor.PassiveController, "OnDisable");
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); actors.Clear(); PassiveDispatch.Reset();
    }
    static void Call(object value, string name) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(value, null);
    CharacteContext Actor(bool nested = false)
    {
        var root = Own(new GameObject("Passive test actor")); root.SetActive(false);
        var actor = root.AddComponent<PlayerContext>(); actors.Add(actor);
        actor.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        actor.baseStats.maxStamina = 100; actor.baseStats.maxHP = 100;
        var peer = root;
        if (nested) { peer = new GameObject("Modules"); peer.transform.SetParent(root.transform); }
        actor.CombatEventBus = peer.AddComponent<CombatEventBus>();
        actor.StatsHub = peer.AddComponent<StatsHub>();
        actor.StaminaSystem = peer.AddComponent<StaminaSystem>();
        actor.HealthSystem = peer.AddComponent<HealthSystem>(); actor.HealthSystem.CTX = actor;
        actor.HealthSystem.maximumHealth = 100; actor.HealthSystem.currentHealth = 50;
        actor.StatusEffects = peer.AddComponent<StatusEffectController>();
        actor.PassiveController = root.AddComponent<PassiveController>();
        root.SetActive(true); actor.ResolveReferences();
        Call(actor.CombatEventBus, "Awake"); Call(actor.StatsHub, "Awake"); Call(actor.StaminaSystem, "Awake");
        Call(actor.PassiveController, "OnEnable"); Call(actor.StatusEffects, "Awake");
        actor.StaminaSystem.RecalculateMaximumStamina(true);
        return actor;
    }
    ComposedPassiveDef Definition(params PassiveRecipeRule[] rules)
    {
        var d = Own(ScriptableObject.CreateInstance<ComposedPassiveDef>());
        d.name = "test"; d.recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
        d.recipe.rules.AddRange(rules); return d;
    }
    ComposedPassiveDef Roma() => RomaPassiveExample.Create(owned);
    [Test] public void CombatSurgeRequiresFullMomentumAndCommittedShot()
    {
        foreach (bool nested in new[] { false, true })
        {
            var actor = Actor(nested);
            var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(RomaCombatSurgeAuthoring.Path);
            Equip(actor, definition);
            var runtimes = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(runtimes);
            var runtime = runtimes.Single();
            Assert.That(PassiveRecipeValidator.Validate(definition.recipe), Is.Empty);
            actor.StaminaSystem.Spend(99);
            void Shot(bool commit)
            {
                var shot = new WeaponShotBuildContext { PassiveContext = Event(actor, PassiveEventType.ShotFired) };
                using var phase = actor.PassiveController.PrepareWeaponShot(ref shot);
                if (commit) { phase.Commit(); phase.Commit(); }
            }
            Shot(true);
            runtime.TryGetVariable("momentum", out float value, out _); Assert.That(value, Is.EqualTo(99));
            actor.StaminaSystem.Restore(100); actor.StaminaSystem.Spend(1);
            Shot(false);
            actor.CombatEventBus.Publish(Event(actor, PassiveEventType.DashStarted));
            actor.CombatEventBus.Publish(Event(actor, PassiveEventType.Reload));
            actor.PassiveController.Tick(10);
            runtime.TryGetVariable("momentum", out value, out _); Assert.That(value, Is.EqualTo(100));
            Assert.That(actor.PassiveController.ReserveNextAttack("no-damage-bonus"), Is.Null);
            Shot(true);
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out float duration), Is.EqualTo(5));
            Assert.That(duration, Is.EqualTo(5));
            Assert.That(actor.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1.3f).Within(.001));
            Assert.That(actor.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.8f).Within(.001));
            actor.StaminaSystem.Spend(20);
            runtime.TryGetVariable("momentum", out value, out _); Assert.That(value, Is.Zero);
            actor.PassiveController.Tick(2);
            actor.PassiveController.RefreshPassiveLoadout();
            Shot(true);
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out _), Is.EqualTo(3));
            actor.PassiveController.Tick(3);
            Assert.That(actor.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1));
            Assert.That(actor.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.6f));
            actor.StaminaSystem.Spend(10);
            runtime.TryGetVariable("momentum", out value, out _); Assert.That(value, Is.EqualTo(10));
            actor.StaminaSystem.Restore(100); actor.StaminaSystem.Spend(90); Shot(true);
            Equip(actor);
            Assert.That(actor.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1));
            Assert.That(actor.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.6f));
            Equip(actor, definition);
            runtimes.Clear(); actor.PassiveController.AppendRuntimes(runtimes);
            runtimes.Single().TryGetVariable("momentum", out value, out _); Assert.That(value, Is.Zero);
        }
    }
    static PassiveRecipeRule RestoreRule(int count = 1, float cooldown = 0) => new()
    {
        id = "restore", eventType = PassiveEventType.Hit, count = new(count), cooldown = new(cooldown),
        steps = new() { new PassiveResourceStep { id = "restore", amount = new(10) } }
    };
    static void Equip(CharacteContext actor, params PassiveDefinition[] definitions) => actor.PassiveController.SetRuntimePassives(definitions);
    static PassiveEventContext Event(CharacteContext actor, PassiveEventType type = PassiveEventType.Hit, GameObject target = null) =>
        actor.CombatEventBus.CreateExternalContext(type, target: target, attackId: actor.CombatEventBus.CreateAttackId());
    static void Hit(CharacteContext actor) => actor.CombatEventBus.Publish(Event(actor));
    static List<RuntimeStatModifier> Modifiers(CharacteContext actor)
    { var result = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(result); return result; }

    [Test] public void EmptyOwnerHasNoRuntimeStateSubscriptionOrUpdate()
    {
        var actor = Actor(); var controller = actor.PassiveController;
        Assert.That(controller.RuntimeCount, Is.Zero); Assert.That(controller.RuntimeStateCount, Is.Zero);
        Assert.That(controller.HasCombatSubscription, Is.False); Assert.That(controller.RequiresTick, Is.False);
        Assert.That(typeof(PassiveController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        Assert.That(controller.ReserveNextAttack("attack"), Is.Null);
    }
    [Test] public void StaticModifierAllocatesNoCounterOrTimer()
    {
        var actor = Actor();
        var step = new PassiveModifierStep { whileEquipped = true };
        step.modifier.modifiers.Add(new PassiveStatModifier { statType = StatType.Damage, value = 12 });
        Equip(actor, Definition(new PassiveRecipeRule { trigger = PassiveRuleTrigger.Equipped, steps = new() { step } }));
        Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(12));
        Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero);
        Assert.That(actor.PassiveController.RequiresTick || actor.PassiveController.HasCombatSubscription, Is.False);
        Equip(actor); Assert.That(Modifiers(actor), Is.Empty);
    }
    [Test] public void SharedDefinitionSeparatesActorsAndNestedModules()
    {
        var a = Actor(); var b = Actor(true); var d = Definition(RestoreRule(2));
        a.StaminaSystem.Spend(50); b.StaminaSystem.Spend(50); Equip(a, d); Equip(b, d);
        Hit(a); Hit(b); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(60)); Assert.That(b.StaminaSystem.Current, Is.EqualTo(50));
    }
    [Test] public void DuplicateGrantsHaveIndependentCounters()
    {
        var a = Actor(); a.StaminaSystem.Spend(60); var d = Definition(RestoreRule(2));
        a.PassiveController.SetRuntimeGrants(new[] { new PassiveGrant(d, "slot", "a") }); Hit(a);
        a.PassiveController.SetRuntimeGrants(new[] { new PassiveGrant(d, "slot", "a"), new PassiveGrant(d, "slot", "b") }); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(50));
        Hit(a); Assert.That(a.StaminaSystem.Current, Is.EqualTo(60));
    }
    [Test] public void FragmentOverridesDoNotMutateSharedData()
    {
        var a = Actor(); a.StaminaSystem.Spend(80);
        var f = Own(ScriptableObject.CreateInstance<PassiveFragment>());
        f.parameters.Add(new PassiveParameter { id = "amount", number = 2 });
        f.steps.Add(new PassiveResourceStep { amount = new PassiveNumber { source = PassiveValueSource.Parameter, reference = "amount" } });
        var rule = RestoreRule(); rule.steps = new()
        {
            new PassiveFragmentStep { id = "first", fragment = f, overrides = new() { new PassiveParameter { id = "amount", number = 3 } } },
            new PassiveFragmentStep { id = "second", fragment = f, overrides = new() { new PassiveParameter { id = "amount", number = 7 } } }
        };
        Equip(a, Definition(rule)); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(30)); Assert.That(f.parameters[0].number, Is.EqualTo(2));
    }
    [Test] public void ValidatorRejectsCyclesAndWrongParameterTypes()
    {
        var f = Own(ScriptableObject.CreateInstance<PassiveFragment>());
        f.parameters.Add(new PassiveParameter { id = "number" });
        f.steps.Add(new PassiveFragmentStep { fragment = f });
        var d = Definition(new PassiveRecipeRule { eventType = PassiveEventType.Hit, steps = new() { new PassiveFragmentStep { fragment = f,
            overrides = new() { new PassiveParameter { id = "number", kind = PassiveParameterKind.Status } } } } });
        var errors = PassiveRecipeValidator.Validate(d.recipe);
        Assert.That(errors.Any(e => e.Contains("cycle")), Is.True);
        Assert.That(errors.Any(e => e.Contains("parameter")), Is.True);
    }
    [Test] public void CooldownAndCounterSurviveRefresh()
    {
        var a = Actor(); a.StaminaSystem.Spend(80); var d = Definition(RestoreRule(2, 5)); Equip(a, d);
        Hit(a); a.PassiveController.RefreshPassiveLoadout(); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(30));
        Hit(a); Hit(a); a.PassiveController.RefreshPassiveLoadout(); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(30));
        a.PassiveController.Tick(5); Hit(a); Assert.That(a.StaminaSystem.Current, Is.EqualTo(40));
    }
    [Test] public void ShorterUpgradeCooldownClampsRemainingTime()
    {
        var a = Actor(); a.StaminaSystem.Spend(80); var rule = RestoreRule(1, 10);
        rule.cooldown.upgrades.Add(new PassiveNumericUpgrade { upgradeId = "fast", operation = ModifierOp.Flat, value = -8 });
        var d = Definition(rule); Equip(a, d); Hit(a);
        var upgrade = new SkillUpgradeStatSnapshot(); upgrade.AddNode(new SkillUpgradeNodeData { grantedUpgradeIds = new() { "fast" } });
        // Preserve the grant key while changing its snapshot.
        var controller = a.PassiveController;
        controller.SetRuntimeGrants(new[] { new PassiveGrant(d, "test", "same") }); Hit(a);
        controller.SetRuntimeGrants(new[] { new PassiveGrant(d, "test", "same", upgrade) });
        float before = a.StaminaSystem.Current; controller.Tick(2); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(before + 10));
    }
    [Test] public void TimedBuffSurvivesUnequipAndExpiresWithoutPassive()
    {
        var a = Actor(); var step = new PassiveModifierStep(); step.modifier.durationSeconds = 3;
        step.modifier.modifiers.Add(new PassiveStatModifier { statType = StatType.Damage, value = 8 });
        Equip(a, Definition(new PassiveRecipeRule { eventType = PassiveEventType.Hit, steps = new() { step } }));
        Hit(a); Equip(a);
        Assert.That(a.PassiveController.RuntimeCount, Is.Zero); Assert.That(a.PassiveController.RequiresTick, Is.True);
        Assert.That(Modifiers(a).Single().Value, Is.EqualTo(8));
        a.PassiveController.Tick(3); Assert.That(Modifiers(a), Is.Empty); Assert.That(a.PassiveController.RequiresTick, Is.False);
    }
    [Test] public void RomaResourceSpendUsesActualPaymentsOnly()
    {
        var a = Actor(); Equip(a, Roma());
        Assert.That(a.StaminaSystem.Spend(10, "dash"), Is.True);
        Assert.That(a.StaminaSystem.Spend(6, "slide"), Is.True);
        Assert.That(a.StaminaSystem.Spend(1000), Is.False);
        a.StaminaSystem.Restore(8); a.StaminaSystem.RecalculateMaximumStamina();
        using var attack = a.PassiveController.ReserveNextAttack("one");
        Assert.That(attack.Multiplier, Is.EqualTo(1.32f).Within(0.0001f)); attack.Commit();
        using var next = a.PassiveController.ReserveNextAttack("two"); Assert.That(next.Multiplier, Is.EqualTo(1));
    }
    [Test] public void CancelledReservationRefundsWithoutLosingNewMomentum()
    {
        var a = Actor(); Equip(a, Roma()); a.StaminaSystem.Spend(10);
        var attack = a.PassiveController.ReserveNextAttack("cancel"); a.StaminaSystem.Spend(5); attack.Dispose();
        using var next = a.PassiveController.ReserveNextAttack("next"); Assert.That(next.Multiplier, Is.EqualTo(1.3f).Within(0.0001f));
    }
    [Test] public void CommittedAttackKeepsSnapshotAcrossNewSpendingAndUnequip()
    {
        var a = Actor(); Equip(a, Roma()); a.StaminaSystem.Spend(10);
        using var attack = a.PassiveController.ReserveNextAttack("projectile"); attack.Commit();
        a.StaminaSystem.Spend(5); Equip(a);
        Assert.That(attack.Multiplier, Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(a.PassiveController.ReserveNextAttack("later"), Is.Null);
    }
    [Test] public void MissingStaminaDamageUsesStrictSeventyPercentBoundary()
    {
        var a = Actor(); Equip(a, Roma()); a.StaminaSystem.Spend(30);
        Assert.That(Modifiers(a).Where(m => m.StatType == StatType.Damage), Is.Empty);
        a.StaminaSystem.Spend(10);
        Assert.That(Modifiers(a).Single(m => m.StatType == StatType.Damage).Value, Is.EqualTo(20).Within(0.001f));
        a.StaminaSystem.Restore(10);
        Assert.That(Modifiers(a).Where(m => m.StatType == StatType.Damage), Is.Empty);
    }
    [Test] public void ConversionConsumesBonusOnceAndDoesNotCompound()
    {
        var a = Actor(true); var bonus = new PassiveModifierStep { whileEquipped = true };
        bonus.modifier.modifiers.Add(new PassiveStatModifier { statType = StatType.StaminaRegenRate, value = 5 });
        var regen = Definition(new PassiveRecipeRule { trigger = PassiveRuleTrigger.Equipped, steps = new() { bonus } });
        var roma = Roma(); Equip(a, regen, roma, roma);
        for (int i = 0; i < 4; i++)
        {
            a.PassiveController.RefreshPassiveLoadout(); a.StaminaSystem.RecalculateMaximumStamina();
            Assert.That(a.StatsHub.GetMaximumStamina(), Is.EqualTo(120).Within(0.001f));
            Assert.That(a.StatsHub.GetStaminaRegenerationRate(15), Is.EqualTo(15).Within(0.001f));
        }
        Equip(a, regen); Assert.That(a.StatsHub.GetMaximumStamina(), Is.EqualTo(100));
        Assert.That(a.StatsHub.GetStaminaRegenerationRate(15), Is.EqualTo(20));
    }
    [Test] public void FactsAreDeduplicatedAndSignalsRunAfterCurrentRecipe()
    {
        var a = Actor(); a.StaminaSystem.Spend(80);
        var root = RestoreRule(); root.steps.Insert(0, new PassiveSignalStep { signal = "follow" });
        var follow = RestoreRule(); follow.id = "follow"; follow.eventType = PassiveEventType.PassiveSignal;
        follow.originFilter = PassiveOriginFilter.PassiveOnly; follow.eventSourceId = "follow";
        // Signal listener observes the restore from the root recipe before evaluating.
        follow.condition = new PassiveCondition { kind = PassiveConditionKind.Not, children = new() { new PassiveCondition { kind = PassiveConditionKind.StaminaBelow, threshold = new(0.3f) } } };
        Equip(a, Definition(root, follow)); var fact = Event(a); a.CombatEventBus.Publish(fact); a.CombatEventBus.Publish(fact);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(40));
    }
    [Test] public void SignalCycleTerminatesWithoutRecursion()
    {
        var a = Actor(); var start = new PassiveRecipeRule { id = "start", eventType = PassiveEventType.Hit, steps = new() { new PassiveSignalStep { signal = "loop" } } };
        var loop = new PassiveRecipeRule { id = "loop", eventType = PassiveEventType.PassiveSignal, originFilter = PassiveOriginFilter.Any,
            steps = new() { new PassiveSignalStep { signal = "loop" } } };
        Equip(a, Definition(start, loop)); int procs = 0; a.PassiveController.PassiveTriggered += _ => procs++;
        Hit(a); Assert.That(procs, Is.EqualTo(2));
    }
    [Test] public void DisableClearsStateAndOldReservationCannotRefundNewLifetime()
    {
        var a = Actor(); var d = Roma(); Equip(a, d); a.StaminaSystem.Spend(10);
        var reservation = a.PassiveController.ReserveNextAttack("old");
        Call(a.PassiveController, "OnDisable"); Call(a.PassiveController, "OnEnable"); reservation.Dispose();
        using var next = a.PassiveController.ReserveNextAttack("new"); Assert.That(next.Multiplier, Is.EqualTo(1));
    }
    [Test] public void ExistingAssetsMigratedAndSecondRunIsNoOp()
    {
        var report = PassiveRecipeMigration.Run(true);
        Assert.That(report.errors, Is.Empty); Assert.That(report.pending, Is.Zero);
        Assert.That(report.total, Is.GreaterThanOrEqualTo(10));
        var a = AssetDatabase.LoadAssetAtPath<PassiveDefinition>("Assets/Character/Roma/Passive/Passive.Roma_DamageTree.01.asset");
        var b = AssetDatabase.LoadAssetAtPath<PassiveDefinition>("Assets/Character/Roma/Passive/Passive.Roma_MoveTree.01.asset");
        Assert.That(a.DefinitionId, Is.Not.EqualTo(b.DefinitionId));
    }
    [Test] public void ExistingSkillUpgradeSmokeTestsRemainValid() => ActiveSkillFeatureSmokeTests.RunFromCommandLine();

    [Test] public void PerfectDashRefundsOncePerExecutionNotPerIncomingAttack()
    {
        var a = Actor(); Equip(a, Roma()); a.StaminaSystem.Spend(50);
        PassiveEventContext Dodge(ulong dash) => a.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            attackId: a.CombatEventBus.CreateAttackId(), metadata: new CombatEventMetadata(dashExecutionId: dash));
        a.CombatEventBus.Publish(Dodge(11)); a.CombatEventBus.Publish(Dodge(11));
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(60));
        a.CombatEventBus.Publish(Dodge(12)); Assert.That(a.StaminaSystem.Current, Is.EqualTo(70));
        using var attack = a.PassiveController.ReserveNextAttack("refund-is-not-spend");
        Assert.That(attack.Multiplier, Is.EqualTo(2));
    }
    [Test] public void StatusAppliedFactChainsToHealWithFollowUpDisabled()
    {
        var a = Actor(); var target = Actor(true);
        var status = Own(ScriptableObject.CreateInstance<StatusEffectDef>()); status.effectId = "test.poison";
        var apply = new PassiveRecipeRule { id = "apply", eventType = PassiveEventType.Hit,
            steps = new() { new PassiveStatusStep { spec = new StatusApplicationSpec { effect = status }, target = new PassiveTarget { mode = PassiveTargetMode.EventTarget } } } };
        var heal = new PassiveRecipeRule { id = "heal", eventType = PassiveEventType.StatusApplied,
            originFilter = PassiveOriginFilter.Any, steps = new() { new PassiveResourceStep { operation = PassiveResourceOperation.Heal, amount = new(10) } } };
        Equip(a, Definition(apply, heal));
        a.CombatEventBus.Publish(Event(a, target: target.gameObject));
        Assert.That(target.StatusEffects.HasEffect(status), Is.True);
        Assert.That(a.HealthSystem.currentHealth, Is.EqualTo(60));
    }
    [Test] public void MissingStatusReceiverSkipsSuccessBranch()
    {
        var a = Actor(); var status = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        var rule = new PassiveRecipeRule { eventType = PassiveEventType.Hit, steps = new()
        {
            new PassiveStatusStep { target = new PassiveTarget { mode = PassiveTargetMode.EventTarget }, spec = new StatusApplicationSpec { effect = status } },
            new PassiveBranchStep { condition = new PassiveCondition { kind = PassiveConditionKind.LastSucceeded },
                whenTrue = new() { new PassiveResourceStep { operation = PassiveResourceOperation.Heal } } }
        }};
        Equip(a, Definition(rule)); Hit(a);
        Assert.That(a.HealthSystem.currentHealth, Is.EqualTo(50));
    }
    [Test] public void FragmentLocalCountersDoNotShareBetweenCallSites()
    {
        var a = Actor();
        var fragment = Own(ScriptableObject.CreateInstance<PassiveFragment>());
        fragment.variables.Add(new PassiveVariable { id = "charge", maximum = 100 });
        fragment.steps.Add(new PassiveAccumulatorStep { variable = "charge", amount = new(10) });
        fragment.steps.Add(new PassiveNextAttackStep { variable = "charge", damagePercentPerUnit = new(1) });
        var rule = new PassiveRecipeRule { trigger = PassiveRuleTrigger.Equipped, steps = new()
        {
            new PassiveFragmentStep { id = "a", fragment = fragment },
            new PassiveFragmentStep { id = "b", fragment = fragment }
        }};
        Equip(a, Definition(rule));
        using var attack = a.PassiveController.ReserveNextAttack("separate");
        Assert.That(attack.Multiplier, Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(a.PassiveController.RuntimeStateCount, Is.EqualTo(2));
    }
    [Test] public void ResourceSpendingReportsClampedActualValueAndIgnoresInvalidCosts()
    {
        var a = Actor(); float amount = 0; int events = 0;
        a.StaminaSystem.ResourceSpent += e => { amount += e.Amount; events++; Assert.That(e.FactId, Is.Not.Zero); };
        a.StaminaSystem.Spend(float.NaN); a.StaminaSystem.Spend(0); a.StaminaSystem.Spend(1000);
        Assert.That(events, Is.Zero);
        a.StaminaSystem.Spend(100.00005f, "rounding");
        Assert.That(amount, Is.EqualTo(100)); Assert.That(events, Is.EqualTo(1));
        Assert.That(a.StaminaSystem.Restore(200), Is.EqualTo(100));
        Assert.That(events, Is.EqualTo(1));
    }
    [Test] public void FailedProjectileSpawnReturnsFalseWithoutConsumingReservation()
    {
        var a = Actor(); Equip(a, Roma()); a.StaminaSystem.Spend(10);
        using (var attack = a.PassiveController.ReserveNextAttack("failed"))
        {
            Assert.That(new WeaponProjectileSpawner().TrySpawn(default), Is.False);
        }
        using var retry = a.PassiveController.ReserveNextAttack("retry");
        Assert.That(retry.Multiplier, Is.EqualTo(1.2f).Within(0.0001f));
    }
    [Test] public void ReorderedRuntimeListRetainsCounterWithDefinition()
    {
        var a = Actor(); a.StaminaSystem.Spend(80);
        var first = Definition(RestoreRule(2)); var second = Definition(RestoreRule(3));
        Equip(a, first, second); Hit(a); Equip(a, second, first); Hit(a);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(30));
        Hit(a); Assert.That(a.StaminaSystem.Current, Is.EqualTo(40));
    }
    [Test] public void BudgetStopsLargeRecipeAndReportsOnce()
    {
        var a = Actor(); var rule = new PassiveRecipeRule { eventType = PassiveEventType.Hit };
        for (int i = 0; i < 1025; i++) rule.steps.Add(new PassiveResourceStep { id = "step-" + i, amount = new(0) });
        Equip(a, Definition(rule));
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Chain step budget exceeded"));
        Hit(a);
    }
    [Test] public void MigratedAssetsPreserveLegacyRuleAndPickupData()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:PassiveDefinition"))
        {
            var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition is TriggeredPassiveDef old)
            {
                Assert.That(definition.recipe.rules.Count, Is.EqualTo(old.rules.Count));
                for (int i = 0; i < old.rules.Count; i++)
                {
                    Assert.That(definition.recipe.rules[i].eventType, Is.EqualTo(old.rules[i].trigger));
                    Assert.That(definition.recipe.rules[i].count.value, Is.EqualTo(old.rules[i].requiredCount));
                    Assert.That(definition.recipe.rules[i].steps.Count, Is.EqualTo(old.rules[i].actions.Count));
                }
            }
            if (definition is CustomPassiveDef custom)
            {
                for (int i = 0; i < custom.behaviors.Count; i++)
                {
                    if (custom.behaviors[i] is not DropAmmoOnShotPassiveBehavior behavior) continue;
                    var expected = new SerializedObject(behavior);
                    var pickup = (PassivePickupStep)definition.recipe.rules[i].steps.Single();
                    Assert.That(pickup.pickupPrefab, Is.SameAs(expected.FindProperty("pickupPrefab").objectReferenceValue));
                    Assert.That(pickup.dropCount, Is.EqualTo(expected.FindProperty("dropCount").intValue));
                }
            }
        }
    }

    [Test] public void UpgradeRefreshDoesNotReplayEquippedResourceEffect()
    {
        var a = Actor(); a.StaminaSystem.Spend(50);
        Equip(a, Definition(new PassiveRecipeRule { trigger = PassiveRuleTrigger.Equipped, steps = new() { new PassiveResourceStep { amount = new(10) } } }));
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(60));
        a.PassiveController.RefreshPassiveLoadout();
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(60));
    }
    [Test] public void PassiveResourceChildrenStayInOriginalChain()
    {
        var a = Actor(); a.StaminaSystem.Spend(50);
        var rule = new PassiveRecipeRule { trigger = PassiveRuleTrigger.ResourceChanged, originFilter = PassiveOriginFilter.Any,
            steps = new() { new PassiveResourceStep { amount = new(1) } } };
        Equip(a, Definition(rule)); a.StaminaSystem.Restore(1);
        Assert.That(a.StaminaSystem.Current, Is.EqualTo(52));
    }
    [Test] public void TimersExistOnlyWhileCooldownOrWindowIsPending()
    {
        var a = Actor(); a.StaminaSystem.Spend(50); Equip(a, Definition(RestoreRule(cooldown: 2)));
        Assert.That(a.PassiveController.RequiresTick, Is.False);
        Hit(a); Assert.That(a.PassiveController.RequiresTick, Is.True);
        a.PassiveController.Tick(2.1f); Assert.That(a.PassiveController.RequiresTick, Is.False);
        Hit(a); Assert.That(a.StaminaSystem.Current, Is.EqualTo(70));
    }
    [Test] public void HealingReportsActualAmountBeforeNotificationSideEffects()
    {
        var a = Actor();
        a.HealthSystem.Healed += (_, _, _) => a.HealthSystem.currentHealth = 20;
        Assert.That(a.HealthSystem.HealActual(100), Is.EqualTo(50));
        Assert.That(a.HealthSystem.currentHealth, Is.EqualTo(20));
    }


    [Test] public void MeleeUsesOneMomentumSnapshotAcrossMultipleTargetsAndUnequip()
    {
        var fixture = new MeleeSkillIntegrationTests();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        CharacteContext caster = null;
        try
        {
            caster = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", flags).Invoke(fixture, new object[] { true });
            var first = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", flags).Invoke(fixture, new object[] { false });
            var second = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", flags).Invoke(fixture, new object[] { true });
            caster.baseStats.maxStamina = 100; caster.baseStats.critRate = 0;
            caster.StaminaSystem = caster.gameObject.AddComponent<StaminaSystem>();
            caster.PassiveController = caster.gameObject.AddComponent<PassiveController>();
            Call(caster.StaminaSystem, "Awake"); Call(caster.PassiveController, "OnEnable");
            caster.StatsHub.MarkDirty(); caster.StaminaSystem.RecalculateMaximumStamina(true); Equip(caster, Roma());
            var combo = (SkillGemDefinition)typeof(MeleeSkillIntegrationTests).GetMethod("MakeCombo", flags).Invoke(fixture, new object[] { 1, false });
            caster.baseStats.animProfile.lightMeleeSkill = combo;
            first.transform.position = second.transform.position = Vector3.right * 100;
            var firstCollider = first.gameObject.AddComponent<BoxCollider>(); var secondCollider = second.gameObject.AddComponent<BoxCollider>();
            caster.StaminaSystem.Spend(10);
            float expected = caster.StatsHub.GetSkillBaseDamage() * 1.2f;
            Assert.That(caster.MeleeController.TryStartMelee(MeleeType.Light), Is.True);
            var runtime = caster.MeleeController.GetComponentInChildren<SkillHitboxSequenceRuntime>();
            int request = (int)typeof(MeleeSkillIntegrationTests).GetMethod("RequestId", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { caster.AnimBrain });
            typeof(SkillHitboxSequenceRuntime).GetMethod("OnSkillTimelineEventRaised", flags).Invoke(runtime, new object[] { request, CombatTimelineEventName.HitStart });
            caster.StaminaSystem.Spend(5);
            typeof(SkillHitboxSequenceRuntime).GetMethod("ProcessContact", flags).Invoke(runtime, new object[] { firstCollider });
            Equip(caster);
            typeof(SkillHitboxSequenceRuntime).GetMethod("ProcessContact", flags).Invoke(runtime, new object[] { secondCollider });
            Assert.That(first.HealthSystem.currentHealth, Is.EqualTo(1000 - expected).Within(0.001f));
            Assert.That(second.HealthSystem.currentHealth, Is.EqualTo(1000 - expected).Within(0.001f));
            caster.MeleeController.InterruptMelee();
        }
        finally
        {
            if (caster != null && caster.PassiveController != null) Call(caster.PassiveController, "OnDisable");
            fixture.Cleanup();
        }
    }
    [Test] public void PreparedNormalShotsKeepDamageSnapshotAndGeneratedShotsUseBaseDamage()
    {
        var a = Actor(); Equip(a, Roma());
        var weapon = a.gameObject.AddComponent<WeaponSystem>(); a.WeaponSystem = weapon;
        WeaponAimTestSource.Bind(weapon, a.transform); Call(weapon, "Awake"); weapon.damage = 100;
        a.StaminaSystem.Spend(10);
        using var reservation = a.PassiveController.ReserveNextAttack("shot");
        var prepare = typeof(WeaponSystem).GetMethod("CreateWeaponShotContext", BindingFlags.Instance | BindingFlags.NonPublic);
        var shot = (WeaponShotContext)prepare.Invoke(weapon, new object[] { 10, 9, true, "shot", reservation.Multiplier });
        reservation.Commit(); a.StaminaSystem.Spend(5); Equip(a);
        var generated = (WeaponShotContext)prepare.Invoke(weapon, new object[] { 9, 9, false, null, 1f });
        Assert.That(shot.Damage, Is.EqualTo(120).Within(0.001f));
        Assert.That(shot.AttackId, Is.EqualTo("shot"));
        Assert.That(generated.Damage, Is.EqualTo(100).Within(0.001f));
    }
    [Test] public void InvalidConditionCyclesAreRejectedBeforeRuntime()
    {
        var condition = new PassiveCondition { kind = PassiveConditionKind.All };
        condition.children.Add(condition);
        var d = Definition(new PassiveRecipeRule { eventType = PassiveEventType.Hit, condition = condition });
        Assert.That(PassiveRecipeValidator.Validate(d.recipe).Any(e => e.Contains("cycle")), Is.True);
        condition.children.Clear();
    }


    [Test] public void RecipientTimersRespectWorldSlowWithoutSlowingPlayer()
    {
        var fixture = new MeleeSkillIntegrationTests();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var slow = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", flags).Invoke(fixture, new object[] { false });
        var existingManager = Object.FindAnyObjectByType<TimeSlowManager>();
        var manager = TimeSlowManager.Instance;
        float previous = manager.WorldTimeScale;
        try
        {
            typeof(TimeSlowManager).GetProperty("WorldTimeScale").SetValue(manager, 0.25f);
            slow.PassiveController = slow.gameObject.AddComponent<PassiveController>(); Call(slow.PassiveController, "OnEnable");
            var player = Actor();
            var buff = new PassiveActionDefinition { durationSeconds = 1 };
            buff.modifiers.Add(new PassiveStatModifier { statType = StatType.Armor, value = 2 });
            slow.PassiveController.TryApplyRuntimeModifier(buff, "slow", 0);
            player.PassiveController.TryApplyRuntimeModifier(buff, "player", 0);
            slow.PassiveController.Tick(2); player.PassiveController.Tick(2);
            Assert.That(Modifiers(slow).Count, Is.EqualTo(1));
            Assert.That(Modifiers(player), Is.Empty);
            Assert.That(slow.PassiveController.Clock, Is.EqualTo(0.5).Within(0.001));
            Assert.That(player.PassiveController.Clock, Is.EqualTo(2).Within(0.001));
        }
        finally
        {
            typeof(TimeSlowManager).GetProperty("WorldTimeScale").SetValue(manager, previous);
            if (slow.PassiveController != null) Call(slow.PassiveController, "OnDisable");
            fixture.Cleanup();
            if (existingManager == null && manager != null) Object.DestroyImmediate(manager.gameObject);
        }
    }
    [Test] public void AreaEffectsReportAllSuccessfulTargetsAndActualHealing()
    {
        var a = Actor(); var b = Actor(true);
        a.transform.position = b.transform.position = Vector3.right * 10000;
        CharacterContextRegistry.Register(a); CharacterContextRegistry.Register(b);
        try
        {
            var d = Definition(RestoreRule());
            var runtime = new PassiveRuntime(a.PassiveController, new PassiveGrant(d, "test", "area"));
            var execution = new PassiveExecution(runtime, Event(a), "area");
            var effect = new PassiveResourceStep { operation = PassiveResourceOperation.Heal, amount = new(100),
                target = new PassiveTarget { mode = PassiveTargetMode.AroundSelf, radius = 1 } };
            var result = effect.Execute(execution);
            Assert.That(result.Status, Is.EqualTo(PassiveOutcome.Succeeded));
            Assert.That(result.Amount, Is.EqualTo(100));
            Assert.That(result.SuccessfulTargets, Is.EquivalentTo(new[] { a, b }));
        }
        finally { CharacterContextRegistry.Unregister(a); CharacterContextRegistry.Unregister(b); }
    }


    [Test] public void ConversionChangesInvalidateStatRevisionWithoutNewOrdinaryModifiers()
    {
        var a = Actor();
        var regen = new PassiveModifierStep { whileEquipped = true };
        regen.modifier.modifiers.Add(new PassiveStatModifier { statType = StatType.StaminaRegenRate, value = 5 });
        var conversion = new PassiveConversionStep { ratio = new(4) };
        Equip(a, Definition(new PassiveRecipeRule { trigger = PassiveRuleTrigger.Equipped, steps = new() { regen, conversion } }));
        Assert.That(a.StatsHub.GetMaximumStamina(), Is.EqualTo(120));
        conversion.ratio.value = 6; a.PassiveController.RefreshPassiveLoadout();
        Assert.That(a.StatsHub.RefreshCacheIfInputsChanged(), Is.True);
        Assert.That(a.StatsHub.GetMaximumStamina(), Is.EqualTo(130));
    }

    public System.Collections.IEnumerator RuntimeRomaAcrossFrames()
    {
        var a = Actor(true); var d = Roma(); Equip(a, d);
        yield return null; // Allow the real Stamina.Start before spending.
        a.StaminaSystem.Spend(10, "dash");
        yield return null;
        a.StaminaSystem.Spend(6, "slide");
        using (var attack = a.PassiveController.ReserveNextAttack("runtime"))
        {
            Assert.That(attack.Multiplier, Is.EqualTo(1.32f).Within(0.001f));
            attack.Commit();
        }
        var temporary = new PassiveModifierStep(); temporary.modifier.durationSeconds = 0.025f;
        temporary.modifier.modifiers.Add(new PassiveStatModifier { statType = StatType.Armor, value = 9 });
        Equip(a, Definition(new PassiveRecipeRule { eventType = PassiveEventType.Hit, steps = new() { temporary } }));
        Hit(a); Equip(a);
        Assert.That(Modifiers(a).Any(m => m.StatType == StatType.Armor), Is.True);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Modifiers(a), Is.Empty);
        Assert.That(a.PassiveController.RequiresTick, Is.False);
    }

}
