#ifndef ZLZ_LIGHTING_INCLUDED
#define ZLZ_LIGHTING_INCLUDED

// ═════════════════════════════════════════════════════════════════════════════════════════════════
//  ZLZ Lighting - shared by every ZLZ package.
//
//  This file replaces ZLZ_MainLighting.hlsl (Anime) and ZLZ_Env_Lighting.hlsl (Environment), which
//  were copies of each other that had drifted apart. The drift was not theoretical : the point-light
//  shadow fix below reached the Anime copy and never reached the Environment one, so Environment
//  shipped with point lights picking the wrong cubemap face for as long as both files existed. One
//  file is the only arrangement where fixing a bug once actually fixes it everywhere.
//
//  ─────────────────────────────────────────────────────────────────────────────────────────────────
//  FROZEN CONTRACT - ADD ONLY, same rule as ZLZ_HubProduct.cs.
//
//  Every ZLZ package ships its own copy of Assets/ZLZ/Common, and whichever package the customer
//  imports LAST is the copy that wins - even when it is the older one. A customer who installs
//  Anime 2.0 and then imports an Environment build that predates it silently downgrades this file.
//
//      ADDING a function or an overload      safe
//      removing / renaming anything          BREAKS the other package
//      changing a signature or return type   BREAKS the other package
//
//  Raise ZLZ_LIGHTING_VERSION whenever a package starts depending on something new here, and have
//  that package's shader check it. A downgraded Common then fails with a sentence the customer can
//  act on instead of a pink material with no explanation.
// ═════════════════════════════════════════════════════════════════════════════════════════════════

