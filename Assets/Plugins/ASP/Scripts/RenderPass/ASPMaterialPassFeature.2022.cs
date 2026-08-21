#if UNITY_2022_1_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPMaterialPassFeature
    {
        public partial class ASPMaterialPass
        {
            private RTHandle _materialPassTarget;
            private RTHandle _depthTarget;

            partial void DisposeVersionSpecific()
            {
                _materialPassTarget?.Release();
                _depthTarget?.Release();
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _materialPassTarget, desc, name: "_ASPMaterialTexture");

                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                desc.colorFormat = RenderTextureFormat.Depth;
                RenderingUtils.ReAllocateIfNeeded(ref _depthTarget, desc);

                ConfigureTarget(_materialPassTarget, _depthTarget);
                ConfigureClear(ClearFlag.All, Color.black);
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
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
                cmd.SetGlobalTexture(ASPShaderIDs.ASPMaterialTexture, _materialPassTarget);
                cmd.SetGlobalTexture(ASPShaderIDs.ASPMaterialDepthTexture, _depthTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

    }
}
#endif
