using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[HideMonoScript]
public sealed class SetSkillVfxData : MonoBehaviour
{
    const string CreateEntryUndoLabel = "Create Skill VFX Entry";
    const string SyncSlotsUndoLabel = "Create Skill VFX Slots";
    const string LoadEntriesUndoLabel = "Load Skill VFX Entries";

#if UNITY_EDITOR
    [System.NonSerialized]
    readonly Dictionary<string, List<SkillVfxAuthoringEntry>> activePreviewLoops =
        new Dictionary<string, List<SkillVfxAuthoringEntry>>(System.StringComparer.Ordinal);
#endif

    [Title("Authoring Target")]
    [SerializeField] private SkillGemDefinition skill;
    [SerializeField, HideInInspector] private SkillGemDefinition authoringSkill;
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform sourceVfxRoot;
    [SerializeField] private bool includeInactiveObjects = true;

    [Title("New Entry Settings")]
    [SerializeField, Min(0), LabelText("VFX Cue Index")]
    private int newCueIndex;
    [SerializeField] private SkillVfxAction newAction;
    [SerializeField, AssetsOnly, PreviewField(60, ObjectFieldAlignment.Left), ShowIf(nameof(NewEntryRequiresPrefab))]
    private GameObject newPrefab;
    [SerializeField, ShowIf(nameof(NewEntryRequiresPrefab))]
    private SkillVfxAnchor newAnchor = SkillVfxAnchor.CastOrigin;
    [SerializeField, ShowIf(nameof(ShowNewCustomAnchorPath))]
    private string newCustomAnchorPath;
    [SerializeField, ShowIf(nameof(ShowNewHumanoidBone))]
    private HumanBodyBones newHumanoidBone = HumanBodyBones.RightHand;
    [SerializeField, HideInInspector]
    private bool newParentToAnchor;

    [ShowInInspector, ShowIf(nameof(NewEntryRequiresPrefab)), LabelText("Anchor Mode")]
    [PropertyTooltip("World Space keeps the spawned VFX at its spawn position. Follow Anchor moves it with the selected anchor.")]
    private SkillVfxAnchorMode NewAnchorMode
    {
        get => newParentToAnchor ? SkillVfxAnchorMode.FollowAnchor : SkillVfxAnchorMode.WorldSpace;
        set => newParentToAnchor = value == SkillVfxAnchorMode.FollowAnchor;
    }
    [SerializeField, ShowIf(nameof(NewEntryUsesLoopKey))]
    private string newLoopKey;
    [SerializeField, Min(0f), ShowIf(nameof(ShowNewExtraLife))]
    private float newExtraLife;
    [SerializeField, ShowIf(nameof(ShowNewStopOptions))]
    private bool newAllowParticlesToFinish = true;

    [ShowInInspector, ReadOnly, PropertyOrder(-1), LabelText("Authoring Status")]
    private string AuthoringStatus => BuildAuthoringStatus();

    public SkillGemDefinition Skill => skill;
    public Transform CharacterRoot => GetCharacterRoot();
    public Animator PreviewAnimator
    {
        get
        {
            Transform root = GetCharacterRoot();
            return root != null ? root.GetComponentInChildren<Animator>(true) : null;
        }
    }

    private bool NewEntryRequiresPrefab => newAction != SkillVfxAction.StopLoop;
    private bool NewEntryUsesLoopKey => newAction != SkillVfxAction.OneShot;
    private bool ShowNewCustomAnchorPath => NewEntryRequiresPrefab && newAnchor == SkillVfxAnchor.CustomChildPath;
    private bool ShowNewHumanoidBone => NewEntryRequiresPrefab && newAnchor == SkillVfxAnchor.HumanoidBone;
    private bool ShowNewExtraLife => newAction == SkillVfxAction.OneShot || newAction == SkillVfxAction.StopLoop;
    private bool ShowNewStopOptions => newAction == SkillVfxAction.StopLoop;

