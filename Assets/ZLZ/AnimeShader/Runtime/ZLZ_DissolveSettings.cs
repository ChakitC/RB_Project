using UnityEngine;

namespace ZLZ.AnimeShader
{
    [CreateAssetMenu(fileName = "ZLZ_DissolveSettings", menuName = "ZLZ/FX Settings/Dissolve Settings")]
    public class ZLZ_DissolveSettings : ScriptableObject
    {
        // Defaults tuned for "dissolve out → hold dissolved → restore back" semantics.
        // Activate plays Intro (0 → 1 = dissolved), then holds. Deactivate plays Outro (1 → 0 = restored).
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings
        {
            loopCount     = 0,
            introDuration = 1.0f,
            loopPeriod    = 0f,      // no pulse — stays fully dissolved while in Loop
            outroDuration = 1.0f,
            loopCurve     = AnimationCurve.Constant(0f, 1f, 1f),
        };
    }
}
