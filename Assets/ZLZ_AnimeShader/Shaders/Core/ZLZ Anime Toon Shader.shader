Shader "ZLZ/AnimeToon/Character"
{
    Properties
    {
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Main Settings
        [HideInInspector] _RenderQueue("Render Queue", Float) = 2000
        [Space(20)]
        [Enum(Opaque,2000,AlphaTest,2450,Transparent,3000)] _RenderQueueSelector("Render Queue Mode", Float) = 2000
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("DstBlend", Float) = 0
        [Enum(Both,0,Back,1,Front,2)] _CullMode ("Cull Mode", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(LEqual,4,Always,8)] _ZTest ("ZTest", Float) = 4
        [KeywordEnum(Off, On)] _AlphaClipping ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [KeywordEnum(Off, On)] _CastShadow ("Cast Shadow", Float) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Stencil Settings (Per Pass)
        [Space(20)]
        [IntRange] _StencilRef ("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8     // Always
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0           // Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Float) = 0           // Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Float) = 0  
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Texture Character
        [Space(20)]
        _MainTex ("Main Texture (RGBA)", 2D) = "white" {}
        _RGBA_Masking  ("Feature Mask 1 (RGBA)", 2D) = "black" {}
        _RGBA_Masking2 ("Feature Mask 2 (RGBA)", 2D) = "black" {}
        // Mask Texture Usage (auto-managed by GUI — strips texture sample when no feature uses it)
        [HideInInspector][KeywordEnum(Off, On)] _USE_MASKTEX1 ("Use Mask Texture 1", Float) = 1
        [HideInInspector][KeywordEnum(Off, On)] _USE_MASKTEX2 ("Use Mask Texture 2", Float) = 0
        // Mask Channel Mapping (idx 0..7 -> M1.r/g/b/a, M2.r/g/b/a) — defaults preserve legacy layout
        [HideInInspector] _MetallicMaskCh      ("Metallic Mask Channel",      Float) = 0
        [HideInInspector] _HairHighlightMaskCh ("Hair Highlight Mask Channel",Float) = 1
        [HideInInspector] _EmissiveMaskCh      ("Emissive Mask Channel",      Float) = 2
        [HideInInspector] _OutlineMaskCh       ("Outline Mask Channel",       Float) = 3
        [HideInInspector] _SpecularMaskCh      ("Specular Mask Channel",      Float) = 8
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Base Character Colors
        [Space(20)]
        _Texture_Brightness ("Texture Brightness", Range(0, 2)) = 1.2
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _Shadow_Color ("Shadow Color", Color) = (1,1,1,1)
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Face Shadow
        [KeywordEnum(Off, On)] _Switch_to_Face_Mode ("Switch to Face Mode", Float) = 0
        [Space(20)]
        _FaceTex ("FaceTex (RGB)", 2D) = "black" {}
        [Toggle] _DebugUvFace("DebugUvFace", Float) = 0
        _FaceShadowUVScale ("FaceShadowUVScale", Range(0, 3)) = 1
        _FaceShadowUVOffsetX ("FaceShadowUVOffsetX", Range(-1, 1)) = 0
        _FaceShadowUVOffsetY ("FaceShadowUVOffsetY", Range(-1, 1)) = 0
        [Toggle] _FlipUvFace("FlipUvFace", Float) = 0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Base Character Lighting
        [Space(20)]
        [KeywordEnum(Off, On)] _ReceiveShadow ("Receive Shadow", Float) = 0
        _ShadowSoftness ("Shadow Softness", Range(0, 1)) = 0.5
        _ShadowEdgeSmooth ("Shadow Edge Smooth", Range(0, 1)) = 0.5
        // Reject self-cast shadow (drop the character's own occluder from the URP atlas sample) while keeping environment shadows.
        [Toggle(_ZLZ_REJECT_SELFSHADOW_ON)] _RejectSelfShadow ("Reject Self-Shadow", Float) = 0
        _SelfShadowRejectDist ("Self-Shadow Reject Distance", Range(0.001, 0.2)) = 0.005
        _AdditionalLightIntensity ("AdditionalLightIntensity", Range(0, 2)) = 1
        // Character contact self-shadow (screen-space). Default Off.
        [Toggle(_ZLZ_CONTACTSHADOW_ON)] _UseContactShadow ("Contact Self-Shadow", Float) = 0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Normal Map
        [KeywordEnum(Off, On)] _UseNormal ("Use Normal Map", Float) = 0
        [Space(20)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 5)) = 1.0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // ToonRampShade
        [Space(20)]
        _ToonRampSmoothness ("ToonRampSmoothness", Range(0, 1)) = 0.25
        // Shadow Edge — darker band at the light/shadow boundary (a.k.a. core shadow / terminator) for added form/volume
        [Toggle(_SHADOWEDGE_ON)] _ShadowEdge ("Shadow Edge", Float) = 0
        _ShadowEdgeColor ("Shadow Edge Color", Color) = (1.0, 0.7098, 0.4941, 1)
        _ShadowEdgeIntensity ("Shadow Edge Intensity", Range(0, 1)) = 0.45
        _ShadowEdgeWidth ("Shadow Edge Width", Range(0, 1)) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Outline
        [Space(20)]
        [KeywordEnum(Legacy, PlanarSafe)] _OUTLINE_ZMODE ("Outline ZMode", Float) = 0
        [HideInInspector] _OutlineSmoothNormal ("Smooth Normal Outline", Float) = 0
        _OutlineWidth ("Outline Width", Range(0, 50)) = 1
        _OutlineIntensity ("OutlineIntensity", Range(0, 1)) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineZOffset ("Outline Z Offset", Range(0, .25)) = 0.001
        [KeywordEnum(Off, On)] _OutlineMask ("Outline Mask", Float) = 0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Transparency
        [Space(20)]
        _AlphaValue ("AlphaValue", Range(0, 1)) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Soft BlendBaseColor
        [KeywordEnum(Off, On)] _SoftLight ("SoftLight", Float) = 1
        [Space(20)]
        _SoftLightHighlight ("Soft Light Highlight", Range(-3, 3)) = 0.5
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Rim Light
        [KeywordEnum(Off, On)] _RimLight ("RimLight", Float) = 0
        [Space(20)]
        [KeywordEnum(Light, Custom)] _RimColorMode ("Rim Color Mode", Float) = 0
        _RimColor ("RimColor", Color) = (1,1,1,1)
        _IntensityRimLight ("IntensityRimLight", Range(0, 5)) = 3
        _StepRim ("Rim Steps (Toon)", Range(1, 8)) = 1.5
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Metallic
        [KeywordEnum(Off, On)] _UseMetallic ("Use Metallic", Float) = 1
        [Space(20)]
        _GradientMetallic ("GradientMetallic", 2D) = "white" {}
        _MetalNormalMap ("MetalNormalMap", 2D) = "bump" {}
        _MetalIntensity ("IntensityMetal", Range(0, 10)) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Hair Highlight
        [KeywordEnum(Off, On)] _Hair_Highlight ("Hair Highlight", Float) = 0
        [Space(20)]
        _ColorHair ("ColorHair", Color) = (1,1,1,1)
        _Hair_HighlightValue ("Hair Highlight Value", Range(0, 10)) = 0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Specular (Stylized Blinn-Phong) — uses Feature Mask channel selected by GUI
        [KeywordEnum(Off, On)] _Specular ("Specular", Float) = 0
        [Space(20)]
        [HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularIntensity ("Specular Intensity", Range(0, 10)) = 1
        _SpecularSharpness ("Specular Sharpness", Range(1, 200)) = 80
        _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 0.5
        _SpecularToonStep ("Specular Toon Step (0=Smooth, 1=Hard)", Range(0, 1)) = 0.7
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Emissive Glow
        [KeywordEnum(Off, On)] _EmissiveGlow ("Emissive Glow", Float) = 0
        [Space(20)]
        _Emissive_Color ("Emissive Color", Color) = (1,1,1,1)
        _Emissive_Intensity ("Emissive Intensity", Range(0, 10)) = 0
        [Header(_________________________________________________________________________________________________)]
        [Space(20)]
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Dissolve Character
        [KeywordEnum(Off, On)] _DISSOLVE ("Dissolve", Float) = 0
        [Space(20)]
        _Texture2DDissolve ("Texture2DDissolve", 2D) = "white" {}
        [HDR]_DissolveColor ("DissolveColor", Color) = (1,1,1,0)
        _DissolveValue ("DissolveValue", Range(0, 1)) = 0
        _StartDissolve ("StartDissolve = 0 DissolveValue", Range(-0.5, 0.5)) = -0
        _EndDissolve ("EndDissolve = 1 DissolveValue", Range(0, 1)) = 0.5
        _SizeGlowDissolve ("SizeGlowDissolve", Range(0, 0.2)) = 0.05
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Dither — all properties hidden from Material Inspector.
        // Controlled via ZLZ_CharacterVFX.Dither (Component-level). OnValidate + Awake
        // propagate keyword + values to every shared material on the character.
        [HideInInspector] _DitherAlpha ("Dither Alpha", Range(0, 1)) = 0
        [HideInInspector] _DitherCameraEnable ("Enable Camera Near Fade", Float) = 0
        [HideInInspector] _DitherOcclusionAlpha ("Dither Occlusion Alpha", Range(0, 1)) = 0
        [HideInInspector] _DitherCameraNear     ("Camera Near Distance",   Range(0, 20)) = 1
        [HideInInspector] _DitherCameraFar      ("Camera Far Distance",    Range(0, 20)) = 1.25
        // Per-character anchor written every frame by ZLZ_CharacterVFX (Play only) so the
        // whole character fades from one distance. Must be a declared property (not just a
        // CBUFFER field) so Material.SetVector on the per-character instance binds it and
        // HasProperty returns true. Default (0,0,0,0) is the "unset" sentinel — the Dither
        // shader then falls back to the object-matrix origin (see ZLZ_ComputeCameraDither).
        [HideInInspector] _CharacterAnchorWS    ("Character Anchor (WS)",  Vector) = (0,0,0,0)
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Character Darkening
        [KeywordEnum(Off, On)] _TargetDarken ("TargetDarken", Float) = 0
        [Space(20)]
        _TargetDarkenIntensity ("TargetDarkenIntensity", Range(0, 1)) = 0.1
        _TargetDarkenLocal ("TargetDarkenLocal", Range(0, 1)) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Indicator
        [KeywordEnum(Off, On)] _Indicator ("Indicator", Float) = 0
        [Space(20)]
        _IndicatorStrength ("Indicator Strength", Range(0,1)) = 0
        [HDR]_IndicatorColor ("IndicatorColor ", Color) = (1,1,1,0)
        _FresnelPowerIndicator ("FresnelPowerIndicator", Range(0,5)) = 1
        [Header(_________________________________________________________________________________________________)]
        [Space(20)]
        //-----------------------------------------------------------------------------------------------------------------------------------
        // GetHit
        [KeywordEnum(Off, On)] _GetHit ("GetHit", Float) = 0
        [Space(20)]
        _GetHitStrength ("GetHit Strength", Range(0,1)) = 0
        [HDR]_GetHitColor ("GetHitColor ", Color) = (1,1,1,0)
        _FresnelPowerHit ("FresnelPowerHit", Range(0,5)) = 1
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Light Sweep
        [KeywordEnum(Off, On)] _LIGHTSWEEP ("Use Light Sweep", Float) = 0
        [Space(20)]
        _LightSweepIntensity ("LightSweepIntensity", Range(0,50)) = 1
        _LightSweepDuration ("LightSweepDuration (Seconds)", Range(0.1,10)) = 2.5
        _LightSweepDelay ("LightSweepDelay (Seconds)", Range(0,5)) = 2
        _LightSweepWidth ("LightSweepWidth", Range(0,1)) = 0.3
        _LightSweepSoftness ("LightSweepSoftness", Range(0,5)) = 3
        _LightSweepStart ("LightSweepStart", Range(-5,5)) = -1
        _LightSweepEnd ("LightSweepEnd", Range(-5,5)) = 2
        _LightSweepDirX ("LightSweepDirX", Range(-1,1)) = 0.5
        _LightSweepDirY ("LightSweepDirY", Range(-1,1)) = 0.5
        _LightSweepDirZ ("LightSweepDirZ", Range(-1,1)) = 0
        //-----------------------------------------------------------------------------------------------------------------------------------
        // Upgrade Weapon
        [KeywordEnum(Off, On)] _UPGRADE ("Use Upgrade Weapon", Float) = 0
        [Space(20)]
        _UpgradeActive ("UpgradeActive", Range(0,1)) = 0
        _UpgradeColor ("UpgradeColor", Color) = (1,0.75,0.5,0)
        _UpgradeIntensity ("UpgradeIntensity", Range(0,5)) = 5
        _UpgradeMinBrightness ("UpgradeMinBrightness", Range(0,1)) = 0.5
        //-----------------------------------------------------------------------------------------------------------------------------------
        //  Head Back-Facing Visibility Control (Camera vs Head Direction)
        [KeywordEnum(Off, On)] _HEAD_BACK_HIDE ("HEAD_BACK_HIDE", Float) = 0
        [Space(20)]
        [KeywordEnum(Off, On)] _EYE_BACKFACE_CLIP ("EYE_BACKFACE_CLIP", Float) = 0
        [KeywordEnum(Off, On)] _HAIR_BACKFACE_FADE ("HAIR_BACKFACE_FADE", Float) = 0
        _HeadBackCutoff ("HeadBackCutoff", Range(-1,1)) = 0
        _HeadTopCutoff ("HeadTopCutOff(Top View Only)", Range(-1, 1)) = -0.1
        _HairFadeRange ("HairFadeRange", Range(0,1)) = 0
        [HideInInspector]_HeadCenterWS ("_HeadCenterWS", Vector) = (0,0,0,0)
        [HideInInspector]_HeadForwardWS("_HeadForwardWS", Vector) = (0,0,1,0)
        [HideInInspector]_HeadRightWS("_HeadRightWS", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" "UniversalMaterialType" = "Unlit" "RenderPipeline" = "UniversalPipeline" }

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass [_StencilPass]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }

        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull [_CullMode]                // Material Inspector : Front/Back/Off
            ZWrite [_ZWrite]                // Material Inspector : On/Off
            ZTest [_ZTest]                  // Material Inspector : LEqual/Always
            Blend [_SrcBlend] [_DstBlend]   // Material Inspector : One Zero / SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            // Without this keyword AdditionalLightRealtimeShadow() compiles to "return 1.0",
            // so Point/Spot lights shine straight through shadow-casting walls.
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma shader_feature _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _CASTSHADOW_OFF _CASTSHADOW_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Switch Control Function - On / Off
            #pragma shader_feature_local _ _ZLZ_CONTACTSHADOW_ON
            #pragma shader_feature_local _ _ZLZ_REJECT_SELFSHADOW_ON
            #pragma shader_feature_local _ _SHADOWEDGE_ON
            #pragma shader_feature _SWITCH_TO_FACE_MODE_OFF _SWITCH_TO_FACE_MODE_ON
            #pragma shader_feature _RECEIVESHADOW_OFF _RECEIVESHADOW_ON
            #pragma shader_feature _HAIR_HIGHLIGHT_OFF _HAIR_HIGHLIGHT_ON
            #pragma shader_feature _SPECULAR_OFF _SPECULAR_ON
            #pragma shader_feature _EMISSIVEGLOW_OFF _EMISSIVEGLOW_ON
            #pragma shader_feature _USEMETALLIC_OFF _USEMETALLIC_ON
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            #pragma shader_feature _LIGHTSWEEP_OFF _LIGHTSWEEP_ON
            #pragma shader_feature _UPGRADE_OFF _UPGRADE_ON
            #pragma shader_feature _SOFTLIGHT_OFF _SOFTLIGHT_ON
            #pragma shader_feature _TARGETDARKEN_OFF _TARGETDARKEN_ON
            #pragma shader_feature _INDICATOR_OFF _INDICATOR_ON
            #pragma shader_feature _GETHIT_OFF _GETHIT_ON
            #pragma shader_feature _RIMLIGHT_OFF _RIMLIGHT_ON
            #pragma shader_feature _RIMCOLORMODE_LIGHT _RIMCOLORMODE_CUSTOM
            #pragma shader_feature _HEAD_BACK_HIDE_OFF _HEAD_BACK_HIDE_ON
            #pragma shader_feature _EYE_BACKFACE_CLIP_OFF _EYE_BACKFACE_CLIP_ON
            #pragma shader_feature _HAIR_BACKFACE_FADE_OFF _HAIR_BACKFACE_FADE_ON
            #pragma shader_feature _USENORMAL_OFF _USENORMAL_ON
            // Mask Texture sampling — auto-managed by GUI based on which features need each mask
            #pragma shader_feature_local _USE_MASKTEX1_OFF _USE_MASKTEX1_ON
            #pragma shader_feature_local _USE_MASKTEX2_OFF _USE_MASKTEX2_ON
            // URP DBuffer Decals — local feature so variants only compile when a material
            // (or URP DecalRendererFeature global keyword) actually enables one of these.
            // Saves ~75% shader variants for buyers who don't use decals.
            #pragma shader_feature_local_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #define UNITY_DECLARE_TIME

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Texture Declarations
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_RGBA_Masking);
            SAMPLER(sampler_RGBA_Masking);
            TEXTURE2D(_RGBA_Masking2);
            SAMPLER(sampler_RGBA_Masking2);
            TEXTURE2D(_FaceTex);
            SAMPLER(sampler_FaceTex);
            TEXTURE2D(_GradientMetallic);
            SAMPLER(sampler_GradientMetallic);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);
            TEXTURE2D(_MetalNormalMap);
            SAMPLER(sampler_MetalNormalMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Function/ZLZ_Function.hlsl"
            #include "../Lighting/ZLZ_MainLighting.hlsl"
            #include "../Lighting/ZLZ_CharacterContactShadow.hlsl"
            #include "../Features/ZLZ_FaceShadow.hlsl"
            #include "../Features/ZLZ_CharacterSurface.hlsl"
            #include "../Features/ZLZ_RimLight.hlsl"
            #include "../Features/ZLZ_SoftLight.hlsl"
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"
            #include "../Features/ZLZ_CharacterFX.hlsl"
            #include "../Features/ZLZ_HeadBackfaceControl.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Global Properties
            float _TargetDarkenGlobal;

            //-----------------------------------------------------------------------------------------------------------------------------------
            // GPU Instnacing
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                // UNITY_DEFINE_INSTANCED_PROP(float, _SwitchDissolve)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 tangentWS : TEXCOORD4;        // Transform > Tangent to World
                half3 bitangentWS : TEXCOORD5;      // Transform > Tangent to World
                float4 screenPos : TEXCOORD6;       // Screen Position
                float3 positionOS : TEXCOORD7;      // Position Object
                half2 DebugFaceUV : TEXCOORD8;      // DebugFaceUV
                half2 uvNormal : TEXCOORD9;         // UV Normal
                half2 uvDissolve : TEXCOORD10;      // UV
                half2 FaceUV : TEXCOORD11;          // FaceUV
                #if defined(_SWITCH_TO_FACE_MODE_ON)
                    half2 faceScalars : TEXCOORD12; // x=faceAngle01, y=faceSideMask (precomputed in vert)
                #endif
                half3 indirectLighting : TEXCOORD14; // SampleSH precomputed in vert
                #if defined(_HEAD_BACK_HIDE_ON)
                    float headBackShifted : TEXCOORD15; // (headFacing - finalCutoff) precomputed in vert
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID      //GPU Instance
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);         // GPU Instancing
                UNITY_TRANSFER_INSTANCE_ID(v, o);   // GPU Instancing

                // UVs
                o.uv            = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvNormal      = TRANSFORM_TEX(v.uv, _MetalNormalMap);
                o.uvDissolve    = TRANSFORM_TEX(v.uv, _Texture2DDissolve);

                #if defined(_SWITCH_TO_FACE_MODE_ON)
                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    // Setup UV Face Shadow
                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    half2 OffsetFace = v.uv + half2(_FaceShadowUVOffsetX, _FaceShadowUVOffsetY);
                    half2 RemapFace = Unity_Remap_float2(OffsetFace, half2(0.0, 1.0), half2(-0.5, 0.5));
                    half2 ScaleFace = RemapFace * half2(_FaceShadowUVScale, _FaceShadowUVScale);
                    half2 FinalUVFace = ScaleFace + half2(0.5, 0.5);

                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    // Flip UV Face Shadow
                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    half2 FlipUVy = half2(FinalUVFace.x, 1 - FinalUVFace.y);
                    half2 FaceUV = lerp(FinalUVFace.xy, FlipUVy.xy, _FlipUvFace);

                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    // Debug UV Face Shadow
                    //--------------------------------------------------------------------------------------------------------------------------------------------
                    half2 StepUV = step(half2(0.5, 0.5), FaceUV.xy);
                #else
                    half2 FaceUV = half2(0.0h, 0.0h);
                    half2 StepUV = half2(0.0h, 0.0h);
                #endif

                // UVs2
                o.FaceUV        = FaceUV;
                o.DebugFaceUV   = StepUV;

                #if defined(_SWITCH_TO_FACE_MODE_ON)
                    half fa, fs;
                    ZLZ_ComputeFaceScalars(_MainLightPosition.xyz, fa, fs);
                    o.faceScalars = half2(fa, fs);
                #endif

                float3 posOS = v.positionOS.xyz;

                //--------------------------------------------------------------------------------------------------------------------------------------------
                // Object / World / Clip
                //--------------------------------------------------------------------------------------------------------------------------------------------
                o.positionOS  = posOS;
                o.positionWS  = TransformObjectToWorld(posOS);
                o.positionHCS = TransformWorldToHClip(o.positionWS);

                #if defined(_HEAD_BACK_HIDE_ON)
                    o.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
                #endif

                //--------------------------------------------------------------------------------------------------------------------------------------------
                // Screen / Fog
                //--------------------------------------------------------------------------------------------------------------------------------------------
                o.screenPos = ComputeScreenPos(o.positionHCS);
                o.fogFactor = ComputeFogFactor(o.positionHCS.z);

                //--------------------------------------------------------------------------------------------------------------------------------------------
                // Normal / Tangent / Bitangent (World Space)
                //--------------------------------------------------------------------------------------------------------------------------------------------
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.indirectLighting = SampleSH(o.normalWS);

                float3 tangentWS   = normalize(TransformObjectToWorldDir(v.tangentOS.xyz));
                float3 bitangentWS = normalize(cross(o.normalWS, tangentWS) * v.tangentOS.w);

                o.tangentWS   = tangentWS;
                o.bitangentWS = bitangentWS;

                return o;
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // Fragment Forward Lit
            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            half4 frag(Varyings i) : SV_Target
            {
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // GPU Instancing
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                UNITY_SETUP_INSTANCE_ID(i);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Texture
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half4 TMain         = SAMPLE_TEXTURE2D(_MainTex,            sampler_MainTex,        i.uv);

                #if defined(_USE_MASKTEX1_ON)
                    half4 TRGBAMask  = SAMPLE_TEXTURE2D(_RGBA_Masking,  sampler_RGBA_Masking,  i.uv);
                #else
                    half4 TRGBAMask  = half4(0.0h, 0.0h, 0.0h, 0.0h);
                #endif

                #if defined(_USE_MASKTEX2_ON)
                    half4 TRGBAMask2 = SAMPLE_TEXTURE2D(_RGBA_Masking2, sampler_RGBA_Masking2, i.uv);
                #else
                    half4 TRGBAMask2 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // DBuffer Decal
                // Apply to raw texture before brightness so decal color scales consistently with _Texture_Brightness
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DBUFFER_MRT1) || defined(_DBUFFER_MRT2) || defined(_DBUFFER_MRT3)
                    ApplyDecalToBaseColor(i.positionHCS, TMain.rgb);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Normal (World Space) & View Direction (World Space)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 N_main = normalize(i.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Normal Map
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_USENORMAL_ON)
                    N_main = ZLZ_SampleNormal(i.uv, i.tangentWS, i.bitangentWS, N_main, _NormalStrength);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // DBuffer Decal Normal
                // DBuffer1.rg  = world-space normal (oct encoded)
                // Blend factor = DBuffer0.a  (DBuffer1 may be RG-only format — no reliable alpha)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DBUFFER_MRT2) || defined(_DBUFFER_MRT3)
                    uint2 positionSS = (uint2)i.positionHCS.xy;
                    half4 dBuffer0   = LOAD_TEXTURE2D_X(_DBufferTexture0, positionSS);
                    half4 dBuffer1   = LOAD_TEXTURE2D_X(_DBufferTexture1, positionSS);
                    half3 decalN     = UnpackNormalOctRectEncode(dBuffer1.rg * 2.0h - 1.0h);
                    N_main           = normalize(lerp(decalN, N_main, dBuffer0.a));
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Cache Lighting
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 lightDir, lightColor, AdditionalLightColor;
                half distAtten, shadowAtten;

                //  Custom Function : Main Light
                ZLZ_MainLight(i.positionWS, lightDir, lightColor, distAtten, shadowAtten, AdditionalLightColor);
                AdditionalLightColor = saturate((AdditionalLightColor * _AdditionalLightIntensity) / 4);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Indirect Lighting (Environment + Ambient)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 indirectLighting = i.indirectLighting;
                
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Direct Light Term
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half NdotL = saturate(dot(N_main, lightDir));

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Receive Shadow
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half RecShadow;

                #if defined(_RECEIVESHADOW_ON)
                    half effectiveShadow = shadowAtten;
                    #if defined(_ZLZ_REJECT_SELFSHADOW_ON)
                        // Drop the contribution from occluders that sit on the character
                        // itself, keeping shadows cast by the surrounding scene. Only valid
                        // on the cascade path; the screen-space shadow texture and the
                        // no-shadow path skip the check.
                        #if !defined(_MAIN_LIGHT_SHADOWS_SCREEN) && (defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE))
                            float4 selfShadowCoord = TransformWorldToShadowCoord(i.positionWS);
                            effectiveShadow = ZLZ_RejectSelfShadow(shadowAtten, selfShadowCoord, (half)_SelfShadowRejectDist);
                        #endif
                    #endif
                    half smoothShadowAtten = ZLZ_SmoothShadow(effectiveShadow);
                    half shadowFactor      = lerp(1.0h, smoothShadowAtten, _ReceiveShadow);
                    RecShadow              = distAtten * shadowFactor;
                #else
                    RecShadow = distAtten;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Final Output Main Light
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half FinalMainLight = NdotL * RecShadow;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Face Shadow (Directional Main Light)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_SWITCH_TO_FACE_MODE_ON)
                    half FaceShadowTerm = ZLZ_ComputeFaceShadowTerm(i.faceScalars.x, i.faceScalars.y, i.FaceUV);
                    // Combine the face shadow SDF (controls anime nose / bang shape) with the
                    // URP receive chain (RecShadow) so the face also darkens when external
                    // shadows cover it. Multiplicative so either source can put the face into
                    // shadow; when Receive Shadow is off the material the multiplier is 1.
                    half SwitchFS = FaceShadowTerm * RecShadow;
                #else
                    half SwitchFS = FinalMainLight;
                #endif

                // Character contact self-shadow (hair→face / arm→body). Multiplied into the
                // lighting term so occluded areas fall to the shadow side of the toon ramp.
                #if defined(_ZLZ_CONTACTSHADOW_ON)
                    SwitchFS *= ZLZ_SampleContactShadow(i.positionHCS, lightDir);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Toon Light Ramp (Shadow > Light)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                ZLZ_ToonLightResult toon = ZLZ_ComputeToonLighting(SwitchFS, lightColor, AdditionalLightColor, indirectLighting, _Shadow_Color.rgb, _BaseColor.rgb, _ToonRampSmoothness);

                half  lightRamp01        = toon.lightRamp01;
                half3 finalToonLighting  = toon.toonLighting;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Shadow Edge — deepen the light/shadow terminator before texture/highlight are applied
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_SHADOWEDGE_ON)
                    finalToonLighting = ZLZ_ApplyShadowEdge(finalToonLighting, lightRamp01, _ShadowEdgeColor.rgb, _ShadowEdgeIntensity, _ShadowEdgeWidth);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Hair Highlight
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 hairHighlight = half3(0, 0, 0);

                #if defined(_HAIR_HIGHLIGHT_ON)
                    half hairMask = ZLZ_PickMaskChannel(TRGBAMask, TRGBAMask2, _HairHighlightMaskCh);
                    hairHighlight = ZLZ_ComputeHairHighlight(lightRamp01, hairMask, _ColorHair.rgb, _Hair_HighlightValue, lightColor);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Diffuse
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half4 diffuseBright = ZLZ_ComputeDiffuseBright(TMain, _Texture_Brightness);

                //-----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Combine (Diffuse + Toon Light Ramp + Hair Highlight)
                half3 baseLitColor = (diffuseBright.rgb * finalToonLighting) + hairHighlight;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Emissive Glow
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 finalEmissive = baseLitColor;

                #if defined(_EMISSIVEGLOW_ON)
                    half emissiveMask = ZLZ_PickMaskChannel(TRGBAMask, TRGBAMask2, _EmissiveMaskCh);
                    finalEmissive = ZLZ_ComputeEmissiveGlow(diffuseBright, emissiveMask, _Emissive_Color.rgb, _Emissive_Intensity, baseLitColor);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Metallic Toon Ramp (Normal Map + Gradient Ramp)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half metalHighlight = 0.0h;

                #if defined(_USEMETALLIC_ON)
                    metalHighlight = ZLZ_ComputeMetalHighlight(N_main, lightDir, i.uvNormal, i.tangentWS, i.bitangentWS);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Combine : Metallic
                #if defined(_USEMETALLIC_ON)
                    half metalMask = ZLZ_PickMaskChannel(TRGBAMask, TRGBAMask2, _MetallicMaskCh);
                #else
                    half metalMask = 0.0h;
                #endif
                half metalEmissiveMask = metalHighlight * metalMask;
                half3 metalEmissive = metalEmissiveMask * finalEmissive;
                half3 finalEmissiveColor = metalEmissive + finalEmissive;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Specular (Stylized Blinn-Phong) — additive on top of base lit color
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_SPECULAR_ON)
                    half specularMask = ZLZ_PickMaskChannel(TRGBAMask, TRGBAMask2, _SpecularMaskCh);
                    finalEmissiveColor += ZLZ_ComputeSpecular(
                        N_main, lightDir, viewDirWS, lightColor,
                        specularMask, lightRamp01);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Rim Light Toon
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 finalrimlight = finalEmissiveColor;

                #if defined(_RIMLIGHT_ON)
                    finalrimlight = ZLZ_ComputeRimLight(N_main, viewDirWS, lightDir, lightColor, finalEmissiveColor);
                #endif

                // Screen position
                float2 ScreenPos = i.screenPos.xy / i.screenPos.w;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Soft Light Highlight (Post-Lighting Tone)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 finalSoftLight = finalrimlight;

                #if defined(_SOFTLIGHT_ON)
                    half softLightLevel = 0.2h;
                    finalSoftLight = ZLZ_ComputeSoftLight(finalrimlight, softLightLevel, _SoftLightHighlight);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Combine : Diffuse & Light Smooth & Hair Highlight & Emissive & Normal Map & Rim Light & Edge Highlight
                half3 CBedge = finalSoftLight;
                
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dissolve
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 BaseDiss  = CBedge;
                half  AlphaDiss = 1.0h;

                #if defined(_DISSOLVE_ON)
                    DissolveResult dissolve = ComputeDissolve(CBedge, _DissolveColor.rgb, _DissolveValue, _StartDissolve, _EndDissolve, _SizeGlowDissolve, i.uvDissolve, TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve));
                    BaseDiss  = dissolve.color;
                    AlphaDiss = dissolve.alpha;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Character Darkening (Global × Local Control)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 finalCharDarken = ZLZ_ApplyCharacterDarken(BaseDiss, _TargetDarkenIntensity, _TargetDarkenLocal, _TargetDarkenGlobal);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Shared Fresnel for Indicator & GetHit
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half2 fresAiming = half2(0.0h, 0.0h);

                #if defined(_INDICATOR_ON) || defined(_GETHIT_ON)
                    fresAiming = ZLZ_ComputeSharedFresnel(N_main, viewDirWS);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Indicator
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 FinalIndicator = ZLZ_ApplyIndicator(finalCharDarken, fresAiming.x);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // GetHit
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 FinaGetHit = ZLZ_ApplyGetHit(FinalIndicator, fresAiming.y);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Light Sweep
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 FinalLightSweep;

                #if defined(_LIGHTSWEEP_ON)
                    FinalLightSweep = ZLZ_LightSweep(i.positionOS.xyz, FinaGetHit);
                #else
                    FinalLightSweep = FinaGetHit;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Upgrade Weapon
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 FinalUpgrade = ZLZ_UpgradeWeapon(FinalLightSweep, _UpgradeMinBrightness, _UpgradeColor.rgb, _UpgradeIntensity, _UpgradeActive);
                
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Debug UV Face
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_SWITCH_TO_FACE_MODE_ON)
                    float3 SwitchDebugFace = lerp(FinalUpgrade.rgb, float3(i.DebugFaceUV, 0.0f), _DebugUvFace);
                    float3 DebugUvFace = SwitchDebugFace;
                #else
                    float3 DebugUvFace = FinalUpgrade;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Final Base Color
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                float3 FinalBase = DebugUvFace;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Final Alpha Channel
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half FinalAlpha = saturate(TMain.a * _AlphaValue * AlphaDiss);
                half FinalClip = saturate(TMain.a * AlphaDiss);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Transparent Hair
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_HEAD_BACK_HIDE_ON)
                    ZLZ_ApplyHeadBackfaceControl(i.headBackShifted, FinalAlpha);
                #else
                    ZLZ_ApplyHeadBackfaceControl(FinalAlpha);   // no-op when keyword off
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Output Forward Lit
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                float4 baseColor = float4(FinalBase.rgb, FinalAlpha);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Alpha Clipping
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_ALPHACLIPPING_ON)
                    clip(FinalClip - _Cutoff);
                #endif

                baseColor.rgb = MixFog(baseColor.rgb, i.fogFactor);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dither (Camera Near Fade + manual _DitherAlpha)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    half ditherCam   = (_DitherCameraEnable > 0.5h)
                                       ? ZLZ_ComputeCameraDither(_DitherCameraNear, _DitherCameraFar)
                                       : 0.0h;
                    half ditherFinal = max(max((half)_DitherAlpha, (half)_DitherOcclusionAlpha), ditherCam);
                    ZLZ_ApplyDither(i.positionHCS.xy, ditherFinal);
                #endif

                return baseColor;
            }

            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Character Contact Shadow capture pass — used ONLY by ZLZ_CharacterContactShadowFeature to write the
        // character silhouette into _ZLZCharDepthTex for the screen-space contact self-shadow.
        // It is a STRIPPED depth pass: it keeps just alpha-clip (needed for cutout hair/lashes) and
        // drops every other feature (dissolve / head-backface / backface-fade / dither). With so few
        // keywords, all character materials share one shader variant, so the SRP Batcher collapses
        // the whole character into ~1 batch instead of the many variants the full
        // DepthOnly pass produces. Custom LightMode → only rendered when the feature asks for it, so
        // it adds zero cost to normal rendering.
        // ─────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ZLZContactShadow"
            Tags { "LightMode" = "ZLZContactShadow" }

            // Force a UNIFORM device state across every character material so the SRP Batcher can
            // merge the whole character into ONE batch. We override the per-material Cull and the
            // SubShader-level Stencil block (driven by the _Stencil* props): neither matters here
            // because we only write depth into the separate _ZLZCharDepthTex — there is no main-
            // framebuffer face/hair stencil masking to honour in this capture.
            Cull Off                                              // never cull → complete silhouette, and uniform across materials
            ZWrite On
            ColorMask 0
            Stencil { Ref 0 Comp Always Pass Keep Fail Keep ZFail Keep }   // fixed no-op → same state for all materials

            HLSLPROGRAM

            #pragma vertex   ZLZContactShadowVertex
            #pragma fragment ZLZContactShadowFragment
            #pragma shader_feature _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ZLZContactShadowVertex(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv          = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 ZLZContactShadowFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Cutout silhouette only when the material actually alpha-clips (hair strands / lashes).
                #if defined(_ALPHACLIPPING_ON)
                    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                    clip(alpha - _Cutoff);
                #endif

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_CullMode]
            ZWrite On
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma multi_compile_instancing

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Switch Control Function - On / Off
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            #pragma shader_feature _HEAD_BACK_HIDE_OFF _HEAD_BACK_HIDE_ON
            #pragma shader_feature _EYE_BACKFACE_CLIP_OFF _EYE_BACKFACE_CLIP_ON
            #pragma shader_feature _HAIR_BACKFACE_FADE_OFF _HAIR_BACKFACE_FADE_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"
            #include "../Features/ZLZ_HeadBackfaceControl.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                //float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvDissolve : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                //float2 uv2 : TEXCOORD3;
                #if defined(_HEAD_BACK_HIDE_ON)
                    float headBackShifted : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings DepthOnlyVertex(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                //o.uv2 = i.uv2;
                o.uvDissolve = TRANSFORM_TEX(i.uv, _Texture2DDissolve);
                o.positionOS = i.positionOS.xyz;
                #if defined(_HEAD_BACK_HIDE_ON)
                    o.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
                #endif
                return o;
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // Fragment (DepthOnly)
            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            half4 DepthOnlyFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                #if defined(_ALPHACLIPPING_ON)

                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    // Main Texture & Alpha
                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    half4 TMain      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                    half  alphaFinal = TMain.a;

                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    // Dissolve
                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    half3 BaseDiss  = TMain.rgb;
                    half  AlphaDiss = 1.0h;

                    #if defined(_DISSOLVE_ON)
                        DissolveResult d = ComputeDissolve(
                            BaseDiss,
                            _DissolveColor.rgb,
                            _DissolveValue,
                            _StartDissolve,
                            _EndDissolve,
                            _SizeGlowDissolve,
                            i.uvDissolve,
                            TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve)
                        );

                        BaseDiss  = d.color;
                        AlphaDiss = d.alpha;
                    #endif

                    #if defined(_HEAD_BACK_HIDE_ON)
                        ZLZ_ApplyHeadBackfaceControl(i.headBackShifted, alphaFinal);
                    #else
                        ZLZ_ApplyHeadBackfaceControl(alphaFinal);
                    #endif

                    alphaFinal = saturate(alphaFinal * AlphaDiss);

                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    // Alpha Clipping
                    // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                    clip(alphaFinal - _Cutoff);

                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dither (manual only — Camera Near Fade is computed in ForwardLit)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    ZLZ_ApplyDither(i.positionHCS.xy, max((half)_DitherAlpha, (half)_DitherOcclusionAlpha));
                #endif

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull [_CullMode]
            ZWrite On
            // RGBA: the Screen Space Outline reads the per-material Outline Mask from the
            // normal buffer's alpha channel, so alpha must be written here (was RGB).
            ColorMask RGBA

            HLSLPROGRAM

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma multi_compile_instancing

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Switch Control Function - On / Off
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            #pragma shader_feature _HEAD_BACK_HIDE_OFF _HEAD_BACK_HIDE_ON
            #pragma shader_feature _EYE_BACKFACE_CLIP_OFF _EYE_BACKFACE_CLIP_ON
            #pragma shader_feature _HAIR_BACKFACE_FADE_OFF _HAIR_BACKFACE_FADE_ON
            #pragma shader_feature _USENORMAL_OFF _USENORMAL_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Outline Mask — same channel as the Hull outline (_OutlineMaskCh). Packed into the
            // normal buffer's alpha so the Screen Space Outline can hide masked regions too.
            #pragma shader_feature _OUTLINEMASK_OFF _OUTLINEMASK_ON
            #pragma shader_feature_local _USE_MASKTEX1_OFF _USE_MASKTEX1_ON
            #pragma shader_feature_local _USE_MASKTEX2_OFF _USE_MASKTEX2_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RGBA_Masking);
            SAMPLER(sampler_RGBA_Masking);
            TEXTURE2D(_RGBA_Masking2);
            SAMPLER(sampler_RGBA_Masking2);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Function/ZLZ_Function.hlsl"
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"
            #include "../Features/ZLZ_HeadBackfaceControl.hlsl"
            #include "../Features/ZLZ_CharacterSurface.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                //float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvNormal : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float2 uvDissolve : TEXCOORD5;
                float3 positionOS : TEXCOORD6;
                //float2 uv2 : TEXCOORD7;
                #if defined(_HEAD_BACK_HIDE_ON)
                    float headBackShifted : TEXCOORD8;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings DepthNormalsVertex(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                //o.uv2 = i.uv2;
                o.uvNormal = TRANSFORM_TEX(i.uv, _MetalNormalMap);
                o.uvDissolve = TRANSFORM_TEX(i.uv, _Texture2DDissolve);
                o.positionOS = i.positionOS.xyz;

                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
                float3 tangentWS = normalize(TransformObjectToWorldDir(i.tangentOS.xyz));
                float3 bitangentWS = normalize(cross(normalWS, tangentWS) * i.tangentOS.w);

                o.normalWS = normalWS;
                o.tangentWS = tangentWS;
                o.bitangentWS = bitangentWS;

                #if defined(_HEAD_BACK_HIDE_ON)
                    o.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
                #endif

                return o;
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // DepthNormals Fragment (supports Rendering Layers + Dissolve)
            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            void DepthNormalsFragment(
                Varyings i,
                out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
                #endif
            )
            {
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Main Texture & Alpha
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half4 TMain      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half  alphaFinal = TMain.a;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dissolve
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 BaseDiss  = TMain.rgb;
                half  AlphaDiss = 1.0h;

                #if defined(_DISSOLVE_ON)
                    DissolveResult d = ComputeDissolve(
                        BaseDiss,
                        _DissolveColor.rgb,
                        _DissolveValue,
                        _StartDissolve,
                        _EndDissolve,
                        _SizeGlowDissolve,
                        i.uvDissolve,
                        TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve)
                    );

                    BaseDiss  = d.color;
                    AlphaDiss = d.alpha;
                #endif

                #if defined(_HEAD_BACK_HIDE_ON)
                    ZLZ_ApplyHeadBackfaceControl(i.headBackShifted, alphaFinal);
                #else
                    ZLZ_ApplyHeadBackfaceControl(alphaFinal);
                #endif

                alphaFinal = saturate(alphaFinal * AlphaDiss);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Alpha Clipping
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_ALPHACLIPPING_ON)
                    clip(alphaFinal - _Cutoff);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dither (manual only — Camera Near Fade is computed in ForwardLit)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    ZLZ_ApplyDither(i.positionHCS.xy, max((half)_DitherAlpha, (half)_DitherOcclusionAlpha));
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Normal Output
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 normalWS = normalize(i.normalWS);

                #if defined(_USENORMAL_ON)
                    normalWS = ZLZ_SampleNormal(i.uv, i.tangentWS, i.bitangentWS, normalWS, _NormalStrength);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Outline Mask -> alpha. Same channel (_OutlineMaskCh) the Hull outline uses, so a
                // single mask hides BOTH outlines. The Screen Space Outline reads this alpha to skip
                // masked regions. 1 = show outline, 0 = hide. Default 1 (no mask) costs nothing.
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half outlineMask = 1.0h;
                #if defined(_OUTLINEMASK_ON)
                    #if defined(_USE_MASKTEX1_ON)
                        half4 outlineM1 = SAMPLE_TEXTURE2D(_RGBA_Masking,  sampler_RGBA_Masking,  i.uv);
                    #else
                        half4 outlineM1 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                    #endif
                    #if defined(_USE_MASKTEX2_ON)
                        half4 outlineM2 = SAMPLE_TEXTURE2D(_RGBA_Masking2, sampler_RGBA_Masking2, i.uv);
                    #else
                        half4 outlineM2 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                    #endif
                    outlineMask = ZLZ_PickMaskChannel(outlineM1, outlineM2, _OutlineMaskCh);
                #endif

                //outNormalWS = float4(normalWS * 0.5 + 0.5, 1.0);
                outNormalWS = half4(normalWS, outlineMask);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Rendering Layer Output
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #ifdef _WRITE_RENDERING_LAYERS
                    uint renderingLayers = GetMeshRenderingLayer();
                    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
                #endif
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_CullMode]
            ZWrite On

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHACLIPPING_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _CASTSHADOW_OFF _CASTSHADOW_ON
            // Selects directional vs punctual (point/spot) light shadow bias in the vertex stage.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Switch Control Function - On / Off
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Lighting.hlsl pulls in Shadows.hlsl (ApplyShadowBias) plus its CommonMaterial deps
            // (LerpWhiteTo). Matches the working Environment shader ShadowCaster include set.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Shadow light vectors — set by URP per shadow draw (declared here because we don't
            // include URP's ShadowCasterPass.hlsl). _LightDirection for directional cascades,
            // _LightPosition for punctual (point/spot) shadows.
            float3 _LightDirection;
            float3 _LightPosition;

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;       // needed for ApplyShadowBias (normal bias)
                float2 uv : TEXCOORD0;
                //float2 uv2 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float2 uvDissolve : TEXCOORD2;
                //float2 uv2 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                #if defined(_CASTSHADOW_OFF)
                    o.positionHCS = float4(2e10, 2e10, 2e10, 1.0);
                #else
                    // Proper URP shadow caster (matches Lit / Environment shader).
                    // ApplyShadowBias uses URP's per-cascade _ShadowBias (depth + normal bias);
                    // skipping it makes cascaded shadows break at cascade boundaries. The z-clamp
                    // pancakes geometry onto each cascade's near plane so casters in front of it
                    // are not clipped away (which left holes / disconnected shadows).
                    float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                    float3 normalWS   = TransformObjectToWorldNormal(i.normalOS);

                    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                    #else
                        float3 lightDirectionWS = _LightDirection;
                    #endif

                    o.positionHCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                    #if UNITY_REVERSED_Z
                        o.positionHCS.z = min(o.positionHCS.z, o.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                    #else
                        o.positionHCS.z = max(o.positionHCS.z, o.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                    #endif
                #endif

                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                //o.uv2 = i.uv2;
                o.uvDissolve = TRANSFORM_TEX(i.uv, _Texture2DDissolve);
                o.positionOS = i.positionOS.xyz;
                return o;
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // Fragment (DepthOnly)
            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Main Texture & Alpha
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half4 TMain      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half  alphaFinal = TMain.a;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dissolve
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 BaseDiss  = _OutlineColor.rgb;
                half  AlphaDiss = 1.0h;

                #if defined(_DISSOLVE_ON)
                    DissolveResult d = ComputeDissolve(
                        BaseDiss,                       
                        _DissolveColor.rgb,             
                        _DissolveValue,
                        _StartDissolve,
                        _EndDissolve,
                        _SizeGlowDissolve,
                        i.uvDissolve,
                        TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve)
                    );

                    BaseDiss  = d.color;
                    AlphaDiss = d.alpha;
                #endif

                alphaFinal = saturate(alphaFinal * AlphaDiss);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Alpha Clipping
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_ALPHACLIPPING_ON)
                    clip(alphaFinal - _Cutoff);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Shadow Control
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_CASTSHADOW_OFF)
                    return 0;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dither (manual only — shadow pass uses light view; camera fade omitted)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    ZLZ_ApplyDither(i.positionHCS.xy, max((half)_DitherAlpha, (half)_DitherOcclusionAlpha));
                #endif

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            Cull [_CullMode]
            ZWrite On
            ZTest LEqual
            ColorMask RG       // velocity x, y only

            HLSLPROGRAM

            #pragma vertex MotionVectorsVertex
            #pragma fragment MotionVectorsFragment

            #pragma exclude_renderers d3d11_9x
            #pragma target 3.5
            #pragma multi_compile_instancing

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Geometry-visibility features (mirror ForwardLit clip logic). Dither is always
            // compiled (no keyword); the "force TAA history rejection" path in
            // MotionVectorsFragment is gated at runtime on the dither alphas.
            //-----------------------------------------------------------------------------------------------------------------------------------
            #pragma shader_feature _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            #pragma shader_feature _HEAD_BACK_HIDE_OFF _HEAD_BACK_HIDE_ON
            #pragma shader_feature _EYE_BACKFACE_CLIP_OFF _EYE_BACKFACE_CLIP_ON
            #pragma shader_feature _HAIR_BACKFACE_FADE_OFF _HAIR_BACKFACE_FADE_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_HeadBackfaceControl.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"   // ZLZ_ComputeCameraDither for the runtime TAA-reject gate

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            // positionOld is auto-filled by Unity per-vertex when the renderer has
            // SkinnedMotionVectors enabled (provides previous-frame skinned OS position).
            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 positionOld : TEXCOORD4;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionCS                 : SV_POSITION;
                float4 positionCSNoJitter         : TEXCOORD0;
                float4 previousPositionCSNoJitter : TEXCOORD1;
                float2 uv                         : TEXCOORD2;
                float2 uvDissolve                 : TEXCOORD3;
                #if defined(_HEAD_BACK_HIDE_ON)
                    float headBackShifted         : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings MotionVectorsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posOS     = input.positionOS.xyz;
                float3 prevPosOS = input.positionOld;

                // Jittered CS pos — what rasterizer uses this frame (matches color pass)
                output.positionCS = TransformObjectToHClip(posOS);

                // Bias Z to avoid "gaps" in _MotionVectorTexture at depth boundaries
                #if defined(UNITY_REVERSED_Z)
                    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
                #else
                    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
                #endif

                // Non-jittered current + previous CS positions for stable velocity
                output.positionCSNoJitter         = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M,      float4(posOS, 1.0)));
                float3 prevPosForMatrix           = (unity_MotionVectorsParams.x == 1) ? prevPosOS : posOS;
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix,        mul(UNITY_PREV_MATRIX_M, float4(prevPosForMatrix, 1.0)));

                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.uvDissolve = TRANSFORM_TEX(input.uv, _Texture2DDissolve);

                #if defined(_HEAD_BACK_HIDE_ON)
                    output.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
                #endif

                return output;
            }

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Fragment — geometry-visibility clip + NDC velocity with dither override.
            //
            // Algorithm follows URP built-in ObjectMotionVectors.shader, but with one
            // ZLZ-specific override: while a character is actively dithering we write a
            // saturated motion vector instead of the real velocity. This forces TAA to
            // reject the history sample (apparent motion exceeds the resolve radius) and
            // disables temporal blending for dithered geometry.
            //
            // Why: TAA + binary alpha-test (which dither is) produces a ghost trail because
            // the per-pixel coverage flickers frame-to-frame, and TAA's history-rejection
            // heuristic can't distinguish dither from real surface motion. Disabling
            // temporal blending on dithered surfaces costs a small amount of edge AA but
            // is invisible against the dither pattern itself. For anime/toon stylized
            // rendering this is an acceptable trade — ghost is a hard visual regression
            // while AA loss is buried in the dither.
            //--------------------------------------------------------------------------------------------------------------------------------------------
            half4 MotionVectorsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //-----------------------------------------------------------------------------------------------------------------------------------
                // Real-geometry visibility clip — Alpha / Dissolve / Head backface only.
                //-----------------------------------------------------------------------------------------------------------------------------------
                half4 TMain      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half  alphaFinal = TMain.a;
                half  AlphaDiss  = 1.0h;

                #if defined(_DISSOLVE_ON)
                    DissolveResult d = ComputeDissolve(
                        TMain.rgb,
                        _DissolveColor.rgb,
                        _DissolveValue,
                        _StartDissolve,
                        _EndDissolve,
                        _SizeGlowDissolve,
                        input.uvDissolve,
                        TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve));
                    AlphaDiss = d.alpha;
                #endif

                #if defined(_HEAD_BACK_HIDE_ON)
                    ZLZ_ApplyHeadBackfaceControl(input.headBackShifted, alphaFinal);
                #else
                    ZLZ_ApplyHeadBackfaceControl(alphaFinal);
                #endif

                alphaFinal = saturate(alphaFinal * AlphaDiss);

                #if defined(_ALPHACLIPPING_ON)
                    clip(alphaFinal - _Cutoff);
                #endif

                //-----------------------------------------------------------------------------------------------------------------------------------
                // forceNoMotion flag — respect Unity's per-renderer intent FIRST so
                // MotionVectorGenerationMode.ForceNoMotion is honored even when dither is on.
                //-----------------------------------------------------------------------------------------------------------------------------------
                bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
                if (forceNoMotion)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                //-----------------------------------------------------------------------------------------------------------------------------------
                // Dither override — force TAA history rejection ONLY while this character is
                // actually dithering (manual / occlusion alpha, or an active camera-near
                // fade), not merely because the dither code is compiled in. Dither is always
                // compiled now (the _DITHER keyword was removed), so an unconditional reject
                // would wrongly disable TAA for every ZLZ character. Writing a
                // large MV (= 1.0 UV / full screen) is well past any sane history-resolve
                // radius, so TAA falls back to the current sample.
                //-----------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    half ditherCam = (_DitherCameraEnable > 0.5h)
                                     ? ZLZ_ComputeCameraDither(_DitherCameraNear, _DitherCameraFar)
                                     : 0.0h;
                    half ditherActive = max(max((half)_DitherAlpha, (half)_DitherOcclusionAlpha), ditherCam);
                    if (ditherActive > 0.001h)
                        return half4(1.0h, 1.0h, 0.0h, 0.0h);
                #endif

                //-----------------------------------------------------------------------------------------------------------------------------------
                // NDC velocity (matches URP built-in ObjectMotionVectors.shader)
                //-----------------------------------------------------------------------------------------------------------------------------------
                float4 posCS     = input.positionCSNoJitter;
                float4 prevPosCS = input.previousPositionCSNoJitter;

                half2 posNDC     = posCS.xy     * rcp(posCS.w);
                half2 prevPosNDC = prevPosCS.xy * rcp(prevPosCS.w);

                half2 velocity = posNDC - prevPosNDC;
                #if UNITY_UV_STARTS_AT_TOP
                    velocity.y = -velocity.y;
                #endif

                // NDC (-1..1) → UV offset (-0.5..0.5)
                velocity.xy *= 0.5;

                return half4(velocity, 0, 0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ZLZOutline" }
            //Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend [_SrcBlend] [_DstBlend]
            Cull Front
            ZWrite On
            ZTest LEqual
            ColorMask RGB
            // NOTE: this pass intentionally inherits the SubShader Stencil block (driven by the
            // _Stencil* props) — it is LOAD-BEARING. The Hair Transparent setup relies on it
            // (verified: pinning it to a fixed no-op breaks Hair Transparent). Do NOT override it.

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            //#pragma multi_compile _ALPHACLIPPING_OFF _ALPHACLIPPING_ON
            #pragma shader_feature _ALPHACLIPPING_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Switch Control Function - On / Off
            #pragma shader_feature _DISSOLVE_OFF _DISSOLVE_ON
            #pragma shader_feature _HEAD_BACK_HIDE_OFF _HEAD_BACK_HIDE_ON
            #pragma shader_feature _EYE_BACKFACE_CLIP_OFF _EYE_BACKFACE_CLIP_ON
            #pragma shader_feature _HAIR_BACKFACE_FADE_OFF _HAIR_BACKFACE_FADE_ON
            #pragma shader_feature _OUTLINE_ZMODE_LEGACY _OUTLINE_ZMODE_PLANARSAFE
            #pragma shader_feature _OUTLINESMOOTHNORMAL_OFF _OUTLINESMOOTHNORMAL_ON
            #pragma shader_feature _OUTLINEMASK_OFF _OUTLINEMASK_ON
            // Mask Texture sampling — auto-managed by GUI
            #pragma shader_feature_local _USE_MASKTEX1_OFF _USE_MASKTEX1_ON
            #pragma shader_feature_local _USE_MASKTEX2_OFF _USE_MASKTEX2_ON

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include URP Core Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Material CBUFFER (SRP Batcher)
            #include "../Include/ZLZ_CharacterMaterialBuffer.hlsl"
            #include "../Lighting/ZLZ_MainLighting.hlsl"
            #include "../Features/ZLZ_NiloOutlineUtil.hlsl"
            #include "../Features/ZLZ_HeadBackfaceControl.hlsl"

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Texture2DDissolve);
            SAMPLER(sampler_Texture2DDissolve);
            TEXTURE2D(_RGBA_Masking);
            SAMPLER(sampler_RGBA_Masking);
            TEXTURE2D(_RGBA_Masking2);
            SAMPLER(sampler_RGBA_Masking2);

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Include Function
            #include "../Function/ZLZ_Function.hlsl"
            #include "../Features/ZLZ_Dissolve.hlsl"
            #include "../Features/ZLZ_Dither.hlsl"

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Attributes
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS      : TANGENT;
                float2 uv : TEXCOORD0;
                float2 smoothNormalTS : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //-----------------------------------------------------------------------------------------------------------------------------------
            // Struct Varyings
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float2 uvDissolve : TEXCOORD2;
                #if defined(_DITHER_ON)
                float3 positionWS : TEXCOORD3;      // unextruded — matches body's camera-fade distance
                #endif
                #if defined(_HEAD_BACK_HIDE_ON)
                float headBackShifted : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //--------------------------------------------------------------------------------------------------------------------------------------------
            // Vertex
            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(i.positionOS);
                VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(i.normalOS, i.tangentOS);

                float3 positionWS = vertexInput.positionWS;

                #if defined(_DITHER_ON)
                    // Save BEFORE outline extrusion so camera-fade kicks in at the same
                    // distance as the body (ForwardLit pass) — otherwise outline dithers
                    // slightly earlier and looks mismatched.
                    o.positionWS = vertexInput.positionWS;
                #endif

                float3 normalWS   = vertexNormalInput.normalWS;

                #if defined(_OUTLINESMOOTHNORMAL_ON)
                    float3 smoothNormalTS;
                    smoothNormalTS.xy = i.smoothNormalTS;
                    smoothNormalTS.z  = sqrt(1.0 - saturate(dot(smoothNormalTS.xy, smoothNormalTS.xy)));
                    smoothNormalTS    = normalize(smoothNormalTS);

                    float3x3 TBN = float3x3(
                        vertexNormalInput.tangentWS,
                        vertexNormalInput.bitangentWS,
                        vertexNormalInput.normalWS
                    );
                    normalWS = normalize(mul(smoothNormalTS, TBN));
                #endif

                positionWS = TransformPositionWSToOutlinePositionWS(positionWS, vertexInput.positionVS.z, normalWS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                #if defined(_OUTLINE_ZMODE_LEGACY)
                    // Nilo-style Z Offset (clip-space hack)
                    positionCS = NiloGetNewClipPosWithZOffset(positionCS, _OutlineZOffset);

                #elif defined(_OUTLINE_ZMODE_PLANARSAFE)
                    // Planar-safe mode (no clip-z hack)
                #endif


                o.positionHCS = positionCS;
                o.uv = i.uv;
                //o.uv2 = i.uv2;
                o.uvDissolve = TRANSFORM_TEX(i.uv, _Texture2DDissolve);
                o.positionOS = i.positionOS.xyz;

                #if defined(_HEAD_BACK_HIDE_ON)
                    o.headBackShifted = ZLZ_ComputeHeadBackfaceShifted();
                #endif

                return o;
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            // Fragment (Outline)
            // ----------------------------------------------------------------------------------------------------------------------------------------------------------
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Main Texture & Alpha
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half4 TMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half alphaFinal = TMain.a;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // EARLY ALPHA CLIP — kill obviously-transparent pixels before doing any work.
                // Preserves early-Z on tiled mobile GPUs (Adreno / Mali / PowerVR / Apple).
                // Outline Mask check is also early because if mask=0 the pixel won't render.
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_OUTLINEMASK_ON)
                    #if defined(_USE_MASKTEX1_ON)
                        half4 outlineM1 = SAMPLE_TEXTURE2D(_RGBA_Masking,  sampler_RGBA_Masking,  i.uv);
                    #else
                        half4 outlineM1 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                    #endif
                    #if defined(_USE_MASKTEX2_ON)
                        half4 outlineM2 = SAMPLE_TEXTURE2D(_RGBA_Masking2, sampler_RGBA_Masking2, i.uv);
                    #else
                        half4 outlineM2 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                    #endif
                    half outlineMask = ZLZ_PickMaskChannel(outlineM1, outlineM2, _OutlineMaskCh);
                #endif

                #if defined(_ALPHACLIPPING_ON)
                    half earlyClipAlpha = TMain.a;
                    #if defined(_OUTLINEMASK_ON)
                        earlyClipAlpha *= outlineMask;
                    #endif
                    clip(earlyClipAlpha - _Cutoff);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dissolve
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 BaseDiss  = _OutlineColor.rgb;
                half  AlphaDiss = 1.0h;

                #if defined(_DISSOLVE_ON)

                    DissolveResult d = ComputeDissolve(
                        BaseDiss,
                        _DissolveColor.rgb,
                        _DissolveValue,
                        _StartDissolve,
                        _EndDissolve,
                        _SizeGlowDissolve,
                        i.uvDissolve,
                        TEXTURE2D_ARGS(_Texture2DDissolve, sampler_Texture2DDissolve)
                    );

                    BaseDiss  = d.color;
                    AlphaDiss = d.alpha;

                #endif

                alphaFinal = saturate(alphaFinal * AlphaDiss * _AlphaValue);
                float clipFinal = saturate(alphaFinal * AlphaDiss);

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Outline Mask (RGBA_Masking.a)  —  apply mask to alpha (texture already sampled above)
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_OUTLINEMASK_ON)
                    alphaFinal *= outlineMask;
                    clipFinal  *= outlineMask;
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Outline
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                half3 lightColor = (half3)_MainLightColor.rgb;
                float3 outlineColor = ((TMain.rgb * BaseDiss.rgb) * _OutlineIntensity) * lightColor;

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Transparent Hair
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_HEAD_BACK_HIDE_ON)
                    ZLZ_ApplyHeadBackfaceControl(i.headBackShifted, alphaFinal);
                #else
                    ZLZ_ApplyHeadBackfaceControl(alphaFinal);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Alpla Clipping
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_ALPHACLIPPING_ON)
                    clip(clipFinal - _Cutoff);
                #endif

                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                // Dither — must use the SAME composition as ForwardLit (manual + occlusion + camera).
                // Otherwise when Camera Near Fade hides the body, the outline pixels remain opaque
                // and the character appears as a black silhouette ring against the background.
                // ----------------------------------------------------------------------------------------------------------------------------------------------------------
                #if defined(_DITHER_ON)
                    half ditherCam   = (_DitherCameraEnable > 0.5h)
                                       ? ZLZ_ComputeCameraDither(_DitherCameraNear, _DitherCameraFar)
                                       : 0.0h;
                    half ditherFinal = max(max((half)_DitherAlpha, (half)_DitherOcclusionAlpha), ditherCam);
                    ZLZ_ApplyDither(i.positionHCS.xy, ditherFinal);
                #endif

                return float4(outlineColor, alphaFinal);
            }

            ENDHLSL
        }
        
    }
    CustomEditor "ZLZ.AnimeShader.Editor.ZLZAnimeToonGUI"
}