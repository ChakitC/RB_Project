using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    Locomotion_Block blockState;
    int blockRequestId;
    BlockAnimationSettings blockProfile;
    public BlockAnimationSettings BlockAnimation => TryGetAnimProfile(out var profile) ? profile.block : null;
    public BlockAnimationPhase BlockPhase => blockRequestId > 0 && blockState != null ? blockState.Phase : BlockAnimationPhase.None;
    public bool IsBlockPlaybackActive => blockRequestId > 0;
    public float BlockImpactProgress => BlockPhase == BlockAnimationPhase.Impact ? blockState.ImpactProgress : 0f;
    // Duration of the accepted playback, including the recoil motor's minimum Impact time.
    internal float BlockRecoveryDuration => IsBlockPlaybackActive && blockState != null
        ? blockState.RecoveryDuration : 0f;

    internal bool TryBeginBlock(int requestId, BlockAnimationProfile profile)
        => TryBeginBlockPlayback(requestId, BlockAnimationSettings.FromLegacy(profile));

    internal bool TryBeginBlock(int requestId) => TryBeginBlockPlayback(requestId, BlockAnimation);

    bool TryBeginBlockPlayback(int requestId, BlockAnimationSettings profile)
    {
        if (requestId <= 0 || profile == null || profile.beginClip == null || profile.impactClip == null ||
            !TryInitialize() || !CanStartAnimation(CharacterAnimationMode.Block, CharacterAnimationTransitionReason.NormalCommand))
            return false;
        blockState ??= new Locomotion_Block(this);
        blockRequestId = requestId;
        blockProfile = profile.Copy();
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

    internal void BeginCooperativeStruggle(int requestId)
    {
        if (requestId == blockRequestId && (BlockPhase == BlockAnimationPhase.Begin || BlockPhase == BlockAnimationPhase.Loop))
            blockState.Struggle();
    }

    private sealed class Locomotion_Block : LocomotionState
    {
        readonly CharacterAnimBrain owner;
        AnimancerState clip;
        float elapsed;
        float impactDuration;
        bool previousRootMotion;
        bool completed;
        bool struggling;
        public BlockAnimationPhase Phase { get; private set; }
        public float ImpactProgress => Mathf.Clamp01(elapsed / Mathf.Max(.01f, impactDuration));
        public float RecoveryDuration => impactDuration + Mathf.Max(0f, owner.blockProfile.exitSeconds);
        public Locomotion_Block(CharacterAnimBrain owner) { this.owner = owner; }
        public override void OnEnterState()
        {
            completed = false;
            struggling = false;
            previousRootMotion = owner.EnterExclusiveLocomotion(false, false);
            owner.ClearRootMotionPolicy();
            Phase = BlockAnimationPhase.Begin;
            elapsed = 0f;
            clip = owner.presentation.PlayLocomotionTransition(owner.blockProfile.beginClip, owner.blockProfile.fadeSeconds);
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
                if (elapsed >= profile.beginSeconds)
                {
                    Phase = BlockAnimationPhase.Loop;
                    elapsed = 0f;
                    if (profile.loopClip != null) PlayLoop(profile.loopClip);
                }
            }
            else if (Phase == BlockAnimationPhase.Loop)
            {
                var loop = struggling ? profile.cooperativeStruggleClip : profile.loopClip;
                if (loop != null)
                    clip.NormalizedTime = Mathf.Repeat(elapsed / Mathf.Max(.01f, loop.length), 1f);
            }
            else if (Phase == BlockAnimationPhase.Impact)
            {
                clip.NormalizedTime = Mathf.Lerp(profile.impactStartNormalized,
                    profile.impactEndNormalized, Mathf.Clamp01(elapsed / impactDuration));
                if (elapsed >= impactDuration) Exit();
            }
            else if (Phase == BlockAnimationPhase.Exit)
            {
                if (profile.exitClip != null)
                    clip.NormalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(.01f, profile.exitSeconds));
                if (elapsed >= profile.exitSeconds)
                {
                    completed = true;
                    owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                }
            }
        }
        public void Struggle()
        {
            if (owner.blockProfile.cooperativeStruggleClip == null) return;
            Phase = BlockAnimationPhase.Loop; elapsed = 0f; struggling = true;
            PlayLoop(owner.blockProfile.cooperativeStruggleClip);
        }
        void PlayLoop(AnimationClip animation)
        {
            clip = owner.presentation.PlayLocomotionTransition(animation, owner.blockProfile.fadeSeconds);
            clip.NormalizedTime = 0f; clip.Speed = 0f; clip.SharedEvents = null;
        }
        public void Impact(float minimumDuration)
        {
            Phase = BlockAnimationPhase.Impact;
            elapsed = 0f;
            impactDuration = Mathf.Max(0.01f, owner.blockProfile.impactSeconds, minimumDuration);
            clip = owner.presentation.PlayLocomotionTransition(owner.blockProfile.impactClip, owner.blockProfile.fadeSeconds);
            clip.NormalizedTime = owner.blockProfile.impactStartNormalized;
            clip.Speed = 0f;
            clip.SharedEvents = null;
        }
        public void Exit()
        {
            if (Phase == BlockAnimationPhase.Exit) return;
            Phase = BlockAnimationPhase.Exit;
            elapsed = 0f;
            if (owner.blockProfile.exitClip != null) PlayLoop(owner.blockProfile.exitClip);
        }
        public override void OnExitState()
        {
            // Keep the sampled guard/recoil pose while the next state fades in.
            if (clip != null && UnityEngine.Playables.PlayableExtensions.IsValid(clip.Playable)) { clip.Speed = 0f; clip.SharedEvents = null; }
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
