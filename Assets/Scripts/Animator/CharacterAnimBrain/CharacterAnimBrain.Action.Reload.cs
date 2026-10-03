using Animancer;

public sealed partial class CharacterAnimBrain
{
    private sealed class Action_Reload : SingleClipActionState
    {
        private bool _lockExit;
        public Action_Reload(CharacterAnimBrain owner) : base(owner) { }
        public override bool CanEnterState => owner.ReloadClip != null;
        public override bool CanExitState => !_lockExit;
        public void CancelNow()
        {
            _lockExit = false;
            playback.Stop();
            owner.presentation.FadeActionLayer(owner.ActLayer);
        }
        protected override void EnterPlayback()
        {
            _lockExit = true;
            playback.Play(new AnimationPlaybackOptions
            {
                Transition = owner.ReloadClip, TargetLayer = AnimationPlaybackOptions.Layer.UpperAction,
                PairAction = PairOffsetUpperAction.Reload, Duration = owner._reloadDuration, Vfx = owner.ReloadVfxTrack,
            }, onEnd: endCallback);
            if (state == null) _lockExit = false;
        }
        protected override void ExitPlayback() { _lockExit = false; }
        internal void EndVfxSession() => playback.EndVfx();
        protected override void OnPlaybackEnd()
        {
            if (owner.actionSM.CurrentState != this) return;
            _lockExit = false;
            playback.EndVfx();
            owner.actionSM.TrySetState(owner.empty);
        }
    }

    private sealed class Locomotion_Reload : SingleClipLocomotionState
    {
        private bool _lockExit;
        private bool _prevApplyRootMotion;
        public Locomotion_Reload(CharacterAnimBrain owner) : base(owner) { }
        public override bool CanEnterState => owner.ReloadClip != null && owner.ReloadClip.IsValid &&
            !owner.IsDowned && owner.locomotionSM.CurrentState != owner.deadState;
        public override bool CanExitState => !_lockExit;
        public void CancelNow() { _lockExit = false; playback.Stop(); }
        protected override void EnterPlayback()
        {
            _lockExit = true;
            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(false, true);
            playback.Play(new AnimationPlaybackOptions
            {
                Transition = owner.ReloadClip, NormalizedStart = 0f,
                Duration = owner._reloadDuration, Vfx = owner.ReloadVfxTrack,
            }, onEnd: endCallback);
        }
        protected override void ExitPlayback()
        {
            _lockExit = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
        }
        internal void EndVfxSession() => playback.EndVfx();
        protected override void OnPlaybackEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;
            _lockExit = false;
            owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
        }
    }
}
