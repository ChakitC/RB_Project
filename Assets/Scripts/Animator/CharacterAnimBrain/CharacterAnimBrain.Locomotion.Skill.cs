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
                owner.locomotionSM.TrySetState(owner.locomotion);
                return;
            }

            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: true,
                preserveFireHoldIntent: true);
            owner.ApplyActiveSkillRootMotionPolicy();
            owner.EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Started, owner._skillRequest.RequestId);

            var skillDef = owner._skillRequest.Definition;
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
                owner.locomotionSM.TrySetState(owner.IsDowned ? owner.crawlState : owner.locomotion);
                return;
            }

            _state = owner.LocoLayer.Play(skillClip);
            _state.NormalizedTime = 0f;
            _events = new AnimancerEvent.Sequence(skillClip.Events);

            if (owner.HasPendingSkillReleaseRequest)
            {
                owner.onSkillCastMomentCache ??= owner.NotifySkillCastMoment;
                _events.Add(owner.ActiveSkillCastPointNormalized, owner.onSkillCastMomentCache);
            }

            owner.BindActiveSkillTimelineEvents(_events);

            var origOnEnd = _events.OnEnd;
            _events.OnEnd = origOnEnd == null
                ? _onSkillEndCache
                : () => { origOnEnd(); _onSkillEndCache(); };

            _state.SharedEvents = _events;
        }

        public override void OnExitState()
        {
            _holdActive = false;
            _holdId = 0;

            if (_state != null)
            {
                _state.SharedEvents = null;
                _state = null;
                _events = null;
            }

            _inCutscenePhase = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
            owner.ClearActiveRootMotionPolicy();
            owner.NotifySkillStateExited(_completedNormally);
        }

        internal bool TryGetNormalizedTime(out float normalizedTime)
        {
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

        private void OnSkillEnd()
        {
            if (owner.locomotionSM.CurrentState != this) return;

            _completedNormally = true;

            if (owner.IsDowned)
                owner.locomotionSM.TrySetState(owner.crawlState);
            else
                owner.locomotionSM.TrySetState(owner.locomotion);
        }

        public override void Update()
        {
            if (_holdActive && _state != null)
                EnforceHold(_state.NormalizedTime);
        }

        internal bool TryBeginPreCastHold(int requestId, float speedMultiplier, float safetyMargin, out SkillPreCastHoldHandle handle)
        {
            handle = default;
            if (_inCutscenePhase || _state == null) return false;
            if (requestId != owner._skillRequest.RequestId) return false;

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
