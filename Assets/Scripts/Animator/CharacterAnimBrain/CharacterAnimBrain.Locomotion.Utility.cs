using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Utility : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        private readonly Action _onEndCache;

        public Locomotion_Utility(CharacterAnimBrain owner)
        {
            this.owner = owner;
            _onEndCache = OnUtilityEnd;
        }

        public override bool CanEnterState
        {
            get
            {
                if (!owner.HasActiveUtilityWarpInClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override void OnEnterState()
        {
            _completedNormally = false;
            var utilityClip = owner.UtilityWarpInClip;

            if (utilityClip == null || !utilityClip.IsValid)
            {
                owner.locomotionSM.TrySetState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.animancer.Animator.applyRootMotion;
            owner.animancer.Animator.applyRootMotion = true;
            owner.RootMotionActive = true;

            owner.actionSM.TrySetState(owner.empty);
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);

            state = owner.LocoLayer.Play(utilityClip);
            runtimeEvents = new AnimancerEvent.Sequence(utilityClip.Events);

            if (owner.HasPendingUtilityReleaseRequest)
            {
                owner.onUtilityCastMomentCache ??= owner.NotifyUtilityCastMoment;
                runtimeEvents.Add(owner.ActiveUtilityCastPointNormalized, owner.onUtilityCastMomentCache);
            }

            var transitionOnEnd = runtimeEvents.OnEnd;
            runtimeEvents.OnEnd = transitionOnEnd == null
                ? _onEndCache
                : () =>
                {
                    transitionOnEnd();
                    _onEndCache();
                };

            state.SharedEvents = runtimeEvents;
        }

        public override void OnExitState()
        {
            if (state != null)
            {
                state.SharedEvents = null;
                runtimeEvents = null;
                state = null;
            }

            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;

            owner.actionSM.TrySetState(owner.empty);
            owner.NotifyUtilityStateExited(_completedNormally);
        }

        private void OnUtilityEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            _completedNormally = true;

            if (owner.IsDowned)
                owner.locomotionSM.TrySetState(owner.crawlState);
            else
                owner.locomotionSM.TrySetState(owner.locomotion);
        }
    }
}
