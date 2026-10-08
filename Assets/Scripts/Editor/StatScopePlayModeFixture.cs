using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StatScopePlayModeFixture
{
    readonly PlayerSlideTestFixture player = new();
    readonly List<Object> owned = new();
    readonly List<Projectile> projectiles = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static ProjectileContext Context(Projectile projectile) => (ProjectileContext)typeof(Projectile).GetField("_ctx", Hidden).GetValue(projectile);
    public IEnumerator Run()
    {
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden).Invoke(player, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(player);
        var weapon = ctx.WeaponSystem;
        var gun = Own(Object.Instantiate(weapon.CurrentWeapon));
        gun.damage = 80; gun.autoloader = false; gun.fireRate = .01f; gun.magazine = gun.maxMagazine = 20;
        gun.critRate = 0; gun.critMultiplier = 1; gun.reloadTime = .2f; gun.magazineReload = true; gun.reloadPerBullet = false;
        ctx.baseStats = Own(Object.Instantiate(ctx.baseStats));
        ctx.baseStats.DamageScaling = 0; ctx.baseStats.Damage = 20; ctx.baseStats.critRate = 0; ctx.baseStats.CritrateScaling = 0;
        var curve = Own(ScriptableObject.CreateInstance<WeaponUpgradeCurve>());
        curve.statBonuses = new() { new() { statType = StatType.Damage, operation = ModifierOp.AddPercent, valuePerStep = 20, scope = StatModifierScope.WeaponOnly } };
        curve.milestones = new() { new() { milestoneId = "scope-reload", requiredLevel = 1, effects = new() {
            new() { effectId = "damage", effectType = WeaponUpgradeEffectType.TimedBuffOnReload, statType = StatType.Damage,
                modifierOp = ModifierOp.AddPercent, value = 50, scope = StatModifierScope.WeaponOnly, buffDurationSeconds = .25f, procChance = 1 } } } };
        gun.upgradeCurve = curve;
        var first = WeaponInstanceFactory.CreatePlainInstance(gun); first.upgradeLevel = 1;
        var second = WeaponInstanceFactory.CreatePlainInstance(gun);
        weapon.Equip(gun, first);
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(116).Within(.01));
        Assert.That(ctx.StatsHub.GetSkillBaseDamage(), Is.EqualTo(20).Within(.01));

        var loadout = ctx.AccessoryLoadout;
        Assert.That(loadout, Is.Not.Null);
        loadout.SetPlayerInventorySaveParticipation(false);
        for (int i = 0; i < loadout.SlotCount; i++) loadout.UnequipSlot(i);
        var accessory = Own(ScriptableObject.CreateInstance<AccessoryDefinition>());
        accessory.itemId = "test-stat-scope-accessory";
        accessory.statModifiers = new() { new() { statType = StatType.Damage, value = 10 } };
        Assert.That(loadout.TryEquip(accessory, new AccessoryInstanceData { instanceId = "test-stat-scope-item", accessoryId = accessory.itemId }, 0), Is.True);
        var buff = new PassiveActionDefinition { durationSeconds = .25f, modifiers = new() { new() { statType = StatType.Damage, operation = ModifierOp.AddPercent, value = 30 } } };
        ctx.PassiveController.TryApplyRuntimeModifier(buff, "stat-scope-global", 0);
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(163.8f).Within(.01)); Assert.That(ctx.StatsHub.GetSkillBaseDamage(), Is.EqualTo(39).Within(.01));

        typeof(WeaponSystem).GetMethod("RefreshDerivedStats", Hidden).Invoke(weapon, null);
        typeof(WeaponSystem).GetField("nextFireTime", Hidden).SetValue(weapon, 0f);
        Assert.That(weapon.TryShoot(), Is.True);
        foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (Context(projectile).combatMetadata.WeaponInstanceId == first.instanceId) projectiles.Add(projectile);
        Assert.That(projectiles, Has.Count.EqualTo(1));
        Assert.That(Context(projectiles[0]).stats.damage, Is.EqualTo(163.8f).Within(.01));
        weapon.Equip(gun, second);
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(143).Within(.01));
        Assert.That(Context(projectiles[0]).stats.damage, Is.EqualTo(163.8f).Within(.01), "Spawned snapshot is immutable across equip");
        ctx.PassiveController.Tick(.3f); loadout.UnequipSlot(0);
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(100).Within(.01));

        var status = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        status.effectId = "scope-reload"; status.duration = 1;
        status.modifiers = new() { new() { statType = StatType.ReloadTime, operation = ModifierOp.Multiply, value = .5f, scope = StatModifierScope.WeaponOnly } };
        ctx.StatusEffects.ApplyEffect(new StatusApplicationSpec { effect = status, weaponInstanceId = first.instanceId }, ctx.gameObject);
        Assert.That(ctx.StatsHub.ReloadTime, Is.EqualTo(.2f).Within(.001), "First weapon's status cannot leak into second instance");
        weapon.Equip(gun, first); Assert.That(ctx.StatsHub.ReloadTime, Is.EqualTo(.1f).Within(.001));
        Assert.That(ctx.StatsHub.GetReloadDuration(.4f), Is.EqualTo(.2f).Within(.001));
        weapon.TryReload();
        Assert.That(weapon.IsReloading, Is.True);
        float deadline = Time.realtimeSinceStartup + 10;
        while (weapon.IsReloading && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(weapon.IsReloading, Is.False); Assert.That(weapon.CurrentAmmo, Is.EqualTo(weapon.MagazineSize));
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(156).Within(.01), "Milestone modifier override must be enabled");
        Assert.That(ctx.StatsHub.GetSkillBaseDamage(), Is.EqualTo(20).Within(.01));
        weapon.Equip(gun, second); Assert.That(ctx.StatsHub.Damage, Is.EqualTo(100).Within(.01));
        yield return new WaitForSeconds(.3f);
        weapon.Equip(gun, first);
        ctx.PassiveController.enabled = false; ctx.PassiveController.enabled = true;
        ctx.StatsHub.RefreshCacheIfInputsChanged();
        Assert.That(ctx.StatsHub.Damage, Is.EqualTo(116).Within(.01));
        weapon.Equip(null, null); Assert.That(ctx.StatsHub.GetSkillBaseDamage(), Is.EqualTo(20).Within(.01));
    }
    public void Cleanup()
    {
        foreach (var projectile in projectiles) if (projectile != null && projectile.gameObject.activeInHierarchy) projectile.DespawnForRoomTransition();
        projectiles.Clear(); player.TearDown();
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
}
