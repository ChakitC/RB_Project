using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RomaProductionPassiveTestFixture
{
    readonly PlayerSlideTestFixture player = new();
    readonly List<Object> owned = new();
    readonly List<Projectile> projectiles = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public IEnumerator Run()
    {
        // Reuse the production Player/Roma setup, including its real character loader.
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden)
            .Invoke(player, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(player);
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(
            "Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(PassiveRecipeValidator.Validate(definition.recipe), Is.Empty);
        var grants = new List<EquippedPassive>();
        ctx.SkillManager.AppendConfiguredPassiveDefinitions(grants);
        Assert.That(grants.Count(g => g.Definition == definition), Is.EqualTo(1));
        Assert.That(ctx.PassiveController.RuntimeCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(ctx.PassiveController.HasCombatSubscription, Is.True);

        ctx.StaminaSystem.RecalculateMaximumStamina(true);
        Assert.That(ctx.StaminaSystem.Spend(10, "production-passive-check"), Is.True);
        float before = ctx.StaminaSystem.Current;
        var dodge = ctx.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            metadata: new CombatEventMetadata(dashExecutionId: 900001));
        ctx.CombatEventBus.Publish(dodge);
        ctx.CombatEventBus.Publish(ctx.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            metadata: new CombatEventMetadata(dashExecutionId: 900001)));
        Assert.That(ctx.StaminaSystem.Current, Is.EqualTo(before + 10).Within(0.001f));
        using (var attack = ctx.PassiveController.ReserveNextAttack("production-check"))
        {
            Assert.That(attack, Is.Null);
        }

        float oldMax = ctx.StatsHub.GetMaximumStamina();
        var bonus = new PassiveActionDefinition { durationSeconds = 0.5f };
        bonus.modifiers.Add(new PassiveStatModifier { statType = StatType.StaminaRegenRate, value = 1 });
        Assert.That(ctx.PassiveController.TryApplyRuntimeModifier(bonus, "production-test-regen", 0), Is.True);
        Assert.That(ctx.StatsHub.GetMaximumStamina(), Is.EqualTo(oldMax + 4).Within(0.001f));
        ctx.StaminaSystem.RecalculateMaximumStamina();
        using (var attack = ctx.PassiveController.ReserveNextAttack("max-change-check"))
            Assert.That(attack, Is.Null);

        Assert.That(ctx.StaminaSystem.Spend(ctx.StaminaSystem.Max * 0.4f), Is.True);
        var modifiers = new List<RuntimeStatModifier>();
        ctx.PassiveController.AppendStatModifiers(modifiers);
        var missingDamage = modifiers.Where(m => m.StatType == StatType.Damage &&
            m.ModifierKey.Contains(definition.DefinitionId)).ToList();
        Assert.That(missingDamage.Count, Is.EqualTo(1));
        Assert.That(missingDamage[0].Value, Is.EqualTo(20f).Within(0.001f));
        var runtimes = new List<PassiveRuntime>(); ctx.PassiveController.AppendRuntimes(runtimes);
        var runtime = runtimes.Single(r => r.Grant.Definition == definition);
        var weapon = ctx.WeaponSystem;
        var movement = (PlayerMovementCC)typeof(PlayerSlideTestFixture).GetField("movement", Hidden).GetValue(player);
        void CheckAimMovement(float expectedMultiplier)
        {
            typeof(PlayerMovementCC).GetField("aimSpeedMult", Hidden).SetValue(movement, .6f);
            typeof(PlayerMovementCC).GetField("speedLerp", Hidden).SetValue(movement, 100000f);
            typeof(PlayerMovementCC).GetField("currentSpeed", Hidden).SetValue(movement, 0f);
            ctx.moveInput = Vector2.zero; weapon.isAiming = true;
            typeof(PlayerMovementCC).GetMethod("Move", Hidden).Invoke(movement, null);
            float fireMultiplier = weapon.IsFiringActivity ? (float)typeof(PlayerMovementCC).GetField("fireSpeedMult", Hidden).GetValue(movement) : 1f;
            Assert.That((float)typeof(PlayerMovementCC).GetField("currentSpeed", Hidden).GetValue(movement),
                Is.EqualTo(ctx.GetMoveSpeedForCurrentLifeState() * expectedMultiplier * fireMultiplier).Within(.01), "movement consumer uses the live aim penalty");
            weapon.isAiming = false;
        }
        var source = weapon.CurrentWeapon != null ? weapon.CurrentWeapon : AssetDatabase.FindAssets("t:GunConfig")
            .Select(g => AssetDatabase.LoadAssetAtPath<GunConfig>(AssetDatabase.GUIDToAssetPath(g))).First(g => g.BulletPrefab != null);
        foreach (bool perBullet in new[] { false, true })
        {
            var gun = Object.Instantiate(source); owned.Add(gun);
            gun.autoloader = false; gun.reloadPerBullet = perBullet; gun.magazineReload = !perBullet;
            gun.reloadTime = 2.6f; gun.startInsertDelay = .52f; gun.perBulletInsertTime = 1.56f; gun.endInsertDelay = .52f;
            gun.magazine = gun.maxMagazine = 5; gun.infiniteReserveAmmo = true;
            weapon.Equip(gun, WeaponInstanceFactory.CreatePlainInstance(gun));
            ctx.StaminaSystem.RecalculateMaximumStamina(true);
            runtime.TryGetVariable("momentum", out float momentum, out _);
            while (momentum < 100)
            {
                ctx.StaminaSystem.Restore(ctx.StaminaSystem.Max);
                Assert.That(ctx.StaminaSystem.Spend(Mathf.Min(ctx.StaminaSystem.Max, 100 - momentum)), Is.True);
                runtime.TryGetVariable("momentum", out momentum, out _);
            }
            Assert.That(momentum, Is.EqualTo(100));
            var validPrefab = weapon.projectilePrefab;
            weapon.projectilePrefab = null;
            Assert.That(weapon.TryShoot(), Is.False);
            runtime.TryGetVariable("momentum", out momentum, out _); Assert.That(momentum, Is.EqualTo(100));
            weapon.projectilePrefab = validPrefab;
            typeof(WeaponSystem).GetField("nextFireTime", Hidden).SetValue(weapon, 0f);
            Assert.That(weapon.TryShoot(), Is.True);
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (!projectiles.Contains(projectile)) projectiles.Add(projectile);
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out _), Is.EqualTo(5).Within(.01));
            Assert.That(ctx.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.8f).Within(.001));
            CheckAimMovement(.8f);
            Assert.That(weapon.GetReloadAnimDuration(), Is.EqualTo(2f).Within(.01));
            yield return null;
            float start = (float)typeof(WeaponSystem).GetMethod("GetWeaponTime", Hidden).Invoke(weapon, null);
            weapon.TryReload(); Assert.That(weapon.IsReloading, Is.True);
            float deadline = Time.realtimeSinceStartup + 12;
            while (weapon.IsReloading && Time.realtimeSinceStartup < deadline) yield return null;
            float elapsed = (float)typeof(WeaponSystem).GetMethod("GetWeaponTime", Hidden).Invoke(weapon, null) - start;
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.magazine, Is.EqualTo(5));
            Assert.That(elapsed, Is.InRange(1.8f, 2.3f), perBullet ? "per-bullet reload" : "magazine reload");
            ctx.StaminaSystem.Restore(100); ctx.StaminaSystem.Spend(10);
            runtime.TryGetVariable("momentum", out momentum, out _); Assert.That(momentum, Is.Zero);
            ctx.PassiveController.Tick(5);
            Assert.That(ctx.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1));
            Assert.That(ctx.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.6f));
            CheckAimMovement(.6f);
        }
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
