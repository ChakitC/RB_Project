Shader "RB/Sewer Toon/Scroll Ripples"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (0.2, 0.9, 1.2, 1)
        _ScrollSpeed ("Scroll Speed", Vector) = (0, 0.3, 0, 0)
        _DistortionAmount ("Distortion Amount", Range(0, 0.3)) = 0.1
        _VoronoiSpeed ("Voronoi Speed", Float) = 0.4
        _VoronoiScale ("Voronoi Scale", Float) = 10
        _RippleGain ("Ripple Intensity", Range(1, 16)) = 8
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0.4
        _DissolvePower ("Dissolve Power", Range(0.5, 12)) = 5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "RipplesUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MaskTex_ST;
                float4 _Color;
                float4 _ScrollSpeed;
                float _DistortionAmount;
                float _VoronoiSpeed;
                float _VoronoiScale;
                float _RippleGain;
                float _DissolveAmount;
                float _DissolvePower;
            CBUFFER_END

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float Voronoi(float2 p, float timeValue)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float nearest = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 featurePoint = Hash22(cell + offset);
                        featurePoint = 0.5 + 0.5 * sin(timeValue + 6.2831853 * featurePoint);
                        float2 delta = offset + featurePoint - local;
                        nearest = min(nearest, dot(delta, delta));
                    }
                }
                return sqrt(nearest);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float timeValue = _Time.y;
                float2 baseUV = TRANSFORM_TEX(input.uv, _MainTex) + _ScrollSpeed.xy * timeValue;
                float scale = max(_VoronoiScale, 0.001);
                float motion = timeValue * _VoronoiSpeed;
                float2 distortion;
                distortion.x = Voronoi(baseUV * scale + float2(motion, 0.0), motion);
                distortion.y = Voronoi(baseUV * scale + float2(17.31, 9.17) - float2(0.0, motion), motion + 2.1);
                distortion = (distortion - 0.5) * 0.18;

                float2 sampleUV = lerp(baseUV, baseUV + distortion, _DistortionAmount);
                float4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                float4 maskSample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, TRANSFORM_TEX(input.uv, _MaskTex));
                float mask = saturate(max(maskSample.r, maskSample.a) * 2.0);

                float dissolveNoise = 1.0 - saturate(Voronoi(baseUV * scale + motion, motion));
                // Generated Texture2D assets and their mip levels can become
                // very dim at gameplay distance.  Normalize the strongest
                // channel before dissolve so the ring remains readable.
                float ripple = saturate(max(mainSample.r, mainSample.a) * _RippleGain);
                float radialDistance = length(input.uv * 2.0 - 1.0);
                float proceduralRing = 1.0 - smoothstep(0.035, 0.095, abs(radialDistance - 0.62));
                ripple = max(ripple, proceduralRing);
                float brokenRipple = lerp(ripple, ripple * dissolveNoise, _DissolveAmount);
                brokenRipple = pow(saturate(brokenRipple), max(_DissolvePower, 0.001));

                float4 tint = _Color;
                float visibleRipple = max(proceduralRing, brokenRipple * mask);
                float alpha = visibleRipple * tint.a * saturate(input.color.a);
                return half4(tint.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
