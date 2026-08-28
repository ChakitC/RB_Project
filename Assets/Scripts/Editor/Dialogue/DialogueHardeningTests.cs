using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode coverage for the parts of the dialogue system that are pure data or pure resolution:
/// persisted play-once progress, which cast keys a sequence can put on stage, and how a trigger
/// decides which scene actors it supplies.
///
/// Everything that needs a live party, a paused world, or a portrait camera stays in the Play Mode
/// regression matrix — gameplay types live in `Assembly-CSharp` and tests live in
/// `Assembly-CSharp-Editor`, so a PlayMode-assembly test of them is not possible here.
/// </summary>
public sealed class DialogueHardeningTests
{
    readonly List<GameObject> spawned = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                UnityEngine.Object.DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
    }

    // ---------- DialogueProgressSaveFile ----------

    [Test]
    public void EmptyProgressFile_ReportsNothingCompleted()
    {
        var file = new DialogueProgressSaveFile();

        Assert.IsFalse(file.IsCompleted("Dialogue.Anything"));
        Assert.AreEqual(0, file.GetCompletedCount("Dialogue.Anything"));
    }

    [Test]
    public void MarkCompleted_CompletesAndCounts()
    {
        var file = new DialogueProgressSaveFile();

        file.MarkCompleted("Dialogue.Greeting");
        Assert.IsTrue(file.IsCompleted("Dialogue.Greeting"));
        Assert.AreEqual(1, file.GetCompletedCount("Dialogue.Greeting"));

        file.MarkCompleted("Dialogue.Greeting");
        Assert.AreEqual(2, file.GetCompletedCount("Dialogue.Greeting"));
    }

    [Test]
    public void MarkCompleted_IgnoresBlankIdsAndStampsSchema()
    {
        var file = new DialogueProgressSaveFile();

        file.MarkCompleted(null);
        file.MarkCompleted("   ");
        Assert.AreEqual(0, file.entries.Count);
        Assert.AreEqual(0, file.schemaVersion, "A no-op must not stamp the schema.");

        file.MarkCompleted("Dialogue.Real");
        Assert.AreEqual(DialogueProgressSaveFile.CurrentVersion, file.schemaVersion);
    }

    [Test]
    public void NullEntriesInFile_DoNotThrow()
    {
        // A hand-edited or partially written file can deserialize with null members in the list.
        var file = new DialogueProgressSaveFile();
        file.entries.Add(null);
        file.entries.Add(new DialogueProgressEntry { dialogueId = "Dialogue.Real", completedCount = 1 });

        Assert.IsTrue(file.IsCompleted("Dialogue.Real"));
        Assert.IsFalse(file.IsCompleted("Dialogue.Missing"));
    }

    [Test]
    public void DialogueIdMatching_IsOrdinal()
    {
        var file = new DialogueProgressSaveFile();
        file.MarkCompleted("Dialogue.Greeting");

        Assert.IsTrue(file.IsCompleted("Dialogue.Greeting"));
        Assert.IsFalse(
            file.IsCompleted("dialogue.greeting"),
            "Ids are matched ordinally; changing that silently merges distinct conversations.");
    }

    [Test]
    public void NormalizeSchemaVersion_ReportsOnlyTheFirstUpgrade()
    {
        var file = new DialogueProgressSaveFile();

        Assert.IsTrue(file.NormalizeSchemaVersion(), "A pre-versioning file must report an upgrade.");
        Assert.AreEqual(DialogueProgressSaveFile.CurrentVersion, file.schemaVersion);
        Assert.IsFalse(file.NormalizeSchemaVersion(), "An already-current file must not rewrite.");
    }

    // ---------- sequence traversal ----------

    [Test]
    public void AppearingCastKeys_IncludeOpeningCast()
    {
        DialogueSequenceSO sequence = BuildSequence(
            castIds: new[] { "role.Player", "npc.abbygail" },
            stageChangeIds: null);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sequence.CollectAppearingCastKeys(keys);

        CollectionAssert.AreEquivalent(new[] { "role.Player", "npc.abbygail" }, keys);
    }

    [Test]
    public void AppearingCastKeys_IncludeStageChangeEntrants()
    {
        DialogueSequenceSO sequence = BuildSequence(
            castIds: new[] { "role.Player" },
            stageChangeIds: new[] { "role.PartySlot1" });

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sequence.CollectAppearingCastKeys(keys);

        Assert.IsTrue(
            keys.Contains("role.PartySlot1"),
            "A character who only walks on mid-conversation still needs a source and a profile.");
    }

    [Test]
    public void AppearingCastKeys_IgnoreClearChanges()
    {
        DialogueSequenceSO sequence = BuildSequence(
            castIds: new[] { "role.Player" },
            stageChangeIds: new[] { "" });   // empty characterId == clear the slot

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sequence.CollectAppearingCastKeys(keys);

        CollectionAssert.AreEquivalent(new[] { "role.Player" }, keys);
    }

    // ---------- trigger actor resolution ----------

    [Test]
    public void ResolveStageActors_DiscoversInactiveChildSources()
    {
        DialogueTrigger trigger = BuildTrigger(out GameObject root);

        GameObject child = new GameObject("NPC");
        child.transform.SetParent(root.transform, false);
        child.SetActive(false);
        DialogueStageActorSource source = child.AddComponent<DialogueStageActorSource>();

        IReadOnlyList<DialogueStageActorSource> resolved = trigger.ResolveStageActors();

        Assert.AreEqual(1, resolved.Count);
        Assert.AreSame(source, resolved[0], "An inactive self-casting NPC must still be found.");
    }

    [Test]
    public void ResolveStageActors_PrefersTheExplicitList()
    {
        DialogueTrigger trigger = BuildTrigger(out GameObject root);

        GameObject child = new GameObject("DiscoveredNPC");
        child.transform.SetParent(root.transform, false);
        child.AddComponent<DialogueStageActorSource>();

        GameObject listed = new GameObject("ListedNPC");
        spawned.Add(listed);
        DialogueStageActorSource explicitSource = listed.AddComponent<DialogueStageActorSource>();

        SetSerialized(trigger, "stageActors", new List<DialogueStageActorSource> { explicitSource });

        IReadOnlyList<DialogueStageActorSource> resolved = trigger.ResolveStageActors();

        Assert.AreEqual(1, resolved.Count);
        Assert.AreSame(
            explicitSource, resolved[0],
            "An explicit list is an authoring decision and must not be merged with discovery.");
    }

    [Test]
    public void ResolveStageActors_IsStableAcrossCalls()
    {
        // Runtime and the editor validator both call this; they must never disagree.
        DialogueTrigger trigger = BuildTrigger(out GameObject root);

        GameObject child = new GameObject("NPC");
        child.transform.SetParent(root.transform, false);
        child.AddComponent<DialogueStageActorSource>();

        Assert.AreEqual(trigger.ResolveStageActors().Count, trigger.ResolveStageActors().Count);
    }

    [Test]
    public void DuplicateActorIds_AreDetectable()
    {
        DialogueTrigger trigger = BuildTrigger(out GameObject root);

        AddActor(root, "npc.abbygail");
        AddActor(root, "npc.abbygail");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicates = 0;
        foreach (DialogueStageActorSource actor in trigger.ResolveStageActors())
        {
            if (!seen.Add(actor.CharacterId))
                duplicates++;
        }

        Assert.AreEqual(
            1, duplicates,
            "Runtime keys sources by id, so a duplicate silently hides one NPC entirely.");
    }

    // ---------- cast key helpers ----------

    [Test]
    public void RoleKeys_ParseAndRoundTrip()
    {
        Assert.IsTrue(DialogueCastKeys.IsRoleKey(DialogueCastKeys.PartySlot1));
        Assert.IsFalse(DialogueCastKeys.IsRoleKey("npc.abbygail"));
        Assert.IsFalse(DialogueCastKeys.IsRoleKey("ID.Roma"));

        Assert.IsTrue(DialogueCastKeys.TryParseRole(DialogueCastKeys.Helper, out ChainActorRole role));
        Assert.AreEqual(ChainActorRole.Helper, role);

        Assert.IsFalse(DialogueCastKeys.TryParseRole("role.NotARole", out _));
        Assert.IsFalse(DialogueCastKeys.TryParseRole("ID.Roma", out _));
    }

    // ---------- helpers ----------

    DialogueTrigger BuildTrigger(out GameObject root)
    {
        root = new GameObject("Trigger");
        spawned.Add(root);
        return root.AddComponent<DialogueTrigger>();
    }

    void AddActor(GameObject root, string characterId)
    {
        GameObject child = new GameObject(characterId);
        child.transform.SetParent(root.transform, false);
        DialogueStageActorSource source = child.AddComponent<DialogueStageActorSource>();
        SetSerialized(source, "characterId", characterId);
    }

    static DialogueSequenceSO BuildSequence(string[] castIds, string[] stageChangeIds)
    {
        var sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();

        var cast = new List<DialogueCastEntry>();
        for (int i = 0; castIds != null && i < castIds.Length; i++)
            cast.Add(new DialogueCastEntry { characterId = castIds[i] });

        var line = new DialogueLine { stageChanges = new List<DialogueStageChange>() };
        for (int i = 0; stageChangeIds != null && i < stageChangeIds.Length; i++)
            line.stageChanges.Add(new DialogueStageChange { characterId = stageChangeIds[i] });

        SetSerialized(sequence, "cast", cast);
        SetSerialized(sequence, "lines", new List<DialogueLine> { line });
        return sequence;
    }

    /// <summary>
    /// Writes a private serialized field. These fields are prefab/scene-facing and deliberately not
    /// public; a test setter on the production type would be a worse trade than reflection here.
    /// </summary>
    static void SetSerialized(UnityEngine.Object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(field, $"'{target.GetType().Name}' has no serialized field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
