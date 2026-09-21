#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// A transient draft: VFX saves and animation preview never write these values to the profile.
public sealed class DefensiveBlockTimelineSession : ScriptableObject
{
    public SkillGemDefinition skill;
    public SkillDefensiveBlockSettings profile => skill != null ? skill.defensiveBlock : null;
    public DefensiveBlockMode mode;
    public float timedApproachSeconds;
    public float approachStandOff;
    public bool blockEnabled;
    [SerializeField] bool savedEnabled;
    [SerializeField] DefensiveBlockMode savedMode;
    [SerializeField] float savedDuration;
    [SerializeField] float savedStandOff;
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

    void OnEnable()
    {
        // Preserve an unsaved single-window draft across the editor code upgrade.
        if (windows != null && windows.Count > 0) return;
        windows = new List<DefensiveBlockWindow> { new DefensiveBlockWindow { startNormalized = start, endNormalized = end,
            hitboxSteps = skill != null && skill.BlockSettings != null && skill.BlockSettings.hitboxSteps != null ? (int[])skill.BlockSettings.hitboxSteps.Clone() : new[] { 0 } } };
        savedWindows = WindowsSnapshot;
    }

    public bool IsEnabled => blockEnabled;
    public bool IsDirty => blockEnabled != savedEnabled || mode != savedMode ||
        !timedApproachSeconds.Equals(savedDuration) || !approachStandOff.Equals(savedStandOff) ||
        !start.Equals(savedStart) || !end.Equals(savedEnd) || WindowsSnapshot != savedWindows;
    string SourceSnapshot => skill != null && skill.BlockSettings != null ? JsonUtility.ToJson(skill.BlockSettings) : null;
    public bool HasConflict => skill == null || SourceSnapshot != sourceSnapshot;
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
        blockEnabled = savedEnabled = profile != null;
        var settings = skill != null ? skill.BlockSettings : null;
        mode = savedMode = settings != null ? settings.mode : DefensiveBlockMode.Contact;
        timedApproachSeconds = savedDuration = settings != null ? settings.timedApproachSeconds : .22f;
        approachStandOff = savedStandOff = settings != null ? settings.approachStandOff : 1.6f;
        start = savedStart = settings != null ? settings.windowStartNormalized : 0f;
        end = savedEnd = settings != null ? settings.windowEndNormalized : 0.62f;
        windows = settings != null && settings.HasMultipleWindowData
            ? settings.windows.Select(w => w != null ? w.Copy() : new DefensiveBlockWindow()).ToList()
            : new List<DefensiveBlockWindow> { new DefensiveBlockWindow { startNormalized = start, endNormalized = end,
                hitboxSteps = skill != null && skill.BlockSettings != null && skill.BlockSettings.hitboxSteps != null ? (int[])skill.BlockSettings.hitboxSteps.Clone() : new[] { 0 } } };
        start = savedStart = windows[0].startNormalized;
        end = savedEnd = windows[0].endNormalized;
        savedWindows = WindowsSnapshot;
        selectedWindow = Mathf.Clamp(selectedWindow, 0, windows.Count - 1);
        sourceSnapshot = SourceSnapshot;
    }

    public void AddBlock()
    {
        if (IsEnabled) return;
        Undo.RecordObject(this, "Add Block Window");
        blockEnabled = true;
    }

    public void DisableBlock()
    {
        Undo.RecordObject(this, "Disable Block");
        blockEnabled = false;
    }

    public void SetMode(DefensiveBlockMode value)
    {
        Undo.RecordObject(this, "Edit Block Mode");
        mode = value;
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
            if (!IsEnabled) return null;
            if (!System.Enum.IsDefined(typeof(DefensiveBlockMode), mode)) return "Choose a valid Block Mode.";
            if (mode == DefensiveBlockMode.TimedApproach && (!(timedApproachSeconds > 0f) || float.IsInfinity(timedApproachSeconds) ||
                !(approachStandOff > 0f) || float.IsInfinity(approachStandOff)))
                return "Timed Approach needs a finite duration and stand-off distance greater than zero.";
            if (windows == null || windows.Count == 0) return "Block needs at least one window.";
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

    public bool ValidateSave(out string error)
    {
        error = null;
        if (!IsDirty) return true;
        if (HasConflict) error = "Skill Block settings changed elsewhere. Revert to reload before saving.";
        else if (ValidationError != null) error = ValidationError;
        else if (!AssetDatabase.IsMainAsset(skill)) error = "Save the Skill as its own asset before adding Block.";
        return error == null;
    }

    public bool Save(out string error)
    {
        if (!ValidateSave(out error)) return false;
        if (!IsDirty) return true;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Save Skill Block Window");
        var target = skill.BlockSettings != null ? skill.BlockSettings.Copy() : new SkillDefensiveBlockSettings();
        Undo.RecordObject(skill, "Save Block Settings");
        target.enabled = blockEnabled;
        target.mode = mode;
        target.timedApproachSeconds = timedApproachSeconds;
        target.approachStandOff = approachStandOff;
        target.windowStartNormalized = start;
        target.windowEndNormalized = end;
        if (windows.Count > 1 || target.HasMultipleWindowData || windows[0].onSuccess != DefensiveBlockOutcome.InterruptSkill)
        {
            target.windows = windows.Select(w => w.Copy()).ToArray();
            target.windows[0].startNormalized = start; target.windows[0].endNormalized = end;
        }
        target.hitboxSteps = (int[])windows[0].hitboxSteps.Clone();
        skill.defensiveBlock = target;
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssetIfDirty(skill);
        Undo.CollapseUndoOperations(group);
        Reload();
        return true;
    }
}
#endif
