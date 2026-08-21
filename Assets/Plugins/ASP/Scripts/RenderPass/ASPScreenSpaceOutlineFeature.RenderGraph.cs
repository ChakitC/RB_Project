#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public partial class ASPScreenSpaceOutlineFeature
    {
        public partial class ASPScreenSpaceOutlinePass
        {
            private class OutlinePassData
            {
                public TextureHandle Destination;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_outlineEffectMaterial == null)
                    return;
               _screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();
                if (_screenSpaceOutlineSetting == null)
                    return;
               if (!_screenSpaceOutlineSetting.IsActive())
                    return;
                bool requiresNormals = _screenSpaceOutlineSetting.EnableNormalsEdge.value;
                var resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.cameraDepthTexture.IsValid())
                    return;
                if (requiresNormals && !resourceData.cameraNormalsTexture.IsValid())
                    return;
                using (var builder =
                    renderGraph.AddRasterRenderPass<OutlinePassData>("ASP Screen Space Outline Overlay Pass", out var passData))
                {
                    passData.Destination = resourceData.activeColorTexture;
                    passData.Material = _outlineEffectMaterial;

                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    if (requiresNormals)
                        builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                    builder.UseGlobalTexture(ASPShaderIDs.ASPMaterialTexture, AccessFlags.Read);
                    builder.UseGlobalTexture(ASPShaderIDs.ASPMaterialDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((OutlinePassData data, RasterGraphContext rgContext) =>
                    {
                        SetupOutlineFirstPassParam(data.Material, _screenSpaceOutlineSetting);
                        data.Material.SetColor(ASPShaderIDs.OutlineColor, _screenSpaceOutlineSetting.OutlineColor.value);
                        data.Material.SetVector(ASPShaderIDs.BlitScaleBias, Vector2.one);
                        ASPRenderUtil.DrawFullScreen(rgContext.cmd, data.Material, 0);
                    });
                }
            }
        }
    }
}
#endif
