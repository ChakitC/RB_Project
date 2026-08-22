#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Baseline for the pooled projectile lifecycle: atomic spawn, state reset across reuse, pool
/// return, world-slow scaling, and the split depth/generation contract.
///
/// These are Edit Mode checks driven through the public spawn API plus a couple of reflective pokes
/// at the physics step. Trigger collisions, hit VFX, and real prefab wiring stay Play Mode work.
/// </summary>
public sealed class ProjectileLifecycleSmokeTests
{
    readonly List<Object> createdObjects = new();

    ProjectilePool pool;
    TimeSlowManager timeSlow;

    [SetUp]
    public void SetUp()
    {
        var poolObject = Track(new GameObject("TestProjectilePool"));
        pool = poolObject.AddComponent<ProjectilePool>();

        var timeSlowObject = Track(new GameObject("TestTimeSlowManager"));
        timeSlow = timeSlowObject.AddComponent<TimeSlowManager>();
        SetWorldTimeScale(1f);
    }

    [TearDown]
    public void TearDown()
    {
        SetWorldTimeScale(1f);

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
        DestroyLeakedClones();
        pool = null;
        timeSlow = null;
    }

    /// <summary>
    /// Instances handed out by the pool detach from it on spawn, and split children are created by
    /// the projectile itself, so neither is in <see cref="createdObjects"/>. They all carry the
    /// test-only probe, which makes them safe to identify without touching real scene objects.
    /// </summary>
    static void DestroyLeakedClones()
    {
        ProjectileSpawnProbe[] probes = Object.FindObjectsByType<ProjectileSpawnProbe>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null)
                Object.DestroyImmediate(probes[i].gameObject);
        }
    }

    // ---- atomic spawn ---------------------------------------------------------------------------

    [Test]
    public void AcquiredProjectileStaysInactiveUntilActivateForSpawn()
    {
        Projectile prefab = CreateProjectileSource("WeaponBullet");

        Projectile instance = pool.AcquireInactive(prefab, new Vector3(1f, 2f, 3f), Quaternion.identity);

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.gameObject.activeSelf, Is.False, "Acquire must hand back an inactive instance.");
        Assert.That(instance.GetComponent<ProjectileSpawnProbe>().EnableCount, Is.EqualTo(0),
            "OnEnable must not run before the caller has written runtime state.");
        Assert.That(instance.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
    }

    [Test]
    public void FirstEverInstanceAlsoSkipsOnEnableUntilInitialized()
    {
        // The pool has never seen this prefab, so this exercises the Instantiate branch rather than
        // the reuse branch. A fresh Instantiate is where OnEnable used to fire earliest of all.
        Projectile prefab = CreateProjectileSource("NeverPooled");

        Projectile instance = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        var probe = instance.GetComponent<ProjectileSpawnProbe>();

        Assert.That(probe.EnableCount, Is.EqualTo(0));
    }

    [Test]
    public void OnEnableObservesContextLayerAndStatsAlreadyApplied()
    {
        Projectile prefab = CreateProjectileSource("SkillBullet");
        ProjectileConfig config = CreateConfig("SkillConfig", lifeTime: 4f, baseSpeed: 12f);

        Projectile instance = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        instance.gameObject.layer = 9;
        instance.useAreaDamage = true;
        instance.Init(config, new ProjectileContext
        {
            dir = Vector3.right,
            depth = 4,
            splitGeneration = 2,
            stats = new ProjectileStats { damage = 5f, speed = 12f },
        });

        pool.ActivateForSpawn(instance);

        var probe = instance.GetComponent<ProjectileSpawnProbe>();
        Assert.That(probe.EnableCount, Is.EqualTo(1));
        Assert.That(probe.LayerAtEnable, Is.EqualTo(9), "Layer must be applied before activation.");
        Assert.That(probe.DirectionAtEnable, Is.EqualTo(Vector3.right));
        Assert.That(probe.ChainDepthAtEnable, Is.EqualTo(4));
        Assert.That(probe.SplitGenerationAtEnable, Is.EqualTo(2));
        Assert.That(probe.UseAreaDamageAtEnable, Is.True);
        Assert.That(probe.ConfigAtEnable, Is.SameAs(config), "Runtime stats must be live at OnEnable.");
    }

    // ---- pool return and reuse ------------------------------------------------------------------

    [Test]
    public void DespawnedProjectileGoesInactiveAndIsHandedOutAgain()
    {
        Projectile prefab = CreateProjectileSource("Recycled");

        Projectile first = SpawnSimple(prefab);
        first.DespawnForRoomTransition();

        Assert.That(first.gameObject.activeSelf, Is.False, "A despawned projectile must not stay active.");

        Projectile second = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        Assert.That(second, Is.SameAs(first), "The pool must reuse the returned instance.");
    }

    [Test]
    public void ReuseAsAWeaponBulletClearsSkillAoeCritAndPresentation()
    {
        Projectile prefab = CreateProjectileSource("Shared");
        prefab.useAreaDamage = false;
        prefab.areaRadius = 0f;
        prefab.critRate = 0f;
        prefab.critMult = 1f;
        prefab.hitVfxPrefab = null;
        prefab.vfxScale = 1f;

        // First life: a skill projectile writes area damage, crit, and its own presentation assets.
        Projectile skillShot = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        skillShot.useAreaDamage = true;
        skillShot.areaRadius = 6f;
        skillShot.critRate = 75f;
        skillShot.critMult = 3f;
        skillShot.hitVfxPrefab = Track(new GameObject("SkillHitVfx"));
        skillShot.vfxScale = 4f;
        skillShot.Init(null, SimpleContext());
        pool.ActivateForSpawn(skillShot);
        skillShot.DespawnForRoomTransition();

        // Second life: the same instance comes back as a plain weapon bullet.
        Projectile weaponShot = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);

        Assert.That(weaponShot, Is.SameAs(skillShot));
        Assert.That(weaponShot.useAreaDamage, Is.False, "Skill AoE must not leak into a weapon shot.");
        Assert.That(weaponShot.areaRadius, Is.EqualTo(0f));
        Assert.That(weaponShot.critRate, Is.EqualTo(0f));
        Assert.That(weaponShot.critMult, Is.EqualTo(1f));
        Assert.That(weaponShot.hitVfxPrefab, Is.Null);
        Assert.That(weaponShot.vfxScale, Is.EqualTo(1f));
        Assert.That(weaponShot.SourceSkillDef, Is.Null);
        Assert.That(weaponShot.SourceSkillStats, Is.Null);
    }

    [Test]
    public void ReuseWithoutACollisionIgnoreRootForgetsThePreviousShooter()
    {
        Projectile prefab = CreateProjectileSource("ShooterRootReuse");
        var shooter = Track(new GameObject("Shooter"));

        Projectile first = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        ProjectileContext firstContext = SimpleContext();
        firstContext.collisionIgnoreRoot = shooter.transform;
        first.Init(null, firstContext);
        pool.ActivateForSpawn(first);
        Assert.That(GetShooterRoot(first), Is.SameAs(shooter.transform));

        first.DespawnForRoomTransition();

        Projectile second = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        second.Init(null, SimpleContext());
        pool.ActivateForSpawn(second);

        Assert.That(GetShooterRoot(second), Is.Null,
            "A reused projectile with no ignore root must stop ignoring the previous shooter.");
    }

    // ---- world slow -----------------------------------------------------------------------------

    [Test]
    public void WorldSlowScalesBothTravelSpeedAndLifetime()
    {
        Projectile prefab = CreateProjectileSource("SlowMotion");
        ProjectileConfig config = CreateConfig("SlowConfig", lifeTime: 10f, baseSpeed: 20f);

        // Parked far from whatever scene the Editor happens to have open, and off every wall layer,
        // so the movement sweep inside FixedUpdate cannot hit real geometry or the projectile itself.
        Projectile instance = pool.AcquireInactive(prefab, new Vector3(0f, 10000f, 0f), Quaternion.identity);
        instance.gameObject.layer = 2; // Ignore Raycast
        instance.Init(config, new ProjectileContext
        {
            dir = Vector3.forward,
            stats = new ProjectileStats { damage = 1f, speed = 20f },
        });
        pool.ActivateForSpawn(instance);

        SetWorldTimeScale(0f);
        StepFixedUpdate(instance);

        Assert.That(GetAge(instance), Is.EqualTo(0f).Within(1e-5f),
            "A fully stopped world must not age the projectile out.");
        Assert.That(GetVelocity(instance).magnitude, Is.EqualTo(0f).Within(1e-4f));

        SetWorldTimeScale(0.5f);
        StepFixedUpdate(instance);

        Assert.That(GetAge(instance), Is.EqualTo(Time.fixedDeltaTime * 0.5f).Within(1e-5f));
        Assert.That(GetVelocity(instance).magnitude, Is.EqualTo(10f).Within(1e-3f));
    }

    // ---- split depth and generation -------------------------------------------------------------

    [Test]
    public void SplitChildAdvancesChainDepthByOne()
    {
        Projectile prefab = CreateProjectileSource("Splitter");
        Projectile parent = SpawnSimple(prefab, depth: 2, splitGeneration: 0, projectilePrefab: prefab);

        Projectile child = parent.SpawnChild(null, Vector3.zero, Vector3.forward, 0.5f, 1f);

        Assert.That(child, Is.Not.Null);
        Assert.That(child.ChainDepth, Is.EqualTo(3), "A split child is one link deeper in the event chain.");
    }

    [Test]
    public void SplitChildIsADescendantSoDepthGatedModulesSkipIt()
    {
        // UpgradeGatedStatusOnHitModule refuses to fire when ctx.depth > 0. Before depth was
        // propagated, every split child still read depth 0 and slipped through that gate.
        Projectile prefab = CreateProjectileSource("DescendantGate");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        Projectile child = parent.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);

        Assert.That(parent.ChainDepth, Is.EqualTo(0));
        Assert.That(child.ChainDepth, Is.GreaterThan(0));
    }

    [Test]
    public void SplitGenerationIsTrackedSeparatelyFromChainDepth()
    {
        Projectile prefab = CreateProjectileSource("GenerationCounter");
        Projectile parent = SpawnSimple(prefab, depth: 7, splitGeneration: 0, projectilePrefab: prefab);

        Projectile child = parent.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);
        Projectile grandChild = child.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);

        Assert.That(child.SplitGeneration, Is.EqualTo(1));
        Assert.That(grandChild.SplitGeneration, Is.EqualTo(2));
        Assert.That(grandChild.ChainDepth, Is.EqualTo(9), "Depth keeps its own meaning.");
    }

    [Test]
    public void SplitStopsAtTheAbsoluteGenerationCeiling()
    {
        Projectile prefab = CreateProjectileSource("RunawaySplit");
        Projectile current = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        int spawned = 0;
        for (int i = 0; i < Projectile.AbsoluteMaxSplitGeneration + 5; i++)
        {
            Projectile next = current.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);
            if (next == null)
                break;

            spawned++;
            current = next;
        }

        Assert.That(spawned, Is.EqualTo(Projectile.AbsoluteMaxSplitGeneration),
            "A cyclic split chain must terminate at the hard ceiling even without authoring limits.");
        Assert.That(current.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f), Is.Null);
    }

    [Test]
    public void AuthoredBudgetStopsSplittingBeforeTheCeiling()
    {
        Projectile prefab = CreateProjectileSource("BudgetedSplit");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        Assert.That(parent.CanSpawnSplitChild(1), Is.True);

        Projectile child = parent.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);
        Assert.That(child.CanSpawnSplitChild(1), Is.False, "One generation means the child never splits again.");
        Assert.That(child.CanSpawnSplitChild(0), Is.False);
        Assert.That(child.CanSpawnSplitChild(2), Is.True);
    }

    [Test]
    public void APermissiveChildConfigCannotWidenTheInheritedBudget()
    {
        // The reported hole: parent authors 1 generation, the child's own config authors 8. Without
        // an inherited budget the child read only its own number and kept splitting.
        Projectile prefab = CreateProjectileSource("BudgetInheritance");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        Assert.That(parent.CanSpawnSplitChild(1), Is.True);

        Projectile child = parent.SpawnChild(
            null, Vector3.zero, Vector3.forward, 1f, 1f, authoredMaxSplitGenerations: 1);

        Assert.That(child.SplitBudget, Is.EqualTo(1));
        Assert.That(child.CanSpawnSplitChild(8), Is.False,
            "A child config authoring 8 generations must still be held to the parent's budget of 1.");
        Assert.That(child.CanSpawnSplitChild(Projectile.AbsoluteMaxSplitGeneration), Is.False);
    }

    [Test]
    public void InheritedBudgetKeepsNarrowingDownTheChain()
    {
        Projectile prefab = CreateProjectileSource("BudgetNarrowing");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        // Generous first hop, restrictive second hop: the minimum must stick.
        Projectile child = parent.SpawnChild(
            null, Vector3.zero, Vector3.forward, 1f, 1f, authoredMaxSplitGenerations: 6);
        Assert.That(child.SplitBudget, Is.EqualTo(6));
        Assert.That(child.CanSpawnSplitChild(2), Is.True);

        Projectile grandChild = child.SpawnChild(
            null, Vector3.zero, Vector3.forward, 1f, 1f, authoredMaxSplitGenerations: 2);

        Assert.That(grandChild.SplitBudget, Is.EqualTo(2), "min(6, 2) must win.");
        Assert.That(grandChild.SplitGeneration, Is.EqualTo(2));
        Assert.That(grandChild.CanSpawnSplitChild(6), Is.False,
            "Generation 2 has already spent a budget of 2.");
    }

    [Test]
    public void AZeroBudgetMeansNeverSplitRatherThanUnconstrained()
    {
        Projectile prefab = CreateProjectileSource("NeverSplit");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        Assert.That(parent.CanSpawnSplitChild(0), Is.False);
    }

    [Test]
    public void ASpawnWithoutABudgetPassesTheInheritedOneThrough()
    {
        Projectile prefab = CreateProjectileSource("BudgetPassthrough");
        Projectile parent = SpawnSimple(prefab, depth: 0, splitGeneration: 0, projectilePrefab: prefab);

        Projectile child = parent.SpawnChild(
            null, Vector3.zero, Vector3.forward, 1f, 1f, authoredMaxSplitGenerations: 3);

        // The five-argument overload is the "caller has no budget of its own" path.
        Projectile grandChild = child.SpawnChild(null, Vector3.zero, Vector3.forward, 1f, 1f);

        Assert.That(grandChild.SplitBudget, Is.EqualTo(3), "An absent budget must not clear the inherited one.");
    }

    [Test]
    public void SplitModuleClampsAuthoringToSafeBounds()
    {
        var module = Track(ScriptableObject.CreateInstance<SplitOnHitModule>());
        module.childCount = 5000;
        module.maxSplitGenerations = 99;

        Assert.That(module.ResolvedChildCount, Is.EqualTo(SplitOnHitModule.MaxChildCount));
        Assert.That(module.ResolvedMaxSplitGenerations, Is.EqualTo(Projectile.AbsoluteMaxSplitGeneration));

        module.childCount = 0;
        module.maxSplitGenerations = -3;

        Assert.That(module.ResolvedChildCount, Is.EqualTo(1));
        Assert.That(module.ResolvedMaxSplitGenerations, Is.EqualTo(0));
    }

    [Test]
    public void CyclicSplitConfigurationIsDetected()
    {
        ProjectileConfig first = CreateConfig("CycleA", lifeTime: 3f, baseSpeed: 10f);
        ProjectileConfig second = CreateConfig("CycleB", lifeTime: 3f, baseSpeed: 10f);

        var firstSplit = Track(ScriptableObject.CreateInstance<SplitOnHitModule>());
        firstSplit.childConfig = second;
        first.modules.Add(firstSplit);

        var secondSplit = Track(ScriptableObject.CreateInstance<SplitOnHitModule>());
        secondSplit.childConfig = first;
        second.modules.Add(secondSplit);

        var cyclePath = new List<ProjectileConfig>();
        Assert.That(ProjectileSplitGraphAnalyzer.TryFindCycle(first, cyclePath), Is.True);
        Assert.That(ProjectileSplitGraphAnalyzer.DescribeCycle(cyclePath), Does.Contain("CycleA"));
        Assert.That(ProjectileSplitGraphAnalyzer.DescribeCycle(cyclePath), Does.Contain("CycleB"));
    }

    [Test]
    public void AcyclicSplitConfigurationIsNotFlagged()
    {
        ProjectileConfig parent = CreateConfig("Parent", lifeTime: 3f, baseSpeed: 10f);
        ProjectileConfig leaf = CreateConfig("Leaf", lifeTime: 3f, baseSpeed: 10f);

        var split = Track(ScriptableObject.CreateInstance<SplitOnHitModule>());
        split.childConfig = leaf;
        parent.modules.Add(split);

        Assert.That(ProjectileSplitGraphAnalyzer.TryFindCycle(parent, new List<ProjectileConfig>()), Is.False);
    }

    // ---- helpers --------------------------------------------------------------------------------

    T Track<T>(T obj) where T : Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    Projectile CreateProjectileSource(string name)
    {
        var go = Track(new GameObject(name));

        // Added explicitly rather than through [RequireComponent]: Collider is abstract, so Unity
        // cannot auto-add one. Same pattern as the summon contract tests.
        go.AddComponent<Rigidbody>();
        go.AddComponent<SphereCollider>();

        var projectile = go.AddComponent<Projectile>();
        go.AddComponent<ProjectileSpawnProbe>();

        // Deactivated so the pool clones an inactive template, exactly like a project prefab asset.
        go.SetActive(false);
        return projectile;
    }

    ProjectileConfig CreateConfig(string name, float lifeTime, float baseSpeed)
    {
        var config = Track(ScriptableObject.CreateInstance<ProjectileConfig>());
        config.name = name;
        config.lifeTime = lifeTime;
        config.baseSpeed = baseSpeed;
        return config;
    }

    static ProjectileContext SimpleContext() => new()
    {
        dir = Vector3.forward,
        stats = new ProjectileStats { damage = 1f, speed = 10f },
    };

    Projectile SpawnSimple(
        Projectile prefab,
        int depth = 0,
        int splitGeneration = 0,
        Projectile projectilePrefab = null)
    {
        Projectile instance = pool.AcquireInactive(prefab, Vector3.zero, Quaternion.identity);
        instance.Init(null, new ProjectileContext
        {
            dir = Vector3.forward,
            depth = depth,
            splitGeneration = splitGeneration,
            projectilePrefab = projectilePrefab,
            stats = new ProjectileStats { damage = 1f, speed = 10f },
        });
        pool.ActivateForSpawn(instance);
        return instance;
    }

    void SetWorldTimeScale(float value)
    {
        if (timeSlow == null)
            return;

        // The property setter is private, so the auto-property backing field is the stable handle.
        FieldInfo backingField = typeof(TimeSlowManager).GetField(
            $"<{nameof(TimeSlowManager.WorldTimeScale)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(backingField, Is.Not.Null, "TimeSlowManager.WorldTimeScale is no longer an auto-property.");
        backingField.SetValue(timeSlow, value);
    }

    static void StepFixedUpdate(Projectile projectile)
    {
        MethodInfo method = typeof(Projectile).GetMethod(
            "FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(projectile, null);
    }

    static float GetAge(Projectile projectile) =>
        (float)typeof(Projectile)
            .GetField("_age", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(projectile);

    static Transform GetShooterRoot(Projectile projectile) =>
        (Transform)typeof(Projectile)
            .GetField("_shooterRoot", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(projectile);

    static Vector3 GetVelocity(Projectile projectile)
    {
        var body = projectile.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }
}
#endif
