#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// A transient draft: VFX saves and animation preview never write these values to the profile.
public sealed class DefensiveBlockTimelineSession : ScriptableObject
{
    public SkillGemDefinition skill;
    public DefensiveBlockAttackProfile profile;
    public float start;
    public float end;
    public List<DefensiveBlockWindow> windows = new();
    [SerializeField] string savedWindows;
    public int selectedWindow;
    public int WindowCount => windows.Count;
    string WindowsSnapshot => string.Join("|", windows.Select(JsonUtility.ToJson));
    [SerializeField] float savedStart;
    [SerializeField] float savedEnd;
    [SerializeField] string sourceSnapshot;
    [SerializeField] bool addRequested;
    [SerializeField] bool ownershipRequested;

    void OnEnable()
    {
        // Preserve an unsaved single-window draft across the editor code upgrade.
        if (windows != null && windows.Count > 0) return;
        windows = new List<DefensiveBlockWindow> { new DefensiveBlockWindow { startNormalized = start, endNormalized = end,
            hitboxSteps = profile != null && profile.hitboxSteps != null ? (int[])profile.hitboxSteps.Clone() : new[] { 0, 1 } } };
        savedWindows = WindowsSnapshot;
    }

    public bool IsEnabled => profile != null || addRequested;
    public bool IsDirty => addRequested || ownershipRequested ||
        (IsEnabled && (!start.Equals(savedStart) || !end.Equals(savedEnd) || WindowsSnapshot != savedWindows));
    public bool HasConflict => skill == null || skill.defensiveBlock != profile ||
        (profile != null ? EditorJsonUtility.ToJson(profile) != sourceSnapshot : sourceSnapshot != null);
    public static DefensiveBlockTimelineSession Create(SkillGemDefinition skill)
    {
        var draft = CreateInstance<DefensiveBlockTimelineSession>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        draft.skill = skill;
        draft.Reload();
        return draft;
    }

    public void Reload()
    {
        Undo.ClearUndo(this);
        profile = skill != null ? skill.defensiveBlock : null;
        addRequested = ownershipRequested = false;
        start = savedStart = profile != null ? profile.windowStartNormalized : 0f;
        end = savedEnd = profile != null ? profile.windowEndNormalized : 0.62f;
        windows = profile != null && profile.HasMultipleWindowData
            ? profile.windows.Select(w => w != null ? w.Copy() : new DefensiveBlockWindow()).ToList()
            : new List<DefensiveBlockWindow> { new DefensiveBlockWindow { startNormalized = start, endNormalized = end,
                hitboxSteps = profile != null && profile.hitboxSteps != null ? (int[])profile.hitboxSteps.Clone() : new[] { 0, 1 } } };
        start = savedStart = windows[0].startNormalized;
        end = savedEnd = windows[0].endNormalized;
        savedWindows = WindowsSnapshot;
        selectedWindow = Mathf.Clamp(selectedWindow, 0, windows.Count - 1);
        sourceSnapshot = profile != null ? EditorJsonUtility.ToJson(profile) : null;
    }

    public void AddBlock()
    {
        if (IsEnabled) return;
        Undo.RecordObject(this, "Add Block Window");
        addRequested = true;
    }

    public void RequestOwnership()
    {
        if (profile == null || DefensiveBlockSkillAuthoring.IsOwned(skill)) return;
        Undo.RecordObject(this, "Embed Block Profile");
        ownershipRequested = true;
    }

    internal void FollowOwnershipMigration(DefensiveBlockAttackProfile previous)
    {
        // Rebase only a pure ownership change. Keep unsaved timing and its Undo history;
        // external edits still require the normal conflict/reload flow.
        if (profile != previous || previous == null || EditorJsonUtility.ToJson(previous) != sourceSnapshot ||
            !DefensiveBlockSkillAuthoring.IsOwned(skill)) return;
        Undo.RecordObject(this, "Follow Embedded Block Profile");
        profile = skill.defensiveBlock;
        sourceSnapshot = EditorJsonUtility.ToJson(profile);
        ownershipRequested = false;
    }

    public float StartAt(int index) => index == 0 ? start : windows[index].startNormalized;
    public float EndAt(int index) => index == 0 ? end : windows[index].endNormalized;
    public void SetRange(int index, float from, float to)
    {
        Undo.RecordObject(this, "Edit Block Window");
        if (index == 0) { start = from; end = to; }
        else { windows[index].startNormalized = from; windows[index].endNormalized = to; }
    }
    public void AddWindow(float time)
    {
        Undo.RecordObject(this, "Add Block Window");
        int index = 0;
        while (index < windows.Count && StartAt(index) < time) index++;
        windows[0].startNormalized = start; windows[0].endNormalized = end;
        int nextStep = windows.SelectMany(w => w.hitboxSteps ?? System.Array.Empty<int>()).DefaultIfEmpty(-1).Max() + 1;
        float limit = index < windows.Count ? StartAt(index) - .001f : 1f;
        windows.Insert(index, new DefensiveBlockWindow { startNormalized = time,
            endNormalized = Mathf.Max(time, Mathf.Min(time + .1f, limit)), hitboxSteps = new[] { nextStep } });
        start = windows[0].startNormalized; end = windows[0].endNormalized; selectedWindow = index;
    }
    public void RemoveWindow(int index)
    {
        if (windows.Count <= 1) return;
        Undo.RecordObject(this, "Remove Block Window");
        windows[0].startNormalized = start; windows[0].endNormalized = end;
        windows.RemoveAt(index);
        start = windows[0].startNormalized; end = windows[0].endNormalized;
        selectedWindow = Mathf.Clamp(index, 0, windows.Count - 1);
    }
    public bool Contains(float time) => Enumerable.Range(0, windows.Count).Any(i => time >= StartAt(i) && time <= EndAt(i));

    public string ValidationError
    {
        get
        {
            var steps = new HashSet<int>();
            for (int i = 0; i < windows.Count; i++)
            {
                var window = windows[i].Copy(); window.startNormalized = StartAt(i); window.endNormalized = EndAt(i);
                if (!window.IsValid) return "Block windows need valid 0 <= Start <= End <= 1 and non-negative Hitbox Steps.";
                if (i > 0 && StartAt(i) <= EndAt(i - 1)) return "Block windows must be ordered with a gap between them.";
                foreach (int step in window.hitboxSteps.Distinct())
                    if (!steps.Add(step)) return "Each Hitbox Step must belong to only one Block window.";
            }
            return null;
        }
    }

    public bool Save(out string error)
    {
        error = null;
        if (!IsDirty) return true;
        if (HasConflict) error = "Block profile or skill binding changed elsewhere. Revert to reload before saving.";
        else if (ValidationError != null) error = ValidationError;
        else if (!AssetDatabase.IsMainAsset(skill)) error = "Save the Skill as its own asset before adding Block.";
        if (error != null) return false;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Save Skill Block Window");
        var target = DefensiveBlockSkillAuthoring.EnsureOwned(skill);
        Undo.RecordObject(target, "Save Block Window");
        target.windowStartNormalized = start;
        target.windowEndNormalized = end;
        if (windows.Count > 1 || target.HasMultipleWindowData || windows[0].onSuccess != DefensiveBlockOutcome.InterruptSkill)
        {
            target.windows = windows.Select(w => w.Copy()).ToArray();
            target.windows[0].startNormalized = start; target.windows[0].endNormalized = end;
        }
        target.hitboxSteps = (int[])windows[0].hitboxSteps.Clone();
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssetIfDirty(skill);
        Undo.CollapseUndoOperations(group);
        Reload();
        return true;
    }
}
#endif
