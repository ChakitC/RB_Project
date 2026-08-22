using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Covers the room-transition transaction: a transition either commits fully or leaves the run
/// exactly as it was, and the destination is never half-entered.
/// </summary>
public sealed class MapRunTransitionTests
{
    MapRunTestFixture fixture;

    [SetUp]
    public void SetUp()
    {
        // The failure paths deliberately log errors, which Edit Mode tests otherwise treat as
        // failures.
        fixture = new MapRunTestFixture();
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
        fixture?.Dispose();
        fixture = null;
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void FirstStartWarpFailureCommitsNoRoom()
    {
        fixture.WarpSucceeds = false;

        fixture.Controller.StartRun();

        Assert.That(fixture.Controller.CurrentGraph, Is.Not.Null, "The map should still be generated.");
        Assert.That(fixture.Controller.CurrentNode, Is.Null, "A failed first entry must not commit a node.");
        Assert.That(fixture.Controller.CurrentRoom, Is.Null, "A failed first entry must not commit a room.");
        Assert.That(fixture.Controller.HasActiveRoom, Is.False);
        Assert.That(fixture.Controller.IsTransitioning, Is.False, "The controller must not stay mid-transition.");
        Assert.That(fixture.CommittedCount, Is.Zero, "No commit event may fire for a failed entry.");
        Assert.That(fixture.RolledBackCount, Is.EqualTo(1));

        // MapGenerator marks the Start node visited when it builds the graph, so node state is not
        // the tell here. BeginRoom never running is: the room stays hidden and uncleared.
        RoomController[] rooms = fixture.Controller.GetComponentsInChildren<RoomController>(true);
        Assert.That(rooms.Length, Is.EqualTo(1), "Only the Start room should have been instantiated.");
        Assert.That(rooms[0].gameObject.activeSelf, Is.False, "The room the party never reached must be hidden.");
        Assert.That(rooms[0].RoomCleared, Is.False, "BeginRoom must not run for a failed entry.");
    }

    [Test]
    public void StartRoomCanBeRetriedAfterAFailedFirstEntry()
    {
        fixture.WarpSucceeds = false;
        fixture.Controller.StartRun();

        fixture.WarpSucceeds = true;
        bool retried = fixture.Controller.TryEnterStartRoom();

        Assert.That(retried, Is.True);
        Assert.That(fixture.Controller.CurrentNode, Is.Not.Null);
        Assert.That(fixture.Controller.CurrentNode.Id, Is.EqualTo(fixture.StartNodeId));
        Assert.That(fixture.Controller.HasActiveRoom, Is.True);
        Assert.That(fixture.CommittedCount, Is.EqualTo(1));
    }

    [Test]
    public void RetryIsRefusedOnceARoomIsCommitted()
    {
        fixture.Controller.StartRun();

        Assert.That(fixture.Controller.TryEnterStartRoom(), Is.False);
    }

    [Test]
    public void SuccessfulTransitionCommitsTheDestination()
    {
        fixture.Controller.StartRun();
        string targetId = fixture.FirstOutgoingNodeId();
        Assert.That(targetId, Is.Not.Null, "The Start room should have an outgoing node.");

        fixture.Controller.RequestTravelTo(targetId);

        Assert.That(fixture.Controller.CurrentNode.Id, Is.EqualTo(targetId));
        Assert.That(fixture.Controller.CurrentRoom, Is.Not.Null);
        Assert.That(fixture.Controller.IsTransitioning, Is.False);
        Assert.That(fixture.CommittedCount, Is.EqualTo(2), "Start entry plus one travel.");
        Assert.That(fixture.RolledBackCount, Is.Zero);
    }

    [Test]
    public void FailedTransitionRollsBackToThePreviousRoom()
    {
        fixture.Controller.StartRun();
        MapNode previousNode = fixture.Controller.CurrentNode;
        RoomController previousRoom = fixture.Controller.CurrentRoom;
        string targetId = fixture.FirstOutgoingNodeId();

        fixture.WarpSucceeds = false;
        fixture.Controller.RequestTravelTo(targetId);

        Assert.That(fixture.Controller.CurrentNode, Is.SameAs(previousNode), "The party must stay on its node.");
        Assert.That(fixture.Controller.CurrentRoom, Is.SameAs(previousRoom), "The previous room must be restored.");
        Assert.That(previousRoom.gameObject.activeSelf, Is.True, "The previous room must be active again.");
        Assert.That(fixture.Controller.IsTransitioning, Is.False);
        Assert.That(fixture.RolledBackCount, Is.EqualTo(1));
        Assert.That(fixture.CommittedCount, Is.EqualTo(1), "Only the Start entry may have committed.");
    }

    [Test]
    public void RollbackLeavesTheDestinationNodeUnvisited()
    {
        fixture.Controller.StartRun();
        string targetId = fixture.FirstOutgoingNodeId();
        MapNodeRevealState before = fixture.Controller.GetNode(targetId).State;

        fixture.WarpSucceeds = false;
        fixture.Controller.RequestTravelTo(targetId);

        MapNode target = fixture.Controller.GetNode(targetId);
        Assert.That(target.State, Is.EqualTo(before), "A rollback must not change node reveal state.");
        Assert.That(target.IsVisited, Is.False);
    }

    [Test]
    public void RollbackKeepsTheEncounterAndTemporaryContentOfThePreviousRoom()
    {
        fixture.Controller.StartRun();
        RoomController previousRoom = fixture.Controller.CurrentRoom;
        RoomRuntimeContent content = previousRoom.RuntimeContent;

        var encounterChild = new GameObject("Enemy");
        encounterChild.transform.SetParent(content.EncounterRoot, false);
        var temporaryChild = new GameObject("TemporaryVfx");
        temporaryChild.transform.SetParent(content.TemporaryRoot, false);

        fixture.WarpSucceeds = false;
        fixture.Controller.RequestTravelTo(fixture.FirstOutgoingNodeId());

        Assert.That(encounterChild == null, Is.False, "A rollback must not clear encounter content.");
        Assert.That(temporaryChild == null, Is.False, "A rollback must not clear temporary content.");
        Assert.That(content.EncounterRoot.childCount, Is.EqualTo(1));
        Assert.That(content.TemporaryRoot.childCount, Is.EqualTo(1));
    }

    [Test]
    public void FailedTransitionLeavesTheDestinationRoomInactive()
    {
        fixture.Controller.StartRun();
        string targetId = fixture.FirstOutgoingNodeId();

        fixture.WarpSucceeds = false;
        fixture.Controller.RequestTravelTo(targetId);

        Assert.That(fixture.Controller.CachedRoomCount, Is.EqualTo(2), "The destination room stays cached.");
        RoomController[] rooms = fixture.Controller.GetComponentsInChildren<RoomController>(true);
        int activeRooms = 0;
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i].gameObject.activeSelf)
                activeRooms++;
        }

        Assert.That(activeRooms, Is.EqualTo(1), "Exactly one room may be active after a rollback.");
    }

    [Test]
    public void RevisitedRoomReusesTheCachedInstance()
    {
        fixture.Controller.StartRun();
        string startId = fixture.Controller.CurrentNode.Id;
        RoomController startRoom = fixture.Controller.CurrentRoom;
        string targetId = fixture.FirstOutgoingNodeId();

        fixture.Controller.RequestTravelTo(targetId);
        fixture.Controller.RequestTravelTo(startId);

        Assert.That(fixture.Controller.CurrentNode.Id, Is.EqualTo(startId));
        Assert.That(fixture.Controller.CurrentRoom, Is.SameAs(startRoom), "Revisiting must reuse the cached room.");
        Assert.That(fixture.Controller.CachedRoomCount, Is.EqualTo(2), "No extra room instance may be created.");
    }
}
