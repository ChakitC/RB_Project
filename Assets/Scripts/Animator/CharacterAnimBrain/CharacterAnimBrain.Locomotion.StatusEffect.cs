using Animancer;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_StatusEffect : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private StatusLocomotionPose kind;
        private bool _prevApplyRootMotion;
        private bool _ownsExclusiveLocomotion;

        public Locomotion_StatusEffect(CharacterAnimBrain owner)
        {
            this.owner = owner;
        }

        public void SetKind(StatusLocomotionPose value)
        {
            kind = value;
        }

        public override bool CanEnterState => kind != StatusLocomotionPose.None &&
                                              owner.GetStatusLocomotionClip(kind) != null;

        public override void OnEnterState()
        {
            _ownsExclusiveLocomotion = false;
            ClipTransition clip = owner.GetStatusLocomotionClip(kind);
            if (clip == null)
            {
                owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                return;
            }

            if (owner.ShouldInterruptActionLayer(kind))
            {
                _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                    usesRootMotion: false,
                    preserveFireHoldIntent: false);
                _ownsExclusiveLocomotion = true;
                owner.EmitPlaybackSignal(PlaybackKind.StatusEffect, PlaybackPhase.Started, 0);
            }
            else
            {
                // A soft pose does not take exclusive ownership, but it still may not be moved by
                // root motion, so it declares the policy rather than poking the Animator.
                owner.SetRootMotionActive(false);
            }

            owner.LocoLayer.Play(clip);
        }

        public override void OnExitState()
        {
            // Soft poses never took ownership, so they have nothing to hand back. Hard poses must
            // restore root motion, or a stun permanently disables it for the rest of the run.
            if (!_ownsExclusiveLocomotion)
                return;

            _ownsExclusiveLocomotion = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
        }
    }
}
