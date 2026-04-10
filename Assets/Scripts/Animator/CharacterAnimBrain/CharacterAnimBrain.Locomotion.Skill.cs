using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Skill : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        private readonly Action _onEndCache;

        public Locomotion_Skill(CharacterAnimBrain owner)
        {
            this.owner = owner;
            _onEndCache = OnSkillEnd;
        }

        public override bool CanEnterState
        {
            get
            {
                if (!owner.HasActiveSkillClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override void OnEnterState()
        {
            _completedNormally = false;
            var skillClip = owner.SkillClip;

            if (skillClip == null || !skillClip.IsValid)
            {
                owner.locomotionSM.TrySetState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.animancer.Animator.applyRootMotion;
            owner.animancer.Animator.applyRootMotion = true; // ถ้ามีหลาย skill ควรใช้ flag/config
            owner.RootMotionActive = true;

            owner.ClearActionLayerForExclusiveLocomotion();

            state = owner.LocoLayer.Play(skillClip);
            state.NormalizedTime = 0f;
            runtimeEvents = new AnimancerEvent.Sequence(skillClip.Events);

            if (owner.HasPendingSkillReleaseRequest)
            {
                owner.onSkillCastMomentCache ??= owner.NotifySkillCastMoment;
                runtimeEvents.Add(owner.ActiveSkillCastPointNormalized, owner.onSkillCastMomentCache);
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

            owner.ClearActionLayerForExclusiveLocomotion();
            owner.TryResumeHoldAction();
            owner.NotifySkillStateExited(_completedNormally);
            
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

        private void OnSkillEnd()
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
