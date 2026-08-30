Shader "ZLZ/ScreenSpaceOutline"
{
    // Fullscreen overlay that draws an outline from a CHARACTER-ONLY depth + normal
    // buffer captured by ZLZ_ScreenSpaceOutlineFeature. It complements the per-object
    // inverted-hull outline: the hull draws the OUTER silhouette, this pass adds the
    // INTERIOR detail lines (creases, overlapping parts) that a hull cannot produce.
    //
    // Inputs (set as globals by the feature):
    //   _ZLZSSO_Normals : world-space normal (xyz). Length ~1 where a character was
    //                     drawn this frame, 0 elsewhere -> doubles as a coverage mask.
    //   _ZLZSSO_Depth   : character camera-view raw device depth.
    //   _CameraDepthTexture : scene depth, used only to hide the line behind geometry.

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ZLZ Screen Space Outline"

            Cull   Off
            ZWrite Off
            ZTest  Always
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex   Vert
            #pragma fragment frag

            // Each edge source is a separate keyword so unused work is stripped on
            // mobile / performance builds via the shader optimizer.
            #pragma multi_compile_local _ ZLZ_SSO_DEPTH_EDGE
            #pragma multi_compile_local _ ZLZ_SSO_NORMAL_EDGE
            #pragma multi_compile_local _ ZLZ_SSO_OCCLUSION
            #pragma multi_compile_local _ ZLZ_SSO_DISTANCE_FADE
            #pragma multi_compile_local _ ZLZ_SSO_DEBUG

            TEXTURE2D(_ZLZSSO_Normals);
            SAMPLER(sampler_ZLZSSO_Normals);
            TEXTURE2D(_ZLZSSO_Depth);
            SAMPLER(sampler_ZLZSSO_Depth);

            float4 _ZLZSSO_Normals_TexelSize;   // auto-filled: 1/width, 1/height, width, height

            CBUFFER_START(UnityPerMaterial)
                float4 _ZLZSSO_Color;    // rgb = line color
                float4 _ZLZSSO_Params;   // x = thickness (px), y = depth sensitivity, z = normal sensitivity, w = intensity
                float4 _ZLZSSO_Params2;  // x = occlusion bias (metres)
                float4 _ZLZSSO_FadeParams; // x = fade start (m), y = fade end (m), z = far intensity
                float  _ZLZSSO_DebugMode;  // 0=Coverage 1=Normal 2=Depth 3=Combined 4=Fade 5=Occlusion
            CBUFFER_END

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Coverage: keep the line on the character only (length^2 of a unit
                // normal is ~1; the cleared background is 0). This confines the whole
                // effect to ZLZ characters and avoids re-lining the hull silhouette.
                // alpha (w) carries the per-material Outline Mask written by the character
                // DepthNormals pass: 1 = show outline, 0 = hide (same mask as the Hull outline).
                half4 nSample  = SAMPLE_TEXTURE2D(_ZLZSSO_Normals, sampler_ZLZSSO_Normals, uv);
                float3 nC = nSample.xyz;
                float outlineMask = nSample.w;
                float coverage = step(0.25, dot(nC, nC));

                #ifndef ZLZ_SSO_DEBUG
                    // Fast path: skip everything off the character (debug needs every pixel).
                    if (coverage < 0.5) return 0;
                #endif

                float  thickness = max(_ZLZSSO_Params.x, 0.5);
                float2 off       = _ZLZSSO_Normals_TexelSize.xy * thickness;

                // Roberts cross (two diagonals) — cheap, crisp for outlines.
                float2 uvTL = uv + float2(-off.x,  off.y);
                float2 uvTR = uv + float2( off.x,  off.y);
                float2 uvBL = uv + float2(-off.x, -off.y);
                float2 uvBR = uv + float2( off.x, -off.y);

                float normalEdge = 0.0;
                float depthEdge  = 0.0;

                #ifdef ZLZ_SSO_NORMAL_EDGE
                    float3 nTL = SAMPLE_TEXTURE2D(_ZLZSSO_Normals, sampler_ZLZSSO_Normals, uvTL).xyz;
                    float3 nTR = SAMPLE_TEXTURE2D(_ZLZSSO_Normals, sampler_ZLZSSO_Normals, uvTR).xyz;
                    float3 nBL = SAMPLE_TEXTURE2D(_ZLZSSO_Normals, sampler_ZLZSSO_Normals, uvBL).xyz;
                    float3 nBR = SAMPLE_TEXTURE2D(_ZLZSSO_Normals, sampler_ZLZSSO_Normals, uvBR).xyz;

                    float nd = length(nTL - nBR) + length(nTR - nBL);
                    // Higher sensitivity -> lower threshold -> more lines.
                    float nThresh = lerp(1.4, 0.15, saturate(_ZLZSSO_Params.z));
                    normalEdge = saturate((nd - nThresh) * 4.0);
                #endif

                #ifdef ZLZ_SSO_DEPTH_EDGE
                    float dC  = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uv  ).r, _ZBufferParams);
                    float dTL = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uvTL).r, _ZBufferParams);
                    float dTR = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uvTR).r, _ZBufferParams);
                    float dBL = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uvBL).r, _ZBufferParams);
                    float dBR = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uvBR).r, _ZBufferParams);

                    float dd   = abs(dTL - dBR) + abs(dTR - dBL);
                    float dRel = dd / max(dC, 0.01);            // distance-independent
                    float dThresh = lerp(0.5, 0.01, saturate(_ZLZSSO_Params.y));
                    depthEdge = saturate((dRel - dThresh) * 8.0);
                #endif

                float edge = max(normalEdge, depthEdge);

                // Occlusion: 1 where the character pixel sits behind scene geometry.
                float occluded = 0.0;
                #ifdef ZLZ_SSO_OCCLUSION
                    float charLin  = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uv).r, _ZBufferParams);
                    float sceneLin = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    occluded = step(sceneLin + _ZLZSSO_Params2.x, charLin);
                #endif

                // Distance fade multiplier (1 near -> far intensity far).
                float fadeMul = 1.0;
                #ifdef ZLZ_SSO_DISTANCE_FADE
                    float eyeD  = LinearEyeDepth(SAMPLE_TEXTURE2D(_ZLZSSO_Depth, sampler_ZLZSSO_Depth, uv).r, _ZBufferParams);
                    float fadeT = saturate((eyeD - _ZLZSSO_FadeParams.x) / max(_ZLZSSO_FadeParams.y - _ZLZSSO_FadeParams.x, 1e-3));
                    fadeMul = lerp(1.0, _ZLZSSO_FadeParams.z, fadeT);
                #endif

                #ifdef ZLZ_SSO_DEBUG
                    // Opaque full-screen visualization of one channel, masked to the
                    // character — makes each slider's effect directly observable.
                    int   mode    = (int)(_ZLZSSO_DebugMode + 0.5);
                    float visible = coverage * (1.0 - occluded) * outlineMask;
                    float dbg     = coverage;                      // 0 = Coverage
                    if      (mode == 1) dbg = normalEdge * visible; // Normal Edge
                    else if (mode == 2) dbg = depthEdge  * visible; // Depth Edge
                    else if (mode == 3) dbg = edge       * visible; // Combined Edge
                    else if (mode == 4) dbg = fadeMul    * coverage;// Distance Fade factor
                    else if (mode == 5) dbg = visible;              // Occlusion (white = visible)
                    return half4(dbg.xxx, 1.0);
                #endif

                #ifdef ZLZ_SSO_OCCLUSION
                    if (occluded > 0.5) return 0;
                #endif

                float alpha = saturate(edge) * saturate(_ZLZSSO_Params.w) * fadeMul * outlineMask;
                return half4(_ZLZSSO_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
