using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Action: Reload  =====================
    private sealed class Action_Reload : ActionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
        private AnimationVfxSessionToken vfxSession;
        private bool _lockExit;

        public Action_Reload(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => owner.ReloadClip != null;
        public override bool CanExitState => !_lockExit;

        public void CancelNow()
        {
            _lockExit = false;
            ClearStateEvents();
            EndVfxSession();

            owner.ActLayer.StartFade(0f, owner.ActionFadeOut);
        }

        public override void OnExitState()
        {
            _lockExit = false;
            ClearStateEvents();
            EndVfxSession();
        }

        public override void OnEnterState()
        {
            ClearStateEvents();
            EndVfxSession();
            _lockExit = true;

            state = owner.PlayActionTransition(owner.ReloadClip, PairOffsetUpperAction.Reload);
            if (state == null)
            {
                _lockExit = false;
                return;
            }

            float len = Mathf.Max(0.01f, state.Length);
            float dur = Mathf.Max(0.01f, owner._reloadDuration);
            state.Speed = len / dur;

            runtimeEvents = state.Events(owner);
            BindVfx();
            runtimeEvents.OnEnd = OnReloadEnd;
        }

        internal void EndVfxSession()
        {
            owner.EndAnimationVfxSession(vfxSession);
            vfxSession = default;
        }

        private void BindVfx()
        {
            AnimationVfxTrack track = owner.ReloadVfxTrack;
            if (runtimeEvents == null || track == null || track.CueCount == 0)
                return;

            AnimationVfxSessionToken token = owner.BeginAnimationVfxSession(track);
            vfxSession = token;
            AnimationVfxEventBinder.Bind(
                runtimeEvents,
                cueIndex => owner.HandleAnimationVfxCue(token, cueIndex));
        }

        private void OnReloadEnd()
        {
            if (owner.actionSM.CurrentState != this)
                return;

            _lockExit = false;
            EndVfxSession();
            owner.actionSM.TrySetState(owner.empty);
        }

        private void ClearStateEvents()
        {
            if (runtimeEvents != null)
                runtimeEvents.OnEnd = null;

            runtimeEvents = null;
            state = null;
        }
    }

    private sealed class Locomotion_Reload : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
        private AnimationVfxSessionToken vfxSession;
        private bool _lockExit;
        private bool _prevApplyRootMotion;

        public Locomotion_Reload(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState =>
            owner.ReloadClip != null &&
            owner.ReloadClip.IsValid &&
            !owner.IsDowned &&
            owner.locomotionSM.CurrentState != owner.deadState;

        public override bool CanExitState => !_lockExit;

        public void CancelNow()
        {
            _lockExit = false;
            ClearStateEvents();
            EndVfxSession();
        }

        public override void OnEnterState()
        {
            ClearStateEvents();
            EndVfxSession();
            _lockExit = true;

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: false,
                preserveFireHoldIntent: true);

            state = owner.LocoLayer.Play(owner.ReloadClip);
            state.NormalizedTime = 0f;

            float len = Mathf.Max(0.01f, state.Length);
            float dur = Mathf.Max(0.01f, owner._reloadDuration);
            state.Speed = len / dur;

            runtimeEvents = state.Events(owner);
            BindVfx();
            runtimeEvents.OnEnd = OnReloadEnd;
        }

        public override void OnExitState()
        {
            ClearStateEvents();
            EndVfxSession();
            _lockExit = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
        }

        internal void EndVfxSession()
        {
            owner.EndAnimationVfxSession(vfxSession);
            vfxSession = default;
        }

        private void BindVfx()
        {
            AnimationVfxTrack track = owner.ReloadVfxTrack;
            if (runtimeEvents == null || track == null || track.CueCount == 0)
                return;

            AnimationVfxSessionToken token = owner.BeginAnimationVfxSession(track);
            vfxSession = token;
            AnimationVfxEventBinder.Bind(
                runtimeEvents,
                cueIndex => owner.HandleAnimationVfxCue(token, cueIndex));
        }

        private void OnReloadEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            _lockExit = false;
            owner.locomotionSM.TrySetState(owner.IsDowned ? owner.crawlState : owner.locomotion);
        }

        private void ClearStateEvents()
        {
            if (runtimeEvents != null)
                runtimeEvents.OnEnd = null;

            runtimeEvents = null;
            state = null;
        }
    }

}
