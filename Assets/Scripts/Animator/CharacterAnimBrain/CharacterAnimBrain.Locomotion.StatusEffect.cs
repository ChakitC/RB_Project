using Animancer;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_StatusEffect : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private StatusLocomotionPose kind;

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
            ClipTransition clip = owner.GetStatusLocomotionClip(kind);
            if (clip == null)
            {
                owner.locomotionSM.TrySetState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                return;
            }

            if (owner.ShouldInterruptActionLayer(kind))
            {
                owner.EnterExclusiveLocomotion(
                    usesRootMotion: false,
                    preserveFireHoldIntent: false);
                owner.EmitPlaybackSignal(PlaybackKind.StatusEffect, PlaybackPhase.Started, 0);
            }
            else
            {
                owner.animancer.Animator.applyRootMotion = false;
                owner.RootMotionActive = false;
            }

            owner.LocoLayer.Play(clip);
        }
    }
}
