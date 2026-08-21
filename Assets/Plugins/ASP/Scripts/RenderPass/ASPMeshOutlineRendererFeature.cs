/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP Mesh Outline")]
    public class ASPMeshOutlineRendererFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("m_layer")]
        [SingleLayerMask] public int Layer;
        [FormerlySerializedAs("m_renderingLayerMask")]
        [RenderingLayerMask] public int RenderingLayerMask;
        public RenderPassEvent InjectPoint = RenderPassEvent.AfterRenderingSkybox;
        private RenderQueueRange _range = RenderQueueRange.opaque;

        [FormerlySerializedAs("InjectPassLightModeTag")]
        [FormerlySerializedAs("lightModeTag")]
        private string _lightModeTag = "ASPOutlineObject";

        private MeshOutlinePass _meshOutlinePass;

        public override void Create()
        {
            _meshOutlinePass = new MeshOutlinePass(name, _lightModeTag, InjectPoint, _range, (uint)RenderingLayerMask,
                1 << Layer, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_meshOutlinePass);
        }

        public class MeshOutlinePass : ScriptableRenderPass
        {
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId;
            private string _profilerTag;

            public MeshOutlinePass(string profilerTag, string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState,
                int stencilReference)
            {
                _profilerTag = profilerTag;
                renderPassEvent = evt;
                _filteringSettings = new FilteringSettings(renderQueueRange);
                _filteringSettings.layerMask = layerMask;
                _filteringSettings.renderingLayerMask = renderingLayerMask;
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _shaderTagId = new ShaderTagId(shaderTagId);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("Mesh Outline Pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = SortingCriteria.CommonOpaque;
                    var sortingSettings = new SortingSettings(renderingData.cameraData.camera);
                    sortingSettings.criteria = sortFlags;
                    var drawSettings = new DrawingSettings(_shaderTagId, sortingSettings);
                    drawSettings.perObjectData = PerObjectData.None;

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                        ref _filteringSettings, ref _renderStateBlock);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#if UNITY_6000_0_OR_NEWER
            private class MeshOutlinePassData
            {
                public TextureHandle Destination;
                public RendererListHandle RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddRasterRenderPass<MeshOutlinePassData>(passName, out var passData, 
                           new ProfilingSampler("ASP MeshOutline Pass RG")))
                {
                    // Access the relevant frame data from the Universal Render Pipeline
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    
                    var sortFlags = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings =
 RenderingUtils.CreateDrawingSettings(_shaderTagId, universalRenderingData, cameraData, lightData, sortFlags);

                    var param =
 new RendererListParams(universalRenderingData.cullResults, drawSettings, _filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(param);
                    
                    passData.Destination = resourceData.activeColorTexture;
                    
                    builder.UseRendererList(passData.RendererListHandle);
                    builder.SetRenderAttachment(passData.Destination, 0);
                    builder.AllowPassCulling(true);
                    builder.SetRenderFunc((MeshOutlinePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.RendererListHandle); 
                    });
                }
            }
#endif
        }
    }
}
