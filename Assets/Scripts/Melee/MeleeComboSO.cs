using System;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

[CreateAssetMenu(menuName = "Combat/Melee Combo", fileName = "MeleeComboSO")]
public sealed class MeleeComboSO : ScriptableObject
{
    public const CombatTimelineEventName HitStartEventName = CombatTimelineEventName.HitStart;
    public const CombatTimelineEventName HitEndEventName = CombatTimelineEventName.HitEnd;
    public static readonly string[] HitEventNames = { "HitStart", "HitEnd", "Vfx" };

    [Serializable]
    public struct Step
    {
        [SerializeField, HideInInspector]
        private string entryId;

        [SerializeField]
        private AnimationVfxTrack animationVfxTrack;

        [Tooltip("Animancer ClipTransition ของท่านี้")]
        [EventNames(typeof(MeleeComboSO), nameof(HitEventNames))]
        public ClipTransition clip;

        [Tooltip("0 = ใช้ความยาวคลิปจริง, >0 จะ speed-match ให้จบตามเวลานี้ (วินาที)")]
        [Min(0f)] public float duration;

        [Header("Windows (Normalized 0..1)")]
        [Tooltip("ช่วงที่อนุญาตให้ chain (buffer input) เช่น (0.35, 0.80). ถ้าเป็นท่าสุดท้ายตั้ง (0,0) ได้")]
        public Vector2 chainWindowN;

        [Tooltip("ถ้า true จะ clear buffered presses เมื่อพลาด chain window (กันกดรัวค้างแล้วหลุดยังไปต่อ)")]
        public bool dropBufferOnWindowExpire;

        [Header("Impact")]
        public bool applyKnockback;
        [Min(0f)] public float knockbackDistance;
        [Min(0f)] public float knockbackDuration;
        [Tooltip("Normalized knockback travel over time. X = time, Y = travel progress. Leave null for linear.")]
        public AnimationCurve knockbackProgressCurve;
        public ImpactReactionKind knockbackReaction;
        public bool knockbackInterruptsActions;

        [Header("Stagger")]
        [Min(0f)] public float staggerPower;

        public string EntryId => entryId;
        public AnimationVfxTrack AnimationVfxTrack => animationVfxTrack;

        internal void SetEntryId(string value)
        {
            entryId = value;
        }

        internal void SetAnimationVfxTrack(AnimationVfxTrack value)
        {
            animationVfxTrack = value;
        }

        public KnockbackSettings ToKnockbackSettings()
        {
            return new KnockbackSettings(
                knockbackDistance,
                knockbackDuration,
                knockbackProgressCurve,
                knockbackReaction,
                knockbackInterruptsActions);
        }
    }

    [Header("Combo Steps")]
    [SerializeField] private List<Step> steps = new();

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultChainStartN = 0.35f;
    [SerializeField, Range(0f, 1f)] private float defaultChainEndN   = 0.80f;

    public IReadOnlyList<Step> Steps => steps;
    public int Count => steps?.Count ?? 0;

    public bool TryGetStep(string entryId, out Step step, out int index)
    {
        if (steps != null && !string.IsNullOrWhiteSpace(entryId))
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (string.Equals(steps[i].EntryId, entryId, StringComparison.Ordinal))
                {
                    step = steps[i];
                    index = i;
                    return true;
                }
            }
        }

        step = default;
        index = -1;
        return false;
    }

    public bool ReplaceStepVfxTrack(string entryId, AnimationVfxTrack track)
    {
        if (!TryGetStep(entryId, out Step step, out int index))
            return false;

        step.SetAnimationVfxTrack(track ?? new AnimationVfxTrack());
        steps[index] = step;
        return true;
    }

    public bool SetStepChainWindow(string entryId, Vector2 chainWindowN)
    {
        if (!TryGetStep(entryId, out Step step, out int index))
            return false;

        step.chainWindowN = Clamp01Ordered(chainWindowN);
        steps[index] = step;
        return true;
    }

#if UNITY_EDITOR
    public bool EnsureUniqueStepEntryIds()
    {
        if (steps == null)
            return false;

        bool changed = false;
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < steps.Count; i++)
        {
            Step step = steps[i];
            string id = step.EntryId;
            if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(id))
            {
                do
                {
                    id = Guid.NewGuid().ToString("N");
                }
                while (!usedIds.Add(id));

                step.SetEntryId(id);
                steps[i] = step;
                changed = true;
            }
        }

        return changed;
    }
#endif

    public bool IsValid(out string reason)
    {
        if (steps == null || steps.Count == 0)
        {
            reason = "No steps.";
            return false;
        }

        if (steps[0].clip == null)
        {
            reason = "Step 0 clip is null.";
            return false;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.clip == null)
            {
                reason = $"Step {i} clip is null.";
                return false;
            }

            int hitStartCount = CountNamedEvents(step.clip, HitStartEventName);
            int hitEndCount = CountNamedEvents(step.clip, HitEndEventName);

            if (hitStartCount == 0 || hitEndCount == 0)
            {
                reason = $"Step {i} must define HitStart and HitEnd events in ClipTransition.Events.";
                return false;
            }

            if (hitStartCount != hitEndCount)
            {
                reason = $"Step {i} has unbalanced HitStart/HitEnd events ({hitStartCount}/{hitEndCount}).";
                return false;
            }

        }

        reason = "";
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (steps == null) return;

        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];

            // clamp & order
            s.chainWindowN = Clamp01Ordered(s.chainWindowN);
            s.knockbackDistance = Mathf.Max(0f, s.knockbackDistance);
            s.knockbackDuration = Mathf.Max(0f, s.knockbackDuration);
            s.staggerPower = Mathf.Max(0f, s.staggerPower);

            // if last step, allow chainWindow to be zeroed by user; otherwise keep as is
            steps[i] = s;
        }
    }
#endif

    public void ApplyDefaultsToStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Count) return;
        var s = steps[index];

        // เฉพาะถ้าไม่ใช่ step สุดท้าย ค่อยใส่ chain default
        if (index < steps.Count - 1)
            s.chainWindowN = new Vector2(defaultChainStartN, defaultChainEndN);

        steps[index] = s;
    }

    private static int CountNamedEvents(ClipTransition clip, CombatTimelineEventName eventName)
    {
        var events = clip != null ? clip.Events : null;
        if (events == null)
            return 0;

        StringReference animancerEventName = CombatTimelineEventNames.ToStringReference(eventName);
        if (animancerEventName == null)
            return 0;

        int count = 0;
        int index = -1;
        while ((index = events.IndexOf(animancerEventName, index + 1)) >= 0)
            count++;

        return count;
    }

    private static Vector2 Clamp01Ordered(Vector2 v)
    {
        float a = Mathf.Clamp01(v.x);
        float b = Mathf.Clamp01(v.y);
        if (b < a) (a, b) = (b, a);
        return new Vector2(a, b);
    }
}
