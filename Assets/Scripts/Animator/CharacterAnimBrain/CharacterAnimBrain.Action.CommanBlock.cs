using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    Action_CommanBlock commanBlockState;

    public bool TryPlayCommanBlock()
    {
        if (!TryInitialize() || !TryGetAnimProfile(out var profile) ||
            profile.commanBlock == null || !profile.commanBlock.IsValid ||
            !CharacterAnimationTransitionPolicy.AllowsAction(CurrentAnimationMode, PairOffsetUpperAction.None, true)) return false;

        commanBlockState ??= new Action_CommanBlock(this);
        return actionSM.TryResetState(commanBlockState);
    }

    void StopCommanBlock()
    {
        if (_initialized && animancer != null && animancer.Animator != null &&
            actionSM.CurrentState == commanBlockState)
            actionSM.ForceSetState(empty);
        else
            commanBlockState?.Dispose();
    }

    // The defender owns Block gameplay; this state owns the Player's temporary control lock.
    private sealed class Action_CommanBlock : SingleClipActionState
    {

        public Action_CommanBlock(CharacterAnimBrain owner) : base(owner) { }

        protected override void EnterPlayback()
        {
            owner.sessions.AcquireCommandControl(owner.ctx != null ? owner.ctx.stateHub : null);
            playback.Play(new AnimationPlaybackOptions
            {
                Transition = owner.AnimProfile.commanBlock,
                TargetLayer = AnimationPlaybackOptions.Layer.FullBodyAction,
                NormalizedStart = 0f,
            }, onEnd: endCallback);
            if (state == null) OnPlaybackEnd();
        }

        protected override void OnPlaybackEnd()
        {
            if (owner.actionSM.CurrentState != this) return;
            owner.actionSM.TrySetState(owner.empty);
            owner.TryResumeHoldAction();
        }

        protected override void ExitPlayback()
        {
            ReleaseControlLock();
            owner.presentation.FadeActionLayer(owner.fullBodyActionLayer);
        }

        public void ReleaseControlLock()
        {
            owner.sessions.ReleaseCommandControl();
        }

        public void Dispose()
        {
            playback.Stop();
            ReleaseControlLock();
        }
    }
}
