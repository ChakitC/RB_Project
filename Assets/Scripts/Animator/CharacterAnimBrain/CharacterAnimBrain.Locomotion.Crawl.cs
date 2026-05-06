using UnityEngine;
using System;
using Animancer;
using Animancer.FSM;
public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion : Downed  =====================
    
    private sealed class LocomotionState_Crawl : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private readonly Action onIntroEndCache;
        private AnimancerState introState;
        private Vector2MixerState mixerState;
        private Vector2 current;

        public LocomotionState_Crawl(CharacterAnimBrain owner)
        {
            this.owner = owner;
            onIntroEndCache = OnIntroEnd;
        }

        public override bool CanEnterState => owner.CrawlMixer != null;

        public override void OnEnterState()
        {
            current = Vector2.zero;

            if (owner.ConsumePendingCrawlIntro() &&
                owner.CrawlingClip != null &&
                owner.CrawlingClip.IsValid)
            {
                introState = owner.LocoLayer.Play(owner.CrawlingClip);
                introState.NormalizedTime = 0f;
                introState.Events(owner).OnEnd = onIntroEndCache;
                mixerState = null;
                return;
            }

            StartCrawlMixer();
        }

        public override void OnExitState()
        {
            if (introState != null)
            {
                introState.Events(owner).OnEnd = null;
                introState = null;
            }

            mixerState = null;
        }

        public override void Update()
        {
            if (mixerState == null) return;

            Vector2 dir = owner.MoveDirLocal;

            if (owner.SnapTo8Directions)
                dir = Snap8(dir);

            float speed01 = Mathf.Clamp01(owner.MoveSpeed01 * owner.CrawlSpeedMultiplier01);
            Vector2 target = dir * speed01;

            float t = 1f - Mathf.Exp(-owner.CrawlParamLerp * owner.AnimationDeltaTime);
            current = Vector2.Lerp(current, target, t);

            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
        }

        private void OnIntroEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            if (introState != null)
            {
                introState.Events(owner).OnEnd = null;
                introState = null;
            }

            StartCrawlMixer();
        }

        private void StartCrawlMixer()
        {
            mixerState = owner.LocoLayer.Play(owner.CrawlMixer) as Vector2MixerState;

            if (mixerState == null)
                return;

            current = Vector2.zero;
            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
        }

        private static Vector2 Snap8(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f) return Vector2.zero;

            v.Normalize();
            float angle = Mathf.Atan2(v.y, v.x);
            float step  = Mathf.PI * 0.25f;
            angle = Mathf.Round(angle / step) * step;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

}
