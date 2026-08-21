#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public static partial class ASPRenderUtil
    {
        private class ClearGlobalTexturePassData
        {
        }

        public static void RecordClearGlobalTexturePass(RenderGraph renderGraph, string passName, int propertyId)
        {
            using var builder = renderGraph.AddRasterRenderPass<ClearGlobalTexturePassData>(passName, out var passData);

            builder.AllowPassCulling(false);
            builder.SetGlobalTextureAfterPass(TextureHandle.nullHandle, propertyId);
            builder.SetRenderFunc((ClearGlobalTexturePassData data, RasterGraphContext context) => { });
        }
    }
}
#endif
