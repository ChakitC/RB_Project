using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[HideMonoScript]
public class SetAnimationVfxData : MonoBehaviour
{
    const string SyncUndoLabel = "Sync Animation VFX Slots";
    const string LoadUndoLabel = "Load Animation VFX Data";

    [BoxGroup("VFX Source/Asset & Entry")]
    [LabelText("Source Asset  (Skill / Combo / Anim Profile / Cutscene)")]
    [OnValueChanged(nameof(OnTimelineSourceAssetChanged))]
    [SerializeField] private ScriptableObject sourceAsset;

    [BoxGroup("VFX Source/Asset & Entry")]
    [ValueDropdown(nameof(GetTimelineEntryOptions))]
    [OnValueChanged(nameof(OnTimelineEntryChanged))]
    [SerializeField] private string selectedEntryId = "main";

    [BoxGroup("VFX Source/Asset & Entry")]
    [ShowInInspector, ReadOnly, LabelText("Selected Entry")]
    private string SelectedTimelineEntryLabel => GetSelectedTimelineEntryLabel();

    [BoxGroup("VFX Source/Asset & Entry")]
    [ShowInInspector, ReadOnly, LabelText("Animation Clip")]
    private AnimationClip SelectedAnimationClip => GetSelectedAnimationClip();

    [BoxGroup("VFX Source/Authoring Roots")]
    [SerializeField] protected Transform characterRoot;

    [BoxGroup("VFX Source/Authoring Roots")]
    [SerializeField] protected Transform sourceVfxRoot;

    [BoxGroup("VFX Source/Authoring Roots")]
    [SerializeField] protected bool includeInactiveObjects = true;

    [FoldoutGroup("VFX Source/Cutscene Roots", false)]
    [SerializeField] protected Transform cutsceneCharacterRoot;

    [FoldoutGroup("VFX Source/Cutscene Roots", false)]
    [SerializeField] protected Transform cutsceneCameraRoot;

    [SerializeField, HideInInspector] private ScriptableObject authoringSourceAsset;
    [SerializeField, HideInInspector] private string authoringEntryId;
    [SerializeField, HideInInspector] private string activeAuthoringEntryId = "main";

    private bool IsAuthoringOutOfSync =>
        !UsesModeContainers() &&
        TimelineSourceAsset != null &&
        (authoringSourceAsset != TimelineSourceAsset ||
         !string.Equals(authoringEntryId, TimelineEntryId, System.StringComparison.Ordinal));

#if UNITY_EDITOR
    [System.NonSerialized]
    readonly Dictionary<string, List<SkillVfxAuthoringEntry>> activePreviewLoops =
        new Dictionary<string, List<SkillVfxAuthoringEntry>>(System.StringComparer.Ordinal);
    [System.NonSerialized]
    readonly Dictionary<SkillVfxAuthoringEntry, TimelinePreviewSample> timelinePreviewSamples =
        new Dictionary<SkillVfxAuthoringEntry, TimelinePreviewSample>();
    [System.NonSerialized]
    readonly Dictionary<string, TimelinePreviewLoopGroup> timelinePreviewLoopGroups =
        new Dictionary<string, TimelinePreviewLoopGroup>(System.StringComparer.Ordinal);
    [System.NonSerialized]
    readonly HashSet<string> timelinePreviewReplacedLoopKeys =
        new HashSet<string>(System.StringComparer.Ordinal);
    [System.NonSerialized] HashSet<SkillVfxAuthoringEntry> timelinePreviewEntries =
        new HashSet<SkillVfxAuthoringEntry>();
    [System.NonSerialized] HashSet<SkillVfxAuthoringEntry> nextTimelinePreviewEntries =
        new HashSet<SkillVfxAuthoringEntry>();
    [System.NonSerialized] bool timelinePreviewModeActive;
    [System.NonSerialized] int timelinePreviewGeneration;
    [System.NonSerialized] bool rebuildingAuthoring;
#endif

    public ScriptableObject TimelineSourceAsset => ResolveTimelineSourceAsset();
    public string TimelineEntryId => ResolveTimelineEntryId();
    public string ActiveAuthoringEntryId =>
        string.IsNullOrEmpty(activeAuthoringEntryId) ? "main" : activeAuthoringEntryId;
    public Transform CharacterRoot => GetAnimationCharacterRoot();
    public Animator PreviewAnimator
    {
        get
        {
            Transform root = GetAnimationCharacterRoot();
            return root != null ? root.GetComponentInChildren<Animator>(true) : null;
        }
    }
    public Animator CutscenePreviewAnimator
    {
        get
        {
            return cutsceneCharacterRoot != null
                ? cutsceneCharacterRoot.GetComponentInChildren<Animator>(true)
                : null;
        }
    }
    public Animator CutsceneCameraAnimator
    {
        get
        {
            return cutsceneCameraRoot != null
                ? cutsceneCameraRoot.GetComponentInChildren<Animator>(true)
                : null;
        }
    }

    protected virtual ScriptableObject ResolveTimelineSourceAsset() => sourceAsset;
    protected virtual string ResolveTimelineEntryId() => selectedEntryId;

    public void SetActiveAuthoringEntry(string entryId)
    {
        activeAuthoringEntryId = NormalizeAuthoringEntryId(entryId);
    }

    protected virtual void AssignTimelineSourceAsset(ScriptableObject asset)
    {
        sourceAsset = asset;
    }

    void OnTimelineSourceAssetChanged()
    {
#if UNITY_EDITOR
        if (rebuildingAuthoring)
            return;

        rebuildingAuthoring = true;
        try
        {
            SelectFirstValidTimelineEntry();
            SetActiveAuthoringEntry(selectedEntryId);
        }
        finally
        {
            rebuildingAuthoring = false;
        }

        RebuildTimelineAuthoring("Change Animation VFX Source");
#endif
    }

