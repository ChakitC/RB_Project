//////////////////////////////////////////////////////
// MK Toon Properties								//
//					                                //
// Created by Michael Kremmel                       //
// www.michaelkremmel.de                            //
// Copyright © 2020 All rights reserved.            //
//////////////////////////////////////////////////////

namespace MK.Toon
{
    public static class Properties
    {
        internal static readonly string shaderComponentOutlineName = "Outline";
        internal static readonly string shaderComponentRefractionName = "Refraction";
        internal static readonly string shaderVariantPBSName = "Physically Based";
        internal static readonly string shaderVariantSimpleName = "Simple";
        internal static readonly string shaderVariantUnlitName = "Unlit";
        
        //CurvedWorldSupport
        //CurvedWorldEditor
        //public static readonly Vector4Property curvedWorldBlendSettings = new Vector4Property(Uniforms.curvedWorldBlendSettings);
        //

        /////////////////
        // Options     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Workflow> workflow = new EnumProperty<Workflow>(Uniforms.workflow, Keywords.workflow);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<RenderFace> renderFace      = new EnumProperty<RenderFace>(Uniforms.renderFace);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly SurfaceProperty surface                  = new SurfaceProperty(Uniforms.surface, Keywords.surface);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<ZWrite> zWrite              = new EnumProperty<ZWrite>(Uniforms.zWrite);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<ZTest> zTest                = new EnumProperty<ZTest>(Uniforms.zTest);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<BlendFactor> blendSrc       = new EnumProperty<BlendFactor>(Uniforms.blendSrc);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<BlendFactor> blendDst       = new EnumProperty<BlendFactor>(Uniforms.blendDst);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<BlendFactor> blendSrcAlpha  = new EnumProperty<BlendFactor>(Uniforms.blendSrcAlpha);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<BlendFactor> blendDstAlpha  = new EnumProperty<BlendFactor>(Uniforms.blendDstAlpha);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BlendProperty blend                      = new BlendProperty(Uniforms.blend, Keywords.blend);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly AlphaClippingProperty alphaClipping      = new AlphaClippingProperty(Uniforms.alphaClipping, Keywords.alphaClipping);

        /////////////////
        // Input       //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty albedoColor                         = new ColorProperty(Uniforms.albedoColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty alphaCutoff                         = new RangeProperty(Uniforms.alphaCutoff, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty albedoMap                         = new TextureProperty(Uniforms.albedoMap, Keywords.albedoMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty albedoMapIntensity                  = new RangeProperty(Uniforms.albedoMapIntensity, 0f, 1f);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TilingProperty mainTiling                         = new TilingProperty(Uniforms.albedoMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly OffsetProperty mainOffset                         = new OffsetProperty(Uniforms.albedoMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty specularColor                       = new ColorProperty(Uniforms.specularColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty metallic                            = new RangeProperty(Uniforms.metallic, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty smoothness                          = new RangeProperty(Uniforms.smoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty roughness                           = new RangeProperty(Uniforms.roughness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty specularMap                       = new TextureProperty(Uniforms.specularMap, Keywords.pbsMap0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty roughnessMap                      = new TextureProperty(Uniforms.roughnessMap, Keywords.pbsMap1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty metallicMap                       = new TextureProperty(Uniforms.metallicMap, Keywords.pbsMap0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty normalMapIntensity                  = new FloatProperty(Uniforms.normalMapIntensity);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty normalMap                         = new TextureProperty(Uniforms.normalMap, Keywords.normalMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty parallax                            = new RangeProperty(Uniforms.parallax, Keywords.parallax, 0, 0.1f);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty heightMap                         = new TextureProperty(Uniforms.heightMap, Keywords.heightMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<LightTransmission> lightTransmission = new EnumProperty<LightTransmission>(Uniforms.lightTransmission);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightTransmissionDistortion         = new RangeProperty(Uniforms.lightTransmissionDistortion, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty lightTransmissionColor              = new ColorProperty(Uniforms.lightTransmissionColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty thicknessMap                      = new TextureProperty(Uniforms.thicknessMap, Keywords.thicknessMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty occlusionMapIntensity               = new RangeProperty(Uniforms.occlusionMapIntensity, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty occlusionMap                      = new TextureProperty(Uniforms.occlusionMap, Keywords.occlusionMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty emissionColor                       = new ColorProperty(Uniforms.emissionColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty emissionMap                       = new TextureProperty(Uniforms.emissionMap, Keywords.emissionMap);

