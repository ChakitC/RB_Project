using UnityEngine;

namespace ZLZ.AnimeShader
{
    [CreateAssetMenu(fileName = "ZLZ_IndicatorSettings", menuName = "ZLZ/FX Settings/Indicator Settings")]
    public class ZLZ_IndicatorSettings : ScriptableObject
    {
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings();
    }
}