    void OnTimelineEntryChanged()
    {
#if UNITY_EDITOR
        SetActiveAuthoringEntry(selectedEntryId);
        if (!rebuildingAuthoring)
            RebuildTimelineAuthoring("Change Animation VFX Entry");
#endif
    }

    void SelectFirstValidTimelineEntry()
    {
#if UNITY_EDITOR
        List<AnimationVfxTimelineEntry> entries = AnimationVfxTimelineSourceFactory.GetEntries(sourceAsset);
        if (entries.Count == 0)
        {
            selectedEntryId = sourceAsset is SkillGemDefinition ? "main" : string.Empty;
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, selectedEntryId, System.StringComparison.Ordinal))
                return;
        }

        selectedEntryId = entries[0].Id;
#endif
    }

    IEnumerable<ValueDropdownItem<string>> GetTimelineEntryOptions()
    {
#if UNITY_EDITOR
        List<AnimationVfxTimelineEntry> entries = AnimationVfxTimelineSourceFactory.GetEntries(sourceAsset);
        for (int i = 0; i < entries.Count; i++)
            yield return new ValueDropdownItem<string>(entries[i].DisplayName, entries[i].Id);
#else
        yield break;
#endif
    }

    string GetSelectedTimelineEntryLabel()
    {
#if UNITY_EDITOR
        List<AnimationVfxTimelineEntry> entries = AnimationVfxTimelineSourceFactory.GetEntries(TimelineSourceAsset);
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Id, TimelineEntryId, System.StringComparison.Ordinal))
                return entries[i].DisplayName;
        }

        if (TimelineSourceAsset is MeleeComboSO)
            return "No valid Melee Step selected. Assign missing Step IDs if required.";
#endif
        return TimelineSourceAsset != null ? TimelineEntryId : "None";
    }

    AnimationClip GetSelectedAnimationClip()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = AnimationVfxTimelineSourceFactory.Create(
            TimelineSourceAsset,
            TimelineEntryId);
        ClipTransition transition = source?.Transition;
        return transition != null && transition.IsValid ? transition.Clip : null;
#else
        return null;
#endif
    }

    public void SetTimelineSource(ScriptableObject asset, string entryId)
    {
#if UNITY_EDITOR
        if (TimelineSourceAsset == asset && string.Equals(TimelineEntryId, entryId, System.StringComparison.Ordinal))
            return;

        Undo.RecordObject(this, "Change Animation VFX Source");
        rebuildingAuthoring = true;
        try
        {
            AssignTimelineSourceAsset(asset);
            sourceAsset = asset;
            selectedEntryId = entryId;
            SelectFirstValidTimelineEntry();
            SetActiveAuthoringEntry(selectedEntryId);
        }
        finally
        {
            rebuildingAuthoring = false;
        }

        RebuildTimelineAuthoring("Change Animation VFX Source");
        EditorUtility.SetDirty(this);
#else
        AssignTimelineSourceAsset(asset);
        sourceAsset = asset;
        selectedEntryId = string.IsNullOrWhiteSpace(entryId) ? "main" : entryId;
        SetActiveAuthoringEntry(selectedEntryId);
#endif
    }

#if UNITY_EDITOR
    bool RebuildTimelineAuthoring(string undoLabel) =>
        RebuildTimelineAuthoring(undoLabel, CreateTimelineSource());

    bool RebuildTimelineAuthoring(string undoLabel, IAnimationVfxTimelineSource source)
    {
        if (rebuildingAuthoring)
            return false;

        string entryId = source != null ? source.EntryId : ActiveAuthoringEntryId;
        Transform root = GetOrCreateModeContainer(entryId, undoLabel);
        if (root == null)
        {
            Debug.LogWarning("Could not resolve Animation VFX Source Root.", this);
            return false;
        }

        Transform character = GetAnimationCharacterRoot();
        string label = string.IsNullOrWhiteSpace(undoLabel) ? SyncUndoLabel : undoLabel;

        StopAllVfx();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(label);
        rebuildingAuthoring = true;
        try
        {
            Undo.RecordObject(this, label);
            RemoveExistingAuthoring(root);

            bool canRebuild = source != null && source.MarkerCount > 0 && character != null;
            if (canRebuild)
                RebuildAuthoring(source, character, root, label);

            authoringSourceAsset = source?.SourceAsset;
            authoringEntryId = source?.EntryId;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(root);
        }
        finally
        {
            rebuildingAuthoring = false;
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (source == null)
        {
            string reason = TimelineSourceAsset == null
                ? "no Source Asset is assigned"
                : $"'{TimelineSourceAsset.name}' is not a supported Animation VFX source";
            Debug.LogWarning($"Cleared Animation VFX authoring because {reason}.", this);
            return false;
        }

        if (source.MarkerCount <= 0)
        {
            Debug.LogWarning(
                $"Cleared Animation VFX authoring because '{source.DisplayName}' has no Vfx marker.",
                source.SourceAsset);
            return false;
        }

        if (character == null)
        {
            Debug.LogWarning("Cleared Animation VFX authoring because Character Root could not be resolved.", this);
            return false;
        }

        return true;
    }
#endif

    public bool PrepareTimelineAuthoring()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null || !TryGetAuthoringRoots(out Transform character, out Transform root))
            return false;

        if (UsesModeContainers())
            return GetOrCreateModeContainer(source.EntryId, "Prepare Animation VFX Authoring") != null;

        if (authoringSourceAsset == source.SourceAsset &&
            string.Equals(authoringEntryId, source.EntryId, System.StringComparison.Ordinal))
        {
            return true;
        }

        SkillVfxAuthoringEntry[] existingEntries = GetSourceEntries();
        SkillVfxAuthoringSlot[] existingSlots = GetSourceSlots();
        if (authoringSourceAsset == null && string.IsNullOrWhiteSpace(authoringEntryId) &&
            (existingEntries.Length > 0 || existingSlots.Length > 0))
        {
            Undo.RecordObject(this, "Adopt Animation VFX Authoring");
            authoringSourceAsset = source.SourceAsset;
            authoringEntryId = source.EntryId;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(root);
            return true;
        }

        return RebuildTimelineAuthoring("Switch Animation VFX Entry");