    [Button("Create / Sync VFX Slots From Timeline", ButtonSizes.Large)]
    [PropertyTooltip("Create one VFX slot for every timeline marker and migrate legacy entries into their matching slots.")]
    public void CreateOrSyncVfxSlotsFromTimeline()
    {
#if UNITY_EDITOR
        if (!TryGetSkill(out SkillGemDefinition targetSkill) ||
            !PrepareAuthoringForAssignedSkill() ||
            !TryGetAuthoringRoots(out Transform character, out Transform sourceRoot))
        {
            return;
        }

        int markerCount = targetSkill.GetSkillVfxMarkerCount();
        if (markerCount <= 0)
        {
            Debug.LogWarning($"Skill '{targetSkill.name}' has no Vfx marker in its Skill Clip.", targetSkill);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(SyncSlotsUndoLabel);

        GameObject firstCreated = null;
        int createdSlotCount = 0;
        int createdEntryCount = 0;
        try
        {
            MigrateLegacyEntriesToSlots(sourceRoot);

            SkillVfxAuthoringSlot[] existingSlots = GetSourceSlots();
            var slotsByCue = new Dictionary<int, SkillVfxAuthoringSlot>();
            for (int i = 0; i < existingSlots.Length; i++)
            {
                SkillVfxAuthoringSlot slot = existingSlots[i];
                if (slot != null && !slotsByCue.ContainsKey(slot.CueIndex))
                    slotsByCue.Add(slot.CueIndex, slot);
            }

            for (int cueIndex = 0; cueIndex < markerCount; cueIndex++)
            {
                if (slotsByCue.ContainsKey(cueIndex))
                    continue;

                SkillVfxAuthoringSlot slot = CreateSlotObject(sourceRoot, cueIndex, SyncSlotsUndoLabel);
                slotsByCue.Add(cueIndex, slot);
                firstCreated ??= slot.gameObject;
                createdSlotCount++;

                createdEntryCount += CreateSavedCueEntries(
                    targetSkill,
                    cueIndex,
                    character,
                    slot.transform);
            }

            MarkHierarchyDirty(sourceRoot);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (firstCreated != null)
        {
            Selection.activeGameObject = firstCreated;
            EditorGUIUtility.PingObject(firstCreated);
        }

        Debug.Log(
            createdSlotCount > 0 || createdEntryCount > 0
                ? $"Created {createdSlotCount} VFX slot(s) and loaded {createdEntryCount} saved VFX entry(s) under '{sourceRoot.name}'."
                : $"All {markerCount} timeline VFX marker(s) already have authoring slots.",
            sourceRoot);
#else
        Debug.LogWarning("CreateOrSyncVfxSlotsFromTimeline is only available in the Unity Editor.", this);
#endif
    }

    [Button("Create Authoring Entry", ButtonSizes.Large)]
    [PropertyTooltip("Create one VFX entry under the slot selected by VFX Cue Index using New Entry Settings.")]
    private void CreateAuthoringEntry()
    {
#if UNITY_EDITOR
        if (!PrepareAuthoringForAssignedSkill() ||
            !TryGetAuthoringRoots(out Transform character, out Transform sourceRoot))
            return;

        SkillVfxEvent cue = CreateNewEntryData();
        var issues = new List<string>();
        cue.CollectValidationIssues(issues, 0);
        CollectAnchorValidationIssues(character, cue, "New entry", issues);
        if (issues.Count > 0)
        {
            Debug.LogWarning("Cannot create Skill VFX entry:\n- " + string.Join("\n- ", issues), this);
            return;
        }

        SkillVfxAuthoringSlot slot = GetOrCreateSlot(sourceRoot, cue.cueIndex, CreateEntryUndoLabel);
        SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, CreateEntryUndoLabel);
        ApplyStoredPose(character, entry, cue);
        entry.RefreshVisualPreview();
        MarkHierarchyDirty(sourceRoot);
        Selection.activeGameObject = entry.gameObject;
        EditorGUIUtility.PingObject(entry.gameObject);
#else
        Debug.LogWarning("CreateAuthoringEntry is only available in the Unity Editor.", this);
#endif
    }

    public void AddPrefabsToSlot(SkillVfxAuthoringSlot slot, IReadOnlyList<GameObject> prefabs)
    {
#if UNITY_EDITOR
        if (!PrepareAuthoringForAssignedSkill() || slot == null || prefabs == null ||
            !ContainsAuthoringTransform(slot.transform))
            return;

        Transform character = GetCharacterRoot();
        SkillVfxAuthoringEntry firstCreated = null;
        int createdCount = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefabAsset = prefabs[i];
            if (prefabAsset == null)
                continue;

            SkillVfxEvent cue = CreateEmptySlotData(slot.CueIndex);
            cue.prefab = prefabAsset;
            SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, CreateEntryUndoLabel);
            ApplyStoredPose(character, entry, cue);
            firstCreated ??= entry;
            createdCount++;
        }

        if (createdCount == 0)
        {
            Debug.LogWarning("Assign at least one VFX prefab to add.", slot);
            return;
        }

        MarkHierarchyDirty(GetSourceRoot());
        Selection.activeGameObject = firstCreated.gameObject;
        EditorGUIUtility.PingObject(firstCreated.gameObject);
#endif
    }

    public void AddEmptyEntryToSlot(SkillVfxAuthoringSlot slot)
    {
#if UNITY_EDITOR
        if (!PrepareAuthoringForAssignedSkill() || slot == null || !ContainsAuthoringTransform(slot.transform))
            return;

        SkillVfxEvent cue = CreateEmptySlotData(slot.CueIndex);
        SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, CreateEntryUndoLabel);
        MarkHierarchyDirty(GetSourceRoot());
        Selection.activeGameObject = entry.gameObject;
