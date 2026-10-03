using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion (Idle/Walk) =====================

    private sealed class LocomotionState_Live : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private Vector2MixerState mixerState;
        private Vector2 current;

        public LocomotionState_Live(CharacterAnimBrain owner) => this.owner = owner;

        public override void OnEnterState()
        {
            mixerState = owner.presentation.PlayLocomotionTransition(owner.LocomotionMixer) as Vector2MixerState;
            current = Vector2.zero;
            owner.SetActivePairBasePoseBlend(current);
        }

        public override void Update()
        {
            if (mixerState == null) return;

            // magnitude = speed, direction = MoveDirLocal
            current = AnimationMixerInput.Smooth(current, owner.MoveDirLocal, owner.MoveSpeed01,
                owner.SnapTo8Directions, owner.LocomotionParamLerp, owner.AnimationDeltaTime);

            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
            owner.SetActivePairBasePoseBlend(current);
        }


    }  
}
