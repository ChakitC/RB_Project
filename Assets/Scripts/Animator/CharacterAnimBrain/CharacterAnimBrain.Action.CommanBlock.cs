using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    Action_CommanBlock commanBlockState;

    public bool TryPlayCommanBlock()
    {
        if (!TryInitialize() || !TryGetAnimProfile(out var profile) ||
            profile.commanBlock == null || !profile.commanBlock.IsValid ||
            (CurrentAnimationMode != CharacterAnimationMode.Locomotion &&
             CurrentAnimationMode != CharacterAnimationMode.SoftStatus)) return false;

        commanBlockState ??= new Action_CommanBlock(this);
        return actionSM.TryResetState(commanBlockState);
    }

    void StopCommanBlock()
    {
        commanBlockState?.ReleaseControlLock();
        if (_initialized && animancer != null && animancer.Animator != null &&
            actionSM.CurrentState == commanBlockState)
            actionSM.ForceSetState(empty);
    }

    // The defender owns Block gameplay; this state owns the Player's temporary control lock.
    private sealed class Action_CommanBlock : ActionState
    {
        readonly CharacterAnimBrain owner;
        AnimancerState state;
        StateHub lockedHub;
        int controlToken;

        public Action_CommanBlock(CharacterAnimBrain owner) => this.owner = owner;

        public override void OnEnterState()
        {
            ReleaseControlLock();
            lockedHub = owner.ctx != null ? owner.ctx.stateHub : null;
            if (lockedHub != null)
                controlToken = lockedHub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Rotate, nameof(CharacterAnimBrain));
            state = owner.PlayActionPresentation(owner.AnimProfile.commanBlock,
                PairOffsetUpperAction.None, fullBody: true);
            if (state != null)
            {
                state.NormalizedTime = 0f;
                state.Events(owner).OnEnd = OnEnd;
            }
            else OnEnd();
        }

        private void OnEnd()
        {
            if (owner.actionSM.CurrentState != this) return;
            owner.actionSM.TrySetState(owner.empty);
            owner.TryResumeHoldAction();
        }

        public override void OnExitState()
        {
            ReleaseControlLock();
            if (state != null) state.Events(owner).OnEnd = null;
            state = null;
            owner.FadeActionLayer(owner.fullBodyActionLayer);
        }

        public void ReleaseControlLock()
        {
            if (lockedHub != null && controlToken != 0)
                lockedHub.ReleaseExternalControlBlockToken(controlToken);
            controlToken = 0;
            lockedHub = null;
        }

        public void Dispose()
        {
            ReleaseControlLock();
        }
    }
}
