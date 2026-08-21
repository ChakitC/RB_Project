#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public partial class ASPToneMappingFeature
    {
        public partial class ASPToneMappingPass
        {
            private class ToneMapPassData
            {
                public TextureHandle Source;
                public Material Material;
                public int LutHeight;
                public int LutWidth;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_toneMapMaterial == null)
                    return;

                _toneMapComponent = VolumeManager.instance.stack.GetComponent<ASPToneMap>();
                if (_toneMapComponent == null || !_toneMapComponent.IsActive())
                    return;

                var postProcessingData = frameData.Get<UniversalPostProcessingData>();
                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                var source = resourceData.activeColorTexture;
                var destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "_ASPToneMapColor";
                destinationDescriptor.msaaSamples = MSAASamples.None;
                destinationDescriptor.depthBufferBits = DepthBits.None;
                destinationDescriptor.clearBuffer = false;
                var destination = renderGraph.CreateTexture(destinationDescriptor);

                using (var builder = renderGraph.AddRasterRenderPass<ToneMapPassData>(
                           "ASP ToneMap Pass Apply", out var passData))
                {
                    passData.Source = source;
                    passData.Material = _toneMapMaterial;
                    passData.LutHeight = postProcessingData.lutSize;
                    passData.LutWidth = postProcessingData.lutSize * postProcessingData.lutSize;

                    builder.UseTexture(passData.Source, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.UseGlobalTexture(ASPShaderIDs.ASPMaterialTexture);
                    builder.UseGlobalTexture(ASPShaderIDs.ASPMaterialDepthTexture);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((ToneMapPassData data, RasterGraphContext rgContext) =>
                    {
                        data.Material.SetVector(ASPShaderIDs.BlitScaleBias, Vector2.one);
                        data.Material.SetTexture(ASPShaderIDs.BaseMap, data.Source);
                        SetToneMapParams(data.Material, data.LutWidth, data.LutHeight);
                        ASPRenderUtil.DrawFullScreen(rgContext.cmd, data.Material,
                            (int)_toneMapComponent.ToneMapType.value);
                    });
                }

                resourceData.cameraColor = destination;
            }
        }
    }
}
#endif
