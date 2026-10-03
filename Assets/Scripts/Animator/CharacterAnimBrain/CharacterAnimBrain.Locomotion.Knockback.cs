using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Knockback : SingleClipLocomotionState
    {
        private bool _prevApplyRootMotion;
        private KnockbackData pendingKnockback;
        private KnockbackData activeKnockback;

        public Locomotion_Knockback(CharacterAnimBrain owner) : base(owner) { }

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

        protected override void EnterPlayback()
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

            playback.Play(new AnimationPlaybackOptions
            {
                Transition = clip, NormalizedStart = 0f, Duration = activeKnockback.Duration,
                HoldLastFrame = true,
            });
        }

        protected override void ExitPlayback()
        {
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            activeKnockback = default(KnockbackData);
        }
    }
}
