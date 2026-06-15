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

    [Title("Animation VFX Source")]
    [OnValueChanged(nameof(OnTimelineSourceAssetChanged))]
    [SerializeField] private ScriptableObject sourceAsset;
    [ValueDropdown(nameof(GetTimelineEntryOptions))]
    [OnValueChanged(nameof(OnTimelineEntryChanged))]
    [SerializeField] private string selectedEntryId = "main";
    [SerializeField] protected Transform characterRoot;
    [SerializeField] protected Transform sourceVfxRoot;
    [SerializeField] protected bool includeInactiveObjects = true;

    [ShowInInspector, ReadOnly, LabelText("Selected Entry")]
    private string SelectedTimelineEntryLabel => GetSelectedTimelineEntryLabel();

    [ShowInInspector, ReadOnly, LabelText("Animation Clip")]
    private AnimationClip SelectedAnimationClip => GetSelectedAnimationClip();

    [SerializeField, HideInInspector] private ScriptableObject authoringSourceAsset;
    [SerializeField, HideInInspector] private string authoringEntryId;

#if UNITY_EDITOR
    [System.NonSerialized]
    readonly Dictionary<string, List<SkillVfxAuthoringEntry>> activePreviewLoops =
        new Dictionary<string, List<SkillVfxAuthoringEntry>>(System.StringComparer.Ordinal);
#endif

    public ScriptableObject TimelineSourceAsset => ResolveTimelineSourceAsset();
    public string TimelineEntryId => ResolveTimelineEntryId();
    public Transform CharacterRoot => GetAnimationCharacterRoot();
    public Animator PreviewAnimator
    {
        get
        {
            Transform root = GetAnimationCharacterRoot();
            return root != null ? root.GetComponentInChildren<Animator>(true) : null;
        }
    }

    protected virtual ScriptableObject ResolveTimelineSourceAsset() => sourceAsset;
    protected virtual string ResolveTimelineEntryId() => selectedEntryId;

    protected virtual void AssignTimelineSourceAsset(ScriptableObject asset)
    {
        sourceAsset = asset;
    }

    void OnTimelineSourceAssetChanged()
    {
        SelectFirstValidTimelineEntry();
        ResetAuthoringOwner();
    }

    void OnTimelineEntryChanged()
    {
        ResetAuthoringOwner();
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

    void ResetAuthoringOwner()
    {
        StopAllVfx();
        authoringSourceAsset = null;
        authoringEntryId = null;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void SetTimelineSource(ScriptableObject asset, string entryId)
    {
#if UNITY_EDITOR
        if (TimelineSourceAsset == asset && string.Equals(TimelineEntryId, entryId, System.StringComparison.Ordinal))
            return;

        StopAllVfx();
        Undo.RecordObject(this, "Change Animation VFX Source");
        AssignTimelineSourceAsset(asset);
        sourceAsset = asset;
        selectedEntryId = entryId;
        SelectFirstValidTimelineEntry();
        ResetAuthoringOwner();
        EditorUtility.SetDirty(this);
#else
        AssignTimelineSourceAsset(asset);
        sourceAsset = asset;
        selectedEntryId = string.IsNullOrWhiteSpace(entryId) ? "main" : entryId;
#endif
    }

    public bool PrepareTimelineAuthoring()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null || !TryGetAuthoringRoots(out Transform character, out Transform root))
            return false;

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

        StopAllVfx();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Switch Animation VFX Entry");
        try
        {
            RemoveExistingAuthoring(root);
            RebuildAuthoring(source, character, root, "Switch Animation VFX Entry");
            Undo.RecordObject(this, "Switch Animation VFX Entry");
            authoringSourceAsset = source.SourceAsset;
            authoringEntryId = source.EntryId;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(root);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        return true;
#else
        return TimelineSourceAsset != null;
#endif
    }

    public void CreateOrSyncTimelineVfxSlots()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null || !PrepareTimelineAuthoring() ||
            !TryGetAuthoringRoots(out Transform character, out Transform root))
        {
            return;
        }

        int markerCount = source.MarkerCount;
        if (markerCount <= 0)
        {
            Debug.LogWarning($"'{source.DisplayName}' has no Vfx marker.", source.SourceAsset);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(SyncUndoLabel);
        try
        {
            MigrateLegacyEntriesToSlots(root);
            var slotsByCue = new Dictionary<int, SkillVfxAuthoringSlot>();
            SkillVfxAuthoringSlot[] slots = GetSourceSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !slotsByCue.ContainsKey(slots[i].CueIndex))
                    slotsByCue.Add(slots[i].CueIndex, slots[i]);
            }

            for (int cueIndex = 0; cueIndex < markerCount; cueIndex++)
            {
                if (slotsByCue.ContainsKey(cueIndex))
                    continue;

                SkillVfxAuthoringSlot slot = CreateSlotObject(root, cueIndex, SyncUndoLabel);
                slotsByCue.Add(cueIndex, slot);
                CreateSavedCueEntries(source, cueIndex, character, slot.transform, SyncUndoLabel);
            }

            MarkHierarchyDirty(root);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