#else
        return TimelineSourceAsset != null;
#endif
    }

    public bool PrepareTimelineAuthoring(IAnimationVfxTimelineSource source)
    {
#if UNITY_EDITOR
        if (source == null || !TryGetAuthoringRoots(out Transform character, out Transform root))
            return false;

        if (UsesModeContainers())
            return GetOrCreateModeContainer(source.EntryId, "Prepare Animation VFX Authoring") != null;

        if (authoringSourceAsset == source.SourceAsset &&
            string.Equals(authoringEntryId, source.EntryId, System.StringComparison.Ordinal))
            return true;

        SkillVfxAuthoringEntry[] existingEntries = GetSourceEntries(source.EntryId);
        SkillVfxAuthoringSlot[] existingSlots = GetSourceSlots(source.EntryId);
        if (authoringSourceAsset == null && string.IsNullOrWhiteSpace(authoringEntryId) &&
            (existingEntries.Length > 0 || existingSlots.Length > 0))
        {
            Undo.RecordObject(this, "Adopt Animation VFX Authoring");
            authoringSourceAsset = source.SourceAsset;
            authoringEntryId = source.EntryId;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(root);
            return true;
        }

        return RebuildTimelineAuthoring("Switch Animation VFX Entry", source);
#else
        return source != null;
#endif
    }

    public void CreateOrSyncTimelineVfxSlots()
    {
#if UNITY_EDITOR
        RebuildTimelineAuthoring(SyncUndoLabel);
#endif
    }

    [Title("Data Sync")]
    [InfoBox("Authoring hierarchy is out of sync with the selected source and entry. Click 'Load / Sync VFX Data' to rebuild from saved data.", InfoMessageType.Warning, nameof(IsAuthoringOutOfSync))]
    [ButtonGroup("vfxDataSyncRow")]
    [GUIColor(0.55f, 0.9f, 0.55f)]
    [Button("Save VFX Data")]
    [PropertyTooltip("Save all authoring entries under Source VFX Root through the selected source adapter.")]
    public void SaveTimelineVfxData()
    {
#if UNITY_EDITOR
        if (UsesModeContainers())
        {
            SaveAllTimelineVfxData();
            return;
        }

        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null || !PrepareTimelineAuthoring() ||
            !TryBuildSourceData(source, out List<AnimationVfxCue> cues, out List<string> issues))
        {
            return;
        }

        source.ReplaceCues(cues);
        source.Save();
        if (issues.Count > 0)
        {
            Debug.LogWarning(
                $"Saved {cues.Count} Animation VFX entries to '{source.DisplayName}', but found {issues.Count} issue(s):\n- " +
                string.Join("\n- ", issues),
                source.SourceAsset);
        }
