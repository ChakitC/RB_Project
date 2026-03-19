using UnityEngine;
using Animancer;
using System;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Skill : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private bool _prevApplyRootMotion;
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
                if (owner.SkillClip == null) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override void OnEnterState()
        {
            if (owner.SkillClip == null)
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
            state.Events(this).OnEnd = _onEndCache;
        }

        public override void OnExitState()
        {
            if (state != null)
                state.Events(this).OnEnd = null;

            owner.animancer.Animator.applyRootMotion = _prevApplyRootMotion;
            owner.RootMotionActive = false;
            

            owner.actionSM.TrySetState(owner.empty);
            owner.gameObject.SetActive(false);
        }

        private void OnSkillEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            if (owner.IsDowned)
                owner.locomotionSM.TrySetState(owner.crawlState);
            else
                owner.locomotionSM.TrySetState(owner.locomotion);
        }
    }
}
