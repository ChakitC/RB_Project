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
        private int step;
        private int bufferedPresses;
        private bool windowExpired;
        private bool _chainOpen;
        private bool _pressedInWindow;
        private bool _hasChain;
        private float _cs;
        private float _ce;
        private MeleeComboSO.Step _cfg;
        private AnimationVfxSessionToken vfxSession;

        public MeleeComboSO CurrentCombo => comboLocked;

        internal AnimancerState DebugState => state;
        internal int DebugBufferedPresses => bufferedPresses;
        internal bool DebugChainWindowOpen => _chainOpen;
        internal bool DebugPressedInWindow => _pressedInWindow;
        internal bool DebugWindowExpired => windowExpired;
        internal float DebugChainWindowStart => _cs;
        internal float DebugChainWindowEnd => _ce;

        public Locomotion_MeleeCombo(CharacterAnimBrain owner) => this.owner = owner;

        public override bool CanEnterState
        {
            get
            {
                var combo = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
                return combo != null && combo.IsValid(out _);
            }
        }

        public void SetCombo(MeleeComboSO newCombo)
        {
            comboLocked = newCombo;
        }

        public void QueueNextPress()
        {
            var combo = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            if (combo == null)
                return;

            int last = combo.Steps.Count - 1;
            bool canRepeat = CanRepeatLastStep(last);
            int maxRemaining = last - step;
            if (maxRemaining <= 0 && !canRepeat)
                return;

            if (windowExpired && _cfg.dropBufferOnWindowExpire)
                return;

            bufferedPresses = canRepeat
                ? Mathf.Min(1, bufferedPresses + 1)
                : Mathf.Min(maxRemaining, bufferedPresses + 1);

            if (_hasChain && _chainOpen && state != null)
            {
                float normalizedTime = state.NormalizedTime;
                if (normalizedTime >= _cs && normalizedTime <= _ce)
                {
                    _pressedInWindow = true;
                    Advance();
                }
            }
        }

        public override void OnEnterState()
        {
            if (comboLocked == null)
                comboLocked = owner.DefaultMeleeCombo;

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: false);
            owner.EmitPlaybackSignal(PlaybackKind.Melee, PlaybackPhase.Started, 0);

            step = 0;
            bufferedPresses = 0;
            windowExpired = false;

            PlayStep(0);
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

        private void PlayStep(int newStep)
        {
            EndVfxSession();
            step = newStep;
            windowExpired = false;

            var combo = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            if (combo == null)
            {
                EndComboSafe();
                return;
            }

            var steps = combo.Steps;
            if (step < 0 || step >= steps.Count)
            {
                EndComboSafe();
                return;
            }

            var cfg = steps[step];
            _cfg = cfg;

            if (cfg.clip == null)
            {
                EndComboSafe();
                return;
            }

            owner.CurrentMeleeStep = cfg;
            owner.CurrentMeleeStepIndex = step;

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

            int currentStepIndex = step;
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

            int last = steps.Count - 1;
            float chainStart = Mathf.Clamp01(cfg.chainWindowN.x);
            float chainEnd = Mathf.Clamp01(cfg.chainWindowN.y);
            if (chainEnd < chainStart)
                (chainStart, chainEnd) = (chainEnd, chainStart);

            _cs = chainStart;
            _ce = chainEnd;

            bool repeatLast = step == last && chainEnd > 0.0001f;
            _hasChain = (step < last || repeatLast) && chainEnd > 0.0001f;
            _chainOpen = false;
            _pressedInWindow = false;

            if (_hasChain)
            {
                runtimeEvents.Add(_cs, () =>
                {
                    _chainOpen = true;
                    _pressedInWindow = false;

                    if (bufferedPresses > 0)
                    {
                        _pressedInWindow = true;
                        Advance();
                    }
                });

                runtimeEvents.Add(_ce, () =>
                {
                    _chainOpen = false;
                    windowExpired = true;

                    if (cfg.dropBufferOnWindowExpire && !_pressedInWindow)
                        bufferedPresses = 0;
                });
            }
            else
            {
                windowExpired = true;
            }

            owner.onMeleeEndCache = () =>
            {
                Debug.Log($"[MeleeCombo OnEnd] current={owner.locomotionSM.CurrentState}", owner);

                if (owner.locomotionSM.CurrentState != owner.meleeCombo)
                    return;

                var currentCombo = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
                int lastStep = currentCombo.Steps.Count - 1;
                bool canRepeat = CanRepeatLastStep(lastStep);

                if (bufferedPresses > 0 && (step < lastStep || canRepeat))
                {
                    Advance();
                    return;
                }

                EndComboSafe();
            };

            var transitionOnEnd = runtimeEvents.OnEnd;
            runtimeEvents.OnEnd = transitionOnEnd == null
                ? owner.onMeleeEndCache
                : () =>
                {
                    transitionOnEnd();
                    owner.onMeleeEndCache();
                };

            state.SharedEvents = runtimeEvents;
        }

        internal void EndVfxSession()
        {
            owner.EndAnimationVfxSession(vfxSession);
            vfxSession = default;
        }

        private void Advance()
        {
            var combo = comboLocked != null ? comboLocked : owner.DefaultMeleeCombo;
            if (combo == null)
                return;

            int last = combo.Steps.Count - 1;
            bool canRepeat = CanRepeatLastStep(last);

            if (step >= last)
            {
                if (!canRepeat)
                    return;

                bufferedPresses = Mathf.Max(0, bufferedPresses - 1);
                PlayStep(step);
                return;
            }

            bufferedPresses = Mathf.Max(0, bufferedPresses - 1);
            PlayStep(step + 1);
        }

        private bool CanRepeatLastStep(int lastIndex)
        {
            return step == lastIndex && _cfg.chainWindowN.y > 0.0001f;
        }

        private void EndComboSafe()
        {
            EndVfxSession();
            owner.MeleeComboEnded?.Invoke();
            owner.EmitPlaybackSignal(PlaybackKind.Melee, PlaybackPhase.Completed, 0);

            bool exited = owner.locomotionSM.TrySetState(owner.locomotion);
            Debug.Log($"[MeleeCombo] EndComboSafe -> locomotion ok={exited}", owner);

            if (!exited)
            {
                owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
                Debug.LogWarning("[MeleeCombo] Failed to exit meleeCombo", owner);
            }
        }
    }
}
