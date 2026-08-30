using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    /// <summary>
    /// The one-shot Special Point Mini Stun playback.
    ///
    /// Deliberately not an extension of <see cref="Locomotion_StatusEffect"/>. Status locomotion is
    /// intent-driven — it holds a pose for as long as the intent says so, runs with
    /// <c>usesRootMotion: false</c>, and has no notion of a clip finishing — while this reaction
    /// needs request identity, complete root motion, a terminal completion/interruption callback,
    /// and a watchdog. Widening the status state to cover both would have changed every generic Mini
    /// Stun in the game.
    /// </summary>
    private sealed class Locomotion_SpecialReaction : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState _state;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;

        /// <summary>Set when the profile clip was missing: the state holds the lock, then completes.</summary>
        private bool _clipFallbackActive;
        private float _fallbackRemaining;

        private float _watchdogRemaining;

        public Locomotion_SpecialReaction(CharacterAnimBrain owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Always true. A missing clip is not a reason to refuse the reaction: the locked design
        /// requires the gameplay lock to be held for the profile's fallback duration and the round
        /// to complete normally, so the ChainReady handoff still happens.
        /// </summary>
        public override bool CanEnterState =>
            !owner.IsDowned && owner.locomotionSM.CurrentState != owner.deadState;

        public override void OnEnterState()
        {
            _completedNormally = false;
            _clipFallbackActive = false;
            _state = null;

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: false);

            // Full animation translation (Y included) and animation yaw, constrained against
            // environment geometry. Planar-only would flatten an authored recoil arc, and the
            // two-argument shape helper cannot express yaw without planar.
            owner.SetRootMotionShape(
                planarOnly: false,
                applyYaw: true,
                ignoreCharacterCollision: false,
                environmentSafe: true);

            owner.EmitPlaybackSignal(
                PlaybackKind.SpecialReaction,
                PlaybackPhase.Started,
                owner._specialReactionChannel.RequestId);

            ClipTransition clip = owner.SpecialReactionClip;
            if (clip == null || !clip.IsValid)
            {
                BeginMissingClipFallback();
                return;
            }

            _state = owner.LocoLayer.Play(clip);
            _state.NormalizedTime = 0f;
            _state.Events(owner).OnEnd = OnClipEnd;

            float speed = Mathf.Abs(_state.Speed) > 0.001f ? Mathf.Abs(_state.Speed) : 1f;
            _watchdogRemaining = Mathf.Max(0.05f, _state.Length / speed) + SpecialReactionWatchdogGrace;
        }

        private void BeginMissingClipFallback()
        {
            _clipFallbackActive = true;
            _fallbackRemaining = Mathf.Max(0.05f, owner._specialReactionFallbackSeconds);
            _watchdogRemaining = _fallbackRemaining + SpecialReactionWatchdogGrace;

            Debug.LogWarning(
                $"[{nameof(CharacterAnimBrain)}] No valid Special Point reaction clip on the anim profile. " +
                $"Holding the gameplay lock for {_fallbackRemaining:0.##}s, then completing normally.",
                owner);
        }

        public override void Update()
        {
            // The clip runs on the Animancer graph, which world slow already scales, so the watchdog
            // has to burn the same clock or it would fire early during a slow.
            float dt = owner.AnimationDeltaTime;
            if (dt <= 0f)
                return;

            if (_clipFallbackActive)
            {
                _fallbackRemaining -= dt;
                if (_fallbackRemaining <= 0f)
                {
                    CompleteNow();
                    return;
                }
            }

            if (_watchdogRemaining <= 0f)
                return;

            _watchdogRemaining -= dt;
            if (_watchdogRemaining <= 0f)
                CompleteNow();
        }

        public override void OnExitState()
        {
            if (_state != null)
            {
                _state.Events(owner).OnEnd = null;
                _state = null;
            }

            _clipFallbackActive = false;
            _watchdogRemaining = 0f;

            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.ClearRootMotionPolicy();

            // Terminal-once: the channel decides whether this close still owes a completion or an
            // interruption, so two paths racing to tear the playback down cannot double-report.
            owner.CloseSpecialReactionSession(_completedNormally);
        }

        private void OnClipEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            CompleteNow();
        }

        private void CompleteNow()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            _completedNormally = true;

            // Leaving the state before the completion signal is raised is what lets the handler
            // enter ChainReady in the same call stack: the transition policy would otherwise still
            // see SpecialReaction owning locomotion and refuse the ChainReady pose.
            bool exited = owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
            if (!exited)
                owner.ForceSetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
        }

        private const float SpecialReactionWatchdogGrace = 0.25f;
    }
}
