#ifndef ZLZ_FACESHADOW_INCLUDED
#define ZLZ_FACESHADOW_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_ComputeFaceScalars  —  call in VERTEX shader
//
//  Precomputes the two per-frame scalars that drive face shadow selection.
//  Both are pure functions of uniforms (light dir + head dir) → identical for every pixel.
//  Computing here: ~26 ALU × ~1K verts  vs the old  71 ALU × ~2M pixels per frame.
//
//  Note: _HeadForwardWS / _HeadRightWS arrive pre-normalized from HeadDirectionBinder
//        (headBone.forward / headBone.right), so the float3 normalize is intentionally skipped.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
void ZLZ_ComputeFaceScalars(
    float3 lightDirWS,
    out half faceAngle01,
    out half faceSideMask
)
{
    half2 mainLightDirXZ = normalize(lightDirWS.xz  + half2(1e-5h, 1e-5h));
    half2 faceForwardXZ  = normalize(_HeadForwardWS.xz + half2(1e-5h, 1e-5h));
    half2 faceRightXZ    = normalize(_HeadRightWS.xz   + half2(1e-5h, 1e-5h));

    faceAngle01  = dot(faceForwardXZ, -mainLightDirXZ) * 0.5h + 0.5h;
    faceSideMask = step(0.0h, dot(faceRightXZ, -mainLightDirXZ));
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ_ComputeFaceShadowTerm  —  call in FRAGMENT shader
//
//  Receives precomputed scalars via interpolator — 6 ALU + 1 TEX only.
//  faceAngle01  : front/back light angle remapped 0..1
//  faceSideMask : 0 = left side, 1 = right side
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half ZLZ_ComputeFaceShadowTerm(
    half   faceAngle01,
    half   faceSideMask,
    float2 FaceUV
)
{
    half4 faceTex        = SAMPLE_TEXTURE2D(_FaceTex, sampler_FaceTex, FaceUV);
    half2 stepRG         = step(faceTex.xy, faceAngle01.xx);
    half  faceShadowMask = lerp(stepRG.x, stepRG.y, faceSideMask);
    return 1.0h - saturate(faceShadowMask);
}

#endif // ZLZ_FACESHADOW_INCLUDED
