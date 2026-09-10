Shader "RB/Sewer Toon/Waterfall"
{
    Properties
    {
        [HDR] _Color ("Base Water Color", Color) = (0.15, 0.8, 1.2, 0.85)
        [HDR] _RippleColor ("Ripple Color", Color) = (0.1, 0.9, 1.4, 0.9)
        [Toggle] _InvertRipples ("Invert Ripples", Float) = 0
        [HDR] _BottomFoamColor ("Bottom Foam Color", Color) = (0.4, 1.5, 1.8, 1)
        _VoronoiScale ("Voronoi Scale", Float) = 12
        _VerticalCellScale ("Vertical Cell Scale", Range(0.08, 1)) = 0.3
        _RippleSpeed ("Ripple Speed", Vector) = (0, 0.5, 0, 0)
        _VoronoiSpeed ("Voronoi Speed", Float) = 3
        _RipplesAmount ("Ripples Amount", Range(0.5, 8)) = 3.3
        _BottomStrength ("Bottom Strength", Range(0.25, 12)) = 4
        _BottomBrightness ("Bottom Brightness", Range(0, 4)) = 1.5
        _NoiseScale ("Bottom Noise Scale", Float) = 20
        _Erosion ("Erosion", Range(0, 1)) = 0
        _VertexOffsetAmount ("Vertex Offset Amount", Range(0, 0.25)) = 0
        _BottomFadeStart ("Bottom Fade Start", Range(0, 1)) = 0.18
        _BottomFadeEnd ("Bottom Fade End", Range(0, 1)) = 0.48
        _BottomFadePower ("Bottom Fade Power", Range(0.25, 8)) = 1.4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite ("Depth Write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "WaterfallUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RippleColor;
                float _InvertRipples;
                float4 _BottomFoamColor;
                float4 _RippleSpeed;
                float _VoronoiScale;
                float _VerticalCellScale;
                float _VoronoiSpeed;
                float _RipplesAmount;
                float _BottomStrength;
                float _BottomBrightness;
                float _NoiseScale;
                float _Erosion;
                float _VertexOffsetAmount;
                float _BottomFadeStart;
                float _BottomFadeEnd;
                float _BottomFadePower;
                float _ZWrite;
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

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash22(i).x;
                float b = Hash22(i + float2(1, 0)).x;
                float c = Hash22(i + float2(0, 1)).x;
                float d = Hash22(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float timeValue = _Time.y;
                float2 flowingUV = input.uv + _RippleSpeed.xy * timeValue;
                float cells = 1.0 - saturate(Voronoi(flowingUV * float2(max(_VoronoiScale, 0.001), max(_VoronoiScale * _VerticalCellScale, 0.001)), timeValue * _VoronoiSpeed));
                cells = lerp(cells, 1.0 - cells, step(0.5, _InvertRipples));
                float displacement = (cells * 2.0 - 1.0) * _VertexOffsetAmount;
                float3 displacedPositionOS = input.positionOS.xyz + normalize(input.normalOS) * displacement;
                output.positionHCS = TransformObjectToHClip(displacedPositionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float timeValue = _Time.y;
                float2 flowingUV = input.uv + _RippleSpeed.xy * timeValue;
                float cells = 1.0 - saturate(Voronoi(flowingUV * float2(max(_VoronoiScale, 0.001), max(_VoronoiScale * _VerticalCellScale, 0.001)), timeValue * _VoronoiSpeed));
                float rippleShape = pow(max(cells, 0.0001), max(_RipplesAmount, 0.001));
                float rippleTint = lerp(rippleShape, 1.0 - rippleShape, step(0.5, _InvertRipples));
                float ripple = lerp(0.1, 1.0, rippleTint);

                float bottomGradient = pow(saturate(1.0 - input.uv.y), max(_BottomStrength, 0.001));
                float bottomNoise = ValueNoise(input.uv * max(_NoiseScale, 0.001) + float2(0.0, -timeValue * 0.35));
                float bottomFoam = bottomGradient * lerp(0.25, 1.0, bottomNoise) * _BottomBrightness;

                float pattern = saturate(ripple + bottomFoam);
                // The depth-writing material is the solid water sheet.  It must
                // never be cut out by the decorative erosion/fade masks, or the
                // wall and the rear side of the mesh become visible through it.
                // The non-depth-writing material remains the transparent foam
                // overlay and keeps the original cutout behaviour.
                float isSolidWater = step(0.5, _ZWrite);
                clip(lerp(pattern - _Erosion, 1.0, isSolidWater));
                float fadeRange = max(_BottomFadeEnd - _BottomFadeStart, 0.0001);
                float bottomFade = saturate((input.uv.y - _BottomFadeStart) / fadeRange);
                bottomFade = pow(bottomFade, max(_BottomFadePower, 0.001));
                float bottomTint = saturate(bottomFoam);
                float3 finalColor = lerp(_Color.rgb, _RippleColor.rgb, rippleTint);
                finalColor = lerp(finalColor, _BottomFoamColor.rgb, bottomTint);

                float colorAlpha = lerp(_Color.a, _RippleColor.a, rippleTint);
                colorAlpha = lerp(colorAlpha, _BottomFoamColor.a, bottomTint);
                float effectAlpha = colorAlpha * saturate((pattern - _Erosion) * 8.0 + 0.08) * bottomFade;

                // On the solid sheet, composite the animated effect over the
                // base colour inside this pass, then output opaque alpha.  This
                // keeps the authored foam/ripple shapes while filling their
                // transparent gaps with water instead of the scene behind it.
                float3 solidColor = lerp(_Color.rgb, finalColor, saturate(effectAlpha));
                finalColor = lerp(finalColor, solidColor, isSolidWater);
                float alpha = lerp(effectAlpha, 1.0, isSolidWater);
                clip(alpha - 0.005);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
