using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class PartyFollowUpPlacementTests
{
    readonly List<Object> created = new();
    readonly Vector3 origin = new Vector3(10000f, 0f, 10000f);
    Camera camera;
    NavMeshDataInstance navMesh;
    CharacterPlacementFootprint footprint;

    [SetUp]
    public void SetUp()
    {
        camera = CreateObject("FollowUp camera", new Vector3(0f, 1f, -8f)).AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.aspect = 1.6f;
        footprint = CharacterPlacementFootprintUtility.CreateFallbackBox(Vector3.up,
            new Vector3(0.35f, 1f, 0.35f));
    }

    [TearDown]
    public void TearDown()
    {
        if (navMesh.valid) navMesh.Remove();
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    GameObject CreateObject(string name, Vector3 position)
    {
        var result = new GameObject(name);
        result.transform.position = origin + position;
        created.Add(result);
        return result;
    }

    BoxCollider Blocker(Vector3 size)
    {
        var box = CreateObject("Target body", Vector3.zero).AddComponent<BoxCollider>();
        box.center = Vector3.up * size.y * 0.5f;
        box.size = size;
        Physics.SyncTransforms();
        return box;
    }

    PartyFollowUpPlacementVisibility Evaluate(Vector3 position, Transform ignored = null)
    {
        return PartyFollowUpPlacementVisibility.Evaluate(camera, origin + position, Quaternion.identity,
            footprint, ignored, null, ~0);
    }

    [Test]
    public void TargetBodyBlocksRearPoseButNotSidePose()
    {
        Blocker(new Vector3(2f, 3f, 0.5f));
        Assert.That(Evaluate(new Vector3(0f, 0f, 2f)).VisibleSamples, Is.EqualTo(0));
        Assert.That(Evaluate(new Vector3(3f, 0f, 0f)).VisibleSamples, Is.EqualTo(5));
    }

    [Test]
    public void LeastObscuredPoseWinsWhenNeitherIsFullyVisible()
    {
        BoxCollider box = Blocker(new Vector3(2f, 3f, 0.5f));
        var full = Evaluate(new Vector3(0f, 0f, 2f));
        box.size = new Vector3(2f, 1f, 0.5f);
        box.center = Vector3.up * 0.5f;
        Physics.SyncTransforms();
        var partial = Evaluate(new Vector3(0f, 0f, 2f));
        Assert.That(partial.VisibleSamples, Is.InRange(1, 4));
        Assert.That(partial.CompareTo(full), Is.GreaterThan(0));
    }

    [Test]
    public void OffscreenPoseDoesNotWinOverVisiblePose()
    {
        Assert.That(Evaluate(new Vector3(0f, 0f, -12f)).InFrameSamples, Is.Zero);
        Assert.That(Evaluate(Vector3.zero).CompareTo(Evaluate(new Vector3(0f, 0f, -12f))),
            Is.GreaterThan(0));
    }

    [Test]
    public void OwnOldBodyAndSensorVolumesDoNotOccludeDestination()
    {
        BoxCollider box = Blocker(new Vector3(2f, 3f, 0.5f));
        Assert.That(Evaluate(new Vector3(0f, 0f, 2f), box.transform).VisibleSamples, Is.EqualTo(5));
        box.isTrigger = true;
        Physics.SyncTransforms();
        Assert.That(Evaluate(new Vector3(0f, 0f, 2f)).VisibleSamples, Is.EqualTo(5));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RingResolverAvoidsTargetOcclusionAndKeepsNavMeshPose(bool withCharacterController)
    {
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(origin + Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
                size = new Vector3(20f, 0.2f, 20f), area = 0 }
        };
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(origin, new Vector3(24f, 8f, 24f)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);
        created.Add(data);
        navMesh = NavMesh.AddNavMeshData(data);
        BoxCollider target = Blocker(new Vector3(2f, 3f, 0.5f));
        AllyContext actor = CreateObject("FollowUp ally", new Vector3(0f, 0f, 4f)).AddComponent<AllyContext>();
        CharacterController body = null;
        if (withCharacterController)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            created.Add(floor);
            floor.transform.position = origin;
            floor.transform.localScale = Vector3.one * 2f;
            body = actor.gameObject.AddComponent<CharacterController>();
            body.center = Vector3.up * 1.26f;
            body.height = 2.45f;
            body.radius = 0.5f;
            Assert.That(CharacterPlacementFootprintUtility.TryGetColliderFootprint(body,
                actor.transform, out footprint, out _), Is.True);
            Assert.That(footprint.Height, Is.EqualTo(2.45f).Within(0.001f));
            Assert.That(footprint.Radius, Is.EqualTo(0.5f).Within(0.001f));
            Physics.SyncTransforms();
        }
        var profile = ScriptableObject.CreateInstance<PartyFollowUpExecutionProfile>();
        created.Add(profile);
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, body, footprint, target.transform,
            AITargetIdentity.Generic, profile, camera, null, out _, out CharacterPlacementResult result), Is.True);
        Assert.That(Evaluate(result.StartPosition - origin).VisibleSamples, Is.EqualTo(5));
        Assert.That(NavMesh.SamplePosition(result.StartPosition, out _, 0.1f, NavMesh.AllAreas), Is.True);
        Assert.That(result.Score.MaxWorldPenetration, Is.Zero);
        if (withCharacterController)
        {
            Blocker(new Vector3(10f, 4f, 10f));
            Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, body, footprint, target.transform,
                AITargetIdentity.Generic, profile, camera, null, out _, out _), Is.False,
                "Actual wall penetration must still reject every blocked destination.");
        }
    }

    void BuildPlacementNavMesh()
    {
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(origin + Vector3.down * .1f, Quaternion.identity, Vector3.one),
                size = new Vector3(30f, .2f, 30f), area = 0 }
        };
        var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(origin, new Vector3(34f, 8f, 34f)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);
        created.Add(data);
        navMesh = NavMesh.AddNavMeshData(data);
    }

    PartyFollowUpExecutionProfile PlacementProfile()
    {
        var profile = ScriptableObject.CreateInstance<PartyFollowUpExecutionProfile>();
        created.Add(profile);
        profile.placementPolicy = PartyFollowUpPlacementPolicy.AlignPlacementPointToEventTarget;
        profile.placementCandidateCount = 1;
        return profile;
    }

    CharacterPlacementAnimationInput Motion(float endYaw = 0f) => new(null, null, true, new[]
    {
        new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
        new CharacterPlacementAnimationInput.Sample(.5f, Vector3.forward * 2f, endYaw * .5f),
        new CharacterPlacementAnimationInput.Sample(1f, Vector3.forward * 4f, endYaw)
    });

    [Test]
    public void PlacementTimingMovesStartButKeepsContactPoseIncludingYaw()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("FollowUp ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        var profile = PlacementProfile();
        var animation = Motion(180f);
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out var request, out var first,
            animation, .25f), Is.True);
        Assert.That(request.EffectivePlanarRootMotion && request.AnimationRequired, Is.True);
        Assert.That(request.Animation, Is.SameAs(animation));
        Assert.That(request.ImpactNormalizedTime, Is.EqualTo(.25f));
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out _, out var second,
            animation, .5f), Is.True);
        Assert.That(Vector3.Distance(first.StartPosition, second.StartPosition), Is.GreaterThan(.5f));
        Assert.That(Vector3.Distance(first.ImpactPosition, second.ImpactPosition), Is.LessThan(.01f));
        animation.TrySample(.5f, out var impact);
        Assert.That(Vector3.Distance(second.StartPosition + second.StartRotation * impact.LocalPosition,
            second.ImpactPosition), Is.LessThan(.01f));
        Assert.That(Quaternion.Angle(second.StartRotation * Quaternion.Euler(0f, impact.LocalYaw, 0f),
            Quaternion.LookRotation(Vector3.back)), Is.LessThan(.01f));
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(second.ImpactPosition - origin, Vector3.up),
            Vector3.forward * profile.desiredRange), Is.LessThan(.01f));
    }

    [Test]
    public void PlacementChecksWholeMotionAndDoesNotFallBackToStaticWarp()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("FollowUp ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        var profile = PlacementProfile();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out _, out _, Motion(), .5f), Is.True);
        var wall = CreateObject("Wall between S and A", Vector3.forward * 3f).AddComponent<BoxCollider>();
        wall.center = Vector3.up * 1.5f;
        wall.size = new Vector3(3f, 3f, .25f);
        Physics.SyncTransforms();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out _, out _, Motion(), .5f), Is.False);
        profile.placementPolicy = PartyFollowUpPlacementPolicy.PlaceNearEventTarget;
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out var request, out _), Is.True);
        Assert.That(request.EffectivePlanarRootMotion, Is.False);
        Assert.That(request.Animation, Is.Null);
    }

    [Test]
    public void ThroughCharacterMotionKeepsWorldAndReservationChecks()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("FollowUp ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = Blocker(new Vector3(1f, 3f, 1f));
        target.gameObject.layer = LayerMask.NameToLayer("Enemy");
        LayerMask ignoredLayers = 1 << target.gameObject.layer;
        var profile = PlacementProfile();
        var motion = Motion();
        Physics.SyncTransforms();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target.transform,
            AITargetIdentity.Generic, profile, null, null, out _, out _, motion, .5f), Is.False,
            "The target body intersects the trajectory after the contact window.");
        var reservations = new CharacterPlacementReservationService();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target.transform,
            AITargetIdentity.Generic, profile, null, reservations, out var request, out var placement,
            motion, .5f, ignoredLayers), Is.True);
        Assert.That(reservations.TryReserve(request, placement, out var handle), Is.True);
        var other = CreateObject("Second ally", Vector3.forward * 9f).AddComponent<AllyContext>();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(other, null, footprint, target.transform,
            AITargetIdentity.Generic, profile, null, reservations, out _, out _, motion, .5f,
            ignoredLayers), Is.False, "Ignoring body layers must retain trajectory reservations.");
        Assert.That(reservations.Release(handle), Is.True);
        target.gameObject.layer = 0;
        Physics.SyncTransforms();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target.transform,
            AITargetIdentity.Generic, profile, null, reservations, out _, out _, motion, .5f,
            ignoredLayers), Is.False, "World geometry must still block the same trajectory.");
    }

    [Test]
    public void PlacementModeRejectsMissingTrajectory()
    {
        var actor = CreateObject("FollowUp ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, PlacementProfile(), null, null, out var request, out var result), Is.False);
        Assert.That(request, Is.Null);
        StringAssert.Contains("trajectory", result.FailureReason);
    }

    [Test]
    public void PlacementReservationBlocksOverlapUntilReleased()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("First ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var other = CreateObject("Second ally", Vector3.forward * 9f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        var profile = PlacementProfile();
        var reservations = new CharacterPlacementReservationService();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, reservations, out var request, out var placement,
            Motion(), .5f), Is.True);
        Assert.That(reservations.TryReserve(request, placement, out var handle), Is.True);
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(other, null, footprint, target,
            AITargetIdentity.Generic, profile, null, reservations, out _, out _, Motion(), .5f), Is.False);
        Assert.That(reservations.Release(handle), Is.True);
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(other, null, footprint, target,
            AITargetIdentity.Generic, profile, null, reservations, out _, out _, Motion(), .5f), Is.True);
    }

    CharacterPlacementAnimationInput JumpMotion(float landingDistance = 4f) => new(null, null, true, new[]
    {
        new CharacterPlacementAnimationInput.Sample(0f, Vector3.zero, 0f),
        new CharacterPlacementAnimationInput.Sample(.25f, new Vector3(0f, 3f, 1f), 0f),
        new CharacterPlacementAnimationInput.Sample(.5f, new Vector3(0f, 4f, 2f), 0f),
        new CharacterPlacementAnimationInput.Sample(.75f, new Vector3(0f, 2f, 3f), 0f),
        new CharacterPlacementAnimationInput.Sample(1f, Vector3.forward * landingDistance, 0f)
    }, preserveVerticalMotion: true);

    [Test]
    public void JumpKeepsGroundedStartAndAuthoredHeightAtPlacement()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("Jump ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, PlacementProfile(), null, null, out var request, out var result,
            JumpMotion(), .5f), Is.True);
        Assert.That(result.StartPosition.y, Is.EqualTo(origin.y).Within(.05f));
        Assert.That(result.ImpactPosition.y - result.StartPosition.y, Is.EqualTo(4f).Within(.01f));
        Assert.That(request.Animation.PreserveVerticalMotion, Is.True);
    }

    [Test]
    public void JumpRejectsCeilingAndMissingLandingSupport()
    {
        BuildPlacementNavMesh();
        var actor = CreateObject("Jump ally", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        var profile = PlacementProfile();
        var ceiling = CreateObject("Ceiling above jump", Vector3.up * 5f).AddComponent<BoxCollider>();
        ceiling.size = new Vector3(10f, .25f, 10f);
        Physics.SyncTransforms();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out _, out _, JumpMotion(), .5f), Is.False);
        ceiling.enabled = false;
        Physics.SyncTransforms();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out _, out _, JumpMotion(40f), .5f), Is.False);
    }

    // Run explicitly in Play Mode: Unity only emits the sampler's OnAnimatorMove there.
    public void AiresAuthoredClipAlignsAtSavedPlacementPoint()
    {
        Assert.That(Application.isPlaying, Is.True, "This smoke check requires Play Mode.");
        BuildPlacementNavMesh();
        var followUp = UnityEditor.AssetDatabase.LoadAssetAtPath<PartyFollowUpSkillDef>(
            "Assets/Data/PartyFollowUp/Test/Test.Aires.FollowUp.asset");
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Charactor/Aires.prefab");
        var skill = followUp.executionSkill;
        Assert.That(followUp.executionProfile.placementPolicy,
            Is.EqualTo(PartyFollowUpPlacementPolicy.AlignPlacementPointToEventTarget));
        var animator = prefab.GetComponentInChildren<Animator>(true);
        var cache = typeof(TargetedSkillRootMotionTrajectory).Assembly.GetType("TargetedSkillRootMotionTrajectoryCache");
        object[] args = { skill.skillClip.Clip, animator, null, null };
        Assert.That(cache.GetMethod("TryGetWithVerticalMotion").Invoke(null, args), Is.True, args[3] as string);
        var trajectory = (TargetedSkillRootMotionTrajectory)args[2];
        float peak = 0f;
        foreach (var sample in trajectory.Samples) peak = Mathf.Max(peak, sample.localPosition.y);
        Assert.That(peak, Is.GreaterThan(.1f), "Aires must retain its authored jump height.");
        object[] planarArgs = { skill.skillClip.Clip, animator, null, null };
        Assert.That(cache.GetMethod("TryGet").Invoke(null, planarArgs), Is.True, planarArgs[3] as string);
        var planar = (TargetedSkillRootMotionTrajectory)planarArgs[2];
        Assert.That(planar, Is.Not.SameAs(trajectory), "Planar and full-motion cache entries must be separate.");
        foreach (var sample in planar.Samples) Assert.That(sample.localPosition.y, Is.EqualTo(0f));
        var actor = CreateObject("Aires placement probe", Vector3.forward * 8f).AddComponent<AllyContext>();
        var target = CreateObject("Target", Vector3.zero).transform;
        var profile = PlacementProfile();
        float time = skill.GetPlacementPointNormalized();
        Assert.That(PartyFollowUpPlacementResolver.TryResolve(actor, null, footprint, target,
            AITargetIdentity.Generic, profile, null, null, out var request, out var placement,
            trajectory.PlacementInput, time), Is.True);
        trajectory.TrySample(time, out var impact);
        Assert.That(Vector3.Distance(placement.StartPosition + placement.StartRotation * impact.localPosition,
            placement.ImpactPosition), Is.LessThan(.01f));
        Assert.That(request.ImpactNormalizedTime, Is.EqualTo(time));
        Assert.That(request.Animation.PreserveVerticalMotion, Is.True);
        Assert.That(placement.ImpactPosition.y - placement.StartPosition.y,
            Is.EqualTo(impact.localPosition.y).Within(.01f));
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(placement.ImpactPosition - origin, Vector3.up),
            Vector3.forward * profile.desiredRange), Is.LessThan(.01f));
    }
}
