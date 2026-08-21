#if UNITY_2021
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPDepthOffsetShadowFeature
    {
        public partial class ASPDepthOffsetShadowPass
        {
            private RenderTargetHandle _depthTextureHandle;

            partial void InitVersionSpecific()
            {
                _depthTextureHandle.Init("_ASPDepthOffsetShadowTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.Depth;
                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                cmd.GetTemporaryRT(_depthTextureHandle.id, desc);

                ConfigureTarget(_depthTextureHandle.Identifier());
                ConfigureClear(ClearFlag.Depth, Color.black);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(_depthTextureHandle.id);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
                {
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
                cmd.SetGlobalTexture(ASPShaderIDs.ASPDepthOffsetShadowTexture, _depthTextureHandle.Identifier());
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

        public partial class ASPDepthOffsetShadowCleanUpPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
            }
        }
    }
}
#endif