// Version 2 : Forward+ support. New ScreenUV overloads of ZLZ_MainLight iterate the clustered
// light list (Forward+ stores lights per screen tile, not per object, so the pixel's screen UV
// is required to find them). The pre-existing overloads are untouched and keep their classic
// per-object loop : correct on Forward, and on Forward+ they light with main light only, exactly
// as before this version - a shader must BOTH call a ScreenUV overload AND declare
//     #pragma multi_compile _ _FORWARD_PLUS
// to pick up additional lights under Forward+.
// Version 3 : Ambient Fallback Light (ZLZ_ApplyAmbientFallback + ZLZ_EffectiveMainLightDir).
// With NO Directional Light in the scene, a main light is synthesised from the ambient probe so
// light-anchored features (toon ramp, rim, highlights, face SDF) keep working. Opt-in per caller.
// Version 4 : ZLZ_ComputeToonLighting gains a 9-argument overload carrying Indirect Intensity and
// Indirect Tint By Base. The 7-argument form is untouched and still produces the exact pre-v4 result,
// so a package that does not know about the new controls keeps its shipped look.
// Version 5 : ZLZ_SubtractMainLightFromLightmap, for Subtractive mixed lighting. Purely additive.
// Version 6 : ZLZ_BakedMainLightOcclusion, the baked shadow a DYNAMIC object receives from static
// geometry via light probes. Purely additive.
#define ZLZ_LIGHTING_VERSION 6


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Main light + additional lights
//
//  One implementation, three entry points. The overloads below are thin wrappers that pass literal
//  constants, so every branch in here folds away at compile time and each caller gets exactly the
//  code it would have written by hand.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ScreenUV : the pixel's normalized screen UV, as returned by
// GetNormalizedScreenSpaceUV(positionCS). Forward+ groups lights by screen tile, so this is the
// key that finds the pixel's cluster ; the classic Forward path never reads it.
void ZLZ_MainLightInternal(
    float3 WorldPos,
    float2 ScreenUV,
    half4  ShadowMask,
    half   ShadowFade,
    bool   UseShadowMask,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
#if SHADERGRAPH_PREVIEW
    // Dead in a hand-written .shader (the macro only exists inside Shader Graph). Kept so this file
    // can back a SubGraph later without anyone having to rediscover why the preview renders black.
    MainLightDir         = half3(0.5h, 0.5h, 0.0h);
    MainLightColor       = half3(1.0h, 1.0h, 1.0h);
    MainDistAtten        = 1.0h;
    MainShadowAtten      = 1.0h;
    AdditionalLightColor = half3(0.0h, 0.0h, 0.0h);
#else
    // KEEP THIS FLAT. The Light struct is fetched ONCE, unconditionally, and the only thing written
    // inside the branch below is a scalar.
    //
    // The first version of this file nested `if (UseShadowMask)` inside the [branch], assigning the
    // struct in both arms. The ground shader survived it (its ShadowFade is the literal 1, so the
    // outer branch folded away and the whole thing collapsed to straight-line code) but grass did
    // not : grass feeds ShadowFade from an interpolator, so its branch is a real runtime one, and
    // with a struct assigned across the nested arms the blades stopped receiving the main light
    // shadow entirely. Do not "tidy" the ternary back into an if/else that assigns `mainLight`.
    //
    // GetMainLight() reads constants only (direction, colour, unity_LightData.z) - no texture work -
    // so taking it up front is free. The two calls below are exactly what the GetMainLight overloads
    // do internally; the only thing skipped is their `_LIGHT_COOKIES` block, which no ZLZ shader
    // declares the keyword for, so it never compiled in either package anyway.
    Light mainLight = GetMainLight();
    MainShadowAtten = 1.0h;

    // ShadowFade gates the main-light shadow SAMPLE itself. At 1 it samples normally ; at 0 the
    // shadow-map lookup is skipped with a real [branch] rather than multiplied out afterwards, so
    // geometry that does not need the shadow (far grass, say) pays none of the bandwidth - the
    // dominant per-pixel cost under heavy overdraw. Between 0 and 1 the attenuation is lerped
    // toward fully-lit, which is what lets a caller fade receive-shadow out across a distance band.
    [branch]
    if (ShadowFade > 0.0h)
    {
        // TransformWorldToShadowCoord already returns a screen-space coord when
        // _MAIN_LIGHT_SHADOWS_SCREEN is on and a shadow-map coord otherwise, so there is nothing
        // for us to branch on. (Both source files used to guard this with `#if SHADOWS_SCREEN`,
        // which is not a keyword URP defines - the block never compiled in either package.)
        float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);

        // UseShadowMask is a literal at every call site, so one side folds out.
        //
        //   true  - MainLightShadow mixes the realtime shadow with the baked shadowmask AND applies
        //           URP's shadow distance fade.
        //   false - the plain realtime sample, no baked mixing and no distance fade.
        //
        // The character shader deliberately stays on the second path : it declares no baked-lighting
        // keywords at all, and switching it to the first would change the look of a shipped product
        // (its shadows would start fading out near the shadow distance instead of ending abruptly).
        // That is worth doing, but as its own change where it can be seen and judged - not folded
        // invisibly into a file move.
        half atten = UseShadowMask
            ? (half)MainLightShadow(shadowCoord, WorldPos, ShadowMask, _MainLightOcclusionProbes)
            : (half)MainLightRealtimeShadow(shadowCoord);

        MainShadowAtten = lerp(1.0h, atten, saturate(ShadowFade));
    }

    MainLightDir         = (half3)mainLight.direction;
    MainLightColor       = (half3)mainLight.color;
    MainDistAtten        = (half) mainLight.distanceAttenuation;

    #if USE_FORWARD_PLUS
        // Forward+ parity : GetMainLight() hardcodes distanceAttenuation to 1 on this path
        // (unity_LightData is not populated there), while classic Forward reads
        // unity_LightData.z, which drops to 0 when the Directional Light is off. Every ZLZ
        // toon ramp multiplies by this value, so without the patch a scene with the sun
        // DISABLED renders its foliage on the LIT side of the ramp under Forward+ (wrong
        // tint) and on the shadow side under Forward. A missing main light publishes a pure
        // black colour - use that as the off signal and restore the 0.
        if (!any(MainLightColor))
            MainDistAtten = 0.0h;
    #endif

    AdditionalLightColor = half3(0.0h, 0.0h, 0.0h);

    #ifdef _ADDITIONAL_LIGHTS
    #if USE_FORWARD_PLUS
        // Forward+ (URP's _FORWARD_PLUS variant ; Core.hlsl defines _ADDITIONAL_LIGHTS 1 there).
        // The classic loop below CANNOT run here : GetAdditionalLightsCount() is hardwired to 0
        // on this path (lights live in per-tile clusters, not per-object lists), which is exactly
        // the "no additional lights on Forward+" customer bug.
        //
        // URP's own LIGHT_LOOP_BEGIN macro is not usable in this function : it hardcodes reads of
        // a local named `inputData`, so the same iteration is written out by hand, mirroring
        // Lighting.hlsl :
        //   1. Extra DIRECTIONAL lights sit at the FRONT of the light buffer, outside the
        //      clusters - a plain loop over URP_FP_DIRECTIONAL_LIGHTS_COUNT.
        //   2. Point / spot lights come from the pixel's cluster via ClusterInit / ClusterNext,
        //      with the iterator's local index offset past those directionals.
        // Both loops name their index `lightIndex` because FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        // (subtractive-lightmap skip, empty unless that mode is on) expands against that name.
        for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
            Light addLight       = GetAdditionalPerObjectLight(lightIndex, WorldPos);
            half  addShadowAtten = (half)AdditionalLightRealtimeShadow(lightIndex, WorldPos, addLight.direction);
            AdditionalLightColor += (half3)(addLight.color * addLight.distanceAttenuation * addShadowAtten);
        }
        {
            ClusterIterator clusterIterator = ClusterInit(ScreenUV, WorldPos, 0);
            uint lightIndex;
            [loop] while (ClusterNext(clusterIterator, lightIndex))
            {
                lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT;
                FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                // Same shadow contract as the classic loop below : direction picks the point-light
                // cubemap face, and the call collapses to 1.0 without _ADDITIONAL_LIGHT_SHADOWS.
                Light addLight       = GetAdditionalPerObjectLight(lightIndex, WorldPos);
                half  addShadowAtten = (half)AdditionalLightRealtimeShadow(lightIndex, WorldPos, addLight.direction);
                AdditionalLightColor += (half3)(addLight.color * addLight.distanceAttenuation * addShadowAtten);
            }
        }
    #else
        int additionalLightsCount = GetAdditionalLightsCount();
        for (int i = 0; i < additionalLightsCount; ++i)
        {
            int perObjectLightIndex = GetPerObjectLightIndex(i);
            Light addLight          = GetAdditionalPerObjectLight(perObjectLightIndex, WorldPos);

            // Pass the light direction: point-light shadows live in a cubemap atlas and the
            // direction picks the face. The 2-arg overload hardcodes (1,0,0) — wrong face.
            //
            // This whole call collapses to "return 1.0" unless the calling shader declares
            //     #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            // so a shader that wants Point/Spot shadows needs BOTH this line and that pragma.
            half  addShadowAtten    = (half)AdditionalLightRealtimeShadow(perObjectLightIndex, WorldPos, addLight.direction);
            half3 finalAddColor     = (half3)(addLight.color * addLight.distanceAttenuation * addShadowAtten);

            AdditionalLightColor   += finalAddColor;
        }
    #endif
    #endif

