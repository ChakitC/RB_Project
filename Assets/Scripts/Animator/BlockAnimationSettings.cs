using System;
using UnityEngine;

// Inline character animation data; owned by CharacterAnimProfileSO.
[Serializable]
public sealed class BlockAnimationSettings
{
    public AnimationClip beginClip;
    [Tooltip("Optional guard loop. Empty holds the Begin guard pose.")]
    public AnimationClip loopClip;
    public AnimationClip impactClip;
    [Tooltip("Optional lowering-guard clip, played over exitSeconds.")]
    public AnimationClip exitClip;
    [Tooltip("Optional struggle loop. Empty keeps the held guard pose.")]
    public AnimationClip cooperativeStruggleClip;
    [Range(0f, 1f)] public float beginStartNormalized;
    [Range(0f, 1f)] public float guardPoseNormalized = .35f;
    [Range(0f, 1f)] public float impactStartNormalized;
    [Range(0f, 1f)] public float impactEndNormalized = 1f;
    [Min(.01f)] public float beginSeconds = .12f;
    [Min(.01f)] public float impactSeconds = .35f;
    [Min(.01f)] public float exitSeconds = .12f;
    [Min(0f)] public float fadeSeconds = .06f;
    public bool IsConfigured => beginClip != null && impactClip != null;
    public bool HasClips => beginClip != null || loopClip != null || impactClip != null || exitClip != null || cooperativeStruggleClip != null;
    public BlockAnimationSettings Copy() => (BlockAnimationSettings)MemberwiseClone();

    public static BlockAnimationSettings FromLegacy(BlockAnimationProfile source) => source == null ? null : new BlockAnimationSettings
    {
        beginClip = source.beginClip, impactClip = source.impactClip, cooperativeStruggleClip = source.cooperativeStruggleClip,
        beginStartNormalized = source.beginStartNormalized, guardPoseNormalized = source.guardPoseNormalized,
        impactStartNormalized = source.impactStartNormalized, impactEndNormalized = source.impactEndNormalized,
        beginSeconds = source.beginSeconds, impactSeconds = source.impactSeconds,
        exitSeconds = source.exitSeconds, fadeSeconds = source.fadeSeconds
    };
}
