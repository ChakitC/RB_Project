using System;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

[CreateAssetMenu(menuName = "Combat/Melee Combo", fileName = "MeleeComboSO")]
public sealed class MeleeComboSO : ScriptableObject
{
    [Serializable]
    public struct Step
    {
        [Tooltip("Animancer ClipTransition ของท่านี้")]
        public ClipTransition clip;

        [Tooltip("0 = ใช้ความยาวคลิปจริง, >0 จะ speed-match ให้จบตามเวลานี้ (วินาที)")]
        [Min(0f)] public float duration;

        [Header("Windows (Normalized 0..1)")]
        [Tooltip(" (X) ช่วงเปิด/ (Y) ปิด hitbox เช่น (0.25, 0.45)")]
        public Vector2 hitWindowN;

        [Tooltip("ช่วงที่อนุญาตให้ chain (buffer input) เช่น (0.35, 0.80). ถ้าเป็นท่าสุดท้ายตั้ง (0,0) ได้")]
        public Vector2 chainWindowN;

        [Tooltip("ถ้า true จะ clear buffered presses เมื่อพลาด chain window (กันกดรัวค้างแล้วหลุดยังไปต่อ)")]
        public bool dropBufferOnWindowExpire;

        [Header("Impact")]
        public bool applyKnockback;
        [Min(0f)] public float knockbackDistance;
        [Min(0f)] public float knockbackDuration;
        public ImpactReactionKind knockbackReaction;
        public bool knockbackInterruptsActions;
    }

    [Header("Combo Steps")]
    [SerializeField] private List<Step> steps = new();

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultHitStartN = 0.25f;
    [SerializeField, Range(0f, 1f)] private float defaultHitEndN   = 0.45f;
    [SerializeField, Range(0f, 1f)] private float defaultChainStartN = 0.35f;
    [SerializeField, Range(0f, 1f)] private float defaultChainEndN   = 0.80f;

    public IReadOnlyList<Step> Steps => steps;
    public int Count => steps?.Count ?? 0;

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
            s.hitWindowN = Clamp01Ordered(s.hitWindowN);
            s.chainWindowN = Clamp01Ordered(s.chainWindowN);
            s.knockbackDistance = Mathf.Max(0f, s.knockbackDistance);
            s.knockbackDuration = Mathf.Max(0f, s.knockbackDuration);

            // if last step, allow chainWindow to be zeroed by user; otherwise keep as is
            steps[i] = s;
        }
    }
#endif

    public void ApplyDefaultsToStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Count) return;
        var s = steps[index];

        s.hitWindowN = new Vector2(defaultHitStartN, defaultHitEndN);

        // เฉพาะถ้าไม่ใช่ step สุดท้าย ค่อยใส่ chain default
        if (index < steps.Count - 1)
            s.chainWindowN = new Vector2(defaultChainStartN, defaultChainEndN);

        steps[index] = s;
    }

    private static Vector2 Clamp01Ordered(Vector2 v)
    {
        float a = Mathf.Clamp01(v.x);
        float b = Mathf.Clamp01(v.y);
        if (b < a) (a, b) = (b, a);
        return new Vector2(a, b);
    }
}
