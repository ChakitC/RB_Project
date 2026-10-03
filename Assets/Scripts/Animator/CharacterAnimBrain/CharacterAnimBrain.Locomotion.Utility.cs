using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Utility : SingleClipLocomotionState
    {
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        public Locomotion_Utility(CharacterAnimBrain owner) : base(owner) { }

        public override bool CanEnterState
        {
            get
            {
                if (!owner.HasActiveUtilityWarpOutClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        protected override void EnterPlayback()
        {
            _completedNormally = false;
            var utilityClip = owner.UtilityWarpOutClip;

            if (utilityClip == null || !utilityClip.IsValid)
            {
                owner.TrySetLocomotionState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: true);
            owner.EmitPlaybackSignal(PlaybackKind.UtilityWarpOut, PlaybackPhase.Started, owner._utilityChannel.RequestId);

            playback.Play(new AnimationPlaybackOptions
            {
                Transition = utilityClip, NormalizedStart = 0f,
                Channel = owner._utilityChannel, InvokeAuthoredEnd = true,
            }, ConfigureEvents, endCallback);
        }

        private void ConfigureEvents(AnimancerEvent.Sequence events)
        {
            if (owner.HasPendingUtilityReleaseRequest)
            {
                owner.onUtilityCastMomentCache ??= owner.NotifyUtilityCastMoment;
                events.Add(owner.ActiveUtilityCastPointNormalized, owner.onUtilityCastMomentCache);
            }
        }

        protected override void ExitPlayback()
        {
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.NotifyUtilityStateExited(_completedNormally);
        }

        protected override void OnPlaybackEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            _completedNormally = true;

            if (owner.IsDowned)
                owner.TrySetLocomotionState(owner.crawlState);
            else
                owner.TrySetLocomotionState(owner.locomotion);
        }
    }
}