#endif
    }

    [ButtonGroup("vfxDataSyncRow")]
    [GUIColor(1f, 0.78f, 0.35f)]
    [Button("Load / Sync VFX Data")]
    [PropertyTooltip("Discard unsaved hierarchy changes and rebuild from the selected Source Asset and Entry.")]
    public void LoadTimelineVfxData()
    {
#if UNITY_EDITOR
        if (UsesModeContainers())
        {
            LoadAllTimelineVfxData();
            return;
        }

        RebuildTimelineAuthoring(LoadUndoLabel);
#endif
    }

    public void SaveTimelineVfxData(IAnimationVfxTimelineSource source)
    {
#if UNITY_EDITOR
        if (source == null || !PrepareTimelineAuthoring(source) ||
            !TryBuildSourceData(source, out List<AnimationVfxCue> cues, out List<string> issues))
        {
            return;
        }

        source.ReplaceCues(cues);
        source.Save();
        if (issues.Count > 0)
        {
            Debug.LogWarning(
                $"Saved {cues.Count} Animation VFX entries to '{source.DisplayName}', but found {issues.Count} issue(s):\n- " +
                string.Join("\n- ", issues),
                source.SourceAsset);
        }
#endif
    }

    public void LoadTimelineVfxData(IAnimationVfxTimelineSource source)
    {
#if UNITY_EDITOR
        RebuildTimelineAuthoring(LoadUndoLabel, source);
#endif
    }

    public void SaveAllTimelineVfxData()
    {
#if UNITY_EDITOR
        foreach (IAnimationVfxTimelineSource source in EnumerateModeSources())
            SaveTimelineVfxData(source);
#endif
    }

    public void LoadAllTimelineVfxData()
    {
#if UNITY_EDITOR
        foreach (IAnimationVfxTimelineSource source in EnumerateModeSources())
            RebuildTimelineAuthoring(LoadUndoLabel, source);
        MigrateLegacyFlatAuthoring();
#endif
    }

    public void MoveTimelineVfxCue(int oldCueIndex, int newCueIndex)
    {
#if UNITY_EDITOR
        if (oldCueIndex < 0 || newCueIndex < 0 || oldCueIndex == newCueIndex || !PrepareTimelineAuthoring())
            return;

        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        Undo.RecordObjects(slots, "Move Animation VFX Cue");
        for (int i = 0; i < slots.Length; i++)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot == null)
                continue;

            int value = slot.CueIndex;
            if (value == oldCueIndex)
                value = newCueIndex;
            else if (oldCueIndex < newCueIndex && value > oldCueIndex && value <= newCueIndex)
                value--;
            else if (newCueIndex < oldCueIndex && value >= newCueIndex && value < oldCueIndex)
                value++;
            slot.SetCueIndex(value);
        }

        IAnimationVfxTimelineSource source = CreateTimelineSource();
        source?.MoveCueIndex(oldCueIndex, newCueIndex);
        source?.Save();
        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    public void RemoveTimelineVfxCue(int cueIndex)
    {
#if UNITY_EDITOR
        if (cueIndex < 0 || !PrepareTimelineAuthoring())
            return;

        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot == null)
                continue;

            if (slot.CueIndex == cueIndex)
                Undo.DestroyObjectImmediate(slot.gameObject);
            else if (slot.CueIndex > cueIndex)
            {
                Undo.RecordObject(slot, "Remove Animation VFX Cue");
                slot.SetCueIndex(slot.CueIndex - 1);
            }
        }

        IAnimationVfxTimelineSource source = CreateTimelineSource();
        source?.RemoveCueIndex(cueIndex);
        source?.Save();
        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    public void AddPrefabsToSlot(SkillVfxAuthoringSlot slot, IReadOnlyList<GameObject> prefabs)
    {
#if UNITY_EDITOR
        if (!PrepareTimelineAuthoring() || slot == null || prefabs == null ||
            !ContainsAuthoringTransform(slot.transform))
        {
            return;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null)
                continue;

            AnimationVfxCue cue = CreateEmptyCue(slot.CueIndex);
            cue.prefab = prefabs[i];
            SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, "Create Animation VFX Entry");
            ApplyStoredPose(GetAnimationCharacterRoot(), entry, cue);
        }

        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    public void AddEmptyEntryToSlot(SkillVfxAuthoringSlot slot)
    {
#if UNITY_EDITOR
        if (!PrepareTimelineAuthoring() || slot == null || !ContainsAuthoringTransform(slot.transform))
            return;

        CreateEntryObject(slot.transform, CreateEmptyCue(slot.CueIndex), "Create Animation VFX Entry");
        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    [Button("Refresh All Visuals")]
    [PropertyOrder(10)]
    public void RefreshAllVisuals()
    {
        StopTimelineVfxPreview();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
            entries[i]?.RefreshVisualPreview();
    }

    [Title("Preview")]
    [ButtonGroup("vfxPreviewRow")]
    [Button("Play All VFX")]
    public void PlayAllVfx()
    {
        StopAllVfx();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        var cueIndices = new SortedSet<int>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].CueIndex >= 0)
                cueIndices.Add(entries[i].CueIndex);
        }

        foreach (int cueIndex in cueIndices)
            PlayVfx(cueIndex);
    }

    [ButtonGroup("vfxPreviewRow")]
    [Button("Stop All VFX")]
    public void StopAllVfx()
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
            entries[i]?.StopVisualPreview();
#if UNITY_EDITOR
        activePreviewLoops.Clear();
        timelinePreviewSamples.Clear();
        timelinePreviewLoopGroups.Clear();
        timelinePreviewReplacedLoopKeys.Clear();
        timelinePreviewEntries.Clear();
        nextTimelinePreviewEntries.Clear();
        timelinePreviewModeActive = false;
