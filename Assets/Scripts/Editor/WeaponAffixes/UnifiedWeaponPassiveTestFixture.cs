using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public sealed class UnifiedWeaponPassiveTestFixture
{
    PassiveCompositionTestFixture fixture;
    readonly List<UnityEngine.Object> owned = new();
    [SetUp] public void Setup() { fixture = new(); fixture.Setup(); }
    [TearDown] public void Cleanup()
    {
        fixture.Cleanup();
        foreach (var obj in owned) if (obj != null)
        {
            if (obj is GameObject go && go.TryGetComponent<CharacteContext>(out var ctx)) CharacterContextRegistry.Unregister(ctx);
            UnityEngine.Object.DestroyImmediate(obj);
        }
        owned.Clear();
    }
    CharacteContext Actor() => (CharacteContext)typeof(PassiveCompositionTestFixture).GetMethod("Actor", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fixture, new object[] { false });
    PassiveGrant Grant(WeaponAffixRuntimeKind kind, float roll = 20, WeaponInstanceData weapon = null)
    {
        var behavior = ScriptableObject.CreateInstance<ConfiguredWeaponAffixBehavior>(); owned.Add(behavior);
        behavior.kind = kind; behavior.statType = StatType.Damage; behavior.duration = 4; behavior.threshold = 2;
        var recipe = WeaponPassiveRecipeMigration.Build(behavior); owned.Add(recipe);
        Assert.That(PassiveRecipeValidator.Validate(recipe), Is.Empty);
        var definition = ScriptableObject.CreateInstance<ComposedPassiveDef>(); owned.Add(definition); definition.recipe = recipe;
        return new PassiveGrant(definition, "weapon", "grant", options: new PassiveGrantOptions("weapon-1",
            weapon != null ? new WeaponPassiveStateStore(weapon, "grant", "old") : null,
            new Dictionary<string, float> { ["roll"] = roll }, false));
    }
    static PassiveRuntime Runtime(CharacteContext actor)
    { var list = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(list); return list.Single(); }
    static void Reload(CharacteContext actor) => actor.CombatEventBus.Publish(actor.CombatEventBus.CreateExternalContext(
        PassiveEventType.Reload, metadata: new CombatEventMetadata(weaponInstanceId: "weapon-1")));
    static WeaponShotBuildContext Shot(CharacteContext actor) => new()
    {
        Damage = 100, AmmoConsumed = true, WeaponInstanceId = "weapon-1",
        PassiveContext = actor.CombatEventBus.CreateExternalContext(PassiveEventType.ShotFired,
            attackId: actor.CombatEventBus.CreateAttackId(), metadata: new CombatEventMetadata(weaponInstanceId: "weapon-1", ammoConsumed: true))
    };
    [Test] public void StaticDamageUsesFrozenRollWithoutCounterSubscriptionOrTimer()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.StaticStat) });
        var modifiers = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(modifiers);
        Assert.That(modifiers.Single().Value, Is.EqualTo(20)); Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero);
        Assert.That(actor.PassiveController.HasCombatSubscription || actor.PassiveController.RequiresTick, Is.False);
    }
    [Test] public void ReloadDamageExpiresAndUnequipRemovesImmediately()
    {
        var actor = Actor(); var grant = Grant(WeaponAffixRuntimeKind.ReloadBuff);
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); Reload(actor);
        var modifiers = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(modifiers);
        Assert.That(modifiers.Single().Value, Is.EqualTo(20));
        actor.PassiveController.Tick(4.1f); modifiers.Clear(); actor.PassiveController.AppendStatModifiers(modifiers);
        Assert.That(modifiers, Is.Empty); Assert.That(actor.PassiveController.RequiresTick, Is.False);
        Reload(actor); actor.PassiveController.SetRuntimeGrants(Array.Empty<PassiveGrant>());
        modifiers.Clear(); actor.PassiveController.AppendStatModifiers(modifiers); Assert.That(modifiers, Is.Empty);
    }
    [Test] public void FreshChamberCancellationRestoresChargesCommitConsumesExactlyOnce()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.FreshChamber) }); Reload(actor);
        var shot = Shot(actor);
        using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) Assert.That(shot.Damage, Is.EqualTo(120).Within(.001));
        Assert.That(Runtime(actor).TryGetVariable("charges", out var value, out _), Is.True); Assert.That(value, Is.EqualTo(3));
        shot = Shot(actor); using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) { phase.Commit(); phase.Commit(); }
        Runtime(actor).TryGetVariable("charges", out value, out _); Assert.That(value, Is.EqualTo(2));
    }
    [Test] public void EchoProgressChangesOnlyOnCommitAndPersistsWithWeapon()
    {
        var actor = Actor(); var weapon = new WeaponInstanceData(); var grant = Grant(WeaponAffixRuntimeKind.EchoChamber, weapon: weapon);
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); var shot = Shot(actor);
        actor.PassiveController.PrepareWeaponShot(ref shot).Dispose(); Assert.That(weapon.affixRuntimeStates, Is.Empty);
        shot = Shot(actor); using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) phase.Commit();
        Runtime(actor).TryGetVariable("progress", out float value, out _); Assert.That(value, Is.EqualTo(1));
        actor.PassiveController.SetRuntimeGrants(Array.Empty<PassiveGrant>()); actor.PassiveController.SetRuntimeGrants(new[] { grant });
        Runtime(actor).TryGetVariable("progress", out value, out _); Assert.That(value, Is.EqualTo(1));
        shot = Shot(actor); using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) phase.Commit();
        Runtime(actor).TryGetVariable("progress", out value, out _); Assert.That(value, Is.Zero);
    }

    static void Fact(CharacteContext actor, PassiveEventType type, CharacteContext target = null,
        CharacterHitZone zone = CharacterHitZone.None, bool chainReady = false, string attack = null, string weapon = "weapon-1", string generated = null)
    {
        actor.CombatEventBus.Publish(actor.CombatEventBus.CreateExternalContext(type,
            target: target != null ? target.gameObject : null, attackId: attack ?? actor.CombatEventBus.CreateAttackId(),
            metadata: new CombatEventMetadata(appliedDamage: 10, resolvedDamage: 50, healthBeforeHit: 10, maxHealth: 100,
                weaponInstanceId: weapon, hitZone: zone, enteredChainReady: chainReady, ammoAfter: 1, maxMagazine: 20, weaponAffixId: generated)));
    }
    static float Value(CharacteContext actor, string id)
    { Assert.That(Runtime(actor).TryGetVariable(id, out float value, out _), Is.True); return value; }
    static List<RuntimeStatModifier> Modifiers(CharacteContext actor)
    { var list = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(list); return list; }
    [Test] public void AllTwentySevenAssetsUseRecipesAndPreserveRollMetadata()
    {
        var db = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
        Assert.That(db.Affixes.Count, Is.EqualTo(27));
        foreach (var definition in db.Affixes)
        {
            Assert.That(definition.passiveDefinition, Is.Not.Null, definition.name);
            Assert.That(PassiveRecipeValidator.Validate(definition.passiveDefinition.recipe), Is.Empty, definition.name);
            Assert.That(definition.rollSpec.minimum, Is.EqualTo(definition.rootBehavior.RollSpec.minimum));
            Assert.That(definition.rollSpec.maximum, Is.EqualTo(definition.rootBehavior.RollSpec.maximum));
            Assert.That(definition.tooltip.effect, Is.EqualTo(definition.rootBehavior.Tooltip.effect));
        }
        Assert.That(typeof(WeaponAffixRuntimeController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
    }
    [Test] public void AllStaticPreviewsMatchRuntimeWithoutChangingSaveOrCreatingState()
    {
        var actor = Actor();
        var db = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
        foreach (var definition in db.Affixes)
        {
            var weapon = new WeaponInstanceData { instanceId = "weapon-1", mainAffix = new() { affixId = definition.affixId,
                hasPrimaryValue = true, primaryValue = definition.rollSpec.maximum } };
            string before = JsonUtility.ToJson(weapon);
            var preview = new List<RuntimeStatModifier>(); WeaponStatPreview.AppendStaticAffixModifiers(preview, weapon, db);
            Assert.That(JsonUtility.ToJson(weapon), Is.EqualTo(before), definition.name);
            var grant = new PassiveGrant(definition.passiveDefinition, "weapon", "grant", options: new PassiveGrantOptions("weapon-1",
                parameters: new Dictionary<string, float> { ["roll"] = weapon.mainAffix.primaryValue }));
            actor.PassiveController.SetRuntimeGrants(new[] { grant }); var actual = Modifiers(actor);
            Assert.That(actual.Count, Is.EqualTo(preview.Count), definition.name);
            for (int i = 0; i < actual.Count; i++) Assert.That(actual[i].Value, Is.EqualTo(preview[i].Value), definition.name);
            Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero, definition.name);
        }
    }
    [Test] public void HeadHunterCountsAttackOnceCapsAndBodyHitResets()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.HeadHunter, 3) });
        Fact(actor, PassiveEventType.Hit, zone: CharacterHitZone.Head, attack: "same");
        Fact(actor, PassiveEventType.Hit, zone: CharacterHitZone.Head, attack: "same");
        Assert.That(Value(actor, "stacks"), Is.EqualTo(1));
        for (int i = 0; i < 7; i++) Fact(actor, PassiveEventType.Hit, zone: CharacterHitZone.Head);
        Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(15));
        Fact(actor, PassiveEventType.Hit); Assert.That(Modifiers(actor), Is.Empty);
    }
    [Test] public void HotStreakCapsAndKillClipExpires()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.HotStreak, 3) });
        for (int i = 0; i < 8; i++) Fact(actor, PassiveEventType.Kill);
        Assert.That(Value(actor, "stacks"), Is.EqualTo(3)); Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(-9));
        actor.PassiveController.Tick(4.1f); Assert.That(Modifiers(actor), Is.Empty);
        actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.KillClip) }); Fact(actor, PassiveEventType.Kill);
        Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(20)); actor.PassiveController.Tick(4.1f); Assert.That(Modifiers(actor), Is.Empty);
    }
    [Test] public void TargetModifiersUseActorIdentityAndResetOnNewTargetAndLifetime()
    {
        var actor = Actor(); var first = Actor(); var second = Actor();
        foreach (var kind in new[] { WeaponAffixRuntimeKind.PressurePoint, WeaponAffixRuntimeKind.MarkedQuarry })
        {
            actor.PassiveController.SetRuntimeGrants(new[] { Grant(kind, 10) });
            for (int i = 0; i < 5; i++) Fact(actor, PassiveEventType.Hit, first);
            var damage = new WeaponDamageBuildContext { WeaponInstanceId = "weapon-1", Target = first.gameObject, Damage = 100, StaggerPower = 10 };
            actor.PassiveController.ModifyWeaponDamage(ref damage);
            Assert.That(kind == WeaponAffixRuntimeKind.PressurePoint ? damage.StaggerPower : damage.Damage, Is.EqualTo(kind == WeaponAffixRuntimeKind.PressurePoint ? 15 : 110).Within(.001));
            Fact(actor, PassiveEventType.Hit, second); Assert.That(Value(actor, "stacks"), Is.EqualTo(1));
            actor.PassiveController.Tick(4.1f); Assert.That(Value(actor, "stacks"), Is.Zero);
        }
    }
    [Test] public void BrokenGuardCancellationAfterExpiryCannotResurrectCharge()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.BrokenGuard, .3f) });
        Fact(actor, PassiveEventType.Hit, chainReady: true); var shot = Shot(actor); shot.CritMultiplier = 1.5f;
        using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot))
        {
            Assert.That(shot.CritRate, Is.EqualTo(100)); Assert.That(shot.CritMultiplier, Is.EqualTo(1.8f).Within(.001));
            actor.PassiveController.Tick(5.1f);
        }
        Assert.That(Value(actor, "charges"), Is.Zero);
    }
    [Test] public void LastRoundSnapshotsImpactBeforeItsDirectDamageMultiplier()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.LastRound, 40) });
        var shot = Shot(actor); shot.PassiveContext = actor.CombatEventBus.CreateExternalContext(PassiveEventType.ShotFired,
            metadata: new CombatEventMetadata(weaponInstanceId: "weapon-1", ammoConsumed: true, isLastRound: true));
        using var phase = actor.PassiveController.PrepareWeaponShot(ref shot);
        Assert.That(shot.Damage, Is.EqualTo(125)); Assert.That(shot.ImpactPayload.Damage, Is.EqualTo(40));
        phase.Commit(); actor.PassiveController.SetRuntimeGrants(Array.Empty<PassiveGrant>());
        Assert.That(shot.ImpactPayload.Damage, Is.EqualTo(40));
    }
    [Test] public void UnrelatedWeaponsAndGeneratedFactsCannotActivateWeaponGrants()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.HotStreak) });
        Fact(actor, PassiveEventType.Kill, weapon: "other"); Fact(actor, PassiveEventType.Kill, generated: "echo");
        Assert.That(Value(actor, "stacks"), Is.Zero); Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero);
    }
    [Test] public void TwoGrantsOfSameDefinitionHaveIndependentCharges()
    {
        var actor = Actor(); var first = Grant(WeaponAffixRuntimeKind.FreshChamber);
        actor.PassiveController.SetRuntimeGrants(new[] { first, new PassiveGrant(first.Definition, first.SourceId, "second", options: first.Options) });
        Reload(actor); var shot = Shot(actor); using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) phase.Commit();
        Assert.That(shot.Damage, Is.EqualTo(144).Within(.001));
        var runtimes = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(runtimes);
        foreach (var runtime in runtimes) { runtime.TryGetVariable("charges", out float count, out _); Assert.That(count, Is.EqualTo(2)); }
    }
    [Test] public void LegacySaveMigratesProgressOnceAndDeepClonePreservesGrantIdentity()
    {
        var weapon = new WeaponInstanceData { mainAffix = new() { affixId = "echo" }, subAffixes = new() { new() { affixId = "echo" } } };
        weapon.GetOrCreateAffixState("echo").entries.Add(new() { key = "progress", intValue = 3 });
        WeaponAffixRuntimeController.NormalizeGrantIds(weapon);
        weapon.GetOrCreateAffixState(null).entries.Add(new() { key = "progress", intValue = 99 });
        var main = new WeaponPassiveStateStore(weapon, weapon.mainAffix.grantId, null);
        var other = new WeaponPassiveStateStore(weapon, weapon.subAffixes[0].grantId, null);
        Assert.That(main.TryRead("progress", out float value), Is.True); Assert.That(value, Is.EqualTo(3));
        Assert.That(other.TryRead("progress", out _), Is.False);
        main.Write("progress", 4); var clone = weapon.DeepClone();
        Assert.That(clone.mainAffix.grantId, Is.EqualTo(weapon.mainAffix.grantId));
        new WeaponPassiveStateStore(clone, clone.mainAffix.grantId, null).Write("progress", 9);
        main.TryRead("progress", out value); Assert.That(value, Is.EqualTo(4));
    }
    [Test] public void RepeatedRefreshPreservesChargeAndRollSnapshot()
    {
        var actor = Actor(); var grant = Grant(WeaponAffixRuntimeKind.FreshChamber);
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); Reload(actor);
        var runtime = Runtime(actor); actor.PassiveController.RefreshPassiveLoadout();
        Assert.That(Runtime(actor), Is.SameAs(runtime)); Assert.That(Value(actor, "charges"), Is.EqualTo(3));
    }
    WeaponSystem Weapon(CharacteContext actor, int ammo = 1)
    {
        var weapon = actor.gameObject.AddComponent<WeaponSystem>(); actor.WeaponSystem = weapon; weapon.ctx = actor; WeaponAimTestSource.Bind(weapon, actor.transform);
        var gun = ScriptableObject.CreateInstance<GunConfig>(); owned.Add(gun); gun.magazine = gun.maxMagazine = 20;
        typeof(WeaponSystem).GetField("currentWeapon", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, gun);
        typeof(WeaponSystem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
        weapon.magazine = ammo; weapon.maxMagazine = 20; actor.StatsHub.MarkDirty(); return weapon;
    }
    [Test] public void AmmoEffectsReportActualAmountAndConservationRetainsProgressWhenFull()
    {
        var actor = Actor(); var weapon = Weapon(actor, 20);
        var grant = Grant(WeaponAffixRuntimeKind.ConservationRound, 2, new WeaponInstanceData());
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); Fact(actor, PassiveEventType.Hit); Fact(actor, PassiveEventType.Hit);
        Assert.That(Value(actor, "progress"), Is.EqualTo(2)); weapon.magazine = 19;
        Fact(actor, PassiveEventType.Hit); Assert.That(weapon.magazine, Is.EqualTo(20)); Assert.That(Value(actor, "progress"), Is.Zero);
        weapon.magazine = 19;
        Assert.That(weapon.RestoreMagazineFromPassive(8), Is.EqualTo(1)); Assert.That(weapon.RestoreMagazineFromPassive(8), Is.Zero);
        actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.ExecutionFeed, 20) }); weapon.magazine = 1;
        Fact(actor, PassiveEventType.Kill); Assert.That(weapon.magazine, Is.EqualTo(5));
        actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.BloodMagazine, 20) }); weapon.magazine = 1;
        Fact(actor, PassiveEventType.Kill); Assert.That(weapon.magazine, Is.EqualTo(5));
        Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(15));
    }
    [Test] public void ParameterBindingsUseFrozenGrantValuesInRuntimeAndReadOnlyPreview()
    {
        var actor = Actor(); var grant = Grant(WeaponAffixRuntimeKind.StaticStat, 9);
        var fragment = ScriptableObject.CreateInstance<PassiveFragment>(); owned.Add(fragment);
        fragment.parameters.Add(new() { id = "amount", number = 2 });
        fragment.steps.Add(new PassiveDynamicModifierStep { value = new() { source = PassiveValueSource.Parameter, reference = "amount" } });
        grant.Definition.recipe.rules[0].steps = new() { new PassiveFragmentStep { fragment = fragment,
            parameterBindings = new() { new() { local = "amount", parent = "roll" } } } };
        Assert.That(PassiveRecipeValidator.Validate(grant.Definition.recipe), Is.Empty);
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); Assert.That(Modifiers(actor).Single().Value, Is.EqualTo(9));
        var preview = new List<RuntimeStatModifier>(); PassiveStatPreview.Append(grant, preview); Assert.That(preview.Single().Value, Is.EqualTo(9));
        Assert.That(fragment.parameters[0].number, Is.EqualTo(2));
    }
    [Test] public void OverkillUsesRegistryAndNestedHealthWithoutCollidersAndReportsActualDamage()
    {
        var actor = Actor(); actor.transform.position = Vector3.one * 10000;
        IdentityProbeContext Enemy(float offset, float health)
        {
            var root = new GameObject("Passive area target"); owned.Add(root);
            var ctx = root.AddComponent<IdentityProbeContext>(); ctx.SetIdentity(AITargetIdentity.Enemy);
            root.transform.position = actor.transform.position + Vector3.right * offset;
            var child = new GameObject("Nested health"); child.transform.SetParent(root.transform, false);
            ctx.HealthSystem = child.AddComponent<HealthSystem>(); ctx.HealthSystem.CTX = ctx;
            ctx.HealthSystem.maximumHealth = 100; ctx.HealthSystem.currentHealth = health;
            CharacterContextRegistry.Register(ctx); return ctx;
        }
        var victim = Enemy(0, 0); var neighbor = Enemy(1, 100); var distant = Enemy(8, 100);
        actor.PassiveController.SetRuntimeGrants(new[] { Grant(WeaponAffixRuntimeKind.Overkill, 20) });
        PassiveResult result = default; Runtime(actor).EffectResolved += (step, actual, evt) => { if (step is PassiveAreaDamageStep) result = actual; };
        Fact(actor, PassiveEventType.Kill, victim);
        Assert.That(neighbor.HealthSystem.currentHealth, Is.EqualTo(92).Within(.001)); Assert.That(distant.HealthSystem.currentHealth, Is.EqualTo(100));
        Assert.That(result.Amount, Is.EqualTo(8)); Assert.That(result.SuccessfulTargets, Has.Count.EqualTo(1));
        Assert.That(result.Target, Is.SameAs(neighbor));
    }
    [Test] public void FeedbackForReservedChargesIsPublishedOnlyAtCommit()
    {
        var actor = Actor(); var grant = Grant(WeaponAffixRuntimeKind.FreshChamber);
        var signals = new List<string>();
        grant = new PassiveGrant(grant.Definition, grant.SourceId, grant.GrantId, options: new PassiveGrantOptions("weapon-1",
            parameters: new Dictionary<string, float> { ["roll"] = 20 }, feedback: (signal, value) => signals.Add(signal + ":" + value)));
        actor.PassiveController.SetRuntimeGrants(new[] { grant }); Reload(actor);
        Assert.That(signals, Is.EqualTo(new[] { "ChargeReady:3" }));
        var shot = Shot(actor); actor.PassiveController.PrepareWeaponShot(ref shot).Dispose(); Assert.That(signals.Count, Is.EqualTo(1));
        shot = Shot(actor); using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) phase.Commit();
        Assert.That(signals.Last(), Is.EqualTo("Consumed:2"));
    }
    [Test] public void WeaponAccessoryAndRomaGrantsCoexistWithRootAndNestedWeaponModules()
    {
        foreach (bool nested in new[] { false, true })
        {
            var actor = Actor(); var body = actor.gameObject;
            if (nested) { body = new GameObject("Nested weapon modules"); body.transform.SetParent(actor.transform, false); }
            var weapon = body.AddComponent<WeaponSystem>(); weapon.ctx = actor; WeaponAimTestSource.Bind(weapon, actor.transform); actor.WeaponSystem = weapon;
            var gun = ScriptableObject.CreateInstance<GunConfig>(); owned.Add(gun); gun.magazine = gun.maxMagazine = 20;
            typeof(WeaponSystem).GetField("currentWeapon", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, gun);
            typeof(WeaponSystem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
            var db = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
            var fresh = db.Affixes.Single(d => d.rootBehavior is ConfiguredWeaponAffixBehavior b && b.kind == WeaponAffixRuntimeKind.FreshChamber);
            var instance = new WeaponInstanceData { instanceId = "weapon-1", mainAffix = new() { affixId = fresh.affixId, hasPrimaryValue = true, primaryValue = 20 } };
            typeof(WeaponSystem).GetField("currentWeaponInstance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, instance);
            var adapter = weapon.GetComponent<WeaponAffixRuntimeController>();
            typeof(WeaponAffixRuntimeController).GetField("affixDatabase", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, db);
            var roma = AssetDatabase.LoadAssetAtPath<PassiveDefinition>("Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset");
            actor.PassiveController.SetRuntimePassives(new[] { roma }); adapter.NotifyWeaponEquipped();
            var accessory = actor.gameObject.AddComponent<AccessoryLoadout>(); actor.AccessoryLoadout = accessory;
            accessory.SetPlayerInventorySaveParticipation(false);
            var definition = ScriptableObject.CreateInstance<AccessoryDefinition>(); owned.Add(definition);
            definition.itemId = "test-accessory-" + Guid.NewGuid().ToString("N");
            var staticGrant = Grant(WeaponAffixRuntimeKind.StaticStat, 9);
            staticGrant.Definition.recipe.parameters[0].number = 9; definition.passives.Add(staticGrant.Definition);
            Assert.That(accessory.TryEquip(definition, new AccessoryInstanceData { instanceId = Guid.NewGuid().ToString("N"), accessoryId = definition.itemId }, 0), Is.True);
            Assert.That(actor.PassiveController.RuntimeCount, Is.EqualTo(3), "root/nested=" + nested);
            Reload(actor); actor.StaminaSystem.Spend(10);
            var runtimes = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(runtimes);
            var romaRuntime = runtimes.Single(r => r.Grant.Definition == roma);
            romaRuntime.TryGetVariable("momentum", out float momentum, out _); Assert.That(momentum, Is.EqualTo(10));
            Assert.That(Modifiers(actor).Any(m => Mathf.Approximately(m.Value, 9)), Is.True);
            typeof(WeaponSystem).GetField("currentWeaponInstance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, null);
            adapter.NotifyWeaponEquipped(); Assert.That(actor.PassiveController.RuntimeCount, Is.EqualTo(2));
            Assert.That(accessory.UnequipSlot(0), Is.True); Assert.That(actor.PassiveController.RuntimeCount, Is.EqualTo(1));
            Assert.That(actor.PassiveController.ContainsRuntime(romaRuntime), Is.True);
        }
    }
    public static string MeasureIdlePreparation()
    {
        var test = new UnifiedWeaponPassiveTestFixture(); test.Setup();
        try
        {
            var actor = test.Actor(); var shot = Shot(actor);
            long Measure()
            {
                for (int i = 0; i < 32; i++) actor.PassiveController.PrepareWeaponShot(ref shot);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) actor.PassiveController.PrepareWeaponShot(ref shot);
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
            long empty = Measure(); actor.PassiveController.SetRuntimeGrants(new[] { test.Grant(WeaponAffixRuntimeKind.StaticStat) });
            long staticBytes = Measure();
            Assert.That(empty, Is.Zero); Assert.That(staticBytes, Is.Zero);
            return "10000 warmed preparation probes: empty=" + empty + " bytes; static=" + staticBytes + " bytes; state=" + actor.PassiveController.RuntimeStateCount + "; ticker=" + actor.PassiveController.RequiresTick;
        }
        finally { test.Cleanup(); }
    }
    [Test] public void LegacyWeaponActorCreatesSharedModulesOnlyWhenRequired()
    {
        var actor = Actor(); var weapon = Weapon(actor);
        UnityEngine.Object.DestroyImmediate(actor.PassiveController); actor.PassiveController = null;
        UnityEngine.Object.DestroyImmediate(actor.CombatEventBus); actor.CombatEventBus = null;
        var adapter = weapon.GetComponent<WeaponAffixRuntimeController>();
        var db = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
        typeof(WeaponAffixRuntimeController).GetField("affixDatabase", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, db);
        var instance = new WeaponInstanceData { instanceId = "weapon-1" };
        typeof(WeaponSystem).GetField("currentWeaponInstance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, instance);
        adapter.NotifyWeaponEquipped(); Assert.That(actor.PassiveController, Is.Null); Assert.That(actor.CombatEventBus, Is.Null);
        var stat = db.Affixes.Single(d => d.affixId == "weapon.sub.damage.v1");
        instance.mainAffix = new() { affixId = stat.affixId, hasPrimaryValue = true, primaryValue = 5 };
        adapter.NotifyWeaponEquipped(); Assert.That(actor.PassiveController.RuntimeCount, Is.EqualTo(1)); Assert.That(actor.CombatEventBus, Is.Null);
        var fresh = db.Affixes.Single(d => d.rootBehavior is ConfiguredWeaponAffixBehavior b && b.kind == WeaponAffixRuntimeKind.FreshChamber);
        instance.mainAffix = new() { affixId = fresh.affixId, hasPrimaryValue = true, primaryValue = 20 };
        adapter.NotifyWeaponEquipped(); Assert.That(actor.CombatEventBus, Is.Not.Null);
        Assert.That(actor.GetComponentsInChildren<PassiveController>(true).Length, Is.EqualTo(1));
        Assert.That(typeof(WeaponSystem).GetField("combatEventBus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon), Is.SameAs(actor.CombatEventBus));
        Reload(actor); Assert.That(Value(actor, "charges"), Is.EqualTo(3));
        instance.mainAffix = null; adapter.NotifyWeaponEquipped(); Assert.That(actor.PassiveController.RuntimeCount, Is.Zero);
        Assert.That(actor.PassiveController.HasCombatSubscription || actor.PassiveController.RequiresTick, Is.False);
    }
}
