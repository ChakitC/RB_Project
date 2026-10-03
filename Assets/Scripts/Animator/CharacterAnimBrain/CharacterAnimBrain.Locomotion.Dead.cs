using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;

public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Dead ================================

    private sealed class Locomotion_Dead : SingleClipLocomotionState
    {

        public Locomotion_Dead(CharacterAnimBrain owner) : base(owner) { }

        public override bool CanEnterState => owner.DeadClip != null;

        public override bool CanExitState => false;

        protected override void EnterPlayback()
        {
            owner.EnterExclusiveLocomotion(
                usesRootMotion: false,
                preserveFireHoldIntent: false);
            owner.EmitPlaybackSignal(PlaybackKind.Dead, PlaybackPhase.Started, 0);

            playback.Play(new AnimationPlaybackOptions
            {
                Transition = owner.DeadClip, HoldLastFrame = true,
            });
        }
    }
}
