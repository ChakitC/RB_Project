Shader "RB/Special Shoot Point Target"
{
    // Unlit and additive on purpose: the marker has to read the same in the dark boss arena as in a
    // lit room, and it is a HUD-like cue rather than a physical object.
    //
    // _Fill, _Flash and _Alpha exist because SpecialShootPointInstance already drives exactly those
    // three names through a MaterialPropertyBlock. With a stock URP material those writes are
    // silently ignored, which is why the point used to pop out of existence instead of draining and
    // fading.
    Properties
    {
        _BaseMap ("Target Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 0.62, 0.08, 1)
        _FlashColor ("Flash Tint", Color) = (1, 1, 0.9, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.25

        // Remaining point HP, 1 -> 0. Sweeps the ring away clockwise from the top.
        _Fill ("Fill", Range(0, 1)) = 1
        // Hit flash and the last-second warning pulse.
        _Flash ("Flash", Range(0, 1)) = 0
        // Resolve fade.
        _Alpha ("Alpha", Range(0, 1)) = 1

        // How strongly the marker shows through geometry that occludes it. Driven per instance:
        // the runtime only raises it for a point the player could actually still hit from here, so
        // a marker on the far side of the body stays hidden and the player has to reposition.
        _OccludedStrength ("Occluded Strength", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
    };

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float4 _FlashColor;
        float  _Intensity;
        float  _Fill;
        float  _Flash;
        float  _Alpha;
        float  _OccludedStrength;
    CBUFFER_END

    Varyings vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        return output;
    }

    // Shared shading for both the visible and the see-through pass.
    half4 ShadeTarget(float2 uv, half weight)
    {
        half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

        // Radial wipe for remaining HP: 0 rad at the top, sweeping clockwise.
        float2 centred = uv - 0.5;
        float angle = atan2(centred.x, centred.y);           // -PI..PI, 0 at the top
        float sweep = (angle < 0 ? angle + 6.2831853 : angle) / 6.2831853;
        float fillMask = step(sweep, saturate(_Fill));

        // The very centre has no meaningful angle, so never wipe it.
        float radius = length(centred) * 2.0;
        fillMask = max(fillMask, 1.0 - smoothstep(0.0, 0.12, radius));

        half3 colour = lerp(_BaseColor.rgb, _FlashColor.rgb, saturate(_Flash));
        half intensity = _Intensity * (1.0 + saturate(_Flash) * 1.6);

        // Blend is SrcAlpha One, so alpha already weights the contribution. Multiplying it into rgb
        // as well squares it and eats the softer glow entirely.
        half alpha = tex.a * _BaseColor.a * saturate(_Alpha) * fillMask * weight;
        return half4(tex.rgb * colour * intensity, alpha);
    }

    half4 fragVisible(Varyings input) : SV_Target
    {
        return ShadeTarget(input.uv, 1.0h);
    }

    half4 fragOccluded(Varyings input) : SV_Target
    {
        // Dimmed on purpose: the player should read "it is behind something" at a glance rather
        // than mistake it for a clear shot.
        return ShadeTarget(input.uv, saturate(_OccludedStrength) * 0.45h);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // See-through pass first: only where the marker is actually behind geometry.
        Pass
        {
            Name "TargetOccluded"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest Greater
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragOccluded
            ENDHLSL
        }

        Pass
        {
            Name "Target"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragVisible
            ENDHLSL
        }
    }

    Fallback Off
}
