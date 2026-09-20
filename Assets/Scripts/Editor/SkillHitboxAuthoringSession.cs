#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using UnityEditor;
using UnityEngine;

// Only this transient object participates in authoring Undo. Assets are touched by Save alone.
public sealed class SkillHitboxAuthoringSession : ScriptableObject
{
    [Serializable]
    public sealed class PayloadDraft
    {
        public PrefabHitboxSkillPayloadDef source;
        public SkillHitboxLayoutData hitboxLayout = new();
        public List<PrefabHitboxSkillPayloadDef.HitboxStep> steps = new();
        public List<string> startIds = new();
        public string baseline;
    }

    [Serializable]
    public sealed class Marker
    {
        public string id;
        public CombatTimelineEventName name;
        public float time;
        public int originalIndex = -1;
    }

    public readonly struct Window
    {
        public readonly Marker Start, End;
        public Window(Marker start, Marker end) { Start = start; End = end; }
    }

    public SkillGemDefinition skill;
    public List<PayloadDraft> payloads = new();
    public List<Marker> markers = new();
    [SerializeField] string baselineClip;
    [SerializeField] string baselineDraft;
    public bool IsDirty => DraftJson() != baselineDraft;

    [Serializable] sealed class DraftSnapshot
    {
        public List<PayloadDraft> payloads;
        public List<Marker> markers;
    }
    string DraftJson() => JsonUtility.ToJson(new DraftSnapshot { payloads = payloads, markers = markers });

    public static SkillHitboxAuthoringSession Create(SkillGemDefinition skill)
    {
        var session = CreateInstance<SkillHitboxAuthoringSession>();
        session.hideFlags = HideFlags.HideAndDontSave;
        session.skill = skill;
        session.Reload();
        return session;
    }

    public void Reload()
    {
        Undo.ClearUndo(this);
        payloads.Clear(); markers.Clear();
        if (skill != null)
        {
            foreach (var source in FindPayloads(skill.payload))
            {
                var draft = JsonUtility.FromJson<PayloadDraft>(JsonUtility.ToJson(source));
                draft.source = source;
                draft.baseline = JsonUtility.ToJson(source);
                draft.startIds = new List<string>();
                payloads.Add(draft);
            }
            var events = skill.skillClip?.SerializedEvents;
            for (int i = 0; events?.NormalizedTimes != null && i < events.NormalizedTimes.Length - 1; i++)
            {
                var name = EventName(skill.skillClip, i);
                if (Owns(name)) markers.Add(new Marker { id = Guid.NewGuid().ToString("N"), name = name,
                    time = events.NormalizedTimes[i], originalIndex = i });
            }
        }
        foreach (var draft in payloads)
            draft.startIds = Starts(draft).Select(m => m.id).ToList();
        baselineClip = skill != null ? JsonUtility.ToJson(skill.skillClip) : string.Empty;
        baselineDraft = DraftJson();
    }

    public static List<PrefabHitboxSkillPayloadDef> FindPayloads(SkillPayloadDef root)
    {
        var result = new List<PrefabHitboxSkillPayloadDef>();
        var visited = new HashSet<SkillPayloadDef>();
        void Visit(SkillPayloadDef candidate)
        {
            if (candidate == null || !visited.Add(candidate)) return;
            if (candidate is PrefabHitboxSkillPayloadDef hitbox) result.Add(hitbox);
            if (candidate is CompositeSkillPayloadDef composite)
                foreach (var step in composite.Steps)
                    if (step is PayloadStep child) Visit(child.Payload);
        }
        Visit(root);
        return result;
    }

    static CombatTimelineEventName EventName(ClipTransition clip, int index)
    {
        var names = clip.SerializedEvents?.Names;
        string name = names != null && index < names.Length && names[index] != null
            ? names[index].name : clip.Events.GetName(index)?.String;
        return Enum.TryParse(name, true, out CombatTimelineEventName parsed) ? parsed : CombatTimelineEventName.None;
    }

    public bool Owns(CombatTimelineEventName name) => payloads.Any(p => p.source != null &&
        (p.source.HitboxStartEventName == name || p.source.HitboxEndEventName == name));
    IEnumerable<Marker> Ordered() => markers.OrderBy(m => m.time);
    IEnumerable<Marker> Starts(PayloadDraft draft) => Ordered().Where(m => m.name == draft.source.HitboxStartEventName);

