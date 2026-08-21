#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public partial class ASPDepthOffsetShadowFeature
    {
        public partial class ASPDepthOffsetShadowPass
        {
            private class DepthOffsetShadowPassData
            {
                public TextureHandle DestinationDepth;
                public RendererListHandle RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder =
                    renderGraph.AddUnsafePass<DepthOffsetShadowPassData>(passName, out var passData,
                        new ProfilingSampler("ASP Depth Offset Shadow Pass RG")))
                {
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    var shadowData = frameData.Create<ASPDepthOffsetShadowCleanUpPass.ASPDepthOffsetShadowData>();
                    var sortFlags = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings =
                        RenderingUtils.CreateDrawingSettings(_shaderTagId, universalRenderingData, cameraData, lightData, sortFlags);

                    var param =
                        new RendererListParams(universalRenderingData.cullResults, drawSettings, _filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(param);

                    RenderTextureDescriptor desc = new RenderTextureDescriptor(
                        cameraData.cameraTargetDescriptor.width,
                        cameraData.cameraTargetDescriptor.height);
                    desc.colorFormat = RenderTextureFormat.Depth;
                    desc.depthBufferBits = cameraData.cameraTargetDescriptor.depthBufferBits;
                    TextureHandle destinationDepth =
                        UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ASPDepthOffsetShadowTexture", false);
                    passData.DestinationDepth = destinationDepth;

                    builder.UseRendererList(passData.RendererListHandle);
                    builder.UseTexture(passData.DestinationDepth, AccessFlags.WriteAll);
                    builder.AllowPassCulling(true);

                    builder.SetRenderFunc((DepthOffsetShadowPassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        var nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);
            
                        // Manually set up the depth render target
                        nativeCmd.SetRenderTarget(data.DestinationDepth);
                        nativeCmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1, 0);
                        nativeCmd.DrawRendererList(data.RendererListHandle);
                        nativeCmd.SetGlobalTexture(ASPShaderIDs.ASPDepthOffsetShadowTexture, data.DestinationDepth);
                    });
                    shadowData.DepthOffsetShadowTexture = destinationDepth;
                }
            }
        }

        public partial class ASPDepthOffsetShadowCleanUpPass : ScriptableRenderPass
        {
            private class CleanUpPassData
            {
                public TextureHandle DepthOffsetShadowTexture;
            }
            
            public class ASPDepthOffsetShadowData : ContextItem
            {
                public TextureHandle DepthOffsetShadowTexture;
                public override void Reset()
                {
                    DepthOffsetShadowTexture = TextureHandle.nullHandle;
                }
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var shadowData = frameData.Get<ASPDepthOffsetShadowData>();
                if (!shadowData.DepthOffsetShadowTexture.IsValid())
                    return;

                using var builder = renderGraph.AddRasterRenderPass<CleanUpPassData>(
                    "ASP DepthOffsetShadow Lifetime Anchor", out var passData);

                passData.DepthOffsetShadowTexture = shadowData.DepthOffsetShadowTexture;
                builder.UseTexture(passData.DepthOffsetShadowTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CleanUpPassData data, RasterGraphContext ctx) => { });
            }
        }
    }
}
#endif
