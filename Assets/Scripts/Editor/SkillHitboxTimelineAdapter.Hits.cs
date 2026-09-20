#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class SkillHitboxTimelineAdapter
{
    static PrefabHitboxSkillPayloadDef.HitboxStep StepFor(SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        int index = draft.startIds.IndexOf(id);
        return index >= 0 && index < draft.steps.Count ? draft.steps[index] : null;
    }

    void SelectWindow(SkillHitboxAuthoringSession session, string id)
    {
        selectedWindow = id;
        var draft = Selected(session);
        var step = draft != null ? StepFor(draft, id) : null;
        if (step != null)
        {
            int group = draft.hitboxLayout.Groups.ToList().FindIndex(g => g != null &&
                step.GroupKeys.Any(k => string.Equals(k, g.GroupKey, StringComparison.OrdinalIgnoreCase)));
            if (group >= 0 && group != GroupIndex) { GroupIndex = group; ShapeIndex = 0; }
        }
        Repaint?.Invoke(); SceneView.RepaintAll();
    }

    internal void AddHit(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, float time)
    {
        if (session == null || !session.payloads.Contains(draft)) return;
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        var before = session.markers.Select(m => m.id).ToArray();
        float start = Mathf.Min(time, .998f);
        session.AddWindow(draft, start, Mathf.Min(1, start + .1f));
        string id = session.markers.First(m => !before.Contains(m.id) && m.name == draft.source.HitboxStartEventName).id;
        if (GroupIndex >= 0 && GroupIndex < draft.hitboxLayout.Groups.Count && draft.hitboxLayout.Groups[GroupIndex] != null)
            ToggleWindowGroup(session, draft, id, draft.hitboxLayout.Groups[GroupIndex].GroupKey);
        Undo.CollapseUndoOperations(undo);
        SelectWindow(session, id);
    }

    void DuplicateHit(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        if (session == null || !session.payloads.Contains(draft)) return;
        var window = session.Windows(draft).Find(w => w.Start.id == id);
        if (window.Start == null) return;
        Undo.IncrementCurrentGroup();
        string copy = session.DuplicateWindow(window);
        if (copy != null) SelectWindow(session, copy);
    }

    void DeleteHit(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        if (session == null || !session.payloads.Contains(draft)) return;
        var windows = session.Windows(draft);
        int index = windows.FindIndex(w => w.Start.id == id);
        if (index < 0) return;
        EndTimelineDrag(); Undo.IncrementCurrentGroup(); session.RemoveWindow(windows[index]);
        windows = session.Windows(draft);
        SelectWindow(session, windows.Count > 0 ? windows[Mathf.Min(index, windows.Count - 1)].Start.id : null);
    }

    internal bool HandleHitDelete(SkillHitboxAuthoringSession session, Event current, bool focused, bool editingText)
    {
        if (!focused || editingText || IsDragging) return false;
        bool key = current.type == EventType.KeyDown && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace);
        bool command = (current.type == EventType.ValidateCommand || current.type == EventType.ExecuteCommand) &&
            (current.commandName == "Delete" || current.commandName == "SoftDelete");
        var draft = Selected(session);
        if ((!key && !command) || draft == null || !session.Windows(draft).Any(w => w.Start.id == selectedWindow)) return false;
        if (current.type != EventType.ValidateCommand) DeleteHit(session, draft, selectedWindow);
        current.Use(); return true;
    }

    internal GenericMenu CreateHitMenu(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        var menu = new GenericMenu();
        var window = session.Windows(draft).Find(w => w.Start.id == id);
        if (window.Start == null) return menu;
        if (session.TryDuplicatePlacement(window, out _))
            menu.AddItem(new GUIContent("Duplicate Hit"), false, () => DuplicateHit(session, draft, id));
        else menu.AddDisabledItem(new GUIContent("Duplicate Hit (no free space or invalid timing)"));
        menu.AddItem(new GUIContent("Delete Hit"), false, () => DeleteHit(session, draft, id));
        menu.AddSeparator("");
        AddWindowGroupItems(menu, "Assign Groups/", session, draft, id);
        menu.AddSeparator("");
        menu.AddDisabledItem(new GUIContent("Shared timing affects: " + session.AffectedBy(window)));
        return menu;
    }

    void AddWindowGroupItems(GenericMenu menu, string prefix, SkillHitboxAuthoringSession session,
        SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        var step = StepFor(draft, id);
        foreach (var group in draft.hitboxLayout.Groups.Where(g => g != null))
        {
            string key = group.GroupKey;
            menu.AddItem(new GUIContent(prefix + key), step != null && step.GroupKeys.Contains(key), () =>
            { ToggleWindowGroup(session, draft, id, key); Repaint?.Invoke(); SceneView.RepaintAll(); });
        }
        if (draft.hitboxLayout.Groups.Count == 0) menu.AddDisabledItem(new GUIContent(prefix + "Create a group first"));
    }

    SerializedProperty SerializedStep(SerializedObject so, SkillHitboxAuthoringSession session,
        SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        int index = draft.startIds.IndexOf(id);
        return index >= 0 && index < draft.steps.Count ? so.FindProperty("payloads")
            .GetArrayElementAtIndex(session.payloads.IndexOf(draft)).FindPropertyRelative("steps").GetArrayElementAtIndex(index) : null;
    }

    void DrawHitSummary(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        var so = new SerializedObject(session); var step = SerializedStep(so, session, draft, id);
        if (step == null) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Damage ×", GUILayout.Width(65));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("damageMultiplier"), GUIContent.none, GUILayout.Width(65));
            GUILayout.Space(12);
            var enabled = step.FindPropertyRelative("overrideKnockback");
            enabled.boolValue = GUILayout.Toggle(enabled.boolValue, "Override Knockback", GUILayout.Width(150));
            if (enabled.boolValue)
            {
                GUILayout.Label("Distance", GUILayout.Width(52));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackDistance"), GUIContent.none, GUILayout.Width(65));
                GUILayout.Label("Duration (s)", GUILayout.Width(72));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackDuration"), GUIContent.none, GUILayout.Width(65));
            }
            else GUILayout.Label("No Hit knockback", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            showHitDetails = GUILayout.Toggle(showHitDetails, "More settings", EditorStyles.miniButton, GUILayout.Width(100));
        }
        so.ApplyModifiedProperties();
    }

    void DrawHitDetails(SkillHitboxAuthoringSession session)
    {
        if (!showHitDetails) return;
        var draft = Selected(session); if (draft == null) return;
        var so = new SerializedObject(session); var step = SerializedStep(so, session, draft, selectedWindow);
        if (step == null) return;
        GUILayout.Label("SELECTED HIT · " + draft.source.name, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(step.FindPropertyRelative("hitPolicy"));
        EditorGUILayout.PropertyField(step.FindPropertyRelative("clearHitCacheOnEnter"));
        using (new EditorGUI.DisabledScope(!step.FindPropertyRelative("overrideKnockback").boolValue))
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackProgressCurve"), new GUIContent("Progress curve"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackReaction"), new GUIContent("Reaction"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackInterruptsActions"), new GUIContent("Interrupt actions"));
        }
        GUILayout.Label("These values apply only to this payload's selected Hit.", EditorStyles.miniLabel);
        so.ApplyModifiedProperties(); GUILayout.Space(12);
    }
}
#endif