#endif
    }

    [Button("Load VFX From Skill", ButtonSizes.Large)]
    [PropertyTooltip("Replace existing VFX slots and entries with grouped data loaded from the assigned Skill asset.")]
    private void LoadVfxFromSkill()
    {
#if UNITY_EDITOR
        if (!TryGetSkill(out SkillGemDefinition targetSkill) ||
            !TryGetAuthoringRoots(out Transform character, out Transform sourceRoot))
        {
            return;
        }

        IReadOnlyList<SkillVfxEvent> cues = targetSkill.SkillVfxEvents;
        if (cues.Count == 0)
        {
            Debug.LogWarning($"Skill '{targetSkill.name}' has no saved timeline VFX entries.", targetSkill);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(LoadEntriesUndoLabel);

        try
        {
            RemoveExistingAuthoring(sourceRoot);
            authoringSkill = targetSkill;
            EditorUtility.SetDirty(this);

            SkillVfxAuthoringEntry firstEntry = null;
            var slotsByCue = new Dictionary<int, SkillVfxAuthoringSlot>();
            int createdCount = 0;
            for (int i = 0; i < cues.Count; i++)
            {
                SkillVfxEvent cue = cues[i];
                if (cue == null)
                    continue;

                if (!slotsByCue.TryGetValue(cue.cueIndex, out SkillVfxAuthoringSlot slot))
                {
                    slot = CreateSlotObject(sourceRoot, cue.cueIndex, LoadEntriesUndoLabel);
                    slotsByCue.Add(cue.cueIndex, slot);
                }

                SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, LoadEntriesUndoLabel);
                ApplyStoredPose(character, entry, cue);
                entry.RefreshVisualPreview();
                firstEntry ??= entry;
                createdCount++;
            }

            MarkHierarchyDirty(sourceRoot);
            if (firstEntry != null)
                Selection.activeGameObject = firstEntry.gameObject;

            Debug.Log(
                $"Loaded {createdCount} Skill VFX entries into {slotsByCue.Count} slot(s) from '{targetSkill.name}'.",
                sourceRoot);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
#else
        Debug.LogWarning("LoadVfxFromSkill is only available in the Unity Editor.", this);
#endif
    }

    [Button("Save VFX To Skill", ButtonSizes.Large)]
    [PropertyTooltip("Read authoring markers and save their settings and anchor-relative transforms into the Skill asset.")]
    public void SaveVfxToSkill()
    {
#if UNITY_EDITOR
        if (!TryGetSkill(out SkillGemDefinition targetSkill) ||
            !PrepareAuthoringForAssignedSkill() ||
            !TryBuildSourceData(out List<SkillVfxEvent> cues, out List<string> issues))
        {
            return;
        }

        Undo.RecordObject(targetSkill, "Save Skill VFX Data");
        targetSkill.ReplaceSkillVfxEvents(cues);
        EditorUtility.SetDirty(targetSkill);
        AssetDatabase.SaveAssets();

        if (issues.Count > 0)
        {
            Debug.LogWarning(
                $"Saved {cues.Count} timeline VFX entries to '{targetSkill.name}', but found {issues.Count} issue(s):\n- " +
                string.Join("\n- ", issues),
                targetSkill);
            return;
        }

        Debug.Log($"Saved {cues.Count} timeline VFX entries to skill '{targetSkill.name}'.", targetSkill);
#else
        Debug.LogWarning("SaveVfxToSkill is only available in the Unity Editor.", this);
#endif
    }

    [Button("Refresh All Visuals")]
    public void RefreshAllVisuals()
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
            entries[i]?.RefreshVisualPreview();
    }

    [Button("Play All VFX")]
    public void PlayAllVfx()
    {
        StopAllVfx();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        int maximumCueIndex = -1;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
                maximumCueIndex = Mathf.Max(maximumCueIndex, entries[i].CueIndex);
        }

        for (int cueIndex = 0; cueIndex <= maximumCueIndex; cueIndex++)
            PlayVfx(cueIndex);
    }

    [Button("Stop All VFX")]
    public void StopAllVfx()
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
                entries[i].StopVisualPreview();
        }

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
                SkillVfxAuthoringEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (allowParticlesToFinish)
                    entry.StopLoopVisualPreview(true, 0f);
                else
                    entry.StopVisualPreview();
            }
        }

        activePreviewLoops.Clear();
#endif
    }

    public void PlayVfx(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
#if UNITY_EDITOR
        var replacedLoopKeys = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.CueIndex != cueIndex || entry.Action != SkillVfxAction.StartLoop ||
                entry.Prefab == null)
            {
                continue;
            }

            string key = NormalizeLoopKey(entry.LoopKey);
            if (key != null && replacedLoopKeys.Add(key))
                StopPreviewLoopGroup(key, allowParticlesToFinish: false, extraLife: 0f);
        }
#endif

        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.CueIndex != cueIndex)
                continue;

#if UNITY_EDITOR
            PlayPreviewEntry(entry);
#else
            entry.PlayVisualPreview();
#endif
        }
    }

    public void PlayOneShotVfx(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null && entry.CueIndex == cueIndex && entry.Action == SkillVfxAction.OneShot)
                entry.PlayVisualPreview();
        }
    }

    public void StopVfx(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null && entry.CueIndex == cueIndex)
                entry.StopVisualPreview();
        }

#if UNITY_EDITOR
        var activeKeys = new List<string>();
        foreach (KeyValuePair<string, List<SkillVfxAuthoringEntry>> pair in activePreviewLoops)
        {
            List<SkillVfxAuthoringEntry> loopEntries = pair.Value;
            if (loopEntries == null)
            {
                activeKeys.Add(pair.Key);
                continue;
            }

            for (int i = 0; i < loopEntries.Count; i++)
            {
                if (loopEntries[i] == null || loopEntries[i].CueIndex == cueIndex)
                {
                    activeKeys.Add(pair.Key);
                    break;
                }
            }
        }

        for (int i = 0; i < activeKeys.Count; i++)
            StopPreviewLoopGroup(activeKeys[i], allowParticlesToFinish: false, extraLife: 0f);
