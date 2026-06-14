#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

public sealed class SkillAnimationVfxEditorWindow : EditorWindow
{
    const string WindowTitle = "Skill Animation VFX";
    const string MenuPath = "Tools/RB Tools/Skill Animation VFX Timeline";
    const string EventAssetFolder = "Assets/Data/CombatTimelineEvents";
    const float LabelWidth = 110f;
    const float RulerHeight = 28f;
    const float TrackHeight = 38f;
    const float MarkerWidth = 10f;
    const double PreviewUpdateInterval = 1d / 75d;

    readonly SkillAnimationPreviewSession previewSession = new SkillAnimationPreviewSession();
    readonly List<TimelineEvent> timelineEvents = new List<TimelineEvent>();

    SetSkillVfxData authoringTarget;
    bool isPlaying;
    bool loopPlayback;
    float playbackSpeed = 1f;
    float normalizedTime;
    double lastEditorTime;
    double nextPreviewUpdateTime;
    bool editorUpdateSubscribed;

    int draggingEventIndex = -1;
    int selectedEventIndex = -1;
    bool draggingCastPoint;
    float draggedNormalizedTime;
    CombatTimelineEventName eventToAdd = CombatTimelineEventName.Vfx;

    [MenuItem(MenuPath)]
    static void OpenWindow()
    {
        SkillAnimationVfxEditorWindow window = GetWindow<SkillAnimationVfxEditorWindow>(WindowTitle);
        window.minSize = new Vector2(680f, 390f);
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

        SkillGemDefinition skill = GetSkill();
        AnimationClip clip = GetClip(skill);
        Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;

        DrawSkillSummary(skill, clip, animator);
        DrawTransport(skill, clip, animator);
        DrawEventAuthoring(skill);

        Rect timelineArea = GUILayoutUtility.GetRect(
            300f,
            10000f,
            RulerHeight + TrackHeight * 5f,
            10000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));

