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
        private AnimancerEvent.Sequence runtimeEvents;
        private AnimationVfxSessionToken vfxSession;

        public Locomotion_Dash(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => owner.DashForward != null && owner.DashForward.IsValid;

        public override void OnEnterState()
        {
            EndVfxSession();
            ClearStateEvents();

            ClipTransition clip = PickClip(out AnimationVfxTrack vfxTrack);
            float dur = Mathf.Max(0.01f, owner._dashDuration);
            float fadeDuration = ResolveFadeDuration(clip, dur);

            state = owner.LocoLayer.Play(clip, fadeDuration);
            if (state == null)
                return;

            state.Time = 0f;

            float len = Mathf.Max(0.01f, state.Length);
            state.Speed = len / dur;

            owner.onDashEndCache ??= owner.HandleDashEnd;
            runtimeEvents = state.Events(owner);
            BindVfx(vfxTrack);
            runtimeEvents.OnEnd = owner.onDashEndCache;
        }

        public override void OnExitState()
        {
            EndVfxSession();
            ClearStateEvents();
        }

        internal void EndVfxSession()
        {
            owner.EndAnimationVfxSession(vfxSession);
            vfxSession = default;
        }

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

        private void BindVfx(AnimationVfxTrack vfxTrack)
        {
            if (runtimeEvents == null || vfxTrack == null || vfxTrack.CueCount == 0)
                return;

            AnimationVfxSessionToken token = owner.BeginAnimationVfxSession(vfxTrack);
            vfxSession = token;
            AnimationVfxEventBinder.Bind(
                runtimeEvents,
                cueIndex => owner.HandleAnimationVfxCue(token, cueIndex));
        }

        private void ClearStateEvents()
        {
            if (runtimeEvents != null)
                runtimeEvents.OnEnd = null;

            runtimeEvents = null;
            state = null;
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
