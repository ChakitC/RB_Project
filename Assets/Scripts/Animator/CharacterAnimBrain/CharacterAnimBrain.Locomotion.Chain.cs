using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Chain : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
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
                owner.TrySetLocomotionState(owner.locomotion);
                return;
            }

            owner.ApplyActiveChainRootMotionPolicy();
            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: owner.ActiveChainUsesRootMotion,
                preserveFireHoldIntent: true);
            owner.EmitPlaybackSignal(owner.ResolveActiveChainPlaybackKind(), PlaybackPhase.Started, owner.ActiveChainRequestId);

            state = owner.LocoLayer.Play(chainClip);
            state.NormalizedTime = 0f;
            runtimeEvents = new AnimancerEvent.Sequence(chainClip.Events);
            owner.BindActiveChainTimelineEvents(runtimeEvents);

            var transitionOnEnd = runtimeEvents.OnEnd;
            runtimeEvents.OnEnd = transitionOnEnd == null
                ? _onEndCache
                : () =>
                {
                    transitionOnEnd();
                    _onEndCache();
                };

            state.SharedEvents = runtimeEvents;
            _watchdogDeadline = owner.AnimationTime + ResolveWatchdogDuration(state) + owner.ChainPlaybackWatchdogGraceSeconds;
        }

        public override void Update()
        {
            if (state == null)
                return;

            owner.PollActiveChainPlayback(state);

            if (owner.AnimationTime >= _watchdogDeadline)
                ForceExitFromWatchdog();
        }

        public override void OnExitState()
        {
            _watchdogDeadline = float.PositiveInfinity;

            if (state != null)
            {
                state.SharedEvents = null;
                runtimeEvents = null;
                state = null;
            }

            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.ClearActiveChainRootMotionPolicy();
            owner.NotifyChainPlaybackStateExited(_completedNormally);
        }

        internal bool TryGetNormalizedTime(out float normalizedTime)
        {
            if (state != null)
            {
                normalizedTime = state.NormalizedTime;
                return true;
            }

            normalizedTime = 0f;
            return false;
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

            bool castReleased = owner._chainChannel.Request.Released;
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
