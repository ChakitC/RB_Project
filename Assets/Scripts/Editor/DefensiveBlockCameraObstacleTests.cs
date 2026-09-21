#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DefensiveBlockCameraObstacleTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new();
    readonly Vector3 origin = new Vector3(5000, 5000, 5000);
    GameplayCameraController camera;
    PlayerContext player;

    void Setup()
    {
        var rig = Track(new GameObject("Block camera query test"));
        rig.SetActive(false);
        camera = rig.AddComponent<GameplayCameraController>();
        var actor = Track(new GameObject("Protected Player"));
        actor.transform.position = origin + Vector3.forward;
        player = actor.AddComponent<PlayerContext>();
        actor.AddComponent<BoxCollider>().size = new Vector3(.4f, 2f, .2f);
        typeof(GameplayCameraController).GetField("playerContext", Hidden).SetValue(camera, player);
        typeof(GameplayCameraController).GetField("cameraCollisionMask", Hidden).SetValue(camera, (LayerMask)1);
        Box("Player nested model collider", 1.5f, player.transform);
    }

    GameObject Track(GameObject value) { objects.Add(value); value.hideFlags = HideFlags.HideAndDontSave; return value; }
    BoxCollider Box(string name, float distance, Transform parent = null)
    {
        var value = Track(new GameObject(name));
        value.transform.SetParent(parent);
        value.transform.position = origin + Vector3.forward * distance;
        var collider = value.AddComponent<BoxCollider>();
        collider.size = new Vector3(.4f, 2f, .2f);
        return collider;
    }

    bool Cast(out RaycastHit hit)
    {
        Physics.SyncTransforms();
        object[] args = { origin, Vector3.forward * 10, null };
        bool found = (bool)typeof(GameplayCameraController).GetMethod("TryGetDefensiveBlockObstacle", Hidden).Invoke(camera, args);
        hit = (RaycastHit)args[2];
        return found;
    }

    [TearDown] public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test] public void PlayerRootAndNestedModelDoNotShortenTheShot()
    {
        Setup();
        Assert.IsFalse(Cast(out _));
        // The filter belongs to this Player; other colliders on the same layer remain solid.
        typeof(GameplayCameraController).GetField("playerContext", Hidden).SetValue(camera, null);
        Assert.IsTrue(Cast(out var hit));
        Assert.That(hit.collider.gameObject, Is.SameAs(player.gameObject));
    }

    [Test] public void NearestSolidWallBehindPlayerStillShortensTheShot()
    {
        Setup();
        var far = Box("Far wall", 8);
        var near = Box("Near wall on same layer as Player", 4);
        Assert.IsTrue(Cast(out var hit));
        Assert.That(hit.collider, Is.SameAs(near));
        Assert.That(hit.distance, Is.InRange(3.7f, 3.8f));
        near.isTrigger = true;
        Assert.IsTrue(Cast(out hit));
        Assert.That(hit.collider, Is.SameAs(far));
        far.enabled = false;
        Assert.IsFalse(Cast(out _));
    }

    [Test] public void FullHitBufferDoesNotLoseTheWallBehindPlayer()
    {
        Setup();
        for (int i = 0; i < 24; i++) Box("Player attachment " + i, 2 + i * .04f, player.transform);
        var wall = Box("Wall behind dense Player hierarchy", 5);
        Assert.IsTrue(Cast(out var hit));
        Assert.That(hit.collider, Is.SameAs(wall));
    }

    [MenuItem("Tools/RB/Defensive Block/Run Camera Obstacle Tests")]
    public static void Run()
    {
        var tests = new DefensiveBlockCameraObstacleTests();
        try { tests.PlayerRootAndNestedModelDoNotShortenTheShot(); } finally { tests.Cleanup(); }
        try { tests.NearestSolidWallBehindPlayerStillShortensTheShot(); } finally { tests.Cleanup(); }
        try { tests.FullHitBufferDoesNotLoseTheWallBehindPlayer(); } finally { tests.Cleanup(); }
        Debug.Log("DefensiveBlock camera obstacles: 3 tests passed.");
    }
}
#endif
