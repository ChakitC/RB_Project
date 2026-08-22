using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Knockback : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private bool _prevApplyRootMotion;
        private KnockbackData pendingKnockback;
        private KnockbackData activeKnockback;

        public Locomotion_Knockback(CharacterAnimBrain owner)
        {
            this.owner = owner;
        }

        public void SetKnockback(KnockbackData value)
        {
            pendingKnockback = value;
        }

        public override bool CanEnterState =>
            pendingKnockback.IsValid &&
            owner.KnockbackClip != null &&
            owner.KnockbackClip.IsValid &&
            !owner.IsDowned &&
            owner.locomotionSM.CurrentState != owner.deadState;

        public override void OnEnterState()
        {
            activeKnockback = pendingKnockback;
            pendingKnockback = default(KnockbackData);

            ClipTransition clip = owner.KnockbackClip;
            if (clip == null || !clip.IsValid || !activeKnockback.IsValid)
            {
                activeKnockback = default(KnockbackData);
                owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: false,
                preserveFireHoldIntent: false);

            state = owner.LocoLayer.Play(clip);
            state.NormalizedTime = 0f;

            float clipLength = Mathf.Max(0.01f, state.Length);
            float knockbackDuration = Mathf.Max(0.01f, activeKnockback.Duration);
            state.Speed = clipLength / knockbackDuration;
            state.Events(owner).OnEnd = OnClipEnd;
        }

        public override void OnExitState()
        {
            if (state != null)
            {
                state.Events(owner).OnEnd = null;
                state = null;
            }

            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            activeKnockback = default(KnockbackData);
        }

        void OnClipEnd()
        {
            if (owner.locomotionSM.CurrentState != this || state == null)
                return;

            state.Time = state.Length;
            state.Speed = 0f;
        }
    }
}