#endif
    }

    public void SampleTimelineVfx(
        float playheadTime,
        IReadOnlyList<float> markerTimes,
        bool forceRestart)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || markerTimes == null)
        {
            StopTimelineVfxPreview();
            return;
        }

        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        if (!timelinePreviewModeActive)
        {
            for (int i = 0; i < entries.Length; i++)
                entries[i]?.StopVisualPreview();
            activePreviewLoops.Clear();
            timelinePreviewModeActive = true;
            forceRestart = true;
        }

        timelinePreviewSamples.Clear();
        timelinePreviewGeneration++;
        if (timelinePreviewGeneration <= 0)
        {
            timelinePreviewGeneration = 1;
            foreach (TimelinePreviewLoopGroup group in timelinePreviewLoopGroups.Values)
                group.Generation = 0;
        }

        // Rebuild cue state from marker zero so play and scrub resolve the same loops.
        float clampedPlayheadTime = Mathf.Max(0f, playheadTime);
        for (int cueIndex = 0; cueIndex < markerTimes.Count; cueIndex++)
        {
            float markerTime = Mathf.Max(0f, markerTimes[cueIndex]);
            if (markerTime > clampedPlayheadTime)
                break;

            timelinePreviewReplacedLoopKeys.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                SkillVfxAuthoringEntry entry = entries[i];
                if (entry == null || entry.CueIndex != cueIndex ||
                    entry.AnimationAction != AnimationVfxAction.StartLoop || entry.Prefab == null)
                {
                    continue;
                }

                string key = NormalizeLoopKey(entry.LoopKey);
                if (key == null || !timelinePreviewReplacedLoopKeys.Add(key))
                    continue;

                TimelinePreviewLoopGroup group = GetTimelinePreviewLoopGroup(key);
                group.Generation = timelinePreviewGeneration;
                group.StartTime = markerTime;
                group.Active = true;
                group.Entries.Clear();
            }

            for (int i = 0; i < entries.Length; i++)
            {
                SkillVfxAuthoringEntry entry = entries[i];
                if (entry == null || entry.CueIndex != cueIndex)
                    continue;

                if (entry.AnimationAction == AnimationVfxAction.OneShot)
                {
                    timelinePreviewSamples[entry] = new TimelinePreviewSample(
                        clampedPlayheadTime - markerTime,
                        -1f,
                        false,
                        0f);
                    continue;
                }

                string key = NormalizeLoopKey(entry.LoopKey);
                if (key == null)
                    continue;

                if (entry.AnimationAction == AnimationVfxAction.StartLoop)
                {
                    if (entry.Prefab == null ||
                        !timelinePreviewLoopGroups.TryGetValue(key, out TimelinePreviewLoopGroup group) ||
                        group.Generation != timelinePreviewGeneration || !group.Active)
                    {
                        continue;
                    }

                    group.Entries.Add(entry);
                    continue;
                }

                if (!timelinePreviewLoopGroups.TryGetValue(key, out TimelinePreviewLoopGroup activeGroup) ||
                    activeGroup.Generation != timelinePreviewGeneration || !activeGroup.Active)
                {
                    continue;
                }

                if (entry.AllowParticlesToFinish)
                {
                    float elapsedTime = clampedPlayheadTime - activeGroup.StartTime;
                    float stopTime = markerTime - activeGroup.StartTime;
                    for (int j = 0; j < activeGroup.Entries.Count; j++)
                    {
                        SkillVfxAuthoringEntry loopEntry = activeGroup.Entries[j];
                        if (loopEntry != null)
                        {
                            timelinePreviewSamples[loopEntry] = new TimelinePreviewSample(
                                elapsedTime,
                                stopTime,
                                true,
                                entry.ExtraLife);
                        }
                    }
                }

                activeGroup.Active = false;
            }
        }

        foreach (TimelinePreviewLoopGroup group in timelinePreviewLoopGroups.Values)
        {
            if (group.Generation != timelinePreviewGeneration || !group.Active)
                continue;

            float elapsedTime = clampedPlayheadTime - group.StartTime;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                SkillVfxAuthoringEntry entry = group.Entries[i];
                if (entry != null)
                    timelinePreviewSamples[entry] = new TimelinePreviewSample(elapsedTime, -1f, false, 0f);
            }
        }

        nextTimelinePreviewEntries.Clear();
        foreach (KeyValuePair<SkillVfxAuthoringEntry, TimelinePreviewSample> pair in timelinePreviewSamples)
        {
            SkillVfxAuthoringEntry entry = pair.Key;
            TimelinePreviewSample sample = pair.Value;
            if (entry != null && entry.SampleTimelinePreview(
                    sample.ElapsedTime,
                    sample.StopTime,
                    sample.AllowParticlesToFinish,
                    sample.StopExtraLife,
                    forceRestart))
            {
                nextTimelinePreviewEntries.Add(entry);
            }
        }

        foreach (SkillVfxAuthoringEntry entry in timelinePreviewEntries)
        {
            if (entry != null && !nextTimelinePreviewEntries.Contains(entry))
                entry.StopTimelinePreview();
        }

        HashSet<SkillVfxAuthoringEntry> previousEntries = timelinePreviewEntries;
        timelinePreviewEntries = nextTimelinePreviewEntries;
        nextTimelinePreviewEntries = previousEntries;
#endif
    }

    public void StopTimelineVfxPreview()
    {
#if UNITY_EDITOR
        foreach (SkillVfxAuthoringEntry entry in timelinePreviewEntries)
            entry?.StopTimelinePreview();
        timelinePreviewEntries.Clear();
        nextTimelinePreviewEntries.Clear();
        timelinePreviewSamples.Clear();
        timelinePreviewLoopGroups.Clear();
        timelinePreviewReplacedLoopKeys.Clear();
        timelinePreviewModeActive = false;
#endif
    }

    [ButtonGroup("vfxMaintenanceRow")]
    [PropertyOrder(1)]
    [GUIColor(1f, 0.6f, 0.6f)]
    [Button("Clear Authoring Slots")]
    [PropertyTooltip("Remove only Animation VFX slots and entries under Source VFX Root.")]
    void ClearAuthoringEntries()
    {
#if UNITY_EDITOR
        Transform root = GetSourceRoot();
        if (root == null)
            return;

        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        if (entries.Length == 0 && slots.Length == 0)
            return;

        if (!EditorUtility.DisplayDialog(
                "Clear Animation VFX Authoring Slots",
                $"Remove {slots.Length} slot(s) and {entries.Length} VFX entry(s) under '{root.name}'?",
                "Remove",
                "Cancel"))
        {
            return;
        }

        const string undoLabel = "Clear Animation VFX Authoring Slots";
        StopAllVfx();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoLabel);
        RemoveExistingAuthoring(root);
        MarkHierarchyDirty(root);
        Undo.CollapseUndoOperations(undoGroup);
#endif
    }

    [Title("Maintenance")]
    [ButtonGroup("vfxMaintenanceRow")]
    [Button("Validate Current Data")]
    void ValidateCurrentData()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null)
        {
            Debug.LogWarning("Select a supported Animation VFX Source Asset and Entry before validation.", this);
            return;
        }

        if (!TryBuildSourceData(source, out List<AnimationVfxCue> cues, out List<string> issues))
            return;

        if (issues.Count == 0)
        {
            Debug.Log($"Animation VFX authoring data for '{source.DisplayName}' passed validation ({cues.Count} entries).", this);
            return;
        }

        Debug.LogWarning(
            $"Animation VFX authoring data for '{source.DisplayName}' has {issues.Count} issue(s):\n- " +
            string.Join("\n- ", issues),
            this);
#endif
    }

    public void PlayVfx(int cueIndex)
    {
        StopTimelineVfxPreview();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
#if UNITY_EDITOR
        var replacedKeys = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.CueIndex != cueIndex ||
                entry.AnimationAction != AnimationVfxAction.StartLoop || entry.Prefab == null)
                continue;
            string key = NormalizeLoopKey(entry.LoopKey);
            if (key != null && replacedKeys.Add(key))
                StopPreviewLoopGroup(key, false, 0f);
        }
