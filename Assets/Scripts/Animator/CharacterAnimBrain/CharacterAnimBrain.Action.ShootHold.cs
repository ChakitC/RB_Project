using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
using UnityEngine;
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
     

        public override void OnEnterState()
        {
            owner.ActLayer.StartFade(1f, owner.actionFadeIn);

            if (owner.shootHoldLoop != null)
                state = owner.ActLayer.Play(owner.shootHoldLoop);
            else
                state = null;
        }

        public override void Update()
        {
          
            
        }

        public override void OnExitState()
        {
            owner.ActLayer.StartFade(0f, owner.actionFadeOut);
        }
    }

}
