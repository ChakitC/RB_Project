using UnityEngine;

[CreateAssetMenu(menuName = "Game/Characters/Block Animation Profile")]
public sealed class BlockAnimationProfile : ScriptableObject
{
    public AnimationClip beginClip;
    public AnimationClip impactClip;
    [Tooltip("First sampled frame of Begin. Skip any approach/jump baked into the clip.")]
    [Range(0f, 1f)] public float beginStartNormalized;
    [Range(0f, 1f)] public float guardPoseNormalized = 0.35f;
    [Min(0.01f)] public float beginSeconds = 0.12f;
    [Min(0.01f)] public float impactSeconds = 0.35f;
    [Min(0.01f)] public float exitSeconds = 0.12f;
    [Min(0f)] public float fadeSeconds = 0.06f;
}
