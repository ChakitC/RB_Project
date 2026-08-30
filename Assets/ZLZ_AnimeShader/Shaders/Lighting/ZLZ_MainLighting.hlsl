#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Custom Lighting (Main Light + Additional Light)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
void ZLZ_MainLight(
    float3 WorldPos,
    out half3 MainLightDir, out half3 MainLightColor,
    out half MainDistAtten, out half MainShadowAtten,
    out half3 AdditionalLightColor)
{
#if SHADERGRAPH_PREVIEW
    // Preview Path Shader Graph
    MainLightDir        = half3(0.5h, 0.5h, 0.0h);
    MainLightColor      = half3(1.0h, 1.0h, 1.0h);
    MainDistAtten       = 1.0h;
    MainShadowAtten     = 1.0h;
    AdditionalLightColor = half3(0.0h, 0.0h, 0.0h);
#else
    // Main Light (Runtime)
    #if SHADOWS_SCREEN
        float4 clipPos     = TransformWorldToHClip(WorldPos);
        float4 shadowCoord = ComputeScreenPos(clipPos);
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    #endif

    Light mainLight      = GetMainLight(shadowCoord);

    MainLightDir         = (half3)mainLight.direction;
    MainLightColor       = (half3)mainLight.color;
    MainDistAtten        = (half) mainLight.distanceAttenuation;
    MainShadowAtten      = (half) mainLight.shadowAttenuation;

    // Additional Lights (Per-Object)
    AdditionalLightColor = half3(0.0h, 0.0h, 0.0h);

    #ifdef _ADDITIONAL_LIGHTS
        int additionalLightsCount = GetAdditionalLightsCount();
        for (int i = 0; i < additionalLightsCount; ++i)
        {
            int perObjectLightIndex = GetPerObjectLightIndex(i);
            Light addLight          = GetAdditionalPerObjectLight(perObjectLightIndex, WorldPos);

            // Pass the light direction: point-light shadows live in a cubemap atlas and the
            // direction picks the face. The 2-arg overload hardcodes (1,0,0) — wrong face.
            half  addShadowAtten    = (half)AdditionalLightRealtimeShadow(perObjectLightIndex, WorldPos, addLight.direction);
            half3 finalAddColor     = (half3)(addLight.color * addLight.distanceAttenuation * addShadowAtten);

            AdditionalLightColor   += finalAddColor;
        }
    #endif

#endif // SHADERGRAPH_PREVIEW
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Smooth Shadow
// Cleans up cascade shadow map aliasing (the visible stair-step pattern where a
// character's cast shadow lands on its own skin) and hides the per-frame texel
// crawl that appears when the character animates. Two lightweight stages:
//   1) Value-space smoothstep widens the shadow/light transition band so partial
//      PCF samples turn into a smooth ramp instead of a near-binary edge.
//   2) Screen-space gradient feather stretches the shadow boundary across several
//      pixels. Wider than a single-pixel SDF anti-alias on purpose: the band
//      itself is what buries the texel-grid jitter, so a moving character no
//      longer shows visible boundary wobble.
// URP's _SHADOWS_SOFT keyword should be enabled by the Renderer asset to provide
// partial input; without it Stage 1 collapses but Stage 2 still blurs the edge.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half ZLZ_SmoothShadow(half rawAtten)
{
    // Stage 1 : value-space smoothstep remap
    half halfWidth = lerp(0.0h, 0.5h, saturate(_ShadowSoftness));
    half lo        = 0.5h - halfWidth;
    half hi        = 0.5h + halfWidth;
    half smoothed  = smoothstep(lo, hi, rawAtten);
    half a         = lerp(rawAtten, smoothed, saturate(_ShadowSoftness));

    // Stage 2 : wide screen-space gradient feather
    // diff < 0 is lit, diff > 0 is shadowed. The gradient magnitude tells us how
    // fast diff changes per screen pixel; scaling the denominator stretches the
    // transition band by that factor. At _ShadowEdgeSmooth = 0 the band collapses
    // to ~1 pixel (a clean AA). At 1.0 it spans roughly 10 pixels, wide enough
    // to swallow the cascade-texel crawl that shows on a moving character.
    half  diff       = 0.5h - a;
    half2 grad       = half2(ddx(diff), ddy(diff));
    half  gradLen    = sqrt(dot(grad, grad) + 1e-5h);
    half  edgeSmooth = saturate(_ShadowEdgeSmooth);
    half  bandScale  = lerp(1.0h, 10.0h, edgeSmooth);
    half  feather    = saturate(0.5h - diff / (gradLen * bandScale));

    return lerp(a, feather, edgeSmooth);
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Self-Shadow Rejection
// Filters out occluders that sit close to the receiving pixel along the light
// direction. Self-shadow casters (the character's own hair or arm) always land
// within a small depth of the receiving surface, while environment casters
// (walls, trees, other characters) sit much further along the light direction.
// Reads the raw caster depth from the URP main light shadow atlas and rejects
// the shadow contribution when the gap is too small.
//   rawShadow         : the URP attenuation already sampled via GetMainLight
//   shadowCoord       : the same coord URP used for that sample
//   rejectDist        : threshold in shadow normalized depth (0.001 - 0.05 typical)
// Returns 1.0 (lit) when the occluder is classified as self, the original
// attenuation when it is classified as external, with a smoothstep transition
// in between.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half ZLZ_RejectSelfShadow(half rawShadow, float4 shadowCoord, half rejectDist)
{
    // Skip the texture fetches when there is no occluder anyway.
    if (rawShadow >= 0.99h)
        return rawShadow;

    // Bilinear filter the caster depth by hand. A single point fetch would show
    // the shadow atlas texel grid as visible square blocks on the rejection mask;
    // reading the four nearest texels and lerping produces a smooth caster depth
    // so the rejection edge follows the silhouette instead of the texel boundary.
    // Done in shader (not via a linear sampler) because depth textures cannot be
    // hardware-filtered on every platform we target.
    float2 uvScaled  = shadowCoord.xy * _MainLightShadowmapSize.zw - 0.5;
    int2   baseCoord = int2(floor(uvScaled));
    float2 w         = saturate(uvScaled - float2(baseCoord));

    float d00 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(0, 0)).r;
    float d10 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(1, 0)).r;
    float d01 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(0, 1)).r;
    float d11 = LOAD_TEXTURE2D(_MainLightShadowmapTexture, baseCoord + int2(1, 1)).r;

    float casterDepth = lerp(lerp(d00, d10, w.x), lerp(d01, d11, w.x), w.y);

    // Depth separation between the pixel and the caster along the light view,
    // taken as an absolute value so the same formula works under reverse-Z
    // (Windows / Mac / Mobile) and the legacy forward depth convention. Small
    // value = caster sits on the character itself; large value = external.
    float delta = abs(shadowCoord.z - casterDepth);

    // Smooth transition between "fully reject (self)" and "fully keep (external)".
    // The band starts at zero (not at the threshold) so the transition spans 2x
    // the slider value. The wider band shrinks per-frame rejection jitter when
    // the character rotates and the shadow atlas regenerates, at the cost of a
    // slightly fuzzier self vs external split.
    half guarded   = max(rejectDist, 0.0001h);
    half rejection = smoothstep(0.0h, guarded * 2.0h, delta);

    return lerp(1.0h, rawShadow, rejection);
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// SafeBakedGI
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
float3 SafeBakedGI(
    float3 positionWS,
    float3 normalWS,
    float2 positionSS,  // currently unused, reserved for future screen-space features
    float2 uvStaticLightmap,
    float2 uvDynamicLightmap,
    bool applyScaling
)
{
#if defined(LIGHTMAP_ON)
    float3 result;

    if (all(uvStaticLightmap == 0))
    {
        result = SampleSH(normalWS); // fallback
    }
    else
    {
        if (applyScaling)
        {
            uvStaticLightmap = uvStaticLightmap * unity_LightmapST.xy + unity_LightmapST.zw;
            uvDynamicLightmap = uvDynamicLightmap * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
        }

    #if defined(DYNAMICLIGHTMAP_ON)
        result = SampleLightmap(uvStaticLightmap, uvDynamicLightmap, normalWS);
    #else
        result = SampleLightmap(uvStaticLightmap, normalWS);
    #endif
    }

    return result;
#else
    return SampleSH(normalWS); // fallback
#endif
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Toon Lighting (Shadow → Light Ramp + Direct + Indirect)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
struct ZLZ_ToonLightResult
{
    half  lightRamp01;       // ramp 0–1 for shadow→light
    half3 toonLighting;      // final toon lighting (direct + indirect)
};

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
    ZLZ_ToonLightResult o;

    // Ramp 0–1 (guard against smoothstep UB when ToonRampSmoothness == 0)
    o.lightRamp01 = saturate(smoothstep(0, max(ToonRampSmoothness, 1e-4h), SwitchFS));

    // Toon color
    half3 toonColor = lerp(ShadowColor, BaseColor, o.lightRamp01);

    // Direct light
    half3 directLit_Clamped = toonColor * saturate(lightColor) + AdditionalLightColor;
    half3 directLit_Full    = toonColor * lightColor          + AdditionalLightColor;
    half3 directLit         = lerp(directLit_Clamped, directLit_Full, o.lightRamp01);

    // Final (Direct + Indirect) — indirect tinted toward ShadowColor in unlit areas
    o.toonLighting = directLit + indirectLighting * lerp(ShadowColor, half3(1.0h, 1.0h, 1.0h), o.lightRamp01);

    return o;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Shadow Edge (a.k.a. Core Shadow / Terminator Darkening Band)
// Adds a darker band centered on the light/shadow boundary, tapering to zero in deep
// shadow (where ambient lifts the tone back up) and in full light — so it only deepens
// the transition itself, never over-darkens either side. Pure ALU, no texture. Drives
// form/volume on rounded surfaces (hair, cheeks). The band is built from the post-ramp
// value, so its screen width follows the ramp transition; the exponent shapes it from a
// thin line to a broad band within that transition.
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
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


#endif  // CUSTOM_LIGHTING_INCLUDED