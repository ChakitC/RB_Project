using System;
using Animancer;
using Animancer.FSM;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Dash : SingleClipLocomotionState
    {

        public Locomotion_Dash(CharacterAnimBrain owner) : base(owner) { }

        public override bool CanEnterState => owner.DashForward != null && owner.DashForward.IsValid;

        protected override void EnterPlayback()
        {
            ClipTransition clip = PickClip(out AnimationVfxTrack vfxTrack);
            float duration = Mathf.Max(0.01f, owner._dashDuration);
            playback.Play(new AnimationPlaybackOptions
            {
                Transition = clip, NormalizedStart = 0f, Duration = duration,
                FadeDuration = ResolveFadeDuration(clip, duration), Vfx = vfxTrack,
            }, onEnd: endCallback);
        }

        protected override void OnPlaybackEnd() => owner.HandleDashEnd();
        internal void EndVfxSession() => playback.EndVfx();

        private ClipTransition PickClip(out AnimationVfxTrack vfxTrack)
        {
            if (ShouldUseBackwardClip(owner._dashDirLocal) &&
                owner.DashBackward != null &&
                owner.DashBackward.IsValid)
            {
                vfxTrack = owner.DashBackwardVfxTrack;
                return owner.DashBackward;
            }

            vfxTrack = owner.DashForwardVfxTrack;
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
