using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Unity 6 (URP 17+): primary rendering path is RenderGraph.
// Unity 2022: Compatibility Mode (CommandBuffer + RenderingData).
// The correct compile-time path is selected via UNITY_6000_0_OR_NEWER.
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ZLZ.AnimeShader
{
    [DisallowMultipleRendererFeature("ZLZ Hull Outline")]
    public class ZLZ_OutlineRendererFeature : ScriptableRendererFeature
    {
        OutlinePass _pass;

        public override void Create()
        {
            _pass = new OutlinePass();
            name  = "ZLZ Hull Outline";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;
            // Hidden cameras (e.g. planar reflection mirror cams spawned by a
            // render feature) need to be skipped too. Those usually set
            // GL.invertCulling for the mirror geometry, which flips Cull Front
            // on the hull outline pass and fills the reflected character with
            // a solid black hull. Owners of hidden cameras manage their own
            // outline strategy, so the standard outline pipeline opts out.
            // The Scene view IS allowed through — its camera object is hidden,
            // which the hideFlags test alone would wrongly reject.
            bool sceneView = camType == CameraType.SceneView;
            if (!sceneView && cam != null && cam.gameObject.hideFlags != HideFlags.None) return;
            renderer.EnqueuePass(_pass);
        }

        // ─────────────────────────────────────────────────────────────────────
        class OutlinePass : ScriptableRenderPass
        {
            static readonly ShaderTagId k_TagId = new ShaderTagId("ZLZOutline");

            public OutlinePass()
            {
                renderPassEvent  = RenderPassEvent.AfterRenderingOpaques;
                profilingSampler = new ProfilingSampler("ZLZ Hull Outline");
            }

            // =================================================================
            // UNITY 6 PATH — RenderGraph
            // =================================================================
#if UNITY_6000_0_OR_NEWER

            class PassData
            {
                public RendererListHandle rendererList;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData    = frameData.Get<UniversalCameraData>();
                var lightingData  = frameData.Get<UniversalLightData>();
                var resourceData  = frameData.Get<UniversalResourceData>();

                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    k_TagId, renderingData, cameraData, lightingData,
                    SortingCriteria.CommonOpaque);

                var filterSettings = new FilteringSettings(RenderQueueRange.all);
                var listParams     = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "ZLZ Hull Outline", out var passData, profilingSampler);

                passData.rendererList = renderGraph.CreateRendererList(listParams);

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    ctx.cmd.DrawRendererList(data.rendererList));
            }

#else
            // =================================================================
            // UNITY 2022 PATH — Compatibility Mode
            // =================================================================

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);   // flush BeginSample so the draws below group under it
                    cmd.Clear();

                    var drawSettings   = CreateDrawingSettings(k_TagId, ref renderingData, SortingCriteria.CommonOpaque);
                    var filterSettings = new FilteringSettings(RenderQueueRange.all);
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
                }
                context.ExecuteCommandBuffer(cmd);       // EndSample
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

#endif
        }
    }
}
