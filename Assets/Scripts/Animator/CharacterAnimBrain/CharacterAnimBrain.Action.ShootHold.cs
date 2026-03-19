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
            owner.ActLayer.StartFade(1f, owner.ActionFadeIn);

            if (owner.ShootHoldLoopClip != null)
                state = owner.ActLayer.Play(owner.ShootHoldLoopClip);
          
            else
                state = null;
            
           
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
            
            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);
        }
    }

}
