#if UNITY_2022_1_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPDepthOffsetShadowFeature
    {
        public partial class ASPDepthOffsetShadowPass
        {
            private RTHandle _depthTarget;

            partial void DisposeVersionSpecific()
            {
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
                desc.colorFormat = RenderTextureFormat.Depth;
                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                RenderingUtils.ReAllocateIfNeeded(ref _depthTarget, desc, name: "_ASPDepthOffsetShadowTexture");

                ConfigureTarget(_depthTarget);
                ConfigureClear(ClearFlag.Depth, Color.black);
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
                cmd.SetGlobalTexture(ASPShaderIDs.ASPDepthOffsetShadowTexture, _depthTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

        public partial class ASPDepthOffsetShadowCleanUpPass
        {
#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
            }
        }
    }
}
#endif
