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
    readonly List<float> vfxMarkerTimes = new List<float>();
    readonly List<Track> tracks = new List<Track>();

    SetAnimationVfxData authoringTarget;
    bool isPlaying;
    bool loopPlayback;
    float playbackSpeed = 1f;
    float normalizedTime;
    double lastEditorTime;
    double nextPreviewUpdateTime;
    bool editorUpdateSubscribed;
    bool _scrubSamplePending;
    bool _scrubSampleIsCutscene;
    int draggingEventIndex = -1;
    int selectedEventIndex = -1;
    int draggingPointTrack = -1;
    int draggingRangeTrack = -1;
    bool draggingRangeEnd;
    float draggedNormalizedTime;
    CombatTimelineEventName eventToAdd = CombatTimelineEventName.Vfx;
    bool _cutsceneModeActive;
    float _cutsceneSkillFraction;
    bool _playheadInCutscene;
    float _cutsceneNormalizedTime;
    int _draggingCutsceneEventIndex = -1;
    readonly List<TimelineEvent> _cutsceneTimelineEvents = new List<TimelineEvent>();

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
        editorUpdateSubscribed = false;
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
        DrawMultiModeToggle(source);

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
                if (DrawTintedButton("Save VFX Data", new Color(0.55f, 0.9f, 0.55f)))
                {
                    authoringTarget.SaveAllTimelineVfxData();
                    BuildTimelineEvents(GetSource());
                }
                if (DrawTintedButton("Load / Sync VFX Data", new Color(1f, 0.78f, 0.35f)))
                {
                    authoringTarget.LoadAllTimelineVfxData();
                }
            }
        }
    }

    static bool DrawTintedButton(string label, Color color)
    {
        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
        bool pressed = GUILayout.Button(label);
        GUI.backgroundColor = previousColor;
        return pressed;
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
                EditorGUILayout.HelpBox("Assign a SetAnimationVfxData component.", MessageType.Info);
            else if (source == null)
                EditorGUILayout.HelpBox("Select a supported SkillGemDefinition, MeleeComboSO, or CharacterAnimProfileSO entry.", MessageType.Warning);
            else if (clip == null)
                EditorGUILayout.HelpBox("The selected entry has no valid ClipTransition.", MessageType.Warning);

            EditorGUILayout.LabelField(
                "Manual Preview",
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
                    AddEventAtPlayhead(source, eventToAdd, useActiveEntry: true);
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
        _cutsceneSkillFraction = 0f;
        if (source is IAnimationVfxTimelineMultiMode m && m.ReferenceTransition is { IsValid: true } refT)
        {
            AnimationClip skillClip = GetClip(source);
            float refLen  = refT.Clip.length;
            float mainLen = skillClip != null ? skillClip.length : 0f;
            float total   = refLen + mainLen;
            _cutsceneSkillFraction = total > 0f ? refLen / total : 0f;
        }
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
        AnimationClip refClip = source is IAnimationVfxTimelineMultiMode rm && rm.ReferenceTransition is { IsValid: true } rt
            ? rt.Clip : null;
        DrawAnimationTrack(GetTrackContentRect(area, FindTrack(TrackKind.Animation)), clip, _cutsceneSkillFraction, refClip);
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
            bool readOnly = tracks[trackIndex].Lane?.ReadOnly ?? false;
            Rect row = GetTrackContentRect(area, trackIndex);
            float time = draggingEventIndex == timelineEvent.SerializedIndex ? draggedNormalizedTime : timelineEvent.NormalizedTime;
            Rect marker = GetMarkerRect(row, time);
            Color color = GetEventColor(timelineEvent.EventName);
            if (readOnly) color.a *= 0.55f;
            DrawMarker(marker, color, timelineEvent.DisplayName);
            if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
            {
                if (!readOnly)
                {
                    draggingEventIndex = timelineEvent.SerializedIndex;
                    draggedNormalizedTime = time;
                    isPlaying = false;
                }
                selectedEventIndex = timelineEvent.SerializedIndex;
                SelectAndPreviewEntry(timelineEvent);
                current.Use();
            }
            if (!readOnly && current.type == EventType.ContextClick && marker.Contains(current.mousePosition))
            {
                int capturedIndex = timelineEvent.SerializedIndex;
                string capturedName = timelineEvent.DisplayName;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent($"Remove '{capturedName}'"), false, () =>
                {
                    selectedEventIndex = capturedIndex;
                    RemoveSelectedEvent(source);
                });
                menu.ShowAsContext();
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

        if (_cutsceneSkillFraction > 0f)
        {
            for (int i = 0; i < _cutsceneTimelineEvents.Count; i++)
            {
                TimelineEvent ev = _cutsceneTimelineEvents[i];
                int trackIndex = GetTrackIndex(ev.EventName);
                Rect row = GetTrackContentRect(area, trackIndex);
                float evTime = _draggingCutsceneEventIndex == ev.SerializedIndex ? draggedNormalizedTime : ev.NormalizedTime;
                Rect marker = GetCutsceneMarkerRect(row, evTime);
                Color color = GetEventColor(ev.EventName);
                DrawMarker(marker, color, ev.DisplayName);
                if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
                {
                    _draggingCutsceneEventIndex = ev.SerializedIndex;
                    draggedNormalizedTime = evTime;
                    isPlaying = false;
                    current.Use();
                }
                if (current.type == EventType.ContextClick && marker.Contains(current.mousePosition))
                {
                    int capturedIndex = ev.SerializedIndex;
                    string capturedName = ev.DisplayName;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent($"Remove '{capturedName}'"), false, () =>
                        RemoveCutsceneEvent(source, capturedIndex));
                    menu.ShowAsContext();
                    current.Use();
                }
            }

            if (_draggingCutsceneEventIndex >= 0 && current.type == EventType.MouseDrag)
            {
                TimelineEvent dragged = FindCutsceneTimelineEvent(_draggingCutsceneEventIndex);
                Rect row = GetTrackContentRect(area, GetTrackIndex(dragged.EventName));
                float rawNorm = Mathf.InverseLerp(row.xMin, row.xMax, current.mousePosition.x);
                draggedNormalizedTime = _cutsceneSkillFraction > 0f
                    ? Mathf.Clamp01(rawNorm / _cutsceneSkillFraction)
                    : Mathf.Clamp01(rawNorm);
                ScrubToCutscene(draggedNormalizedTime);
                current.Use();
            }
            else if (_draggingCutsceneEventIndex >= 0 && current.rawType == EventType.MouseUp)
            {
                CommitCutsceneEventTime(source, _draggingCutsceneEventIndex, draggedNormalizedTime);
                _draggingCutsceneEventIndex = -1;
                current.Use();
            }
        }
    }

    void DrawRuler(Rect area, AnimationClip clip)
    {
        EditorGUI.DrawRect(area, new Color(0.17f, 0.17f, 0.17f));
        float skillLen = clip != null ? clip.length : 0f;
        float totalLen = _cutsceneSkillFraction > 0f
            ? skillLen / Mathf.Max(0.001f, 1f - _cutsceneSkillFraction)
            : skillLen;
        for (int i = 0; i <= 10; i++)
        {
            float n = i / 10f;
            float x = Mathf.Lerp(area.xMin, area.xMax, n);
            EditorGUI.DrawRect(new Rect(x, area.yMax - 8f, 1f, 8f), new Color(0.55f, 0.55f, 0.55f));
            GUI.Label(new Rect(x + 2f, area.y + 2f, 58f, 18f), (totalLen * n).ToString("0.00") + "s", EditorStyles.miniLabel);
        }
        if (_cutsceneSkillFraction > 0f)
        {
            float sepX = Mathf.Lerp(area.xMin, area.xMax, _cutsceneSkillFraction);
            EditorGUI.DrawRect(new Rect(sepX - 1f, area.y, 2f, area.height), new Color(0.3f, 0.9f, 0.6f, 0.7f));
        }
    }

    static void DrawTrackBackground(Rect row, string label, int index)
    {
        EditorGUI.DrawRect(row, index % 2 == 0 ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.16f, 0.16f, 0.16f));
        EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), new Color(0.08f, 0.08f, 0.08f));
        GUI.Label(new Rect(row.x + 6f, row.y + 9f, LabelWidth - 10f, 20f), label, EditorStyles.miniBoldLabel);
    }

    static void DrawAnimationTrack(Rect row, AnimationClip clip, float cutsceneFraction = 0f, AnimationClip refClip = null)
    {
        float totalW = row.width - 4f;
        if (cutsceneFraction > 0f && refClip != null)
        {
            float cw = totalW * cutsceneFraction;
            Rect cr = new Rect(row.x + 2f, row.y + 7f, cw, row.height - 14f);
            EditorGUI.DrawRect(cr, new Color(0.18f, 0.48f, 0.38f));
            GUI.Label(new Rect(cr.x + 4f, cr.y + 2f, Mathf.Max(0f, cr.width - 8f), 18f),
                $"{refClip.name}  ({refClip.length:0.00}s)", EditorStyles.whiteMiniLabel);
            float sw = totalW * (1f - cutsceneFraction);
            Rect sr = new Rect(row.x + 2f + cw, row.y + 7f, sw, row.height - 14f);
            EditorGUI.DrawRect(sr, new Color(0.18f, 0.38f, 0.58f));
            GUI.Label(new Rect(sr.x + 4f, sr.y + 2f, Mathf.Max(0f, sr.width - 8f), 18f), clip.name, EditorStyles.whiteMiniLabel);
        }
        else
        {
            Rect rect = new Rect(row.x + 2f, row.y + 7f, totalW, row.height - 14f);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.38f, 0.58f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), clip.name, EditorStyles.whiteMiniLabel);
        }
    }

    void HandleTimelineScrub(Rect contentArea, AnimationClip clip)
    {
        Event current = Event.current;
        if (draggingEventIndex >= 0 || _draggingCutsceneEventIndex >= 0 || draggingPointTrack >= 0 || draggingRangeTrack >= 0 || current.button != 0)
            return;
        bool scrub = current.type == EventType.MouseDown || current.type == EventType.MouseDrag;
        if (!scrub || !contentArea.Contains(current.mousePosition))
            return;
        isPlaying = false;
        float rawNorm = Mathf.InverseLerp(contentArea.xMin, contentArea.xMax, current.mousePosition.x);
        if (_cutsceneSkillFraction > 0f && rawNorm < _cutsceneSkillFraction)
        {
            ScrubToCutscene(rawNorm / _cutsceneSkillFraction);
        }
        else
        {
            _playheadInCutscene = false;
            ScrubTo(MouseToNormalized(contentArea, current.mousePosition.x));
        }
        current.Use();
    }

    void HandleTimelineContextMenu(Rect contentArea, IAnimationVfxTimelineSource source)
    {
        Event current = Event.current;
        if (current.type != EventType.ContextClick || !contentArea.Contains(current.mousePosition))
            return;
        float rawNorm = Mathf.InverseLerp(contentArea.xMin, contentArea.xMax, current.mousePosition.x);
        bool clickInCutscene = _cutsceneSkillFraction > 0f && rawNorm < _cutsceneSkillFraction;
        float capturedCutsceneTime = clickInCutscene ? Mathf.Clamp01(rawNorm / _cutsceneSkillFraction) : 0f;
        float time = clickInCutscene ? 0f : MouseToNormalized(contentArea, current.mousePosition.x);
        var menu = new GenericMenu();
        foreach (CombatTimelineEventName eventName in Enum.GetValues(typeof(CombatTimelineEventName)))
        {
            if (eventName == CombatTimelineEventName.None)
                continue;
            CombatTimelineEventName captured = eventName;
            menu.AddItem(new GUIContent(GetEventMenuPath(captured)), false, () =>
            {
                if (clickInCutscene)
                {
                    _playheadInCutscene = true;
                    _cutsceneNormalizedTime = capturedCutsceneTime;
                    normalizedTime = 0f;
                }
                else
                {
                    _playheadInCutscene = false;
                    normalizedTime = time;
                }
                AddEventAtPlayhead(source, captured);
                if (!clickInCutscene) ScrubTo(time);
            });
        }
        menu.ShowAsContext();
        current.Use();
    }

    void AddEventAtPlayhead(IAnimationVfxTimelineSource source, CombatTimelineEventName eventName, bool useActiveEntry = false)
    {
        if (source == null || eventName == CombatTimelineEventName.None)
            return;

        Debug.Log($"[AddEvent] useActiveEntry={useActiveEntry} _playheadInCutscene={_playheadInCutscene} _cutsceneModeActive={_cutsceneModeActive} IsSecondary={((source as IAnimationVfxTimelineMultiMode)?.IsSecondaryMode)} entryId={source.EntryId} clip={source.Transition?.Clip?.name}");

        if (!useActiveEntry && _playheadInCutscene
            && source is IAnimationVfxTimelineMultiMode multiMode
            && !multiMode.IsSecondaryMode
            && multiMode.ReferenceTransition is { IsValid: true })
        {
            float savedTime = normalizedTime;
            string savedEntry = authoringTarget?.ActiveAuthoringEntryId;
            try
            {
                authoringTarget?.SetActiveAuthoringEntry("cutscene");
                multiMode.SetMode(true);
                normalizedTime = _cutsceneNormalizedTime;
                AddEventAtPlayhead(source, eventName);
            }
            finally
            {
                multiMode.SetMode(false);
                normalizedTime = savedTime;
                authoringTarget?.SetActiveAuthoringEntry(savedEntry ?? CurrentAuthoringEntryId());
            }
            return;
        }

        bool isVfxEvent = eventName == CombatTimelineEventName.Vfx;
        if (isVfxEvent && source.MarkerCount > 0 &&
            authoringTarget != null && !authoringTarget.PrepareTimelineAuthoring(source))
        {
            return;
        }
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
        if (isVfxEvent && authoringTarget != null)
        {
            TimelineEvent added = FindTimelineEvent(selectedEventIndex);
            if (added.VfxCueIndex >= 0)
                authoringTarget.EnsureTimelineVfxSlot(source, added.VfxCueIndex);
        }
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

    void CommitCutsceneEventTime(IAnimationVfxTimelineSource source, int serializedIndex, float newTime)
    {
        if (source is not IAnimationVfxTimelineMultiMode multiMode || multiMode.ReferenceTransition == null)
            return;
        string savedEntry = authoringTarget?.ActiveAuthoringEntryId;
        try
        {
            authoringTarget?.SetActiveAuthoringEntry("cutscene");
            multiMode.SetMode(true);
            BuildTimelineEvents(source);
            CommitEventTime(source, serializedIndex, newTime);
        }
        finally
        {
            multiMode.SetMode(false);
            authoringTarget?.SetActiveAuthoringEntry(savedEntry ?? CurrentAuthoringEntryId());
        }
    }

    void RemoveCutsceneEvent(IAnimationVfxTimelineSource source, int serializedIndex)
    {
        if (source is not IAnimationVfxTimelineMultiMode multiMode || multiMode.ReferenceTransition == null)
            return;
        string savedEntry = authoringTarget?.ActiveAuthoringEntryId;
        multiMode.SetMode(true);
        authoringTarget?.SetActiveAuthoringEntry("cutscene");
        try
        {
            BuildTimelineEvents(source);
            TimelineEvent target = FindTimelineEvent(serializedIndex);
            if (target.EventName == CombatTimelineEventName.None)
                return;
            if (!EditorUtility.DisplayDialog("Remove Cutscene Event",
                $"Remove '{target.DisplayName}' from cutscene?", "Remove", "Cancel"))
                return;
            AnimancerEvent.Sequence.Serializable events = source.Transition?.SerializedEvents;
            if (events == null)
                return;
            Undo.RecordObject(source.SourceAsset, "Remove Cutscene Timeline Event");
            if (target.EventName == CombatTimelineEventName.Vfx && authoringTarget != null)
                authoringTarget.RemoveTimelineVfxCue(target.VfxCueIndex);
            events.RemoveEvent(serializedIndex);
            events.Events = null;
            source.Transition.SerializedEvents = events;
            source.Save();
        }
        finally
        {
            multiMode.SetMode(false);
            authoringTarget?.SetActiveAuthoringEntry(savedEntry ?? CurrentAuthoringEntryId());
            BuildTimelineEvents(GetSource());
        }
    }

    void BuildTimelineEvents(IAnimationVfxTimelineSource source)
    {
        timelineEvents.Clear();
        _cutsceneTimelineEvents.Clear();
        ClipTransition transition = source?.Transition;
        AnimancerEvent.Sequence.Serializable events = transition?.SerializedEvents;
        float[] times = events?.NormalizedTimes;
        if (times != null && times.Length > 1)
        {
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

        if (_cutsceneSkillFraction > 0f && source is IAnimationVfxTimelineMultiMode mm && !mm.IsSecondaryMode)
        {
            ClipTransition refT = mm.ReferenceTransition;
            if (refT != null && refT.IsValid)
                BuildCutsceneTimelineEventsFrom(refT, mm.ReferenceCueSource);
        }
    }

    void BuildCutsceneTimelineEventsFrom(ClipTransition transition, IAnimationVfxCueSource cueSource)
    {
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
            if (cueIndex >= 0 && cueSource != null)
                displayName = BuildVfxMarkerLabel(cueSource, cueIndex);
            _cutsceneTimelineEvents.Add(new TimelineEvent(i, Mathf.Clamp01(times[i]), displayName, eventName, cueIndex));
        }
    }

    static string BuildVfxMarkerLabel(IAnimationVfxCueSource source, int cueIndex)
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
        IAnimationVfxTimelineSource source = GetSource();
        AnimationClip cutsceneClip = GetCutsceneClip(source);
        Animator cutsceneAnimator = cutsceneClip != null ? authoringTarget?.CutscenePreviewAnimator : null;
        if (cutsceneClip != null && cutsceneAnimator != null && (_playheadInCutscene || normalizedTime == 0f))
        {
            if (!_playheadInCutscene)
            {
                _playheadInCutscene = true;
                _cutsceneNormalizedTime = 0f;
            }
            AnimationClip cameraClip = GetCutsceneCameraClip(source);
            Animator cameraAnimator = authoringTarget?.CutsceneCameraAnimator;
            previewSession.Configure(cutsceneAnimator, cutsceneClip);
            if (cameraClip != null && cameraAnimator != null)
                previewSession.ConfigureSecondary(cameraAnimator, cameraClip);
            else
                previewSession.ClearSecondary();
            previewSession.Sample(_cutsceneNormalizedTime);
            SampleCutsceneTimelineVfx(cutsceneClip, true);
        }
        else
        {
            _playheadInCutscene = false;
            ConfigureMainPreview(source, animator, clip);
            previewSession.Sample(normalizedTime);
            SampleTimelineVfx(clip, true);
        }
        isPlaying = true;
        _scrubSamplePending = false;
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
        _scrubSamplePending = false;
        authoringTarget?.StopAllVfx();
        previewSession.Stop();
        if (rewind)
        {
            normalizedTime = 0f;
            _cutsceneNormalizedTime = 0f;
            _playheadInCutscene = _cutsceneSkillFraction > 0f;
        }
        else
        {
            _playheadInCutscene = false;
        }
        RepaintViews();
        UpdateEditorUpdateSubscription();
    }

    // Scrub requests arrive one-per-MouseDrag while the user drags the playhead. Sampling the
    // animation on every one of those events spams AnimationMode/TempJob allocations faster than
    // the editor player loop can reclaim them, which trips Unity's "Internal: deleting an allocation
    // that is older than its permitted lifetime of 4 frames" warning. Instead we only record the
    // requested playhead time here and coalesce the actual sample into a single pass per editor
    // tick (OnEditorUpdate), which also ticks the player loop so those temp allocations get freed.
    void ScrubTo(float nextTime)
    {
        normalizedTime = Mathf.Clamp01(nextTime);
        RequestScrubSample(false);
    }

    void ScrubToCutscene(float cutsceneNorm)
    {
        _cutsceneNormalizedTime = Mathf.Clamp01(cutsceneNorm);
        _playheadInCutscene = true;
        RequestScrubSample(true);
    }

    void RequestScrubSample(bool cutscene)
    {
        _scrubSamplePending = true;
        _scrubSampleIsCutscene = cutscene;
        UpdateEditorUpdateSubscription();
        RepaintViews();
    }

    // Runs at most once per editor tick (throttled by PreviewUpdateInterval) regardless of how many
    // scrub requests were queued since the last tick, so a fast drag collapses to a single sample.
    void PerformPendingScrubSample()
    {
        _scrubSamplePending = false;
        if (_scrubSampleIsCutscene)
            SampleCutsceneAtPlayhead();
        else
            SampleMainAtPlayhead();
    }

    void SampleMainAtPlayhead()
    {
        IAnimationVfxTimelineSource source = GetSource();
        AnimationClip clip = GetClip(source);
        Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
        if (clip != null && animator != null && !Application.isPlaying)
        {
            ConfigureMainPreview(source, animator, clip);
            previewSession.Sample(normalizedTime);
            SampleTimelineVfx(clip, true);
        }
        else
            authoringTarget?.StopTimelineVfxPreview();
    }

    void SampleCutsceneAtPlayhead()
    {
        IAnimationVfxTimelineSource source = GetSource();
        AnimationClip cutsceneClip = GetCutsceneClip(source);
        Animator cutsceneAnimator = authoringTarget?.CutscenePreviewAnimator;
        if (cutsceneClip != null && cutsceneAnimator != null && !Application.isPlaying)
        {
            AnimationClip cameraClip = GetCutsceneCameraClip(source);
            Animator cameraAnimator = authoringTarget?.CutsceneCameraAnimator;
            previewSession.Configure(cutsceneAnimator, cutsceneClip);
            if (cameraClip != null && cameraAnimator != null)
                previewSession.ConfigureSecondary(cameraAnimator, cameraClip);
            else
                previewSession.ClearSecondary();
            previewSession.Sample(_cutsceneNormalizedTime);
            SampleCutsceneTimelineVfx(cutsceneClip, true);
        }
        else
            authoringTarget?.StopTimelineVfxPreview();
    }

    void SampleTimelineVfx(AnimationClip clip, bool forceRestart)
    {
        if (authoringTarget == null || clip == null)
            return;

        BuildTimelineEvents(GetSource());
        vfxMarkerTimes.Clear();
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (timelineEvent.EventName == CombatTimelineEventName.Vfx)
                vfxMarkerTimes.Add(timelineEvent.NormalizedTime * clip.length);
        }

        authoringTarget.SampleTimelineVfx(normalizedTime * clip.length, vfxMarkerTimes, forceRestart);
    }

    // Samples the cutscene clip's VFX cues against its own authoring container while the playhead
    // is inside the cutscene section (split timeline, Main toggle). _cutsceneTimelineEvents is
    // populated by BuildTimelineEvents whenever the source is a multi-mode in its primary (Main) mode.
    void SampleCutsceneTimelineVfx(AnimationClip cutsceneClip, bool forceRestart)
    {
        if (authoringTarget == null || cutsceneClip == null)
            return;

        BuildTimelineEvents(GetSource());
        vfxMarkerTimes.Clear();
        for (int i = 0; i < _cutsceneTimelineEvents.Count; i++)
        {
            TimelineEvent ev = _cutsceneTimelineEvents[i];
            if (ev.EventName == CombatTimelineEventName.Vfx)
                vfxMarkerTimes.Add(ev.NormalizedTime * cutsceneClip.length);
        }

        authoringTarget.SetActiveAuthoringEntry("cutscene");
        authoringTarget.SampleTimelineVfx(_cutsceneNormalizedTime * cutsceneClip.length, vfxMarkerTimes, forceRestart);
    }

    void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPreviewUpdateTime)
            return;
        nextPreviewUpdateTime = now + PreviewUpdateInterval;
        bool repaint = false;
        if (_scrubSamplePending)
        {
            PerformPendingScrubSample();
            repaint = true;
        }
        if (isPlaying)
        {
            float delta = Mathf.Max(0f, (float)(now - lastEditorTime)) * playbackSpeed;
            lastEditorTime = now;
            if (_playheadInCutscene)
            {
                IAnimationVfxTimelineSource source = GetSource();
                AnimationClip cutsceneClip = GetCutsceneClip(source);
                Animator cutsceneAnimator = authoringTarget?.CutscenePreviewAnimator;
                if (cutsceneClip == null || cutsceneAnimator == null)
                {
                    StopPreview(false);
                    return;
                }
                float next = _cutsceneNormalizedTime + delta / Mathf.Max(0.0001f, cutsceneClip.length);
                if (next >= 1f)
                {
                    _playheadInCutscene = false;
                    normalizedTime = 0f;
                    AnimationClip skillClip = GetClip(source);
                    Animator skillAnimator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
                    if (skillClip != null && skillAnimator != null)
                    {
                        ConfigureMainPreview(source, skillAnimator, skillClip);
                        previewSession.Sample(normalizedTime);
                        SampleTimelineVfx(skillClip, true);
                    }
                    else
                    {
                        isPlaying = false;
                    }
                }
                else
                {
                    _cutsceneNormalizedTime = next;
                    AnimationClip cameraClip = GetCutsceneCameraClip(source);
                    Animator cameraAnimator = authoringTarget?.CutsceneCameraAnimator;
                    previewSession.Configure(cutsceneAnimator, cutsceneClip);
                    if (cameraClip != null && cameraAnimator != null)
                        previewSession.ConfigureSecondary(cameraAnimator, cameraClip);
                    else
                        previewSession.ClearSecondary();
                    previewSession.Sample(_cutsceneNormalizedTime);
                    SampleCutsceneTimelineVfx(cutsceneClip, false);
                }
            }
            else
            {
                IAnimationVfxTimelineSource mainSource = GetSource();
                AnimationClip clip = GetClip(mainSource);
                Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
                if (clip == null || animator == null)
                {
                    StopPreview(false);
                    return;
                }
                float next = normalizedTime + delta / Mathf.Max(0.0001f, clip.length);
                if (next >= 1f)
                {
                    if (loopPlayback)
                    {
                        IAnimationVfxTimelineSource loopSource = mainSource;
                        AnimationClip cutsceneClip = GetCutsceneClip(loopSource);
                        Animator cutsceneAnimator = authoringTarget?.CutscenePreviewAnimator;
                        if (cutsceneClip != null && cutsceneAnimator != null)
                        {
                            _playheadInCutscene = true;
                            _cutsceneNormalizedTime = 0f;
                            AnimationClip cameraClip = GetCutsceneCameraClip(loopSource);
                            Animator cameraAnimator = authoringTarget?.CutsceneCameraAnimator;
                            previewSession.Configure(cutsceneAnimator, cutsceneClip);
                            if (cameraClip != null && cameraAnimator != null)
                                previewSession.ConfigureSecondary(cameraAnimator, cameraClip);
                            else
                                previewSession.ClearSecondary();
                            previewSession.Sample(0f);
                            SampleCutsceneTimelineVfx(cutsceneClip, true);
                        }
                        else
                        {
                            normalizedTime = Mathf.Repeat(next, 1f);
                            ConfigureMainPreview(loopSource, animator, clip);
                            previewSession.Sample(normalizedTime);
                            SampleTimelineVfx(clip, true);
                        }
                    }
                    else
                    {
                        normalizedTime = 1f;
                        isPlaying = false;
                        ConfigureMainPreview(mainSource, animator, clip);
                        previewSession.Sample(normalizedTime);
                        SampleTimelineVfx(clip, false);
                    }
                }
                else
                {
                    normalizedTime = next;
                    ConfigureMainPreview(mainSource, animator, clip);
                    previewSession.Sample(normalizedTime);
                    SampleTimelineVfx(clip, false);
                }
            }
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
        ScrubTo(timelineEvent.NormalizedTime);
    }

    void SetSourceSelection(ScriptableObject asset, string entryId)
    {
        StopPreview(true);
        authoringTarget.SetTimelineSource(asset, entryId);
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
        _draggingCutsceneEventIndex = -1;
        _cutsceneModeActive = false;
        _playheadInCutscene = false;
        authoringTarget?.SetActiveAuthoringEntry(CurrentAuthoringEntryId());
    }

    void DrawMultiModeToggle(IAnimationVfxTimelineSource source)
    {
        if (source is not IAnimationVfxTimelineMultiMode multi)
            return;
        EditorGUILayout.BeginHorizontal();
        bool wantMain = GUILayout.Toggle(!_cutsceneModeActive, "Main", EditorStyles.miniButtonLeft);
        bool wantSecondary = GUILayout.Toggle(_cutsceneModeActive, multi.SecondaryModeLabel, EditorStyles.miniButtonRight);
        EditorGUILayout.EndHorizontal();
        if (wantMain && _cutsceneModeActive)
        {
            _cutsceneModeActive = false;
            authoringTarget?.SetActiveAuthoringEntry(CurrentAuthoringEntryId());
            Repaint();
        }
        else if (wantSecondary && !_cutsceneModeActive)
        {
            _cutsceneModeActive = true;
            authoringTarget?.SetActiveAuthoringEntry(CurrentAuthoringEntryId());
            Repaint();
        }
    }

    IAnimationVfxTimelineSource GetSource()
    {
        if (authoringTarget == null)
            return null;
        IAnimationVfxTimelineSource source = AnimationVfxTimelineSourceFactory.Create(
            authoringTarget.TimelineSourceAsset, authoringTarget.TimelineEntryId);
        if (source is IAnimationVfxTimelineMultiMode multiMode)
        {
            multiMode.SetMode(_cutsceneModeActive);
            authoringTarget.SetActiveAuthoringEntry(CurrentAuthoringEntryId());
        }
        return source;
    }

    string CurrentAuthoringEntryId()
    {
        return _cutsceneModeActive ? "cutscene" : "main";
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
        _scrubSamplePending = false;
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
        bool needs = isPlaying || _scrubSamplePending || SkillVfxAuthoringEntry.HasActivePreviews;
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

    TimelineEvent FindCutsceneTimelineEvent(int serializedIndex)
    {
        for (int i = 0; i < _cutsceneTimelineEvents.Count; i++)
        {
            if (_cutsceneTimelineEvents[i].SerializedIndex == serializedIndex)
                return _cutsceneTimelineEvents[i];
        }
        return default;
    }

    static AnimationClip GetClip(IAnimationVfxTimelineSource source)
    {
        ClipTransition transition = source?.Transition;
        return transition != null && transition.IsValid ? transition.Clip : null;
    }
    static AnimationClip GetCutsceneClip(IAnimationVfxTimelineSource source)
    {
        if (source is not IAnimationVfxTimelineMultiMode mm) return null;
        ClipTransition refT = mm.ReferenceTransition;
        return refT != null && refT.IsValid ? refT.Clip : null;
    }
    static AnimationClip GetCutsceneCameraClip(IAnimationVfxTimelineSource source)
    {
        return source?.SourceAsset switch
        {
            SkillGemDefinition skill => skill.CutsceneDef?.cameraCutsceneClip,
            CutsceneDefSO cutscene => cutscene.cutscene?.cameraCutsceneClip,
            _ => null,
        };
    }

    void ConfigureMainPreview(
        IAnimationVfxTimelineSource source,
        Animator animator,
        AnimationClip clip)
    {
        previewSession.Configure(animator, clip);
        if (source?.SourceAsset is CutsceneDefSO)
        {
            AnimationClip cameraClip = GetCutsceneCameraClip(source);
            Animator cameraAnimator = authoringTarget?.CutsceneCameraAnimator;
            if (cameraClip != null && cameraAnimator != null)
            {
                previewSession.ConfigureSecondary(cameraAnimator, cameraClip);
                return;
            }
        }

        previewSession.ClearSecondary();
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
        if (eventName == CombatTimelineEventName.HitLag) return "Feedback/" + eventName;
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
        float combined = _playheadInCutscene
            ? _cutsceneNormalizedTime * _cutsceneSkillFraction
            : _cutsceneSkillFraction + normalizedTime * (1f - _cutsceneSkillFraction);
        float x = Mathf.Lerp(contentArea.xMin, contentArea.xMax, combined);
        EditorGUI.DrawRect(new Rect(x - 1f, contentArea.y, 2f, contentArea.height), new Color(1f, 0.25f, 0.2f));
    }

    void RepaintViews() { Repaint(); SceneView.RepaintAll(); }
    static Rect GetTrackRect(Rect area, int index) => new Rect(area.x, area.y + RulerHeight + TrackHeight * index, area.width, TrackHeight);
    static Rect GetTrackContentRect(Rect area, int index)
    {
        Rect row = GetTrackRect(area, index);
        return new Rect(row.x + LabelWidth, row.y, Mathf.Max(1f, row.width - LabelWidth), row.height);
    }
    Rect GetMarkerRect(Rect row, float normalized)
    {
        float adjusted = _cutsceneSkillFraction + Mathf.Clamp01(normalized) * (1f - _cutsceneSkillFraction);
        float x = Mathf.Lerp(row.xMin, row.xMax, adjusted);
        return new Rect(x - MarkerWidth * 0.5f, row.y + 3f, MarkerWidth, row.height - 6f);
    }
    Rect GetCutsceneMarkerRect(Rect row, float cutsceneNormalized)
    {
        float combined = Mathf.Clamp01(cutsceneNormalized) * _cutsceneSkillFraction;
        float x = Mathf.Lerp(row.xMin, row.xMax, combined);
        return new Rect(x - MarkerWidth * 0.5f, row.y + 3f, MarkerWidth, row.height - 6f);
    }
    static void DrawMarker(Rect marker, Color color, string label)
    {
        EditorGUI.DrawRect(marker, color);
        GUI.Label(new Rect(marker.xMax + 3f, marker.y + 7f, 100f, 18f), label, EditorStyles.miniLabel);
    }
    float MouseToNormalized(Rect row, float mouseX)
    {
        float raw = Mathf.InverseLerp(row.xMin, row.xMax, mouseX);
        if (_cutsceneSkillFraction <= 0f) return raw;
        return Mathf.Clamp01((raw - _cutsceneSkillFraction) / Mathf.Max(0.001f, 1f - _cutsceneSkillFraction));
    }
    static Color GetEventColor(CombatTimelineEventName name)
    {
        if (name == CombatTimelineEventName.HitStart) return new Color(0.3f, 0.9f, 0.35f);
        if (name == CombatTimelineEventName.HitEnd) return new Color(0.95f, 0.35f, 0.3f);
        if (name == CombatTimelineEventName.Vfx) return new Color(0.65f, 0.35f, 1f);
        if (name == CombatTimelineEventName.HitLag) return new Color(0.95f, 0.85f, 0.25f);
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
        Animator secondaryAnimator;
        AnimationClip secondaryClip;
        bool ownsAnimationMode;

        // Re-origin state: keeps the character at its placed scene position while still
        // showing root-motion displacement relative to where the animation was authored.
        bool rootOffsetReady;
        Vector3 placedPos;       // character world position when preview started
        Quaternion placedRot;    // character world rotation when preview started
        Vector3 clipOriginPos;   // clip's root position at t=0 (authored origin)
        Quaternion clipOriginRot;// clip's root rotation at t=0

        public void Configure(Animator nextAnimator, AnimationClip nextClip)
        {
            if (animator == nextAnimator && clip == nextClip) return;
            Stop();
            animator = nextAnimator;
            clip = nextClip;
        }
        public void ConfigureSecondary(Animator nextAnimator, AnimationClip nextClip)
        {
            secondaryAnimator = nextAnimator;
            secondaryClip = nextClip;
        }
        public void ClearSecondary()
        {
            secondaryAnimator = null;
            secondaryClip = null;
        }
        public void Sample(float time)
        {
            if (animator == null || clip == null || !animator.gameObject.activeInHierarchy) return;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
            }
            float t = Mathf.Clamp01(time);
            bool hasSecondary = secondaryAnimator != null && secondaryClip != null
                && secondaryAnimator.gameObject.activeInHierarchy;

            // On the first sample after entering animation mode, record the character's
            // placed position and the clip's authored root pose at t=0. Every subsequent
            // frame we apply only the *displacement* from that authored origin on top of
            // the placed position. This means:
            //   - in-place animations → character stays at its scene position
            //   - root-motion animations → character moves by the same delta as the clip
            // NOTE: do NOT re-root the secondary (camera) animator — camera clips are
            // authored to sweep freely through the scene and must animate unrestrained.
            if (!rootOffsetReady)
            {
                placedPos = animator.transform.position;
                placedRot = animator.transform.rotation;
                AnimationMode.BeginSampling();
                try { AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0f); }
                finally { AnimationMode.EndSampling(); }
                clipOriginPos = animator.transform.position;
                clipOriginRot = animator.transform.rotation;
                animator.transform.SetPositionAndRotation(placedPos, placedRot);
                rootOffsetReady = true;
            }

            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, t * clip.length);
                if (hasSecondary)
                    AnimationMode.SampleAnimationClip(secondaryAnimator.gameObject, secondaryClip, t * secondaryClip.length);
            }
            finally { AnimationMode.EndSampling(); }

            // Re-root: displace from placed position by the clip's motion delta.
            Vector3 displacement = animator.transform.position - clipOriginPos;
            Quaternion rotDelta = Quaternion.Inverse(clipOriginRot) * animator.transform.rotation;
            animator.transform.SetPositionAndRotation(placedPos + displacement, placedRot * rotDelta);
        }
        public void Stop()
        {
            if (ownsAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            ownsAnimationMode = false;
            rootOffsetReady = false;
            animator = null;
            clip = null;
            secondaryAnimator = null;
            secondaryClip = null;
        }
    }
}
#endif
