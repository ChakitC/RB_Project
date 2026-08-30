#ifndef ZLZ_RIMLIGHT_INCLUDED
#define ZLZ_RIMLIGHT_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Compute Rim Light
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

half3 ZLZ_ComputeRimLight(
    half3 N_main,              // Normal (world space, normalized)
    half3 viewDirWS,           // View direction (world space)
    half3 lightDir,            // Main light direction
    half3 lightColor,          // Main light color
    half3 baseEmissiveColor    // Emissive color before rim light
)
{
    // Choose Rim Base Color
    half3 rimBaseColor;

    #if defined(_RIMCOLORMODE_LIGHT)
        rimBaseColor = lightColor;
    #elif defined(_RIMCOLORMODE_CUSTOM)
        rimBaseColor = _RimColor.rgb;
    #else
        rimBaseColor = lightColor; // fallback
    #endif

    // Light-side mask (N · L)
    half litSideMask = saturate(dot(N_main, lightDir));

    // Fresnel rim
    half fresRim = 1.0h - saturate(dot(N_main, viewDirWS));

    // Toon step
    half fresRimStep = floor(fresRim * _StepRim) / _StepRim;

    // Final rim mask
    half rimMask = litSideMask * fresRimStep;

    // Rim lighting contribution
    half3 CrimLight     = rimMask * rimBaseColor;
    half3 IntenRimlight = CrimLight * _IntensityRimLight;

    // Return final rim result
    return baseEmissiveColor + IntenRimlight;
}

#endif // ZLZ_RIMLIGHT_INCLUDED