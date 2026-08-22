using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Room-specific behaviour is authored on the room prefab and driven by <see cref="RoomController"/>
/// through <see cref="IRoomLifecycleListener"/>. These tests cover the hook using the behaviour that
/// motivated it: the Test Stage heal and ammo stations, which used to live inside the generic
/// controller.
/// </summary>
public sealed class RoomLifecycleListenerTests
{
    MapRunTestFixture fixture;
    GameObject roomObject;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (roomObject != null)
            Object.DestroyImmediate(roomObject);
        roomObject = null;

        fixture?.Dispose();
        fixture = null;
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void HealRoomInATestStageGetsItsRecoveryStations()
    {
        RoomController room = BuildRoom(asTestStage: true, withRecoveryStations: true);

        room.Initialize(fixture.Controller, new MapNode("heal", MapNodeType.Heal, 1, false));

        Assert.That(room.GetComponentInChildren<HealInteractable>(true), Is.Not.Null, "Missing heal station.");
        Assert.That(room.GetComponentInChildren<AmmoRefillInteractable>(true), Is.Not.Null, "Missing ammo station.");
    }

    [Test]
    public void RoomWithoutTheListenerGetsNoRecoveryStations()
    {
        RoomController room = BuildRoom(asTestStage: true, withRecoveryStations: false);

        room.Initialize(fixture.Controller, new MapNode("heal", MapNodeType.Heal, 1, false));

        Assert.That(
            room.GetComponentInChildren<HealInteractable>(true),
            Is.Null,
            "The generic room controller must not create stations on its own.");
    }

    [Test]
    public void NonHealNodeGetsNoRecoveryStations()
    {
        RoomController room = BuildRoom(asTestStage: true, withRecoveryStations: true);

        room.Initialize(fixture.Controller, new MapNode("combat", MapNodeType.Combat, 1, false));

        Assert.That(room.GetComponentInChildren<HealInteractable>(true), Is.Null);
    }

    [Test]
    public void HealRoomOutsideATestStageGetsNoRecoveryStations()
    {
        RoomController room = BuildRoom(asTestStage: false, withRecoveryStations: true);

        room.Initialize(fixture.Controller, new MapNode("heal", MapNodeType.Heal, 1, false));

        Assert.That(room.GetComponentInChildren<HealInteractable>(true), Is.Null);
    }

    RoomController BuildRoom(bool asTestStage, bool withRecoveryStations)
    {
        fixture = new MapRunTestFixture(asTestStage);
        fixture.Controller.StartRun();

        roomObject = new GameObject("LifecycleRoom");
        RoomController room = roomObject.AddComponent<RoomController>();
        if (withRecoveryStations)
            roomObject.AddComponent<TestStageRecoveryStations>();

        return room;
    }
}
