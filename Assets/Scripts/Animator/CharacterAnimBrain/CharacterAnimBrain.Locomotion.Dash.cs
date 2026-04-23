using System;
using Animancer;
using Animancer.FSM;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Dash : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        public Locomotion_Dash(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => owner.DashForward != null && owner.DashForward.IsValid;

        public override void OnEnterState()
        {
            ClipTransition clip = PickClip();
            float dur = Mathf.Max(0.01f, owner._dashDuration);
            float fadeDuration = ResolveFadeDuration(clip, dur);

            state = owner.LocoLayer.Play(clip, fadeDuration);
            state.Time = 0f;

            float len = Mathf.Max(0.01f, state.Length);
            state.Speed = len / dur;

            owner.onDashEndCache ??= owner.HandleDashEnd;
            state.Events(owner).OnEnd = owner.onDashEndCache;
        }

        public override void OnExitState()
        {
            if (state != null)
            {
                state.Events(owner).OnEnd = null;
                state = null;
            }
        }

        private ClipTransition PickClip()
        {
            if (ShouldUseBackwardClip(owner._dashDirLocal) &&
                owner.DashBackward != null &&
                owner.DashBackward.IsValid)
            {
                return owner.DashBackward;
            }

            return owner.DashForward;
        }

        private static float ResolveFadeDuration(ClipTransition clip, float dashDuration)
        {
            if (clip == null)
                return 0f;

            float fadeDuration = clip.FadeDuration;
            if (float.IsNaN(fadeDuration) || fadeDuration < 0f)
                fadeDuration = 0f;

            return Mathf.Min(fadeDuration, dashDuration * 0.25f);
        }

        private static bool ShouldUseBackwardClip(Vector2 dashDirLocal)
        {
            if (dashDirLocal.sqrMagnitude <= 0.0001f)
                return false;

            return dashDirLocal.y < 0f && Mathf.Abs(dashDirLocal.y) >= Mathf.Abs(dashDirLocal.x);
        }
    }
}
