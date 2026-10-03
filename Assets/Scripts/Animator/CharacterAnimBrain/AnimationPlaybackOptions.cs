using Animancer;

// Runtime choices for one clip. Unspecified overrides retain transition authoring.
internal struct AnimationPlaybackOptions
{
    internal enum Layer { Locomotion, UpperAction, FullBodyAction }
    internal ClipTransition Transition;
    internal Layer TargetLayer;
    internal PairOffsetUpperAction PairAction;
    internal PlaybackChannel Channel;
    internal AnimationVfxTrack Vfx;
    internal float? FadeDuration, NormalizedStart, Duration, Speed;
    internal FadeMode FadeMode;
    internal bool InvokeAuthoredEnd, HoldLastFrame;
}