#endif
    }

    public void SyncLoopPreviews(int appliedCueCount)
    {
#if UNITY_EDITOR
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        var desiredLoops = new Dictionary<string, List<SkillVfxAuthoringEntry>>(System.StringComparer.Ordinal);
        for (int cueIndex = 0; cueIndex < appliedCueCount; cueIndex++)
        {
            var replacedLoopKeys = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                SkillVfxAuthoringEntry entry = entries[i];
                if (entry == null || entry.CueIndex != cueIndex)
                    continue;

                string key = NormalizeLoopKey(entry.LoopKey);
                if (key == null)
                    continue;

                if (entry.Action == SkillVfxAction.StartLoop)
                {
                    if (entry.Prefab == null)
                        continue;

                    List<SkillVfxAuthoringEntry> group;
                    if (replacedLoopKeys.Add(key))
                    {
                        group = new List<SkillVfxAuthoringEntry>();
                        desiredLoops[key] = group;
                    }
                    else if (!desiredLoops.TryGetValue(key, out group))
                    {
                        group = new List<SkillVfxAuthoringEntry>();
                        desiredLoops[key] = group;
                    }

                    group.Add(entry);
                }
                else if (entry.Action == SkillVfxAction.StopLoop)
                    desiredLoops.Remove(key);
            }
        }

        var activeKeys = new List<string>(activePreviewLoops.Keys);
        for (int i = 0; i < activeKeys.Count; i++)
        {
            string key = activeKeys[i];
            List<SkillVfxAuthoringEntry> activeEntries = activePreviewLoops[key];
            if (desiredLoops.TryGetValue(key, out List<SkillVfxAuthoringEntry> desiredEntries) &&
                AreSamePreviewGroup(activeEntries, desiredEntries))
            {
                continue;
            }

            StopPreviewLoopGroup(key, allowParticlesToFinish: false, extraLife: 0f);
        }

        foreach (KeyValuePair<string, List<SkillVfxAuthoringEntry>> pair in desiredLoops)
        {
            if (activePreviewLoops.ContainsKey(pair.Key) || pair.Value == null || pair.Value.Count == 0)
                continue;

            var activeEntries = new List<SkillVfxAuthoringEntry>(pair.Value.Count);
            for (int i = 0; i < pair.Value.Count; i++)
            {
                SkillVfxAuthoringEntry entry = pair.Value[i];
                if (entry == null)
                    continue;

                entry.PlayVisualPreview();
                activeEntries.Add(entry);
            }

            if (activeEntries.Count > 0)
                activePreviewLoops[pair.Key] = activeEntries;
        }
#endif
    }

#if UNITY_EDITOR
    void PlayPreviewEntry(SkillVfxAuthoringEntry entry)
    {
        if (entry.Action == SkillVfxAction.OneShot)
        {
            entry.PlayVisualPreview();
            return;
        }

        string key = NormalizeLoopKey(entry.LoopKey);
        if (key == null)
            return;

        if (entry.Action == SkillVfxAction.StartLoop)
        {
            entry.PlayVisualPreview();
            if (!activePreviewLoops.TryGetValue(key, out List<SkillVfxAuthoringEntry> activeEntries))
            {
                activeEntries = new List<SkillVfxAuthoringEntry>();
                activePreviewLoops[key] = activeEntries;
            }

            activeEntries.Add(entry);
            return;
        }

        StopPreviewLoopGroup(key, entry.AllowParticlesToFinish, entry.ExtraLife);
    }

    void StopPreviewLoopGroup(string key, bool allowParticlesToFinish, float extraLife)
    {
        if (!activePreviewLoops.TryGetValue(key, out List<SkillVfxAuthoringEntry> entries))
            return;

        activePreviewLoops.Remove(key);
        for (int i = 0; i < entries.Count; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null)
                continue;

            if (allowParticlesToFinish)
                entry.StopLoopVisualPreview(true, extraLife);
            else
                entry.StopVisualPreview();
        }
    }

    static bool AreSamePreviewGroup(
        List<SkillVfxAuthoringEntry> left,
        List<SkillVfxAuthoringEntry> right)
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

    static string NormalizeLoopKey(string loopKey)
    {
        return string.IsNullOrWhiteSpace(loopKey) ? null : loopKey.Trim();
    }
