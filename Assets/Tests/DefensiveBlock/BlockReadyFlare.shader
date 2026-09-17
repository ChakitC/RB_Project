Shader "RB/Defensive Block Ready Flare"
{
    Properties
    {
        [HDR] _Color ("Gold", Color) = (1, 0.42, 0.025, 1)
        _Intensity ("Intensity", Float) = 1.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = abs(i.uv * 2 - 1);
                float horizontalFalloff = pow(saturate(1-p.x), 0.4);
                float verticalFalloff = pow(saturate(1-p.y), 1.8);
                float horizontal = exp2(-p.y * 190) * horizontalFalloff;
                float vertical = exp2(-p.x * 330) * verticalFalloff;
                float glow = exp2(-p.y * 35) * horizontalFalloff * 0.22
                    + exp2(-p.x * 65) * verticalFalloff * 0.13;
                float star = exp2(-abs(p.x-p.y*0.36) * 200) * exp2(-(p.x+p.y)*28);
                float halo = exp2(-dot(p,p) * 280) * 0.3;
                float3 gold = _Color.rgb * (horizontal + vertical * 0.85 + glow + halo + star);
                float3 core = float3(1,0.85,0.38) * pow(saturate(horizontal + vertical), 4) * 0.75;
                return half4((gold + core) * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
