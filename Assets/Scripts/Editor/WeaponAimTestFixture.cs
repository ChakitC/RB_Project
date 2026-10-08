#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WeaponAimTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    public void CloseAndElevatedTargetsConverge()
    {
        Vector3 muzzle = new(-0.5f, 1f, 0.7f);
        foreach (Vector3 point in new[] { new Vector3(0, 1, .8f), new Vector3(0, 15, 20), new Vector3(0, -10, 30) })
        {
            var sample = new WeaponAimSample(point, point.normalized);
            Assert.That(WeaponAimResolver.TryResolveDirection(muzzle, sample, 0, out var direction), Is.True);
            Assert.That(Vector3.Angle(direction, point - muzzle), Is.LessThan(.02f));
        }
        var reverse = new WeaponAimSample(muzzle - Vector3.forward, Vector3.forward);
        Assert.That(WeaponAimResolver.TryResolveDirection(muzzle, reverse, 0, out var forward), Is.True);
        Assert.That(Vector3.Dot(forward, Vector3.forward), Is.GreaterThan(.99f));
        Assert.That(WeaponAimResolver.TryResolveDirection(muzzle, default, 0, out _), Is.False);
        Assert.That(WeaponAimResolver.TryResolveDirection(new Vector3(float.NaN, 0, 0), reverse, 0, out _), Is.False);
    }

    public void PlayerAndAISourcesUseSameSpread()
    {
        var actor = Own(new GameObject("Aim sources"));
        var enemy = actor.AddComponent<EnemyContext>();
        var ai = actor.AddComponent<AIAimTargetDriver>();
        var playerObject = Own(new GameObject("Camera source"));
        var player = playerObject.AddComponent<ThirdPersonAimController>();
        Vector3 point = new(2, 3, 20);
        ai.SetOverridePoint(point);
        Assert.That(ai.TryGetAimSample(out var sample), Is.True);
        typeof(ThirdPersonAimController).GetProperty("AimPoint").SetValue(player, point);
        typeof(ThirdPersonAimController).GetProperty("CameraRayDirection").SetValue(player, sample.ReferenceDirection);
        var muzzle = Own(new GameObject("Muzzle")).transform;
        muzzle.position = new(-.5f, 1, .5f);
        var saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(123);
            Vector3 playerDirection = player.ResolveShotDirection(muzzle, 4);
            UnityEngine.Random.InitState(123);
            Assert.That(WeaponAimResolver.TryResolveDirection(muzzle.position, sample, 4, out var aiDirection), Is.True);
            Assert.That(Vector3.Distance(playerDirection, aiDirection), Is.LessThan(.00001f));
            Assert.That(Vector3.Angle(aiDirection, point - muzzle.position), Is.GreaterThan(.001f));
        }
        finally { UnityEngine.Random.state = saved; }
    }

    public void AIRejectsMissingTargetAndOldLifetime()
    {
        var actor = Own(new GameObject("AI owner"));
        actor.AddComponent<EnemyContext>();
        var source = actor.AddComponent<AIAimTargetDriver>();
        Assert.That(source.TryGetAimSample(out _), Is.False);
        source.SetOverridePoint(new Vector3(0, 4, 10));
        Assert.That(source.TryGetAimSample(out _), Is.True);
        source.ClearOverride();
        Assert.That(source.TryGetAimSample(out _), Is.False);
        var target = Own(new GameObject("Target"));
        var ctx = target.AddComponent<EnemyContext>();
        target.transform.position = Vector3.forward * 10;
        source.SetOverrideTarget(target.transform);
        Assert.That(source.TryGetAimSample(out _), Is.True);
        typeof(CharacteContext).GetField("_lifeGeneration", Hidden).SetValue(ctx, ctx.LifeGeneration + 1);
        Assert.That(source.TryGetAimSample(out _), Is.False);
    }

    public void AuthoringBindingsAreComplete()
    {
        Assert.That(WeaponMuzzleAuthoringValidator.Validate(false), Is.Empty);
        var issues = WeaponMuzzleAuthoringValidator.Validate(true);
        Assert.That(issues.Count, Is.EqualTo(4));
        Assert.That(issues.All(i => i.Contains("WeaponPrefab is missing")), Is.True);
    }

    public void ExistingHurtboxAndCoverRegressions()
    {
        // Test Runner already owns a temporary scene; use the self-cleaning cases directly.
        var check = typeof(ThirdPersonAimRegressionChecks).GetMethod("CheckColliderLayout", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (bool nested in new[] { false, true })
            Assert.That((int)check.Invoke(null, new object[] { nested }), Is.EqualTo(7));
    }

    public void SpreadUsesMovementStabilityAndBloom()
    {
        var root = Own(new GameObject("Spread owner"));
        root.SetActive(false);
        var owner = root.AddComponent<EnemyContext>();
        var weapon = root.AddComponent<WeaponSystem>();
        weapon.ctx = owner; owner.WeaponSystem = weapon;
        WeaponAimTestSource.Bind(weapon, root.transform);
        var source = (WeaponAimTestSource)owner.WeaponAim;
        var gun = Own(ScriptableObject.CreateInstance<GunConfig>());
        gun.hipSpreadDegrees = 2; gun.aimSpreadDegrees = .5f; gun.moveSpreadPenaltyDegrees = 1;
        typeof(WeaponSystem).GetField("currentWeapon", Hidden).SetValue(weapon, gun);
        source.Movement = 1;
        typeof(WeaponSystem).GetField("spreadBloom", Hidden).SetValue(weapon, 2f);
        weapon.stability = 50;
        Assert.That(weapon.CurrentSpreadDegrees, Is.EqualTo(2.5f).Within(.001));
        weapon.stability = 100;
        Assert.That(weapon.CurrentSpreadDegrees, Is.Zero);
    }

    public IEnumerator PlayerSwitchAndPool() => ProductionSwitchAndPool("Assets/Prefab/Player/Player.prefab", true);
    public IEnumerator AllySwitchAndPool() => ProductionSwitchAndPool("Assets/Prefab/Player/Ally_Stryker.prefab", false);
    public IEnumerator EnemySwitchAndPool() => ProductionSwitchAndPool("Assets/Prefab/GameEnemy/Enemy_Base.prefab", false);
    public IEnumerator TurretSwitchAndPool() => ProductionSwitchAndPool("Assets/Prefab/Player/MinigunTerret_Summon.prefab", false);

    IEnumerator ProductionSwitchAndPool(string path, bool player)
    {
        Assert.That(Application.isPlaying, Is.True);
        var parent = Own(new GameObject("Weapon aim production fixture"));
        parent.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.transform);
        var owner = root.GetComponent<CharacteContext>();
        var loader = root.GetComponentInChildren<CharacterContextPartyLoader>(true);
        if (loader != null)
            loader.ConfigureRuntimeDefinitionOverride(AssetDatabase.LoadAssetAtPath<CharacterStats>(
                "Assets/Scripts/CharacterStats/Asosiation/ChaDef.Roma.asset"));
        foreach (var agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = false;
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is UnityEngine.InputSystem.PlayerInput || behaviour is CharacterVerticalMotor ||
                behaviour.GetType().Name == "BehaviorTree" || behaviour is AgentMoveDriver) behaviour.enabled = false;
        Camera camera = null;
        if (player)
        {
            var cameraObject = Own(new GameObject("Weapon test camera")); cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 2, -4);
        }
        parent.SetActive(true);
        yield return null;
        yield return null;
        owner.ResolveReferences();
        var weapon = owner.WeaponSystem;
        Assert.That(weapon, Is.Not.Null);
        var gunPaths = new[] {
            "Assets/Scripts/WeponStats/Smg_Roma/SMG_RB01.asset",
            "Assets/Scripts/ScriptableObject/Weapon/AR/SMG_RB02.asset",
            "Assets/Scripts/ScriptableObject/Weapon/HMG/HMG.RB.01.asset",
            "Assets/Scripts/ScriptableObject/Weapon/SP/SR_RB_01.asset",
            "Assets/Scripts/ScriptableObject/Weapon/SMG/Enemy_SMG_GR01.asset"
        };
        Transform previous = null;
        foreach (string gunPath in gunPaths)
        {
            var gun = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GunConfig>(gunPath)));
            gun.hipSpreadDegrees = gun.aimSpreadDegrees = gun.moveSpreadPenaltyDegrees = gun.spreadPerShotDegrees = 0;
            weapon.Equip(gun, null);
            Assert.That(weapon.FirePoint, Is.Not.Null, path + " " + gunPath);
            Assert.That(weapon.FirePoint.GetComponentInParent<WeaponMuzzleBinding>().IsValid, Is.True);
            if (!path.Contains("Terret") && previous != null) Assert.That(weapon.FirePoint, Is.Not.SameAs(previous));
            previous = weapon.FirePoint;
            Vector3 aimPoint = weapon.FirePoint.position + new Vector3(1, 3, 20);
            if (player) camera.transform.LookAt(aimPoint);
            else ((AIAimTargetDriver)owner.WeaponAim).SetOverridePoint(aimPoint);
            yield return null;
            Assert.That(owner.WeaponAim.TryGetAimSample(out var sample), Is.True);
            Assert.That(WeaponAimResolver.TryResolveDirection(weapon.FirePoint.position, sample, 0, out var direction), Is.True);
            Assert.That(Vector3.Distance(direction, (sample.Point - weapon.FirePoint.position).normalized), Is.LessThan(.0001f));
            if (owner.EnegySystem != null) Assert.That(owner.EnegySystem.CastOrigin, Is.SameAs(weapon.FirePoint));
            // Real admission, ammo, passive notifications, pool spawn and explicit shot direction.
            weapon.magazine = Mathf.Max(1, weapon.MagazineSize);
            typeof(WeaponSystem).GetField("nextFireTime", Hidden).SetValue(weapon, 0f);
            int ammoBefore = weapon.magazine;
            var boundMuzzle = weapon.FirePoint;
            weapon.BindFirePoint(null);
            Assert.That(weapon.TryShoot(), Is.False, "Missing muzzle must reject the shot.");
            Assert.That(weapon.magazine, Is.EqualTo(ammoBefore));
            Assert.That(weapon.BindFirePoint(boundMuzzle), Is.True);
            owner.WeaponAim.enabled = false;
            Assert.That(weapon.TryShoot(), Is.False, "Missing aim must reject the shot.");
            Assert.That(weapon.magazine, Is.EqualTo(ammoBefore));
            owner.WeaponAim.enabled = true;
            if (!player) ((AIAimTargetDriver)owner.WeaponAim).SetOverridePoint(aimPoint);
            Vector3 muzzlePosition = weapon.FirePoint.position;
            var existing = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).ToHashSet();
            Assert.That(weapon.TryShoot(), Is.True, "Production actor could not fire: " + path);
            var spawned = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .Where(p => !existing.Contains(p)).ToArray();
            Assert.That(spawned, Is.Not.Empty);
            Assert.That(spawned.Any(p => Vector3.Distance(p.transform.position, muzzlePosition) < .001f &&
                Vector3.Distance(p.Direction.normalized, direction) < .0001f), Is.True,
                "A real projectile must use the current muzzle position and resolved aim direction.");
        }
        if (!path.Contains("Terret"))
        {
            var visual = owner.Visual;
            // Enemy definitions can reference a full actor prefab for spawning; that is not a form model.
            var form = owner.baseStats.CharacterPrefab;
            if (form != null && form.GetComponent<CharacteContext>() == null)
            {
                var oldMuzzle = weapon.FirePoint;
                visual.ApplyFormOverride(form);
                Assert.That(weapon.FirePoint, Is.Not.Null);
                Assert.That(weapon.FirePoint, Is.Not.SameAs(oldMuzzle), "Model rebuild retained the old muzzle.");
                visual.RestoreDefaultForm();
                Assert.That(weapon.FirePoint, Is.Not.Null);
            }
            if (!visual.IsSlot)
            {
                owner.baseStats = Own(Object.Instantiate(owner.baseStats));
                foreach (var hand in new[] { CharacterWeaponHandMode.LeftHand, CharacterWeaponHandMode.BothHands })
                {
                    owner.baseStats.weaponHandMode = hand;
                    visual.BuildModelFromWeaponDef();
                    var primary = (GameObject)typeof(CharacterVisualController)
                        .GetField(hand == CharacterWeaponHandMode.LeftHand ? "_leftObj" : "_rightObj", Hidden).GetValue(visual);
                    Assert.That(primary, Is.Not.Null);
                    Assert.That(weapon.FirePoint.IsChildOf(primary.transform), Is.True,
                        "LeftHand uses left muzzle; BothHands uses right muzzle.");
                }
            }
        }
        root.SetActive(false);
        Assert.That(weapon.FirePoint, Is.Null);
        root.SetActive(true);
        yield return null;
        Assert.That(weapon.FirePoint, Is.Not.Null, "Pool re-enable did not rebind the weapon muzzle.");
        weapon.Equip(AssetDatabase.LoadAssetAtPath<GunConfig>(
            "Assets/Scripts/ScriptableObject/Weapon/AR/Rifle_GR02.asset"), null);
        Assert.That(weapon.FirePoint, Is.Null, "An incomplete gun must not borrow the integrated turret muzzle.");
        Assert.That(weapon.TryShoot(), Is.False);
        weapon.Equip(null, null);
        Assert.That(weapon.FirePoint, Is.Null);
        Assert.That(weapon.TryShoot(), Is.False);
    }
}
#endif