#endif

    public SkillVfxAuthoringEntry FindEntry(int cueIndex)
    {
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null && entry.CueIndex == cueIndex)
                return entry;
        }

        return null;
    }

    public SkillVfxAuthoringSlot FindSlot(int cueIndex)
    {
        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot != null && slot.CueIndex == cueIndex)
                return slot;
        }

        return null;
    }

    public void MoveVfxCue(int oldCueIndex, int newCueIndex)
    {
#if UNITY_EDITOR
        if (oldCueIndex < 0 || newCueIndex < 0 || oldCueIndex == newCueIndex)
            return;

        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot == null)
                continue;

            int cueIndex = slot.CueIndex;
            int nextCueIndex = cueIndex;
            if (cueIndex == oldCueIndex)
                nextCueIndex = newCueIndex;
            else if (oldCueIndex < newCueIndex && cueIndex > oldCueIndex && cueIndex <= newCueIndex)
                nextCueIndex--;
            else if (oldCueIndex > newCueIndex && cueIndex >= newCueIndex && cueIndex < oldCueIndex)
                nextCueIndex++;

            if (nextCueIndex == cueIndex)
                continue;

            Undo.RecordObject(slot, "Move Skill VFX Cue");
            slot.SetCueIndex(nextCueIndex);
            EditorUtility.SetDirty(slot);
        }

        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.GetComponentInParent<SkillVfxAuthoringSlot>() != null)
                continue;

            int cueIndex = entry.CueIndex;
            int nextCueIndex = cueIndex;
            if (cueIndex == oldCueIndex)
                nextCueIndex = newCueIndex;
            else if (oldCueIndex < newCueIndex && cueIndex > oldCueIndex && cueIndex <= newCueIndex)
                nextCueIndex--;
            else if (oldCueIndex > newCueIndex && cueIndex >= newCueIndex && cueIndex < oldCueIndex)
                nextCueIndex++;

            if (nextCueIndex == cueIndex)
                continue;

            Undo.RecordObject(entry, "Move Skill VFX Cue");
            entry.SetCueIndex(nextCueIndex);
            EditorUtility.SetDirty(entry);
        }

        if (skill != null)
        {
            Undo.RecordObject(skill, "Move Skill VFX Cue");
            skill.MoveSkillVfxCue(oldCueIndex, newCueIndex);
            EditorUtility.SetDirty(skill);
        }

        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    public void RemoveVfxCue(int cueIndex)
    {
#if UNITY_EDITOR
        if (cueIndex < 0)
            return;

        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot == null)
                continue;

            if (slot.CueIndex == cueIndex)
            {
                Undo.DestroyObjectImmediate(slot.gameObject);
            }
            else if (slot.CueIndex > cueIndex)
            {
                Undo.RecordObject(slot, "Remove Skill VFX Cue");
                slot.SetCueIndex(slot.CueIndex - 1);
                EditorUtility.SetDirty(slot);
            }
        }

        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.GetComponentInParent<SkillVfxAuthoringSlot>() != null)
                continue;

            if (entry.CueIndex == cueIndex)
            {
                Undo.DestroyObjectImmediate(entry.gameObject);
            }
            else if (entry.CueIndex > cueIndex)
            {
                Undo.RecordObject(entry, "Remove Skill VFX Cue");
                entry.SetCueIndex(entry.CueIndex - 1);
                EditorUtility.SetDirty(entry);
            }
        }

        if (skill != null)
        {
            Undo.RecordObject(skill, "Remove Skill VFX Cue");
            skill.RemoveSkillVfxCue(cueIndex);
            EditorUtility.SetDirty(skill);
        }

        MarkHierarchyDirty(GetSourceRoot());
