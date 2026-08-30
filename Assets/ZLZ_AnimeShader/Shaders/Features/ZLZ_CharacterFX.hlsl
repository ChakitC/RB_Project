#ifndef ZLZ_CHARACTERFX_INCLUDED
#define ZLZ_CHARACTERFX_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_CharacterFX.hlsl
// Character FX Utilities for Anime Shader
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Character Darkening (Global × Local Control)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Example usage with 2 characters:
//
// Global Darken  = Controls the darkness level for the entire game (set via Script)
//    0 = normal brightness
//    1 = fully darkened
//
// Local Darken   = Controls the darkness for this specific character
//    0 = stays bright (ignores Global darken)
//    1 = darkens according to the Global value
//
// _TargetDarkenIntensity = Adjusts how strong the darkening effect is (lower values = darker result)
//
// Character 1: Local = 1   // Will darken according to Global
// Character 2: Local = 0   // Will stay bright and unaffected by Global
//
// Final darken calculation:
//    lerp(BaseColor, BaseColor * Intensity, Global * Local)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

inline half3 ZLZ_ApplyCharacterDarken(
    half3 BaseDiss,
    half  _TargetDarkenIntensity,
    half  _TargetDarkenLocal,
    half  _TargetDarkenGlobal
)
{
    #if defined(_TARGETDARKEN_ON)
        half3 basedarken    = BaseDiss * _TargetDarkenIntensity;
        half  switchdarken  = saturate(_TargetDarkenLocal * _TargetDarkenGlobal);
        half3 finalCharDarken = lerp(BaseDiss, basedarken, switchdarken);
        return finalCharDarken;
    #else
        return BaseDiss;
    #endif
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Shared Fresnel – A shared Fresnel calculation used by both Indicator and GetHit effects
//
// This function computes a Fresnel value based on the view angle and shares the result
// with both the Indicator and GetHit features. By using a single Fresnel calculation,
// these effects remain visually consistent while reducing redundant computations
// and improving overall performance.
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline half2 ZLZ_ComputeSharedFresnel(half3 N_main, half3 viewDirWS)
{
    half dotNV = saturate(dot(N_main, viewDirWS));   
    half2 Paramfreshnel = half2(_FresnelPowerIndicator, _FresnelPowerHit);
    half2 fresAiming = pow(1.0h - dotNV, Paramfreshnel.xy); // x = Indicator, y = GetHit
    return fresAiming;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Indicator – Highlight Effect for Emphasizing Special States or Visual Cues
//
// This function applies a highlight effect on the surface to make the object stand out.
// It is commonly used to indicate special states such as aiming mode, selection highlights,
// detection effects, or positional indicators in Anime / Stylized visuals.
//
//
// Important Parameters:
// - _IndicatorStrength : Controls the effect intensity (0 = off, 1 = full)
// - fresIndicator      : Fresnel-based value controlling the shape and visibility of the effect depending on the camera viewing angle
// - _IndicatorColor    : Highlight color used for the Indicator effect
//
//
// Recommended For:
// - Aiming highlights, selection indicators, scanning effects, positional markers, or any other readable visual cue applied on 3D models
// - Anime, Action, and Stylized games that require clear and expressive visual indicators
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

inline half3 ZLZ_ApplyIndicator(half3 BaseDiss, half fresIndicator)
{
    #if defined(_INDICATOR_ON)
        half3 MHLcolor_Indicator = fresIndicator * _IndicatorColor.rgb;
        half3 AHAim_Indicator    = BaseDiss + MHLcolor_Indicator;

        return lerp(BaseDiss, AHAim_Indicator, _IndicatorStrength);
    #else
        return BaseDiss;
    #endif
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// GetHit – Light Effect Triggered When an Object Is Hit (Hit Reaction Effect)
//
// This function creates a glowing or reflective highlight when the object takes damage.
// It is commonly used to enhance player feedback, such as a flash on characters,
// a bright highlight on weapons, or a colored impact effect in Anime / Stylized games.
//
//
// Important Parameters:
// - _GetHitStrength : Controls the effect intensity (0 = off, 1 = full)
// - fresGetHit      : Fresnel-based value used to shape and control the strength of the effect
// - _GetHitColor    : Tint color applied when the object is hit
//
// Recommended For:
// - Characters, weapons, enemies, robots, and any objects requiring hit feedback
// - Anime, Action, and Stylized games that benefit from clear and customizable hit reactions
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

inline half3 ZLZ_ApplyGetHit(half3 FinalIndicator, half fresGetHit)
{
    #if defined(_GETHIT_ON)
        half3 MHLcolor_GetHit = fresGetHit * _GetHitColor.rgb;
        half3 AHAim_GetHit    = FinalIndicator + MHLcolor_GetHit;

        return lerp(FinalIndicator, AHAim_GetHit, _GetHitStrength);
    #else
        return FinalIndicator;
    #endif
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Light Sweep – Anime / Stylized Moving Light Band Effect
//
// This function creates a “moving light band” effect across the surface of an object,
// using its Object Space position as the foundation. It is ideal for weapons, items,
// anime-style models, and stylized assets that require visually striking light motion such as:
// - A diagonal light streak sweeping across a sword
// - Light passing over armor or mechanical parts
// - A scanning light that moves across the surface
//
// Key Features:
// - Light direction is controlled by three independent sliders (X/Y/Z) without normalization,
//   allowing artists to adjust the sweep direction directly and intuitively.
// - Easy to customize, suitable for anime and stylized visuals that require a bold sweeping highlight.
// - Smooth and natural blending achieved through smoothstep shaping.
//
// Important Parameters:
// - _LightSweepDuration  : Controls the sweep speed (time for one full cycle)
// - _LightSweepDelay     : Pause duration before starting the next cycle
// - _LightSweepStart/End : Sweep start and end positions
// - _LightSweepDirX/Y/Z  : Sweep direction (not normalized, preserving the artist-defined ratios)
// - _LightSweepWidth     : Controls the thickness of the light band
// - _LightSweepSoftness  : Adjusts the softness of the sweep edge
// - _LightSweepIntensity : Light band brightness
//
// Recommended For:
// - Swords, guns, energy weapons, anime-style models, robots, upgrade effects,
//   and any stylized assets that benefit from a dynamic sweeping light highlight.
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

inline half3 ZLZ_LightSweep(float3 positionOS, half3 FinaGetHit)
{
    // SetTimeLightSweep
    half SetTimeSweep      = _LightSweepDuration + _LightSweepDelay;
    half LightSweepModulo  = fmod(_Time.y, SetTimeSweep);
    half ClampLightSweep   = clamp(LightSweepModulo - _LightSweepDelay, 0, _LightSweepDuration);
    half LoopLightSweep    = frac(saturate(ClampLightSweep / _LightSweepDuration));
    half StartEndLightSweep = lerp(_LightSweepStart, _LightSweepEnd, LoopLightSweep - 0.5h);

    // SetLightSweep
    float3 objectPosition  = positionOS;
    half3 sweepDirOS       = half3(_LightSweepDirX, _LightSweepDirY, _LightSweepDirZ);
    half SetRotation       = dot(objectPosition, sweepDirOS);
    half ABSLightSweep     = abs(SetRotation - StartEndLightSweep);
    half SetSoftness       = _LightSweepWidth * (1.0h - _LightSweepSoftness);
    half ShapeLightSweep   = smoothstep(_LightSweepWidth, SetSoftness, ABSLightSweep);

    half IntensityLight    = _LightSweepIntensity * ShapeLightSweep;
    half3 BlendDiffuse     = IntensityLight.xxx * FinaGetHit;
    half3 FinalLightSweep  = lerp(FinaGetHit, BlendDiffuse, ShapeLightSweep.xxx);

    return FinalLightSweep;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Upgrade Weapon – A highlight effect that enhances brightness and visual emphasis for weapons or items.
//
// This function adds an “upgrade” effect on top of a previously calculated Base value.
// It can be used to create standout visuals such as a bright sweep across a weapon
// or a glowing effect when the weapon is in a powered-up or special state.
//
// Concept:
// 1. Use the Base lighting value as the foundation of the effect.
// 2. Enforce a minimum brightness using _UpgradeMinBrightness to ensure the effect remains visible.
// 3. Apply the upgrade tint color using _UpgradeColor.
// 4. Multiply the result by _UpgradeIntensity to strengthen the visual impact.
// 5. Activate or adjust the effect using _UpgradeActive (0 = off, 1 = full).
//
// Activation:
// - This feature is executed only when the Shader Keyword _UPGRADE_ON is enabled.
// - If the keyword is disabled, the shader will not compute this effect.
//
// Parameters:
// - _UpgradeActive        : Controls the intensity or activation level of the effect (0 = off, 1 = full).
// - _UpgradeMinBrightness : Minimum brightness of the effect (prevents the highlight from disappearing in dark areas).
// - _UpgradeColor         : The main tint color of the upgrade effect.
// - _UpgradeIntensity     : The strength/brightness of the effect.
//
// Recommended for:
// - Weapons, items, upgrade animations, evolution effects, energy blades, and more.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

inline half3 ZLZ_UpgradeWeapon(half3 FinalLightSweep, half  _UpgradeMinBrightness, half3 _UpgradeColor, half  _UpgradeIntensity, half  _UpgradeActive)
{
    #if defined(_UPGRADE_ON)
        half3 upgradeBase   = max(FinalLightSweep, _UpgradeMinBrightness);
        half3 upgradeColor  = upgradeBase * _UpgradeColor;
        half3 upgradeLit    = upgradeColor * _UpgradeIntensity;
        half3 FinalUpgrade  = lerp(FinalLightSweep, upgradeLit, _UpgradeActive);
        return FinalUpgrade;
    #else
        return FinalLightSweep;
    #endif
}


#endif // ZLZ_CHARACTERFX_INCLUDED