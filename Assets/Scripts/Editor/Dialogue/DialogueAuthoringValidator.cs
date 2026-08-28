using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project-wide authoring check for the dialogue system: sequences, pose profiles, the profile
/// database, the presentation stage, and every <see cref="DialogueTrigger"/> in the open scenes.
///
/// Catches the failures that only show up at runtime otherwise — a duplicate dialogueId silently
/// sharing play-once completion, a line spoken by someone who is not in the cast, a cast member with
/// no pose profile, or a missing DialogueActor layer.
/// </summary>
public static class DialogueAuthoringValidator
{
    [MenuItem("Tools/Dialogue/Validate Dialogue Authoring")]
    public static void Validate()
    {
        var issues = new List<string>();

        DialogueSequenceSO[] sequences = LoadAll<DialogueSequenceSO>();
        CharacterDialogueAnimationProfileSO[] profiles = LoadAll<CharacterDialogueAnimationProfileSO>();
        DialogueProfileDatabaseSO[] databases = LoadAll<DialogueProfileDatabaseSO>();

        ValidateSequences(sequences, issues);
        ValidateProfiles(profiles, issues);
        ValidateDatabases(databases, issues);
        ValidateCastCoverage(sequences, databases, issues);
        ValidateOpenScenes(issues);
        ValidateProjectSetup(issues);

        if (issues.Count == 0)
        {
            Debug.Log(
                $"[Dialogue] Authoring OK — {sequences.Length} sequence(s), {profiles.Length} pose " +
                $"profile(s), {databases.Length} database(s).");
            return;
        }

        Debug.LogWarning($"[Dialogue] {issues.Count} authoring issue(s):\n- {string.Join("\n- ", issues)}");
    }