        /////////////////
        // Detail      //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<DetailBlend> detailBlend  = new EnumProperty<DetailBlend>(Uniforms.detailBlend, Keywords.detailBlend);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty detailColor              = new ColorProperty(Uniforms.detailColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty detailMix                = new RangeProperty(Uniforms.detailMix, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty detailMap              = new TextureProperty(Uniforms.detailMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TilingProperty detailTiling            = new TilingProperty(Uniforms.detailMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly OffsetProperty detailOffset            = new OffsetProperty(Uniforms.detailMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty detailNormalMapIntensity = new FloatProperty(Uniforms.detailNormalMapIntensity);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty detailNormalMap        = new TextureProperty(Uniforms.detailNormalMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly IntProperty detailUVSet                = new IntProperty(Uniforms.detailUVSet);

        /////////////////
        // Stylize     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty receiveShadows                         = new BoolProperty(Uniforms.receiveShadows, Keywords.receiveShadows);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty wrappedLighting                        = new BoolProperty(Uniforms.wrappedLighting, Keywords.wrappedLighting);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty diffuseSmoothness                     = new RangeProperty(Uniforms.diffuseSmoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty diffuseThresholdOffset                = new RangeProperty(Uniforms.diffuseThresholdOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty specularSmoothness                    = new RangeProperty(Uniforms.specularSmoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty specularThresholdOffset               = new RangeProperty(Uniforms.specularThresholdOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty rimSmoothness                         = new RangeProperty(Uniforms.rimSmoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty rimThresholdOffset                    = new RangeProperty(Uniforms.rimThresholdOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightTransmissionSmoothness           = new RangeProperty(Uniforms.lightTransmissionSmoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightTransmissionThresholdOffset      = new RangeProperty(Uniforms.lightTransmissionThresholdOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Light> light                           = new EnumProperty<Light>(Uniforms.light, Keywords.light);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty diffuseRamp                         = new TextureProperty(Uniforms.diffuseRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty specularRamp                        = new TextureProperty(Uniforms.specularRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty rimRamp                             = new TextureProperty(Uniforms.rimRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty lightTransmissionRamp               = new TextureProperty(Uniforms.lightTransmissionRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly StepProperty lightBands                             = new StepProperty(Uniforms.lightBands, 2, 12);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightBandsScale                       = new RangeProperty(Uniforms.lightBandsScale, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightThreshold                        = new RangeProperty(Uniforms.lightThreshold, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty additionalLightsThreshold             = new RangeProperty(Uniforms.additionalLightsThreshold, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty additionalLightsFalloff               = new RangeProperty(Uniforms.additionalLightsFalloff, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty thresholdMap                        = new TextureProperty(Uniforms.thresholdMap, Keywords.thresholdMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty thresholdMapScale                     = new FloatProperty(Uniforms.thresholdMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty goochRampIntensity                    = new RangeProperty(Uniforms.goochRampIntensity, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty goochRamp                           = new TextureProperty(Uniforms.goochRamp, Keywords.goochRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty goochBrightColor                      = new ColorProperty(Uniforms.goochBrightColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty goochBrightMap                      = new TextureProperty(Uniforms.goochBrightMap, Keywords.goochBrightMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty goochDarkColor                        = new ColorProperty(Uniforms.goochDarkColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty goochDarkRemapMin                     = new ColorProperty(Uniforms.goochDarkRemapMin);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty goochDarkRemapMax                     = new ColorProperty(Uniforms.goochDarkRemapMax);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly IntProperty goochDarkRemapFadeWithIndirect          = new IntProperty(Uniforms.goochDarkRemapFadeWithIndirect);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty goochDarkMap                        = new TextureProperty(Uniforms.goochDarkMap, Keywords.goochDarkMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<ColorGrading> colorGrading             = new EnumProperty<ColorGrading>(Uniforms.colorGrading, Keywords.colorGrading);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty hue                                   = new RangeProperty(Uniforms.hue, 0f, 1f);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty contrast                              = new FloatProperty(Uniforms.contrast);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty saturation                            = new RangeProperty(Uniforms.saturation, 0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty brightness                            = new RangeProperty(Uniforms.brightness, 0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Iridescence> iridescence               = new EnumProperty<Iridescence>(Uniforms.iridescence, Keywords.iridescence);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty iridescenceRamp                     = new TextureProperty(Uniforms.iridescenceRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty iridescenceSize                       = new RangeProperty(Uniforms.iridescenceSize, 0, 5);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty iridescenceThresholdOffset            = new RangeProperty(Uniforms.iridescenceThresholdOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty iridescenceSmoothness                 = new RangeProperty(Uniforms.iridescenceSmoothness, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty iridescenceColor                      = new ColorProperty(Uniforms.iridescenceColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Rim> rim                               = new EnumProperty<Rim>(Uniforms.rim, Keywords.rim);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty rimColor                              = new ColorProperty(Uniforms.rimColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty rimBrightColor                        = new ColorProperty(Uniforms.rimBrightColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty rimDarkColor                          = new ColorProperty(Uniforms.rimDarkColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty rimSize                               = new RangeProperty(Uniforms.rimSize, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<VertexAnimation> vertexAnimation       = new EnumProperty<VertexAnimation>(Uniforms.vertexAnimation, Keywords.vertexAnimation);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty vertexAnimationStutter                 = new BoolProperty(Uniforms.vertexAnimationStutter, Keywords.vertexAnimationStutter);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty vertexAnimationMap                  = new TextureProperty(Uniforms.vertexAnimationMap, Keywords.vertexAnimationMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty vertexAnimationIntensity              = new RangeProperty(Uniforms.vertexAnimationIntensity, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly Vector3Property vertexAnimationFrequency            = new Vector3Property(Uniforms.vertexAnimationFrequency);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Dissolve> dissolve                     = new EnumProperty<Dissolve>(Uniforms.dissolve, Keywords.dissolve);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty dissolveMap                         = new TextureProperty(Uniforms.dissolveMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty dissolveMapScale                      = new FloatProperty(Uniforms.dissolveMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty dissolveAmount                        = new RangeProperty(Uniforms.dissolveAmount, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty dissolveBorderSize                    = new RangeProperty(Uniforms.dissolveBorderSize, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty dissolveBorderRamp                  = new TextureProperty(Uniforms.dissolveBorderRamp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty dissolveBorderColor                   = new ColorProperty(Uniforms.dissolveBorderColor);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Artistic> artistic                     = new EnumProperty<Artistic>(Uniforms.artistic, Keywords.artistic);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<ArtisticProjection> artisticProjection = new EnumProperty<ArtisticProjection>(Uniforms.artisticProjection, Keywords.artisticProjection);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty artisticFrequency                     = new RangeProperty(Uniforms.artisticFrequency, Keywords.artisticAnimation, 1.0f, 1, 10);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty drawnMapScale                         = new FloatProperty(Uniforms.drawnMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty drawnMap                            = new TextureProperty(Uniforms.drawnMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty hatchingMapScale                      = new FloatProperty(Uniforms.hatchingMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty hatchingBrightMap                   = new TextureProperty(Uniforms.hatchingBrightMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty hatchingDarkMap                     = new TextureProperty(Uniforms.hatchingDarkMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty drawnClampMin                         = new RangeProperty(Uniforms.drawnClampMin, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty drawnClampMax                         = new RangeProperty(Uniforms.drawnClampMax, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty sketchMapScale                        = new FloatProperty(Uniforms.sketchMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty sketchMap                           = new TextureProperty(Uniforms.sketchMap);
        #if MK_TOON_STYLIZE_SYSTEM_SHADOWS
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty artisticShadowFilter                  = new RangeProperty(Uniforms.artisticShadowFilter, 0f, 0.5f);
        #endif
        /////////////////
        // Advanced    //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FBoolProperty screenSpaceReflections = new FBoolProperty(Uniforms.screenSpaceReflections, "_SCREENSPACEREFLECTIONS_OFF");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FBoolProperty screenSpaceReflectionsContributeTransparent = new FBoolProperty(Uniforms.screenSpaceReflectionsContributeTransparent, "_SCREENSPACEREFLECTIONSCONTRIBUTETRANSPARENT_OFF");
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Diffuse> diffuse                        = new EnumProperty<Diffuse>(Uniforms.diffuse, Keywords.diffuse);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly SpecularProperty specular                            = new SpecularProperty(Uniforms.specular, Keywords.specular);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty specularIntensity                      = new RangeProperty(Uniforms.specularIntensity, 0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty anisotropy                             = new RangeProperty(Uniforms.anisotropy, -1, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty lightTransmissionIntensity             = new RangeProperty(Uniforms.lightTransmissionIntensity, 0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnvironmentReflectionProperty environmentReflections = new EnvironmentReflectionProperty(Uniforms.environmentReflections, Keywords.environmentReflections);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty fresnelHighlights                       = new BoolProperty(Uniforms.fresnelHighlights, Keywords.fresnelHighlights);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty indirectFade                           = new RangeProperty(Uniforms.IndirectFade, 0.0f, 1.0f);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty alembicMotionVectors                    = new BoolProperty(Uniforms.alembicMotionVectors, Keywords.alembicMotionVectors);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RenderPriorityProperty renderPriority                = new RenderPriorityProperty(Uniforms.renderPriority);
        //Stencil
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly StencilModeProperty stencil                          = new StencilModeProperty(Uniforms.stencil);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly StepProperty stencilRef                              = new StepProperty(Uniforms.stencilRef, 0, 255);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly StepProperty stencilReadMask                         = new StepProperty(Uniforms.stencilReadMask, 0, 255);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly StepProperty stencilWriteMask                        = new StepProperty(Uniforms.stencilWriteMask, 0, 255);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<StencilComparison> stencilComp          = new EnumProperty<StencilComparison>(Uniforms.stencilComp);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<StencilOperation> stencilPass           = new EnumProperty<StencilOperation>(Uniforms.stencilPass);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<StencilOperation> stencilFail           = new EnumProperty<StencilOperation>(Uniforms.stencilFail);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<StencilOperation> stencilZFail          = new EnumProperty<StencilOperation>(Uniforms.stencilZFail);
       
        /////////////////
        // Outline     //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<Outline> outline         = new EnumProperty<Outline>(Uniforms.outline, Keywords.outline);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<OutlineData> outlineData = new EnumProperty<OutlineData>(Uniforms.outlineData, Keywords.outlineData);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty outlineMap            = new TextureProperty(Uniforms.outlineMap, Keywords.outlineMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty outlineClipOffset       = new RangeProperty(Uniforms.outlineClipOffset, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty outlineSize             = new RangeProperty(Uniforms.outlineSize, 0);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty outlineConstantSize      = new BoolProperty(Uniforms.outlineConstantSize);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly ColorProperty outlineColor            = new ColorProperty(Uniforms.outlineColor);
        #if MK_TOON_OUTLINE_FADING_LINEAR  || MK_TOON_OUTLINE_FADING_EXPONENTIAL || MK_TOON_OUTLINE_FADING_INVERSE_EXPONENTIAL
            #if UNITY_6000_5_OR_NEWER
            [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
            #endif
            public static readonly FloatProperty outlineFadeMin      = new FloatProperty(Uniforms.outlineFadeMin);
            #if UNITY_6000_5_OR_NEWER
            [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
            #endif
            public static readonly FloatProperty outlineFadeMax      = new FloatProperty(Uniforms.outlineFadeMax);
            #endif
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty outlineNoise            = new RangeProperty(Uniforms.outlineNoise, Keywords.outlineNoise, -1, 1);

        /////////////////
        // Refraction  //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty refractionDistortionMapScale = new FloatProperty(Uniforms.refractionDistortionMapScale);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly TextureProperty refractionDistortionMap    = new TextureProperty(Uniforms.refractionDistortionMap, Keywords.refractionDistortionMap);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty refractionDistortion         = new FloatProperty(Uniforms.refractionDistortion);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty refractionDistortionFade     = new RangeProperty(Uniforms.refractionDistortionFade, 0, 1);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly RangeProperty indexOfRefraction            = new RangeProperty(Uniforms.indexOfRefraction, Keywords.indexOfRefraction, 0, 0.5f);

        /////////////////
        // Particles   //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty flipbook                = new BoolProperty(Uniforms.flipbook, Keywords.flipbook);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty softFade                = new BoolProperty(Uniforms.softFade, Keywords.softFade);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty softFadeNearDistance   = new FloatProperty(Uniforms.softFadeNearDistance);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty softFadeFarDistance    = new FloatProperty(Uniforms.softFadeFarDistance);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly BoolProperty cameraFade              = new BoolProperty(Uniforms.cameraFade, Keywords.cameraFade);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty cameraFadeNearDistance = new FloatProperty(Uniforms.cameraFadeNearDistance);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly FloatProperty cameraFadeFarDistance  = new FloatProperty(Uniforms.cameraFadeFarDistance);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        public static readonly EnumProperty<ColorBlend> colorBlend  = new EnumProperty<ColorBlend>(Uniforms.colorBlend, Keywords.colorBlend);

        /////////////////
        // System      //
        /////////////////
        /// <summary>
        /// This function should be called after changing the Albedo Map or AlphaCutoff Properties.
        /// It makes sure that baked system shadows work correctly with alpha clipping.
        /// </summary>
        /// <param name="material"></param>
        public static void UpdateSystemProperties(UnityEngine.Material material)
        {
            material.SetTexture(Uniforms.mainTex.id, Properties.albedoMap.GetValue(material));
            material.SetFloat(Uniforms.cutoff.id, Properties.alphaCutoff.GetValue(material));
            material.SetColor(Uniforms.color.id, Properties.albedoColor.GetValue(material));
        }
    }
}
