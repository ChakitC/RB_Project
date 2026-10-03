using System;
using Animancer;

public sealed partial class CharacterAnimBrain
{
    private abstract class SingleClipLocomotionState : LocomotionState
    {
        protected readonly CharacterAnimBrain owner;
        private protected readonly AnimationPlayback playback;
        protected readonly Action endCallback;
        protected AnimancerState state => playback.State;
        protected SingleClipLocomotionState(CharacterAnimBrain owner)
        { this.owner = owner; playback = owner.CreatePlayback(); endCallback = OnPlaybackEnd; }
        public sealed override void OnEnterState() { playback.Stop(); EnterPlayback(); }
        public sealed override void OnExitState() { playback.Stop(); ExitPlayback(); }
        protected abstract void EnterPlayback();
        protected virtual void ExitPlayback() { }
        protected virtual void OnPlaybackEnd() { }
    }
}
