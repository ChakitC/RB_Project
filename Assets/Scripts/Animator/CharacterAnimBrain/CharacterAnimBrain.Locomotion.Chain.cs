using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Chain : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        private float _watchdogDeadline;
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

            owner.ClearActionLayerForExclusiveLocomotion();

            state = owner.LocoLayer.Play(chainClip);
            state.NormalizedTime = 0f;
            state.Events(owner).OnEnd = _onEndCache;
            _watchdogDeadline = Time.time + ResolveWatchdogDuration(state) + owner.ChainPlaybackWatchdogGraceSeconds;
        }

        public override void Update()
        {
            if (state == null)
                return;

            owner.PollActiveChainPlayback(state);

            if (Time.time >= _watchdogDeadline)
                ForceExitFromWatchdog();
        }

        public override void OnExitState()
        {
            _watchdogDeadline = float.PositiveInfinity;

            if (state != null)
            {
                state.Events(owner).OnEnd = null;
                state = null;
            }

            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;

            owner.ClearActionLayerForExclusiveLocomotion();
            owner.TryResumeHoldAction();
            owner.NotifyChainPlaybackStateExited(_completedNormally);
        }

        private void OnChainEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            _completedNormally = true;
            owner.CompleteActiveChainPlayback();
        }

        private float ResolveWatchdogDuration(AnimancerState playingState)
        {
            if (playingState == null)
                return 0.01f;

            float speed = Mathf.Abs(playingState.Speed);
            if (speed < 0.0001f)
                speed = 1f;

            return Mathf.Max(0.01f, playingState.Length / speed);
        }

        private void ForceExitFromWatchdog()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            _watchdogDeadline = float.PositiveInfinity;

            bool castReleased = owner._activeChainReleased;
            int requestId = owner.ActiveChainRequestId;

            Debug.LogWarning(
                $"[CharacterAnimBrain] Chain playback watchdog forced {(castReleased ? "completion" : "interruption")} " +
                $"for request {requestId} on '{owner.name}'.",
                owner);

            if (castReleased)
            {
                _completedNormally = true;
                owner.CompleteActiveChainPlayback();
                return;
            }

            owner.CancelChainPlaybackRequest(requestId);
        }
    }
}
