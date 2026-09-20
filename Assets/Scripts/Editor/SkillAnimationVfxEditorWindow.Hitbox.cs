#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    [SerializeField] bool hitboxMode;
    [SerializeField] SkillHitboxAuthoringSession hitboxSession;
    [SerializeField] ScriptableObject hitboxSourceAsset;
    [SerializeField] string hitboxSourceEntry;
    readonly SkillHitboxTimelineAdapter hitboxPanel = new();
    List<string> hitboxIssues = new();
    Vector2 hitboxTimelineScroll;
    Vector2 hitboxValidationScroll;
    bool hitboxShowValidation;
    float hitboxHeaderBottom = 90f;
    Animator hitboxPreviewAnimator;
    [MenuItem("Tools/RB/Animation VFX/Edit Hitboxes")]
    public static void OpenHitboxAuthoring() => OpenCharacterHitboxes(Selection.activeGameObject);

    void InitializeHitboxAuthoring()
    {
        SceneView.duringSceneGui -= DrawHitboxScene;
        SceneView.duringSceneGui += DrawHitboxScene;
        Undo.undoRedoPerformed -= HitboxUndoChanged;
        Undo.undoRedoPerformed += HitboxUndoChanged;
        EditorApplication.projectChanged -= InvalidateHitboxChoices;
        EditorApplication.projectChanged += InvalidateHitboxChoices;
        hitboxChoicesDirty = true;
        hitboxPanel.Repaint = () =>
        {
            if (hitboxSession != null) hitboxIssues = hitboxSession.Validate(AnchorExists);
            UpdateAuthoringDirtyState(); Repaint();
        };
        saveChangesMessage = "Save pending Hitbox and Block Window changes?";
    }

    void StopHitboxAuthoring()
    {
        hitboxPanel.EndTimelineDrag();
        SceneView.duringSceneGui -= DrawHitboxScene;
        Undo.undoRedoPerformed -= HitboxUndoChanged;
        EditorApplication.projectChanged -= InvalidateHitboxChoices;
        hitboxPreviewAnimator = null;
        // Dirty drafts survive assembly reload as serialized transient ScriptableObjects.
        if (hitboxSession != null && !hitboxSession.IsDirty) ReleaseHitboxSession();
    }

    void OnDestroy() { ReleaseHitboxSession(); ReleaseBlockSession(); }

    void ReleaseHitboxSession()
    {
        hitboxPanel.EndTimelineDrag();
        if (hitboxSession != null) DestroyImmediate(hitboxSession);
        hitboxSession = null; hitboxSourceAsset = null; hitboxSourceEntry = null;
        hitboxIssues.Clear(); UpdateAuthoringDirtyState();
    }

    bool HitboxSourceChanged => hitboxMode && hitboxSession != null && authoringTarget != null &&
        (authoringTarget.TimelineSourceAsset != hitboxSourceAsset || authoringTarget.TimelineEntryId != hitboxSourceEntry);

    void GuardExternalHitboxSourceChange()
    {
        if (!HitboxSourceChanged) return;
        StopPreview(true);
        if (ConfirmHitboxDraft()) ReleaseHitboxSession();
        else authoringTarget.SetTimelineSource(hitboxSourceAsset, hitboxSourceEntry);
    }

    bool ConfirmHitboxDraft()
    {
        if (!ConfirmBlockDraft()) return false;
        if (hitboxSession == null || !hitboxSession.IsDirty) return true;
        int choice = EditorUtility.DisplayDialogComplex("Unsaved Hitbox Draft", "Save changes to " + hitboxSession.skill.name + "?", "Save", "Cancel", "Discard");
        if (choice == 1) return false;
        if (choice == 0) return SaveHitboxDraft();
        hitboxSession.Reload(); UpdateAuthoringDirtyState(); return true;
    }

    public override void SaveChanges()
    {
        // Unity keeps the window open while hasUnsavedChanges remains true.
        if (!SaveHitboxDraft() || !SaveBlockDraft()) return;
        base.SaveChanges();
    }
    public override void DiscardChanges()
    {
        if (hitboxSession != null) hitboxSession.Reload();
        if (blockSession != null) blockSession.Reload();
        base.DiscardChanges();
    }

    bool SaveHitboxDraft()
    {
        if (hitboxSession == null) return true;
        if (!hitboxSession.Save(out string error, AnchorExists))
        {
            hitboxIssues = hitboxSession.Validate(AnchorExists);
            EditorUtility.DisplayDialog("Hitbox Save", error, "OK"); return false;
        }
        UpdateAuthoringDirtyState(); hitboxIssues.Clear(); Repaint(); return true;
    }
    bool AnchorExists(SkillHitboxAuthoringSession.PayloadDraft payload, SkillHitboxLayoutData.HitBoxGroupData group) =>
        SkillHitboxSceneHandles.TryBasis(authoringTarget, payload, group, out _);

    void HitboxUndoChanged()
    {
        hitboxChoicesDirty = true;
        if (hitboxSession != null) hitboxIssues = hitboxSession.Validate(AnchorExists);
        UpdateAuthoringDirtyState();
        Repaint(); SceneView.RepaintAll();
    }

    Animator GetPreviewAnimator() => hitboxMode
        ? SkillHitboxSceneHandles.AnchorRoot(authoringTarget, SkillHitboxLayoutData.AnchorSpace.AnimatorRoot)?.GetComponent<Animator>()
        : authoringTarget != null ? authoringTarget.PreviewAnimator : null;

    void DrawHitboxModeSelector()
    {
        int mode = GUILayout.Toolbar(hitboxMode ? 1 : 0, new[] { "Animation / VFX", "Hitbox" });
        if ((mode == 1) == hitboxMode || !ConfirmHitboxDraft()) return;
        StopPreview(true); ReleaseHitboxSession();
        hitboxMode = mode == 1; _cutsceneModeActive = false; _playheadInCutscene = false;
        if (hitboxMode) SetupHitboxCharacter(authoringTarget != null ? authoringTarget.gameObject : Selection.activeGameObject);
        SceneView.RepaintAll();
    }

    void DrawHitboxMode()
    {
        minSize = new Vector2(850, 670);
        if (authoringTarget == null) return;
        if (EditorUtility.IsPersistent(authoringTarget))
        { EditorGUILayout.HelpBox("Open the prefab in Prefab Mode, or select a scene instance, to preview safely.", MessageType.Warning); return; }
        var source = GetSource();
        var skill = source?.SourceAsset as SkillGemDefinition;
        if (skill == null) { EditorGUILayout.HelpBox("Choose a Skill or a migrated Melee combo step.", MessageType.Info); return; }
        if (hitboxSession == null || hitboxSession.skill != skill)
        {
            if (!ConfirmHitboxDraft()) return;
            ReleaseHitboxSession(); hitboxSession = SkillHitboxAuthoringSession.Create(skill);
            hitboxSourceAsset = authoringTarget.TimelineSourceAsset;
            hitboxSourceEntry = authoringTarget.TimelineEntryId;
            hitboxPreviewAnimator = GetPreviewAnimator();
            hitboxIssues = hitboxSession.Validate(AnchorExists);
        }
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(GetClip(source) != null ? GetClip(source).name : "No animation clip", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(hitboxSession.IsDirty ? "Unsaved changes" : "Saved", EditorStyles.miniLabel, GUILayout.Width(105));
                hitboxShowValidation = GUILayout.Toggle(hitboxShowValidation, hitboxIssues.Count == 0 ? "Checks OK" : $"{hitboxIssues.Count} issues", EditorStyles.toolbarButton, GUILayout.Width(75));
                if (GUILayout.Button("Save Hitboxes", EditorStyles.toolbarButton, GUILayout.Width(95))) SaveHitboxDraft();
                if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    if (!hitboxSession.IsDirty || EditorUtility.DisplayDialog("Revert Hitboxes", "Discard this Hitbox draft and reload the skill?", "Revert", "Cancel"))
                    { hitboxSession.Reload(); hitboxIssues = hitboxSession.Validate(AnchorExists); }
                }
            }
            if (hitboxShowValidation)
            {
                hitboxValidationScroll = EditorGUILayout.BeginScrollView(hitboxValidationScroll, GUILayout.Height(hitboxIssues.Count == 0 ? 35 : 85));
                if (hitboxIssues.Count == 0) GUILayout.Label("Ready to save. Preview never deals damage.", EditorStyles.miniLabel);
                foreach (var issue in hitboxIssues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
                EditorGUILayout.EndScrollView();
            }
            if (Event.current.type == EventType.Repaint)
            {
                float bottom = GUILayoutUtility.GetLastRect().yMax;
                if (!Mathf.Approximately(bottom, hitboxHeaderBottom)) { hitboxHeaderBottom = bottom; Repaint(); }
            }
            float timelineHeight = Mathf.Clamp(position.height * .27f, 140, 240);
            var selectedPayload = hitboxPanel.Selected(hitboxSession);
            bool hasWindows = selectedPayload != null && hitboxSession.Windows(selectedPayload).Count > 0;
            float bodyHeight = Mathf.Max(160, position.height - hitboxHeaderBottom - timelineHeight - (hasWindows ? 114 : 50));
            hitboxPanel.DrawPanel(hitboxSession, authoringTarget, bodyHeight);
            GUILayout.Space(5);
            DrawTransport(source, GetClip(source), GetPreviewAnimator());
            hitboxPanel.DrawWindows(hitboxSession, normalizedTime);
            hitboxTimelineScroll = EditorGUILayout.BeginScrollView(hitboxTimelineScroll, GUILayout.Height(timelineHeight));
            float contentHeight = Mathf.Max(timelineHeight - 20, hitboxPanel.TimelineHeight(hitboxSession));
            var rect = GUILayoutUtility.GetRect(300, 10000, contentHeight, contentHeight);
            hitboxPanel.DrawTimeline(hitboxSession, rect, normalizedTime, ScrubTo);
            if (hitboxPanel.IsDragging && isPlaying) PausePreview();
            EditorGUILayout.EndScrollView();
        }
        UpdateAuthoringDirtyState();
        if (GUI.changed) { hitboxIssues = hitboxSession.Validate(AnchorExists); Repaint(); SceneView.RepaintAll(); }
    }

    void DrawHitboxScene(SceneView view)
    {
        if (!hitboxMode || hitboxSession == null || authoringTarget == null || Application.isPlaying || EditorUtility.IsPersistent(authoringTarget)) return;
        var animator = GetPreviewAnimator();
        if (hitboxPreviewAnimator != animator)
        {
            if (!ReferenceEquals(hitboxPreviewAnimator, null)) StopPreview(true);
            hitboxPreviewAnimator = animator;
        }
        hitboxPanel.DrawScene(hitboxSession, authoringTarget, normalizedTime);
        UpdateAuthoringDirtyState();
    }
}
#endif
