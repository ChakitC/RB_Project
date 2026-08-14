#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SummonContractSmokeTests
{
    readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
        SummonRegressionProbePayload.ExecutionCount = 0;
    }

    [Test]
    public void WeaponProjectilePreservesPhysicalSummonAndCreditedOwner()
    {
        PlayerContext owner = CreateOwner();
        SummonedEntityRuntime summon = CreateInitializedSummon(owner, despawnDelay: 2f);

        var projectileObject = Track(new GameObject("SummonProjectile"));
        projectileObject.AddComponent<Rigidbody>();
        projectileObject.AddComponent<SphereCollider>();
        Projectile projectile = projectileObject.AddComponent<Projectile>();

        projectile.Init(null, new ProjectileContext
        {
            sourceActor = summon.transform,
            attribution = summon.Attribution,
            dir = Vector3.forward,
            stats = new ProjectileStats { damage = 10f, speed = 5f },
        });

        Assert.That(projectile.Attribution.PhysicalActor, Is.SameAs(summon.gameObject));
        Assert.That(projectile.Attribution.CreditedActor, Is.SameAs(owner.gameObject));
    }

    [Test]
    public void MeleeProjectileAndDotDamageUseOwnerKillCredit()
    {
        PlayerContext owner = CreateOwner();
        SummonedEntityRuntime summon = CreateInitializedSummon(owner, despawnDelay: 2f);
        DamageContext damageContext = CreateSummonDamageContext(summon);

        Assert.That(EnemyHealth.ResolveKillCredit(in damageContext), Is.SameAs(owner.gameObject));

        StatusEffectDef statusDefinition = Track(ScriptableObject.CreateInstance<StatusEffectDef>());
        StatusEffectInstance status = new StatusEffectInstance(
            statusDefinition,
            summon.gameObject,
            1,
            0f,
            "test",
            0,
            0,
            PassiveEventOrigin.StatusEffect,
            null,
            null);

        UnityEngine.Object.DestroyImmediate(summon.gameObject);

        Assert.That(status.Attribution.CreditedActor, Is.SameAs(owner.gameObject));
        Assert.That(EnemyHealth.ResolveKillCredit(in damageContext), Is.SameAs(owner.gameObject));
    }

    [Test]
    public void NormalActorStillUsesAttackerAsKillCredit()
    {
        GameObject attacker = Track(new GameObject("NormalAttacker"));
        DamageContext damageContext = new DamageContext(
            10f,
            attacker,
            "test",
            "test",
            1,
            0,
            PassiveEventOrigin.External);

        Assert.That(damageContext.Attribution.CreditedActor, Is.Null);
        Assert.That(EnemyHealth.ResolveKillCredit(in damageContext), Is.SameAs(attacker));
    }

    [Test]
    public void CompositeSummonWithoutMapStillRunsOtherPayloadsAndStampsCast()
    {
        SkillGemDefinition definition = CreateCompositeSummonSkill();
        definition.baseCooldown = 1f;
        var skill = new SkillInstance { def = definition };
        var user = new FakeSkillUser(100f);

        Assert.That(skill.Cast(user), Is.True);
        Assert.That(user.Energy, Is.EqualTo(90f));
        Assert.That(SummonRegressionProbePayload.ExecutionCount, Is.EqualTo(1));
        Assert.That(skill.Cast(user), Is.False, "The normal cast must stamp cooldown even when Summon does not spawn.");

        var ignoringCosts = new SkillInstance { def = definition };
        Assert.That(ignoringCosts.TryCastIgnoringResourceCosts(user), Is.True);
        Assert.That(SummonRegressionProbePayload.ExecutionCount, Is.EqualTo(2));
    }

    [Test]
    public void SummonStagingRootIsInactiveBeforeInitialization()
    {
        GameObject mapObject = Track(new GameObject("SummonMap"));
        MapRunController map = mapObject.AddComponent<MapRunController>();

        Transform stagingRoot = map.GetOrCreateSummonStagingRoot();

        Assert.That(stagingRoot.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void CapEvictionBeginsDelayedDespawnWithoutDestroyingImmediately()
    {
        PlayerContext owner = CreateOwner();
        SummonedEntityRuntime summon = CreateInitializedSummon(owner, despawnDelay: 10f);
        bool callbackReceived = false;
        summon.DespawnRequested += _ => callbackReceived = true;

        Assert.That(summon.BeginDespawn(SummonDespawnReason.CapEvicted), Is.True);
        Assert.That(summon.LifecycleState, Is.EqualTo(SummonLifecycleState.Despawning));
        Assert.That(summon.IsActive, Is.False);
        Assert.That(callbackReceived, Is.True);
        Assert.That(summon.gameObject, Is.Not.Null);
    }

    [Test]
    public void SummonOwnedVfxCapabilitySurvivesRoomTransitionCleanupOwnershipCheck()
    {
        GameObject summonObject = Track(new GameObject("SummonVfxOwner"));
        summonObject.AddComponent<SummonContext>();

        MethodInfo method = typeof(RoomTransitionCleanup).GetMethod(
            "IsOwnedByActiveParty",
            BindingFlags.Static | BindingFlags.NonPublic);
        bool preserved = (bool)method.Invoke(null, new object[] { summonObject.transform });

        Assert.That(preserved, Is.True);
    }

    [Test]
    public void NestedRotatedBoxUsesAccumulatedRootSpaceFootprintAndOrientedReservation()
    {
        GameObject nestedPrefab = Track(new GameObject("NestedBoxPrefab"));
        GameObject colliderObject = Track(new GameObject("NestedBoxCollider"));
        colliderObject.transform.SetParent(nestedPrefab.transform, false);
        colliderObject.transform.localPosition = new Vector3(1f, 0.5f, -0.25f);
        colliderObject.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        colliderObject.transform.localScale = new Vector3(2f, 1f, 0.5f);
        BoxCollider nestedCollider = colliderObject.AddComponent<BoxCollider>();
        nestedCollider.size = new Vector3(1f, 2f, 1f);

        CharacterColliderRefs nestedRefs = nestedPrefab.AddComponent<CharacterColliderRefs>();
        nestedRefs.CharacterPositionCollider = nestedCollider;

        Assert.That(CharacterPlacementProbeUtility.TryGetFootprint(
                nestedPrefab,
                SummonMobility.Stationary,
                out CharacterPlacementFootprint footprint,
                out string error), Is.True, error);

        Vector3 expectedCenter = nestedPrefab.transform.InverseTransformPoint(
            colliderObject.transform.TransformPoint(nestedCollider.center));
        Assert.That(Vector3.Distance(footprint.CenterOffset, expectedCenter), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(footprint.Rotation, colliderObject.transform.localRotation), Is.LessThan(0.01f));
        nestedPrefab.SetActive(false);

        PlayerContext owner = CreateOwner();
        GameObject longPrefab = Track(new GameObject("LongBoxPrefab"));
        longPrefab.transform.position = new Vector3(100f, 0f, 100f);
        BoxCollider longCollider = longPrefab.AddComponent<BoxCollider>();
        longCollider.size = new Vector3(4f, 0.2f, 0.2f);
        CharacterColliderRefs longRefs = longPrefab.AddComponent<CharacterColliderRefs>();
        longRefs.CharacterPositionCollider = longCollider;

        var request = new SummonSpawnContext
        {
            Caster = owner,
            Prefab = longPrefab,
            Mobility = SummonMobility.Stationary,
            Position = Vector3.zero,
        };
        var settings = new SummonPlacementSettings
        {
            ResolveGround = false,
            ClearanceMask = ~0,
        };
        var reserved = new List<SummonPlacementCandidate>();

        Assert.That(SummonPlacementResolver.TryResolve(
                request,
                Vector3.zero,
                Quaternion.identity,
                settings,
                reserved,
                out SummonPlacementCandidate first,
                out string firstError), Is.True, firstError);
        reserved.Add(first);

        Assert.That(SummonPlacementResolver.TryResolve(
                request,
                new Vector3(0f, 0f, 0.5f),
                Quaternion.identity,
                settings,
                reserved,
                out _,
                out string secondError), Is.True, secondError);
    }

    [Test]
    public void GroundUsedForResolutionDoesNotBlockDefaultClearance()
    {
        GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "SummonPlacementGround";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(20f, 1f, 20f);

        GameObject prefab = Track(new GameObject("GroundPlacementPrefab"));
        prefab.transform.position = new Vector3(100f, 0f, 100f);
        BoxCollider collider = prefab.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 2f, 1f);
        collider.center = new Vector3(0f, 1f, 0f);
        CharacterColliderRefs refs = prefab.AddComponent<CharacterColliderRefs>();
        refs.CharacterPositionCollider = collider;

        PlayerContext owner = CreateOwner();
        var request = new SummonSpawnContext
        {
            Caster = owner,
            Prefab = prefab,
            Mobility = SummonMobility.Stationary,
            Position = Vector3.zero,
        };
        var settings = new SummonPlacementSettings
        {
            ResolveGround = true,
            GroundMask = ~0,
            ClearanceMask = ~0,
            Padding = 0.05f,
        };

        Physics.SyncTransforms();
        Assert.That(SummonPlacementResolver.TryResolve(
                request,
                Vector3.zero,
                Quaternion.identity,
                settings,
                null,
                out SummonPlacementCandidate candidate,
                out string error), Is.True, error);
        Assert.That(candidate.Position.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void HorizontalCapsuleUsesAuthoredAxisForClearance()
    {
        GameObject blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        blocker.name = "HorizontalCapsuleBlocker";
        blocker.transform.position = new Vector3(0f, 0.5f, 1.5f);
        blocker.transform.localScale = Vector3.one * 0.2f;

        GameObject prefab = Track(new GameObject("HorizontalCapsulePrefab"));
        prefab.transform.position = new Vector3(100f, 0f, 100f);
        CapsuleCollider collider = prefab.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.height = 4f;
        collider.radius = 0.5f;
        collider.center = new Vector3(0f, 0.5f, 0f);
        CharacterColliderRefs refs = prefab.AddComponent<CharacterColliderRefs>();
        refs.CharacterPositionCollider = collider;

        PlayerContext owner = CreateOwner();
        var request = new SummonSpawnContext
        {
            Caster = owner,
            Prefab = prefab,
            Mobility = SummonMobility.Stationary,
            Position = Vector3.zero,
        };
        var settings = new SummonPlacementSettings
        {
            ResolveGround = false,
            ClearanceMask = ~0,
        };

        Physics.SyncTransforms();
        Assert.That(SummonPlacementResolver.TryResolve(
                request,
                Vector3.zero,
                Quaternion.identity,
                settings,
                null,
                out _,
                out string error), Is.False);
        Assert.That(error, Is.EqualTo("Summon placement clearance is blocked."));
    }

    [Test]
    public void AttributionAndHealthResolveRuntimeThroughSummonContext()
    {
        PlayerContext owner = CreateOwner();
        GameObject summonRoot = Track(new GameObject("SummonHierarchy"));
        summonRoot.AddComponent<SummonContext>();

        GameObject runtimeObject = new GameObject("RuntimeSibling");
        runtimeObject.transform.SetParent(summonRoot.transform, false);
        SummonedEntityRuntime runtime = runtimeObject.AddComponent<SummonedEntityRuntime>();

        GameObject sourceObject = new GameObject("PhysicalSourceSibling");
        sourceObject.transform.SetParent(summonRoot.transform, false);

        GameObject healthObject = new GameObject("HealthSibling");
        healthObject.transform.SetParent(summonRoot.transform, false);
        healthObject.AddComponent<SummonHealthSystem>();

        runtime.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon.hierarchy",
            Mobility = SummonMobility.Stationary,
            Lifetime = 30f,
            DespawnDelay = 2f,
        }, null);

        CombatAttributionSnapshot snapshot = CombatAttributionSnapshot.FromPhysicalActor(sourceObject);
        Assert.That(snapshot.CreditedActor, Is.SameAs(owner.gameObject));

        MethodInfo deplete = typeof(SummonHealthSystem).GetMethod(
            "HandleHealthDepleted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(deplete, Is.Not.Null);
        deplete.Invoke(healthObject.GetComponent<SummonHealthSystem>(), null);
        Assert.That(runtime.LifecycleState, Is.EqualTo(SummonLifecycleState.Despawning));
    }

    [Test]
    public void DespawnDisablesActorGameplayOutsideGameplayRootButKeepsPresentationAlive()
    {
        PlayerContext owner = CreateOwner();
        GameObject summonRoot = Track(new GameObject("SummonLifecycle"));
        summonRoot.AddComponent<SummonContext>();
        SummonedEntityRuntime runtime = summonRoot.AddComponent<SummonedEntityRuntime>();
        SummonRegressionProbeBehaviour rootGameplay = summonRoot.AddComponent<SummonRegressionProbeBehaviour>();

        GameObject gameplayRoot = new GameObject("GameplayRoot");
        gameplayRoot.transform.SetParent(summonRoot.transform, false);
        GameObject presentationRoot = new GameObject("PresentationRoot");
        presentationRoot.transform.SetParent(summonRoot.transform, false);
        SummonRegressionProbeBehaviour presentation = presentationRoot.AddComponent<SummonRegressionProbeBehaviour>();

        var serialized = new SerializedObject(runtime);
        serialized.FindProperty("gameplayRoot").objectReferenceValue = gameplayRoot;
        serialized.FindProperty("presentationRoot").objectReferenceValue = presentationRoot.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        runtime.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon.lifecycle",
            Mobility = SummonMobility.Stationary,
            Lifetime = 30f,
            DespawnDelay = 2f,
        }, null);

        Assert.That(runtime.BeginDespawn(SummonDespawnReason.CapEvicted), Is.True);
        Assert.That(rootGameplay.enabled, Is.False);
        Assert.That(gameplayRoot.activeSelf, Is.False);
        Assert.That(presentation.enabled, Is.True);
        Assert.That(presentationRoot.activeSelf, Is.True);
    }

    PlayerContext CreateOwner()
    {
        GameObject ownerObject = Track(new GameObject("SummonOwner"));
        return ownerObject.AddComponent<PlayerContext>();
    }

    SummonedEntityRuntime CreateInitializedSummon(PlayerContext owner, float despawnDelay)
    {
        GameObject summonObject = Track(new GameObject("Summon"));
        summonObject.AddComponent<SummonContext>();
        SummonedEntityRuntime runtime = summonObject.AddComponent<SummonedEntityRuntime>();
        runtime.Initialize(new SummonSpawnContext
        {
            Caster = owner,
            SkillId = "test.summon",
            Mobility = SummonMobility.Stationary,
            Lifetime = 30f,
            DespawnDelay = despawnDelay,
        }, null);
        return runtime;
    }

    static DamageContext CreateSummonDamageContext(SummonedEntityRuntime summon)
    {
        return new DamageContext(
            10f,
            summon.gameObject,
            "test",
            "test",
            1,
            0,
            PassiveEventOrigin.External,
            attribution: summon.Attribution);
    }

    SkillGemDefinition CreateCompositeSummonSkill()
    {
        SkillGemDefinition definition = Track(ScriptableObject.CreateInstance<SkillGemDefinition>());
        definition.skillId = "test.composite.summon";

        CompositeSkillPayloadDef composite = Track(ScriptableObject.CreateInstance<CompositeSkillPayloadDef>());
        SummonSkillPayloadDef summon = Track(ScriptableObject.CreateInstance<SummonSkillPayloadDef>());
        SummonRegressionProbePayload probe = Track(ScriptableObject.CreateInstance<SummonRegressionProbePayload>());

        var summonStep = new PayloadStep();
        summonStep.SetPayload(summon);
        var probeStep = new PayloadStep();
        probeStep.SetPayload(probe);
        composite.AddStep(summonStep);
        composite.AddStep(probeStep);
        definition.payload = composite;
        return definition;
    }

    T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
    }

    sealed class FakeSkillUser : ISkillUser
    {
        public FakeSkillUser(float energy)
        {
            Energy = energy;
        }

        public float Energy { get; private set; }
        public Transform CastOrigin => null;
        public Transform AimTransform => null;
        public Vector3 AimDirection => Vector3.forward;
        public float currentEnagy => Energy;
        public StatsHub StatsHub => null;

        public void SpendEnagy(float amount)
        {
            Energy -= amount;
        }
    }
}

public sealed class SummonRegressionProbePayload : SkillPayloadDef
{
    public static int ExecutionCount;

    public override void Execute(SkillCastContext context)
    {
        ExecutionCount++;
    }
}

public sealed class SummonRegressionProbeBehaviour : MonoBehaviour
{
}
#endif