#endif
    }

    public void LoadTimelineVfxData()
    {
#if UNITY_EDITOR
        IAnimationVfxTimelineSource source = CreateTimelineSource();
        if (source == null || !TryGetAuthoringRoots(out Transform character, out Transform root))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(LoadUndoLabel);
        try
        {
            StopAllVfx();
            RemoveExistingAuthoring(root);
            RebuildAuthoring(source, character, root, LoadUndoLabel);
            Undo.RecordObject(this, LoadUndoLabel);
            authoringSourceAsset = source.SourceAsset;
            authoringEntryId = source.EntryId;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(root);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
#endif
    }

    public void SaveTimelineVfxData()
    {
#if UNITY_EDITOR
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

    public void RefreshAllVisuals()
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
            entries[i]?.RefreshVisualPreview();
    }

    public void StopAllVfx()
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
            entries[i]?.StopVisualPreview();
#if UNITY_EDITOR
        activePreviewLoops.Clear();
#endif
    }

    public void StopAllLoopPreviews(bool allowParticlesToFinish)
    {
#if UNITY_EDITOR
        foreach (List<SkillVfxAuthoringEntry> entries in activePreviewLoops.Values)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null)
                    continue;
                if (allowParticlesToFinish)
                    entries[i].StopLoopVisualPreview(true, 0f);
                else
                    entries[i].StopVisualPreview();
            }
        }
        activePreviewLoops.Clear();
#endif
    }

    public void PlayVfx(int cueIndex)
    {
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

    public void PlayOneShotVfx(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].CueIndex == cueIndex &&
                entries[i].AnimationAction == AnimationVfxAction.OneShot)
                entries[i].PlayVisualPreview();
        }
    }

    public void StopVfx(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].CueIndex == cueIndex)
                entries[i].StopVisualPreview();
        }
    }

    public void SyncLoopPreviews(int appliedCueCount)
    {
#if UNITY_EDITOR
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        var desired = new Dictionary<string, List<SkillVfxAuthoringEntry>>(System.StringComparer.Ordinal);
        for (int cueIndex = 0; cueIndex < appliedCueCount; cueIndex++)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                SkillVfxAuthoringEntry entry = entries[i];
                if (entry == null || entry.CueIndex != cueIndex)
                    continue;
                string key = NormalizeLoopKey(entry.LoopKey);
                if (key == null)
                    continue;
                if (entry.AnimationAction == AnimationVfxAction.StartLoop && entry.Prefab != null)
                {
                    if (!desired.TryGetValue(key, out List<SkillVfxAuthoringEntry> group))
                    {
                        group = new List<SkillVfxAuthoringEntry>();
                        desired[key] = group;
                    }
                    group.Add(entry);
                }
                else if (entry.AnimationAction == AnimationVfxAction.StopLoop)
                    desired.Remove(key);
            }
        }

        var activeKeys = new List<string>(activePreviewLoops.Keys);
        for (int i = 0; i < activeKeys.Count; i++)
        {
            string key = activeKeys[i];
            if (!desired.TryGetValue(key, out List<SkillVfxAuthoringEntry> group) ||
                !AreSamePreviewGroup(activePreviewLoops[key], group))
                StopPreviewLoopGroup(key, false, 0f);
        }

        foreach (KeyValuePair<string, List<SkillVfxAuthoringEntry>> pair in desired)
        {
            if (activePreviewLoops.ContainsKey(pair.Key))
                continue;
            for (int i = 0; i < pair.Value.Count; i++)
                pair.Value[i]?.PlayVisualPreview();
            activePreviewLoops[pair.Key] = new List<SkillVfxAuthoringEntry>(pair.Value);
        }
#endif
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

        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        if (entries.Length == 0 && slots.Length == 0)
        {
            Debug.LogWarning($"No Animation VFX authoring slot was found under '{root.name}'.", this);
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
                cue.localPosition = anchor.InverseTransformPoint(placement.position);
                cue.localEulerAngles = (Quaternion.Inverse(anchor.rotation) * placement.rotation).eulerAngles;
                cue.localScale = CalculateScaleMultiplier(placement, cue.prefab);
            }
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

    static void CreateSavedCueEntries(
        IAnimationVfxTimelineSource source,
        int cueIndex,
        Transform character,
        Transform root,
        string undoLabel)
    {
        for (int i = 0; i < source.CueCount; i++)
        {
            IAnimationVfxCue cue = source.GetCue(i);
            if (cue == null || cue.CueIndex != cueIndex)
                continue;
            SkillVfxAuthoringEntry entry = CreateEntryObject(root, cue, undoLabel);
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

    static void MigrateLegacyEntriesToSlots(Transform root)
    {
        SkillVfxAuthoringEntry[] entries = root.GetComponentsInChildren<SkillVfxAuthoringEntry>(true);
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.GetComponentInParent<SkillVfxAuthoringSlot>() != null)
                continue;
            SkillVfxAuthoringSlot slot = CreateSlotObject(root, entry.CueIndex, SyncUndoLabel);
            Undo.SetTransformParent(entry.transform, slot.transform, SyncUndoLabel);
        }
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

    static bool AreSamePreviewGroup(List<SkillVfxAuthoringEntry> left, List<SkillVfxAuthoringEntry> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
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
        Transform root = GetSourceRoot();
        return root != null
            ? root.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringEntry>();
    }

    SkillVfxAuthoringSlot[] GetSourceSlots()
    {
        Transform root = GetSourceRoot();
        return root != null
            ? root.GetComponentsInChildren<SkillVfxAuthoringSlot>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringSlot>();
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
