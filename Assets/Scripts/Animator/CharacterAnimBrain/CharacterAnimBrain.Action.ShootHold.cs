using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Action: Shoot Hold (button held) =====================

    private sealed class Action_ShootHold : ActionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        private WeaponSystem WS => owner.ctx != null ? owner.ctx.WeaponSystem : null;

        public Action_ShootHold(CharacterAnimBrain owner) => this.owner = owner;

        // public override bool CanEnterState => owner.IsHoldingFire;
     
        public override bool CanEnterState
            => owner.IsHoldingFire && owner.ShootHoldLoopClip != null;
        
        public override void OnEnterState()
        {
            state = owner.PlayActionPresentation(owner.ShootHoldLoopClip, PairOffsetUpperAction.ShootHold, false);
        }

        public override void Update()
        {
            if (!owner.IsHoldingFire)
            {
                owner.actionSM.TrySetState(owner.empty);
            
            }
            
        }

        public override void OnExitState()
        {
            owner.presentation.FadeActionLayer(owner.ActLayer);
        }
    }

}
