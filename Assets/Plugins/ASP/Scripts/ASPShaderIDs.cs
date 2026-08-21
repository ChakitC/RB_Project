using UnityEngine;

namespace ASP
{
    public static class ASPShaderIDs
    {
        // Common
        public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        // Material Pass
        public static readonly int ASPMaterialTexture = Shader.PropertyToID("_ASPMaterialTexture");
        public static readonly int ASPMaterialDepthTexture = Shader.PropertyToID("_ASPMaterialDepthTexture");
        public static readonly int MaterialID = Shader.PropertyToID("_MaterialID");

        // Shadow
        public static readonly int ASPDepthOffsetShadowTexture = Shader.PropertyToID("_ASPDepthOffsetShadowTexture");
        public static readonly int ASPShadowMap = Shader.PropertyToID("_ASPShadowMap");
        public static readonly int ASPShadowMapValid = Shader.PropertyToID("_ASPShadowMapValid");
        public static readonly int ASPShadowBias = Shader.PropertyToID("_ASPShadowBias");
        public static readonly int ASPLightDirection = Shader.PropertyToID("_ASPLightDirection");
        public static readonly int ASPLightPosition = Shader.PropertyToID("_ASPLightPosition");
        public static readonly int ASPMainLightWorldToShadow = Shader.PropertyToID("_ASPMainLightWorldToShadow");
        public static readonly int ASPMainLightShadowParams = Shader.PropertyToID("_ASPMainLightShadowParams");
        public static readonly int ASPCascadeCount = Shader.PropertyToID("_ASPCascadeCount");
        public static readonly int ASPCascadeShadowSplitSpheres0 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres0");
        public static readonly int ASPCascadeShadowSplitSpheres1 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres1");
        public static readonly int ASPCascadeShadowSplitSpheres2 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres2");
        public static readonly int ASPCascadeShadowSplitSpheres3 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres3");
        public static readonly int ASPCascadeShadowSplitSphereRadii = Shader.PropertyToID("_ASPCascadeShadowSplitSphereRadii");
        public static readonly int ASPMainLightShadowOffset0 = Shader.PropertyToID("_ASPMainLightShadowOffset0");
        public static readonly int ASPMainLightShadowOffset1 = Shader.PropertyToID("_ASPMainLightShadowOffset1");
        public static readonly int ASPMainLightShadowmapSize = Shader.PropertyToID("_ASPMainLightShadowmapSize");

        // Face
        public static readonly int FaceFrontDirection = Shader.PropertyToID("_FaceFrontDirection");
        public static readonly int FaceRightDirection = Shader.PropertyToID("_FaceRightDirection");
        public static readonly int OverrideLightDirToggle = Shader.PropertyToID("_OverrideLightDirToggle");
        public static readonly int FakeLightEuler = Shader.PropertyToID("_FakeLightEuler");

        // Dithering
        public static readonly int Dithering = Shader.PropertyToID("_Dithering");
        public static readonly int DitherTexelSize = Shader.PropertyToID("_DitherTexelSize");
        public static readonly int FOVShiftX = Shader.PropertyToID("_FOVShiftX");

        // Character Shadow
        public static readonly int UseSimpleAABBCutOffForCharacterShadow = Shader.PropertyToID("_UseSimpleAABBCutOffForCharacterShadow");

        // Tone Mapping
        public static readonly int LutParams = Shader.PropertyToID("_Lut_Params");
        public static readonly int ToneMapLowerBound = Shader.PropertyToID("_ToneMapLowerBound");
        public static readonly int Exposure = Shader.PropertyToID("_Exposure");
        public static readonly int IgnoreCharacterPixels = Shader.PropertyToID("_IgnoreCharacterPixels");

        // Outline
        public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

        // Outline Edge Detection
        public static readonly int DebugBackgroundColor = Shader.PropertyToID("_DebugBackgroundColor");
        public static readonly int DebugEdgeType = Shader.PropertyToID("_DebugEdgeType");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int MaterialThreshold = Shader.PropertyToID("_MaterialThreshold");
        public static readonly int LumaThreshold = Shader.PropertyToID("_LumaThreshold");
        public static readonly int DepthThreshold = Shader.PropertyToID("_DepthThreshold");
        public static readonly int NormalsThreshold = Shader.PropertyToID("_NormalsThreshold");
        public static readonly int EnableDistanceFalloff = Shader.PropertyToID("_EnableDistanceFalloff");
        public static readonly int DistanceFalloffStartEnd = Shader.PropertyToID("_DistanceFalloffStartEnd");

        // Lens Flare
        public static readonly int LensFlareScreenSpaceBloomMipTexture = Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture");
        public static readonly int LensFlareScreenSpaceResultTexture = Shader.PropertyToID("_LensFlareScreenSpaceResultTexture");
        public static readonly int LensFlareScreenSpaceSpectralLut = Shader.PropertyToID("_LensFlareScreenSpaceSpectralLut");
        public static readonly int LensFlareScreenSpaceStreakTex = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex");
        public static readonly int LensFlareScreenSpaceMipLevel = Shader.PropertyToID("_LensFlareScreenSpaceMipLevel");
        public static readonly int LensFlareScreenSpaceTintColor = Shader.PropertyToID("_LensFlareScreenSpaceTintColor");
        public static readonly int LensFlareScreenSpaceParams1 = Shader.PropertyToID("_LensFlareScreenSpaceParams1");
        public static readonly int LensFlareScreenSpaceParams2 = Shader.PropertyToID("_LensFlareScreenSpaceParams2");
        public static readonly int LensFlareScreenSpaceParams3 = Shader.PropertyToID("_LensFlareScreenSpaceParams3");
        public static readonly int LensFlareScreenSpaceParams4 = Shader.PropertyToID("_LensFlareScreenSpaceParams4");
        public static readonly int LensFlareScreenSpaceParams5 = Shader.PropertyToID("_LensFlareScreenSpaceParams5");
        public static readonly int BloomTexture = Shader.PropertyToID("_Bloom_Texture");
        public static readonly int SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
    }
}
