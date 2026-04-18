using System;
using UnityEngine;

[Serializable]
public struct KnockbackSettings
{
    public float distance;
    public float duration;
    public AnimationCurve progressCurve;
    public ImpactReactionKind reaction;
    public bool interruptActions;

    public KnockbackSettings(
        float distance,
        float duration,
        AnimationCurve progressCurve,
        ImpactReactionKind reaction = ImpactReactionKind.None,
        bool interruptActions = true)
    {
        this.distance = distance;
        this.duration = duration;
        this.progressCurve = progressCurve;
        this.reaction = reaction;
        this.interruptActions = interruptActions;
    }
}
