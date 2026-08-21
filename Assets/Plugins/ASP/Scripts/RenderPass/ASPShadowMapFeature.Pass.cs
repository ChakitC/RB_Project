/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPShadowMapFeature
    {
        public partial class ASPShadowRenderPass : ScriptableRenderPass
        {
            public CullingResults[] CullResults;
            public bool UseInjectCullResult;
            public bool PerformExtraCull;
            public bool IsNotActive;

            public ASPShadowData ShadowData;
            public bool IsEmptyShadowMap;

            // Shadow map-related data
            public Matrix4x4[] CustomWorldToShadowMatrices;
            public List<Plane[]> CascadeCullPlanes;
            public Matrix4x4[] LightViewMatrices;
            public Matrix4x4[] LightProjectionMatrices;
            public ShadowSliceData[] ShadowSliceDatas;
            public Vector4[] CascadeSplitDistances;

            protected FilteringSettings _filteringSettings;
            protected RenderStateBlock _renderStateBlock;
            protected ShaderTagId _shaderTagId;
            protected int _customBufferNameId;
            protected ScriptableCullingParameters _cullingParameters;

            protected const int k_ShadowmapBufferBits = 16;
            protected static readonly ProfilingSampler s_shadowMapExecuteSampler =
                new ProfilingSampler("ASP ShadowMap Pass");

            public ASPShadowRenderPass(uint renderingLayerMask, int customBufferNameId,
                RenderPassEvent passEvent, RenderQueueRange queueRange, LayerMask layerMask, ASPShadowData shadowData)
            {
                profilingSampler = new ProfilingSampler("ASP Shadow Render Pass");
                _customBufferNameId = customBufferNameId;
                renderPassEvent = passEvent;
                _filteringSettings = new FilteringSettings(RenderQueueRange.all);
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _shaderTagId = new ShaderTagId("ASPShadowCaster");
                ShadowData = shadowData;

                ClearData();
                ASPMainLightShadowConstantBuffer.Initialize();
                InitVersionSpecific();
            }

            private void ClearData()
            {
                _cullingParameters = new ScriptableCullingParameters();
                CascadeSplitDistances = new Vector4[4];

                CustomWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < CustomWorldToShadowMatrices.Length; i++)
                {
                    CustomWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                LightViewMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    LightViewMatrices[i] = Matrix4x4.identity;
                }

                LightProjectionMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    LightProjectionMatrices[i] = Matrix4x4.identity;
                }

                ShadowSliceDatas = new ShadowSliceData[4];

                CascadeCullPlanes = new List<Plane[]>();
                for (int i = 0; i < 4; i++)
                {
                    CascadeCullPlanes.Add(new Plane[6]);
                }
            }

            public void Dispose()
            {
                Shader.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                DisposeVersionSpecific();
            }

            partial void InitVersionSpecific();
            partial void DisposeVersionSpecific();
        }
    }

    public static class ASPMainLightShadowConstantBuffer
    {
        public static int s_WorldToShadow;
        public static int s_ShadowParams;
        public static int s_CascadeCount;
        public static int s_CascadeShadowSplitSpheres0;
        public static int s_CascadeShadowSplitSpheres1;
        public static int s_CascadeShadowSplitSpheres2;
        public static int s_CascadeShadowSplitSpheres3;
        public static int s_CascadeShadowSplitSphereRadii;
        public static int s_ShadowOffset0;
        public static int s_ShadowOffset1;
        public static int s_ShadowmapSize;

        private static bool s_initialized;

        public static void Initialize()
        {
            if (s_initialized)
            {
                return;
            }

            s_WorldToShadow = ASPShaderIDs.ASPMainLightWorldToShadow;
            s_ShadowParams = ASPShaderIDs.ASPMainLightShadowParams;
            s_CascadeCount = ASPShaderIDs.ASPCascadeCount;
            s_CascadeShadowSplitSpheres0 = ASPShaderIDs.ASPCascadeShadowSplitSpheres0;
            s_CascadeShadowSplitSpheres1 = ASPShaderIDs.ASPCascadeShadowSplitSpheres1;
            s_CascadeShadowSplitSpheres2 = ASPShaderIDs.ASPCascadeShadowSplitSpheres2;
            s_CascadeShadowSplitSpheres3 = ASPShaderIDs.ASPCascadeShadowSplitSpheres3;
            s_CascadeShadowSplitSphereRadii = ASPShaderIDs.ASPCascadeShadowSplitSphereRadii;
            s_ShadowOffset0 = ASPShaderIDs.ASPMainLightShadowOffset0;
            s_ShadowOffset1 = ASPShaderIDs.ASPMainLightShadowOffset1;
            s_ShadowmapSize = ASPShaderIDs.ASPMainLightShadowmapSize;

            s_initialized = true;
        }
    }
}
