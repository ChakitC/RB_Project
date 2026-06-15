#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class SkillAnimationVfxEditorWindow : EditorWindow
{
    const string WindowTitle = "Animation Event VFX Timeline";
    const string MenuPath = "Tools/RB/Animation VFX/Animation Event VFX Timeline";
    const string LegacyMenuPath = "Tools/RB Tools/Skill Animation VFX Timeline";
    const string EventAssetFolder = "Assets/Data/CombatTimelineEvents";
    const float LabelWidth = 120f;
    const float RulerHeight = 28f;
    const float TrackHeight = 38f;
    const float MarkerWidth = 10f;
    const double PreviewUpdateInterval = 1d / 75d;

    readonly AnimationPreviewSession previewSession = new AnimationPreviewSession();
    readonly List<TimelineEvent> timelineEvents = new List<TimelineEvent>();
    readonly List<Track> tracks = new List<Track>();

    SetAnimationVfxData authoringTarget;
    bool isPlaying;
    bool loopPlayback;
    float playbackSpeed = 1f;
    float normalizedTime;
    double lastEditorTime;
    double nextPreviewUpdateTime;
    bool editorUpdateSubscribed;
    int draggingEventIndex = -1;
    int selectedEventIndex = -1;
    int draggingPointTrack = -1;
    int draggingRangeTrack = -1;
    bool draggingRangeEnd;
    float draggedNormalizedTime;
    CombatTimelineEventName eventToAdd = CombatTimelineEventName.Vfx;

    [MenuItem(MenuPath)]
    static void OpenWindow()
    {
        Open();
    }

    [MenuItem(LegacyMenuPath)]
    static void OpenLegacyWindow()
    {
        Open();
    }

    static void Open()
    {
        SkillAnimationVfxEditorWindow window = GetWindow<SkillAnimationVfxEditorWindow>(WindowTitle);
        window.minSize = new Vector2(720f, 430f);
        window.TryUseCurrentSelection();
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
        SkillVfxAuthoringEntry.PreviewActivityChanged -= OnPreviewActivityChanged;
        SkillVfxAuthoringEntry.PreviewActivityChanged += OnPreviewActivityChanged;
        SkillVfxAuthoringEntry.CleanupOrphanedVisualPreviews();
        TryUseCurrentSelection();
        UpdateEditorUpdateSubscription();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        Selection.selectionChanged -= OnSelectionChanged;
        SkillVfxAuthoringEntry.PreviewActivityChanged -= OnPreviewActivityChanged;
        StopPreview(true);
        StopEditorUpdates();
    }

    void OnGUI()
    {
        ClearDestroyedAuthoringTarget();
        DrawAuthoringTarget();

        IAnimationVfxTimelineSource source = GetSource();
        AnimationClip clip = GetClip(source);
        Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
        BuildTracks(source);

        DrawSourceSummary(source, clip, animator);
        DrawTransport(source, clip, animator);
        DrawEventAuthoring(source);

        float height = RulerHeight + TrackHeight * Mathf.Max(3, tracks.Count);
        Rect timelineArea = GUILayoutUtility.GetRect(
            300f, 10000f, height, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawTimeline(timelineArea, source, clip);
    }

    void DrawAuthoringTarget()
    {
        EditorGUI.BeginChangeCheck();
        SetAnimationVfxData nextTarget = (SetAnimationVfxData)EditorGUILayout.ObjectField(
            "Authoring Component", authoringTarget, typeof(SetAnimationVfxData), true);
        if (EditorGUI.EndChangeCheck())
            SetAuthoringTarget(nextTarget);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                TryUseCurrentSelection(true);
            using (new EditorGUI.DisabledScope(authoringTarget == null))
            {
                if (GUILayout.Button("Select Target", GUILayout.Width(100f)))
                    Selection.activeObject = authoringTarget;
                if (GUILayout.Button("Refresh VFX", GUILayout.Width(100f)))
                    authoringTarget.RefreshAllVisuals();
                if (GUILayout.Button("Stop VFX", GUILayout.Width(85f)))
                    authoringTarget.StopAllVfx();
            }
        }

        if (authoringTarget == null)
            return;

        ScriptableObject currentAsset = authoringTarget.TimelineSourceAsset;
        EditorGUI.BeginChangeCheck();
        ScriptableObject nextAsset = (ScriptableObject)EditorGUILayout.ObjectField(
            "Source Asset", currentAsset, typeof(ScriptableObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            List<AnimationVfxTimelineEntry> entries = AnimationVfxTimelineSourceFactory.GetEntries(nextAsset);
            SetSourceSelection(nextAsset, entries.Count > 0 ? entries[0].Id : "main");
        }

        List<AnimationVfxTimelineEntry> sourceEntries =
            AnimationVfxTimelineSourceFactory.GetEntries(authoringTarget.TimelineSourceAsset);
        if (sourceEntries.Count > 0)
        {
            int selected = FindEntryIndex(sourceEntries, authoringTarget.TimelineEntryId);
            string[] labels = new string[sourceEntries.Count];
            for (int i = 0; i < sourceEntries.Count; i++)
                labels[i] = sourceEntries[i].DisplayName;
            int next = EditorGUILayout.Popup("Entry", Mathf.Max(0, selected), labels);
            if (next != selected && next >= 0 && next < sourceEntries.Count)
                SetSourceSelection(authoringTarget.TimelineSourceAsset, sourceEntries[next].Id);
        }
        else if (authoringTarget.TimelineSourceAsset != null)
        {
            EditorGUILayout.HelpBox(
                "Unsupported source or Melee steps have no stable IDs. Run Tools > RB > Animation VFX > Assign Missing Melee Step IDs.",
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(GetSource() == null))
            {
                if (GUILayout.Button("Create / Sync VFX Slots"))
                    authoringTarget.CreateOrSyncTimelineVfxSlots();
                if (GUILayout.Button("Load VFX Data"))
                    authoringTarget.LoadTimelineVfxData();
                if (GUILayout.Button("Save VFX Data"))
                {
                    authoringTarget.SaveTimelineVfxData();
                    BuildTimelineEvents(GetSource());
                }
            }
        }
    }

    void DrawSourceSummary(IAnimationVfxTimelineSource source, AnimationClip clip, Animator animator)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Source", source?.SourceAsset, typeof(ScriptableObject), false);
                EditorGUILayout.TextField("Entry", source?.DisplayName ?? "None");
                EditorGUILayout.ObjectField("Animation Clip", clip, typeof(AnimationClip), false);
                EditorGUILayout.ObjectField("Scene Animator", animator, typeof(Animator), true);
            }

            if (authoringTarget == null)
                EditorGUILayout.HelpBox("Assign a SetAnimationVfxData or SetSkillVfxData component.", MessageType.Info);
            else if (source == null)
                EditorGUILayout.HelpBox("Select a supported SkillGemDefinition, MeleeComboSO, or CharacterAnimProfileSO entry.", MessageType.Warning);
            else if (clip == null)
                EditorGUILayout.HelpBox("The selected entry has no valid ClipTransition.", MessageType.Warning);

            EditorGUILayout.LabelField(
                "Preview Runtime",
                $"Active: {SkillVfxAuthoringEntry.ActivePreviewCount} / " +
                $"Particles: {SkillVfxAuthoringEntry.ActivePreviewParticleSystemCount} / " +
                $"CFXR: {SkillVfxAuthoringEntry.ActivePreviewCartoonFxCallbackCount} / " +
                $"Update: {SkillVfxAuthoringEntry.ActivePreviewUpdateFps:0.0} FPS");
        }
    }

    void DrawTransport(IAnimationVfxTimelineSource source, AnimationClip clip, Animator animator)
    {
        bool canPreview = !Application.isPlaying && clip != null && animator != null && animator.gameObject.activeInHierarchy;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    if (isPlaying) PausePreview(); else PlayPreview(animator, clip);
                }
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    StopPreview(true);
            }
            loopPlayback = GUILayout.Toggle(loopPlayback, "Loop", EditorStyles.toolbarButton, GUILayout.Width(48f));
            GUILayout.Label("Speed", GUILayout.Width(40f));
            playbackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.1f, 2f, GUILayout.Width(100f));
            GUILayout.Label(playbackSpeed.ToString("0.00") + "x", GUILayout.Width(42f));
            GUILayout.FlexibleSpace();
            float length = clip != null ? clip.length : 0f;
            GUILayout.Label($"{normalizedTime * length:0.000}s / {length:0.000}s   {normalizedTime * 100f:0.0}%", GUILayout.Width(210f));
            using (new EditorGUI.DisabledScope(source?.SourceAsset == null))
            {
                if (GUILayout.Button("Ping Source", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    EditorGUIUtility.PingObject(source.SourceAsset);
            }
        }
    }

    void DrawEventAuthoring(IAnimationVfxTimelineSource source)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            eventToAdd = (CombatTimelineEventName)EditorGUILayout.EnumPopup("Event At Playhead", eventToAdd);
            using (new EditorGUI.DisabledScope(source == null || eventToAdd == CombatTimelineEventName.None))
            {
                if (GUILayout.Button("Add Event", GUILayout.Width(90f)))
                    AddEventAtPlayhead(source, eventToAdd);
            }
            using (new EditorGUI.DisabledScope(source == null || selectedEventIndex < 0))
            {
                if (GUILayout.Button("Remove Selected", GUILayout.Width(120f)))
                    RemoveSelectedEvent(source);
            }
        }
    }

    void BuildTracks(IAnimationVfxTimelineSource source)
    {
        tracks.Clear();
        tracks.Add(new Track("Animation", TrackKind.Animation, null));
        if (source != null)
        {
            for (int i = 0; i < source.Lanes.Count; i++)
                tracks.Add(new Track(source.Lanes[i].Label, ToTrackKind(source.Lanes[i].Kind), source.Lanes[i]));
        }
        tracks.Add(new Track("VFX", TrackKind.Vfx, null));
        tracks.Add(new Track("Other Events", TrackKind.Other, null));
    }

    void DrawTimeline(Rect area, IAnimationVfxTimelineSource source, AnimationClip clip)
    {
        EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.13f));
        Rect contentArea = new Rect(area.x + LabelWidth, area.y, Mathf.Max(1f, area.width - LabelWidth), area.height);
        DrawRuler(new Rect(contentArea.x, area.y, contentArea.width, RulerHeight), clip);
        for (int i = 0; i < tracks.Count; i++)
            DrawTrackBackground(GetTrackRect(area, i), tracks[i].Label, i);
        if (source == null || clip == null)
            return;

        BuildTimelineEvents(source);
        DrawAnimationTrack(GetTrackContentRect(area, 0), clip);
        DrawAdapterLanes(area, source);
        DrawEventMarkers(area, source);
        DrawPlayhead(contentArea);
        HandleTimelineContextMenu(contentArea, source);
        HandleTimelineScrub(contentArea, clip);
    }

    void DrawAdapterLanes(Rect area, IAnimationVfxTimelineSource source)
    {
        Event current = Event.current;
        for (int i = 0; i < tracks.Count; i++)
        {
            Track track = tracks[i];
            Rect row = GetTrackContentRect(area, i);
            if (track.Kind == TrackKind.Point)
            {
                float time = draggingPointTrack == i ? draggedNormalizedTime : source.PointValue;
                Rect marker = GetMarkerRect(row, time);
                DrawMarker(marker, new Color(1f, 0.72f, 0.18f), track.Label);
                if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
                {
                    draggingPointTrack = i;
                    draggedNormalizedTime = time;
                    isPlaying = false;
                    current.Use();
                }
            }
            else if (track.Kind == TrackKind.Range)
            {
                Vector2 range = source.RangeValue;
                if (draggingRangeTrack == i)
                {
                    if (draggingRangeEnd) range.y = draggedNormalizedTime; else range.x = draggedNormalizedTime;
                    range = ClampOrdered(range);
                }
                float xMin = Mathf.Lerp(row.xMin, row.xMax, range.x);
                float xMax = Mathf.Lerp(row.xMin, row.xMax, range.y);
                EditorGUI.DrawRect(new Rect(xMin, row.y + 10f, Mathf.Max(2f, xMax - xMin), row.height - 20f), new Color(0.2f, 0.75f, 0.85f, 0.45f));
                Rect start = GetMarkerRect(row, range.x);
                Rect end = GetMarkerRect(row, range.y);
                DrawMarker(start, new Color(0.2f, 0.85f, 0.75f), "Open");
                DrawMarker(end, new Color(0.95f, 0.55f, 0.2f), "Close");
                if (current.type == EventType.MouseDown && current.button == 0 && (start.Contains(current.mousePosition) || end.Contains(current.mousePosition)))
                {
                    draggingRangeTrack = i;
                    draggingRangeEnd = end.Contains(current.mousePosition);
                    draggedNormalizedTime = draggingRangeEnd ? range.y : range.x;
                    isPlaying = false;
                    current.Use();
                }
            }
        }

        if ((draggingPointTrack >= 0 || draggingRangeTrack >= 0) && current.type == EventType.MouseDrag)
        {
            int trackIndex = draggingPointTrack >= 0 ? draggingPointTrack : draggingRangeTrack;
            draggedNormalizedTime = MouseToNormalized(GetTrackContentRect(area, trackIndex), current.mousePosition.x);
            ScrubTo(draggedNormalizedTime);
            current.Use();
        }
        else if ((draggingPointTrack >= 0 || draggingRangeTrack >= 0) && current.rawType == EventType.MouseUp)
        {
            if (draggingPointTrack >= 0)
                source.SetPointValue(draggedNormalizedTime);
            else
            {
                Vector2 range = source.RangeValue;
                if (draggingRangeEnd) range.y = draggedNormalizedTime; else range.x = draggedNormalizedTime;
                source.SetRangeValue(ClampOrdered(range));
            }
            source.Save();
            draggingPointTrack = -1;
            draggingRangeTrack = -1;
            current.Use();
        }
    }

    void DrawEventMarkers(Rect area, IAnimationVfxTimelineSource source)
    {
        Event current = Event.current;
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            int trackIndex = GetTrackIndex(timelineEvent.EventName);
            Rect row = GetTrackContentRect(area, trackIndex);
            float time = draggingEventIndex == timelineEvent.SerializedIndex ? draggedNormalizedTime : timelineEvent.NormalizedTime;
            Rect marker = GetMarkerRect(row, time);
            DrawMarker(marker, GetEventColor(timelineEvent.EventName), timelineEvent.DisplayName);
            if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
            {
                draggingEventIndex = timelineEvent.SerializedIndex;
                selectedEventIndex = timelineEvent.SerializedIndex;
                draggedNormalizedTime = time;
                isPlaying = false;
                SelectAndPreviewEntry(timelineEvent);
                current.Use();
            }
        }
        if (draggingEventIndex >= 0 && current.type == EventType.MouseDrag)
        {
            TimelineEvent dragged = FindTimelineEvent(draggingEventIndex);
            draggedNormalizedTime = MouseToNormalized(GetTrackContentRect(area, GetTrackIndex(dragged.EventName)), current.mousePosition.x);
            ScrubTo(draggedNormalizedTime);
            current.Use();
        }
        else if (draggingEventIndex >= 0 && current.rawType == EventType.MouseUp)
        {
            CommitEventTime(source, draggingEventIndex, draggedNormalizedTime);
            draggingEventIndex = -1;
            current.Use();
        }
    }

    void DrawRuler(Rect area, AnimationClip clip)
    {
        EditorGUI.DrawRect(area, new Color(0.17f, 0.17f, 0.17f));
        float length = clip != null ? clip.length : 0f;
        for (int i = 0; i <= 10; i++)
        {
            float n = i / 10f;
            float x = Mathf.Lerp(area.xMin, area.xMax, n);
            EditorGUI.DrawRect(new Rect(x, area.yMax - 8f, 1f, 8f), new Color(0.55f, 0.55f, 0.55f));
            GUI.Label(new Rect(x + 2f, area.y + 2f, 58f, 18f), (length * n).ToString("0.00") + "s", EditorStyles.miniLabel);
        }
    }

    static void DrawTrackBackground(Rect row, string label, int index)
    {
        EditorGUI.DrawRect(row, index % 2 == 0 ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.16f, 0.16f, 0.16f));
        EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), new Color(0.08f, 0.08f, 0.08f));
        GUI.Label(new Rect(row.x + 6f, row.y + 9f, LabelWidth - 10f, 20f), label, EditorStyles.miniBoldLabel);
    }

    static void DrawAnimationTrack(Rect row, AnimationClip clip)
    {
        Rect rect = new Rect(row.x + 2f, row.y + 7f, row.width - 4f, row.height - 14f);
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.38f, 0.58f));
        GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), clip.name, EditorStyles.whiteMiniLabel);
    }

    void HandleTimelineScrub(Rect contentArea, AnimationClip clip)
    {
        Event current = Event.current;
        if (draggingEventIndex >= 0 || draggingPointTrack >= 0 || draggingRangeTrack >= 0 || current.button != 0)
            return;
        bool scrub = current.type == EventType.MouseDown || current.type == EventType.MouseDrag;
        if (!scrub || !contentArea.Contains(current.mousePosition))
            return;
        isPlaying = false;
        ScrubTo(MouseToNormalized(contentArea, current.mousePosition.x), true);
        current.Use();
    }

    void HandleTimelineContextMenu(Rect contentArea, IAnimationVfxTimelineSource source)
    {
        Event current = Event.current;
        if (current.type != EventType.ContextClick || !contentArea.Contains(current.mousePosition))
            return;
        float time = MouseToNormalized(contentArea, current.mousePosition.x);
        var menu = new GenericMenu();
        foreach (CombatTimelineEventName eventName in Enum.GetValues(typeof(CombatTimelineEventName)))
        {
            if (eventName == CombatTimelineEventName.None)
                continue;
            CombatTimelineEventName captured = eventName;
            menu.AddItem(new GUIContent(GetEventMenuPath(captured)), false, () =>
            {
                normalizedTime = time;
                AddEventAtPlayhead(source, captured);
                ScrubTo(time);
            });
        }
        menu.ShowAsContext();
        current.Use();
    }

    void AddEventAtPlayhead(IAnimationVfxTimelineSource source, CombatTimelineEventName eventName)
    {
        if (source == null || eventName == CombatTimelineEventName.None)
            return;
        if (eventName == CombatTimelineEventName.Vfx && authoringTarget != null && !authoringTarget.PrepareTimelineAuthoring())
            return;
        BuildTimelineEvents(source);
        if (!AllowsDuplicateEvent(source, eventName))
        {
            for (int i = 0; i < timelineEvents.Count; i++)
            {
                if (timelineEvents[i].EventName == eventName)
                {
                    EditorUtility.DisplayDialog("Duplicate Timeline Event", $"'{eventName}' already exists. Move the existing marker instead.", "OK");
                    return;
                }
            }
        }
        StringAsset nameAsset = ResolveOrCreateEventNameAsset(eventName);
        ClipTransition transition = source.Transition;
        if (nameAsset == null || transition == null)
            return;
        Undo.RecordObject(source.SourceAsset, "Add Animation Timeline Event");
        AnimancerEvent.Sequence.Serializable events = transition.SerializedEvents ?? new AnimancerEvent.Sequence.Serializable();
        selectedEventIndex = events.AddEvent(Mathf.Clamp(normalizedTime, 0f, 0.999f), null, nameAsset);
        events.Events = null;
        transition.SerializedEvents = events;
        source.Save();
        BuildTimelineEvents(source);
    }

    void RemoveSelectedEvent(IAnimationVfxTimelineSource source)
    {
        AnimancerEvent.Sequence.Serializable events = source?.Transition?.SerializedEvents;
        float[] times = events?.NormalizedTimes;
        if (times == null || selectedEventIndex < 0 || selectedEventIndex >= times.Length - 1)
            return;
        TimelineEvent selected = FindTimelineEvent(selectedEventIndex);
        if (!EditorUtility.DisplayDialog("Remove Timeline Event", $"Remove '{selected.DisplayName}' from '{source.DisplayName}'?", "Remove", "Cancel"))
            return;
        Undo.RecordObject(source.SourceAsset, "Remove Animation Timeline Event");
        if (selected.EventName == CombatTimelineEventName.Vfx && authoringTarget != null)
            authoringTarget.RemoveTimelineVfxCue(selected.VfxCueIndex);
        events.RemoveEvent(selectedEventIndex);
        events.Events = null;
        source.Transition.SerializedEvents = events;
        source.Save();
        selectedEventIndex = -1;
        BuildTimelineEvents(source);
    }

    void CommitEventTime(IAnimationVfxTimelineSource source, int serializedIndex, float newTime)
    {
        AnimancerEvent.Sequence.Serializable events = source?.Transition?.SerializedEvents;
        float[] times = events?.NormalizedTimes;
        if (times == null || serializedIndex < 0 || serializedIndex >= times.Length - 1)
            return;
        IInvokable[] callbacks = events.Callbacks;
        StringAsset[] names = events.Names;
        IInvokable callback = callbacks != null && serializedIndex < callbacks.Length ? callbacks[serializedIndex] : null;
        StringAsset name = names != null && serializedIndex < names.Length ? names[serializedIndex] : null;
        TimelineEvent moved = FindTimelineEvent(serializedIndex);
        Undo.RecordObject(source.SourceAsset, "Move Animation Timeline Event");
        events.RemoveEvent(serializedIndex);
        int newIndex = events.AddEvent(Mathf.Clamp(newTime, 0f, 0.999f), callback, name);
        events.Events = null;
        source.Transition.SerializedEvents = events;
        source.Save();
        BuildTimelineEvents(source);
        TimelineEvent reordered = FindTimelineEvent(newIndex);
        if (moved.EventName == CombatTimelineEventName.Vfx && moved.VfxCueIndex >= 0 &&
            reordered.VfxCueIndex >= 0 && moved.VfxCueIndex != reordered.VfxCueIndex)
            authoringTarget?.MoveTimelineVfxCue(moved.VfxCueIndex, reordered.VfxCueIndex);
        selectedEventIndex = -1;
        BuildTimelineEvents(GetSource());
    }

    void BuildTimelineEvents(IAnimationVfxTimelineSource source)
    {
        timelineEvents.Clear();
        ClipTransition transition = source?.Transition;
        AnimancerEvent.Sequence.Serializable events = transition?.SerializedEvents;
        float[] times = events?.NormalizedTimes;
        if (times == null || times.Length <= 1)
            return;
        StringAsset[] names = events.Names;
        AnimancerEvent.Sequence runtimeEvents = transition.Events;
        int vfxCueIndex = 0;
        for (int i = 0; i < times.Length - 1; i++)
        {
            if (!float.IsFinite(times[i]))
                continue;
            string displayName = names != null && i < names.Length && names[i] != null
                ? names[i].name
                : runtimeEvents.GetName(i)?.String;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Event " + (i + 1);
            Enum.TryParse(displayName, true, out CombatTimelineEventName eventName);
            int cueIndex = eventName == CombatTimelineEventName.Vfx ? vfxCueIndex++ : -1;
            if (cueIndex >= 0)
                displayName = BuildVfxMarkerLabel(source, cueIndex);
            timelineEvents.Add(new TimelineEvent(i, Mathf.Clamp01(times[i]), displayName, eventName, cueIndex));
        }
    }

    static string BuildVfxMarkerLabel(IAnimationVfxTimelineSource source, int cueIndex)
    {
        int count = 0;
        string first = null;
        for (int i = 0; i < source.CueCount; i++)
        {
            IAnimationVfxCue cue = source.GetCue(i);
            if (cue == null || cue.CueIndex != cueIndex)
                continue;
            count++;
            first ??= cue.Prefab != null ? cue.Prefab.name : cue.Action == AnimationVfxAction.StopLoop ? $"Stop {cue.LoopKey}" : cue.Action.ToString();
        }
        if (count == 0) return $"Vfx {cueIndex + 1} (Empty)";
        return count == 1 ? first : $"{first} +{count - 1}";
    }

    void PlayPreview(Animator animator, AnimationClip clip)
    {
        previewSession.Configure(animator, clip);
        previewSession.Sample(normalizedTime);
        SyncLoopPreviewsAt(normalizedTime);
        TriggerOneShotVfxAt(normalizedTime);
        isPlaying = true;
        lastEditorTime = EditorApplication.timeSinceStartup;
        nextPreviewUpdateTime = 0d;
        UpdateEditorUpdateSubscription();
    }

    void PausePreview()
    {
        isPlaying = false;
        UpdateEditorUpdateSubscription();
    }

    void StopPreview(bool rewind)
    {
        isPlaying = false;
        authoringTarget?.StopAllVfx();
        previewSession.Stop();
        if (rewind) normalizedTime = 0f;
        RepaintViews();
        UpdateEditorUpdateSubscription();
    }

    void ScrubTo(float nextTime, bool triggerCrossedVfx = false)
    {
        float previous = normalizedTime;
        normalizedTime = Mathf.Clamp01(nextTime);
        if (triggerCrossedVfx)
            TriggerVfxBetweenScrubPositions(previous, normalizedTime);
        SyncLoopPreviewsAt(normalizedTime);
        AnimationClip clip = GetClip(GetSource());
        Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
        if (clip != null && animator != null && !Application.isPlaying)
        {
            previewSession.Configure(animator, clip);
            previewSession.Sample(normalizedTime);
        }
        RepaintViews();
    }

    void TriggerVfxBetweenScrubPositions(float previous, float next)
    {
        if (authoringTarget == null || Mathf.Approximately(previous, next))
            return;
        BuildTimelineEvents(GetSource());
        bool forward = next > previous;
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent e = timelineEvents[i];
            if (e.EventName != CombatTimelineEventName.Vfx)
                continue;
            bool crossed = forward ? e.NormalizedTime > previous && e.NormalizedTime <= next : e.NormalizedTime < previous && e.NormalizedTime >= next;
            if (crossed) authoringTarget.PlayVfx(e.VfxCueIndex);
        }
    }

    void TriggerVfxBetween(float start, float end)
    {
        BuildTimelineEvents(GetSource());
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent e = timelineEvents[i];
            if (e.EventName == CombatTimelineEventName.Vfx && e.NormalizedTime > start && e.NormalizedTime <= end)
                authoringTarget?.PlayVfx(e.VfxCueIndex);
        }
    }

    void TriggerVfxAt(float time)
    {
        BuildTimelineEvents(GetSource());
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent e = timelineEvents[i];
            if (e.EventName == CombatTimelineEventName.Vfx && Mathf.Approximately(e.NormalizedTime, time))
                authoringTarget?.PlayVfx(e.VfxCueIndex);
        }
    }

    void TriggerOneShotVfxAt(float time)
    {
        BuildTimelineEvents(GetSource());
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent e = timelineEvents[i];
            if (e.EventName == CombatTimelineEventName.Vfx && Mathf.Approximately(e.NormalizedTime, time))
                authoringTarget?.PlayOneShotVfx(e.VfxCueIndex);
        }
    }

    void SyncLoopPreviewsAt(float time)
    {
        if (authoringTarget == null)
            return;
        BuildTimelineEvents(GetSource());
        int applied = 0;
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            if (timelineEvents[i].EventName == CombatTimelineEventName.Vfx && timelineEvents[i].NormalizedTime <= time)
                applied++;
        }
        authoringTarget.SyncLoopPreviews(applied);
    }

    void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPreviewUpdateTime)
            return;
        nextPreviewUpdateTime = now + PreviewUpdateInterval;
        bool repaint = false;
        if (isPlaying)
        {
            AnimationClip clip = GetClip(GetSource());
            Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
            if (clip == null || animator == null)
            {
                StopPreview(false);
                return;
            }
            float previous = normalizedTime;
            float delta = Mathf.Max(0f, (float)(now - lastEditorTime)) * playbackSpeed;
            lastEditorTime = now;
            float next = normalizedTime + delta / Mathf.Max(0.0001f, clip.length);
            if (next >= 1f)
            {
                TriggerVfxBetween(previous, 1f);
                if (loopPlayback)
                {
                    authoringTarget?.StopAllLoopPreviews(false);
                    next = Mathf.Repeat(next, 1f);
                    TriggerVfxAt(0f);
                    TriggerVfxBetween(0f, next);
                }
                else
                {
                    next = 1f;
                    isPlaying = false;
                }
            }
            else
            {
                TriggerVfxBetween(previous, next);
            }
            normalizedTime = Mathf.Clamp01(next);
            SyncLoopPreviewsAt(normalizedTime);
            previewSession.Configure(animator, clip);
            previewSession.Sample(normalizedTime);
            repaint = true;
        }
        if (SkillVfxAuthoringEntry.UpdateActivePreviews(now))
            repaint = true;
        if (repaint)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            RepaintViews();
        }
        UpdateEditorUpdateSubscription();
    }

    void SelectAndPreviewEntry(TimelineEvent timelineEvent)
    {
        if (authoringTarget == null || timelineEvent.EventName != CombatTimelineEventName.Vfx)
            return;
        SkillVfxAuthoringSlot slot = authoringTarget.FindSlot(timelineEvent.VfxCueIndex);
        SkillVfxAuthoringEntry entry = authoringTarget.FindEntry(timelineEvent.VfxCueIndex);
        Selection.activeGameObject = slot != null ? slot.gameObject : entry != null ? entry.gameObject : null;
        authoringTarget.PlayVfx(timelineEvent.VfxCueIndex);
    }

    void SetSourceSelection(ScriptableObject asset, string entryId)
    {
        StopPreview(true);
        authoringTarget.SetTimelineSource(asset, entryId);
        authoringTarget.PrepareTimelineAuthoring();
        ResetSelection();
        Repaint();
    }

    void SetAuthoringTarget(SetAnimationVfxData target)
    {
        if (authoringTarget == target)
            return;
        StopPreview(true);
        authoringTarget = target;
        ResetSelection();
        BuildTimelineEvents(GetSource());
        Repaint();
    }

    void ResetSelection()
    {
        draggingEventIndex = -1;
        selectedEventIndex = -1;
        draggingPointTrack = -1;
        draggingRangeTrack = -1;
    }

    IAnimationVfxTimelineSource GetSource()
    {
        return authoringTarget != null
            ? AnimationVfxTimelineSourceFactory.Create(authoringTarget.TimelineSourceAsset, authoringTarget.TimelineEntryId)
            : null;
    }

    void TryUseCurrentSelection(bool clearWhenMissing = false)
    {
        GameObject selected = Selection.activeGameObject;
        SetAnimationVfxData target = selected != null ? selected.GetComponentInParent<SetAnimationVfxData>() : null;
        if (target == null && selected != null)
            target = selected.GetComponentInChildren<SetAnimationVfxData>(true);
        if (target != null || clearWhenMissing)
            SetAuthoringTarget(target);
    }

    void ClearDestroyedAuthoringTarget()
    {
        if (ReferenceEquals(authoringTarget, null) || authoringTarget != null)
            return;
        authoringTarget = null;
        previewSession.Stop();
        isPlaying = false;
        ResetSelection();
        timelineEvents.Clear();
    }

    void OnSelectionChanged() => TryUseCurrentSelection();
    void OnPreviewActivityChanged() { UpdateEditorUpdateSubscription(); Repaint(); }
    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            StopPreview(true);
    }

    void UpdateEditorUpdateSubscription()
    {
        bool needs = isPlaying || SkillVfxAuthoringEntry.HasActivePreviews;
        if (needs == editorUpdateSubscribed)
            return;
        if (needs)
        {
            nextPreviewUpdateTime = 0d;
            EditorApplication.update += OnEditorUpdate;
        }
        else EditorApplication.update -= OnEditorUpdate;
        editorUpdateSubscribed = needs;
    }

    void StopEditorUpdates()
    {
        EditorApplication.update -= OnEditorUpdate;
        editorUpdateSubscribed = false;
        nextPreviewUpdateTime = 0d;
    }

    int GetTrackIndex(CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.Vfx)
            return FindTrack(TrackKind.Vfx);
        for (int i = 0; i < tracks.Count; i++)
        {
            AnimationVfxTimelineLane lane = tracks[i].Lane;
            if (lane == null || lane.Kind != AnimationVfxTimelineLaneKind.Events)
                continue;
            for (int j = 0; j < lane.EventNames.Count; j++)
            {
                if (lane.EventNames[j] == eventName)
                    return i;
            }
        }
        return FindTrack(TrackKind.Other);
    }

    int FindTrack(TrackKind kind)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Kind == kind)
                return i;
        }
        return Mathf.Max(0, tracks.Count - 1);
    }

    static bool AllowsDuplicateEvent(IAnimationVfxTimelineSource source, CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.Vfx)
            return true;
        for (int i = 0; i < source.Lanes.Count; i++)
        {
            AnimationVfxTimelineLane lane = source.Lanes[i];
            if (!lane.AllowDuplicateEvents)
                continue;
            for (int j = 0; j < lane.EventNames.Count; j++)
            {
                if (lane.EventNames[j] == eventName)
                    return true;
            }
        }
        return false;
    }

    static int FindEntryIndex(List<AnimationVfxTimelineEntry> entries, string id)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                return i;
        }
        return 0;
    }

    TimelineEvent FindTimelineEvent(int serializedIndex)
    {
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            if (timelineEvents[i].SerializedIndex == serializedIndex)
                return timelineEvents[i];
        }
        return default;
    }

    static AnimationClip GetClip(IAnimationVfxTimelineSource source)
    {
        ClipTransition transition = source?.Transition;
        return transition != null && transition.IsValid ? transition.Clip : null;
    }

    static StringAsset ResolveOrCreateEventNameAsset(CombatTimelineEventName eventName)
    {
        StringReference reference = CombatTimelineEventNames.ToStringReference(eventName);
        StringAsset asset = StringAsset.Find(reference, out _);
        if (asset != null)
            return asset;
        EnsureAssetFolder(EventAssetFolder);
        string name = CombatTimelineEventNames.ToAnimancerEventName(eventName);
        if (string.IsNullOrWhiteSpace(name))
            return null;
        asset = ScriptableObject.CreateInstance<StringAsset>();
        asset.name = name;
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{EventAssetFolder}/{name}.asset"));
        AssetDatabase.SaveAssets();
        return asset;
    }

    static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string GetEventMenuPath(CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.Vfx) return "VFX/Vfx";
        if (eventName == CombatTimelineEventName.HitStart || eventName == CombatTimelineEventName.HitEnd) return "Hitbox/" + eventName;
        if (eventName == CombatTimelineEventName.PreCastOpen || eventName == CombatTimelineEventName.PreCastClose) return "Pre-Cast/" + eventName;
        return "Other/" + eventName;
    }

    static TrackKind ToTrackKind(AnimationVfxTimelineLaneKind kind)
    {
        return kind == AnimationVfxTimelineLaneKind.Point ? TrackKind.Point :
            kind == AnimationVfxTimelineLaneKind.Range ? TrackKind.Range : TrackKind.Events;
    }

    static Vector2 ClampOrdered(Vector2 value)
    {
        float start = Mathf.Clamp01(value.x);
        float end = Mathf.Clamp01(value.y);
        if (end < start) (start, end) = (end, start);
        return new Vector2(start, end);
    }

    void DrawPlayhead(Rect contentArea)
    {
        float x = Mathf.Lerp(contentArea.xMin, contentArea.xMax, normalizedTime);
        EditorGUI.DrawRect(new Rect(x - 1f, contentArea.y, 2f, contentArea.height), new Color(1f, 0.25f, 0.2f));
    }

    void RepaintViews() { Repaint(); SceneView.RepaintAll(); }
    static Rect GetTrackRect(Rect area, int index) => new Rect(area.x, area.y + RulerHeight + TrackHeight * index, area.width, TrackHeight);
    static Rect GetTrackContentRect(Rect area, int index)
    {
        Rect row = GetTrackRect(area, index);
        return new Rect(row.x + LabelWidth, row.y, Mathf.Max(1f, row.width - LabelWidth), row.height);
    }
    static Rect GetMarkerRect(Rect row, float normalized)
    {
        float x = Mathf.Lerp(row.xMin, row.xMax, Mathf.Clamp01(normalized));
        return new Rect(x - MarkerWidth * 0.5f, row.y + 3f, MarkerWidth, row.height - 6f);
    }
    static void DrawMarker(Rect marker, Color color, string label)
    {
        EditorGUI.DrawRect(marker, color);
        GUI.Label(new Rect(marker.xMax + 3f, marker.y + 7f, 100f, 18f), label, EditorStyles.miniLabel);
    }
    static float MouseToNormalized(Rect row, float mouseX) => Mathf.InverseLerp(row.xMin, row.xMax, mouseX);
    static Color GetEventColor(CombatTimelineEventName name)
    {
        if (name == CombatTimelineEventName.HitStart) return new Color(0.3f, 0.9f, 0.35f);
        if (name == CombatTimelineEventName.HitEnd) return new Color(0.95f, 0.35f, 0.3f);
        if (name == CombatTimelineEventName.Vfx) return new Color(0.65f, 0.35f, 1f);
        return new Color(0.35f, 0.75f, 0.95f);
    }

    enum TrackKind { Animation, Point, Events, Range, Vfx, Other }

    readonly struct Track
    {
        public readonly string Label;
        public readonly TrackKind Kind;
        public readonly AnimationVfxTimelineLane Lane;
        public Track(string label, TrackKind kind, AnimationVfxTimelineLane lane)
        {
            Label = label;
            Kind = kind;
            Lane = lane;
        }
    }

    readonly struct TimelineEvent
    {
        public readonly int SerializedIndex;
        public readonly float NormalizedTime;
        public readonly string DisplayName;
        public readonly CombatTimelineEventName EventName;
        public readonly int VfxCueIndex;
        public TimelineEvent(int serializedIndex, float time, string displayName, CombatTimelineEventName eventName, int cueIndex)
        {
            SerializedIndex = serializedIndex;
            NormalizedTime = time;
            DisplayName = displayName;
            EventName = eventName;
            VfxCueIndex = cueIndex;
        }
    }

    sealed class AnimationPreviewSession
    {
        Animator animator;
        AnimationClip clip;
        bool ownsAnimationMode;
        public void Configure(Animator nextAnimator, AnimationClip nextClip)
        {
            if (animator == nextAnimator && clip == nextClip) return;
            Stop();
            animator = nextAnimator;
            clip = nextClip;
        }
        public void Sample(float time)
        {
            if (animator == null || clip == null || !animator.gameObject.activeInHierarchy) return;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
            }
            AnimationMode.BeginSampling();
            try { AnimationMode.SampleAnimationClip(animator.gameObject, clip, Mathf.Clamp01(time) * clip.length); }
            finally { AnimationMode.EndSampling(); }
        }
        public void Stop()
        {
            if (ownsAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            ownsAnimationMode = false;
            animator = null;
            clip = null;
        }
    }
}
#endif
