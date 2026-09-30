#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public sealed class DefensiveBlockPlayerRecoilTests
{
    static readonly Vector3 Origin = new Vector3(16000f, 0f, 16000f);
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> created = new();
    NavMeshDataInstance navMesh;
    GameObject actor;
    CharacterMovementMotor motor;
    GameObject Go(string name) { var go = new GameObject(name); created.Add(go); return go; }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Hidden).SetValue(o, value);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Hidden).Invoke(o, null);
    Vector3 Move(Vector3 delta) => (Vector3)typeof(DefensiveBlockController)
        .GetMethod("MovePlayerAlongNavMesh", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { motor, delta });

    [SetUp]
    public void SetUp()
    {
        var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(Origin + Vector3.down * .1f, Quaternion.identity, Vector3.one),
                size = new Vector3(12f, .2f, 12f), area = 0 }
        }, new Bounds(Origin, new Vector3(16f, 8f, 16f)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null); created.Add(data); navMesh = NavMesh.AddNavMeshData(data);
        var floor = Go("Recoil floor"); floor.transform.position = Origin + Vector3.down * .1f;
        floor.AddComponent<BoxCollider>().size = new Vector3(12f, .2f, 12f);
        actor = Go("Recoil player");
        Assert.That(NavMesh.SamplePosition(Origin, out var hit, 1f, NavMesh.AllAreas), Is.True);
        actor.transform.position = hit.position;
        var cc = actor.AddComponent<CharacterController>(); cc.center = Vector3.up; cc.height = 2; cc.radius = .3f;
        motor = actor.AddComponent<CharacterMovementMotor>(); motor.Configure(null, actor.transform, cc);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        if (navMesh.valid) navMesh.Remove();
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void FollowsRequestedPlanarDistance()
    {
        var delta = Move(Vector3.back * 1.2f);
        Assert.That(delta.z, Is.EqualTo(-1.2f).Within(.02f));
        Assert.That(Mathf.Abs(delta.y), Is.LessThan(.05f));
    }

    [Test]
    public void WallStopsPlayerBeforePenetration()
    {
        var wall = Go("Recoil wall"); wall.transform.position = Origin + new Vector3(0, 1, -1);
        wall.AddComponent<BoxCollider>().size = new Vector3(4, 2, .2f);
        Physics.SyncTransforms();
        var delta = Move(Vector3.back * 2);
        Assert.That(-delta.z, Is.InRange(.1f, .7f));
    }

    [Test]
    public void NavMeshEdgeCapsRecoil()
    {
        var delta = Move(Vector3.back * 20);
        Assert.That(-delta.z, Is.InRange(1f, 6f));
        Assert.That(NavMesh.SamplePosition(actor.transform.position, out _, .1f, NavMesh.AllAreas), Is.True);
    }

    [Test]
    public void MissingNavMeshDoesNotMovePlayer()
    {
        navMesh.Remove();
        Assert.That(Move(Vector3.back), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void RecoilCleanupPreservesOtherControlOwnersAndSkipsSelfGuard()
    {
        var player = actor.AddComponent<PlayerContext>(); player.cc = actor.GetComponent<CharacterController>();
        player.MovementMotor = motor; motor.Bind(player);
        var hub = actor.AddComponent<StateHub>(); player.stateHub = hub;
        var movement = actor.AddComponent<PlayerMovementCC>(); player.movement = movement;
        var allyObject = Go("Recoil ally"); allyObject.SetActive(false);
        var ally = allyObject.AddComponent<AllyContext>();
        var guard = allyObject.AddComponent<DefensiveBlockController>();
        Set(guard, "ctx", ally); Set(guard, "player", player);
        int other = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Rotate);
        Call(guard, "BeginPlayerRecoil");
        Call(guard, "BeginPlayerRecoil"); // Replacement releases its old tokens.
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Move | ControlBlockFlags.Rotate));
        var tokens = (HashSet<int>)typeof(PlayerMovementCC).GetField("suspensionTokens", Hidden).GetValue(movement);
        Assert.That(tokens.Count, Is.EqualTo(1));
        Set(guard, "active", true);
        guard.Cancel();
        Assert.That(tokens.Count, Is.EqualTo(0));
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Rotate));
        hub.ReleaseExternalControlBlockToken(other);
        Set(guard, "ctx", player); Set(guard, "player", player);
        Call(guard, "BeginPlayerRecoil");
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
    }
}
#endif
