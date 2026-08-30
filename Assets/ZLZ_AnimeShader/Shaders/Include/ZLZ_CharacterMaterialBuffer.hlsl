// Dither is always compiled now — the per-material _DITHER keyword / shader_feature
// was removed because it lived on shared materials as per-character state and churned
// .mat files on every save. This define keeps the existing `#if defined(_DITHER_ON)`
// guards valid without a keyword/variant; the dither path early-outs when its alphas
// are 0, so the cost when a character isn't dithering is negligible. (This header is
// included before any pass's `#if defined(_DITHER_ON)`, including ZLZ_Dither.hlsl.)
#ifndef _DITHER_ON
#define _DITHER_ON
#endif

CBUFFER_START(UnityPerMaterial)
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Settings
    float _Cutoff;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Base Character
    float4 _MainTex_ST;
    float4 _Texture2DDissolve_ST;
    float _Switch_to_Face_Mode;
    float4 _FaceTex_ST;
    float _ReceiveShadow;
    float _ShadowSoftness;
    float _ShadowEdgeSmooth;
    float _SelfShadowRejectDist;
    float _Texture_Brightness;
    float4 _BaseColor;
    float4 _Shadow_Color;
    float _AdditionalLightIntensity;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Face Shadow
    float _DebugUvFace;
    float _FaceShadowUVScale;
    float _FaceShadowUVOffsetX;
    float _FaceShadowUVOffsetY;
    float _FlipUvFace;
    // float _FaceForwardOS;
    // float _FaceRightOS;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // ToonRampSmoothness
    float _ToonRampSmoothness;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Shadow Edge (core shadow / terminator darkening band)
    float4 _ShadowEdgeColor;
    float  _ShadowEdgeIntensity;
    float  _ShadowEdgeWidth;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Soft BlendBaseColor
    float _SoftLightHighlight;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Rim Light
    float _RimLight;
    float4 _RimColor;
    float _IntensityRimLight;
    float _StepRim;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Metallic
    float4 _MetalNormalMap_ST;
    float _MetalIntensity;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Hair Highlight
    float4 _ColorHair;
    float _Hair_HighlightValue;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Specular (Stylized Blinn-Phong, toon-stepped)
    float4 _SpecularColor;
    float  _SpecularIntensity;
    float  _SpecularSharpness;
    float  _SpecularThreshold;
    float  _SpecularToonStep;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Emissive Glow
    float4 _Emissive_Color;
    float _Emissive_Intensity;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Outline
    float _OutlineWidth;
    float _OutlineIntensity;
    float4 _OutlineColor;
    float _OutlineZOffset;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Dissolve Character
    float4 _DissolveColor;
    float _DissolveValue;
    float _SizeGlowDissolve;
    float _StartDissolve;
    float _EndDissolve;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Dither
    float _DitherAlpha;
    float _DitherOcclusionAlpha;
    float _DitherCameraEnable;
    float _DitherCameraNear;
    float _DitherCameraFar;
    // Character-level anchor in world space — set per-frame by ZLZ_CharacterVFX.LateUpdate
    // to the character root's transform.position. Camera-near fade measures distance from
    // this single anchor so the whole character fades uniformly (instead of front-pixels
    // fading more than back-pixels which would let the camera see through).
    // Falls back to UNITY_MATRIX_M translation in the shader when xyz == 0 (no VFX setup).
    float4 _CharacterAnchorWS;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Character Darkening
    float _TargetDarkenIntensity;
    float _TargetDarkenLocal;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Indicator
    float _IndicatorStrength;
    float4 _IndicatorColor;
    float _FresnelPowerIndicator;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // GetHit
    float _GetHitStrength;
    float4 _GetHitColor;
    float _FresnelPowerHit;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Light Sweep
    float _LightSweepIntensity;
    float _LightSweepDuration;
    float _LightSweepDelay;
    float _LightSweepWidth;
    float _LightSweepSoftness;
    float _LightSweepStart;
    float _LightSweepEnd;
    float _LightSweepDirX;
    float _LightSweepDirY;
    float _LightSweepDirZ;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Upgrade Weapon
    float _UpgradeActive;
    float4 _UpgradeColor;
    float _UpgradeIntensity;
    float _UpgradeMinBrightness;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Alpha Channel Transparent
    float _AlphaValue;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Head Transparent
    float _HeadBackCutoff;
    float _HeadTopCutoff;
    float _HairFadeRange;
    float4 _HeadCenterWS;
    float4 _HeadForwardWS;
    float4 _HeadRightWS;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Normal Map
    float4 _NormalMap_ST;
    float  _NormalStrength;
    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
    // Mask Channel Mapping (idx 0..7 -> M1.r/g/b/a, M2.r/g/b/a)
    float _MetallicMaskCh;
    float _HairHighlightMaskCh;
    float _EmissiveMaskCh;
    float _OutlineMaskCh;
    float _SpecularMaskCh;
CBUFFER_END