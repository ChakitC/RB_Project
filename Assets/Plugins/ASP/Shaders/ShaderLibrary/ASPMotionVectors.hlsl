#ifndef ASP_MOTION_VECTOR_INCLUDED
#define ASP_MOTION_VECTOR_INCLUDED

#pragma target 3.5
#pragma vertex ASPMotionVectorVertex
#pragma fragment ASPMotionVectorFragment
#pragma multi_compile_instancing

#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#if UNITY_VERSION >= 202301
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#else
#include "../SRP/FoveatedRenderingKeywords.hlsl"
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
#if UNITY_VERSION >= 60000001
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#else
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

void ApplyMotionVectorZBias(inout float4 positionCS)
{
    #if defined(UNITY_REVERSED_Z)
        positionCS.z -= unity_MotionVectorsParams.z * positionCS.w;
    #else
        positionCS.z += unity_MotionVectorsParams.z * positionCS.w;
    #endif
}

float2 CalcNdcMotionVectorFromCsPositions(float4 posCS, float4 prevPosCS)
{
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
    {
        return float2(0.0, 0.0);
    }

    float2 posNDC = posCS.xy * rcp(posCS.w);
    float2 prevPosNDC = prevPosCS.xy * rcp(prevPosCS.w);

    half2 velocity;
    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    {
        half2 posUV = RemapFoveatedRenderingLinearToNonUniform(posNDC * 0.5 + 0.5);
        half2 prevPosUV = RemapFoveatedRenderingPrevFrameLinearToNonUniform(prevPosNDC * 0.5 + 0.5);
        velocity = posUV - prevPosUV;
        #if UNITY_UV_STARTS_AT_TOP
            velocity.y = -velocity.y;
        #endif
    }
    else
    #endif
    {
        velocity = posNDC - prevPosNDC;
        #if UNITY_UV_STARTS_AT_TOP
            velocity.y = -velocity.y;
        #endif
        velocity *= 0.5;
    }

    return velocity;
}
#endif
#if defined(LOD_FADE_CROSSFADE)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "ASPLitInput.hlsl"
#include "ASPCommon.hlsl"

struct ASPMotionVectorAttributes
{
    float4 position : POSITION;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float3 positionOld : TEXCOORD4;
#if _ADD_PRECOMPUTED_VELOCITY
    float3 alembicMotionVector : TEXCOORD5;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ASPMotionVectorVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
    float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
    float4 uv : TEXCOORD0;
    float4 positionSS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float4 GetASPMotionVectorUVs(ASPMotionVectorAttributes input)
{
    float2 baseUV = input.uv0 * _SurfaceUVCtrl.x +
                    input.uv1 * _SurfaceUVCtrl.y +
                    input.uv2 * _SurfaceUVCtrl.z +
                    input.uv3 * _SurfaceUVCtrl.w;

    float2 clipUV = input.uv0 * _AlphaClipUVCtrl.x +
                    input.uv1 * _AlphaClipUVCtrl.y +
                    input.uv2 * _AlphaClipUVCtrl.z +
                    input.uv3 * _AlphaClipUVCtrl.w;

    return float4(baseUV, clipUV);
}

ASPMotionVectorVaryings ASPMotionVectorVertex(ASPMotionVectorAttributes input)
{
    ASPMotionVectorVaryings output = (ASPMotionVectorVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = GetFOVAdjustedPositionOS(input.position.xyz, _CharacterCenterWS, _FOVShiftX);
    const VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);

    output.uv = GetASPMotionVectorUVs(input);
    output.positionSS = ComputeScreenPos(vertexInput.positionCS);

#if defined(APPLICATION_SPACE_WARP_MOTION)
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, float4(positionOS, 1.0)));
    output.positionCS = output.positionCSNoJitter;
#else
    output.positionCS = vertexInput.positionCS;
    ApplyMotionVectorZBias(output.positionCS);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, float4(positionOS, 1.0)));
#endif

    float3 previousPositionOS = (unity_MotionVectorsParams.x == 1) ? input.positionOld : input.position.xyz;
#if _ADD_PRECOMPUTED_VELOCITY
    previousPositionOS -= input.alembicMotionVector;
#endif
    previousPositionOS = GetFOVAdjustedPositionOS(previousPositionOS, _CharacterCenterWS, _FOVShiftX);
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, float4(previousPositionOS, 1.0)));

    return output;
}

void ASPApplyMotionVectorAlphaClip(ASPMotionVectorVaryings input)
{
#if defined(_ALPHATEST_ON)
    float ditherOut = 1.0;
    Unity_Dither((1.0 - _Dithering), input.positionSS.xy / input.positionSS.w, _ScreenParams.xy, _DitherTexelSize,
                 ditherOut);
    clip(ditherOut);

    half alpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy).a;
    clip(alpha - _Cutoff);

    float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, input.uv.zw).r;
    clip(clipMapValue - _Cutoff);
#endif
}

float4 ASPMotionVectorFragment(ASPMotionVectorVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ASPApplyMotionVectorAlphaClip(input);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

#if defined(APPLICATION_SPACE_WARP_MOTION)
    return float4(CalcAswNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 1);
#else
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
#endif
}

#endif
