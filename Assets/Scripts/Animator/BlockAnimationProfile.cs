using UnityEngine;

// Legacy serialized asset retained for migration and existing references.
// Author new Block animation in CharacterAnimProfileSO.block.
public sealed class BlockAnimationProfile : ScriptableObject
{
    [Header("Legacy — edit Character Animation Profile / Block instead")]
    public AnimationClip beginClip;
    public AnimationClip impactClip;
    [Tooltip("Optional Cooperative Block struggle loop. Empty keeps the existing held guard pose.")]
    public AnimationClip cooperativeStruggleClip;
    [Tooltip("First sampled frame of Begin. Skip any approach/jump baked into the clip.")]
    [Range(0f, 1f)] public float beginStartNormalized;
    [Range(0f, 1f)] public float guardPoseNormalized = 0.35f;
    [Tooltip("Recoil segment to sample. Exclude jumps or approach motion elsewhere in the clip.")]
    [Range(0f, 1f)] public float impactStartNormalized;
    [Range(0f, 1f)] public float impactEndNormalized = 1f;
    [Min(0.01f)] public float beginSeconds = 0.12f;
    [Min(0.01f)] public float impactSeconds = 0.35f;
    [Min(0.01f)] public float exitSeconds = 0.12f;
    [Min(0f)] public float fadeSeconds = 0.06f;
}
