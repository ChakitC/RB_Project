#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class SkillHitboxTimelineAdapter
{
    const float TimelineLabelWidth = 100f;
    const float TimelineRowHeight = 32f;
    string selectedWindow;
    string draggingStart, draggingEnd;
    int dragMode, dragControl, dragUndo;
    float dragOrigin, dragStartTime, dragEndTime;
    bool showMarkers, showHitDetails;

    public bool IsDragging => dragControl != 0;
    internal Rect TimelineScreenRect { get; private set; }
    public float TimelineHeight(SkillHitboxAuthoringSession session) =>
        Mathf.Max(110, 40 + (Selected(session) is { } draft ? session.Windows(draft).Count : 0) * TimelineRowHeight);

    public void EndTimelineDrag()
    {
        if (dragControl == 0) return;
        if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
        if (dragMode != 0) Undo.CollapseUndoOperations(dragUndo);
        dragControl = 0; draggingStart = draggingEnd = null;
    }

    public static float TimelineTime(Rect area, float x) =>
        Mathf.Clamp01((x - area.x - TimelineLabelWidth) / Mathf.Max(1f, area.width - TimelineLabelWidth - 12));

    public void DrawTimeline(SkillHitboxAuthoringSession session, Rect area, float time, Action<float> scrub)
    {
        var draft = Selected(session); if (draft == null || draft.source == null) return;
        if (Event.current.type == EventType.Repaint)
            TimelineScreenRect = new Rect(GUIUtility.GUIToScreenPoint(area.position), area.size);
        EditorGUI.DrawRect(area, new Color(.105f, .115f, .13f));
        float left = area.x + TimelineLabelWidth, width = Mathf.Max(1, area.width - TimelineLabelWidth - 12);
        float seconds = Mathf.Max(.001f, session.skill.skillClip?.Clip != null ? session.skill.skillClip.Clip.length : 1f);
        int ticks = area.width < 1000 ? 5 : 10;
        for (int i = 0; i <= ticks; i++)
        {
            float x = left + width * i / ticks;
            EditorGUI.DrawRect(new Rect(x, area.y + 25, 1, area.height - 25), new Color(.22f, .24f, .27f));
            GUI.Label(new Rect(Mathf.Min(x + 3, area.xMax - 58), area.y + 3, 58, 20), $"{seconds * i / ticks:0.##}s", EditorStyles.miniLabel);
        }
        var events = session.skill.skillClip?.SerializedEvents;
        for (int i = 0; events?.NormalizedTimes != null && i < events.NormalizedTimes.Length - 1; i++)
            if (events.Names != null && i < events.Names.Length && events.Names[i] != null && events.Names[i].name == "Vfx")
                EditorGUI.DrawRect(new Rect(left + width * events.NormalizedTimes[i], area.y + 23, 3, 8), new Color(.75f, .4f, 1));
        var windows = session.Windows(draft);
        var current = Event.current;
        if (!GUI.enabled) EndTimelineDrag();
        int control = GUIUtility.GetControlID("HitboxTimelineDrag".GetHashCode(), FocusType.Keyboard);
        if (current.type == EventType.MouseDown && area.Contains(current.mousePosition))
        { GUI.FocusControl(null); GUIUtility.keyboardControl = control; EditorGUIUtility.editingTextField = false; }
        else if (current.rawType == EventType.MouseDown && GUIUtility.keyboardControl == control)
            GUIUtility.keyboardControl = 0;
        if (HandleHitDelete(session, current, GUI.enabled && GUIUtility.keyboardControl == control, EditorGUIUtility.editingTextField)) return;
        for (int i = 0; i < windows.Count; i++)
        {
            var window = windows[i]; float y = area.y + 36 + i * TimelineRowHeight;
            bool chosen = selectedWindow == window.Start.id;
            var label = new Rect(area.x + 4, y, TimelineLabelWidth - 8, 26);
            if (GUI.Button(label, $"Hit {i + 1}", chosen ? EditorStyles.miniButton : EditorStyles.label))
            { SelectWindow(session, window.Start.id); GUIUtility.keyboardControl = control; }
            float start = left + width * window.Start.time, end = left + width * window.End.time;
            var body = new Rect(start, y + 3, Mathf.Max(2, end - start), 22);
            bool active = time >= window.Start.time && time < window.End.time;
            EditorGUI.DrawRect(body, active ? new Color(.13f, .58f, .35f) : chosen ? new Color(.24f, .48f, .67f) : new Color(.23f, .32f, .4f));
            var step = StepFor(draft, window.Start.id);
            bool missing = step == null || step.GroupKeys.Count == 0 || step.GroupKeys.Any(k =>
                !draft.hitboxLayout.Groups.Any(g => g != null && string.Equals(g.GroupKey, k, StringComparison.OrdinalIgnoreCase)));
            string caption = $"Hit {i + 1} · " + (missing ? "! Choose groups" : string.Join(", ", step.GroupKeys));
            GUI.Label(new Rect(start + 8, y + 5, Mathf.Max(0, body.width - 16), 18),
                new GUIContent(caption, caption + $"\n{window.Start.time * seconds:0.###}–{window.End.time * seconds:0.###}s"), EditorStyles.miniLabel);
            if (missing) EditorGUI.DrawRect(new Rect(start, y + 24, body.width, 2), new Color(1f, .55f, .2f));
            var open = new Rect(start - 5, y, 10, 28);
            var close = new Rect(end - 5, y, 10, 28);
            EditorGUI.DrawRect(open, new Color(.25f, .8f, .9f));
            EditorGUI.DrawRect(close, new Color(1, .65f, .3f));
            EditorGUIUtility.AddCursorRect(body, MouseCursor.MoveArrow);
            EditorGUIUtility.AddCursorRect(open, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(close, MouseCursor.ResizeHorizontal);
            if (GUI.enabled && current.type == EventType.ContextClick &&
                (label.Contains(current.mousePosition) || open.Contains(current.mousePosition) || close.Contains(current.mousePosition) || body.Contains(current.mousePosition)))
            {
                SelectWindow(session, window.Start.id); GUIUtility.keyboardControl = control;
                CreateHitMenu(session, draft, window.Start.id).ShowAsContext(); current.Use();
            }
            if (GUI.enabled && current.type == EventType.MouseDown && current.button == 0 &&
                (open.Contains(current.mousePosition) || close.Contains(current.mousePosition) || body.Contains(current.mousePosition)))
            {
                SelectWindow(session, window.Start.id); draggingStart = window.Start.id; draggingEnd = window.End.id;
                dragMode = close.Contains(current.mousePosition) ? 2 : open.Contains(current.mousePosition) ? 1 : 3;
                dragStartTime = window.Start.time; dragEndTime = window.End.time;
                dragOrigin = TimelineTime(area, current.mousePosition.x);
                dragControl = control; GUIUtility.hotControl = control;
                Undo.IncrementCurrentGroup(); dragUndo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Drag Hitbox Window"); Repaint?.Invoke(); current.Use();
            }
        }
        if (GUI.enabled && current.type == EventType.ContextClick && area.Contains(current.mousePosition))
        {
            float at = TimelineTime(area, current.mousePosition.x);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Hit Here"), false, () => AddHit(session, draft, at));
            menu.ShowAsContext(); current.Use();
        }
        if (GUI.enabled && current.type == EventType.MouseDown && current.button == 0 && area.Contains(current.mousePosition))
        {
            dragMode = 0; dragControl = control; GUIUtility.hotControl = control;
            scrub(TimelineTime(area, current.mousePosition.x)); current.Use();
        }
        if (dragControl != 0 && GUIUtility.hotControl == dragControl)
        {
            if (current.type == EventType.MouseDrag)
            {
                float next = TimelineTime(area, current.mousePosition.x);
                if (dragMode == 1) session.MoveMarker(draggingStart, Mathf.Min(next, dragEndTime - .0001f));
                else if (dragMode == 2) session.MoveMarker(draggingEnd, Mathf.Max(next, dragStartTime + .0001f));
                else if (dragMode == 3) session.MoveWindow(draggingStart, draggingEnd, dragStartTime + next - dragOrigin, dragEndTime - dragStartTime);
                if (dragMode != 0) { selectedWindow = draggingStart; GUI.changed = true; }
                scrub(next); Repaint?.Invoke(); current.Use();
            }
            if (current.type == EventType.MouseUp) { EndTimelineDrag(); current.Use(); }
        }
        float playhead = left + Mathf.Clamp01(time) * width;
        EditorGUI.DrawRect(new Rect(playhead, area.y, 2, area.height), new Color(1f, .4f, .3f));
        if (windows.Count == 0) GUI.Label(new Rect(left + 8, area.y + 42, width - 16, 30), "Right-click to add a Hit, or use + Hit at Playhead.", EditorStyles.miniLabel);
    }

    public void DrawWindows(SkillHitboxAuthoringSession session, float time)
    {
        var draft = Selected(session); if (draft == null || draft.source == null) return;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("HIT TIMING", EditorStyles.boldLabel, GUILayout.Width(95));
            GUILayout.Label("Right-click: manage hits   |   Drag edges / bar: resize / move", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            showMarkers = GUILayout.Toggle(showMarkers, "Repair markers", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (GUILayout.Button("+ Hit at Playhead", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                AddHit(session, draft, time);
                GUIUtility.ExitGUI();
            }
        }
        var windows = session.Windows(draft);
        if (windows.Count == 0) return;
        int selected = windows.FindIndex(w => w.Start.id == selectedWindow);
        if (selected < 0) { selected = 0; selectedWindow = windows[0].Start.id; }
        var window = windows[selected];
        float seconds = Mathf.Max(.001f, session.skill.skillClip?.Clip != null ? session.skill.skillClip.Clip.length : 1f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"Hit {selected + 1}", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Start (s)", GUILayout.Width(50));
            float start = EditorGUILayout.DelayedFloatField(window.Start.time * seconds, GUILayout.Width(70)) / seconds;
            GUILayout.Label("End (s)", GUILayout.Width(45));
            float end = EditorGUILayout.DelayedFloatField(window.End.time * seconds, GUILayout.Width(70)) / seconds;
            if (start != window.Start.time && Mathf.Abs(start - window.Start.time) > .000001f) { session.MoveMarker(window.Start.id, start); GUIUtility.ExitGUI(); }
            if (end != window.End.time && Mathf.Abs(end - window.End.time) > .000001f) { session.MoveMarker(window.End.id, end); GUIUtility.ExitGUI(); }
            GUILayout.Label($"{(window.End.time - window.Start.time) * seconds:0.###}s", EditorStyles.miniLabel, GUILayout.Width(55));
            int stepIndex = draft.startIds.IndexOf(window.Start.id);
            string keys = stepIndex >= 0 && stepIndex < draft.steps.Count ? string.Join(", ", draft.steps[stepIndex].GroupKeys) : "";
            if (GUILayout.Button(new GUIContent(string.IsNullOrEmpty(keys) ? "Groups: choose..." : "Groups: " + keys, "Assign groups for this payload and hit."), EditorStyles.popup, GUILayout.MinWidth(120)))
                ShowWindowGroups(session, draft, window.Start.id);
            using (new EditorGUI.DisabledScope(!session.TryDuplicatePlacement(window, out _)))
                if (GUILayout.Button("Duplicate", GUILayout.Width(70))) { DuplicateHit(session, draft, window.Start.id); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("Delete Hit", GUILayout.Width(75))) { DeleteHit(session, draft, window.Start.id); GUIUtility.ExitGUI(); }
        }
        DrawHitSummary(session, draft, window.Start.id);
        GUILayout.Label("Shared timing affects: " + session.AffectedBy(window), EditorStyles.miniLabel);
    }

    void ShowWindowGroups(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id)
    {
        var menu = new GenericMenu();
        AddWindowGroupItems(menu, "", session, draft, id);
        menu.ShowAsContext();
    }

    static void ToggleWindowGroup(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, string id, string key)
    {
        if (session == null || !session.payloads.Contains(draft)) return;
        int index = draft.startIds.IndexOf(id); if (index < 0 || index >= draft.steps.Count) return;
        session.Record("Assign Hitbox Window Group");
        var so = new SerializedObject(session);
        var keys = so.FindProperty("payloads").GetArrayElementAtIndex(session.payloads.IndexOf(draft)).FindPropertyRelative("steps")
            .GetArrayElementAtIndex(index).FindPropertyRelative("groupKeys");
        int found = -1;
        for (int i = 0; i < keys.arraySize; i++) if (keys.GetArrayElementAtIndex(i).stringValue == key) found = i;
        if (found >= 0) keys.DeleteArrayElementAtIndex(found);
        else { keys.arraySize++; keys.GetArrayElementAtIndex(keys.arraySize - 1).stringValue = key; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    void DrawRepairMarkers(SkillHitboxAuthoringSession session)
    {
        if (!showMarkers) return;
        var draft = Selected(session); if (draft == null || draft.source == null) return;
        GUILayout.Space(10); GUILayout.Label("REPAIR SHARED MARKERS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edits also affect payloads sharing these event names.", MessageType.Info);
        foreach (var marker in session.markers.Where(m => m.name == draft.source.HitboxStartEventName || m.name == draft.source.HitboxEndEventName).OrderBy(m => m.time).ToArray())
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(marker.name.ToString(), GUILayout.Width(90));
                float value = EditorGUILayout.DelayedFloatField(marker.time);
                if (value != marker.time) { session.MoveMarker(marker.id, value); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Delete", GUILayout.Width(55))) { session.RemoveMarker(marker.id); GUIUtility.ExitGUI(); }
            }
    }
}
#endif
