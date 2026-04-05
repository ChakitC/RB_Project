using System;
using Animancer;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Chain : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        private readonly Action _onEndCache;

        public Locomotion_Chain(CharacterAnimBrain owner)
        {
            this.owner = owner;
            _onEndCache = OnChainEnd;
        }

        public override bool CanEnterState
        {
            get
            {
                if (!owner.HasActiveChainClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override bool CanExitState => owner.CanExitChainState;

        public override void OnEnterState()
        {
            _completedNormally = false;
            ClipTransition chainClip = owner.ActiveChainClip;

            if (chainClip == null || !chainClip.IsValid)
            {
                owner.AllowChainStateExit();
                owner.InterruptActiveChainRequest();
                owner.locomotionSM.TrySetState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.animancer.Animator.applyRootMotion;
            owner.animancer.Animator.applyRootMotion = true;
            owner.RootMotionActive = true;

            owner.actionSM.TrySetState(owner.empty);
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);

            state = owner.LocoLayer.Play(chainClip);
            state.NormalizedTime = 0f;
            state.Events(owner).OnEnd = _onEndCache;
        }

        public override void Update()
        {
            if (state == null)
                return;

            owner.PollActiveChainPlayback(state);
        }

        public override void OnExitState()
        {
            if (state != null)
            {
                state.Events(owner).OnEnd = null;
                state = null;
            }

            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;

            owner.actionSM.TrySetState(owner.empty);
            owner.NotifyChainPlaybackStateExited(_completedNormally);
        }

        private void OnChainEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            _completedNormally = true;
            owner.CompleteActiveChainPlayback();
        }
    }
}
