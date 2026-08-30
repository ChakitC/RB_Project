#ifndef ZLZ_CHARACTERSURFACE_INCLUDED
#define ZLZ_CHARACTERSURFACE_INCLUDED

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Diffuse (Brightness)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half4 ZLZ_ComputeDiffuseBright(
    half4 mainTex,           // TMain
    half  textureBrightness  // _Texture_Brightness
)
{
    return mainTex * textureBrightness;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Hair Highlight
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half3 ZLZ_ComputeHairHighlight(
    half  lightRamp01,          // toon light ramp 0–1
    half  hairMask,             // Feature Mask sample (channel chosen via _HairHighlightMaskCh)
    half3 hairColor,            // _ColorHair.rgb
    half  hairHighlightValue,   // _Hair_HighlightValue
    half3 lightColor            // main light color
)
{
    // Factor 0–1 hair highlight
    half  hairFactor01 = lightRamp01 * hairMask;

    // ((factor * hairColor) * intensity) * lightColor
    half3 hairHighlight = ((hairFactor01 * hairColor) * hairHighlightValue) * lightColor;

    return hairHighlight;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Specular  —  Stylized Blinn-Phong with toon-step
// Cost : ~16 ALU (mask is shared via Feature Mask channel picker — no extra sample)
// Use  : eyes, lips, leather, jewellery, weapons, accessories — anything that should "shine"
// Note : multiplies by lightRamp01 so spec stays on lit side only (anime convention)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
#if defined(_SPECULAR_ON)
half3 ZLZ_ComputeSpecular(
    half3 normalWS,         // post-normal-map normal
    half3 lightDirWS,       // main light direction
    half3 viewDirWS,        // camera direction
    half3 lightColor,       // main light color
    half  specMask,         // Feature Mask sample (channel chosen via _SpecularMaskCh)
    half  lightRamp01       // toon ramp — keep highlight on lit side only
)
{
    // Half-vector Blinn-Phong
    half3 H     = normalize(lightDirWS + viewDirWS);
    half  NdotH = saturate(dot(normalWS, H));

    // Phong-style exponent (sharpness controls highlight tightness)
    half specRaw = pow(NdotH, max(_SpecularSharpness, 1.0h));

    // Toon-step blend: 0 = smooth Phong, 1 = hard binary anime edge
    half soft     = lerp(0.5h, 0.02h, saturate(_SpecularToonStep));
    half thr      = saturate(_SpecularThreshold);
    half specToon = smoothstep(thr - soft, thr + soft, specRaw);

    // Final colour
    half3 spec = specToon * _SpecularColor.rgb;
    spec *= _SpecularIntensity;
    spec *= specMask;
    spec *= saturate(lightRamp01 * 2.0h);   // lit-side only
    spec *= lightColor;

    return spec;
}
#endif

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Emissive Glow
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half3 ZLZ_ComputeEmissiveGlow(
    half4 diffuseBright,    // diffuse
    half  emissiveMask,     // Feature Mask sample (channel chosen via _EmissiveMaskCh)
    half3 emissiveColor,    // _Emissive_Color
    half  emissiveIntensity,// _Emissive_Intensity
    half3 baseLitColor      // baseLitColor + emissive
)
{
    // emissive = (diffuseBright * mask * color * intensity)
    half3 emissiveBright = (diffuseBright.rgb * emissiveMask) * emissiveColor * emissiveIntensity;

    // final = base lit + emissive
    return baseLitColor + emissiveBright;
}

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Metallic Toon Ramp (Normal Map + Gradient Ramp)
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
#if defined(_USEMETALLIC_ON)
half ZLZ_ComputeMetalHighlight(
    half3 geomNormalWS,    // N_main (geometry/world normal)
    half3 lightDirWS,      // lightDir (world space)
    half2 uvNormal,        // i.uvNormal
    half3 tangentWS,       // i.tangentWS
    half3 bitangentWS      // i.bitangentWS
)
{
    // Normal Map > World
    half3 normalTS = UnpackNormal(
        SAMPLE_TEXTURE2D(_MetalNormalMap, sampler_MetalNormalMap, uvNormal)
    );

    // TBN Matrix from vertex
    half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, geomNormalWS);
    half3 normalWS = normalize(mul(normalTS, tangentToWorld));

    // Combine Geometry Normal + Normal Map
    half3 metalNormal = normalize(geomNormalWS + normalWS);

    // Gradient Lookup for Toon Shade
    half metalNdotL = saturate(dot(metalNormal, lightDirWS));
    half2 uvGradientMetal = half2(metalNdotL, 0.5h);
    half metalGradient = SAMPLE_TEXTURE2D(_GradientMetallic, sampler_GradientMetallic, uvGradientMetal).r;

    // Output Metallic Toon Ramp
    half metalHighlight = metalGradient * _MetalIntensity;

    return metalHighlight;
}
#endif

// ----------------------------------------------------------------------------------------------------------------------------------------------------------
// ZLZ Body Normal Map
// ----------------------------------------------------------------------------------------------------------------------------------------------------------
half3 ZLZ_SampleNormal(
    half2    uv,
    half3    tangentWS,
    half3    bitangentWS,
    half3    geomNormalWS,
    half     strength
)
{
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), strength);
    half3x3 TBN    = half3x3(tangentWS, bitangentWS, geomNormalWS);
    return NormalizeNormalPerPixel(mul(normalTS, TBN));
}

#endif // ZLZ_CHARACTERSURFACE_INCLUDED