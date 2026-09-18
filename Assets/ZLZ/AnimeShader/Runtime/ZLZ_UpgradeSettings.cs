using UnityEngine;

namespace ZLZ.AnimeShader
{
    [CreateAssetMenu(fileName = "ZLZ_UpgradeSettings", menuName = "ZLZ/FX Settings/Upgrade Settings")]
    public class ZLZ_UpgradeSettings : ScriptableObject
    {
        public ZLZ_FXAnimationSettings animation = new ZLZ_FXAnimationSettings();
    }
}
