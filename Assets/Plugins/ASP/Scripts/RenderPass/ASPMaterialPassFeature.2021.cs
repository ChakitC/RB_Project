#if UNITY_2021
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPMaterialPassFeature
    {
        public partial class ASPMaterialPass
        {
            private RenderTargetHandle _materialTextureHandle;
            private RenderTargetHandle _depthTextureHandle;
            private RenderTextureDescriptor _depthTargetDesc;

            partial void InitVersionSpecific()
            {
                _materialTextureHandle.Init("_ASPMaterialTexture");
                _depthTextureHandle.Init("_ASPMaterialDepthTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.ARGB32;

                cmd.GetTemporaryRT(_materialTextureHandle.id, desc);

                _depthTargetDesc = desc;
                _depthTargetDesc.width = desc.width;
                _depthTargetDesc.height = desc.height;
                _depthTargetDesc.graphicsFormat = GraphicsFormat.None;
                _depthTargetDesc.msaaSamples = 1;
                _depthTargetDesc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                cmd.GetTemporaryRT(_depthTextureHandle.id, _depthTargetDesc);

                ConfigureTarget(_materialTextureHandle.Identifier(), _depthTextureHandle.Identifier());
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(_materialTextureHandle.id);
                cmd.ReleaseTemporaryRT(_depthTextureHandle.id);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
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
                cmd.SetGlobalTexture(ASPShaderIDs.ASPMaterialTexture, _materialTextureHandle.Identifier());
                cmd.SetGlobalTexture(ASPShaderIDs.ASPMaterialDepthTexture, _depthTextureHandle.Identifier());
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

    }
}
#endif