#endif
    }

    [Button("Clear Authoring Slots")]
    [PropertyTooltip("Remove VFX slots and legacy authoring entries under Source VFX Root.")]
    private void ClearAuthoringEntries()
    {
#if UNITY_EDITOR
        Transform sourceRoot = GetSourceRoot();
        SkillVfxAuthoringEntry[] entries = GetSourceEntries();
        SkillVfxAuthoringSlot[] slots = GetSourceSlots();
        if (entries.Length == 0 && slots.Length == 0)
            return;

        if (!EditorUtility.DisplayDialog(
                "Clear Skill VFX Authoring Slots",
                $"Remove {slots.Length} slot(s) and {entries.Length} VFX entry(s) under '{sourceRoot.name}'?",
                "Remove",
                "Cancel"))
        {
            return;
        }

        RemoveExistingAuthoring(sourceRoot);
        MarkHierarchyDirty(sourceRoot);
#endif
    }

    [Button("Validate Current Data")]
    private void ValidateCurrentData()
    {
        if (!TryBuildSourceData(out List<SkillVfxEvent> cues, out List<string> issues))
            return;

        if (issues.Count == 0)
        {
            Debug.Log($"Current Skill VFX authoring data passed validation ({cues.Count} entries).", this);
            return;
        }

        Debug.LogWarning(
            $"Current Skill VFX authoring data has {issues.Count} issue(s):\n- " + string.Join("\n- ", issues),
            this);
    }

    SkillVfxEvent CreateNewEntryData()
    {
        return new SkillVfxEvent
        {
            cueIndex = Mathf.Max(0, newCueIndex),
            action = newAction,
            prefab = newPrefab,
            anchor = newAnchor,
            customAnchorPath = newCustomAnchorPath,
            humanoidBone = newHumanoidBone,
            parentToAnchor = newParentToAnchor,
            loopKey = newLoopKey,
            extraLife = newExtraLife,
            allowParticlesToFinish = newAllowParticlesToFinish,
            localScale = Vector3.one,
        };
    }

    bool TryBuildSourceData(out List<SkillVfxEvent> cues, out List<string> issues)
    {
        cues = new List<SkillVfxEvent>();
        issues = new List<string>();

        if (!TryGetAuthoringRoots(out Transform character, out Transform sourceRoot))
            return false;

        SkillVfxAuthoringEntry[] entries = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects);
        SkillVfxAuthoringSlot[] slots = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringSlot>(includeInactiveObjects);
        if (entries.Length == 0 && slots.Length == 0)
        {
            Debug.LogWarning($"No Skill VFX authoring slot was found under '{sourceRoot.name}'.", this);
            return false;
        }

        var slotCueIndices = new HashSet<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot == null)
                continue;

            if (!slotCueIndices.Add(slot.CueIndex))
                issues.Add($"Multiple VFX slots target cue {slot.CueIndex + 1}.");

            if (slot.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects).Length == 0)
                issues.Add($"VFX slot {slot.CueIndex + 1} has no VFX entries.");
        }

        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.GetComponentInParent<SkillVfxAuthoringSlot>() == null)
                issues.Add($"Entry '{entry.name}' is not inside a SkillVfxAuthoringSlot. Run Create / Sync VFX Slots From Timeline.");

            SkillVfxEvent cue = entry.CreateData();
            Transform placement = entry.GetPlacementTransform();
            Transform anchor = SkillVfxAnchorResolver.Resolve(character, cue);
            if (anchor == null)
            {
                issues.Add($"Entry '{entry.name}' could not resolve its anchor.");
            }
            else if (cue.RequiresPrefab)
            {
                cue.localPosition = anchor.InverseTransformPoint(placement.position);
                cue.localEulerAngles = (Quaternion.Inverse(anchor.rotation) * placement.rotation).eulerAngles;
                cue.localScale = CalculateScaleMultiplier(placement, cue.prefab);

                int prefabInstanceCount = entry.GetAuthoredPrefabInstanceCount();
                if (prefabInstanceCount == 0 && cue.prefab == null)
                    issues.Add($"Entry '{entry.name}' needs a prefab instance as a direct child.");
                else if (prefabInstanceCount > 1)
                    issues.Add($"Entry '{entry.name}' has {prefabInstanceCount} direct prefab children; only the first is saved.");
            }

            cues.Add(cue);
            CollectAnchorValidationIssues(character, cue, $"Entry '{entry.name}'", issues);
        }

        SkillGemDefinition.CollectSkillVfxValidationIssues(cues, issues);
        if (skill != null)
        {
            int markerCount = skill.GetSkillVfxMarkerCount();
            var authoredCueIndices = new HashSet<int>(slotCueIndices);
            for (int i = 0; i < cues.Count; i++)
            {
                SkillVfxEvent cue = cues[i];
                if (cue == null)
                    continue;

                if (cue.cueIndex >= 0 && cue.cueIndex < markerCount)
                    authoredCueIndices.Add(cue.cueIndex);
                else if (cue.cueIndex >= markerCount)
                {
                    issues.Add(
                        $"Entry {i + 1} targets cue {cue.cueIndex + 1}, but the Skill Clip has only {markerCount} Vfx marker(s).");
                }
            }

            for (int cueIndex = 0; cueIndex < markerCount; cueIndex++)
            {
                if (!authoredCueIndices.Contains(cueIndex))
                    issues.Add($"Timeline Vfx marker {cueIndex + 1} has no authoring slot.");
            }


            for (int i = 0; i < slots.Length; i++)
            {
                SkillVfxAuthoringSlot slot = slots[i];
                if (slot != null && slot.CueIndex >= markerCount)
                    issues.Add($"VFX slot {slot.CueIndex + 1} is outside the {markerCount} timeline marker(s).");
            }
        }

        return true;
    }

    static SkillVfxAuthoringEntry CreateEntryObject(Transform sourceRoot, SkillVfxEvent cue, string undoLabel)
    {
        string objectName = $"Vfx_{cue.cueIndex + 1}_{cue.action}";
        GameObject entryObject = new GameObject(objectName);
        entryObject.transform.SetParent(sourceRoot, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(entryObject, undoLabel);
        SkillVfxAuthoringEntry entry = Undo.AddComponent<SkillVfxAuthoringEntry>(entryObject);
#else
        SkillVfxAuthoringEntry entry = entryObject.AddComponent<SkillVfxAuthoringEntry>();
#endif
        entry.Configure(cue);
#if UNITY_EDITOR
        entry.CreateAuthoredPrefabInstance(undoLabel);
#endif
        return entry;
    }

    static SkillVfxAuthoringSlot CreateSlotObject(Transform sourceRoot, int cueIndex, string undoLabel)
    {
        GameObject slotObject = new GameObject($"Vfx_Slot_{cueIndex + 1}");
        slotObject.transform.SetParent(sourceRoot, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(slotObject, undoLabel);
        SkillVfxAuthoringSlot slot = Undo.AddComponent<SkillVfxAuthoringSlot>(slotObject);
#else
        SkillVfxAuthoringSlot slot = slotObject.AddComponent<SkillVfxAuthoringSlot>();
#endif
        slot.Configure(cueIndex);
        return slot;
    }

    static SkillVfxAuthoringSlot GetOrCreateSlot(Transform sourceRoot, int cueIndex, string undoLabel)
    {
        SkillVfxAuthoringSlot[] slots = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot != null && slot.CueIndex == cueIndex)
                return slot;
        }

        return CreateSlotObject(sourceRoot, cueIndex, undoLabel);
    }

    static void ApplyStoredPose(Transform character, SkillVfxAuthoringEntry entry, SkillVfxEvent cue)
    {
        if (entry == null || cue == null)
            return;

        Transform placement = entry.GetPlacementTransform();
        Transform anchor = SkillVfxAnchorResolver.Resolve(character, cue);
        if (anchor != null && cue.RequiresPrefab)
        {
            SkillVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
            placement.SetPositionAndRotation(position, rotation);
            placement.localScale = cue.prefab != null
                ? Vector3.Scale(cue.prefab.transform.localScale, cue.localScale)
                : cue.localScale;
        }
        else
        {
            placement.localPosition = Vector3.zero;
            placement.localRotation = Quaternion.identity;
            placement.localScale = Vector3.one;
        }
    }

    static int CreateSavedCueEntries(
        SkillGemDefinition targetSkill,
        int cueIndex,
        Transform character,
        Transform slotRoot)
    {
        IReadOnlyList<SkillVfxEvent> savedCues = targetSkill.SkillVfxEvents;
        int createdCount = 0;
        for (int i = 0; i < savedCues.Count; i++)
        {
            SkillVfxEvent cue = savedCues[i];
            if (cue == null || cue.cueIndex != cueIndex)
                continue;

            SkillVfxAuthoringEntry entry = CreateEntryObject(slotRoot, cue, SyncSlotsUndoLabel);
            ApplyStoredPose(character, entry, cue);
            createdCount++;
        }

        return createdCount;
    }

    static SkillVfxEvent CreateEmptySlotData(int cueIndex)
    {
        return new SkillVfxEvent
        {
            cueIndex = cueIndex,
            action = SkillVfxAction.OneShot,
            anchor = SkillVfxAnchor.CastOrigin,
            localScale = Vector3.one,
            allowParticlesToFinish = true,
        };
    }

    static Vector3 CalculateScaleMultiplier(Transform placement, GameObject prefabAsset)
    {
        if (placement == null || prefabAsset == null)
            return Vector3.one;

        Vector3 authoredScale = placement.lossyScale;
        Vector3 prefabScale = prefabAsset.transform.lossyScale;
        return new Vector3(
            SafeDivide(authoredScale.x, prefabScale.x),
            SafeDivide(authoredScale.y, prefabScale.y),
            SafeDivide(authoredScale.z, prefabScale.z));
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? 1f : value / divisor;
    }

    static void CollectAnchorValidationIssues(
        Transform character,
        SkillVfxEvent cue,
        string label,
        List<string> issues)
    {
        if (character == null || cue == null || issues == null || !cue.RequiresPrefab)
            return;

        Transform root = ResolveCharacterRoot(character);
        if (cue.anchor == SkillVfxAnchor.CustomChildPath &&
            !string.IsNullOrWhiteSpace(cue.customAnchorPath) &&
            root.Find(cue.customAnchorPath.Trim()) == null)
        {
            issues.Add($"{label} cannot find custom anchor path '{cue.customAnchorPath}'.");
        }

        if (cue.anchor == SkillVfxAnchor.HumanoidBone)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.GetBoneTransform(cue.humanoidBone) == null)
                issues.Add($"{label} cannot resolve Humanoid bone '{cue.humanoidBone}'.");
        }
    }

    public bool PrepareAuthoringForAssignedSkill()
    {
#if UNITY_EDITOR
        if (!TryGetSkill(out SkillGemDefinition targetSkill) ||
            !TryGetAuthoringRoots(out Transform character, out Transform sourceRoot))
        {
            return false;
        }

        if (authoringSkill == targetSkill)
            return true;

        if (authoringSkill == null && AuthoringHierarchyMatchesSkill(targetSkill, sourceRoot))
        {
            authoringSkill = targetSkill;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(sourceRoot);
            return true;
        }

        StopAllVfx();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Switch Skill VFX Authoring");
        try
        {
            RemoveExistingAuthoring(sourceRoot);
            RebuildAuthoringFromSkill(targetSkill, character, sourceRoot, "Switch Skill VFX Authoring");
            Undo.RecordObject(this, "Switch Skill VFX Authoring");
            authoringSkill = targetSkill;
            EditorUtility.SetDirty(this);
            MarkHierarchyDirty(sourceRoot);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        return true;
#else
        return skill != null;
#endif
    }

#if UNITY_EDITOR
    bool AuthoringHierarchyMatchesSkill(SkillGemDefinition targetSkill, Transform sourceRoot)
    {
        SkillVfxAuthoringEntry[] entries = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects);
        IReadOnlyList<SkillVfxEvent> savedCues = targetSkill.SkillVfxEvents;
        if (entries.Length != savedCues.Count)
            return false;

        var matched = new bool[savedCues.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxEvent authoredCue = entries[i].CreateData();
            bool found = false;
            for (int j = 0; j < savedCues.Count; j++)
            {
                if (matched[j] || !HasSameAuthoringIdentity(authoredCue, savedCues[j]))
                    continue;

                matched[j] = true;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        SkillVfxAuthoringSlot[] slots = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringSlot>(includeInactiveObjects);
        int markerCount = targetSkill.GetSkillVfxMarkerCount();
        if (slots.Length != markerCount)
            return false;

        var cueIndices = new HashSet<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].CueIndex < 0 || slots[i].CueIndex >= markerCount ||
                !cueIndices.Add(slots[i].CueIndex))
            {
                return false;
            }
        }

        return true;
    }

    static bool HasSameAuthoringIdentity(SkillVfxEvent left, SkillVfxEvent right)
    {
        if (left == null || right == null)
            return left == right;

        return left.cueIndex == right.cueIndex &&
               left.action == right.action &&
               left.prefab == right.prefab &&
               left.anchor == right.anchor &&
               left.customAnchorPath == right.customAnchorPath &&
               left.humanoidBone == right.humanoidBone &&
               left.parentToAnchor == right.parentToAnchor &&
               string.Equals(left.loopKey?.Trim(), right.loopKey?.Trim(), System.StringComparison.Ordinal);
    }

    static void RebuildAuthoringFromSkill(
        SkillGemDefinition targetSkill,
        Transform character,
        Transform sourceRoot,
        string undoLabel)
    {
        int markerCount = targetSkill.GetSkillVfxMarkerCount();
        var slotsByCue = new Dictionary<int, SkillVfxAuthoringSlot>();
        for (int cueIndex = 0; cueIndex < markerCount; cueIndex++)
            slotsByCue.Add(cueIndex, CreateSlotObject(sourceRoot, cueIndex, undoLabel));

        IReadOnlyList<SkillVfxEvent> savedCues = targetSkill.SkillVfxEvents;
        for (int i = 0; i < savedCues.Count; i++)
        {
            SkillVfxEvent cue = savedCues[i];
            if (cue == null)
                continue;

            if (!slotsByCue.TryGetValue(cue.cueIndex, out SkillVfxAuthoringSlot slot))
            {
                slot = CreateSlotObject(sourceRoot, cue.cueIndex, undoLabel);
                slotsByCue.Add(cue.cueIndex, slot);
            }

            SkillVfxAuthoringEntry entry = CreateEntryObject(slot.transform, cue, undoLabel);
            ApplyStoredPose(character, entry, cue);
        }
    }
