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
        private readonly AnimationPlayback introPlayback;
        private Vector2MixerState mixerState;
        private Vector2 current;

        public LocomotionState_Crawl(CharacterAnimBrain owner)
        {
            this.owner = owner;
            introPlayback = owner.CreatePlayback();
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
                introPlayback.Play(new AnimationPlaybackOptions
                {
                    Transition = owner.CrawlingClip, NormalizedStart = 0f,
                }, onEnd: onIntroEndCache);
                mixerState = null;
                return;
            }

            StartCrawlMixer();
        }

        public override void OnExitState()
        {
            introPlayback.Stop();

            mixerState = null;
        }

        public override void Update()
        {
            if (mixerState == null) return;

            current = AnimationMixerInput.Smooth(current, owner.MoveDirLocal, owner.MoveSpeed01 * owner.CrawlSpeedMultiplier01,
                owner.SnapTo8Directions, owner.CrawlParamLerp, owner.AnimationDeltaTime);

            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
        }

        private void OnIntroEnd()
        {
            if (owner.locomotionSM.CurrentState != this)
                return;

            introPlayback.Stop();

            StartCrawlMixer();
        }

        private void StartCrawlMixer()
        {
            mixerState = owner.presentation.PlayLocomotionTransition(owner.CrawlMixer) as Vector2MixerState;

            if (mixerState == null)
                return;

            current = Vector2.zero;
            mixerState.ParameterX = current.x;
            mixerState.ParameterY = current.y;
        }


    }

}
