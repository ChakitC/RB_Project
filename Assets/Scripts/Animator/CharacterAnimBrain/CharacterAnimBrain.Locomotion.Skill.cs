using System;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private sealed class Locomotion_Skill : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState _state;
        private AnimancerEvent.Sequence _events;
        private bool _prevApplyRootMotion;
        private bool _completedNormally;
        private bool _inCutscenePhase;
        private readonly Action _onCutsceneEndCache;
        private readonly Action _onSkillEndCache;
        private readonly System.Action<int> _raiseCutsceneVfxCueCache;

        private bool _holdActive;
        private int _holdId;
        private float _holdCeilingNormalized;
        private float _holdSpeedMultiplier;
        private float _originalStateSpeed = 1f;
        private bool _approachActive;
        private float _approachOriginalSpeed;
        private bool _impactApproachActive;
        private float _impactApproachStart, _impactApproachTarget, _impactApproachProgress;
        private int _impactArrivalFrame;
        private AnimancerState _enemyStruggleState;
        private float _enemyStruggleElapsed;
        private float _enemyStruggleAttackTime;
        private static int _nextHoldId = 1;

        public Locomotion_Skill(CharacterAnimBrain owner)
        {
            this.owner = owner;
            _onCutsceneEndCache = OnCutscenePhaseEnd;
            _onSkillEndCache = OnSkillEnd;
            _raiseCutsceneVfxCueCache = owner.RaiseCutsceneVfxCueInternal;
        }

        public override bool CanEnterState
        {
            get
            {
                if (!owner.HasActiveSkillClip) return false;
                if (owner.IsDowned) return false;
                if (owner.locomotionSM.CurrentState == owner.deadState) return false;
                return true;
            }
        }

        public override void OnEnterState()
        {
            _completedNormally = false;
            _inCutscenePhase = false;
            _state = null;
            _events = null;

            var skillClip = owner.SkillClip;
            if (skillClip == null || !skillClip.IsValid)
            {
                owner.TrySetLocomotionState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: !owner.IsBasicMeleeExecution);
            if (!owner.IsBasicMeleeExecution) owner.ApplyActiveSkillRootMotionPolicy();
            owner.EmitPlaybackSignal(owner.ActiveSkillPlaybackKind, PlaybackPhase.Started, owner._skillChannel.Request.RequestId);

            var skillDef = owner._skillChannel.Request.Definition;
            if (skillDef != null && skillDef.IsCutsceneSkill)
            {
                var def = skillDef.CutsceneDef;
                if (def?.characterCutsceneClip != null && def.characterCutsceneClip.IsValid)
                {
                    StartCutscenePhase(def);
                    return;
                }
            }

            StartMainSkillPhase();
        }

        private void StartCutscenePhase(CutsceneDef def)
        {
            _inCutscenePhase = true;
            owner.RaiseSkillTimelineEvent(CombatTimelineEventName.CutsceneSkillStart);

            _state = owner.LocoLayer.Play(def.characterCutsceneClip);
            _state.NormalizedTime = 0f;
            _events = new AnimancerEvent.Sequence(def.characterCutsceneClip.Events);

            if (def.cutsceneVfxEvents != null && def.cutsceneVfxEvents.Count > 0)
            {
                int boundCount = AnimationVfxEventBinder.Bind(_events, _raiseCutsceneVfxCueCache);
                if (boundCount == 0)
                    Debug.LogWarning(
                        $"[CharacterAnimBrain] Cutscene clip '{def.characterCutsceneClip.Clip?.name}' has VFX data but no 'Vfx' timeline event.",
                        owner);
            }

            var origOnEnd = _events.OnEnd;
            _events.OnEnd = origOnEnd == null
                ? _onCutsceneEndCache
                : () => { origOnEnd(); _onCutsceneEndCache(); };

            _state.SharedEvents = _events;
        }

        private void OnCutscenePhaseEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            _inCutscenePhase = false;
            _state = null;
            _events = null;

            owner.RaiseSkillTimelineEvent(CombatTimelineEventName.CutsceneSkillEnd);
            StartMainSkillPhase();
        }

        private void StartMainSkillPhase()
        {
            var skillClip = owner.SkillClip;
            if (skillClip == null || !skillClip.IsValid)
            {
                _completedNormally = true;
                owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                return;
            }

            _state = owner.LocoLayer.Play(skillClip);
            _state.NormalizedTime = 0f;
            _events = new AnimancerEvent.Sequence(skillClip.Events);

            if (owner.HasPendingSkillReleaseRequest)
            {
                int releaseRequestId = owner._skillChannel.Request.RequestId;
                _events.Add(owner.ActiveSkillCastPointNormalized, () =>
                {
                    if (owner._skillChannel.Request.RequestId == releaseRequestId) owner.NotifySkillCastMoment();
                });
            }

            owner.BindActiveSkillTimelineEvents(_events);

            var origOnEnd = _events.OnEnd;
            int endingRequestId = owner._skillChannel.Request.RequestId;
            _events.OnEnd = () =>
            {
                if (owner._skillChannel.Request.RequestId != endingRequestId) return;
                origOnEnd?.Invoke();
                if (owner._skillChannel.Request.RequestId == endingRequestId) _onSkillEndCache();
            };

            if (owner.IsBasicMeleeExecution)
            {
                float duration = owner._skillChannel.Request.Definition.baseCastTime;
                _state.Speed = duration > 0.01f ? Mathf.Max(0.01f, _state.Length) / duration : 1f;
                int requestId = owner._skillChannel.Request.RequestId;
                Vector2 window = owner._skillChannel.Request.MeleeChainWindow;
                float start = Mathf.Clamp01(Mathf.Min(window.x, window.y));
                float end = Mathf.Clamp01(Mathf.Max(window.x, window.y));
                if (end > 0.0001f)
                {
                    _events.Add(start, () => owner.RaiseMeleeChainWindow(requestId, true));
                    _events.Add(end, () => owner.RaiseMeleeChainWindow(requestId, false));
                }
            }
            _state.SharedEvents = _events;
        }

        public override void OnExitState()
        {
            ReleaseEnemyStruggle();
            _holdActive = false;
            _holdId = 0;

            if (_state != null)
            {
                if (_approachActive) _state.Speed = _approachOriginalSpeed;
                _state.SharedEvents = null;
                _state = null;
                _events = null;
            }

            _approachActive = false;
            _impactApproachActive = false;

            _inCutscenePhase = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.ClearActiveRootMotionPolicy();
            owner.NotifySkillStateExited(_completedNormally);
        }

        internal bool TryGetNormalizedTime(out float normalizedTime)
        {
            if (_enemyStruggleState != null)
            {
                // Fading the original state out may stop/reset its cached clip. Request
                // time remains the accepted impact time, never the looping presentation.
                normalizedTime = _enemyStruggleAttackTime;
                return true;
            }
            if (_inCutscenePhase)
            {
                normalizedTime = 0f;
                return _state != null;
            }

            if (_state != null)
            {
                normalizedTime = _state.NormalizedTime;
                return true;
            }

            normalizedTime = 0f;
            return false;
        }

        internal bool TryBeginApproach(float endNormalized, float duration)
        {
            if (_approachActive || _holdActive || _inCutscenePhase || _state == null ||
                _state.Length <= 0f || endNormalized <= _state.NormalizedTime + .01f) return false;
            _approachOriginalSpeed = _state.Speed;
            // Keep normal motion when possible; a late command stretches the remaining charge
            // segment instead of playing the following strike or freezing on a single pose.
            float end = Mathf.Min(endNormalized, _state.NormalizedTime + duration * _approachOriginalSpeed / _state.Length);
            _state.Speed = (end - _state.NormalizedTime) * _state.Length / duration;
            _approachActive = true;
            return true;
        }

        internal bool TryEndApproach()
        {
            if (!_approachActive || _state == null || _enemyStruggleState != null) return false;
            _state.Speed = _approachOriginalSpeed;
            _approachActive = false;
            _impactApproachActive = false;
            return true;
        }

        internal bool CanBeginImpactApproach(float impactNormalized) =>
            !_approachActive && !_inCutscenePhase && _state != null && _state.Length > 0f &&
            float.IsFinite(impactNormalized) && impactNormalized > _state.NormalizedTime + .001f &&
            impactNormalized < _state.NormalizedEndTime;

        internal bool TryBeginImpactApproach(float impactNormalized)
        {
            if (_holdActive || !CanBeginImpactApproach(impactNormalized)) return false;
            _approachOriginalSpeed = _state.Speed;
            _impactApproachStart = _state.NormalizedTime;
            _impactApproachTarget = impactNormalized;
            _impactApproachProgress = 0f;
            _impactArrivalFrame = -1;
            _approachActive = _impactApproachActive = true;
            // The approach motor supplies the clock. MoveTime preserves intervening timeline
            // events and prevents low-frame-rate overshoot into the strike or clip end.
            _state.Speed = 0f;
            return true;
        }

        internal bool TryAdvanceImpactApproach(float progress, out bool ready)
        {
            ready = false;
            if (!_impactApproachActive || !_approachActive || _state == null || !float.IsFinite(progress)) return false;
            progress = Mathf.Clamp01(progress);
            if (progress > _impactApproachProgress)
            {
                _impactApproachProgress = progress;
                _state.MoveTime(Mathf.Lerp(_impactApproachStart, _impactApproachTarget, progress), true);
                if (progress >= 1f) _impactArrivalFrame = Time.frameCount;
            }
            // Allow the graph and its event invoker to observe the final advance before
            // resolving/stopping the request, including when called from LateUpdate.
            ready = _impactArrivalFrame >= 0 && Time.frameCount > _impactArrivalFrame + 1;
            return true;
        }

        internal bool TryHoldApproach()
        {
            if (!_approachActive || _state == null) return false;
            _state.Speed = 0f;
            return true;
        }

        internal bool TryBeginEnemyStruggle(AnimationClip clip, float fadeSeconds)
        {
            if (!_approachActive || _state == null || _state.Speed != 0f) return false;
            if (_enemyStruggleState != null) return _enemyStruggleState.Clip == clip;
            if (clip == null) return true;
            if (clip.legacy || clip.length <= 0f || !float.IsFinite(fadeSeconds) || fadeSeconds < 0f) return false;
            // Use a distinct state even if the author chose the attack clip itself. The
            // original state remains frozen for request timing; no skill events are rebound.
            _enemyStruggleAttackTime = _state.NormalizedTime;
            _enemyStruggleState = owner.LocoLayer.Play(new ClipState(clip), fadeSeconds);
            _enemyStruggleState.SharedEvents = null;
            _enemyStruggleState.Speed = 0f;
            _enemyStruggleState.NormalizedTime = 0f;
            _enemyStruggleElapsed = 0f;
            return true;
        }

        void ReleaseEnemyStruggle()
        {
            var state = _enemyStruggleState;
            _enemyStruggleState = null;
            _enemyStruggleElapsed = 0f;
            state?.Destroy();
        }

        internal bool TryGetPlaybackTiming(out float remainingDuration, out float totalDuration)
        {
            remainingDuration = 0f;
            totalDuration = 0f;

            // A cutscene skill continues into its main skill clip, so the cutscene phase alone is
            // not a valid point from which to schedule the actor's final fade-out.
            if (_inCutscenePhase || _state == null || !_state.IsPlaying)
                return false;

            float speed = Mathf.Abs(_state.Speed);
            if (speed < 0.0001f)
                return false;

            remainingDuration = _state.RemainingDuration;
            totalDuration = _state.Length / speed;
            return float.IsFinite(remainingDuration) && float.IsFinite(totalDuration);
        }

        private void OnSkillEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            _completedNormally = true;

            if (owner.IsDowned)
                owner.TrySetLocomotionState(owner.crawlState);
            else
                owner.TrySetLocomotionState(owner.locomotion);
        }

        public override void Update()
        {
            if (_enemyStruggleState != null)
            {
                _enemyStruggleElapsed = Mathf.Repeat(_enemyStruggleElapsed + owner.AnimationDeltaTime, _enemyStruggleState.Length);
                // Sampling skips both authored events and root displacement in the loop.
                _enemyStruggleState.NormalizedTime = _enemyStruggleElapsed / _enemyStruggleState.Length;
            }
            if (_holdActive && _state != null)
                EnforceHold(_state.NormalizedTime);
        }

        internal bool TryBeginPreCastHold(int requestId, float speedMultiplier, float safetyMargin, out SkillPreCastHoldHandle handle)
        {
            handle = default;
            if (_inCutscenePhase || _state == null) return false;
            if (requestId != owner._skillChannel.Request.RequestId) return false;

            float castPoint = owner.ActiveSkillCastPointNormalized;
            float currentNt = _state.NormalizedTime;
            float margin = Mathf.Max(0.005f, safetyMargin);
            float ceiling = Mathf.Max(castPoint - margin, currentNt);

            _holdActive = true;
            _holdId = _nextHoldId++;
            _holdCeilingNormalized = ceiling;
            _holdSpeedMultiplier = Mathf.Clamp01(speedMultiplier);
            _originalStateSpeed = _state.Speed;

            EnforceHold(currentNt);
            handle = new SkillPreCastHoldHandle(requestId, _holdId);
            return true;
        }

        internal void ReleasePreCastHold(SkillPreCastHoldHandle handle)
        {
            if (!_holdActive || handle.HoldId != _holdId) return;
            if (_state != null) _state.Speed = _originalStateSpeed;
            _holdActive = false;
            _holdId = 0;
        }

        private void EnforceHold(float currentNt)
        {
            if (_state == null) return;
            if (currentNt >= _holdCeilingNormalized)
            {
                _state.NormalizedTime = _holdCeilingNormalized;
                _state.Speed = 0f;
            }
            else
            {
                _state.Speed = _originalStateSpeed * _holdSpeedMultiplier;
            }
        }
    }
}
