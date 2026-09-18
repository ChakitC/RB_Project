using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    Locomotion_Block blockState;
    int blockRequestId;
    BlockAnimationProfile blockProfile;
    public BlockAnimationPhase BlockPhase => blockRequestId > 0 && blockState != null ? blockState.Phase : BlockAnimationPhase.None;
    public bool IsBlockPlaybackActive => blockRequestId > 0;

    internal bool TryBeginBlock(int requestId, BlockAnimationProfile profile)
    {
        if (requestId <= 0 || profile == null || profile.beginClip == null || profile.impactClip == null ||
            !TryInitialize() || !CanStartAnimation(CharacterAnimationMode.Block, CharacterAnimationTransitionReason.NormalCommand))
            return false;
        blockState ??= new Locomotion_Block(this);
        blockRequestId = requestId;
        blockProfile = profile;
        StopReloadAction();
        if (TrySetLocomotionState(blockState)) return true;
        blockRequestId = 0;
        return false;
    }

    internal bool TryBlockImpact(int requestId, float minimumDuration)
    {
        if (requestId != blockRequestId ||
            (BlockPhase != BlockAnimationPhase.Begin && BlockPhase != BlockAnimationPhase.Loop)) return false;
        blockState.Impact(minimumDuration);
        return true;
    }

    internal void EndBlock(int requestId, bool immediate)
    {
        if (requestId <= 0 || requestId != blockRequestId) return;
        if (immediate) TrySetLocomotionState(IsDowned ? crawlState : locomotion);
        else blockState.Exit();
    }

    private sealed class Locomotion_Block : LocomotionState
    {
        readonly CharacterAnimBrain owner;
        AnimancerState clip;
        float elapsed;
        float impactDuration;
        bool previousRootMotion;
        bool completed;
        public BlockAnimationPhase Phase { get; private set; }
        public Locomotion_Block(CharacterAnimBrain owner) { this.owner = owner; }
        public override void OnEnterState()
        {
            completed = false;
            previousRootMotion = owner.EnterExclusiveLocomotion(false, false);
            owner.ClearRootMotionPolicy();
            Phase = BlockAnimationPhase.Begin;
            elapsed = 0f;
            clip = owner.LocoLayer.Play(owner.blockProfile.beginClip, owner.blockProfile.fadeSeconds);
            clip.NormalizedTime = owner.blockProfile.beginStartNormalized;
            clip.Speed = 0f;
            clip.SharedEvents = null;
            owner.EmitPlaybackSignal(PlaybackKind.Block, PlaybackPhase.Started, owner.blockRequestId);
        }
        public override void Update()
        {
            elapsed += owner.AnimationDeltaTime;
            var profile = owner.blockProfile;
            if (Phase == BlockAnimationPhase.Begin)
            {
                clip.NormalizedTime = Mathf.Lerp(profile.beginStartNormalized, profile.guardPoseNormalized,
                    Mathf.Clamp01(elapsed / profile.beginSeconds));
                if (elapsed >= profile.beginSeconds) Phase = BlockAnimationPhase.Loop;
            }
            else if (Phase == BlockAnimationPhase.Impact)
            {
                clip.NormalizedTime = Mathf.Clamp01(elapsed / impactDuration);
                if (elapsed >= impactDuration) Exit();
            }
            else if (Phase == BlockAnimationPhase.Exit && elapsed >= profile.exitSeconds)
            {
                completed = true;
                owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
            }
        }
        public void Impact(float minimumDuration)
        {
            Phase = BlockAnimationPhase.Impact;
            elapsed = 0f;
            impactDuration = Mathf.Max(0.01f, owner.blockProfile.impactSeconds, minimumDuration);
            clip = owner.LocoLayer.Play(owner.blockProfile.impactClip, owner.blockProfile.fadeSeconds);
            clip.Time = 0f;
            clip.Speed = 0f;
            clip.SharedEvents = null;
        }
        public void Exit() { Phase = BlockAnimationPhase.Exit; elapsed = 0f; }
        public override void OnExitState()
        {
            if (clip != null) { clip.Speed = 1f; clip.SharedEvents = null; }
            owner.ExitExclusiveLocomotion(previousRootMotion);
            owner.ClearRootMotionPolicy();
            int request = owner.blockRequestId;
            owner.blockRequestId = 0;
            Phase = BlockAnimationPhase.None;
            if (request > 0) owner.EmitPlaybackSignal(PlaybackKind.Block,
                completed ? PlaybackPhase.Completed : PlaybackPhase.Interrupted, request);
        }
    }
}
