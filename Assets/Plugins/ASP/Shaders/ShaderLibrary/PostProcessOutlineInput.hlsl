#ifndef ANIME_SHADING_POST_PROCESS_OUTLINE_INPUT_INCLUDED
#define ANIME_SHADING_POST_PROCESS_OUTLINE_INPUT_INCLUDED

float _OutlineWidth;

float _MaterialThreshold;

float _LumaThreshold;

float _DepthThreshold;

float _NormalsThreshold;

float _EnableDistanceFalloff;
float2 _DistanceFalloffStartEnd;

float _DebugEdgeType;
half3 _DebugBackgroundColor;

half4 _OutlineColor;
#endif