#endif // SHADERGRAPH_PREVIEW
}

// Version-1 internal signature, kept verbatim for the frozen contract. Without a ScreenUV the
// Forward+ cluster lookup falls back to the screen-centre tile - but that code only compiles
// when the calling shader declares the _FORWARD_PLUS pragma, which every version-1 caller
// predates, so in practice this wrapper always runs the classic path it always ran.
void ZLZ_MainLightInternal(
    float3 WorldPos,
    half4  ShadowMask,
    half   ShadowFade,
    bool   UseShadowMask,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, float2(0.5, 0.5), ShadowMask, ShadowFade, UseShadowMask,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

// Shadowmask + distance-band fade. Used by the environment shaders ; grass drives ShadowFade from a
// per-vertex distance ramp so far blades stop sampling the shadow map entirely.
void ZLZ_MainLight(
    float3 WorldPos,
    half4  ShadowMask,
    half   ShadowFade,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, ShadowMask, ShadowFade, true,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

// Shadowmask, always sampled. Constant fade of 1 folds the branch to "always sample".
void ZLZ_MainLight(
    float3 WorldPos,
    half4  ShadowMask,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, ShadowMask, 1.0h, true,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

// No shadowmask, always sampled - the character shader's entry point. Passing a dummy mask is safe
// because UseShadowMask = false removes the only code that would have read it.
void ZLZ_MainLight(
    float3 WorldPos,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, half4(1.0h, 1.0h, 1.0h, 1.0h), 1.0h, false,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Version 2 entry points : identical to the three above plus the pixel's ScreenUV
//  (GetNormalizedScreenSpaceUV(positionCS)), which is what lets the additional-light loop work on
//  Forward+ (per-tile clusters need the screen position). The calling shader must also declare
//      #pragma multi_compile _ _FORWARD_PLUS
//  or the Forward+ variant never exists and these behave exactly like their version-1 twins.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
void ZLZ_MainLight(
    float3 WorldPos,
    float2 ScreenUV,
    half4  ShadowMask,
    half   ShadowFade,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, ScreenUV, ShadowMask, ShadowFade, true,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

void ZLZ_MainLight(
    float3 WorldPos,
    float2 ScreenUV,
    half4  ShadowMask,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, ScreenUV, ShadowMask, 1.0h, true,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}

void ZLZ_MainLight(
    float3 WorldPos,
    float2 ScreenUV,
    out half3 MainLightDir, out half3 MainLightColor,
    out half  MainDistAtten, out half  MainShadowAtten,
    out half3 AdditionalLightColor)
{
    ZLZ_MainLightInternal(WorldPos, ScreenUV, half4(1.0h, 1.0h, 1.0h, 1.0h), 1.0h, false,
        MainLightDir, MainLightColor, MainDistAtten, MainShadowAtten, AdditionalLightColor);
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Ambient Fallback Light (version 3)
//
//  A toon character is drawn BY its main light : the ramp side picks Base vs Shadow colour, and
//  rim / hair highlight / specular / outline / the face SDF all multiply the light's colour or
//  read its direction. A scene with no Directional Light therefore kills every one of those
//  features at once (black colour, distanceAttenuation 0) - reported by customers who light
//  night / indoor scenes with point lights or baked ambient only.
//
//  The fallback synthesises a stand-in main light FROM THE AMBIENT PROBE when the real one is
//  absent :
//    colour    = SampleSH along the fallback direction, so the character adopts the scene's
//                ambient tint (blue night scene = blue rim) instead of an arbitrary constant.
//    direction = the ambient's dominant direction from the SH linear (L1) band, luminance-
//                weighted - the direction most of the ambient light actually comes from. A flat
//                ambient has no dominant direction ; then the caller's fallbackDir is used.
//  A scene that HAS a Directional Light is left completely untouched, black-but-enabled suns
//  included aside : "no main light" is detected as the pure black colour URP publishes then.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Default fallback direction : an upper-front-side key light (normalized (0.3, 0.8, 0.5)),
// chosen over straight up so the XZ-projected consumers (face SDF, hair highlight) still get a
// meaningful side to work with.
#define ZLZ_AMBIENT_FALLBACK_DEFAULT_DIR half3(0.303h, 0.808h, 0.505h)

// Dominant ambient direction from the SH L1 band, or fallbackDir when the ambient is flat.
half3 ZLZ_AmbientDominantLightDir(half3 fallbackDir)
{
    // unity_SHA{r,g,b}.xyz are the per-channel linear SH coefficients : each points toward the
    // direction its channel's ambient light mostly arrives from. Luminance-weighted sum = the
    // overall dominant light direction, the same vector a directional light approximating this
    // ambient would use.
    float3 dominant = unity_SHAr.xyz * 0.299 + unity_SHAg.xyz * 0.587 + unity_SHAb.xyz * 0.114;
    float  len      = length(dominant);
    return len > 1e-4 ? (half3)(dominant / len) : fallbackDir;
}

// The real sun's direction when one exists, else the ambient-dominant direction. Callable from
// the VERTEX stage too - the face SDF reads its light direction there, and it must agree with
// the fragment's fallback or the face shadow argues with the body shadow.
half3 ZLZ_EffectiveMainLightDir(half3 fallbackDir)
{
    if (any(_MainLightColor.rgb)) return (half3)_MainLightPosition.xyz;
    return ZLZ_AmbientDominantLightDir(fallbackDir);
}

// Rewrites the ZLZ_MainLight outputs with the synthesised light when the real main light is
// absent. intensity scales the fallback's colour ; 0 disables the whole feature (classic
// behaviour : light-anchored features go dark without a sun). MainDistAtten is restored to 1 so
// the toon ramp runs - the shadow-map attenuation is left alone (no light = no shadow map = 1).
void ZLZ_ApplyAmbientFallback(
    half  intensity,
    half3 fallbackDir,
    inout half3 MainLightDir,
    inout half3 MainLightColor,
    inout half  MainDistAtten)
{
    if (intensity <= 0.0h || any(MainLightColor)) return;
    MainLightDir   = ZLZ_AmbientDominantLightDir(fallbackDir);
    MainLightColor = (half3)SampleSH(MainLightDir) * intensity;
    MainDistAtten  = 1.0h;
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  ZLZ Smooth Shadow
//  Cleans up cascade shadow map aliasing (the visible stair-step pattern where a character's cast
//  shadow lands on its own skin) and hides the per-frame texel crawl that appears when the character
//  animates. Two lightweight stages:
//    1) Value-space smoothstep widens the shadow/light transition band so partial PCF samples turn
//       into a smooth ramp instead of a near-binary edge.
//    2) Screen-space gradient feather stretches the shadow boundary across several pixels. Wider
//       than a single-pixel SDF anti-alias on purpose: the band itself is what buries the texel-grid
//       jitter, so a moving character no longer shows visible boundary wobble.
//  URP's _SHADOWS_SOFT keyword should be enabled by the Renderer asset to provide partial input;
//  without it Stage 1 collapses but Stage 2 still blurs the edge.
//
//  softness / edgeSmooth arrive as parameters rather than being read from a material constant
//  buffer, because the buffer they used to come from (_ShadowSoftness / _ShadowEdgeSmooth) only
//  exists in the character shader - reading it here would stop every environment shader compiling.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
half ZLZ_SmoothShadow(half rawAtten, half softness, half edgeSmooth)
{
    // Stage 1 : value-space smoothstep remap
    half halfWidth = lerp(0.0h, 0.5h, saturate(softness));
    half lo        = 0.5h - halfWidth;
    half hi        = 0.5h + halfWidth;
    half smoothed  = smoothstep(lo, hi, rawAtten);
    half a         = lerp(rawAtten, smoothed, saturate(softness));

    // Stage 2 : wide screen-space gradient feather
    // diff < 0 is lit, diff > 0 is shadowed. The gradient magnitude tells us how fast diff changes
    // per screen pixel; scaling the denominator stretches the transition band by that factor. At
    // edgeSmooth = 0 the band collapses to ~1 pixel (a clean AA). At 1.0 it spans roughly 10 pixels,
    // wide enough to swallow the cascade-texel crawl that shows on a moving character.
    half  diff       = 0.5h - a;
    half2 grad       = half2(ddx(diff), ddy(diff));
    half  gradLen    = sqrt(dot(grad, grad) + 1e-5h);
    half  edge       = saturate(edgeSmooth);
    half  bandScale  = lerp(1.0h, 10.0h, edge);
    half  feather    = saturate(0.5h - diff / (gradLen * bandScale));

    return lerp(a, feather, edge);
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  ZLZ Self-Shadow Rejection
//  Filters out occluders that sit close to the receiving pixel along the light direction.
//  Self-shadow casters (the character's own hair or arm) always land within a small depth of the
//  receiving surface, while environment casters (walls, trees, other characters) sit much further
//  along the light direction. Reads the raw caster depth from the URP main light shadow atlas and
//  rejects the shadow contribution when the gap is too small.
//    rawShadow    : the URP attenuation already sampled via GetMainLight
//    shadowCoord  : the same coord URP used for that sample
//    rejectDist   : threshold in shadow normalized depth (0.001 - 0.05 typical)
//  Returns 1.0 (lit) when the occluder is classified as self, the original attenuation when it is
//  classified as external, with a smoothstep transition in between.
//
//  Only valid on the shadow-map path : the screen-space shadow texture holds no caster depth, so
//  callers must skip this when _MAIN_LIGHT_SHADOWS_SCREEN is on.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
half ZLZ_RejectSelfShadow(half rawShadow, float4 shadowCoord, half rejectDist)
{
    // Skip the texture fetches when there is no occluder anyway.
    if (rawShadow >= 0.99h)
        return rawShadow;

    // Bilinear filter the caster depth by hand. A single point fetch would show the shadow atlas
    // texel grid as visible square blocks on the rejection mask; reading the four nearest texels and
    // lerping produces a smooth caster depth so the rejection edge follows the silhouette instead of
    // the texel boundary. Done in shader (not via a linear sampler) because depth textures cannot be
    // hardware-filtered on every platform we target.
    float2 uvScaled  = shadowCoord.xy * _MainLightShadowmapSize.zw - 0.5;
    int2   baseCoord = int2(floor(uvScaled));
    float2 w         = saturate(uvScaled - float2(baseCoord));

    float d00 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(0, 0)).r;
    float d10 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(1, 0)).r;
    float d01 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(0, 1)).r;
    float d11 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(1, 1)).r;

    float casterDepth = lerp(lerp(d00, d10, w.x), lerp(d01, d11, w.x), w.y);

    // Depth separation between the pixel and the caster along the light view, taken as an absolute
    // value so the same formula works under reverse-Z (Windows / Mac / Mobile) and the legacy forward
    // depth convention. Small value = caster sits on the character itself; large value = external.
    float delta = abs(shadowCoord.z - casterDepth);

    // Smooth transition between "fully reject (self)" and "fully keep (external)". The band starts at
    // zero (not at the threshold) so the transition spans 2x the slider value. The wider band shrinks
    // per-frame rejection jitter when the character rotates and the shadow atlas regenerates, at the
    // cost of a slightly fuzzier self vs external split.
    half guarded   = max(rejectDist, 0.0001h);
    half rejection = smoothstep(0.0h, guarded * 2.0h, delta);

    return lerp(1.0h, rawShadow, rejection);
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  ZLZ Toon Lighting (Shadow → Light Ramp + Direct + Indirect)
// ─────────────────────────────────────────────────────────────────────────────────────────────────
struct ZLZ_ToonLightResult
{
    half  lightRamp01;       // ramp 0-1 for shadow -> light
    half3 toonLighting;      // final toon lighting (direct + indirect)
};

// Toon lighting core : turns a signed shading term into a two-tone (shadow -> light) look.
// The ramp position drives three things at once : the shadow/base colour blend, whether the
// direct light is clamped (in shade) or allowed to overbright (in light), and how much the
// indirect (ambient) is tinted by the shadow colour. Output carries both the ramp 0..1 and the
// final lit colour so callers can reuse the ramp (e.g. for rim or specular gating).
// The 9-argument form. IndirectIntensity and IndirectTintByBase are the two v4 controls ; the
// 7-argument overload below feeds them (1, 0), which reproduces the pre-v4 result exactly.
//
//   IndirectIntensity   - multiplies the baked GI / ambient term ONLY. Unlike the Lighting window's
//                         global Indirect Intensity, which scales GI for the whole scene and so
//                         amplifies every transfer at once (raise it to redden a floor and the wall
//                         above it just turns whiter from the floor's own bounce), this is per
//                         material : the surface that should read the bounce is the one you raise.
//
//   IndirectTintByBase  - NEW CALLERS SHOULD PASS 1, AND SHOULD NOT EXPOSE THIS AS A MATERIAL
//                         PROPERTY. It is a compatibility lever, not an artist control.
//                           1 : GI on the lit side is tinted toward BaseColor, so ambient obeys the
//                               material's own colour the way direct light already does. Correct.
//                           0 : GI on the lit side is tinted toward WHITE. Pre-v4 behaviour, and a
//                               bug : BaseColor multiplied the direct term but not this one, so a red
//                               material lit by white bounce washed out to WHITE. A surface cannot
//                               change colour depending on which direction its light arrives from.
//                         The tell that 0 was an oversight rather than a decision : the SHADE side of
//                         the same expression already tinted GI by ShadowColor, i.e. GI was meant to
//                         follow the ramp colours all along - only the lit half was left behind.
//                         0 survives for one reason only : the 7-argument overload passes it, which is
//                         what keeps the already-shipped Anime package rendering byte-identical.
ZLZ_ToonLightResult ZLZ_ComputeToonLighting(
    half    SwitchFS,      // signed shading term, -1..1 (typically N.L, or a face-shadow driven value) ; > 0 lit, < 0 shade
    half3   lightColor,
    half3   AdditionalLightColor,
    half3   indirectLighting,
    half3   ShadowColor,
    half3   BaseColor,
    half    ToonRampSmoothness,
    half    IndirectIntensity,
    half    IndirectTintByBase
)
{
    ZLZ_ToonLightResult o;

    // Ramp 0..1 : 0 = full shadow, 1 = full light. Smoothstep width = ToonRampSmoothness controls
    // how soft the shadow-to-light transition is (near 0 = hard toon edge). The max() guards against
    // smoothstep being undefined when the two edges are equal.
    o.lightRamp01 = saturate(smoothstep(0, max(ToonRampSmoothness, 1e-4h), SwitchFS));

    half3 toonColor = lerp(ShadowColor, BaseColor, o.lightRamp01);

    // Two direct-light variants : in shade the light is saturated (clamped to 1) so an over-bright
    // sun cannot wash the shadow tone ; in light the raw (unclamped) light is kept so HDR sun can
    // overbright the lit side. The ramp blends between them so the switch is smooth, not a hard step.
    half3 directLit_Clamped = toonColor * saturate(lightColor) + AdditionalLightColor;
    half3 directLit_Full    = toonColor * lightColor           + AdditionalLightColor;
    half3 directLit         = lerp(directLit_Clamped, directLit_Full, o.lightRamp01);

    // Indirect (ambient / baked GI) is tinted toward the shadow colour in shade, so ambient in
    // shadowed areas picks up the shadow tone instead of reading flat and grey. On the LIT side it
    // is tinted toward white or toward BaseColor, per IndirectTintByBase - see the note above.
    half3 litTint = lerp(half3(1.0h, 1.0h, 1.0h), BaseColor, saturate(IndirectTintByBase));

    o.toonLighting = directLit
                   + indirectLighting * max(IndirectIntensity, 0.0h) * lerp(ShadowColor, litTint, o.lightRamp01);

    return o;
}

// Pre-v4 signature, kept verbatim so every shader written against it - including packages that
// ship their own older Environment / Anime build - compiles and renders exactly as it did.
// (1, 0) = full-strength indirect, tinted toward white on the lit side.
ZLZ_ToonLightResult ZLZ_ComputeToonLighting(
    half    SwitchFS,
    half3   lightColor,
    half3   AdditionalLightColor,
    half3   indirectLighting,
    half3   ShadowColor,
    half3   BaseColor,
    half    ToonRampSmoothness
)
{
    return ZLZ_ComputeToonLighting(SwitchFS, lightColor, AdditionalLightColor, indirectLighting,
                                   ShadowColor, BaseColor, ToonRampSmoothness,
                                   1.0h, 0.0h);
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Subtractive Mixed Lighting  (ZLZ_LIGHTING_VERSION 5)
//
//  Subtractive is the only mixed mode whose lightmap carries the mixed light's DIRECT term as well as
//  its indirect one - the sun's shading is already painted into the texture. The one thing a runtime
//  shader can still contribute is the shadow of a DYNAMIC object, which simply was not in the scene
//  when the bake ran. This does that the way URP's Lit does (SubtractDirectMainLightFromLightmap) :
//  estimate the direct light the bake laid down here, remove it where the realtime shadow says the
//  pixel is occluded NOW, and clamp the result on both ends.
//
//  CALLERS MUST GUARD WITH
//      #if defined(LIGHTMAP_ON) && defined(_MIXED_LIGHTING_SUBTRACTIVE)
//  In every other lighting mode this function is simply wrong : a Shadowmask or Baked Indirect
//  lightmap holds indirect light only, so there is no direct term in it to take away, and calling
//  this would carve a shadow out of the bounce light instead.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Baked shadow occlusion for DYNAMIC objects  (ZLZ_LIGHTING_VERSION 6)
//
//  In Shadowmask / Distance Shadowmask / Subtractive, the shadow that STATIC geometry casts onto a
//  MOVING object does not come from the shadow map. It is baked into the light probes and arrives as
//  unity_ProbesOcclusion, masked by whichever channel the bake assigned to the main light. A shader
//  that reads only MainLightRealtimeShadow therefore keeps a character fully lit inside a baked
//  shadow the moment the caster leaves the realtime shadow map - past the shadow distance, or when
//  the static geometry has Cast Shadows turned off, which is exactly the saving Shadowmask mode
//  exists to enable.
//
//  SAFE TO CALL UNCONDITIONALLY. URP leaves _MainLightOcclusionProbes at zero for a light with no
//  baked occlusion channel, and BakedShadow() then returns 1, so a realtime-only scene renders
//  byte-identically. That is what lets this be added to a shipped shader without a new keyword and
//  without a look change anywhere a bake has not happened.
//
//  DELIBERATELY NOT MainLightShadow(). That function would give the same baked value, but it also
//  folds in URP's shadow DISTANCE FADE, which would change a shipped character's realtime shadow from
//  ending abruptly to fading out. Combining this separately keeps the realtime half exactly as it was.
//
//  The result is a per-RENDERER constant, not per-pixel : light probes are sampled at the object's
//  anchor, so a character darkens as a whole when it enters a baked shadow. That is what probe
//  occlusion IS - URP's own Lit behaves identically - not a resolution this can improve.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
half ZLZ_BakedMainLightOcclusion()
{
    return (half)BakedShadow(unity_ProbesOcclusion, _MainLightOcclusionProbes);
}


half3 ZLZ_SubtractMainLightFromLightmap(
    half3 bakedGI,
    half3 MainLightDir,
    half3 MainLightColor,
    half  MainShadowAtten,
    half3 normalWS)
{
    // The lambert term the bake would have put here had nothing occluded it. Taken against the pixel
    // normal on purpose : a surface already facing away from the sun was dark in the bake too, and
    // saturate() keeps a realtime shadow from darkening it a second time.
    half3 lambert = MainLightColor * saturate(dot(MainLightDir, normalWS));

    // Removed only where the realtime shadow says the pixel is occluded now. At full light
    // (atten = 1) this subtracts exactly nothing, so a scene with no dynamic casters renders the
    // bake untouched - which is the behaviour that makes the whole approximation safe.
    half3 subtracted = bakedGI - lambert * (1.0h - MainShadowAtten);

    // The scene's authored floor for how dark a subtractive shadow may go (Lighting window >
    // Subtractive Shadow Color), then faded by the light's own Shadow Strength so that slider keeps
    // meaning something in this mode.
    half3 shadowed = max(subtracted, (half3)_SubtractiveShadowColor.xyz);
    shadowed = lerp(bakedGI, shadowed, GetMainLightShadowParams().x);

    // Never brighter than the bake. The subtraction is an estimate, and an over-estimate would
    // otherwise ADD light that no lightmap ever contained.
    return min(bakedGI, shadowed);
}


// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  ZLZ Shadow Edge (a.k.a. Core Shadow / Terminator Darkening Band)
//  Adds a darker band centered on the light/shadow boundary, tapering to zero in deep shadow (where
//  ambient lifts the tone back up) and in full light - so it only deepens the transition itself,
//  never over-darkens either side. Pure ALU, no texture. Drives form/volume on rounded surfaces
//  (hair, cheeks). The band is built from the post-ramp value, so its screen width follows the ramp
//  transition; the exponent shapes it from a thin line to a broad band within that transition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
half3 ZLZ_ApplyShadowEdge(
    half3 litColor,
    half  lightRamp01,
    half3 edgeColor,
    half  intensity,
    half  width
)
{
    half hump     = 4.0h * lightRamp01 * (1.0h - lightRamp01);   // 0..1, peaks at the terminator
    half shapeExp = lerp(6.0h, 0.5h, saturate(width));           // small width = thin line, large = broad band
    half edge     = pow(saturate(hump), shapeExp) * saturate(intensity);
    return lerp(litColor, litColor * edgeColor, edge);           // multiplicative darken (edgeColor white = no change)
}

#endif  // ZLZ_LIGHTING_INCLUDED
