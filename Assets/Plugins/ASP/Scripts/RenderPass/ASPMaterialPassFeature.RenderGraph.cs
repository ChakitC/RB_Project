#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public partial class ASPMaterialPassFeature
    {
        public partial class ASPMaterialPass
        {
            private class MaterialPassData
            {
                public TextureHandle Destination;
                public TextureHandle DestinationDepth;
                public RendererListHandle RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder =
                    renderGraph.AddUnsafePass<MaterialPassData>(passName, out var passData,
                        new ProfilingSampler("ASP Material Pass RG")))
                {
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();

                    var sortFlags = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings =
                        RenderingUtils.CreateDrawingSettings(_shaderTagId, universalRenderingData, cameraData, lightData, sortFlags);

                    var param =
                        new RendererListParams(universalRenderingData.cullResults, drawSettings, _filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(param);

                    RenderTextureDescriptor desc = new RenderTextureDescriptor(
                        cameraData.cameraTargetDescriptor.width,
                        cameraData.cameraTargetDescriptor.height);
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    desc.depthBufferBits = 0;
                    TextureHandle destination =
                        UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ASPMaterialTexture", false);
                    passData.Destination = destination;

                    desc.colorFormat = RenderTextureFormat.Depth;
                    desc.depthBufferBits = cameraData.cameraTargetDescriptor.depthBufferBits;
                    TextureHandle destinationDepth =
                        UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ASPMaterialDepthTexture", false);
                    passData.DestinationDepth = destinationDepth;

                    builder.UseRendererList(passData.RendererListHandle);
                    builder.UseTexture(passData.Destination, AccessFlags.WriteAll);
                    builder.UseTexture(passData.DestinationDepth, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaterialPassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        var nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);
                        nativeCmd.SetRenderTarget(data.Destination, data.DestinationDepth);
                        nativeCmd.ClearRenderTarget(RTClearFlags.All, Color.clear, 1, 0);
                        nativeCmd.DrawRendererList(data.RendererListHandle);
                    });
                    builder.SetGlobalTextureAfterPass(destination, ASPShaderIDs.ASPMaterialTexture);
                    builder.SetGlobalTextureAfterPass(destinationDepth, ASPShaderIDs.ASPMaterialDepthTexture);
                }
            }
        }
    }
}
#endif
