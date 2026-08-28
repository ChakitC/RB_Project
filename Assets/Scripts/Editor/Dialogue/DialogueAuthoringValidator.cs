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
    /// <summary>
    /// Id prefix that marks a cast key as a scene-authored actor rather than a party member. Only
    /// these can be checked against a trigger, because a party role resolves from the live party.
    /// </summary>
    const string ScenePrefix = "npc.";

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
        ValidateAspRenderingLayers(issues);

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

            // A required role key cannot be proven here: whether role.PartySlot2 resolves depends on
            // the party the player deployed, which only exists at runtime. Static validation can only
            // point out that the conversation will refuse to start when it does not.
            for (int c = 0; c < cast.Count; c++)
            {
                DialogueCastEntry entry = cast[c];
                if (entry == null || !entry.IsValid || entry.optional)
                    continue;

                if (DialogueCastKeys.IsRoleKey(entry.characterId))
                {
                    issues.Add(
                        $"'{sequence.name}': cast entry '{entry.characterId}' is required " +
                        "(optional off). If the player deploys a party without that slot filled, the " +
                        "conversation will refuse to start. Mark it optional unless the lines make " +
                        "no sense without that character.");
                }
            }

            // Everyone who can stand on stage needs a profile, not just the opening cast — a
            // character swapped in mid-conversation would otherwise reach the stage un-posed.
            var appearing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            sequence.CollectAppearingCastKeys(appearing);

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
    /// Checks a trigger's scene actors against the ids its sequence can actually put on stage.
    ///
    /// Resolution goes through <see cref="DialogueTrigger.ResolveStageActors"/> — the same call the
    /// runtime makes — so an auto-discovered self-casting NPC is validated exactly like an explicitly
    /// listed one. Coverage uses every appearing key, not just the opening cast, so an NPC that only
    /// walks on mid-conversation is not reported as unused.
    /// </summary>
    static void ValidateStageActors(DialogueTrigger trigger, List<string> issues)
    {
        IReadOnlyList<DialogueStageActorSource> actors = trigger.ResolveStageActors();
        string path = GetScenePath(trigger.transform);

        var appearing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (trigger.Sequence != null)
            trigger.Sequence.CollectAppearingCastKeys(appearing);

        var supplied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            // Runtime registers scene actors into a dictionary, so a duplicate id silently keeps only
            // the last one and the other NPC never appears with no error anywhere.
            if (!supplied.Add(actor.CharacterId))
            {
                issues.Add(
                    $"DialogueTrigger '{path}' supplies more than one stage actor with id " +
                    $"'{actor.CharacterId}'. Only the last one would ever be used.");
                continue;
            }

            if (trigger.Sequence != null && !appearing.Contains(actor.CharacterId))
            {
                issues.Add(
                    $"DialogueTrigger '{path}' supplies stage actor '{actor.CharacterId}', but " +
                    $"'{trigger.Sequence.name}' never puts that id on stage — it will never appear.");
            }
        }

        // The other direction: a scene-actor id the sequence casts but no source supplies. Party
        // roles are excluded because they resolve from the live PartyRuntime, which static
        // validation cannot see.
        foreach (string key in appearing)
        {
            if (DialogueCastKeys.IsRoleKey(key) || supplied.Contains(key))
                continue;

            if (!key.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(
                $"'{trigger.Sequence.name}' casts scene actor '{key}', but DialogueTrigger " +
                $"'{path}' supplies no DialogueStageActorSource with that id.");
        }
    }

    static void ValidateProjectSetup(List<string> issues)
    {
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

    /// <summary>
    /// Checks that dialogue clones still claim the rendering layers ASP's layer-filtered features
    /// draw.
    ///
    /// `DialogueLayers.AspFeatureRenderingLayerMask` mirrors values authored on the URP renderer, so
    /// retuning `ASPMeshOutlineRendererFeature` or `ASPDepthOffsetShadowFeature` silently drops the
    /// clones out of those passes. The portraits then render flatter than the same character does in
    /// gameplay, which is easy to miss by eye and impossible to guess the cause of.
    /// </summary>
    static void ValidateAspRenderingLayers(List<string> issues)
    {
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
            as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (pipeline == null)
            return;

        UnityEditor.SerializedProperty list =
            new UnityEditor.SerializedObject(pipeline).FindProperty("m_RendererDataList");

        for (int i = 0; list != null && i < list.arraySize; i++)
        {
            UnityEngine.Object data = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (data == null)
                continue;

            UnityEditor.SerializedProperty features =
                new UnityEditor.SerializedObject(data).FindProperty("m_RendererFeatures");

            for (int f = 0; features != null && f < features.arraySize; f++)
            {
                UnityEngine.Object feature = features.GetArrayElementAtIndex(f).objectReferenceValue;
                if (feature == null)
                    continue;

                string typeName = feature.GetType().Name;
                if (typeName != "ASPMeshOutlineRendererFeature" && typeName != "ASPDepthOffsetShadowFeature")
                    continue;

                var featureSerialized = new UnityEditor.SerializedObject(feature);
                UnityEditor.SerializedProperty layer = featureSerialized.FindProperty("Layer");
                UnityEditor.SerializedProperty mask = featureSerialized.FindProperty("RenderingLayerMask");
                if (layer == null || mask == null)
                    continue;

                if (layer.intValue != DialogueLayers.ActorLayer)
                {
                    issues.Add(
                        $"{typeName} draws Unity layer {layer.intValue}, but dialogue clones are on " +
                        $"layer {DialogueLayers.ActorLayer}, so they will not be drawn by that pass.");
                }

                if ((DialogueLayers.ActorRenderingLayerMask & (uint)mask.intValue) == 0)
                {
                    issues.Add(
                        $"{typeName} filters rendering layer mask {mask.intValue}, which dialogue " +
                        $"clones ({DialogueLayers.ActorRenderingLayerMask}) do not claim. Update " +
                        "DialogueLayers.AspFeatureRenderingLayerMask to match.");
                }
            }
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