#endif
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null && entry.CueIndex == cueIndex)
                PlayPreviewEntry(entry);
        }
    }

    public void StopVfx(int cueIndex)
    {
        StopTimelineVfxPreview();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].CueIndex == cueIndex)
                entries[i].StopVisualPreview();
        }
    }

    public SkillVfxAuthoringEntry FindEntry(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].CueIndex == cueIndex)
                return entries[i];
        }
        return null;
    }

    public SkillVfxAuthoringSlot FindSlot(int cueIndex)
    {
        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].CueIndex == cueIndex)
                return slots[i];
        }
        return null;
    }

    public bool ContainsAuthoringTransform(Transform candidate)
    {
        Transform root = GetSourceRoot();
        return root != null && candidate != null && (candidate == root || candidate.IsChildOf(root));
    }

#if UNITY_EDITOR
    public bool TryGetSelectedGenericBonePath(out string path, out string error)
    {
        path = null;
        error = null;

        Animator animator = PreviewAnimator;
        if (animator == null)
        {
            error = "The Animation VFX authoring target has no Animator below Character Root.";
            return false;
        }

        Transform selected = Selection.activeTransform;
        if (selected == null)
        {
            error = "Select a bone Transform below the preview Animator first.";
            return false;
        }

        if (selected == animator.transform || !selected.IsChildOf(animator.transform))
        {
            error = $"'{selected.name}' must be below Animator '{animator.name}' and cannot be the Animator root.";
            return false;
        }

        path = AnimationUtility.CalculateTransformPath(selected, animator.transform);
        if (!string.IsNullOrWhiteSpace(path))
            return true;

        error = $"Could not calculate a Generic bone path for '{selected.name}'.";
        return false;
    }

    public Transform ResolveEntryPreviewAnchor(IAnimationVfxCue cue)
    {
        Transform character = GetAnimationCharacterRoot();
        AnimationVfxAnchorContext context = BuildAnchorContext(character);
        return AnimationVfxAnchorResolver.Resolve(context, cue);
    }

    IAnimationVfxTimelineSource CreateTimelineSource()
    {
        return AnimationVfxTimelineSourceFactory.Create(TimelineSourceAsset, TimelineEntryId);
    }

    bool TryBuildSourceData(
        IAnimationVfxTimelineSource source,
        out List<AnimationVfxCue> cues,
        out List<string> issues)
    {
        cues = new List<AnimationVfxCue>();
        issues = new List<string>();
        if (!TryGetAuthoringRoots(out Transform character, out Transform root))
            return false;

        root = ResolveScanRoot(source.EntryId);
        SkillVfxAuthoringEntry[] entries = GetSourceEntries(source.EntryId);
        SkillVfxAuthoringSlot[] slots = GetSourceSlots(source.EntryId);
        if (entries.Length == 0 && slots.Length == 0)
        {
            string rootName = root != null ? root.name : GetSourceRoot()?.name ?? name;
            Debug.LogWarning($"No Animation VFX authoring slot was found under '{rootName}'.", this);
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null)
                continue;

            AnimationVfxCue cue = entry.CreateAnimationData();
            Transform placement = entry.GetPlacementTransform();
            AnimationVfxAnchorContext context = BuildAnchorContext(character);
            Transform anchor = AnimationVfxAnchorResolver.Resolve(context, cue);
            if (anchor == null)
                issues.Add($"Entry '{entry.name}' could not resolve its anchor.");
            else if (cue.action != AnimationVfxAction.StopLoop)
            {
                cue.localPosition = AnimationVfxAnchorResolver.ResolveLocalPosition(anchor, cue, placement.position);
                cue.localEulerAngles = (Quaternion.Inverse(anchor.rotation) * placement.rotation).eulerAngles;
                cue.localScale = CalculateScaleMultiplier(placement, cue.prefab);
            }
            AnimationVfxValidation.CollectAnchorResolutionIssues(
                context,
                cue,
                $"Entry '{entry.name}'",
                issues);
            cues.Add(cue);
        }

        AnimationVfxValidation.CollectIssues(new ListCueSource(cues), source.MarkerCount, source.DisplayName, issues);
        source.CollectValidationIssues(issues);
        return true;
    }

    static void RebuildAuthoring(
        IAnimationVfxTimelineSource source,
        Transform character,
        Transform root,
        string undoLabel)
    {
        var slots = new Dictionary<int, SkillVfxAuthoringSlot>();
        for (int cueIndex = 0; cueIndex < source.MarkerCount; cueIndex++)
            slots.Add(cueIndex, CreateSlotObject(root, cueIndex, undoLabel));

        for (int i = 0; i < source.CueCount; i++)
        {
            IAnimationVfxCue cue = source.GetCue(i);
            if (cue == null)
                continue;
            if (!slots.TryGetValue(cue.CueIndex, out SkillVfxAuthoringSlot slot))
            {
                slot = CreateSlotObject(root, cue.CueIndex, undoLabel);
                slots.Add(cue.CueIndex, slot);
            }
            SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, undoLabel);
            ApplyStoredPose(character, entry, cue);
        }
    }

    static SkillVfxAuthoringEntry CreateEntryObject(Transform root, IAnimationVfxCue cue, string undoLabel)
    {
        GameObject entryObject = new GameObject($"Vfx_{cue.CueIndex + 1}_{cue.Action}");
        entryObject.transform.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(entryObject, undoLabel);
        SkillVfxAuthoringEntry entry = Undo.AddComponent<SkillVfxAuthoringEntry>(entryObject);
        entry.Configure(cue);
        entry.CreateAuthoredPrefabInstance(undoLabel);
        return entry;
    }

    static SkillVfxAuthoringSlot CreateSlotObject(Transform root, int cueIndex, string undoLabel)
    {
        GameObject slotObject = new GameObject($"Vfx_Slot_{cueIndex + 1}");
        slotObject.transform.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(slotObject, undoLabel);
        SkillVfxAuthoringSlot slot = Undo.AddComponent<SkillVfxAuthoringSlot>(slotObject);
        slot.Configure(cueIndex);
        return slot;
    }

    static void ApplyStoredPose(Transform character, SkillVfxAuthoringEntry entry, IAnimationVfxCue cue)
    {
        if (entry == null || cue == null)
            return;
        Transform placement = entry.GetPlacementTransform();
        Transform anchor = AnimationVfxAnchorResolver.Resolve(BuildAnchorContext(character), cue);
        if (anchor != null && cue.Action != AnimationVfxAction.StopLoop)
        {
            AnimationVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
            placement.SetPositionAndRotation(position, rotation);
            placement.localScale = cue.Prefab != null
                ? Vector3.Scale(cue.Prefab.transform.localScale, cue.LocalScale)
                : cue.LocalScale;
        }
    }

    static AnimationVfxAnchorContext BuildAnchorContext(Transform character)
    {
        Transform root = ResolveCharacterRoot(character);
        CharacteContext context = root != null ? root.GetComponent<CharacteContext>() : null;
        SkillUserSystem skillUser = context != null ? context.EnegySystem : null;
        Animator animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        return new AnimationVfxAnchorContext(
            root,
            skillUser != null ? skillUser.CastOrigin : root,
            skillUser != null ? skillUser.AimTransform : root,
            animator);
    }

    static AnimationVfxCue CreateEmptyCue(int cueIndex)
    {
        return new AnimationVfxCue
        {
            cueIndex = cueIndex,
            action = AnimationVfxAction.OneShot,
            anchor = AnimationVfxAnchor.CastOrigin,
            localScale = Vector3.one,
            allowParticlesToFinish = true,
        };
    }

    bool TryGetAuthoringRoots(out Transform character, out Transform root)
    {
        character = GetAnimationCharacterRoot();
        root = GetSourceRoot();
        if (character != null && root != null)
            return true;
        Debug.LogWarning("Could not resolve Animation VFX character/source root.", this);
        return false;
    }

    static void RemoveExistingAuthoring(Transform root)
    {
        SkillVfxAuthoringSlot[] slots = root.GetComponentsInChildren<SkillVfxAuthoringSlot>(true);
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            if (slots[i] != null && slots[i].transform != root)
                Undo.DestroyObjectImmediate(slots[i].gameObject);
        }
        SkillVfxAuthoringEntry[] entries = root.GetComponentsInChildren<SkillVfxAuthoringEntry>(true);
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i] != null && entries[i].transform != root &&
                entries[i].GetComponentInParent<SkillVfxAuthoringSlot>() == null)
                Undo.DestroyObjectImmediate(entries[i].gameObject);
        }
    }

    static void MarkHierarchyDirty(Transform root)
    {
        if (root == null)
            return;
        EditorUtility.SetDirty(root.gameObject);
        if (root.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    void PlayPreviewEntry(SkillVfxAuthoringEntry entry)
    {
        if (entry == null)
            return;
        if (entry.AnimationAction == AnimationVfxAction.OneShot)
        {
            entry.PlayVisualPreview();
            return;
        }
        string key = NormalizeLoopKey(entry.LoopKey);
        if (key == null)
            return;
        if (entry.AnimationAction == AnimationVfxAction.StartLoop)
        {
            entry.PlayVisualPreview();
            if (!activePreviewLoops.TryGetValue(key, out List<SkillVfxAuthoringEntry> group))
            {
                group = new List<SkillVfxAuthoringEntry>();
                activePreviewLoops[key] = group;
            }
            group.Add(entry);
        }
        else
            StopPreviewLoopGroup(key, entry.AllowParticlesToFinish, entry.ExtraLife);
    }

    void StopPreviewLoopGroup(string key, bool allowParticlesToFinish, float extraLife)
    {
        if (!activePreviewLoops.TryGetValue(key, out List<SkillVfxAuthoringEntry> entries))
            return;
        activePreviewLoops.Remove(key);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null)
                continue;
            if (allowParticlesToFinish)
                entries[i].StopLoopVisualPreview(true, extraLife);
            else
                entries[i].StopVisualPreview();
        }
    }

