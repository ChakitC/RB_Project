using UnityEngine;

namespace ZLZ.AnimeShader
{
    [CreateAssetMenu(fileName = "ZLZ_GetHitSettings", menuName = "ZLZ/FX Settings/GetHit Settings")]
    public class ZLZ_GetHitSettings : ScriptableObject
    {
        // Snappy defaults tuned for hit reactions — brief flash that auto-fades.
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings
        {
            loopCount     = 1,
            introDuration = 0.05f,
            loopPeriod    = 0.1f,
            outroDuration = 0.25f,
            loopCurve     = AnimationCurve.Constant(0f, 1f, 1f),
        };
    }
}
