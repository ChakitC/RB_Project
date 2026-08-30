using UnityEngine;

namespace ZLZ.AnimeShader
{
    [CreateAssetMenu(fileName = "ZLZ_DarkenSettings", menuName = "ZLZ/FX Settings/Darken Settings")]
    public class ZLZ_DarkenSettings : ScriptableObject
    {
        // Defaults tuned for cinematic darken — held steady at 1 while active.
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings
        {
            loopCount     = 0,
            introDuration = 0.6f,
            loopPeriod    = 0f,
            outroDuration = 0.6f,
            loopCurve     = AnimationCurve.Constant(0f, 1f, 1f),
        };
    }
}
