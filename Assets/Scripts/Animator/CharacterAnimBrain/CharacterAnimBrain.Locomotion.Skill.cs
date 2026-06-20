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
            owner.EmitPlaybackSignal(PlaybackKind.Skill, PlaybackPhase.Started, owner._activeSkillRequestId);

            var skillDef = owner._activeSkillDefinition;
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
            if (_state != null)
            {
                _state.SharedEvents = null;
                _state = null;
                _events = null;
            }

            _inCutscenePhase = false;
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
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
    }
}