    public List<Window> Windows(PayloadDraft draft, List<string> issues = null)
    {
        var result = new List<Window>();
        if (draft.source == null) return result;
        Marker active = null;
        foreach (var marker in Ordered())
        {
            if (marker.name == draft.source.HitboxStartEventName)
            {
                if (active != null) issues?.Add($"{draft.source.name}: overlapping HitStart at {marker.time:0.###}.");
                else active = marker;
            }
            else if (marker.name == draft.source.HitboxEndEventName)
            {
                if (active == null) issues?.Add($"{draft.source.name}: HitEnd without HitStart at {marker.time:0.###}.");
                else
                {
                    if (marker.time <= active.time) issues?.Add($"{draft.source.name}: hit window must have positive duration.");
                    result.Add(new Window(active, marker)); active = null;
                }
            }
        }
        if (active != null) issues?.Add($"{draft.source.name}: HitStart has no HitEnd.");
        return result;
    }

    public bool IsGroupActive(PayloadDraft draft, string key, float time)
    {
        var windows = Windows(draft);
        foreach (var window in windows)
        {
            int index = draft.startIds.IndexOf(window.Start.id);
            if (index >= 0 && index < draft.steps.Count && time >= window.Start.time && time < window.End.time &&
                draft.steps[index].GroupKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    public void Record(string label) => Undo.RegisterCompleteObjectUndo(this, label);

    // Stable start IDs keep step settings attached to their window when times cross/reorder.
    void ReconcileSteps()
    {
        foreach (var draft in payloads)
        {
            var old = new Dictionary<string, PrefabHitboxSkillPayloadDef.HitboxStep>();
            for (int i = 0; i < draft.startIds.Count && i < draft.steps.Count; i++) old[draft.startIds[i]] = draft.steps[i];
            var ids = Starts(draft).Select(m => m.id).ToList();
            draft.steps = ids.Select(id => old.TryGetValue(id, out var step) ? step : new PrefabHitboxSkillPayloadDef.HitboxStep()).ToList();
            draft.startIds = ids;
        }
    }

    public void MoveMarker(string id, float time)
    {
        var marker = markers.Find(m => m.id == id);
        if (marker == null) return;
        Record("Move Hitbox Window"); marker.time = Mathf.Clamp01(time); ReconcileSteps();
    }

    public void MoveWindow(string startId, string endId, float startTime, float duration)
    {
        var start = markers.Find(m => m.id == startId);
        var end = markers.Find(m => m.id == endId);
        if (start == null || end == null || start == end || float.IsNaN(startTime) ||
            float.IsInfinity(startTime) || duration <= 0f || duration > 1f || float.IsNaN(duration)) return;
        float next = Mathf.Clamp(startTime, 0f, 1f - duration);
        Record("Move Hitbox Window");
        // Move both endpoints before reconciling. Damage/knockback stay attached to the start ID.
        start.time = next; end.time = next + duration;
        ReconcileSteps();
    }

    public void AddWindow(PayloadDraft selected, float start, float end)
    {
        Record("Add Hitbox Window");
        var affected = new HashSet<PayloadDraft> { selected };
        bool changed;
        do
        {
            changed = false;
            var names = new HashSet<CombatTimelineEventName>(affected.SelectMany(p => new[] { p.source.HitboxStartEventName, p.source.HitboxEndEventName }));
            foreach (var p in payloads)
                if (names.Contains(p.source.HitboxStartEventName) || names.Contains(p.source.HitboxEndEventName)) changed |= affected.Add(p);
        } while (changed);
        foreach (var name in affected.Select(p => p.source.HitboxStartEventName).Distinct())
            markers.Add(new Marker { id = Guid.NewGuid().ToString("N"), name = name, time = Mathf.Clamp(start, 0, .998f) });
        foreach (var name in affected.Select(p => p.source.HitboxEndEventName).Distinct())
            markers.Add(new Marker { id = Guid.NewGuid().ToString("N"), name = name, time = Mathf.Clamp01(end) });
        ReconcileSteps();
    }

    public void RemoveWindow(Window window)
    {
        Record("Remove Shared Hitbox Window");
        var removed = new HashSet<string> { window.Start.id, window.End.id };
        bool changed;
        do
        {
            changed = false;
            foreach (var draft in payloads)
                foreach (var pair in Windows(draft))
                    if (removed.Contains(pair.Start.id) || removed.Contains(pair.End.id))
                    { changed |= removed.Add(pair.Start.id); changed |= removed.Add(pair.End.id); }
        } while (changed);
        markers.RemoveAll(m => removed.Contains(m.id)); ReconcileSteps();
    }

    HashSet<string> ConnectedWindowMarkers(Window window)
    {
        var ids = new HashSet<string> { window.Start.id, window.End.id };
        bool changed;
        do
        {
            changed = false;
            foreach (var draft in payloads)
                foreach (var pair in Windows(draft))
                    if (ids.Contains(pair.Start.id) || ids.Contains(pair.End.id))
                    { changed |= ids.Add(pair.Start.id); changed |= ids.Add(pair.End.id); }
        } while (changed);
        return ids;
    }

    public bool TryDuplicatePlacement(Window window, out float start)
    {
        start = 0;
        if (window.Start == null || window.End == null || !markers.Contains(window.Start) || !markers.Contains(window.End)) return false;
        var ids = ConnectedWindowMarkers(window);
        var originals = markers.Where(m => ids.Contains(m.id)).ToList();
        float first = originals.Min(m => m.time), last = originals.Max(m => m.time), duration = last - first;
        if (!float.IsFinite(duration) || duration <= 0 || duration > 1) return false;
        var affected = payloads.Where(p => p.startIds.Any(ids.Contains)).ToList();
        var issues = new List<string>();
        var occupied = affected.SelectMany(p => Windows(p, issues)).ToList();
        if (issues.Count > 0) return false;
        // Prefer the next free gap, then wrap to an earlier gap. Never create an overlapping copy.
        var candidates = occupied.Select(w => w.End.time + .01f).Append(0f).Distinct()
            .OrderBy(t => t < last ? 1 : 0).ThenBy(t => t);
        foreach (float candidate in candidates)
        {
            if (candidate + duration > 1f || occupied.Any(w => candidate < w.End.time && candidate + duration > w.Start.time)) continue;
            start = candidate; return true;
        }
        return false;
    }

    public string DuplicateWindow(Window window)
    {
        if (!TryDuplicatePlacement(window, out float start)) return null;
        var ids = ConnectedWindowMarkers(window);
        var originals = markers.Where(m => ids.Contains(m.id)).ToArray();
        float offset = start - originals.Min(m => m.time);
        var copies = originals.ToDictionary(m => m.id, m => new Marker
        { id = Guid.NewGuid().ToString("N"), name = m.name, time = m.time + offset });
        Record("Duplicate Shared Hitbox Window");
        // Capture each payload's own settings by start ID before sorting changes any step index.
        var settings = payloads.ToDictionary(p => p, p => p.startIds
            .Select((id, index) => (id, index)).Where(x => copies.ContainsKey(x.id) && x.index < p.steps.Count)
            .ToDictionary(x => copies[x.id].id, x => JsonUtility.FromJson<PrefabHitboxSkillPayloadDef.HitboxStep>(JsonUtility.ToJson(p.steps[x.index]))));
        markers.AddRange(copies.Values);
        ReconcileSteps();
        foreach (var draft in payloads)
            foreach (var pair in settings[draft]) draft.steps[draft.startIds.IndexOf(pair.Key)] = pair.Value;
        return copies[window.Start.id].id;
    }

    public void RemoveMarker(string id)
    {
        Record("Remove Incomplete Hitbox Marker");
        markers.RemoveAll(m => m.id == id);
        ReconcileSteps();
    }

    public string AffectedBy(Window window)
    {
        var ids = ConnectedWindowMarkers(window);
        return string.Join(", ", payloads.Where(p => p.startIds.Any(ids.Contains)).Select(p => p.source.name));
    }

    public void RenameGroup(PayloadDraft draft, int index, string name)
    {
        Record("Rename Hitbox Group");
        string old = draft.hitboxLayout.Groups[index].GroupKey;
        draft.hitboxLayout.Groups[index].GroupKey = name;
        var serialized = new SerializedObject(this);
        var steps = serialized.FindProperty("payloads").GetArrayElementAtIndex(payloads.IndexOf(draft)).FindPropertyRelative("steps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            var keys = steps.GetArrayElementAtIndex(i).FindPropertyRelative("groupKeys");
            for (int k = 0; k < keys.arraySize; k++)
                if (string.Equals(keys.GetArrayElementAtIndex(k).stringValue, old, StringComparison.OrdinalIgnoreCase))
                    keys.GetArrayElementAtIndex(k).stringValue = name;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public bool HasConflict() => skill == null || JsonUtility.ToJson(skill.skillClip) != baselineClip ||
        !FindPayloads(skill.payload).SequenceEqual(payloads.Select(p => p.source)) ||
        payloads.Any(p => p.source == null || JsonUtility.ToJson(p.source) != p.baseline);

    public List<string> Validate(Func<PayloadDraft, SkillHitboxLayoutData.HitBoxGroupData, bool> anchorExists = null)
    {
        var issues = new List<string>();
        if (HasConflict()) issues.Add("Source changed outside this tool. Revert/reload the draft before saving.");
        if (payloads.Count == 0) issues.Add("This skill has no hitbox payload.");
        if (skill == null || skill.skillClip == null || !skill.skillClip.IsValid) issues.Add("A valid skill animation is required.");
        foreach (var marker in markers)
            if (!float.IsFinite(marker.time) || marker.time < 0 || marker.time > 1) issues.Add("Hitbox marker time must be within [0, 1].");
        foreach (var draft in payloads)
        {
            if (draft.source == null) continue;
            Windows(draft, issues);
            if (draft.steps.Count != Starts(draft).Count() || draft.steps.Count != Windows(draft).Count)
                issues.Add($"{draft.source.name}: each complete hit window needs one step.");
            var probe = Instantiate(draft.source);
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft), probe);
                probe.CollectValidationIssues(issues);
            }
            finally { DestroyImmediate(probe); }
            foreach (var group in draft.hitboxLayout.Groups)
            {
                if (group == null) continue;
                if (anchorExists != null && !anchorExists(draft, group))
                    issues.Add($"{draft.source.name}/{group.GroupKey}: missing {group.Anchor} anchor '{group.AnchorPath}'.");
                foreach (var shape in group.Shapes)
                {
                    if (shape == null) continue;
                    string label = $"{draft.source.name}/{group.GroupKey}/{shape.ShapeName}";
                    if (!Finite(shape.LocalPosition) || !Finite(shape.LocalEulerAngles) || !Finite(shape.LocalScale) ||
                        !Finite(shape.Center) || !Finite(shape.Size) || !float.IsFinite(shape.Radius) || !float.IsFinite(shape.Height))
                        issues.Add(label + ": shape values must be finite.");
                    if (Mathf.Abs(shape.LocalScale.x) < .0001f || Mathf.Abs(shape.LocalScale.y) < .0001f || Mathf.Abs(shape.LocalScale.z) < .0001f)
                        issues.Add(label + ": shape scale must be non-zero on every axis.");
                    if (!Enum.IsDefined(typeof(SkillHitboxLayoutData.HitBoxType), shape.Type)) issues.Add(label + ": invalid shape type.");
                }
            }
        }
        return issues;
    }

    static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    public bool Save(out string error, Func<PayloadDraft, SkillHitboxLayoutData.HitBoxGroupData, bool> anchorExists = null)
    {
        var issues = Validate(anchorExists);
        string path = skill != null ? AssetDatabase.GetAssetPath(skill) : null;
        if (string.IsNullOrEmpty(path) || payloads.Any(p => AssetDatabase.GetAssetPath(p.source) != path))
            issues.Add("Save requires hitbox payloads embedded in the selected skill asset.");
        if (issues.Count > 0) { error = string.Join("\n", issues); return false; }
        // Name assets must already exist; opening or validating the tool never creates assets.
        var names = new Dictionary<CombatTimelineEventName, StringAsset>();
        foreach (var marker in markers)
        {
            var asset = StringAsset.Find(CombatTimelineEventNames.ToStringReference(marker.name), out _);
            if (asset == null) { error = $"Missing timeline event asset '{marker.name}'. Create it in the existing event authoring tool."; return false; }
            names[marker.name] = asset;
        }
        var events = skill.skillClip.SerializedEvents ?? new AnimancerEvent.Sequence.Serializable();
        var callbacks = events.Callbacks != null ? events.Callbacks.ToArray() : Array.Empty<IInvokable>();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Save Hitbox Authoring");
        Undo.RecordObjects(payloads.Select(p => (UnityEngine.Object)p.source).Append(skill).ToArray(), "Save Hitbox Authoring");
        for (int i = (events.NormalizedTimes?.Length ?? 1) - 2; i >= 0; i--)
            if (Owns(EventName(skill.skillClip, i))) events.RemoveEvent(i);
        foreach (var marker in Ordered())
            events.AddEvent(marker.time, marker.originalIndex >= 0 && marker.originalIndex < callbacks.Length ? callbacks[marker.originalIndex] : null, names[marker.name]);
        events.Events = null;
        skill.skillClip.SerializedEvents = events;
        foreach (var draft in payloads)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft), draft.source);
            EditorUtility.SetDirty(draft.source);
        }
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssetIfDirty(skill);
        Undo.CollapseUndoOperations(undo);
        Reload(); error = null; return true;
    }
}
#endif
