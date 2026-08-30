#ifndef OUTLINE_UTIL_INCLUDED
#define OUTLINE_UTIL_INCLUDED

//--------------------------------------------------------------------------------------------------------------------------------------------
// Some outline utility code adapted from:
// "Unity URP Toon Lit Shader Example" by ColinLeung-NiloCat
// Source: https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
// Licensed under the MIT License.
//--------------------------------------------------------------------------------------------------------------------------------------------
//NiloOutlineUtil
float GetCameraFOV()
{
    float t = unity_CameraProjection._m11;
    float Rad2Deg = 180 / 3.1415;
    return atan(1.0f / t) * 2.0 * Rad2Deg;
}

float ApplyOutlineDistanceFadeOut(float inputMulFix)
{
    return inputMulFix;
}

float GetOutlineCameraFovAndDistanceFixMultiplier(float positionVS_Z)
{
    float cameraMulFix;

    if(unity_OrthoParams.w == 0)
    {
        // -------------------------------
        // Perspective camera case
        // -------------------------------
        float z = abs(positionVS_Z);

        float zNorm = saturate(z / 50.0);
        float zFix  = sqrt(zNorm);

        cameraMulFix = zFix * 5.0;
        cameraMulFix *= GetCameraFOV();
    }
    else
    {
        // -------------------------------
        // Orthographic camera case
        // -------------------------------
        float orthoSize = abs(unity_OrthoParams.y);
        orthoSize = ApplyOutlineDistanceFadeOut(orthoSize);
        cameraMulFix = orthoSize * 50;
    }

    return cameraMulFix * 0.00005;
}


//--------------------------------------------------------------------------------------------------------------------------------------------
//NiloZOffset

float4 NiloGetNewClipPosWithZOffset(float4 originalPositionCS, float viewSpaceZOffsetAmount)
{
    if(unity_OrthoParams.w == 0)
    {
        ////////////////////////////////
        //Perspective camera case
        ////////////////////////////////
        float2 ProjM_ZRow_ZW = UNITY_MATRIX_P[2].zw;
        float modifiedPositionVS_Z = -originalPositionCS.w + -viewSpaceZOffsetAmount; // push imaginary vertex
        float modifiedPositionCS_Z = modifiedPositionVS_Z * ProjM_ZRow_ZW[0] + ProjM_ZRow_ZW[1];
        originalPositionCS.z = modifiedPositionCS_Z * originalPositionCS.w / (-modifiedPositionVS_Z); // overwrite positionCS.z
        return originalPositionCS;    
    }
    else
    {
        ////////////////////////////////
        //Orthographic camera case
        ////////////////////////////////
        originalPositionCS.z += -viewSpaceZOffsetAmount / _ProjectionParams.z; // push imaginary vertex and overwrite positionCS.z
        return originalPositionCS;
    }
}

//--------------------------------------------------------------------------------------------------------------------------------------------
//SimpleURPToonLitOutlineExample_Shared

float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS)
{
    float outlineExpandAmount = _OutlineWidth * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);

    #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(UNITY_STEREO_DOUBLE_WIDE_ENABLED)
    outlineExpandAmount *= 0.5;
    #endif

    return positionWS + normalWS * outlineExpandAmount;
}

#endif