#ifndef ZLZ_HEAD_BACKFACE_CONTROL_INCLUDED
#define ZLZ_HEAD_BACKFACE_CONTROL_INCLUDED

// Requires:
// - _HeadCenterWS, _HeadForwardWS
// - _HeadBackCutoff, _HeadTopCutoff, _HairFadeRange
// - Keywords: _EYE_BACKFACE_CLIP_ON, _HAIR_BACKFACE_FADE_ON
// - _WorldSpaceCameraPos (URP)

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_ComputeHeadBackfaceShifted  —  call in VERTEX shader
//
//  Computes (headFacingToCamera - finalCutoff) as a single scalar.
//  Both terms are CONSTANT for all vertices/pixels of a character in a given frame
//  (depend only on uniforms: _WorldSpaceCameraPos, _HeadCenterWS, _HeadForwardWS,
//   _HeadBackCutoff, _HeadTopCutoff). Precomputing in vertex avoids
//   2× normalize + dot + saturate + mad PER PIXEL → moves it to ~1× per vertex.
//
//  Returns large positive (effectively "always pass") when the binder hasn't been
//  set up — so the fragment fast-path becomes a no-op.
//
//  NOTE: returned as FLOAT (not half) because Eye clip() and Hair smoothstep both
//  operate at the precision boundary where fp16 produces visible popping artifacts.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline float ZLZ_ComputeHeadBackfaceShifted()
{
    #if !defined(_HEAD_BACK_HIDE_ON)
        return 1.0;  // no-op marker
    #endif

    float3 headForwardWS = _HeadForwardWS.xyz;
    if (dot(headForwardWS, headForwardWS) <= 1e-4) return 1.0;

    float3 headCenterWS = _HeadCenterWS.xyz;
    if (dot(headCenterWS, headCenterWS) <= 1e-6) return 1.0;

    float3 cameraDirToHeadWS = normalize(_WorldSpaceCameraPos - _HeadCenterWS.xyz);
    float3 headForwardDirWS  = normalize(headForwardWS);
    float  headFacingToCamera = dot(headForwardDirWS, cameraDirToHeadWS);

    // Top view bias (World Up = +Y). From top => 1, from side => 0
    float topViewWeight = saturate(cameraDirToHeadWS.y);
    float finalCutoff   = _HeadBackCutoff + topViewWeight * _HeadTopCutoff;

    return headFacingToCamera - finalCutoff;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Head Back-Facing Visibility Control (shared for ForwardLit + DepthOnly + DepthNormals + Outline)
// - Eye materials: clip when camera goes behind the head
// - Hair materials: fade alpha to opaque near the back-facing threshold
//
// Usage (fast path — precomputed in vertex):
//   Vertex:    o.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
//   Fragment:  ZLZ_ApplyHeadBackfaceControl(i.headBackShifted, FinalAlpha);
//
// Cost reduction: ~18 ALU/pixel → ~5 ALU/pixel  (only smoothstep+lerp remain in frag).
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline void ZLZ_ApplyHeadBackfaceControl(float headBackShifted, inout float alphaValue)
{
    #if !defined(_HEAD_BACK_HIDE_ON)
        return;
    #endif

    #if defined(_EYE_BACKFACE_CLIP_ON)
        clip(headBackShifted);  // already shifted by finalCutoff in vertex

    #elif defined(_HAIR_BACKFACE_FADE_ON)
        float fadeRange    = max(_HairFadeRange, 1e-4);
        float fadeToOpaque = 1.0 - smoothstep(-fadeRange, +fadeRange, headBackShifted);
        alphaValue = lerp(alphaValue, 1.0, fadeToOpaque);
    #endif
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// Backward-compat overload — computes scalar internally per pixel.
// Kept so any external integration that calls the old signature still works.
// Prefer the precomputed-scalar overload above for performance.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
inline void ZLZ_ApplyHeadBackfaceControl(inout float alphaValue)
{
    float h = ZLZ_ComputeHeadBackfaceShifted();
    ZLZ_ApplyHeadBackfaceControl(h, alphaValue);
}

#endif // ZLZ_HEAD_BACKFACE_CONTROL_INCLUDED
