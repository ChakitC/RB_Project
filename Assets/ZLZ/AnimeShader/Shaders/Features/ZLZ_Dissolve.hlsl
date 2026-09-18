#ifndef ZLZ_DISSOLVE_INCLUDED
#define ZLZ_DISSOLVE_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Dissolve – Transparency and Glow-Edge Dissolve Function (usable in all passes)
//
// This function creates a surface dissolve effect, commonly used for:
// - teleportation effects
// - enemy death/disintegration
// - magic or energy-based effects
// - anime/stylized visuals where objects fade out with a glowing edge
//
// The function outputs two values:
// - color : base color modified by the glow edge (if applicable)
// - alpha : transparency value after the dissolve calculation
//
//
// Important Parameters:
// - dissolveColor    : Glow-edge color applied along the dissolve boundary
// - dissolveValue    : Controls dissolve progression (0 = off, 1 = fully disappeared)
// - startDissolve    : Starting threshold of the dissolve
// - endDissolve      : Ending threshold of the dissolve
// - glowSize         : Thickness of the glowing edge
// - texture dissolve : Noise texture that defines the dissolve pattern (changing the texture changes the dissolve shape)
//
// Recommended For:
// - Vanishing effects, teleportation, enemy disintegration, magical effects
// - Anime and stylized visuals requiring sharp dissolve transitions with glow borders
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

struct DissolveResult
{
    half3 color;   // BaseDiss
    half  alpha;   // AlphaDiss
};

DissolveResult ComputeDissolve(
    half3  baseColor,          // CBedge
    half3  dissolveColor,      // _DissolveColor
    half   dissolveValue,      // _DissolveValue
    half   startDissolve,      // _StartDissolve
    half   endDissolve,        // _EndDissolve
    half   glowSize,           // _SizeGlowDissolve
    half2  uvDissolve,         // i.uvDissolve
    TEXTURE2D_PARAM(dissTex, dissSampler) // _Texture2DDissolve, sampler
)
{
    DissolveResult o;
    o.color = baseColor;
    o.alpha = 1.0h;

    const half kEps = 1e-5h;

    // if DissolveValue = 0 > no dissolve
    if (dissolveValue <= kEps)
        return o;

    // Lerp Dissolve
    half DissolveEdge = lerp(startDissolve, endDissolve, dissolveValue);

    // Glow threshold
    half2 DissolveThresholds = saturate(half2(DissolveEdge - glowSize, DissolveEdge));

    // Noise sample
    half4 texDissolve = SAMPLE_TEXTURE2D(dissTex, dissSampler, uvDissolve);

    // Step 2
    half2 stepdiss = step(DissolveThresholds, texDissolve.r);

    // Glow mask = R - G
    half glowMask = stepdiss.r - stepdiss.g;

    // Output
    o.color = lerp(baseColor, dissolveColor, glowMask);
    o.alpha = stepdiss.r;

    return o;
}

#endif