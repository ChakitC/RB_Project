using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Stage progress is keyed by Stage Id, so renaming a stage is a save-compatibility event. These
/// tests pin the alias migration and the schema version that identifies a pre-versioning file.
/// </summary>
public sealed class StageProgressSchemaTests
{
    [Test]
    public void FileWrittenBeforeVersioningReadsAsVersionZero()
    {
        var legacy = JsonUtility.FromJson<StageProgressSaveFile>("{\"entries\":[{\"stageId\":\"a\",\"progressCount\":2}]}");

        Assert.That(legacy.schemaVersion, Is.Zero, "A missing key must be recognisable as an old file.");
        Assert.That(legacy.GetProgress("a"), Is.EqualTo(2));
        Assert.That(legacy.NormalizeSchemaVersion(), Is.True);
        Assert.That(legacy.schemaVersion, Is.EqualTo(StageProgressSaveFile.CurrentVersion));
        Assert.That(legacy.NormalizeSchemaVersion(), Is.False, "Normalising twice must not report a second change.");
    }

    [Test]
    public void WritingProgressStampsTheCurrentSchemaVersion()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("stage_a", 1);

        Assert.That(file.schemaVersion, Is.EqualTo(StageProgressSaveFile.CurrentVersion));
    }

    [Test]
    public void ProgressSavedUnderALegacyIdIsAdopted()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("test_stage_01", 2);

        Assert.That(file.GetProgress("stage_one"), Is.Zero, "The new id has nothing of its own yet.");
        Assert.That(file.GetProgress("stage_one", Ids("test_stage_01")), Is.EqualTo(2));
    }

    [Test]
    public void MigrationRewritesTheLegacyEntryToTheCurrentId()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("test_stage_01", 2);

        Assert.That(file.MigrateStageId("stage_one", Ids("test_stage_01")), Is.True);
        Assert.That(file.GetProgress("stage_one"), Is.EqualTo(2));
        Assert.That(file.GetProgress("test_stage_01"), Is.Zero, "The legacy row is gone once migrated.");
        Assert.That(file.entries.Count, Is.EqualTo(1), "Migration must not duplicate the stage.");
    }

    [Test]
    public void MigrationKeepsTheFurthestProgressWhenBothIdsExist()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("stage_one", 1);
        file.SetProgress("test_stage_01", 3);

        Assert.That(file.MigrateStageId("stage_one", Ids("test_stage_01")), Is.True);
        Assert.That(file.GetProgress("stage_one"), Is.EqualTo(3), "A rename must never cost the player progress.");
        Assert.That(file.entries.Count, Is.EqualTo(1));
    }

    [Test]
    public void MigrationIsIdempotentAndSilentWhenThereIsNothingToDo()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("stage_one", 2);

        Assert.That(file.MigrateStageId("stage_one", Ids("test_stage_01")), Is.False);
        Assert.That(file.MigrateStageId("stage_one", Ids("stage_one")), Is.False, "Its own id is not a legacy id.");
        Assert.That(file.MigrateStageId("stage_one", null), Is.False);
        Assert.That(file.GetProgress("stage_one"), Is.EqualTo(2));
    }

    [Test]
    public void CurrentIdWinsOverALegacyId()
    {
        var file = new StageProgressSaveFile();
        file.SetProgress("stage_one", 1);
        file.SetProgress("test_stage_01", 3);

        Assert.That(file.GetProgress("stage_one", Ids("test_stage_01")), Is.EqualTo(1));
    }

    static IReadOnlyList<string> Ids(params string[] ids) => ids;
}