    static void ValidateSequences(DialogueSequenceSO[] sequences, List<string> issues)
    {
        var idOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < sequences.Length; i++)
        {
            DialogueSequenceSO sequence = sequences[i];
            if (sequence == null)
                continue;

            sequence.CollectValidationIssues(issues);

            string id = sequence.DialogueId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // Two sequences sharing an id share their play-once completion, so finishing one silently
            // locks the other out.
            if (idOwners.TryGetValue(id, out string owner))
                issues.Add($"dialogueId '{id}' is used by both '{owner}' and '{sequence.name}'.");
            else
                idOwners[id] = sequence.name;
        }
    }

    static void ValidateProfiles(CharacterDialogueAnimationProfileSO[] profiles, List<string> issues)
    {
        for (int i = 0; i < profiles.Length; i++)
            profiles[i]?.CollectValidationIssues(issues);
    }

    static void ValidateDatabases(DialogueProfileDatabaseSO[] databases, List<string> issues)
    {
        if (databases.Length == 0)
        {
            issues.Add("No DialogueProfileDatabase exists; no actor can be posed.");
            return;
        }

        if (databases.Length > 1)
        {
            issues.Add(
                $"{databases.Length} DialogueProfileDatabase assets exist. The stage references one, " +
                "so poses authored in the others are never used.");
        }

        for (int i = 0; i < databases.Length; i++)
            databases[i]?.CollectValidationIssues(issues);
    }

    /// <summary>Reports cast members that no database can pose — they would stand un-posed on stage.</summary>
    static void ValidateCastCoverage(
        DialogueSequenceSO[] sequences, DialogueProfileDatabaseSO[] databases, List<string> issues)
    {
        if (databases.Length == 0)
            return;

        for (int s = 0; s < sequences.Length; s++)
        {
            DialogueSequenceSO sequence = sequences[s];
            IReadOnlyList<DialogueCastEntry> cast = sequence != null ? sequence.Cast : null;
            if (cast == null)
                continue;

            // Everyone who can stand on stage needs a profile, not just the opening cast — a
            // character swapped in mid-conversation would otherwise reach the stage un-posed.
            var appearing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < cast.Count; c++)
            {
                DialogueCastEntry entry = cast[c];
                if (entry != null && entry.IsValid)
                    appearing.Add(entry.characterId);
            }

            IReadOnlyList<DialogueLine> lines = sequence.Lines;
            for (int l = 0; lines != null && l < lines.Count; l++)
            {
                DialogueLine line = lines[l];
                for (int c = 0; line != null && line.HasStageChanges && c < line.stageChanges.Count; c++)
                {
                    DialogueStageChange change = line.stageChanges[c];
                    if (change != null && !change.IsClear)
                        appearing.Add(change.characterId);
                }
            }

            foreach (string key in appearing)
            {
                // A role key is filled by whoever the player deployed, so which profile it needs
                // cannot be known here — only that the role itself is real. Every party character
                // needs a profile for role casting to be safe, which the per-profile checks cover.
                if (DialogueCastKeys.IsRoleKey(key))
                {
                    if (!DialogueCastKeys.TryParseRole(key, out _))
                    {
                        issues.Add(
                            $"'{sequence.name}': '{key}' is not a party role. Valid keys are " +
                            $"'{DialogueCastKeys.Player}', '{DialogueCastKeys.PartySlot1}', " +
                            $"'{DialogueCastKeys.PartySlot2}', '{DialogueCastKeys.Helper}'.");
                    }

                    continue;
                }

                bool covered = false;
                for (int d = 0; d < databases.Length && !covered; d++)
                    covered = databases[d] != null && databases[d].Find(key) != null;

                if (!covered)
                {
                    issues.Add(
                        $"'{sequence.name}': no dialogue pose profile is registered for " +
                        $"'{key}', who appears on stage.");
                }
            }
        }
    }

    static void ValidateOpenScenes(List<string> issues)
    {
        DialogueStage[] stages = UnityEngine.Object.FindObjectsByType<DialogueStage>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < stages.Length; i++)
            stages[i]?.CollectValidationIssues(issues);

        DialogueDirector[] directors = UnityEngine.Object.FindObjectsByType<DialogueDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (directors.Length > 1)
            issues.Add($"{directors.Length} DialogueDirectors are loaded; only one may own the stage.");

        for (int i = 0; i < directors.Length; i++)
            directors[i]?.CollectValidationIssues(issues);

        DialogueTrigger[] triggers = UnityEngine.Object.FindObjectsByType<DialogueTrigger>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < triggers.Length; i++)
        {
            DialogueTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            if (trigger.Sequence == null)
            {
                issues.Add($"DialogueTrigger '{GetScenePath(trigger.transform)}' has no sequence.");
                continue;
            }

            if (trigger.Repeat == DialogueTrigger.RepeatMode.PlayOnce &&
                string.IsNullOrWhiteSpace(trigger.Sequence.DialogueId))
            {
                issues.Add(
                    $"DialogueTrigger '{GetScenePath(trigger.transform)}' is play-once but its " +
                    "sequence has no dialogueId, so completion cannot be persisted.");
            }

            ValidateStageActors(trigger, issues);
        }
    }

    /// <summary>
    /// Scene actors a trigger supplies are only useful when the sequence actually casts their id, and
    /// an id nobody casts is silently ignored at runtime — which looks exactly like a broken NPC.
    /// </summary>
    static void ValidateStageActors(DialogueTrigger trigger, List<string> issues)
    {
        IReadOnlyList<DialogueStageActorSource> actors = trigger.StageActors;
        string path = GetScenePath(trigger.transform);

        for (int i = 0; actors != null && i < actors.Count; i++)
        {
            DialogueStageActorSource actor = actors[i];
            if (actor == null)
            {
                issues.Add($"DialogueTrigger '{path}' has an empty stage actor slot.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(actor.CharacterId))
            {
                issues.Add(
                    $"DialogueStageActorSource on '{GetScenePath(actor.transform)}' has no " +
                    "characterId, so no sequence can cast it.");
                continue;
            }

            if (actor.ModelRoot == null)
            {
                issues.Add(
                    $"DialogueStageActorSource '{actor.CharacterId}' has no model root to clone.");
                continue;
            }

            if (trigger.Sequence != null && trigger.Sequence.FindCastEntry(actor.CharacterId) == null)
            {
                issues.Add(
                    $"DialogueTrigger '{path}' supplies stage actor '{actor.CharacterId}', but " +
                    $"'{trigger.Sequence.name}' does not cast that id — it will never appear.");
            }
        }
    }

    static void ValidateProjectSetup(List<string> issues)
    {
        if (LayerMask.NameToLayer(DialogueLayers.ActorLayerName) < 0)
        {
            issues.Add(
                $"Layer '{DialogueLayers.ActorLayerName}' is missing. " +
                "Run Tools/Dialogue/Set Up Project Layers.");
        }

        bool sceneInBuild = false;
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length && !sceneInBuild; i++)
        {
            sceneInBuild = scenes[i] != null &&
                           scenes[i].enabled &&
                           scenes[i].path.EndsWith($"/{DialoguePresentationScene.SceneName}.unity",
                                                   StringComparison.Ordinal);
        }

        if (!sceneInBuild)
        {
            issues.Add(
                $"Scene '{DialoguePresentationScene.SceneName}' is not enabled in Build Settings; " +
                "it cannot be loaded additively at runtime.");
        }
    }

    static T[] LoadAll<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var results = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            results[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
        }

        return results;
    }

    static string GetScenePath(Transform target)
    {
        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }
}
