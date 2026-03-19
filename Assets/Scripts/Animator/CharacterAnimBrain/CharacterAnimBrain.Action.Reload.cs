using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Action: Reload  =====================
    private sealed class Action_Reload : ActionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private bool _lockExit;

        public Action_Reload(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => owner.ReloadClip != null;
        public override bool CanExitState => !_lockExit;

        public void CancelNow()
        {
            _lockExit = false;

            if (state != null)
            {
                state.Events(owner).OnEnd = null;
                state = null;
            }

            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);
        }

        public override void OnEnterState()
        {
            _lockExit = true;

            owner.ActLayer.StartFade(1f, owner.ActionFadeIn);

            state = owner.ActLayer.Play(owner.ReloadClip);

            float len = Mathf.Max(0.01f, state.Length);
            float dur = Mathf.Max(0.01f, owner._reloadDuration);
            state.Speed = len / dur;

            state.Events(owner).OnEnd = () =>
            {
                _lockExit = false;
                owner.actionSM.TrySetState(owner.empty);
            };
        }
    }

}
