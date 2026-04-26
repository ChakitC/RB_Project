using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Status Effect VFX Profile", menuName = "Combat/Status Effect VFX Profile")]
public sealed class StatusEffectVfxProfile : ScriptableObject
{
    [Header("Cues")]
    public StatusEffectVfxCue applyBurst = new StatusEffectVfxCue();
    public StatusEffectVfxCue activeLoop = new StatusEffectVfxCue { parentToAnchor = true };
    public StatusEffectVfxCue refreshBurst = new StatusEffectVfxCue();
    public StatusEffectVfxCue stackBurst = new StatusEffectVfxCue();
    public StatusEffectVfxCue removeBurst = new StatusEffectVfxCue();
    public StatusEffectVfxCue tickBurst = new StatusEffectVfxCue();

    [Header("Loop Stack Scaling")]
    public bool scaleLoopByStacks = true;
    [Min(0f)] public float loopScalePerAdditionalStack = 0.1f;
    [Min(0.01f)] public float loopMaxScaleMultiplier = 2f;

    public float GetLoopScaleMultiplier(int stacks)
    {
        if (!scaleLoopByStacks)
            return 1f;

        int extraStacks = Mathf.Max(0, stacks - 1);
        float multiplier = 1f + extraStacks * Mathf.Max(0f, loopScalePerAdditionalStack);
        return Mathf.Clamp(multiplier, 0f, Mathf.Max(0.01f, loopMaxScaleMultiplier));
    }
}

public enum StatusEffectVfxAnchor
{
    Root,
    Center,
    Feet,
    Head,
    CustomName
}

[Serializable]
public sealed class StatusEffectVfxCue
{
    public GameObject prefab;
    public StatusEffectVfxAnchor anchor = StatusEffectVfxAnchor.Center;
    public string customAnchorName;
    public Vector3 localOffset;
    public Vector3 rotationEuler;
    [Min(0f)] public float scale = 1f;
    [Min(0f)] public float extraLife;
    [Min(0f)] public float minInterval;
    public bool parentToAnchor;
    public bool useAnchorRotation;

    public bool IsEnabled => prefab != null;
}
