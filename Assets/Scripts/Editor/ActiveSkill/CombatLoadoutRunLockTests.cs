#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

public sealed class CombatLoadoutRunLockTests
{
    [Test]
    public void RunSnapshotDoesNotChangeWhenSourceSelectionChanges()
    {
        var session = new MapRunSession();
        session.SetGraph(new MapGraph());
        var source = new List<CharacterSkillSelectionSaveData>
        {
            new CharacterSkillSelectionSaveData { slotId = "active", optionId = "active.a" }
        };

        List<CharacterSkillSelectionSaveData> first = session.GetOrCaptureLoadout("ID.test", source);
        source[0].optionId = "active.b";
        List<CharacterSkillSelectionSaveData> second = session.GetOrCaptureLoadout("ID.test", source);

        Assert.That(session.IsLoadoutLocked, Is.True);
        Assert.That(first[0].optionId, Is.EqualTo("active.a"));
        Assert.That(second[0].optionId, Is.EqualTo("active.a"));
    }

    [Test]
    public void ClearRunReleasesSnapshotForNextRun()
    {
        var session = new MapRunSession();
        var source = new List<CharacterSkillSelectionSaveData>
        {
            new CharacterSkillSelectionSaveData { slotId = "active", optionId = "active.a" }
        };

        session.SetGraph(new MapGraph());
        session.GetOrCaptureLoadout("ID.test", source);
        session.ClearRun();
        source[0].optionId = "active.b";
        session.SetGraph(new MapGraph());

        Assert.That(session.GetOrCaptureLoadout("ID.test", source)[0].optionId, Is.EqualTo("active.b"));
    }
}
#endif
