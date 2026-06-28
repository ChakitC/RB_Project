using System;
using Animancer;
using Animancer.FSM;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_MeleeCombo : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;
        private AnimancerEvent.Sequence runtimeEvents;
        private MeleeComboSO comboLocked;
        private bool _prevApplyRootMotion;
        private MeleeComboSO.Step _pendingStep;
        private int _pendingStepIndex;
        private AnimationVfxSessionToken vfxSession;

        public MeleeComboSO CurrentCombo => comboLocked;

        internal AnimancerState DebugState => state;

        public Locomotion_MeleeCombo(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState => comboLocked != null && comboLocked.IsValid(out _);

        public void PrepareForStart(MeleeComboSO combo, MeleeComboSO.Step firstStep, int stepIndex)
        {
            comboLocked = combo;
            _pendingStep = firstStep;
            _pendingStepIndex = stepIndex;
        }

        public override void OnEnterState()
        {
            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: false);
            owner.EmitPlaybackSignal(PlaybackKind.Melee, PlaybackPhase.Started, 0);

            PlayStep(_pendingStep, _pendingStepIndex);
        }

        public override void OnExitState()
        {
            EndVfxSession();
            if (state != null)
            {
                state.SharedEvents = null;
                runtimeEvents = null;
                state = null;
            }

            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.MeleeHitEnd?.Invoke();
        }

        public void PlayStepExternal(MeleeComboSO.Step step, int stepIndex)
        {
            PlayStep(step, stepIndex);
        }

        private void PlayStep(MeleeComboSO.Step cfg, int stepIndex)
        {
            EndVfxSession();

            if (cfg.clip == null)
            {
                owner.CompleteMeleePlayback();
                return;
            }

            owner.CurrentMeleeStep = cfg;
            owner.CurrentMeleeStepIndex = stepIndex;

            state = owner.LocoLayer.Play(cfg.clip);

            if (cfg.duration > 0.01f)
            {
                float length = Mathf.Max(0.01f, state.Length);
                state.Speed = length / Mathf.Max(0.01f, cfg.duration);
            }
            else
            {
                state.Speed = 1f;
            }

            int currentStepIndex = stepIndex;
            string clipName = cfg.clip.Clip != null ? cfg.clip.Clip.name : "<none>";

            owner.onMeleeHitStartCache = () =>
            {
                Debug.Log($"[Invoke] MeleeHitStart step={currentStepIndex} frame={Time.frameCount}");
                owner.MeleeHitStart?.Invoke();
            };

            owner.onMeleeHitEndCache = () => owner.MeleeHitEnd?.Invoke();

            runtimeEvents = new AnimancerEvent.Sequence(cfg.clip.Events);

            if (cfg.AnimationVfxTrack != null)
            {
                vfxSession = owner.BeginAnimationVfxSession(cfg.AnimationVfxTrack);
                AnimationVfxEventBinder.Bind(
                    runtimeEvents,
                    cueIndex => owner.HandleAnimationVfxCue(vfxSession, cueIndex));
            }

            int hitStartCount = runtimeEvents.SetCallbacks(
                CombatTimelineEventNames.ToStringReference(MeleeComboSO.HitStartEventName),
                owner.onMeleeHitStartCache);
            int hitEndCount = runtimeEvents.SetCallbacks(
                CombatTimelineEventNames.ToStringReference(MeleeComboSO.HitEndEventName),
                owner.onMeleeHitEndCache);

            if (hitStartCount == 0 || hitEndCount == 0)
            {
                Debug.LogWarning(
                    $"[MeleeCombo] Step {currentStepIndex} clip '{clipName}' is missing HitStart/HitEnd events.",
                    owner);
            }
            else if (hitStartCount != hitEndCount)
            {
                Debug.LogWarning(
                    $"[MeleeCombo] Step {currentStepIndex} clip '{clipName}' has unbalanced HitStart/HitEnd events ({hitStartCount}/{hitEndCount}).",
                    owner);
            }

            float chainStart = Mathf.Clamp01(cfg.chainWindowN.x);
            float chainEnd = Mathf.Clamp01(cfg.chainWindowN.y);
            if (chainEnd < chainStart)
                (chainStart, chainEnd) = (chainEnd, chainStart);

            bool hasChain = chainEnd > 0.0001f;

            if (hasChain)
            {
                runtimeEvents.Add(chainStart, () => owner.MeleeChainWindowOpened?.Invoke());
                runtimeEvents.Add(chainEnd, () => owner.MeleeChainWindowClosed?.Invoke());
            }

            runtimeEvents.OnEnd = () =>
            {
                if (owner.locomotionSM.CurrentState != owner.meleeCombo)
                    return;

                owner.MeleeStepCompleted?.Invoke();
            };

            state.SharedEvents = runtimeEvents;
        }

        internal void EndVfxSession()
        {
            owner.EndAnimationVfxSession(vfxSession);
            vfxSession = default;
        }
    }
}
