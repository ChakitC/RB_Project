using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    public class InjectRenderPassFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("mask")]
        [FormerlySerializedAs("m_mask")]
        public LayerMask Mask;

        // [RenderingLayerMask]
        private int _renderingLayerMask;
        public RenderPassEvent Event;
        public RenderQueueRange Range = RenderQueueRange.opaque;
        public string InjectPassLightModeTag = "MyCustomLightModeTag";
        private InjectCustomPass _injectPass;

        public override void Create()
        {
            _injectPass = new InjectCustomPass(name, InjectPassLightModeTag, Event, Range, (uint)_renderingLayerMask,
                Mask, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_injectPass);
        }

        public class InjectCustomPass : ScriptableRenderPass
        {
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId;
            private string _profilerTag;

            public InjectCustomPass(string profilerTag, string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, LayerMask layerMask,
                StencilState stencilState, int stencilReference)
            {
                _profilerTag = profilerTag;
                renderPassEvent = evt;
                _filteringSettings = new FilteringSettings(renderQueueRange);
                _filteringSettings.layerMask = layerMask.value;
                //_filteringSettings.renderingLayerMask = renderingLayerMask;
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _shaderTagId = new ShaderTagId(shaderTagId);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("Inject Render Pass")))
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
        }
    }
}
