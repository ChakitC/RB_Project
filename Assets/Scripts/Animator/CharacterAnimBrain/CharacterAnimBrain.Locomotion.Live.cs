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
        private ClipTransition activeClip;

        private ClipTransition ResolveClip()
        {
            var profile = owner.AnimProfile;
            ClipTransition clip = owner.IsSliding ? profile.slide :
                owner.IsCrouching ? (owner.MoveSpeed01 > 0.01f ? profile.crouchMove : profile.crouchIdle) :
                owner.IsSprinting ? profile.sprint : null;
            return clip != null && clip.IsValid ? clip : null;
        }

        public LocomotionState_Live(CharacterAnimBrain owner) => this.owner = owner;

        public override void OnEnterState()
        {
            activeClip = ResolveClip();
            if (activeClip != null)
            {
                owner.presentation.PlayLocomotionTransition(activeClip);
                mixerState = null;
                current = Vector2.up;
            }
            else
            {
                mixerState = owner.presentation.PlayLocomotionTransition(owner.LocomotionMixer) as Vector2MixerState;
                current = Vector2.zero;
            }
        }

        public override void Update()
        {
            // In-place locomotion clips leave upper-body actions and motion ownership intact.
            ClipTransition clip = ResolveClip();
            if (clip != activeClip)
            {
                activeClip = clip;
                if (activeClip != null)
                    owner.presentation.PlayLocomotionTransition(activeClip);
                else
                    mixerState = owner.presentation.PlayLocomotionTransition(owner.LocomotionMixer) as Vector2MixerState;
            }
            if (activeClip != null)
            {
                return;
            }
            if (mixerState == null) return;

            // magnitude = speed, direction = MoveDirLocal
            current = AnimationMixerInput.Smooth(current, owner.MoveDirLocal, owner.MoveSpeed01,
                owner.SnapTo8Directions, owner.LocomotionParamLerp, owner.AnimationDeltaTime);

            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
        }


    }  
}
