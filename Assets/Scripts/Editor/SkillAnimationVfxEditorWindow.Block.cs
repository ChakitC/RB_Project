#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    [SerializeField] DefensiveBlockTimelineSession blockSession;
    [SerializeField] ScriptableObject blockSourceAsset;
    [SerializeField] string blockSourceEntry;
    int blockDragControl;
    int blockUndoGroup;
    bool blockDragEnd;

    void UpdateAuthoringDirtyState() => hasUnsavedChanges = HasPendingAuthoringChanges();

    void ReleaseBlockSession()
    {
        EndBlockDrag();
        if (blockSession != null) DestroyImmediate(blockSession);
        blockSession = null;
        blockSourceAsset = null; blockSourceEntry = null;
        UpdateAuthoringDirtyState();
    }

    bool ConfirmBlockDraft()
    {
        if (blockSession != null && blockSession.IsDirty)
        {
            int choice = EditorUtility.DisplayDialogComplex("Unsaved Block Window",
                "Save this Skill's Block Window to " + (blockSession.skill != null ? blockSession.skill.name : "the Skill") + "?",
                "Save", "Cancel", "Discard");
            if (choice == 1 || (choice == 0 && !SaveBlockDraft())) return false;
        }
        ReleaseBlockSession();
        return true;
    }

    void GuardExternalBlockSourceChange()
    {
        if (blockSession == null || authoringTarget == null ||
            (authoringTarget.TimelineSourceAsset == blockSourceAsset && authoringTarget.TimelineEntryId == blockSourceEntry)) return;
        StopPreview(true);
        var oldAsset = blockSourceAsset;
        string oldEntry = blockSourceEntry;
        if (!ConfirmBlockDraft()) authoringTarget.SetTimelineSource(oldAsset, oldEntry);
    }

    void EnsureBlockSession(IAnimationVfxTimelineSource source)
    {
        if (source == null && blockSession != null) return;
        var skill = source?.SourceAsset as SkillGemDefinition;
        if (blockSession != null && blockSession.skill != skill && !ConfirmBlockDraft()) return;
        if (blockSession == null && skill != null)
        {
            blockSession = DefensiveBlockTimelineSession.Create(skill);
            blockSourceAsset = authoringTarget.TimelineSourceAsset;
            blockSourceEntry = authoringTarget.TimelineEntryId;
        }
    }

    bool IsBlockMainSource(IAnimationVfxTimelineSource source) => blockSession != null &&
        source?.SourceAsset == blockSession.skill && source is not CutsceneSkillVfxTimelineSource &&
        !(source is IAnimationVfxTimelineMultiMode mode && mode.IsSecondaryMode);

    bool ShowBlockTrack(IAnimationVfxTimelineSource source) => IsBlockMainSource(source) && blockSession.IsEnabled;

    void AddBlockContextItems(GenericMenu menu, IAnimationVfxTimelineSource source, float time, bool clickInCutscene)
    {
        if (!IsBlockMainSource(source) || clickInCutscene)
        {
            menu.AddDisabledItem(new GUIContent("Block/Main Skill only"));
            return;
        }
        if (Application.isPlaying)
        {
            menu.AddDisabledItem(new GUIContent("Block/Exit Play Mode to edit"));
            return;
        }
        var draft = blockSession;
        bool CanEdit() => draft != null && blockSession == draft && !Application.isPlaying && IsBlockMainSource(GetSource());
        void AddSaveCommands()
        {
            menu.AddSeparator("Block/");
            if (draft.HasConflict)
                menu.AddDisabledItem(new GUIContent("Block/Settings changed - Revert to reload"));
            if (draft.ValidationError != null)
                menu.AddDisabledItem(new GUIContent("Block/" + draft.ValidationError));
            if (draft.IsEnabled)
            {
                foreach (DefensiveBlockMode mode in System.Enum.GetValues(typeof(DefensiveBlockMode)))
                {
                    var value = mode;
                    string label = value == DefensiveBlockMode.Contact ? "Contact" : "Timed Approach";
                    menu.AddItem(new GUIContent("Block/Mode/" + label), draft.mode == value, () =>
                    { if (CanEdit()) { EndBlockDrag(); draft.SetMode(value); UpdateAuthoringDirtyState(); Repaint(); } });
                }
                if (draft.mode == DefensiveBlockMode.TimedApproach)
                    menu.AddItem(new GUIContent("Block/Timed Approach Settings..."), false, () =>
                    { if (CanEdit()) DefensiveBlockApproachPopup.Show(draft, () => { UpdateAuthoringDirtyState(); Repaint(); }); });
                menu.AddItem(new GUIContent("Block/Disable Block"), false, () =>
                { if (CanEdit()) { EndBlockDrag(); draft.DisableBlock(); UpdateAuthoringDirtyState(); Repaint(); } });
            }
            if (draft.IsDirty)
                menu.AddItem(new GUIContent("Block/Save Block"), false, () => { if (CanEdit()) SaveBlockDraft(); });
            else menu.AddDisabledItem(new GUIContent("Block/Save Block"));
            menu.AddItem(new GUIContent("Block/Revert Block"), false, () =>
            { if (CanEdit()) { EndBlockDrag(); draft.Reload(); UpdateAuthoringDirtyState(); Repaint(); } });
        }
        if (!draft.IsEnabled)
        {
            menu.AddItem(new GUIContent("Block/Add Block Window Here"), false, () =>
            {
                if (!CanEdit() || draft.IsEnabled) return;
                float duration = Mathf.Max(0f, draft.end - draft.start);
                draft.AddBlock();
                SetBlockWindow(time, Mathf.Min(1f, time + duration));
            });
            AddSaveCommands();
            return;
        }
        if (!draft.Contains(time))
        {
            menu.AddItem(new GUIContent("Block/Add Block Window Here"), false, () =>
            { if (CanEdit()) { EndBlockDrag(); draft.AddWindow(time); UpdateAuthoringDirtyState(); Repaint(); } });
            AddSaveCommands();
            return;
        }
        // Context actions target only the window under the cursor.
        for (int i = 0; i < draft.WindowCount; i++)
            if (time >= draft.StartAt(i) && time <= draft.EndAt(i)) { draft.selectedWindow = i; break; }
        int selected = Mathf.Clamp(draft.selectedWindow, 0, draft.WindowCount - 1);
        var window = draft.windows[selected];
        if (time <= draft.EndAt(selected))
            menu.AddItem(new GUIContent("Block/Set Block Open Here"), false, () =>
            { if (CanEdit() && time <= draft.EndAt(selected)) SetBlockWindow(time, draft.EndAt(selected)); });
        else menu.AddDisabledItem(new GUIContent("Block/Set Block Open Here", "Open must be before or at Close."));
        if (time >= draft.StartAt(selected))
            menu.AddItem(new GUIContent("Block/Set Block Close Here"), false, () =>
            { if (CanEdit() && time >= draft.StartAt(selected)) SetBlockWindow(draft.StartAt(selected), time); });
        else menu.AddDisabledItem(new GUIContent("Block/Set Block Close Here", "Close must be after or at Open."));
        menu.AddSeparator("Block/");
        foreach (DefensiveBlockOutcome outcome in System.Enum.GetValues(typeof(DefensiveBlockOutcome)))
        {
            var value = outcome;
            string label = value == DefensiveBlockOutcome.ContinueSkill ? "Continue Skill" : "Interrupt Skill";
            menu.AddItem(new GUIContent("Block/On Success/" + label), window.onSuccess == value, () =>
            {
                if (!CanEdit() || !draft.windows.Contains(window)) return;
                Undo.RecordObject(draft, "Edit Block Outcome"); window.onSuccess = value;
                UpdateAuthoringDirtyState(); Repaint();
            });
        }
        // Include existing bindings as well, so stale step indices can be removed here.
        var stepIndices = new System.Collections.Generic.SortedSet<int>();
        foreach (var payload in SkillHitboxAuthoringSession.FindPayloads(draft.skill.payload))
            for (int i = 0; payload.Steps != null && i < payload.Steps.Count; i++) stepIndices.Add(i);
        if (window.hitboxSteps != null) foreach (int step in window.hitboxSteps) stepIndices.Add(step);
        foreach (int step in stepIndices)
        {
            int index = step;
            bool bound = window.AllowsStep(index);
            menu.AddItem(new GUIContent($"Block/Hitbox Steps/Step {index}"), bound, () =>
            {
                if (!CanEdit() || !draft.windows.Contains(window)) return;
                Undo.RecordObject(draft, "Edit Block Hitbox Steps");
                var steps = new System.Collections.Generic.List<int>(window.hitboxSteps ?? System.Array.Empty<int>());
                if (steps.Contains(index)) steps.RemoveAll(s => s == index); else steps.Add(index);
                steps.Sort(); window.hitboxSteps = steps.ToArray();
                UpdateAuthoringDirtyState(); Repaint();
            });
        }
        if (stepIndices.Count == 0) menu.AddDisabledItem(new GUIContent("Block/Hitbox Steps/No hitbox steps authored"));
        if (draft.WindowCount > 1)
            menu.AddItem(new GUIContent($"Block/Remove Window {selected + 1}"), false, () =>
            {
                if (!CanEdit() || !draft.windows.Contains(window)) return;
                EndBlockDrag(); draft.RemoveWindow(draft.windows.IndexOf(window));
                UpdateAuthoringDirtyState(); Repaint();
            });
        else menu.AddDisabledItem(new GUIContent("Block/Remove Window (keep at least one)"));
        AddSaveCommands();
    }

    bool SaveBlockDraft()
    {
        if (blockSession != null && !blockSession.Save(out string error))
        { EditorUtility.DisplayDialog("Save Block Window", error, "OK"); return false; }
        UpdateAuthoringDirtyState(); Repaint(); return true;
    }

    void SetBlockWindow(float start, float end)
    {
        blockSession.SetRange(blockSession.selectedWindow, start, end);
        UpdateAuthoringDirtyState(); Repaint();
    }

    void EndBlockDrag()
    {
        if (blockDragControl == 0) return;
        if (GUIUtility.hotControl == blockDragControl) GUIUtility.hotControl = 0;
        Undo.CollapseUndoOperations(blockUndoGroup);
        blockDragControl = 0;
    }

    void DrawBlockRange(Rect area, IAnimationVfxTimelineSource source)
    {
        if (!ShowBlockTrack(source)) return;
        int track = FindTrack(TrackKind.Block);
        if (track < 0) return;
        Rect row = GetTrackContentRect(area, track);
        if (blockSession.ValidationError != null)
        {
            EndBlockDrag();
            GUI.Label(row, new GUIContent("Invalid Block window - right-click > Block to edit or Revert.", blockSession.ValidationError), EditorStyles.miniLabel);
            return;
        }
        for (int i = 0; i < blockSession.WindowCount; i++)
        {
            Rect start = GetMarkerRect(row, blockSession.StartAt(i));
            Rect end = GetMarkerRect(row, blockSession.EndAt(i));
            bool active = !_playheadInCutscene && normalizedTime >= blockSession.StartAt(i) && normalizedTime <= blockSession.EndAt(i);
            EditorGUI.DrawRect(Rect.MinMaxRect(start.center.x, row.y + 8, end.center.x, row.yMax - 8),
                active ? new Color(.15f, .55f, .35f) : new Color(.17f, .32f, .42f));
            DrawMarker(start, new Color(.3f, .85f, 1f), $"Block {i + 1} Open");
            DrawMarker(end, new Color(1f, .7f, .25f), $"Block {i + 1} Close");
            EditorGUIUtility.AddCursorRect(start, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(end, MouseCursor.ResizeHorizontal);
            int control = GUIUtility.GetControlID("Block Window".GetHashCode(), FocusType.Passive);
            var evt = Event.current;
            if (Application.isPlaying) { EndBlockDrag(); return; }
            if (evt.type == EventType.MouseDown && evt.button == 0 && (start.Contains(evt.mousePosition) || end.Contains(evt.mousePosition)))
            {
                blockDragEnd = end.Contains(evt.mousePosition) && (!start.Contains(evt.mousePosition) || evt.mousePosition.x >= start.center.x);
                blockSession.selectedWindow = i;
                blockDragControl = control; GUIUtility.hotControl = control;
                Undo.IncrementCurrentGroup(); blockUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Drag Block Window"); evt.Use();
            }
            if (i == blockSession.selectedWindow && blockDragControl != 0 && GUIUtility.hotControl == blockDragControl)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    float time = MouseToNormalized(row, evt.mousePosition.x);
                    SetBlockWindow(blockDragEnd ? blockSession.StartAt(i) : Mathf.Min(time, blockSession.EndAt(i)),
                        blockDragEnd ? Mathf.Max(time, blockSession.StartAt(i)) : blockSession.EndAt(i));
                    evt.Use();
                }
                if (evt.type == EventType.MouseUp) { EndBlockDrag(); evt.Use(); }
            }
        }
    }

}
#endif