#else
    void PlayPreviewEntry(SkillVfxAuthoringEntry entry)
    {
        entry?.PlayVisualPreview();
    }
#endif

    protected Transform GetAnimationCharacterRoot()
    {
        return characterRoot != null ? characterRoot : ResolveCharacterRoot(transform);
    }

    protected Transform GetSourceRoot()
    {
        return sourceVfxRoot != null ? sourceVfxRoot : transform;
    }

    SkillVfxAuthoringEntry[] GetSourceEntries()
    {
        return GetSourceEntries(ActiveAuthoringEntryId);
    }

    SkillVfxAuthoringEntry[] GetSourceEntries(string entryId)
    {
        Transform root = ResolveScanRoot(entryId);
        return root != null
            ? root.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringEntry>();
    }

    SkillVfxAuthoringSlot[] GetSourceSlots()
    {
        return GetSourceSlots(ActiveAuthoringEntryId);
    }

    SkillVfxAuthoringSlot[] GetSourceSlots(string entryId)
    {
        Transform root = ResolveScanRoot(entryId);
        return root != null
            ? root.GetComponentsInChildren<SkillVfxAuthoringSlot>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringSlot>();
    }

    bool UsesModeContainers()
    {
        return TimelineSourceAsset is SkillGemDefinition skill && skill.IsCutsceneSkill;
    }

    Transform ResolveScanRoot(string entryId)
    {
        Transform root = GetSourceRoot();
        if (root == null || !UsesModeContainers())
            return root;

        string wanted = NormalizeAuthoringEntryId(entryId);
        SkillVfxAuthoringGroup[] groups = root.GetComponentsInChildren<SkillVfxAuthoringGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            SkillVfxAuthoringGroup group = groups[i];
            if (group != null && string.Equals(group.EntryId, wanted, System.StringComparison.Ordinal))
                return group.transform;
        }

        return null;
    }

