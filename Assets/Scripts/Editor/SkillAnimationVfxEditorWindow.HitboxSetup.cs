#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    [SerializeField] bool hitboxAdvanced;
    [SerializeField] string hitboxAttackCategory = "Light";
    [SerializeField] CharacterStats hitboxModelStats;
    readonly List<CharacterStats> hitboxMatchingStats = new();
    readonly List<SkillHitboxCharacterSetup.Attack> hitboxAttacks = new();
    CharacteContext hitboxCharacter;
    CharacterStats hitboxSetupStats;
    bool hitboxSetupInProgress;
    bool hitboxChoicesDirty = true;
    string hitboxSetupMessage;

    // Priority 10 also exposes this GameObject command in the Hierarchy context menu.
    [MenuItem("GameObject/Edit Hitboxes", false, 10)]
    static void EditSelectedCharacterHitboxes(MenuCommand command)
    {
        OpenCharacterHitboxes(command.context as GameObject ?? Selection.activeGameObject);
    }

    [MenuItem("GameObject/Edit Hitboxes", true)]
    static bool CanEditSelectedCharacterHitboxes() => !EditorApplication.isPlayingOrWillChangePlaymode &&
        (SkillHitboxCharacterSetup.ResolveCharacter(Selection.activeGameObject) != null ||
         SkillHitboxCharacterSetup.ResolveModel(Selection.activeGameObject) != null);

    [MenuItem("Assets/Edit Hitboxes", false, 49)]
    static void EditPrefabHitboxes() => OpenCharacterHitboxes(Selection.activeGameObject);

    [MenuItem("Assets/Edit Hitboxes", true)]
    static bool CanEditPrefabHitboxes() => CanEditSelectedCharacterHitboxes() &&
        PrefabUtility.GetPrefabAssetType(Selection.activeGameObject) != PrefabAssetType.Model;

    static void OpenCharacterHitboxes(GameObject selected)
    {
        var window = GetWindow<SkillAnimationVfxEditorWindow>(WindowTitle);
        if (!window.ConfirmHitboxDraft()) return;
        window.StopPreview(true);
        window.hitboxMode = true;
        window.SetupHitboxCharacter(selected);
        window.Show(); window.Repaint();
    }

    void InvalidateHitboxChoices() { hitboxChoicesDirty = true; Repaint(); }

    void RefreshHitboxChoices()
    {
        hitboxCharacter = authoringTarget != null ? SkillHitboxCharacterSetup.ResolveCharacter(authoringTarget.gameObject) : null;
        hitboxCharacter?.ResolveReferences();
        hitboxSetupStats = hitboxCharacter != null ? hitboxCharacter.baseStats : null;
        hitboxAttacks.Clear();
        hitboxAttacks.AddRange(SkillHitboxCharacterSetup.CollectAttacks(hitboxCharacter));
        hitboxMatchingStats.Clear();
        if (hitboxCharacter == null && authoringTarget != null)
        {
            hitboxMatchingStats.AddRange(SkillHitboxCharacterSetup.FindModelStats(authoringTarget.CharacterRoot.gameObject));
            IEnumerable<CharacterStats> statsChoices = hitboxModelStats != null ? new[] { hitboxModelStats } : hitboxMatchingStats;
            foreach (var stats in statsChoices)
                foreach (var attack in SkillHitboxCharacterSetup.CollectAttacks(null, stats))
                    if (!hitboxAttacks.Any(a => a.Source == attack.Source && a.EntryId == attack.EntryId)) hitboxAttacks.Add(attack);
        }
        hitboxChoicesDirty = false;
        var current = hitboxAttacks.Find(a => MatchesHitboxAttack(a));
        if (current != null) hitboxAttackCategory = current.Category;
    }

    bool MatchesHitboxAttack(SkillHitboxCharacterSetup.Attack attack) => authoringTarget != null &&
        attack.Source == authoringTarget.TimelineSourceAsset && attack.EntryId == authoringTarget.TimelineEntryId;

    bool SetupHitboxCharacter(GameObject selected)
    {
        if (hitboxSetupInProgress || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        var ctx = SkillHitboxCharacterSetup.ResolveCharacter(selected);
        var model = ctx == null ? SkillHitboxCharacterSetup.ResolveModel(selected) : null;
        if (ctx == null && model == null)
        {
            hitboxSetupMessage = "Choose one character or one of its bones. A container with several characters is ambiguous.";
            return false;
        }
        if (!ConfirmHitboxDraft()) return false;
        hitboxSetupInProgress = true;
        try
        {
            StopPreview(true);
            var subject = ctx != null ? ctx.gameObject : model;
            if (EditorUtility.IsPersistent(subject))
            {
                string path = AssetDatabase.GetAssetPath(selected);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Model)
                {
                    hitboxSetupMessage = "Use a character prefab (.prefab) or a scene character, rather than a model import.";
                    return false;
                }
                string relative = AnimationUtility.CalculateTransformPath(subject.transform, prefab.transform);
                var stage = PrefabStageUtility.OpenPrefab(path);
                if (stage == null) return false; // Includes cancellation of Unity's stage-change prompt.
                var actor = string.IsNullOrEmpty(relative) ? stage.prefabContentsRoot.transform : stage.prefabContentsRoot.transform.Find(relative);
                ctx = actor != null ? actor.GetComponent<CharacteContext>() : null;
                model = ctx == null && actor != null ? SkillHitboxCharacterSetup.ResolveModel(actor.gameObject) : null;
                if (ctx == null && model == null) { hitboxSetupMessage = "The character could not be resolved in Prefab Mode."; return false; }
            }
            Undo.IncrementCurrentGroup();
            int setupUndo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Hitbox Authoring");
            var tool = ctx != null ? SkillHitboxCharacterSetup.Prepare(ctx) : SkillHitboxCharacterSetup.PrepareModel(model);
            if (tool == null) return false;
            if (tool != authoringTarget) hitboxModelStats = null;
            SetAuthoringTarget(tool);
            RefreshHitboxChoices();
            // Honour a skill explicitly assigned to the old importer, then keep an existing selection.
            var importer = selected != null ? selected.GetComponent<SetSkillHitBoxData>() : null;
            var preferred = importer != null ? new SerializedObject(importer).FindProperty("skill").objectReferenceValue as SkillGemDefinition : null;
            if (preferred != null) SetSourceSelection(preferred, "main");
            else if (GetSource()?.SourceAsset is not SkillGemDefinition && hitboxAttacks.Count > 0)
                SetSourceSelection(hitboxAttacks[0].Source, hitboxAttacks[0].EntryId);
            RefreshHitboxChoices();
            Undo.CollapseUndoOperations(setupUndo);
            hitboxSetupMessage = null;
            Repaint(); SceneView.RepaintAll();
            return true;
        }
        finally { hitboxSetupInProgress = false; }
    }

    void DrawHitboxSetup()
    {
        var ctx = authoringTarget != null ? SkillHitboxCharacterSetup.ResolveCharacter(authoringTarget.gameObject) : null;
        if (hitboxChoicesDirty || ctx != hitboxCharacter || (ctx != null && ctx.baseStats != hitboxSetupStats)) RefreshHitboxChoices();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Character", GUILayout.Width(65));
                var current = hitboxCharacter != null ? hitboxCharacter.gameObject : authoringTarget != null ? authoringTarget.gameObject : null;
                EditorGUI.BeginChangeCheck();
                var chosen = (GameObject)EditorGUILayout.ObjectField(current, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck() && chosen != null) { SetupHitboxCharacter(chosen); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Use Selection", GUILayout.Width(100))) { SetupHitboxCharacter(Selection.activeGameObject); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Refresh", GUILayout.Width(60))) RefreshHitboxChoices();
                hitboxAdvanced = GUILayout.Toggle(hitboxAdvanced, "Setup...", EditorStyles.miniButton, GUILayout.Width(65));
            }
            if (!string.IsNullOrEmpty(hitboxSetupMessage)) EditorGUILayout.HelpBox(hitboxSetupMessage, MessageType.Warning);
            if (authoringTarget == null)
            { EditorGUILayout.HelpBox("Choose a character, then choose an attack to edit its Hitboxes.", MessageType.Info); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] categories = { "Light", "Heavy", "Skills" };
                int category = System.Array.IndexOf(categories, hitboxAttackCategory);
                int nextCategory = GUILayout.Toolbar(Mathf.Max(0, category), categories, GUILayout.Width(235));
                if (nextCategory != category) hitboxAttackCategory = categories[nextCategory];
                var choices = hitboxAttacks.Where(a => a.Category == hitboxAttackCategory).ToList();
                int selected = choices.FindIndex(MatchesHitboxAttack);
                var labels = new[] { "Choose an attack..." }.Concat(choices.Select(a => a.Label)).ToArray();
                int next = EditorGUILayout.Popup(selected + 1, labels);
                if (next > 0 && next - 1 != selected) SetSourceSelection(choices[next - 1].Source, choices[next - 1].EntryId);
            }
            if (GetPreviewAnimator() == null)
                EditorGUILayout.HelpBox("No preview Animator found. Assign the character model in its visual setup.", MessageType.Warning);
            if (hitboxAttacks.Count == 0)
                EditorGUILayout.HelpBox("No attacks found. Use Setup to choose Character Stats or a source Skill.", MessageType.Info);
            if (hitboxAdvanced)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (hitboxCharacter == null)
                    {
                        EditorGUI.BeginChangeCheck();
                        var stats = (CharacterStats)EditorGUILayout.ObjectField("Character Stats", hitboxModelStats, typeof(CharacterStats), false);
                        if (EditorGUI.EndChangeCheck()) { hitboxModelStats = stats; RefreshHitboxChoices(); }
                    }
                    DrawSourceAssetPicker(true);
                    var sourceEntries = AnimationVfxTimelineSourceFactory.GetEntries(authoringTarget.TimelineSourceAsset);
                    if (sourceEntries.Count > 0)
                    {
                        int selected = FindEntryIndex(sourceEntries, authoringTarget.TimelineEntryId);
                        int next = EditorGUILayout.Popup("Entry", Mathf.Max(0, selected), sourceEntries.Select(e => e.DisplayName).ToArray());
                        if (next != selected) SetSourceSelection(authoringTarget.TimelineSourceAsset, sourceEntries[next].Id);
                    }
                    var animator = GetPreviewAnimator();
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Preview Animator", animator, typeof(Animator), true);
                }
            }
        }
    }
}
#endif
