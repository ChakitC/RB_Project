#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterProgressMigrationTests
{
    [Test]
    public void LegacyActiveSkillPointsMigrateWithoutLosingTreeOrSelectionData()
    {
        const string legacyJson =
            @"{""entries"":[{""characterId"":""ID.test"",""progress"":{""level"":7,""xp"":123,""unlocked"":true,""activeSkillPoints"":9,""activeSkillProgressInitialized"":true,""selectedSkillOptions"":[{""slotId"":""helper.proc.0"",""optionId"":""variant.a""}],""activeSkillTrees"":[{""slotId"":""helper.proc.0"",""optionId"":""variant.a"",""treeId"":""tree.a"",""unlockedNodes"":[{""nodeId"":""root"",""paidCost"":2}]}]}}]}";

        CharacterProgressSaveFile migrated =
            SaveDataMigration.LoadAndMigrateCharacterProgressSaveFile(legacyJson, out bool changed);

        Assert.That(changed, Is.True);
        Assert.That(migrated.schemaVersion, Is.EqualTo(CharacterProgressSaveFile.CurrentVersion));
        Assert.That(migrated.entries[0].progress.skillPoints, Is.EqualTo(9));
        Assert.That(migrated.entries[0].progress.skillProgressInitialized, Is.True);
        Assert.That(migrated.entries[0].progress.selectedSkillOptions.Count, Is.EqualTo(1));
        Assert.That(migrated.entries[0].progress.activeSkillTrees[0].unlockedNodes[0].paidCost, Is.EqualTo(2));
    }

    [Test]
    public void MigratingTheSameCharacterProgressTwiceIsIdempotent()
    {
        const string legacyJson =
            @"{""entries"":[{""characterId"":""ID.test"",""progress"":{""activeSkillPoints"":4,""activeSkillProgressInitialized"":true}}]}";

        CharacterProgressSaveFile first =
            SaveDataMigration.LoadAndMigrateCharacterProgressSaveFile(legacyJson, out _);
        string currentJson = JsonUtility.ToJson(first);

        CharacterProgressSaveFile second =
            SaveDataMigration.LoadAndMigrateCharacterProgressSaveFile(currentJson, out bool changed);

        Assert.That(changed, Is.False);
        Assert.That(second.entries[0].progress.skillPoints, Is.EqualTo(4));
        Assert.That(second.entries[0].progress.skillProgressInitialized, Is.True);
    }

    [Test]
    public void CurrentEntryIsNotOverwrittenByLegacyDefaultsFromAnotherEntry()
    {
        const string currentJson =
            @"{""entries"":[{""characterId"":""ID.legacy"",""progress"":{""activeSkillPoints"":4,""activeSkillProgressInitialized"":true}},{""characterId"":""ID.current"",""progress"":{""skillPoints"":12,""skillProgressInitialized"":true}}]}";

        CharacterProgressSaveFile file =
            SaveDataMigration.LoadAndMigrateCharacterProgressSaveFile(currentJson, out bool changed);

        Assert.That(changed, Is.True);
        Assert.That(file.entries[1].progress.skillPoints, Is.EqualTo(12));
        Assert.That(file.entries[1].progress.skillProgressInitialized, Is.True);
    }
}
#endif
