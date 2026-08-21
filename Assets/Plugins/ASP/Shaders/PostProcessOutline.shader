/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "Hidden/ASP/PostProcess/Outline"
{
    Properties
    {
        [Space(10)]
        _MaterialThreshold ("Material Threshold", Range(0.05, 1)) = 0.1
        [Space(10)]
        _LumaThreshold ("Luma Threshold", Range(0.05, 1)) = 0.1
        [Space(10)]
        _DepthThreshold ("Depth Threshold", Range(0.1, 1)) = 0.1
        [Space(10)]
        _NormalsThreshold ("Normals Threshold", Range(0.1, 1)) = 0.1
        _DebugEdgeType ("Debug Edge Type", Float) = 6

        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _DebugBackgroundColor("Debug Background Color", Color) = (1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        ZWrite Off
        Cull Off
        ZTest Always
        Pass
        {
            Name "Outline Overlay Pass"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/PostProcessOutlineInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _IS_DEBUG_MODE
            #pragma multi_compile_local _ MATERIAL_EDGE
            #pragma multi_compile_local _ LUMA_EDGE
            #pragma multi_compile_local _ NORMAL_EDGE
            #pragma multi_compile_local _ DEPTH_EDGE
            #pragma multi_compile_local _ SCENE_OBJECT_OUTLINE

            #define RETURN_SKIP \
                _IS_DEBUG_MODE_IFDEF return half4(_DebugBackgroundColor, 1); \
                _IS_DEBUG_MODE_ELSE return half4(0, 0, 0, 0); \
                _IS_DEBUG_MODE_ENDIF

            // Workaround: can't nest #ifdef inside #define, so use inline branch for skip
            half4 SkipPixel()
            {
                #ifdef _IS_DEBUG_MODE
                return half4(_DebugBackgroundColor, 1);
                #else
                return half4(0, 0, 0, 0);
                #endif
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 centerMaterialPassValue = SampleMateriaPass(input.texcoord);
                bool isCharacterPixel = centerMaterialPassValue.r > 0;

                // Early-out: determine if this pixel should be processed
                float sceneDepth = SampleSceneDepth(input.texcoord);
                float linear01Depth = Linear01Depth(sceneDepth, _ZBufferParams);
                bool isSkyPixel = linear01Depth > 0.99999;
                float sceneEyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                #if !SCENE_OBJECT_OUTLINE || MATERIAL_EDGE || LUMA_EDGE
                float characterDepth = SampleCharacterSceneDepth(input.texcoord);
                float characterEyeDepth = LinearEyeDepth(characterDepth, _ZBufferParams);
                #endif
                float isCharacterBlocked = 0;
                // Skip sky pixels (no geometry to outline)
                if (isSkyPixel)
                    return SkipPixel();

                #if !SCENE_OBJECT_OUTLINE
                // Without scene object outline, only process character pixels
                if (!isCharacterPixel)
                    return SkipPixel();

                // Skip character pixels that are occluded by scene geometry
                isCharacterBlocked = step(
                    sceneEyeDepth + ASP_DEPTH_EYE_BIAS,
                    characterEyeDepth);
                if (isCharacterBlocked > 0)
                    return SkipPixel();
                #endif
                
                float2 uvStep = _OutlineWidth / GetScaledScreenParams().xy;
                float vertexWeight = isCharacterPixel ? centerMaterialPassValue.b : 1.0;

                half edgeMaterial = 1;
                half edgeLuma = 1;
                half edgeDepth = 1;
                half edgeNormals = 1;

                #if MATERIAL_EDGE || LUMA_EDGE || DEPTH_EDGE
                float2 crossUVs[4];
                SetupSurroundCrossUVs(input.texcoord, crossUVs, uvStep);
                #endif

                // Material & luma edges only apply to character pixels (require material pass data)
                #if MATERIAL_EDGE || LUMA_EDGE
                if (isCharacterPixel)
                {
                    #if SCENE_OBJECT_OUTLINE
                    isCharacterBlocked = step(sceneEyeDepth + ASP_DEPTH_EYE_BIAS, characterEyeDepth);
                    if (isCharacterBlocked < 1)
                    #endif
                    {
                        half4 mpL = SampleMateriaPass(crossUVs[0]);
                        half4 mpR = SampleMateriaPass(crossUVs[1]);
                        half4 mpT = SampleMateriaPass(crossUVs[2]);
                        half4 mpD = SampleMateriaPass(crossUVs[3]);

                        #if MATERIAL_EDGE
                        {
                            half3 mC = DecodeMaterialIDToColor(centerMaterialPassValue.r);
                            half3 mL = DecodeMaterialIDToColor(mpL.r);
                            half3 mR = DecodeMaterialIDToColor(mpR.r);
                            half3 mT = DecodeMaterialIDToColor(mpT.r);
                            half3 mD = DecodeMaterialIDToColor(mpD.r);

                            half3 mH = (mC - mR) + (mC - mL);
                            half3 mV = (mC - mT) + (mC - mD);

                            half factor = length(half2(length(mH), length(mV)));
                            half threshold = _MaterialThreshold * 0.01;
                            half aaWidth = max(fwidth(factor), threshold);

                            edgeMaterial = 1.0 - smoothstep(
                                threshold - aaWidth * vertexWeight,
                                threshold + aaWidth,
                                factor * vertexWeight);
                        }
                        #endif

                        #if LUMA_EDGE
                        {
                            half lumaC = centerMaterialPassValue.g;
                            half lumaL = mpL.g;
                            half lumaR = mpR.g;
                            half lumaT = mpT.g;
                            half lumaD = mpD.g;

                            half lumaH = (lumaC - lumaR) + (lumaC - lumaL);
                            half lumaV = (lumaC - lumaT) + (lumaC - lumaD);

                            half factor = length(half2(lumaH, lumaV));
                            half threshold = _LumaThreshold;
                            half aaWidth = max(fwidth(factor), threshold);

                            edgeLuma = 1.0 - smoothstep(
                                threshold - aaWidth * vertexWeight,
                                threshold + aaWidth,
                                factor * vertexWeight);
                        }
                        #endif
                    }
                }
                #endif

                #if NORMAL_EDGE
                {
                    float2 normalUVs[4];
                    normalUVs[0] = input.texcoord + uvStep * float2(-1, 1);
                    normalUVs[1] = input.texcoord + uvStep * float2(-1, -1);
                    normalUVs[2] = input.texcoord + uvStep * float2(1, 1);
                    normalUVs[3] = input.texcoord + uvStep * float2(1, -1);

                    half3 nC = SampleSceneNormals(input.texcoord).rgb;
                    half3 nLT = SampleSceneNormals(normalUVs[0]).rgb;
                    half3 nLB = SampleSceneNormals(normalUVs[1]).rgb;
                    half3 nRT = SampleSceneNormals(normalUVs[2]).rgb;
                    half3 nRB = SampleSceneNormals(normalUVs[3]).rgb;

                    half3 nH = clamp(nC - nRT, -5, 5) + clamp(nC - nLB, -5, 5);
                    half3 nV = clamp(nC - nLT, -5, 5) + clamp(nC - nRB, -5, 5);

                    half factor = length(abs(nH + nV)) * 0.25;
                    half aaWidth = fwidth(factor);

                    edgeNormals = 1.0 - smoothstep(
                        _NormalsThreshold - aaWidth,
                        _NormalsThreshold + aaWidth,
                        factor * vertexWeight);
                }
                #endif

                #if DEPTH_EDGE
                {
                    float dL = LinearEyeDepth(SampleSceneDepth(crossUVs[0]), _ZBufferParams);
                    float dR = LinearEyeDepth(SampleSceneDepth(crossUVs[1]), _ZBufferParams);
                    float dT = LinearEyeDepth(SampleSceneDepth(crossUVs[2]), _ZBufferParams);
                    float dD = LinearEyeDepth(SampleSceneDepth(crossUVs[3]), _ZBufferParams);
                    float invDepth = rcp(max(sceneEyeDepth, 1e-5));

                    float dH = ((sceneEyeDepth - dR) + (sceneEyeDepth - dL)) * invDepth;
                    float dV = ((sceneEyeDepth - dT) + (sceneEyeDepth - dD)) * invDepth;

                    float factor = length(float2(dH, dV));

                    float threshold = _DepthThreshold * 0.01;
                    float aaWidth = max(fwidth(factor), threshold);

                    edgeDepth = 1.0 - smoothstep(
                        threshold - aaWidth * vertexWeight,
                        threshold + aaWidth * vertexWeight,
                        factor * vertexWeight
                    );
                }
                #endif

                float finalEdge = min(min(edgeMaterial, edgeLuma), min(edgeDepth, edgeNormals));

                // Distance falloff
                float distanceFalloff = 1.0;
                if (_EnableDistanceFalloff > 0)
                {
                    distanceFalloff = 1.0 - smoothstep(
                        _DistanceFalloffStartEnd.x,
                        _DistanceFalloffStartEnd.y,
                        sceneEyeDepth);
                }

                half4 outlineColor = _OutlineColor;

                #ifdef _IS_DEBUG_MODE
                float debugEdge = finalEdge;
                if (_DebugEdgeType == 0)
                    debugEdge = edgeMaterial;
                else if (_DebugEdgeType == 1)
                    debugEdge = edgeLuma;
                else if (_DebugEdgeType == 2)
                    debugEdge = edgeDepth;
                else if (_DebugEdgeType == 3)
                    debugEdge = edgeNormals;

                float hasDebugEdge = (1.0 - debugEdge) * distanceFalloff;

                if (_DebugEdgeType == 4)
                {
                    float3 debugBaseColor = lerp(float3(1, 1, 0), _DebugBackgroundColor, centerMaterialPassValue.b);
                    return half4(debugBaseColor, 1);
                }

                if (_DebugEdgeType == 5)
                {
                    float3 debugBaseColor = lerp(float3(1, 1, 0), _DebugBackgroundColor, centerMaterialPassValue.b);
                    return half4(lerp(debugBaseColor, outlineColor.rgb, hasDebugEdge), 1);
                }

                return half4(lerp(_DebugBackgroundColor.rgb, outlineColor.rgb, hasDebugEdge), 1);
                #endif

                float outlineOpacity = saturate(1.0 - finalEdge);
                return half4(outlineColor.rgb, outlineColor.a * outlineOpacity * distanceFalloff);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
