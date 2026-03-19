using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Dead ================================
    private sealed class  Locomotion_Dead : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        public Locomotion_Dead(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => owner.DeadClip != null;
        
        public override bool CanExitState => false;

        public override void OnEnterState()
        {
            owner.IsHoldingFire = false;
            owner._pendingAction = PendingAction.Empty;
            owner._pendingPulse = false;

            owner.actionSM.TrySetState(owner.empty);
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);

            
            state = owner.LocoLayer.Play(owner.DeadClip);

            
            owner.onDeadEndCache ??= () =>
            {
                if (state == null) return;
                state.Time = state.Length;
                state.Speed = 0f;
            };

            state.Events(owner).OnEnd = owner.onDeadEndCache;
        }
    }
}
