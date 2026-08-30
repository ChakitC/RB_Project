#ifndef ZLZ_SOFTLIGHT_INCLUDED
#define ZLZ_SOFTLIGHT_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_ComputeSoftLight
// Applies a soft-light style tone curve on top of the final lit color.
// This function does NOT check _SOFTLIGHT_ON; call it only when feature is active.
//
// Parameters:
// - baseColor: the lighting result before soft-light
// - softLightLevel: shape parameter controlling dark/light branch
// - softLightOpacity: blend factor to mix soft-light with baseColor
//
// Return: baseColor after applying soft-light blend
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

half3 ZLZ_ComputeSoftLight(
    half3 baseColor,
    half  softLightLevel,
    half  softLightOpacity
)
{
    // Soft-light formula (standard)
    half3 softDark = 2.0h * baseColor * softLightLevel + baseColor * baseColor * (1.0h - 2.0h * softLightLevel);

    half3 softBright = sqrt(baseColor) * (2.0h * softLightLevel - 1.0h) + 2.0h * baseColor * (1.0h - softLightLevel);

    // Match Shader Graph
    half isBright = step(0.5h, softLightLevel);

    half3 softResult = lerp(softDark, softBright, isBright);

    // Final blend with opacity
    return lerp(baseColor, softResult, softLightOpacity);
}

#endif // ZLZ_SOFTLIGHT_INCLUDED
