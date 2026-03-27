using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Skill : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
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
                if (!owner.HasValidSkillClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override void OnEnterState()
        {
            _completedNormally = false;

            if (!owner.HasValidSkillClip)
            {
                owner.locomotionSM.TrySetState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.animancer.Animator.applyRootMotion;
            owner.animancer.Animator.applyRootMotion = true; // ถ้ามีหลาย skill ควรใช้ flag/config
            owner.RootMotionActive = true;

            owner.actionSM.TrySetState(owner.empty);
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);

            state = owner.LocoLayer.Play(owner.SkillClip);

            var events = state.Events(this);
            events.Clear();

            if (owner.HasPendingSkillReleaseRequest)
            {
                owner.onSkillCastMomentCache ??= owner.NotifySkillCastMoment;
                events.Add(owner.ActiveSkillCastPointNormalized, owner.onSkillCastMomentCache);
            }

            events.OnEnd = _onEndCache;
        }

        public override void OnExitState()
        {
            if (state != null)
            {
                var events = state.Events(this);
                events.Clear();
                events.OnEnd = null;
                state = null;
            }

            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;

            owner.actionSM.TrySetState(owner.empty);
            owner.NotifySkillStateExited(_completedNormally);
            
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
