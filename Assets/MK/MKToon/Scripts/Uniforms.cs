//////////////////////////////////////////////////////
// MK Toon Uniforms   	    	   	                //
//					                                //
// Created by Michael Kremmel                       //
// www.michaelkremmel.de                            //
// Copyright © 2020 All rights reserved.            //
//////////////////////////////////////////////////////

namespace MK.Toon
{
    public static class Uniforms
    {
        //CurvedWorldSupport
        //CurvedWorldEditor
        //public static readonly Uniform curvedWorldBlendSettings = new Uniform("_CurvedWorldBendSettings");
        //

        /////////////////
        // Options     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform workflow = new Uniform("_Workflow");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform renderFace    = new Uniform("_RenderFace");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform surface       = new Uniform("_Surface");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform zWrite        = new Uniform("_ZWrite");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform zTest         = new Uniform("_ZTest");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform blendSrc      = new Uniform("_BlendSrc");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform blendDst      = new Uniform("_BlendDst");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform blendSrcAlpha = new Uniform("_BlendSrcAlpha");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform blendDstAlpha = new Uniform("_BlendDstAlpha");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform blend         = new Uniform("_Blend");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform alphaClipping = new Uniform("_AlphaClipping");

        /////////////////
        // Input       //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform albedoColor                 = new Uniform("_AlbedoColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform alphaCutoff                 = new Uniform("_AlphaCutoff");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform albedoMap                   = new Uniform("_AlbedoMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform albedoMapIntensity          = new Uniform("_AlbedoMapIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularColor               = new Uniform("_SpecularColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform metallic                    = new Uniform("_Metallic");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform smoothness                  = new Uniform("_Smoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform roughness                   = new Uniform("_Roughness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularMap                 = new Uniform("_SpecularMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform roughnessMap                = new Uniform("_RoughnessMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform metallicMap                 = new Uniform("_MetallicMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform normalMapIntensity          = new Uniform("_NormalMapIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform normalMap                   = new Uniform("_NormalMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform parallax                    = new Uniform("_Parallax");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform heightMap                   = new Uniform("_HeightMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmission           = new Uniform("_LightTransmission");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionDistortion = new Uniform("_LightTransmissionDistortion");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionColor      = new Uniform("_LightTransmissionColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform thicknessMap                = new Uniform("_ThicknessMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform occlusionMapIntensity       = new Uniform("_OcclusionMapIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform occlusionMap                = new Uniform("_OcclusionMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform emissionColor               = new Uniform("_EmissionColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform emissionMap                 = new Uniform("_EmissionMap");

        /////////////////
        // Detail      //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailBlend              = new Uniform("_DetailBlend");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailColor              = new Uniform("_DetailColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailMix                = new Uniform("_DetailMix");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailMap                = new Uniform("_DetailMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailNormalMapIntensity = new Uniform("_DetailNormalMapIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailNormalMap          = new Uniform("_DetailNormalMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform detailUVSet              = new Uniform("_DetailUVSet");

        /////////////////
        // Stylize     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform receiveShadows                   = new Uniform("_ReceiveShadows");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform wrappedLighting                  = new Uniform("_WrappedLighting");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform diffuseSmoothness                = new Uniform("_DiffuseSmoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform diffuseThresholdOffset           = new Uniform("_DiffuseThresholdOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularSmoothness               = new Uniform("_SpecularSmoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularThresholdOffset          = new Uniform("_SpecularThresholdOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimSmoothness                    = new Uniform("_RimSmoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimThresholdOffset               = new Uniform("_RimThresholdOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionSmoothness      = new Uniform("_LightTransmissionSmoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionThresholdOffset = new Uniform("_LightTransmissionThresholdOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform light                            = new Uniform("_Light");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform diffuseRamp                      = new Uniform("_DiffuseRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularRamp                     = new Uniform("_SpecularRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimRamp                          = new Uniform("_RimRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionRamp            = new Uniform("_LightTransmissionRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightBands                       = new Uniform("_LightBands");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightBandsScale                  = new Uniform("_LightBandsScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightThreshold                   = new Uniform("_LightThreshold");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform additionalLightsThreshold        = new Uniform("_AdditionalLightsThreshold");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform additionalLightsFalloff          = new Uniform("_AdditionalLightsFalloff");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform thresholdMap                     = new Uniform("_ThresholdMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform thresholdMapScale                = new Uniform("_ThresholdMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochRampIntensity               = new Uniform("_GoochRampIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochRamp                        = new Uniform("_GoochRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochBrightColor                 = new Uniform("_GoochBrightColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochBrightMap                   = new Uniform("_GoochBrightMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochDarkColor                   = new Uniform("_GoochDarkColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochDarkRemapMin                = new Uniform("_GoochDarkRemapMin");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochDarkRemapMax                = new Uniform("_GoochDarkRemapMax");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochDarkRemapFadeWithIndirect   = new Uniform("_GoochDarkRemapFadeWithIndirect");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform goochDarkMap                     = new Uniform("_GoochDarkMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform colorGrading                     = new Uniform("_ColorGrading");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform hue                              = new Uniform("_Hue");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform contrast                         = new Uniform("_Contrast");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform saturation                       = new Uniform("_Saturation");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform brightness                       = new Uniform("_Brightness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescence                      = new Uniform("_Iridescence");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescenceRamp                  = new Uniform("_IridescenceRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescenceSize                  = new Uniform("_IridescenceSize");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescenceThresholdOffset       = new Uniform("_IridescenceThresholdOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescenceSmoothness            = new Uniform("_IridescenceSmoothness");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform iridescenceColor                 = new Uniform("_IridescenceColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rim                              = new Uniform("_Rim");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimColor                         = new Uniform("_RimColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimBrightColor                   = new Uniform("_RimBrightColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimDarkColor                     = new Uniform("_RimDarkColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform rimSize                          = new Uniform("_RimSize");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform vertexAnimation                  = new Uniform("_VertexAnimation");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform vertexAnimationStutter           = new Uniform("_VertexAnimationStutter");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform vertexAnimationMap               = new Uniform("_VertexAnimationMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform vertexAnimationIntensity         = new Uniform("_VertexAnimationIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform vertexAnimationFrequency         = new Uniform("_VertexAnimationFrequency");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolve                         = new Uniform("_Dissolve");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveMap                      = new Uniform("_DissolveMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveMapScale                 = new Uniform("_DissolveMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveAmount                   = new Uniform("_DissolveAmount");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveBorderSize               = new Uniform("_DissolveBorderSize");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveBorderRamp               = new Uniform("_DissolveBorderRamp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform dissolveBorderColor              = new Uniform("_DissolveBorderColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform artistic                         = new Uniform("_Artistic");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform artisticProjection               = new Uniform("_ArtisticProjection");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform artisticFrequency                = new Uniform("_ArtisticFrequency");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform drawnMapScale                    = new Uniform("_DrawnMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform drawnMap                         = new Uniform("_DrawnMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform hatchingMapScale                 = new Uniform("_HatchingMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform hatchingBrightMap                = new Uniform("_HatchingBrightMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform hatchingDarkMap                  = new Uniform("_HatchingDarkMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform drawnClampMin                    = new Uniform("_DrawnClampMin");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform drawnClampMax                    = new Uniform("_DrawnClampMax");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform sketchMapScale                   = new Uniform("_SketchMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform sketchMap                        = new Uniform("_SketchMap");
        #if MK_TOON_STYLIZE_SYSTEM_SHADOWS
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform artisticShadowFilter             = new Uniform("_ArtisticShadowFilter");
        #endif