#endif

    bool TryGetSkill(out SkillGemDefinition targetSkill)
    {
        targetSkill = skill;
        if (targetSkill != null)
            return true;

        Debug.LogWarning("Skill is not assigned.", this);
        return false;
    }

    bool TryGetAuthoringRoots(out Transform character, out Transform sourceRoot)
    {
        character = GetCharacterRoot();
        sourceRoot = GetSourceRoot();

        if (character == null)
        {
            Debug.LogWarning("Could not resolve Character Root.", this);
            return false;
        }

        if (sourceRoot == null)
        {
            Debug.LogWarning("Could not resolve Source VFX Root.", this);
            return false;
        }

        if (sourceRoot.GetComponent<SkillVfxAuthoringEntry>() != null)
        {
            Debug.LogWarning("Source VFX Root must be a container, not a SkillVfxAuthoringEntry.", sourceRoot);
            return false;
        }

        return true;
    }

    public bool ContainsAuthoringTransform(Transform candidate)
    {
        Transform sourceRoot = GetSourceRoot();
        return sourceRoot != null && candidate != null &&
               (candidate == sourceRoot || candidate.IsChildOf(sourceRoot));
    }

    Transform GetCharacterRoot()
    {
        if (characterRoot != null)
            return characterRoot;

        return ResolveCharacterRoot(transform);
    }

    Transform GetSourceRoot()
    {
        if (this == null)
            return null;

        return sourceVfxRoot != null ? sourceVfxRoot : transform;
    }

    SkillVfxAuthoringEntry[] GetSourceEntries()
    {
        Transform sourceRoot = GetSourceRoot();
        return sourceRoot != null
            ? sourceRoot.GetComponentsInChildren<SkillVfxAuthoringEntry>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringEntry>();
    }

    SkillVfxAuthoringSlot[] GetSourceSlots()
    {
        Transform sourceRoot = GetSourceRoot();
        return sourceRoot != null
            ? sourceRoot.GetComponentsInChildren<SkillVfxAuthoringSlot>(includeInactiveObjects)
            : System.Array.Empty<SkillVfxAuthoringSlot>();
    }

    string BuildAuthoringStatus()
    {
        int slotCount = GetSourceSlots().Length;
        int entryCount = GetSourceEntries().Length;
        int savedCount = skill != null ? skill.SkillVfxEvents.Count : 0;
        int markerCount = skill != null ? skill.GetSkillVfxMarkerCount() : 0;
        return $"Timeline: {markerCount} / Slots: {slotCount} / Entries: {entryCount} / Saved: {savedCount}";
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

    static void MigrateLegacyEntriesToSlots(Transform sourceRoot)
    {
#if UNITY_EDITOR
        if (sourceRoot == null)
            return;

        SkillVfxAuthoringEntry[] entries = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringEntry>(true);
        for (int i = 0; i < entries.Length; i++)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry == null || entry.GetComponentInParent<SkillVfxAuthoringSlot>() != null)
                continue;

            SkillVfxAuthoringSlot slot = GetOrCreateSlot(sourceRoot, entry.CueIndex, SyncSlotsUndoLabel);
            Undo.SetTransformParent(entry.transform, slot.transform, SyncSlotsUndoLabel);
        }
#endif
    }

    static void RemoveExistingAuthoring(Transform sourceRoot)
    {
#if UNITY_EDITOR
        if (sourceRoot == null)
            return;

        SkillVfxAuthoringSlot[] slots = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringSlot>(true);
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringSlot slot = slots[i];
            if (slot != null && slot.transform != sourceRoot)
                Undo.DestroyObjectImmediate(slot.gameObject);
        }

        SkillVfxAuthoringEntry[] entries = sourceRoot.GetComponentsInChildren<SkillVfxAuthoringEntry>(true);
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            SkillVfxAuthoringEntry entry = entries[i];
            if (entry != null && entry.transform != sourceRoot &&
                entry.GetComponentInParent<SkillVfxAuthoringSlot>() == null)
            {
                Undo.DestroyObjectImmediate(entry.gameObject);
            }
        }
#endif
    }

    static void MarkHierarchyDirty(Transform root)
    {
#if UNITY_EDITOR
        if (root == null)
            return;

        EditorUtility.SetDirty(root.gameObject);
        if (root.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
#endif
    }
}
