using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RomaPassiveTreeTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly PassiveCompositionTestFixture actors = new();
    readonly List<Object> owned = new();
    PassiveDefinition definition;
    [SetUp] public void Setup() { actors.Setup(); definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(RomaCombatSurgeAuthoring.Path); }
    [TearDown] public void Cleanup()
    { actors.Cleanup(); foreach (var value in owned) if (value != null) Object.DestroyImmediate(value); owned.Clear(); }
    CharacteContext Actor(bool nested = false) => (CharacteContext)typeof(PassiveCompositionTestFixture).GetMethod("Actor", Hidden).Invoke(actors, new object[] { nested });
    PassiveRuntime Equip(CharacteContext actor, params string[] upgrades)
    {
        var snapshot = new SkillUpgradeStatSnapshot();
        foreach (string id in upgrades) snapshot.AddNode(new SkillUpgradeNodeData { grantedUpgradeIds = new() { id } });
        actor.PassiveController.SetRuntimeGrants(new[] { new PassiveGrant(definition, "tree-test", "roma", snapshot) });
        var list = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(list); return list.Single();
    }
    static float Value(PassiveRuntime runtime, string id) { runtime.TryGetVariable(id, out float value, out _); return value; }
    void Start(CharacteContext actor, PassiveRuntime runtime, float ratio = .4f)
    {
        actor.StaminaSystem.Restore(actor.StaminaSystem.Max);
        actor.StaminaSystem.Spend(actor.StaminaSystem.Max * (1 - ratio));
        runtime.WriteVariable("momentum", definition.recipe.variables.Single(v => v.id == "momentum"), 100);
        var shot = new WeaponShotBuildContext { PassiveContext = actor.CombatEventBus.CreateExternalContext(PassiveEventType.ShotFired, attackId: actor.CombatEventBus.CreateAttackId()) };
        using var phase = actor.PassiveController.PrepareWeaponShot(ref shot); phase.Commit();
    }
    static float Damage(CharacteContext actor)
    { var values = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(values); return values.Where(v => v.StatType == StatType.Damage).Sum(v => v.Value); }
    GameObject Enemy()
    { var root = new GameObject("Surge enemy"); owned.Add(root); root.AddComponent<EnemyContext>(); var child = new GameObject("Hit zone"); child.transform.SetParent(root.transform); return child; }
    static void Hit(CharacteContext actor, GameObject target, string id, CombatSourceKind source = CombatSourceKind.Weapon, float applied = 1, string affix = null)
    { actor.CombatEventBus.Publish(actor.CombatEventBus.CreateExternalContext(PassiveEventType.Hit, target: target, attackId: id,
        metadata: new CombatEventMetadata(appliedDamage: applied, sourceKind: source, weaponAffixId: affix))); }
    static void Dash(CharacteContext actor, ulong id) => actor.CombatEventBus.Publish(actor.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
        metadata: new CombatEventMetadata(dashExecutionId: id)));
    WeaponSystem Weapon(CharacteContext actor, int capacity = 10, bool infinite = false)
    {
        var weapon = actor.WeaponSystem;
        if (weapon == null) { weapon = actor.gameObject.AddComponent<WeaponSystem>(); weapon.ctx = actor; WeaponAimTestSource.Bind(weapon, actor.transform); actor.WeaponSystem = weapon; }
        var gun = ScriptableObject.CreateInstance<GunConfig>(); owned.Add(gun);
        gun.maxMagazine = gun.magazine = capacity; gun.maxReserveAmmo = 100; gun.infiniteReserveAmmo = infinite;
        weapon.Equip(gun, WeaponInstanceFactory.CreatePlainInstance(gun)); WeaponAimTestSource.Bind(weapon, weapon.ctx.transform);
        return weapon;
    }

    [Test] public void TreeUnlockSaveRefundAndValidation()
    {
        var tree = definition.upgradeTree;
        Assert.That(tree.nodes.Count, Is.EqualTo(9)); Assert.That(tree.nodes.Sum(n => n.cost), Is.EqualTo(12));
        Assert.That(tree.nodes.Count(n => n.visualScale == 1.5f), Is.EqualTo(3));
        Assert.That(SkillUpgradeTreeValidator.Validate(tree, new SkillDefinitionBase[] { definition }), Is.Empty);
        Assert.That(PassiveRecipeValidator.Validate(definition.recipe), Is.Empty);
        var data = new CharacterProgressData { skillPoints = 12 };
        var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Scripts/CharacterStats/Asosiation/ChaDef.Roma.asset");
        var progress = new ActiveSkillProgressModel(stats, data, 1);
        const string slot = "roma.passive.stamina-momentum", option = "roma.stamina-momentum";
        Assert.That(progress.TryUnlock(slot, option, tree, RomaPassiveTreeAuthoring.Slip, out _), Is.False);
        foreach (var node in tree.nodes) Assert.That(progress.TryUnlock(slot, option, tree, node.nodeId, out string reason), Is.True, reason);
        Assert.That(progress.AvailablePoints, Is.Zero);
        var loaded = JsonUtility.FromJson<CharacterProgressData>(JsonUtility.ToJson(data));
        progress = new ActiveSkillProgressModel(stats, loaded, 1);
        Assert.That(progress.BuildSnapshot(slot, option, tree, out _).UpgradeIds.Count, Is.EqualTo(9));
        Assert.That(progress.ResetTree(slot, option, tree, out int refund), Is.True);
        Assert.That(refund, Is.EqualTo(12)); Assert.That(progress.AvailablePoints, Is.EqualTo(12));
    }

    [Test] public void MinorNodesApplyAndResetIndependently()
    {
        foreach (bool nested in new[] { false, true })
        {
            var actor = Actor(nested); var runtime = Equip(actor);
            actor.StaminaSystem.Spend(30); Dash(actor, 1); Assert.That(actor.StaminaSystem.Current, Is.EqualTo(80));
            runtime = Equip(actor, RomaPassiveTreeAuthoring.Recovery);
            actor.StaminaSystem.Spend(30); Dash(actor, 2); Assert.That(actor.StaminaSystem.Current, Is.EqualTo(65));
            Equip(actor, RomaPassiveTreeAuthoring.Reserve); Assert.That(actor.StatsHub.GetMaximumStamina(), Is.EqualTo(110));
            Equip(actor); Assert.That(actor.StatsHub.GetMaximumStamina(), Is.EqualTo(100));
            var bonus = new PassiveActionDefinition { durationSeconds = 100, modifiers = new() { new() { statType = StatType.StaminaRegenRate, value = 1 } } };
            actor.PassiveController.TryApplyRuntimeModifier(bonus, "regen", 0);
            Assert.That(actor.StatsHub.GetMaximumStamina(), Is.EqualTo(104));
            Equip(actor, RomaPassiveTreeAuthoring.Deep); Assert.That(actor.StatsHub.GetMaximumStamina(), Is.EqualTo(105));
            runtime = Equip(actor, RomaPassiveTreeAuthoring.Hands); Start(actor, runtime);
            Assert.That(actor.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1.4f).Within(.001));
            Assert.That(actor.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.8f).Within(.001));
            runtime = Equip(actor, RomaPassiveTreeAuthoring.Aim); Start(actor, runtime);
            Assert.That(actor.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1.3f).Within(.001));
            Assert.That(actor.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.86f).Within(.001));
            runtime = Equip(actor, RomaPassiveTreeAuthoring.Power); Start(actor, runtime);
            Assert.That(Damage(actor), Is.EqualTo(36).Within(.001));
            Equip(actor); Assert.That(Damage(actor), Is.EqualTo(30).Within(.001));
        }
    }

    [Test] public void SlipReloadConservesAmmoDeduplicatesAndCapsSuccessfulTransfers()
    {
        var actor = Actor(); var weapon = Weapon(actor); var runtime = Equip(actor, RomaPassiveTreeAuthoring.Slip);
        Start(actor, runtime); weapon.magazine = 10; weapon.reserveAmmo = 20;
        Dash(actor, 1); weapon.magazine = 0; Dash(actor, 1);
        Assert.That(weapon.magazine, Is.Zero); Assert.That(Value(runtime, "slip-uses"), Is.Zero);
        weapon.reserveAmmo = 0; Dash(actor, 2); weapon.reserveAmmo = 2; Dash(actor, 2);
        Assert.That(weapon.magazine, Is.Zero);
        Dash(actor, 3); Assert.That(weapon.magazine, Is.EqualTo(2)); Assert.That(weapon.reserveAmmo, Is.Zero);
        weapon.reserveAmmo = 20; Dash(actor, 1); Assert.That(weapon.magazine, Is.EqualTo(2), "out-of-order duplicate Dash");
        Dash(actor, 4); Assert.That(weapon.magazine, Is.EqualTo(5)); Assert.That(weapon.reserveAmmo, Is.EqualTo(17));
        Dash(actor, 5); Assert.That(weapon.magazine, Is.EqualTo(5)); Assert.That(Value(runtime, "slip-uses"), Is.EqualTo(2));
        actor.PassiveController.Tick(5); Start(actor, runtime);
        weapon = Weapon(actor, 7, true); weapon.magazine = 6;
        int reloads = 0; actor.CombatEventBus.EventPublished += e => { if (e.Type == PassiveEventType.Reload) reloads++; };
        Dash(actor, 6); Assert.That(weapon.magazine, Is.EqualTo(7)); Assert.That(reloads, Is.Zero);
        Assert.That(weapon.IsReloading, Is.False);
    }

    [Test] public void PressureCountsNormalDamagingShotsOnceAcrossTargetsAndCapsAtEightSeconds()
    {
        var actor = Actor(); var enemy = Enemy(); var other = Enemy(); var runtime = Equip(actor, RomaPassiveTreeAuthoring.Pressure);
        Start(actor, runtime);
        Hit(actor, enemy, "zero", applied: 0); Hit(actor, enemy, "skill", CombatSourceKind.Skill);
        Hit(actor, enemy, "melee", CombatSourceKind.Melee); Hit(actor, enemy, "status", CombatSourceKind.Status);
        Hit(actor, enemy, "extra", affix: "echo"); Hit(actor, actor.gameObject, "friendly");
        Assert.That(Value(runtime, "pressure-hits"), Is.Zero);
        for (int i = 1; i <= 20; i++)
        {
            Hit(actor, enemy, "shot-" + i); Hit(actor, other, "shot-" + i); Hit(actor, enemy, "shot-1");
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out float duration), Is.EqualTo(5 + Mathf.Min(3, i / 5)));
            Assert.That(duration, Is.EqualTo(5 + Mathf.Min(3, i / 5)));
        }
        Assert.That(Value(runtime, "pressure-hits"), Is.EqualTo(15));
        actor.PassiveController.Tick(8); Hit(actor, enemy, "late");
        Assert.That(Value(runtime, "pressure-hits"), Is.Zero); Assert.That(Value(runtime, "surge-active"), Is.Zero);
        Start(actor, runtime); Hit(actor, enemy, "shot-1"); Assert.That(Value(runtime, "pressure-hits"), Is.EqualTo(1));
    }

    [Test] public void RedlineUsesOpeningFloorWithoutStackingAndFollowsExtendedDeadline()
    {
        var actor = Actor(); var runtime = Equip(actor, RomaPassiveTreeAuthoring.Redline, RomaPassiveTreeAuthoring.Power, RomaPassiveTreeAuthoring.Pressure);
        Start(actor, runtime); Assert.That(Value(runtime, "redline-floor"), Is.EqualTo(36).Within(.001));
        actor.StaminaSystem.Restore(100); Assert.That(Damage(actor), Is.EqualTo(36).Within(.001));
        actor.StaminaSystem.Spend(90); Assert.That(Damage(actor), Is.EqualTo(54).Within(.001));
        actor.StaminaSystem.Restore(100); Assert.That(Damage(actor), Is.EqualTo(36).Within(.001));
        var enemy = Enemy(); for (int i = 0; i < 5; i++) Hit(actor, enemy, "extend-" + i);
        actor.PassiveController.Tick(5); Assert.That(Damage(actor), Is.EqualTo(36).Within(.001));
        actor.PassiveController.Tick(1); Assert.That(Damage(actor), Is.Zero);
        Start(actor, runtime, .7f); Assert.That(Value(runtime, "redline-floor"), Is.Zero);
        actor.StaminaSystem.Spend(20); Assert.That(Damage(actor), Is.EqualTo(30).Within(.001));
        actor.StaminaSystem.Restore(100); Assert.That(Damage(actor), Is.Zero);
    }

    [Test] public void RefreshKeepsRoundButChangedUpgradesCancelItAndPreserveIdleMomentum()
    {
        var actor = Actor(); var runtime = Equip(actor, RomaPassiveTreeAuthoring.Redline);
        Start(actor, runtime); actor.PassiveController.Tick(2);
        Equip(actor, RomaPassiveTreeAuthoring.Redline);
        Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out _), Is.EqualTo(3));
        Equip(actor, RomaPassiveTreeAuthoring.Redline, RomaPassiveTreeAuthoring.Recovery);
        Assert.That(Value(runtime, "surge-active"), Is.Zero); Assert.That(Value(runtime, "redline-floor"), Is.Zero);
        actor.StaminaSystem.Restore(100); actor.StaminaSystem.Spend(25); Equip(actor);
        Assert.That(Value(runtime, "momentum"), Is.EqualTo(25));
        runtime = Equip(actor, RomaPassiveTreeAuthoring.Redline); Start(actor, runtime);
        actor.PassiveController.SetRuntimeGrants(new PassiveGrant[0]); actor.StaminaSystem.Restore(100);
        Assert.That(Damage(actor), Is.Zero);
        runtime = Equip(actor, RomaPassiveTreeAuthoring.Redline); Assert.That(Value(runtime, "surge-active"), Is.Zero);
    }
}
