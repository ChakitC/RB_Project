#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterPlacementResolverTests
{
    const int WorldLayer = 0;
    const int ActorLayer = 1;

    readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void WallPenetrationHasPriorityOverActorPenetration()
    {
        GameObject wall = CreateBlocker("PlacementWall", WorldLayer, Vector3.zero, Vector3.one);
        CreateBlocker("PlacementActor", ActorLayer, new Vector3(3f, 0.5f, 0f), Vector3.one * 4f);
        Physics.SyncTransforms();

        CharacterPlacementRequest request = CreateRequest(
            new[]
            {
                Candidate(Vector3.zero, preferredAngleError: 0f, authoredOrder: 0),
                Candidate(new Vector3(3f, 0f, 0f), preferredAngleError: 0f, authoredOrder: 1),
            },
            worldCollisionLayers: 1 << WorldLayer,
            actorCollisionLayers: 1 << ActorLayer);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.CandidateIndex, Is.EqualTo(1),
            "An actor overlap must not beat a candidate with no wall penetration when wall depth has priority.");
        Assert.That(result.Score.MaxWorldPenetration, Is.EqualTo(0f));
        Assert.That(wall, Is.Not.Null);
    }

    [Test]
    public void DisabledPlanarRootMotionEvaluatesStaticTrajectory()
    {
        CreateBlocker("StaticTrajectoryBlocker", WorldLayer, new Vector3(2f, 0.5f, 0f), Vector3.one);
        Physics.SyncTransforms();

        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(1f, new Vector3(2f, 0f, 0f), 0f),
            });
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animation,
            worldCollisionLayers: 1 << WorldLayer,
            effectivePlanarRootMotion: false);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(Vector3.Distance(result.StartPosition, result.ImpactPosition), Is.LessThan(0.001f));
        Assert.That(result.Score.CollisionSampleCount, Is.EqualTo(0));
    }

    [Test]
    public void MotionSweepDetectsThinWorldBlockerBetweenSamples()
    {
        CreateBlocker(
            "ThinPlacementWall",
            WorldLayer,
            new Vector3(0f, 0f, 1f),
            new Vector3(4f, 4f, 0.05f));
        Physics.SyncTransforms();

        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(1f, new Vector3(0f, 0f, 2f), 0f),
            });
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animation,
            worldCollisionLayers: 1 << WorldLayer);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.Score.MaxWorldPenetration, Is.GreaterThan(0f));
        Assert.That(result.Score.CollisionSampleCount, Is.GreaterThan(0));
    }

    [Test]
    public void RotationOnlySweepDetectsBoxCornerBlocker()
    {
        CreateBlocker(
            "RotationOnlyCornerBlocker",
            WorldLayer,
            new Vector3(0.65f, 0f, -0.65f),
            Vector3.one * 0.15f);
        Physics.SyncTransforms();

        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(1f, Vector3.zero, 90f),
            });
        CharacterPlacementFootprint footprint = new(
            CharacterPlacementShape.Box,
            Vector3.zero,
            new Vector3(1f, 0.5f, 0.1f),
            0.5f,
            1f);
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animation,
            worldCollisionLayers: 1 << WorldLayer,
            footprintOverride: footprint);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.Score.CollisionSampleCount, Is.GreaterThan(0));
        Assert.That(result.Score.MaxWorldPenetration, Is.GreaterThan(0f));
    }

    [Test]
    public void TranslationAndRotationSweepDetectsBoxCornerBlocker()
    {
        CreateBlocker(
            "TranslationRotationCornerBlocker",
            WorldLayer,
            new Vector3(0.65f, 0f, -0.65f),
            Vector3.one * 0.15f);
        Physics.SyncTransforms();

        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(1f, new Vector3(0.1f, 0f, 0f), 90f),
            });
        CharacterPlacementFootprint footprint = new(
            CharacterPlacementShape.Box,
            Vector3.zero,
            new Vector3(1f, 0.5f, 0.1f),
            0.5f,
            1f);
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animation,
            worldCollisionLayers: 1 << WorldLayer,
            footprintOverride: footprint);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.Score.CollisionSampleCount, Is.GreaterThan(0));
        Assert.That(result.Score.MaxWorldPenetration, Is.GreaterThan(0f));
    }

    [Test]
    public void DesiredImpactMismatchFailsClosedAfterStartAdjustment()
    {
        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(1f, Vector3.right, 0f),
            });
        CharacterPlacementRequest.Candidate candidate = new(
            Vector3.zero,
            Quaternion.identity,
            preferredAngleError: 0f,
            authoredOrder: 0,
            desiredImpactPosition: Vector3.zero);
        CharacterPlacementRequest request = CreateRequest(
            new[] { candidate },
            animation,
            impactNormalizedTime: 1f);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.False);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void TargetContactWindowIgnoresOnlyTheTargetAtImpactTime()
    {
        GameObject target = CreateBlocker("PlacementTarget", ActorLayer, new Vector3(1.2f, 0.5f, 0f), Vector3.one);
        Physics.SyncTransforms();

        var animation = new CharacterPlacementAnimationInput(
            null,
            null,
            planarRootMotionEnabled: true,
            new[]
            {
                new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
                new CharacterPlacementAnimationInput.Sample(0.5f, new Vector3(1.2f, 0f, 0f), 0f),
                new CharacterPlacementAnimationInput.Sample(1f, Vector3.right * 3f, 0f),
            });
        CharacterPlacementPolicyDef policy = Track(ScriptableObject.CreateInstance<CharacterPlacementPolicyDef>());
        policy.targetContactWindowBefore = 0.1f;
        policy.targetContactWindowAfter = 0.1f;
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animation,
            policy,
            actorCollisionLayers: 1 << ActorLayer,
            targetRoot: target.transform,
            impactNormalizedTime: 0.5f);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.Score.CollisionSampleCount, Is.EqualTo(0));
    }

    [Test]
    public void ReservationOverlapIsScoredAsActorPenetration()
    {
        CharacterPlacementReservationService reservations = new();
        GameObject firstOwner = Track(new GameObject("FirstReservationOwner"));
        CharacterPlacementRequest firstRequest = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            reservationOwner: firstOwner);

        Assert.That(CharacterPlacementResolver.TryResolve(
                firstRequest,
                reservations,
                out CharacterPlacementResult firstResult), Is.True, firstResult.FailureReason);
        Assert.That(reservations.TryReserve(firstRequest, firstResult, out _), Is.True);

        GameObject secondOwner = Track(new GameObject("SecondReservationOwner"));
        CharacterPlacementRequest secondRequest = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            reservationOwner: secondOwner);

        Assert.That(CharacterPlacementResolver.TryResolve(
                secondRequest,
                reservations,
                out CharacterPlacementResult secondResult), Is.True, secondResult.FailureReason);
        Assert.That(secondResult.Score.MaxActorPenetration, Is.GreaterThan(0f));
        Assert.That(reservations.ActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void TransientReservationUsesSummonContextRootForSiblingCollider()
    {
        GameObject summonRoot = Track(new GameObject("SummonContextRoot"));
        SummonContext summonContext = summonRoot.AddComponent<SummonContext>();

        GameObject runtimeObject = Track(new GameObject("SummonedRuntime"));
        runtimeObject.transform.SetParent(summonRoot.transform, false);
        SummonedEntityRuntime spawned = runtimeObject.AddComponent<SummonedEntityRuntime>();
        SerializedObject serializedRuntime = new(spawned);
        serializedRuntime.FindProperty("summonContext").objectReferenceValue = summonContext;
        serializedRuntime.ApplyModifiedPropertiesWithoutUndo();

        GameObject colliderObject = Track(new GameObject("SummonColliderSibling"));
        colliderObject.transform.SetParent(summonRoot.transform, false);
        colliderObject.layer = ActorLayer;
        BoxCollider summonCollider = colliderObject.AddComponent<BoxCollider>();
        summonCollider.size = Vector3.one;
        Physics.SyncTransforms();

        Assert.That(spawned.SummonContext, Is.SameAs(summonContext));
        Assert.That(summonCollider.transform.IsChildOf(spawned.transform), Is.False);

        Transform reservationOwner = SummonPlacementResolver.ResolveReservationOwner(spawned);
        CharacterPlacementReservationService reservations = new();
        CharacterPlacementRequest firstRequest = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            actorCollisionLayers: 1 << ActorLayer,
            reservationOwner: reservationOwner,
            transientReservation: true);

        Assert.That(CharacterPlacementResolver.TryResolve(
                firstRequest,
                reservations,
                out CharacterPlacementResult firstResult), Is.True, firstResult.FailureReason);
        Assert.That(reservations.TryReserve(firstRequest, firstResult, out _), Is.True);

        GameObject secondOwner = Track(new GameObject("SecondSummonOwner"));
        CharacterPlacementRequest secondRequest = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            actorCollisionLayers: 1 << ActorLayer,
            reservationOwner: secondOwner);

        Assert.That(CharacterPlacementResolver.TryResolve(
                secondRequest,
                reservations,
                out CharacterPlacementResult secondResult), Is.True, secondResult.FailureReason);
        Assert.That(secondResult.Score.TotalActorPenetration, Is.EqualTo(1f).Within(0.01f),
            "The sibling collider must be covered by the root reservation and counted once.");
    }

    [Test]
    public void RequiredAnimationFailsWhenNoTrajectoryIsAvailable()
    {
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(Vector3.zero, 0f, 0) },
            animationRequired: true,
            effectivePlanarRootMotion: true);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.False);
        Assert.That(result.FailureReason, Does.Contain("trajectory"));
    }

    [Test]
    public void MobileActorFailsClosedWhenNoNavMeshPoseExists()
    {
        CharacterPlacementRequest request = CreateRequest(
            new[] { Candidate(new Vector3(10000f, 0f, 10000f), 0f, 0) },
            mobileActor: true);

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.False);
        Assert.That(result.FailureReason, Does.Contain("NavMesh"));
    }

    [Test]
    public void LeadInCompositionKeepsAttackImpactOnOneContinuousTrajectory()
    {
        AnimationClip leadInClip = Track(CreateClipWithOneSecondLength("PlacementLeadInClip"));
        AnimationClip attackClip = Track(CreateClipWithOneSecondLength("PlacementAttackClip"));
        TargetedSkillRootMotionTrajectory leadIn = new(
            leadInClip,
            new[]
            {
                new TargetedSkillRootMotionSample(0f, Vector3.zero, 0f),
                new TargetedSkillRootMotionSample(1f, Vector3.right * 2f, 20f),
            });
        TargetedSkillRootMotionTrajectory attack = new(
            attackClip,
            new[]
            {
                new TargetedSkillRootMotionSample(0f, Vector3.zero, 0f),
                new TargetedSkillRootMotionSample(1f, Vector3.forward * 3f, 40f),
            });

        Assert.That(TargetedSkillRootMotionTrajectory.TryComposeLeadIn(
                leadIn,
                0.5f,
                attack,
                out TargetedSkillRootMotionTrajectory composite,
                out float attackStartNormalized,
                out string failureReason), Is.True, failureReason);
        Assert.That(attackStartNormalized, Is.EqualTo(1f / 3f).Within(0.001f));
        Assert.That(composite.PlacementInput.Segments, Has.Length.EqualTo(2));

        Assert.That(composite.TrySample(
                attackStartNormalized,
                out TargetedSkillRootMotionSample attackStart), Is.True);
        Assert.That(Vector3.Distance(attackStart.localPosition, Vector3.right), Is.LessThan(0.001f));
        Assert.That(attackStart.localYaw, Is.EqualTo(10f).Within(0.001f));

        Assert.That(composite.TrySample(1f, out TargetedSkillRootMotionSample end), Is.True);
        Vector3 expectedAttackDelta =
            Quaternion.Euler(0f, 10f, 0f) * (Vector3.forward * 3f);
        Assert.That(
            Vector3.Distance(end.localPosition, Vector3.right + expectedAttackDelta),
            Is.LessThan(0.001f));
        Assert.That(end.localYaw, Is.EqualTo(50f).Within(0.001f));
    }

    [Test]
    public void AuthoredCandidateOrderBreaksEqualScoresDeterministically()
    {
        CharacterPlacementRequest request = CreateRequest(
            new[]
            {
                Candidate(Vector3.zero, 0f, authoredOrder: 4),
                Candidate(new Vector3(2f, 0f, 0f), 0f, authoredOrder: 2),
            });

        Assert.That(CharacterPlacementResolver.TryResolve(
                request,
                null,
                out CharacterPlacementResult result), Is.True, result.FailureReason);
        Assert.That(result.CandidateIndex, Is.EqualTo(1));
    }

    [Test]
    public void AnchorSnapshotCapturePreservesPoseAndNullDefaults()
    {
        Transform anchor = Track(new GameObject("PlacementAnchor")).transform;
        anchor.position = new Vector3(2f, 3f, 4f);
        anchor.rotation = Quaternion.Euler(10f, 25f, 35f);

        CharacterPlacementRequest.AnchorSnapshot captured =
            CharacterPlacementRequest.AnchorSnapshot.Capture(
                anchor,
                AITargetIdentity.Companion);
        CharacterPlacementRequest.AnchorSnapshot empty =
            CharacterPlacementRequest.AnchorSnapshot.Capture(null);

        Assert.That(Vector3.Distance(captured.Position, anchor.position), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(captured.Rotation, anchor.rotation), Is.LessThan(0.001f));
        Assert.That(captured.TargetIdentity, Is.EqualTo(AITargetIdentity.Companion));
        Assert.That(empty.Position, Is.EqualTo(Vector3.zero));
        Assert.That(empty.Rotation, Is.EqualTo(Quaternion.identity));
        Assert.That(empty.TargetIdentity, Is.EqualTo(AITargetIdentity.Generic));
    }

    [Test]
    public void RuntimePolicyDefaultFactoryPreservesResolverDefaults()
    {
        CharacterPlacementRuntimePolicy policy = CharacterPlacementRuntimePolicy.CreateDefault(
            requireNavMesh: true,
            navMeshSampleDistance: 1.25f,
            collisionTriggerInteraction: QueryTriggerInteraction.Ignore,
            collisionPadding: 0.07f,
            targetContactWindowBefore: 0.11f,
            targetContactWindowAfter: 0.13f);

        Assert.That(policy.HasValue, Is.True);
        Assert.That(policy.RequireNavMesh, Is.True);
        Assert.That(policy.NavMeshSampleDistance, Is.EqualTo(1.25f).Within(0.001f));
        Assert.That(policy.NavMeshAreaMask, Is.EqualTo(UnityEngine.AI.NavMesh.AllAreas));
        Assert.That(policy.RequireGroundSupport, Is.False);
        Assert.That(policy.GroundLayers.value, Is.EqualTo(Physics.DefaultRaycastLayers));
        Assert.That(policy.GroundRaycastHeight, Is.EqualTo(2f));
        Assert.That(policy.GroundRaycastDistance, Is.EqualTo(8f));
        Assert.That(policy.CollisionPadding, Is.EqualTo(0.07f).Within(0.001f));
        Assert.That(policy.MaxDetailedCandidates, Is.EqualTo(3));
        Assert.That(policy.MaxTrajectorySamples, Is.EqualTo(240));
        Assert.That(policy.TargetContactWindowBefore, Is.EqualTo(0.11f).Within(0.001f));
        Assert.That(policy.TargetContactWindowAfter, Is.EqualTo(0.13f).Within(0.001f));
    }

    CharacterPlacementRequest CreateRequest(
        CharacterPlacementRequest.Candidate[] candidates,
        CharacterPlacementAnimationInput animation = null,
        CharacterPlacementPolicyDef policy = null,
        LayerMask worldCollisionLayers = default,
        LayerMask actorCollisionLayers = default,
        Transform targetRoot = null,
        float impactNormalizedTime = 0.5f,
        Object reservationOwner = null,
        bool effectivePlanarRootMotion = true,
        bool animationRequired = false,
        bool mobileActor = false,
        CharacterPlacementFootprint? footprintOverride = null,
        bool transientReservation = false)
    {
        GameObject actor = Track(new GameObject("PlacementActorRoot"));
        actor.transform.position = new Vector3(100f, 0f, 100f);
        BoxCollider collider = actor.AddComponent<BoxCollider>();
        collider.size = Vector3.one;
        CharacterPlacementFootprint footprint = footprintOverride ?? new CharacterPlacementFootprint(
            CharacterPlacementShape.Circle,
            Vector3.zero,
            new Vector3(0.5f, 1f, 0.5f),
            0.5f,
            2f);

        return new CharacterPlacementRequest(
            actor.transform,
            collider,
            footprint,
            AITargetIdentity.Generic,
            targetRoot,
            new CharacterPlacementRequest.AnchorSnapshot(
                Vector3.zero,
                Quaternion.identity,
                AITargetIdentity.Generic),
            candidates,
            animation,
            impactNormalizedTime,
            policy,
            worldCollisionLayers,
            actorCollisionLayers,
            actor.transform,
            reservationOwner,
            effectivePlanarRootMotion,
            animationRequired,
            mobileActor,
            transientReservation: transientReservation);
    }

    static CharacterPlacementRequest.Candidate Candidate(
        Vector3 position,
        float preferredAngleError,
        int authoredOrder)
    {
        return new CharacterPlacementRequest.Candidate(
            position,
            Quaternion.identity,
            preferredAngleError,
            authoredOrder);
    }

    GameObject CreateBlocker(string name, int layer, Vector3 position, Vector3 scale)
    {
        GameObject blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        blocker.name = name;
        blocker.layer = layer;
        blocker.transform.position = position;
        blocker.transform.localScale = scale;
        return blocker;
    }

    T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    static AnimationClip CreateClipWithOneSecondLength(string name)
    {
        AnimationClip clip = new() { name = name };
        clip.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, 1f, 1f));
        return clip;
    }

}
#endif
