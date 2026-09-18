#ifndef ZLZ_DITHER_INCLUDED
#define ZLZ_DITHER_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_Dither.hlsl
// Pixel-stipple transparency via Bayer ordered-dither + clip().
//
// Use Cases:
// - Camera Near Fade : fade character out when the camera collides with it (third-person / VRChat)
// - Spawn / Despawn  : pixel-stipple alternative to Dissolve, no noise texture required
// - Stealth / Hide   : runtime-controlled invisibility (drive via MaterialPropertyBlock)
//
// Advantages over Alpha Blend:
// - Writes depth      -> Outline pass keeps working
// - No sort order     -> avoids transparent draw-order artifacts
// - Cheaper           -> no texture sample (Bayer pattern is constant lookup)
// - Stylized by design -> reads as intentional, fits anime / cartoon
//
// Activation:
// - Always compiled (the _DITHER keyword was removed); the clip early-outs when alpha is 0.
//
// ----------------------------------------------------------------------------------------------------------------------------------------------------------

// Bayer 4x4 ordered-dither matrix (values pre-normalized to 0..15/16).
static const float ZLZ_Bayer4x4[16] =
{
     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
};

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Global runtime gate (set by ZLZ_RuntimeFlag.cs):
//   1 = Play mode / Build  → camera-near fade runs normally
//   0 = Editor (default)   → camera-near fade returns 0 so the Scene view shows the
//                            character at full opacity. Manual _DitherAlpha and the
//                            Occlusion-fade path still work in Editor — only the
//                            camera-distance feedback is suppressed.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
float _ZLZ_RuntimeActive;

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Camera Near Fade
// Returns alpha 0..1 — fully visible when camera is past farDist, fully dithered out
// when camera is within nearDist. Used for VRChat (camera = head) and first-person
// scenarios where the camera physically bumps into the character.
// For NPC pass-through / occluder fade, see _DitherOcclusionAlpha (driven by the
// ZLZ_OcclusionFader scene manager via raycast, not by camera distance).
// Only meaningful in the player-camera pass (ForwardLit). Shadow / depth passes use a
// different "camera" (the light), so calling this there would produce mismatched results.
//
// CRITICAL: distance is measured from a SINGLE character-level anchor (not per-pixel
// positionWS) so the entire character fades uniformly. Without this, front-of-character
// pixels fade more than back-of-character pixels and the camera sees through the body.
//
// Anchor resolution chain:
//   1) _CharacterAnchorWS  — set per-frame by ZLZ_CharacterVFX to the character root.
//                            All renderers of one character share the same anchor →
//                            uniform fade across body, hair, face, cloths together.
//   2) UNITY_MATRIX_M translation — fallback when no VFX component drives the anchor
//                            (e.g. enemy / prop using ZLZ shader without VFX setup).
//                            Uniform within one mesh; may differ across renderers if
//                            their transforms aren't co-located.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline half ZLZ_ComputeCameraDither(half nearDist, half farDist)
{
    // Editor preview suppression — only the in-Play camera-distance feedback
    // should drive this fade. See ZLZ_RuntimeFlag.cs for the C# side.
    if (_ZLZ_RuntimeActive < 0.5h) return 0.0h;

    float3 refPos = (dot(_CharacterAnchorWS.xyz, _CharacterAnchorWS.xyz) > 1e-6h)
                    ? _CharacterAnchorWS.xyz
                    : UNITY_MATRIX_M._m03_m13_m23;
    half d = distance(refPos, _WorldSpaceCameraPos.xyz);
    // saturate((far - d) / (far - near))  ->  1 when very near, 0 when past farDist
    return saturate((farDist - d) / max(farDist - nearDist, 1e-4h));
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Apply Dither
// screenPos : i.positionHCS.xy (pixel coordinates from SV_POSITION semantic in fragment)
// alpha     : 0 = render all pixels, 1 = clip all pixels
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline void ZLZ_ApplyDither(float2 screenPos, half alpha)
{
    #if defined(_DITHER_ON)
        // Early-out when fully visible -> compiler keeps the clip but skips matrix lookup.
        if (alpha <= 0.001h) return;

        uint2 ip = (uint2)screenPos;
        float threshold = ZLZ_Bayer4x4[(ip.x & 3) + (ip.y & 3) * 4];

        // clip when alpha exceeds threshold -> larger alpha -> more pixels removed
        clip(threshold - alpha);
    #endif
}

#endif // ZLZ_DITHER_INCLUDED
