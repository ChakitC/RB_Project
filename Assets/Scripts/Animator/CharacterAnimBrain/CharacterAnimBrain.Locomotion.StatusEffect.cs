using Animancer;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_StatusEffect : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private StatusLocomotionKind kind;

        public Locomotion_StatusEffect(CharacterAnimBrain owner)
        {
            this.owner = owner;
        }

        public void SetKind(StatusLocomotionKind value)
        {
            kind = value;
        }

        public override bool CanEnterState => kind != StatusLocomotionKind.None &&
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
                owner.IsHoldingFire = false;
                owner._pendingAction = PendingAction.Empty;
                owner._pendingPulse = false;
                owner.actionSM.TrySetState(owner.empty);
                owner.ActLayer.StartFade(0f, owner.ActionFadeOut);
            }

            owner.animancer.Animator.applyRootMotion = false;
            owner.RootMotionActive = false;

            owner.LocoLayer.Play(clip);
        }
    }
}
