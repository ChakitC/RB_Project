#ifndef ZLZ_CONTACT_SHADOW_INCLUDED
#define ZLZ_CONTACT_SHADOW_INCLUDED

// ─────────────────────────────────────────────────────────────────────────────
// Character Contact Shadow — receiver (D2)
//
// Screen-space contact self-shadow (hair→face, arm→body). From the current pixel we take ONE
// sample offset toward the light (in screen space) and read the character depth captured by
// ZLZ_CharacterContactShadowFeature into `_ZLZCharDepthTex`. If geometry at the offset point sits
// in FRONT of this pixel (closer to the camera, i.e. between us and the light) within a contact
// range, something (e.g. hair) is occluding the light → shadow.
//
// Crisp + cheap (single binary tap, stylized anime look — not a soft penumbra), and stable:
//   • Perspective-corrected — the contact distance is scaled by 1/eyeDepth, so it stays constant
//     on screen no matter how far the character is from the camera (the standard perspective trick).
//   • Stereo-correct — SAMPLE_TEXTURE2D_X + UnityStereoTransformScreenSpaceTex for VR single-pass.
// Implements the well-known screen-space contact-shadow technique (a camera-depth prepass + a
// march toward the light). Gating is a simple depth-RANGE test (min bias .. max range).
//
// `_ZLZContactShadowParams` (global, set by the feature):
//   x = contact march toward the light (screen-UV at 1 m; auto-shrinks with distance)
//   y = depth bias (m) — raise to kill self-shadow speckle (acne)
//   z = max contact range (m) — only occluders closer than this cast (keeps it short, hair→face)
//   w = strength (0..1)
// ─────────────────────────────────────────────────────────────────────────────

TEXTURE2D_X(_ZLZCharDepthTex);
SAMPLER(sampler_ZLZCharDepthTex);

float4 _ZLZContactShadowParams;

// positionCS = the fragment's SV_POSITION (xy = pixels). Returns 1 = lit, <1 = in contact shadow.
half ZLZ_SampleContactShadow(float4 positionCS, half3 lightDirWS)
{
    // _ScaledScreenParams (not _ScreenParams) tracks Render Scale — positionCS.xy is in
    // post-scale render-target pixels, so this keeps screenUV in 0..1 at any Render Scale.
    float2 screenUV   = positionCS.xy / _ScaledScreenParams.xy;
    float  currentEye = LinearEyeDepth(positionCS.z, _ZBufferParams);

    // March toward the light in screen space. We project the world light direction into view space
    // and use its xy as the on-screen direction toward the light. Dividing by eye depth makes the
    // screen-space step shrink with distance → the contact shadow keeps a constant on-screen length
    // (perspective-correct) — the standard inverse-eye-depth screen-space scaling.
    // NOTE (verify in Unity): if the shadow lands on the WRONG side (away from the light), flip the
    // sign of this offset — it is the one platform-dependent (Y-flip) spot in this technique.
    float3 vLight   = TransformWorldToViewDir(lightDirWS);
    float2 sampleUV = screenUV + vLight.xy * (_ZLZContactShadowParams.x / max(currentEye, 1e-4));

    // Off-screen → nothing to occlude with.
    if (any(sampleUV < 0.0) || any(sampleUV > 1.0))
        return 1.0h;

    float sampleRaw = SAMPLE_TEXTURE2D_X(_ZLZCharDepthTex, sampler_ZLZCharDepthTex,
                                         UnityStereoTransformScreenSpaceTex(sampleUV)).r;
    float sampleEye = LinearEyeDepth(sampleRaw, _ZBufferParams);

    // Occluded if the offset point is in FRONT of us (closer to camera/light) within the contact
    // range. `bias` rejects a surface shadowing itself (acne); `maxRange` keeps it a short contact.
    float diff = currentEye - sampleEye;
    half  occluded = (diff > _ZLZContactShadowParams.y && diff < _ZLZContactShadowParams.z) ? 1.0h : 0.0h;

    return 1.0h - occluded * (half)_ZLZContactShadowParams.w;
}

#endif // ZLZ_CONTACT_SHADOW_INCLUDED
