using Animancer;

public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Stage Intro =========================
    // Exclusive, always root-motion-free pose used by the MapRun stage intro.
    // The camera clip owns the intro duration, so a shorter character clip holds its last frame.

    private sealed class Locomotion_StageIntro : LocomotionState
    {
        private readonly CharacterAnimBrain owner;
        private AnimancerState state;

        public Locomotion_StageIntro(CharacterAnimBrain owner) => this.owner = owner;

        public override void OnEnterState()
        {
            owner.EnterExclusiveLocomotion(
                usesRootMotion: false,
                preserveFireHoldIntent: false);
            owner.EmitPlaybackSignal(PlaybackKind.StageIntro, PlaybackPhase.Started, 0);

            ClipTransition clip = owner.StageIntroClip;
            if (clip == null || !clip.IsValid)
            {
                // No authored pose: fall back to the locomotion idle blend, but stay exclusive so
                // aim/fire/root motion cannot leak into the intro.
                state = null;
                if (owner.LocomotionMixer != null)
                    owner.LocoLayer.Play(owner.LocomotionMixer);
                return;
            }

            state = owner.LocoLayer.Play(clip);

            owner.onStageIntroEndCache ??= () =>
            {
                if (state == null) return;
                state.Time = state.Length;
                state.Speed = 0f;
            };

            state.Events(owner).OnEnd = owner.onStageIntroEndCache;
        }

        public override void OnExitState()
        {
            state = null;
        }
    }
}