#if UNITY_EDITOR
    Transform GetOrCreateModeContainer(string entryId, string undoLabel)
    {
        Transform root = GetSourceRoot();
        if (root == null || !UsesModeContainers())
            return root;

        string normalized = NormalizeAuthoringEntryId(entryId);
        Transform existing = ResolveScanRoot(normalized);
        if (existing != null)
            return existing;

        string label = string.IsNullOrWhiteSpace(undoLabel) ? SyncUndoLabel : undoLabel;
        GameObject container = new GameObject("VFX_Container");
        container.transform.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(container, label);
        SkillVfxAuthoringGroup group = Undo.AddComponent<SkillVfxAuthoringGroup>(container);
        group.Configure(normalized);
        return container.transform;
    }

    IEnumerable<IAnimationVfxTimelineSource> EnumerateModeSources()
    {
        ScriptableObject asset = TimelineSourceAsset;
        if (asset is SkillGemDefinition skill && skill.IsCutsceneSkill)
        {
            yield return AnimationVfxTimelineSourceFactory.Create(skill, "main");
            yield return AnimationVfxTimelineSourceFactory.Create(skill, "cutscene");
            yield break;
        }

        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source != null)
            yield return source;
    }

    void MigrateLegacyFlatAuthoring()
    {
        Transform root = GetSourceRoot();
        if (root == null || !UsesModeContainers())
            return;

        SkillVfxAuthoringSlot[] slots = root.GetComponentsInChildren<SkillVfxAuthoringSlot>(true);
        SkillVfxAuthoringEntry[] entries = root.GetComponentsInChildren<SkillVfxAuthoringEntry>(true);
        bool hasLegacy = false;
        for (int i = 0; i < slots.Length && !hasLegacy; i++)
            hasLegacy = slots[i] != null && !IsInsideAuthoringGroup(slots[i].transform, root);
        for (int i = 0; i < entries.Length && !hasLegacy; i++)
            hasLegacy = entries[i] != null && !IsInsideAuthoringGroup(entries[i].transform, root);
        if (!hasLegacy)
            return;

        const string undoLabel = "Migrate Legacy Animation VFX Authoring";
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoLabel);
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot != null && !IsInsideAuthoringGroup(slot.transform, root))
                Undo.DestroyObjectImmediate(slot.gameObject);
        }

        for (int i = entries.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null &&
                !IsInsideAuthoringGroup(entry.transform, root) &&
                entry.GetComponentInParent<SkillVfxAuthoringSlot>() == null)
            {
                Undo.DestroyObjectImmediate(entry.gameObject);
            }
        }

        MarkHierarchyDirty(root);
        Undo.CollapseUndoOperations(undoGroup);
    }

    static bool IsInsideAuthoringGroup(Transform candidate, Transform root)
    {
        for (Transform current = candidate; current != null && current != root; current = current.parent)
        {
            if (current.GetComponent<SkillVfxAuthoringGroup>() != null)
                return true;
        }

        return false;
    }
#endif

    static string NormalizeAuthoringEntryId(string entryId)
    {
        return string.IsNullOrEmpty(entryId) ? "main" : entryId;
    }

    static Transform ResolveCharacterRoot(Transform source)
    {
        if (source == null)
            return null;
        CharacteContext context = source.GetComponentInParent<CharacteContext>();
        if (context == null)
            context = source.GetComponentInChildren<CharacteContext>(true);
        return context != null ? context.transform : source;
    }

    static string NormalizeLoopKey(string loopKey)
    {
        return string.IsNullOrWhiteSpace(loopKey) ? null : loopKey.Trim();
    }

#if UNITY_EDITOR
    TimelinePreviewLoopGroup GetTimelinePreviewLoopGroup(string key)
    {
        if (!timelinePreviewLoopGroups.TryGetValue(key, out TimelinePreviewLoopGroup group))
        {
            group = new TimelinePreviewLoopGroup();
            timelinePreviewLoopGroups.Add(key, group);
        }

        return group;
    }

    readonly struct TimelinePreviewSample
    {
        public readonly float ElapsedTime;
        public readonly float StopTime;
        public readonly bool AllowParticlesToFinish;
        public readonly float StopExtraLife;

        public TimelinePreviewSample(
            float elapsedTime,
            float stopTime,
            bool allowParticlesToFinish,
            float stopExtraLife)
        {
            ElapsedTime = elapsedTime;
            StopTime = stopTime;
            AllowParticlesToFinish = allowParticlesToFinish;
            StopExtraLife = stopExtraLife;
        }
    }

    sealed class TimelinePreviewLoopGroup
    {
        public readonly List<SkillVfxAuthoringEntry> Entries = new List<SkillVfxAuthoringEntry>();
        public int Generation;
        public float StartTime;
        public bool Active;
    }
#endif

    static Vector3 CalculateScaleMultiplier(Transform placement, GameObject prefabAsset)
    {
        if (placement == null || prefabAsset == null)
            return Vector3.one;
        Vector3 authored = placement.lossyScale;
        Vector3 prefab = prefabAsset.transform.lossyScale;
        return new Vector3(SafeDivide(authored.x, prefab.x), SafeDivide(authored.y, prefab.y), SafeDivide(authored.z, prefab.z));
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? 1f : value / divisor;
    }

    sealed class ListCueSource : IAnimationVfxCueSource
    {
        readonly IReadOnlyList<AnimationVfxCue> cues;
        public ListCueSource(IReadOnlyList<AnimationVfxCue> cues) => this.cues = cues;
        public int CueCount => cues?.Count ?? 0;
        public IAnimationVfxCue GetCue(int index) => index >= 0 && index < CueCount ? cues[index] : null;
    }
}
