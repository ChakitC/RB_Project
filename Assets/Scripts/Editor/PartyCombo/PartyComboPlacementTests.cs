using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class PartyComboPlacementTests
{
    readonly List<Object> created = new();
    readonly Vector3 origin = new Vector3(10000f, 0f, 10000f);
    Camera camera;
    NavMeshDataInstance navMesh;
    CharacterPlacementFootprint footprint;

    [SetUp]
    public void SetUp()
    {
        camera = CreateObject("Combo camera", new Vector3(0f, 1f, -8f)).AddComponent<Camera>();
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

    PartyComboPlacementVisibility Evaluate(Vector3 position, Transform ignored = null)
    {
        return PartyComboPlacementVisibility.Evaluate(camera, origin + position, Quaternion.identity,
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
        AllyContext actor = CreateObject("Combo ally", new Vector3(0f, 0f, 4f)).AddComponent<AllyContext>();
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
        var profile = ScriptableObject.CreateInstance<PartyComboExecutionProfile>();
        created.Add(profile);
        Assert.That(PartyComboPlacementResolver.TryResolve(actor, body, footprint, target.transform,
            AITargetIdentity.Generic, profile, camera, null, out _, out CharacterPlacementResult result), Is.True);
        Assert.That(Evaluate(result.StartPosition - origin).VisibleSamples, Is.EqualTo(5));
        Assert.That(NavMesh.SamplePosition(result.StartPosition, out _, 0.1f, NavMesh.AllAreas), Is.True);
        Assert.That(result.Score.MaxWorldPenetration, Is.Zero);
        if (withCharacterController)
        {
            Blocker(new Vector3(10f, 4f, 10f));
            Assert.That(PartyComboPlacementResolver.TryResolve(actor, body, footprint, target.transform,
                AITargetIdentity.Generic, profile, camera, null, out _, out _), Is.False,
                "Actual wall penetration must still reject every blocked destination.");
        }
    }
}
