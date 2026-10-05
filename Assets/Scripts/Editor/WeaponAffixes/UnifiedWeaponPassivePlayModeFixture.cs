using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UnifiedWeaponPassivePlayModeFixture
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
        Assert.That(weapon, Is.Not.Null);
        var db = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>("Assets/Scripts/Weapons/WeaponAffixDatabase.asset");
        var fresh = db.Affixes.Single(d => d.rootBehavior is ConfiguredWeaponAffixBehavior b && b.kind == WeaponAffixRuntimeKind.FreshChamber);
        var echo = db.Affixes.Single(d => d.rootBehavior is ConfiguredWeaponAffixBehavior b && b.kind == WeaponAffixRuntimeKind.EchoChamber);
        var sourceGun = weapon.CurrentWeapon != null ? weapon.CurrentWeapon : AssetDatabase.FindAssets("t:GunConfig")
            .Select(g => AssetDatabase.LoadAssetAtPath<GunConfig>(AssetDatabase.GUIDToAssetPath(g))).First(g => g.BulletPrefab != null);
        var gun = Own(Object.Instantiate(sourceGun)); gun.autoloader = false; gun.fireRate = .01f; gun.magazine = gun.maxMagazine = 20;
        var instance = WeaponInstanceFactory.CreatePlainInstance(gun);
        instance.mainAffix = new() { affixId = fresh.affixId, hasPrimaryValue = true, primaryValue = 20 };
        instance.subAffixes.Add(new() { affixId = echo.affixId, hasPrimaryValue = true, primaryValue = echo.rollSpec.minimum });
        typeof(WeaponAffixRuntimeController).GetField("affixDatabase", Hidden).SetValue(weapon.GetComponent<WeaponAffixRuntimeController>(), db);
        weapon.Equip(gun, instance);
        var runtimes = new List<PassiveRuntime>(); ctx.PassiveController.AppendRuntimes(runtimes);
        var freshRuntime = runtimes.Single(r => r.Grant.Definition == fresh.passiveDefinition);
        var echoRuntime = runtimes.Single(r => r.Grant.Definition == echo.passiveDefinition);
        var roma = runtimes.Single(r => r.Grant.Definition.passiveId == "passive.roma.stamina-momentum");
        echoRuntime.RandomValue = () => 0;
        var echoBehavior = (ConfiguredWeaponAffixBehavior)echo.rootBehavior;
        new WeaponPassiveStateStore(instance, instance.subAffixes[0].grantId, null).Write("progress", echoBehavior.threshold - 1);
        ctx.CombatEventBus.Publish(ctx.CombatEventBus.CreateExternalContext(PassiveEventType.Reload,
            metadata: new CombatEventMetadata(weaponInstanceId: instance.instanceId)));
        ctx.StaminaSystem.RecalculateMaximumStamina(true); ctx.StaminaSystem.Spend(10);
        var validPrefab = weapon.projectilePrefab;
        weapon.projectilePrefab = Own(new GameObject("Invalid projectile for cancellation check"));
        LogAssert.Expect(LogType.Warning, "Projectile prefab is missing Projectile component.");
        Assert.That(weapon.TryShoot(), Is.False, "Invalid primary projectile must not commit passives");
        freshRuntime.TryGetVariable("charges", out float charges, out _); Assert.That(charges, Is.EqualTo(3));
        roma.TryGetVariable("momentum", out float momentum, out _); Assert.That(momentum, Is.EqualTo(10));
        echoRuntime.TryGetVariable("progress", out float progress, out _); Assert.That(progress, Is.EqualTo(echoBehavior.threshold - 1));
        weapon.projectilePrefab = validPrefab;
        typeof(WeaponSystem).GetField("nextFireTime", Hidden).SetValue(weapon, 0f);
        typeof(WeaponSystem).GetMethod("RefreshDerivedStats", Hidden).Invoke(weapon, null);
        float baseDamage = weapon.Damage;
        Assert.That(weapon.TryShoot(), Is.True);
        foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (Context(projectile).combatMetadata.WeaponInstanceId == instance.instanceId) projectiles.Add(projectile);
        Assert.That(projectiles.Count, Is.EqualTo(2), "One primary + one committed Echo projectile");
        var primary = projectiles.Single(p => !Context(p).combatMetadata.IsWeaponAffixGenerated);
        var generated = projectiles.Single(p => Context(p).combatMetadata.IsWeaponAffixGenerated);
        Assert.That(Context(primary).stats.damage, Is.EqualTo(baseDamage * 1.2f * 1.2f).Within(.01));
        Assert.That(Context(generated).stats.damage, Is.EqualTo(baseDamage * echoBehavior.fixedValue).Within(.01));
        Assert.That(Context(generated).origin, Is.EqualTo(PassiveEventOrigin.Passive));
        freshRuntime.TryGetVariable("charges", out charges, out _); Assert.That(charges, Is.EqualTo(2));
        echoRuntime.TryGetVariable("progress", out progress, out _); Assert.That(progress, Is.Zero);
        roma.TryGetVariable("momentum", out momentum, out _); Assert.That(momentum, Is.Zero);
        float snapshot = Context(primary).stats.damage;
        weapon.Equip(gun, WeaponInstanceFactory.CreatePlainInstance(gun));
        Assert.That(ctx.PassiveController.ContainsRuntime(freshRuntime), Is.False);
        Assert.That(Context(primary).stats.damage, Is.EqualTo(snapshot));
        Assert.That(ctx.PassiveController.ContainsRuntime(roma), Is.True);
        yield return null;
    }
    public void Cleanup()
    {
        foreach (var projectile in projectiles) if (projectile != null && projectile.gameObject.activeInHierarchy) projectile.DespawnForRoomTransition();
        projectiles.Clear(); player.TearDown();
        foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
        owned.Clear();
    }
}
