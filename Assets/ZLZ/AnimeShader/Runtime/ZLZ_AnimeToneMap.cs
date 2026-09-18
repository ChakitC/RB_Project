using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZLZ.AnimeShader
{
    public enum ZLZToneCurveMode : int
    {
        AnimeCurve  = 0,
        FilmicCurve = 1,
    }

#if UNITY_6000_0_OR_NEWER
    [DisplayInfo(name = "ZLZ Anime Tone Mapping")]
#endif
    [Serializable, VolumeComponentMenu("ZLZ/Anime Tone Mapping")]
    public class ZLZ_AnimeToneMap : VolumeComponent, IPostProcessComponent
    {
        public ZLZToneCurveModeParameter CurveMode =
            new ZLZToneCurveModeParameter(ZLZToneCurveMode.AnimeCurve, true);

        // -- Anime Curve Exposure
        public ClampedFloatParameter Exposure     = new ClampedFloatParameter(1.0f,  0.1f, 8f,    true);
        public ClampedFloatParameter ToneMapScale = new ClampedFloatParameter(1.6f,  0.1f, 5.0f,  true);

        // -- Anime Curve
        public ClampedFloatParameter HighlightRolloff    = new ClampedFloatParameter(0.05f,  0.0f,   2.0f, true);
        public ClampedFloatParameter HighlightLift       = new ClampedFloatParameter(1.0f,   0.0f,   1.5f, true);
        public ClampedFloatParameter WhiteClip           = new ClampedFloatParameter(1.0f,   0.5f,   4.0f, true);
        public ClampedFloatParameter ShadowPedestal      = new ClampedFloatParameter(0.01f,  0.0f,   0.2f, true);
        public ClampedFloatParameter MidContrast         = new ClampedFloatParameter(0.45f,  0.0f,   2.0f, true);
        public ClampedFloatParameter SaturationRetention = new ClampedFloatParameter(0.243f, 0.0f,   1.5f, true);

        // -- Filmic Curve
        public ClampedFloatParameter ShoulderStrength    = new ClampedFloatParameter(0.22f,  0.0f,   2.0f,  true);
        public ClampedFloatParameter LinearStrength      = new ClampedFloatParameter(0.30f,  0.0f,   2.0f,  true);
        public ClampedFloatParameter LinearAngle         = new ClampedFloatParameter(0.10f,  0.0f,   2.0f,  true);
        public ClampedFloatParameter ToeStrength         = new ClampedFloatParameter(0.20f,  0.0f,   2.0f,  true);
        public ClampedFloatParameter BlackLevel          = new ClampedFloatParameter(0.01f,  0.0f,   1.0f,  true);
        public ClampedFloatParameter DenominatorBalance  = new ClampedFloatParameter(0.30f,  0.001f, 2.0f,  true);
        public ClampedFloatParameter WhitePoint          = new ClampedFloatParameter(11.2f,  0.1f,   20.0f, true);
        public ClampedFloatParameter FilmicSaturation    = new ClampedFloatParameter(1.15f,  0.0f,   3.0f,  true);

        // -- Filmic Curve Exposure
        public ClampedFloatParameter FilmicExposure     = new ClampedFloatParameter(1.0f, 0.1f, 8f,   true);
        public ClampedFloatParameter FilmicToneMapScale = new ClampedFloatParameter(1.6f, 0.1f, 5.0f, true);

        public ZLZ_AnimeToneMap()
        {
            // Unity 6+: display name is declared via [DisplayInfo] attribute above.
            // Unity 2022: the attribute does not exist, so assign the property directly.
#if !UNITY_6000_0_OR_NEWER
            displayName = "ZLZ Anime Tone Mapping";
#endif
        }

        public bool IsActive() => CurveMode.overrideState;

        [Obsolete("Unused #from(2023.1)", false)]
        public bool IsTileCompatible() => false;

        public bool IsFilmicMode => CurveMode.value == ZLZToneCurveMode.FilmicCurve;
    }

    [Serializable]
    public sealed class ZLZToneCurveModeParameter : VolumeParameter<ZLZToneCurveMode>
    {
        public ZLZToneCurveModeParameter(ZLZToneCurveMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}