#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode coverage for the MapRun stage intro: rig validation, fail-open behaviour, and the
/// owner-scoped control block token. Actor warping, agent restore, and the once-per-run MapRun
/// hook need live prefabs and stay Play Mode work.
/// </summary>
public sealed class StageIntroSmokeTests
{
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

    // ---- Rig validation -----------------------------------------------------------------------

    [Test]
    public void RigWithoutMarkersReportsEveryMissingRole()
    {
        StageIntroRig rig = CreateRig();

        string report = Describe(rig);

        Assert.That(report, Does.Contain("role 'Player'"));
        Assert.That(report, Does.Contain("role 'PartySlot1'"));
        Assert.That(report, Does.Contain("role 'PartySlot2'"));
        Assert.That(report, Does.Contain("role 'Helper'"));
    }

    [Test]
    public void DuplicateMarkerRoleIsReported()
    {
        StageIntroRig rig = CreateRig();
        AddMarker(rig, ChainActorRole.Player);
        AddMarker(rig, ChainActorRole.Player);
        AddMarker(rig, ChainActorRole.PartySlot1);
        AddMarker(rig, ChainActorRole.PartySlot2);
        AddMarker(rig, ChainActorRole.Helper);

        Assert.That(Describe(rig), Does.Contain("2 markers"));
    }

    [Test]
    public void MissingCameraClipIsReportedAndKeepsTheRigUnplayable()
    {
        StageIntroRig rig = CreateFullyMarkedRig();

        Assert.That(Describe(rig), Does.Contain("Camera Clip"));
        Assert.That(rig.IsPlayable(out _), Is.False);
        Assert.That(rig.IntroDuration, Is.EqualTo(0f));
    }

    [Test]
    public void UnplayableRigFailsOpenWithoutInvokingTheCallback()
    {
        StageIntroRig rig = CreateFullyMarkedRig();

        bool callbackInvoked = false;
        bool started = rig.TryPlay(null, () => callbackInvoked = true);

        Assert.That(started, Is.False, "A rig without a Camera Clip must not start the intro.");
        Assert.That(callbackInvoked, Is.False,
            "A refused TryPlay must not invoke the completion callback; the caller starts the room itself.");
        Assert.That(rig.IsPlaying, Is.False);
    }

    // ---- Scoped control blocks ----------------------------------------------------------------

    [Test]
    public void ScopedTokenAddsAndRemovesOnlyItsOwnBlocks()
    {
        StateHub hub = CreateStateHub();

        int token = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Shoot);
        Assert.That(token, Is.Not.EqualTo(0));
        Assert.That(hub.ActiveControlBlockFlags,
            Is.EqualTo(ControlBlockFlags.Move | ControlBlockFlags.Shoot));

        hub.ReleaseExternalControlBlockToken(token);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
    }

    [Test]
    public void ReleasingAScopedTokenDoesNotClearLegacyExternalBlocks()
    {
        StateHub hub = CreateStateHub();
        hub.AddExternalControlBlock(ControlBlockFlags.Skill);

        int token = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Skill);
        hub.ReleaseExternalControlBlockToken(token);

        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Skill),
            "A scoped release must never clear a block another system still owns.");
    }

    [Test]
    public void TwoScopedOwnersReleaseIndependently()
    {
        StateHub hub = CreateStateHub();

        int first = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move);
        int second = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Rotate);

        hub.ReleaseExternalControlBlockToken(first);
        Assert.That(hub.ActiveControlBlockFlags,
            Is.EqualTo(ControlBlockFlags.Move | ControlBlockFlags.Rotate));

        hub.ReleaseExternalControlBlockToken(second);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
    }

    [Test]
    public void ReleasingAnUnknownTokenIsANoOp()
    {
        StateHub hub = CreateStateHub();
        int token = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move);

        hub.ReleaseExternalControlBlockToken(token + 999);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Move));

        hub.ReleaseExternalControlBlockToken(token);
        hub.ReleaseExternalControlBlockToken(token);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
    }

    [Test]
    public void AcquiringNoFlagsReturnsAnInertToken()
    {
        StateHub hub = CreateStateHub();

        Assert.That(hub.AcquireExternalControlBlockToken(ControlBlockFlags.None), Is.EqualTo(0));
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.None));
    }

    // ---- Helpers ------------------------------------------------------------------------------

    StageIntroRig CreateRig()
    {
        var go = new GameObject("StageIntroRig");
        createdObjects.Add(go);
        return go.AddComponent<StageIntroRig>();
    }

    StageIntroRig CreateFullyMarkedRig()
    {
        StageIntroRig rig = CreateRig();
        AddMarker(rig, ChainActorRole.Player);
        AddMarker(rig, ChainActorRole.PartySlot1);
        AddMarker(rig, ChainActorRole.PartySlot2);
        AddMarker(rig, ChainActorRole.Helper);
        return rig;
    }

    static void AddMarker(StageIntroRig rig, ChainActorRole role)
    {
        var markerObject = new GameObject($"Marker_{role}");
        markerObject.transform.SetParent(rig.transform, false);
        markerObject.AddComponent<StageIntroActorMarker>().SetRoleForAuthoring(role);
    }

    static string Describe(StageIntroRig rig)
    {
        var issues = new List<string>();
        rig.CollectValidationIssues(issues);
        return string.Join(" | ", issues);
    }

    StateHub CreateStateHub()
    {
        var go = new GameObject("StateHub");
        createdObjects.Add(go);
        return go.AddComponent<StateHub>();
    }
}
#endif
