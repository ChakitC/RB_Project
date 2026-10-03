using Animancer;

public sealed partial class CharacterAnimBrain
{
    // ===================== Locomotion: Stage Intro =========================
    // Exclusive, always root-motion-free pose used by the MapRun stage intro.
    // The camera clip owns the intro duration, so a shorter character clip holds its last frame.

    private sealed class Locomotion_StageIntro : SingleClipLocomotionState
    {
        private bool _prevApplyRootMotion;

        public Locomotion_StageIntro(CharacterAnimBrain owner) : base(owner) { }

        protected override void EnterPlayback()
        {
            _prevApplyRootMotion = owner.EnterExclusiveLocomotion(
                usesRootMotion: false,
                preserveFireHoldIntent: false);
            owner.EmitPlaybackSignal(PlaybackKind.StageIntro, PlaybackPhase.Started, 0);

            ClipTransition clip = owner.StageIntroClip;
            if (clip == null || !clip.IsValid)
            {
                // No authored pose: fall back to the locomotion idle blend, but stay exclusive so
                // aim/fire/root motion cannot leak into the intro.
                if (owner.LocomotionMixer != null)
                    owner.presentation.PlayLocomotionTransition(owner.LocomotionMixer);
                return;
            }

            // Cut, never cross-fade. Blending out of the locomotion mixer interpolates the root and
            // hip rotation between two unrelated poses, so the character visibly swings around into
            // the intro pose. The intro always begins under a fully black overlay, so a hard cut is
            // invisible — and the transition's own fade (0.25s) outlasts the 0.2s fade-in, which is
            // exactly why the tail of that swing was showing on screen.
            playback.Play(new AnimationPlaybackOptions
            {
                Transition = clip, FadeDuration = 0f, FadeMode = FadeMode.FromStart, HoldLastFrame = true,
            });
        }

        protected override void ExitPlayback()
        {
            // The intro forces root motion off while it owns locomotion; hand it back to whoever
            // had it (NavMesh root-motion drivers, an interrupted skill) instead of leaking off.
            owner.ExitExclusiveLocomotion(_prevApplyRootMotion);
        }
    }
}