        /////////////////
        // Advanced    //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform screenSpaceReflections = new Uniform("_ScreenSpaceReflections");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform screenSpaceReflectionsContributeTransparent = new Uniform("_ScreenSpaceReflectionsContributeTransparent");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform diffuse                    = new Uniform("_Diffuse");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specular                   = new Uniform("_Specular");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform specularIntensity          = new Uniform("_SpecularIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform anisotropy                 = new Uniform("_Anisotropy");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform lightTransmissionIntensity = new Uniform("_LightTransmissionIntensity");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform environmentReflections     = new Uniform("_EnvironmentReflections");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform fresnelHighlights          = new Uniform("_FresnelHighlights");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform IndirectFade               = new Uniform("_IndirectFade");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform alembicMotionVectors     = new Uniform("_AlembicMotionVectors");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencil                    = new Uniform("_Stencil");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform renderPriority             = new Uniform("_RenderPriority");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilRef                 = new Uniform("_StencilRef");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilReadMask            = new Uniform("_StencilReadMask");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilWriteMask           = new Uniform("_StencilWriteMask");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilComp                = new Uniform("_StencilComp");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilPass                = new Uniform("_StencilPass");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilFail                = new Uniform("_StencilFail");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stencilZFail               = new Uniform("_StencilZFail");
       
        /////////////////
        // Outline     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outline           = new Uniform("_Outline");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineData       = new Uniform("_OutlineData");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineMap        = new Uniform("_OutlineMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineClipOffset = new Uniform("_OutlineClipOffset");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineSize       = new Uniform("_OutlineSize");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineConstantSize       = new Uniform("_OutlineConstantSize");
        #if MK_TOON_OUTLINE_FADING_LINEAR  || MK_TOON_OUTLINE_FADING_EXPONENTIAL || MK_TOON_OUTLINE_FADING_INVERSE_EXPONENTIAL
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineFadeMin    = new Uniform("_OutlineFadeMin");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineFadeMax    = new Uniform("_OutlineFadeMax");
        #endif
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineColor      = new Uniform("_OutlineColor");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineNoise      = new Uniform("_OutlineNoise");

        /////////////////
        // Refraction  //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform refractionDistortionMapScale = new Uniform("_RefractionDistortionMapScale");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform refractionDistortionMap      = new Uniform("_RefractionDistortionMap");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform refractionDistortion         = new Uniform("_RefractionDistortion");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform indexOfRefraction            = new Uniform("_IndexOfRefraction");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform refractionDistortionFade     = new Uniform("_RefractionDistortionFade");

        /////////////////
        // Particles   //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform flipbook               = new Uniform("_Flipbook");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform softFade               = new Uniform("_SoftFade");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform softFadeNearDistance   = new Uniform("_SoftFadeNearDistance");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform softFadeFarDistance    = new Uniform("_SoftFadeFarDistance");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform cameraFade             = new Uniform("_CameraFade");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform cameraFadeNearDistance = new Uniform("_CameraFadeNearDistance");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform cameraFadeFarDistance  = new Uniform("_CameraFadeFarDistance");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform colorBlend             = new Uniform("_ColorBlend");

        /////////////////
        // Editor Only //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform initialized   = new Uniform("_Initialized");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform optionsTab    = new Uniform("_OptionsTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform inputTab      = new Uniform("_InputTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform stylizeTab    = new Uniform("_StylizeTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform advancedTab   = new Uniform("_AdvancedTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform particlesTab  = new Uniform("_ParticlesTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform outlineTab    = new Uniform("_OutlineTab");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform refractionTab = new Uniform("_RefractionTab");

        /////////////////
        // System      //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform mainTex = new Uniform("_MainTex");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform cutoff  = new Uniform("_Cutoff");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Uniform color  = new Uniform("_Color");
    }
}
