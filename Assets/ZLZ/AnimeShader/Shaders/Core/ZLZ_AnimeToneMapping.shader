Shader "ZLZ/AnimeToneMapping"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "ZLZ Anime Tone Curve"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile_local _ ZLZ_CURVE_FILMIC

            // --- Textures -------------------------------------------------------
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // --- Anime Curve Parameters -----------------------------------------
            float _Exposure;
            float _ToneMapScale;
            float _HighlightRolloff;
            float _HighlightLift;
            float _ShadowPedestal;
            float _MidContrast;
            float _SaturationRetention;
            float _WhiteClip;

            // --- Filmic Curve Parameters ----------------------------------------
            float _FilmicExposure;
            float _FilmicToneMapScale;
            float _ShoulderStrength;
            float _LinearStrength;
            float _LinearAngle;
            float _ToeStrength;
            float _BlackLevel;
            float _DenominatorBalance;
            float _WhitePoint;
            float _FilmicSaturation;

            // ===================================================================
            // MODE A: ZLZ Anime Curve
            // ===================================================================

            float ZLZAnimeCurve_Channel(float x)
            {
                float lifted = max(x - _ShadowPedestal, 0.0);

                float knee = 0.5 * _WhiteClip;
                float contrastPow = 1.0 + _MidContrast * 0.6;
                float midtone = pow(saturate(lifted / knee), contrastPow) * knee;

                float kRolloff = _HighlightRolloff * 3.0 + 0.5;
                float highlight = knee + (1.0 - exp(-kRolloff * max(lifted - knee, 0.0)))
                                  * (_WhiteClip - knee + _HighlightLift);

                float blend = smoothstep(knee * 0.6, knee * 1.2, lifted);
                float curved = lerp(midtone, highlight, blend);

                return curved / (_WhiteClip + _HighlightLift);
            }

            float3 ApplyAnimeCurve(float3 hdrColor)
            {
                float3 exposed = hdrColor * _Exposure * _ToneMapScale;

                float3 curved = float3(
                    ZLZAnimeCurve_Channel(exposed.r),
                    ZLZAnimeCurve_Channel(exposed.g),
                    ZLZAnimeCurve_Channel(exposed.b)
                );

                // Saturation retention
                float lumOut = Luminance(curved);
                float3 grey = lumOut.xxx;
                float satFactor = 1.0 + saturate(_SaturationRetention) * 1.5;
                float3 boosted = lerp(grey, curved, satFactor);
                float mask = smoothstep(0.15, 0.5, lumOut) * (1.0 - smoothstep(0.75, 0.95, lumOut));
                curved = lerp(curved, boosted, mask);

                return saturate(curved);
            }

            // ===================================================================
            // MODE B: ZLZ Filmic Curve
            // Formula adapted from John Hable's "Uncharted 2" filmic tonemapper
            // (GDC 2010, "Filmic Tonemapping for Real-time Rendering", Public Domain)
            // ref: https://www.gdcvault.com/play/1012351
            // ZLZ extensions: per-channel saturation control (_FilmicSaturation),
            //                 separate exposure/scale from Anime mode
            // ===================================================================

            float3 ZLZFilmicEval(float3 x)
            {
                float A  = _ShoulderStrength;
                float B  = _LinearStrength;
                float C  = _LinearAngle;
                float D  = _ToeStrength;
                float E  = _BlackLevel;
                float Fv = _DenominatorBalance;

                return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * Fv)) - E / Fv;
            }

            float3 ApplyFilmicCurve(float3 hdrColor)
            {
                float3 col = _FilmicToneMapScale * _FilmicExposure * hdrColor;
                float3 whiteNorm = ZLZFilmicEval(_WhitePoint.xxx);
                float3 mapped = saturate(ZLZFilmicEval(col) / whiteNorm);

                float lum = Luminance(mapped);
                float3 grey = lum.xxx;
                mapped = lerp(grey, mapped, max(0.0, _FilmicSaturation));

                return saturate(mapped);
            }

            // --- Fragment -------------------------------------------------------
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);

                float3 result;

                #if defined(ZLZ_CURVE_FILMIC)
                    result = ApplyFilmicCurve(col.rgb);
                #else
                    result = ApplyAnimeCurve(col.rgb);
                #endif

                return half4(result, col.a);
            }

            ENDHLSL
        }
    }
}