        DrawTimeline(timelineArea, skill, clip);
    }

    void DrawAuthoringTarget()
    {
        EditorGUI.BeginChangeCheck();
        SetSkillVfxData nextTarget = (SetSkillVfxData)EditorGUILayout.ObjectField(
            "Set Skill VFX Data",
            authoringTarget,
            typeof(SetSkillVfxData),
            true);
        if (EditorGUI.EndChangeCheck())
            SetAuthoringTarget(nextTarget);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                TryUseCurrentSelection(true);

            using (new EditorGUI.DisabledScope(authoringTarget == null))
            {
                if (GUILayout.Button("Select Target", GUILayout.Width(110f)))
                    Selection.activeObject = authoringTarget;

                if (GUILayout.Button("Refresh VFX Visuals", GUILayout.Width(140f)))
                    authoringTarget.RefreshAllVisuals();

                if (GUILayout.Button("Stop VFX", GUILayout.Width(90f)))
                    authoringTarget.StopAllVfx();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(authoringTarget == null || authoringTarget.Skill == null))
            {
                if (GUILayout.Button("Create / Sync VFX Slots"))
                    authoringTarget.CreateOrSyncVfxSlotsFromTimeline();

                if (GUILayout.Button("Save VFX Data"))
                {
                    authoringTarget.SaveVfxToSkill();
                    BuildTimelineEvents(authoringTarget.Skill);
                }
            }
        }
    }

    void DrawSkillSummary(SkillGemDefinition skill, AnimationClip clip, Animator animator)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Skill Definition", skill, typeof(SkillGemDefinition), false);
                EditorGUILayout.ObjectField("Animation Clip", clip, typeof(AnimationClip), false);
                EditorGUILayout.ObjectField("Scene Animator", animator, typeof(Animator), true);
            }

            if (authoringTarget == null)
                EditorGUILayout.HelpBox("Assign a SetSkillVfxData component from the scene or Prefab Mode.", MessageType.Info);
            else if (skill == null)
                EditorGUILayout.HelpBox("The selected SetSkillVfxData has no Skill assigned.", MessageType.Warning);
            else if (clip == null)
                EditorGUILayout.HelpBox("The assigned Skill Definition has no valid Skill Clip.", MessageType.Warning);
            else if (animator == null)
                EditorGUILayout.HelpBox("No Animator was found under the Character Root resolved by SetSkillVfxData.", MessageType.Warning);

            EditorGUILayout.LabelField(
                "Preview Runtime",
                $"Active: {SkillVfxAuthoringEntry.ActivePreviewCount} / " +
                $"Particle Systems: {SkillVfxAuthoringEntry.ActivePreviewParticleSystemCount} / " +
                $"CFXR: {SkillVfxAuthoringEntry.ActivePreviewCartoonFxCallbackCount} / " +
                $"Update: {SkillVfxAuthoringEntry.ActivePreviewUpdateFps:0.0} FPS");
        }
    }

    void DrawTransport(SkillGemDefinition skill, AnimationClip clip, Animator animator)
    {
        bool canPreview = !Application.isPlaying && clip != null && animator != null && animator.gameObject.activeInHierarchy;

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    if (isPlaying)
                        PausePreview();
                    else
                        PlayPreview(animator, clip);
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
            GUILayout.Label(
                string.Format("{0:0.000}s / {1:0.000}s   {2:0.0}%", normalizedTime * length, length, normalizedTime * 100f),
                GUILayout.Width(210f));

            using (new EditorGUI.DisabledScope(skill == null))
            {
                if (GUILayout.Button("Ping Skill", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    EditorGUIUtility.PingObject(skill);
            }
        }
    }

    void DrawEventAuthoring(SkillGemDefinition skill)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            eventToAdd = (CombatTimelineEventName)EditorGUILayout.EnumPopup("Event At Playhead", eventToAdd);

            bool canAdd = skill != null && eventToAdd != CombatTimelineEventName.None;
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add Event", GUILayout.Width(90f)))
                    AddEventAtPlayhead(skill, eventToAdd);
            }

            using (new EditorGUI.DisabledScope(skill == null || selectedEventIndex < 0))
            {
                if (GUILayout.Button("Remove Selected", GUILayout.Width(120f)))
                    RemoveSelectedEvent(skill);
            }
        }
    }

    void DrawTimeline(Rect area, SkillGemDefinition skill, AnimationClip clip)
    {
        EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.13f));

        Rect contentArea = new Rect(area.x + LabelWidth, area.y, Mathf.Max(1f, area.width - LabelWidth), area.height);
        Rect rulerArea = new Rect(contentArea.x, area.y, contentArea.width, RulerHeight);
        DrawRuler(rulerArea, clip);

        string[] labels = { "Animation", "Cast Point", "Hitbox", "VFX", "Other Events" };
        for (int i = 0; i < labels.Length; i++)
        {
            Rect row = GetTrackRect(area, i);
            DrawTrackBackground(row, labels[i], i);
        }

        if (skill == null || clip == null)
            return;

        BuildTimelineEvents(skill);
        DrawAnimationTrack(GetTrackContentRect(area, 0), clip);
        DrawCastPointMarker(GetTrackContentRect(area, 1), skill);
        DrawEventMarkers(area, skill);
        DrawPlayhead(contentArea);
        HandleTimelineContextMenu(contentArea, skill);
        HandleTimelineScrub(contentArea, clip);
    }

    void DrawRuler(Rect area, AnimationClip clip)
    {
        EditorGUI.DrawRect(area, new Color(0.17f, 0.17f, 0.17f));
        float length = clip != null ? clip.length : 0f;

        for (int i = 0; i <= 10; i++)
        {
            float normalized = i / 10f;
            float x = Mathf.Lerp(area.xMin, area.xMax, normalized);
            EditorGUI.DrawRect(new Rect(x, area.yMax - 8f, 1f, 8f), new Color(0.55f, 0.55f, 0.55f));
            GUI.Label(new Rect(x + 2f, area.y + 2f, 58f, 18f), (length * normalized).ToString("0.00") + "s", EditorStyles.miniLabel);
        }
    }

    static void DrawTrackBackground(Rect row, string label, int index)
    {
        Color background = index % 2 == 0
            ? new Color(0.19f, 0.19f, 0.19f)
            : new Color(0.16f, 0.16f, 0.16f);
        EditorGUI.DrawRect(row, background);
        EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), new Color(0.08f, 0.08f, 0.08f));
        GUI.Label(new Rect(row.x + 6f, row.y + 9f, LabelWidth - 10f, 20f), label, EditorStyles.miniBoldLabel);
    }

    static void DrawAnimationTrack(Rect row, AnimationClip clip)
    {
        Rect clipRect = new Rect(row.x + 2f, row.y + 7f, row.width - 4f, row.height - 14f);
        EditorGUI.DrawRect(clipRect, new Color(0.18f, 0.38f, 0.58f));
        GUI.Label(new Rect(clipRect.x + 6f, clipRect.y + 2f, clipRect.width - 12f, 18f), clip.name, EditorStyles.whiteMiniLabel);
    }

    void DrawCastPointMarker(Rect row, SkillGemDefinition skill)
    {
        float markerTime = draggingCastPoint ? draggedNormalizedTime : skill.GetCastPointNormalized();
        Rect marker = GetMarkerRect(row, markerTime);
        DrawMarker(marker, new Color(1f, 0.72f, 0.18f), "Cast");

        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
        {
            draggingCastPoint = true;
            draggedNormalizedTime = markerTime;
            isPlaying = false;
            current.Use();
        }
        else if (draggingCastPoint && current.type == EventType.MouseDrag)
        {
            draggedNormalizedTime = MouseToNormalized(row, current.mousePosition.x);
            ScrubTo(draggedNormalizedTime);
            current.Use();
        }
        else if (draggingCastPoint && current.rawType == EventType.MouseUp)
        {
            Undo.RecordObject(skill, "Move Skill Cast Point");
            skill.castPointNormalized = Mathf.Clamp(draggedNormalizedTime, 0f, 0.999f);
            EditorUtility.SetDirty(skill);
            draggingCastPoint = false;
            current.Use();
        }
    }

    void DrawEventMarkers(Rect area, SkillGemDefinition skill)
    {
        Event current = Event.current;

        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            int trackIndex = GetTrackIndex(timelineEvent.EventName);
            Rect row = GetTrackContentRect(area, trackIndex);
            float markerTime = draggingEventIndex == timelineEvent.SerializedIndex
                ? draggedNormalizedTime
                : timelineEvent.NormalizedTime;
            Rect marker = GetMarkerRect(row, markerTime);

            DrawMarker(marker, GetEventColor(timelineEvent.EventName), timelineEvent.DisplayName);

            if (current.type == EventType.MouseDown && current.button == 0 && marker.Contains(current.mousePosition))
            {
                draggingEventIndex = timelineEvent.SerializedIndex;
                selectedEventIndex = timelineEvent.SerializedIndex;
                draggedNormalizedTime = markerTime;
                isPlaying = false;
                SelectAndPreviewEntry(timelineEvent);
                current.Use();
            }
        }

        if (draggingEventIndex >= 0 && current.type == EventType.MouseDrag)
        {
            TimelineEvent draggedEvent = FindTimelineEvent(draggingEventIndex);
            Rect row = GetTrackContentRect(area, GetTrackIndex(draggedEvent.EventName));
            draggedNormalizedTime = MouseToNormalized(row, current.mousePosition.x);
            ScrubTo(draggedNormalizedTime);
            current.Use();
        }
        else if (draggingEventIndex >= 0 && current.rawType == EventType.MouseUp)
        {
            CommitEventTime(skill, draggingEventIndex, draggedNormalizedTime);
            draggingEventIndex = -1;
            current.Use();
        }
    }

    void DrawPlayhead(Rect contentArea)
    {
        float x = Mathf.Lerp(contentArea.xMin, contentArea.xMax, normalizedTime);
        EditorGUI.DrawRect(new Rect(x - 1f, contentArea.y, 2f, contentArea.height), new Color(1f, 0.25f, 0.2f));
        EditorGUI.DrawRect(new Rect(x - 5f, contentArea.y, 10f, 5f), new Color(1f, 0.25f, 0.2f));
    }

    void HandleTimelineScrub(Rect contentArea, AnimationClip clip)
    {
        Event current = Event.current;
        if (draggingCastPoint || draggingEventIndex >= 0 || current.button != 0)
            return;

        bool isScrubEvent = current.type == EventType.MouseDown || current.type == EventType.MouseDrag;
        if (!isScrubEvent || !contentArea.Contains(current.mousePosition))
            return;

        isPlaying = false;
        float nextTime = MouseToNormalized(contentArea, current.mousePosition.x);
        ScrubTo(nextTime, triggerCrossedVfx: true);
        current.Use();
    }

    void HandleTimelineContextMenu(Rect contentArea, SkillGemDefinition skill)
    {
        Event current = Event.current;
        if (current.type != EventType.ContextClick || !contentArea.Contains(current.mousePosition))
            return;

        float contextTime = MouseToNormalized(contentArea, current.mousePosition.x);
        var menu = new GenericMenu();
        AddEventMenuItem(menu, "VFX/Vfx", skill, CombatTimelineEventName.Vfx, contextTime);
        menu.AddSeparator(string.Empty);
        AddEventMenuItem(menu, "Hitbox/HitStart", skill, CombatTimelineEventName.HitStart, contextTime);
        AddEventMenuItem(menu, "Hitbox/HitEnd", skill, CombatTimelineEventName.HitEnd, contextTime);
        AddEventMenuItem(menu, "Pre-Cast/PreCastOpen", skill, CombatTimelineEventName.PreCastOpen, contextTime);
        AddEventMenuItem(menu, "Pre-Cast/PreCastClose", skill, CombatTimelineEventName.PreCastClose, contextTime);
        AddEventMenuItem(menu, "Other/FootStep", skill, CombatTimelineEventName.FootStep, contextTime);
        AddEventMenuItem(menu, "Other/SpawnEffect", skill, CombatTimelineEventName.SpawnEffect, contextTime);
        AddEventMenuItem(menu, "Other/ShakeCamera", skill, CombatTimelineEventName.ShakeCamera, contextTime);
        menu.ShowAsContext();
        current.Use();
    }

    void AddEventMenuItem(
        GenericMenu menu,
        string path,
        SkillGemDefinition skill,
        CombatTimelineEventName eventName,
        float contextTime)
    {
        if (skill == null)
        {
            menu.AddDisabledItem(new GUIContent(path));
            return;
        }

        menu.AddItem(new GUIContent(path), false, () =>
        {
            normalizedTime = Mathf.Clamp01(contextTime);
            AddEventAtPlayhead(skill, eventName);
            ScrubTo(normalizedTime);
        });
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
        if (authoringTarget != null)
            authoringTarget.StopAllVfx();
        else if (!ReferenceEquals(authoringTarget, null))
            authoringTarget = null;

        previewSession.Stop();

        if (rewind)
            normalizedTime = 0f;

        SceneView.RepaintAll();
        Repaint();
        UpdateEditorUpdateSubscription();
    }

    void ScrubTo(float nextNormalizedTime, bool triggerCrossedVfx = false)
    {
        float previousTime = normalizedTime;
        normalizedTime = Mathf.Clamp01(nextNormalizedTime);
        if (triggerCrossedVfx)
            TriggerVfxBetweenScrubPositions(previousTime, normalizedTime);

        SyncLoopPreviewsAt(normalizedTime);

        AnimationClip clip = GetClip(GetSkill());
        Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
        if (clip == null || animator == null || Application.isPlaying)
            return;

        previewSession.Configure(animator, clip);
        previewSession.Sample(normalizedTime);
        SceneView.RepaintAll();
        Repaint();
    }

    void TriggerVfxBetweenScrubPositions(float previousTime, float nextTime)
    {
        SkillGemDefinition skill = GetSkill();
        if (authoringTarget == null || skill == null || Mathf.Approximately(previousTime, nextTime))
            return;

        BuildTimelineEvents(skill);
        bool movingForward = nextTime > previousTime;
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (!IsVfxEvent(timelineEvent.EventName))
                continue;

            bool crossed = movingForward
                ? timelineEvent.NormalizedTime > previousTime && timelineEvent.NormalizedTime <= nextTime
                : timelineEvent.NormalizedTime < previousTime && timelineEvent.NormalizedTime >= nextTime;
            if (crossed)
                authoringTarget.PlayOneShotVfx(timelineEvent.VfxCueIndex);
        }
    }

    void OnEditorUpdate()
    {
        ClearDestroyedAuthoringTarget();
        if (!isPlaying && !SkillVfxAuthoringEntry.HasActivePreviews)
        {
            UpdateEditorUpdateSubscription();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextPreviewUpdateTime)
            return;

        nextPreviewUpdateTime = now + PreviewUpdateInterval;

        bool repaintViews = false;
        if (isPlaying)
        {
            SkillGemDefinition skill = GetSkill();
            AnimationClip clip = GetClip(skill);
            Animator animator = authoringTarget != null ? authoringTarget.PreviewAnimator : null;
            if (Application.isPlaying || clip == null || animator == null || clip.length <= 0f)
            {
                StopPreview(false);
                return;
            }

            float deltaNormalized = (float)((now - lastEditorTime) * playbackSpeed / clip.length);
            lastEditorTime = now;

            float previousTime = normalizedTime;
            float nextTime = previousTime + deltaNormalized;
            if (nextTime >= 1f)
            {
                TriggerVfxBetween(previousTime, 1f);
                if (loopPlayback)
                {
                    nextTime %= 1f;
                    if (authoringTarget != null)
                        authoringTarget.StopAllLoopPreviews(false);

                    TriggerVfxAt(0f);
                    TriggerVfxBetween(0f, nextTime);
                }
                else
                {
                    normalizedTime = 1f;
                    previewSession.Configure(animator, clip);
                    previewSession.Sample(normalizedTime);
                    isPlaying = false;
                    if (authoringTarget != null)
                        authoringTarget.StopAllLoopPreviews(false);

                    repaintViews = true;
                }
            }
            else
            {
                TriggerVfxBetween(previousTime, nextTime);
            }

            if (isPlaying)
            {
                normalizedTime = Mathf.Clamp01(nextTime);
                previewSession.Configure(animator, clip);
                previewSession.Sample(normalizedTime);
                repaintViews = true;
            }
        }

        if (SkillVfxAuthoringEntry.UpdateActivePreviews(now))
            repaintViews = true;

        if (repaintViews)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            Repaint();
        }

        UpdateEditorUpdateSubscription();
    }

    void OnPreviewActivityChanged()
    {
        UpdateEditorUpdateSubscription();
        Repaint();
    }

    void UpdateEditorUpdateSubscription()
    {
        bool needsUpdates = isPlaying || SkillVfxAuthoringEntry.HasActivePreviews;
        if (needsUpdates == editorUpdateSubscribed)
            return;

        if (needsUpdates)
        {
            nextPreviewUpdateTime = 0d;
            EditorApplication.update += OnEditorUpdate;
        }
        else
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        editorUpdateSubscribed = needsUpdates;
    }

    void StopEditorUpdates()
    {
        if (!editorUpdateSubscribed)
            return;

        EditorApplication.update -= OnEditorUpdate;
        editorUpdateSubscribed = false;
        nextPreviewUpdateTime = 0d;
    }

    void TriggerVfxBetween(float startExclusive, float endInclusive)
    {
        SkillGemDefinition skill = GetSkill();
        if (authoringTarget == null || skill == null)
            return;

        BuildTimelineEvents(skill);
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (!IsVfxEvent(timelineEvent.EventName) ||
                timelineEvent.NormalizedTime <= startExclusive ||
                timelineEvent.NormalizedTime > endInclusive)
            {
                continue;
            }

            authoringTarget.PlayVfx(timelineEvent.VfxCueIndex);
        }
    }

    void TriggerVfxAt(float eventTime)
    {
        SkillGemDefinition skill = GetSkill();
        if (authoringTarget == null || skill == null)
            return;

        BuildTimelineEvents(skill);
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (IsVfxEvent(timelineEvent.EventName) &&
                Mathf.Approximately(timelineEvent.NormalizedTime, eventTime))
            {
                authoringTarget.PlayVfx(timelineEvent.VfxCueIndex);
            }
        }
    }

    void TriggerOneShotVfxAt(float eventTime)
    {
        SkillGemDefinition skill = GetSkill();
        if (authoringTarget == null || skill == null)
            return;

        BuildTimelineEvents(skill);
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (IsVfxEvent(timelineEvent.EventName) &&
                Mathf.Approximately(timelineEvent.NormalizedTime, eventTime))
            {
                authoringTarget.PlayOneShotVfx(timelineEvent.VfxCueIndex);
            }
        }
    }

    void SyncLoopPreviewsAt(float playheadTime)
    {
        SkillGemDefinition skill = GetSkill();
        if (authoringTarget == null || skill == null)
            return;

        BuildTimelineEvents(skill);
        int appliedCueCount = 0;
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            TimelineEvent timelineEvent = timelineEvents[i];
            if (IsVfxEvent(timelineEvent.EventName) && timelineEvent.NormalizedTime <= playheadTime)
                appliedCueCount++;
        }

        authoringTarget.SyncLoopPreviews(appliedCueCount);
    }

    void SelectAndPreviewEntry(TimelineEvent timelineEvent)
    {
        if (authoringTarget == null || !IsVfxEvent(timelineEvent.EventName))
            return;

        SkillVfxAuthoringSlot slot = authoringTarget.FindSlot(timelineEvent.VfxCueIndex);
        if (slot != null)
        {
            Selection.activeGameObject = slot.gameObject;
        }
        else
        {
            SkillVfxAuthoringEntry entry = authoringTarget.FindEntry(timelineEvent.VfxCueIndex);
            if (entry != null)
                Selection.activeGameObject = entry.gameObject;
        }

        authoringTarget.PlayVfx(timelineEvent.VfxCueIndex);
    }

    void CommitEventTime(SkillGemDefinition skill, int serializedIndex, float newNormalizedTime)
    {
        ClipTransition transition = skill != null ? skill.skillClip : null;
        AnimancerEvent.Sequence.Serializable serializedEvents = transition?.SerializedEvents;
        if (serializedEvents == null)
            return;

        float[] times = serializedEvents.NormalizedTimes;
        if (times == null || serializedIndex < 0 || serializedIndex >= times.Length - 1)
            return;

        IInvokable[] callbacks = serializedEvents.Callbacks;
        StringAsset[] names = serializedEvents.Names;
        IInvokable callback = callbacks != null && serializedIndex < callbacks.Length
            ? callbacks[serializedIndex]
            : null;
        StringAsset eventName = names != null && serializedIndex < names.Length
            ? names[serializedIndex]
            : null;

        TimelineEvent movedEvent = FindTimelineEvent(serializedIndex);
        int oldVfxCueIndex = movedEvent.VfxCueIndex;

        Undo.RecordObject(skill, "Move Skill Timeline Event");
        serializedEvents.RemoveEvent(serializedIndex);
        int newSerializedIndex = serializedEvents.AddEvent(Mathf.Clamp(newNormalizedTime, 0f, 0.999f), callback, eventName);
        serializedEvents.Events = null;
        transition.SerializedEvents = serializedEvents;
        EditorUtility.SetDirty(skill);

        BuildTimelineEvents(skill);
        TimelineEvent reorderedEvent = FindTimelineEvent(newSerializedIndex);
        if (IsVfxEvent(movedEvent.EventName) &&
            oldVfxCueIndex >= 0 &&
            reorderedEvent.VfxCueIndex >= 0 &&
            oldVfxCueIndex != reorderedEvent.VfxCueIndex)
        {
            if (authoringTarget != null)
                authoringTarget.MoveVfxCue(oldVfxCueIndex, reorderedEvent.VfxCueIndex);
        }

        selectedEventIndex = -1;
        BuildTimelineEvents(skill);
    }

    void AddEventAtPlayhead(SkillGemDefinition skill, CombatTimelineEventName eventName)
    {
        if (skill == null || eventName == CombatTimelineEventName.None)
            return;

        if (IsVfxEvent(eventName) && authoringTarget != null &&
            !authoringTarget.PrepareAuthoringForAssignedSkill())
        {
            return;
        }

        BuildTimelineEvents(skill);
        for (int i = 0; i < timelineEvents.Count; i++)
        {
            if (timelineEvents[i].EventName == eventName && !IsVfxEvent(eventName))
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Timeline Event",
                    $"'{eventName}' already exists on this Skill Clip. Move the existing marker instead.",
                    "OK");
                return;
            }
        }

        StringAsset nameAsset = ResolveOrCreateEventNameAsset(eventName);
        if (nameAsset == null)
            return;

        ClipTransition transition = skill.skillClip;
        if (transition == null)
            return;

        Undo.RecordObject(skill, "Add Skill Timeline Event");
        AnimancerEvent.Sequence.Serializable serializedEvents = transition.SerializedEvents ?? new AnimancerEvent.Sequence.Serializable();
        selectedEventIndex = serializedEvents.AddEvent(Mathf.Clamp(normalizedTime, 0f, 0.999f), null, nameAsset);
        serializedEvents.Events = null;
        transition.SerializedEvents = serializedEvents;
        EditorUtility.SetDirty(skill);
        BuildTimelineEvents(skill);
    }

    void RemoveSelectedEvent(SkillGemDefinition skill)
    {
        ClipTransition transition = skill != null ? skill.skillClip : null;
        AnimancerEvent.Sequence.Serializable serializedEvents = transition?.SerializedEvents;
        if (serializedEvents == null)
            return;

        float[] times = serializedEvents.NormalizedTimes;
        if (times == null || selectedEventIndex < 0 || selectedEventIndex >= times.Length - 1)
            return;

        TimelineEvent selectedEvent = FindTimelineEvent(selectedEventIndex);
        if (!EditorUtility.DisplayDialog(
                "Remove Timeline Event",
                $"Remove '{selectedEvent.DisplayName}' from '{skill.name}'?",
                "Remove",
                "Cancel"))
        {
            return;
        }

        Undo.RecordObject(skill, "Remove Skill Timeline Event");
        if (IsVfxEvent(selectedEvent.EventName) && authoringTarget != null)
            authoringTarget.RemoveVfxCue(selectedEvent.VfxCueIndex);

        serializedEvents.RemoveEvent(selectedEventIndex);
        serializedEvents.Events = null;
        transition.SerializedEvents = serializedEvents;
        EditorUtility.SetDirty(skill);
        selectedEventIndex = -1;
        BuildTimelineEvents(skill);
    }

    static StringAsset ResolveOrCreateEventNameAsset(CombatTimelineEventName eventName)
    {
        StringReference animancerName = CombatTimelineEventNames.ToStringReference(eventName);
        StringAsset asset = StringAsset.Find(animancerName, out _);
        if (asset != null)
            return asset;

        EnsureAssetFolder(EventAssetFolder);
        string assetName = CombatTimelineEventNames.ToAnimancerEventName(eventName);
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        asset = ScriptableObject.CreateInstance<StringAsset>();
        asset.name = assetName;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{EventAssetFolder}/{assetName}.asset");
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created Animancer event name asset '{assetName}' at '{assetPath}'.", asset);
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

    void BuildTimelineEvents(SkillGemDefinition skill)
    {
        timelineEvents.Clear();

        ClipTransition transition = skill != null ? skill.skillClip : null;
        AnimancerEvent.Sequence.Serializable serializedEvents = transition?.SerializedEvents;
        if (serializedEvents == null)
            return;

        float[] times = serializedEvents.NormalizedTimes;
        StringAsset[] names = serializedEvents.Names;
        if (times == null || times.Length <= 1)
            return;

        AnimancerEvent.Sequence runtimeEvents = transition.Events;
        int eventCount = times.Length - 1;
        int vfxCueIndex = 0;
        for (int i = 0; i < eventCount; i++)
        {
            float time = times[i];
            if (!float.IsFinite(time))
                continue;

            string displayName = names != null && i < names.Length && names[i] != null
                ? names[i].name
                : runtimeEvents.GetName(i)?.String;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Event " + (i + 1);

            Enum.TryParse(displayName, true, out CombatTimelineEventName eventName);
            int currentVfxCueIndex = IsVfxEvent(eventName) ? vfxCueIndex++ : -1;
            if (currentVfxCueIndex >= 0)
                displayName = BuildVfxMarkerLabel(skill, currentVfxCueIndex);

            timelineEvents.Add(new TimelineEvent(i, Mathf.Clamp01(time), displayName, eventName, currentVfxCueIndex));
        }
    }

    static string BuildVfxMarkerLabel(SkillGemDefinition skill, int cueIndex)
    {
        if (skill == null)
            return $"Vfx {cueIndex + 1}";

        IReadOnlyList<SkillVfxEvent> cues = skill.SkillVfxEvents;
        int actionCount = 0;
        string firstAction = null;
        for (int i = 0; i < cues.Count; i++)
        {
            SkillVfxEvent cue = cues[i];
            if (cue == null || cue.cueIndex != cueIndex)
                continue;

            actionCount++;
            if (firstAction == null)
            {
                firstAction = cue.prefab != null
                    ? cue.prefab.name
                    : cue.action == SkillVfxAction.StopLoop
                        ? $"Stop {cue.loopKey}"
                        : cue.action.ToString();
            }
        }

        if (actionCount == 0)
            return $"Vfx {cueIndex + 1} (Empty)";
        if (actionCount == 1)
            return firstAction;

        return $"{firstAction} +{actionCount - 1}";
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

    SkillGemDefinition GetSkill()
    {
        return authoringTarget != null ? authoringTarget.Skill : null;
    }

    static AnimationClip GetClip(SkillGemDefinition skill)
    {
        ClipTransition transition = skill != null ? skill.skillClip : null;
        return transition != null && transition.IsValid ? transition.Clip : null;
    }

    void SetAuthoringTarget(SetSkillVfxData nextTarget)
    {
        if (ReferenceEquals(authoringTarget, nextTarget) ||
            (authoringTarget != null && authoringTarget == nextTarget))
            return;

        StopPreview(true);
        authoringTarget = nextTarget;
        draggingEventIndex = -1;
        selectedEventIndex = -1;
        draggingCastPoint = false;
        BuildTimelineEvents(GetSkill());
        Repaint();
    }

    void ClearDestroyedAuthoringTarget()
    {
        if (ReferenceEquals(authoringTarget, null) || authoringTarget != null)
            return;

        authoringTarget = null;
        isPlaying = false;
        previewSession.Stop();
        draggingEventIndex = -1;
        selectedEventIndex = -1;
        draggingCastPoint = false;
        timelineEvents.Clear();
        RepaintViews();
    }

    void TryUseCurrentSelection(bool clearWhenMissing = false)
    {
        GameObject selectedObject = Selection.activeGameObject;
        SetSkillVfxData selectedTarget = null;
        if (selectedObject != null)
        {
            selectedTarget = selectedObject.GetComponentInParent<SetSkillVfxData>();
            if (selectedTarget == null)
                selectedTarget = selectedObject.GetComponentInChildren<SetSkillVfxData>(true);
        }

        if (selectedTarget != null || clearWhenMissing)
            SetAuthoringTarget(selectedTarget);
    }

    void OnSelectionChanged()
    {
        TryUseCurrentSelection();
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            StopPreview(true);
    }

    void RepaintViews()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    static Rect GetTrackRect(Rect area, int index)
    {
        return new Rect(area.x, area.y + RulerHeight + TrackHeight * index, area.width, TrackHeight);
    }

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
        GUI.Label(new Rect(marker.xMax + 3f, marker.y + 7f, 90f, 18f), label, EditorStyles.miniLabel);
    }

    static float MouseToNormalized(Rect row, float mouseX)
    {
        return Mathf.InverseLerp(row.xMin, row.xMax, mouseX);
    }

    static int GetTrackIndex(CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.HitStart || eventName == CombatTimelineEventName.HitEnd)
            return 2;

        return IsVfxEvent(eventName) ? 3 : 4;
    }

    static bool IsVfxEvent(CombatTimelineEventName eventName)
    {
        return eventName == CombatTimelineEventName.Vfx;
    }

    static Color GetEventColor(CombatTimelineEventName eventName)
    {
        if (eventName == CombatTimelineEventName.HitStart)
            return new Color(0.3f, 0.9f, 0.35f);
        if (eventName == CombatTimelineEventName.HitEnd)
            return new Color(0.95f, 0.35f, 0.3f);
        if (IsVfxEvent(eventName))
            return new Color(0.65f, 0.35f, 1f);

        return new Color(0.35f, 0.75f, 0.95f);
    }

    readonly struct TimelineEvent
    {
        public readonly int SerializedIndex;
        public readonly float NormalizedTime;
        public readonly string DisplayName;
        public readonly CombatTimelineEventName EventName;
        public readonly int VfxCueIndex;

        public TimelineEvent(
            int serializedIndex,
            float normalizedTime,
            string displayName,
            CombatTimelineEventName eventName,
            int vfxCueIndex)
        {
            SerializedIndex = serializedIndex;
            NormalizedTime = normalizedTime;
            DisplayName = displayName;
            EventName = eventName;
            VfxCueIndex = vfxCueIndex;
        }
    }

    sealed class SkillAnimationPreviewSession
    {
        Animator animator;
        AnimationClip clip;
        bool ownsAnimationMode;

        public void Configure(Animator nextAnimator, AnimationClip nextClip)
        {
            if (animator == nextAnimator && clip == nextClip)
                return;

            Stop();
            animator = nextAnimator;
            clip = nextClip;
        }

        public void Sample(float normalizedTime)
        {
            if (animator == null || clip == null || !animator.gameObject.activeInHierarchy)
                return;

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
            }

            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(
                    animator.gameObject,
                    clip,
                    Mathf.Clamp01(normalizedTime) * clip.length);
            }
            finally
            {
                AnimationMode.EndSampling();
            }
        }

        public void Stop()
        {
            if (ownsAnimationMode && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            ownsAnimationMode = false;
            animator = null;
            clip = null;
        }
    }
}
#endif
