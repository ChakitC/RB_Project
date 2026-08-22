using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The transition sweep is global by necessity — pooled projectiles live outside the room — so what
/// keeps it safe is the scope. Content that belongs to the party or to a cached room must survive
/// it; a cached room full of uncollected drops is the whole reason revisiting a node is worth
/// anything.
/// </summary>
public sealed class RoomTransitionCleanupTests
{
    readonly List<GameObject> created = new();

    [SetUp]
    public void SetUp()
    {
        // Object.Destroy is not allowed in Edit Mode; the sweep logs and moves on, and the
        // SetActive(false) it performs first is what these tests observe.
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void LoosePickupsAreSweptButRoomAndPartyContentSurvives()
    {
        GameObject roomRoot = CreateRoot("Room");
        GameObject partyRoot = CreateRoot("Party");
        GameObject roomPickup = CreatePickup("RoomPickup", roomRoot.transform);
        GameObject partyPickup = CreatePickup("PartyPickup", partyRoot.transform);
        GameObject loosePickup = CreatePickup("LoosePickup", null);

        Sweep(partyRoot.transform, roomRoot.transform);

        Assert.That(IsSwept(loosePickup), Is.True, "A pickup lying in the world belongs to the transition.");
        Assert.That(IsSwept(roomPickup), Is.False, "A cached room keeps the drops the player left in it.");
        Assert.That(IsSwept(partyPickup), Is.False, "Party-owned content survives a room transition.");
    }

    [Test]
    public void NestedRoomContentIsRecognisedByItsRoot()
    {
        GameObject roomRoot = CreateRoot("Room");
        var runtimeContent = new GameObject("RuntimeContent");
        runtimeContent.transform.SetParent(roomRoot.transform, false);
        var persistent = new GameObject("Persistent");
        persistent.transform.SetParent(runtimeContent.transform, false);

        GameObject deepPickup = CreatePickup("DeepPickup", persistent.transform);

        Sweep(null, roomRoot.transform);

        Assert.That(IsSwept(deepPickup), Is.False, "Ownership is by hierarchy, at any depth.");
    }

    [Test]
    public void EverythingIsSweptWhenNothingIsScoped()
    {
        GameObject pickup = CreatePickup("LoosePickup", null);

        Sweep(null);

        Assert.That(IsSwept(pickup), Is.True);
    }

    static void Sweep(Transform partyRoot, params Transform[] roomRoots)
    {
        RoomTransitionCleanup.ClearTransientWorldObjects(
            new RoomTransitionCleanupScope(partyRoot, new List<Transform>(roomRoots)));
    }

    static bool IsSwept(GameObject instance)
    {
        return instance == null || !instance.activeSelf;
    }

    GameObject CreateRoot(string name)
    {
        var root = new GameObject(name);
        created.Add(root);
        return root;
    }

    GameObject CreatePickup(string name, Transform parent)
    {
        var pickup = new GameObject(name);
        // SkillPickup requires a Collider, and Collider itself is abstract, so one is added first.
        pickup.AddComponent<SphereCollider>();
        pickup.AddComponent<SkillPickup>();
        if (parent != null)
            pickup.transform.SetParent(parent, false);
        else
            created.Add(pickup);

        return pickup;
    }
}
