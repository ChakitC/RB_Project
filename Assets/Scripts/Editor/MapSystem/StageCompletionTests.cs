using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Covers the Stage Exit hand-off. Completing a stage run is a single transaction: it is either
/// accepted — granting XP, saving progress, and returning to the Basement — or refused outright,
/// leaving the portal usable so nothing is lost.
///
/// Edit Mode has no <c>SaveManager</c> and no <c>SceneLoaderSystem</c> singleton, so these tests
/// cover the refusal half. The accepted half needs Play Mode.
/// </summary>
public sealed class StageCompletionTests
{
    MapRunTestFixture fixture;

    [SetUp]
    public void SetUp()
    {
        fixture = new MapRunTestFixture(asTestStage: true);
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
    public void ClearingTheBossOpensTheStageExit()
    {
        StageExitInteractable stageExit = RunToClearedBoss();

        Assert.That(fixture.Controller.CanCompleteStageRun, Is.True);
        Assert.That(stageExit, Is.Not.Null, "The Stage Exit portal should be spawned in the Boss room.");
        Assert.That(stageExit.CanInteract(null), Is.True);
    }

    [Test]
    public void MissingSaveOrSceneLoaderRefusesTheCompletionWithoutCrashing()
    {
        RunToClearedBoss();

        bool completed = fixture.Controller.TryCompleteStageRunAndReturn();

        Assert.That(completed, Is.False, "Completion must be refused while its dependencies are missing.");
        Assert.That(fixture.Controller.CanCompleteStageRun, Is.True, "A refused completion must not be committed.");
    }

    [Test]
    public void RefusedCompletionLeavesThePortalUsable()
    {
        StageExitInteractable stageExit = RunToClearedBoss();

        stageExit.Interact(null);

        Assert.That(stageExit.CanInteract(null), Is.True, "The portal must stay usable after a refused completion.");
        Assert.That(fixture.Controller.CanCompleteStageRun, Is.True);
    }

    [Test]
    public void RepeatedInteractionsNeverCommitTheRunTwice()
    {
        StageExitInteractable stageExit = RunToClearedBoss();

        for (int i = 0; i < 5; i++)
            stageExit.Interact(null);

        Assert.That(fixture.Controller.CanCompleteStageRun, Is.True, "Nothing may be granted while completion is refused.");
    }

    /// <summary>Walks Start -> Combat -> Boss and returns the portal the cleared Boss spawned.</summary>
    StageExitInteractable RunToClearedBoss()
    {
        fixture.Controller.StartRun();
        Assert.That(fixture.Controller.CurrentNode, Is.Not.Null, "The run should have entered the Start room.");

        for (int guard = 0; guard < 8 && fixture.Controller.CurrentNode.Type != MapNodeType.Boss; guard++)
        {
            string nextId = fixture.FirstOutgoingNodeId();
            Assert.That(nextId, Is.Not.Null, "The critical path should reach the Boss node.");
            fixture.Controller.RequestTravelTo(nextId);
        }

        Assert.That(fixture.Controller.CurrentNode.Type, Is.EqualTo(MapNodeType.Boss));
        return fixture.Controller.CurrentRoom.GetComponentInChildren<StageExitInteractable>(true);
    }
}
