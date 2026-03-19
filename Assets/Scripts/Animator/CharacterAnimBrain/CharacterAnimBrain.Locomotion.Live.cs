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
            mixerState = owner.LocoLayer.Play(owner.LocomotionMixer) as Vector2MixerState;
            current = Vector2.zero;
        }

        public override void Update()
        {
            if (mixerState == null) return;

            // magnitude = speed, direction = MoveDirLocal
            Vector2 dir = owner.MoveDirLocal;

            if (owner.SnapTo8Directions)
                dir = Snap8(dir);

            Vector2 target = dir * Mathf.Clamp01(owner.MoveSpeed01); // 0..1

            float t = 1f - Mathf.Exp(-owner.LocomotionParamLerp * Time.deltaTime);
            current = Vector2.Lerp(current, target, t);

            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;

            
        }

        private static Vector2 Snap8(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f) return Vector2.zero;

            v.Normalize();
            float angle = Mathf.Atan2(v.y, v.x);      // x=right, y=forward
            float step  = Mathf.PI * 0.25f;           // 45°
            angle = Mathf.Round(angle / step) * step;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }  
}
