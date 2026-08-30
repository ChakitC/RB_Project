using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Unity 6 (URP 17+): primary path is RenderGraph.  Unity 2022: Compatibility Mode.
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Experimental.Rendering;          // GraphicsFormat (RenderGraph path only)
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ZLZ.AnimeShader
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Character Contact Shadow — capture (D1)
    //
    // Renders the character into a CAMERA-VIEW depth texture (`_ZLZCharDepthTex`). The
    // character shader then marches in screen space toward the light and compares this depth
    // to decide "is something (e.g. hair) in front of me toward the light?" → a crisp,
    // screen-space contact self-shadow (hair→face, arm→body). Written from scratch for ZLZ.
    //
    // Far simpler than the light-space shadow map: it uses the CAMERA's matrices (no light
    // projection / cascades / cull juggling) — it's just a character-only depth prepass.
    // Draws the character's dedicated "ZLZContactShadow" pass (a stripped, alpha-clip-only depth
    // pass) so all character materials share one variant → the SRP Batcher collapses the capture
    // into ~1 batch instead of the many variants the full DepthOnly pass produces.
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleRendererFeature("ZLZ Character Contact Shadow")]
    public class ZLZ_CharacterContactShadowFeature : ScriptableRendererFeature
    {
        // Depth texture downscale — the enum value IS the divisor (Full=1, Half=2, Quarter=4, Eighth=8).
        public enum DepthResolution { Full = 1, Half = 2, Quarter = 4, Eighth = 8 }

        [Tooltip("Which layers are rendered into the character depth texture. Set this to your character layer(s).")]
        public LayerMask casterLayers = ~0;

        [Tooltip("Depth texture resolution. Lower = much less memory & bandwidth (Half = 1/4, Quarter = 1/16, Eighth = 1/64) at the cost of softer / blockier contact edges. Lower this for mobile / low-end; keep Full on PC.")]
        public DepthResolution resolution = DepthResolution.Full;

        [Header("Contact Shadow")]
        [Tooltip("Contact distance toward the light, in screen-UV at 1 m (perspective-corrected: it auto-shrinks as the character gets farther). Bigger = longer contact shadow.")]
        [Range(0f, 0.15f)] public float offset = 0.0045f;
        [Tooltip("Depth bias (metres) — raise to remove speckle where a surface shadows itself.")]
        [Range(0f, 0.1f)]  public float depthBias = 0.01f;
        [Tooltip("Max contact range (metres). Only occluders closer than this cast the contact shadow (keeps it short-range, like hair→face).")]
        [Range(0.01f, 2f)] public float maxRange = 0.3f;
        [Range(0f, 1f)]    public float strength = 1f;

        ContactShadowPass _pass;

        public override void Create()
        {
            _pass = new ContactShadowPass();
            name  = "ZLZ Character Contact Shadow";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;
            // Hidden cameras (e.g. planar reflection mirror cams spawned by a
            // render feature) own their own shadow strategy and should not pay
            // for a depth-driven contact shadow pass that is invisible in the
            // mirror anyway.
            // The Scene view IS allowed through — its camera object is hidden,
            // which the hideFlags test alone would wrongly reject.
            bool sceneView = camType == CameraType.SceneView;
            if (!sceneView && cam != null && cam.gameObject.hideFlags != HideFlags.None) return;

            _pass.Setup(casterLayers, new Vector4(offset, depthBias, maxRange, strength), (int)resolution);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        // ═════════════════════════════════════════════════════════════════════════
        class ContactShadowPass : ScriptableRenderPass
        {
            // Dedicated stripped depth pass on the character shader (camera-view depth, alpha-clip
            // only). Using this instead of the full "DepthOnly" pass keeps every character material
            // on one shader variant, so the SRP Batcher collapses the capture into ~1 batch.
            static readonly ShaderTagId k_ContactShadow = new ShaderTagId("ZLZContactShadow");
            static readonly int ID_DepthTex = Shader.PropertyToID("_ZLZCharDepthTex");
            static readonly int ID_Params   = Shader.PropertyToID("_ZLZContactShadowParams");

            LayerMask _casterLayers = ~0;
            Vector4   _params = new Vector4(0.0045f, 0.01f, 0.3f, 1f);
            int       _scale  = 1;

            public ContactShadowPass()
            {
                renderPassEvent  = RenderPassEvent.BeforeRenderingOpaques;
                profilingSampler = new ProfilingSampler("ZLZ Character Contact Shadow");
            }

            public void Setup(LayerMask casterLayers, Vector4 prm, int scale) { _casterLayers = casterLayers; _params = prm; _scale = scale; }

#if UNITY_6000_0_OR_NEWER
            // ═════════════════════════════════════════════════════════════════════
            // UNITY 6 — RenderGraph
            // ═════════════════════════════════════════════════════════════════════
            // Capture the character into a camera-view depth texture and publish it as the global
            // `_ZLZCharDepthTex`, so the character ForwardLit pass can march toward the light against
            // it. Mirrors the ZLZ Screen Space Outline capture pass (renderer-list draw into a
            // dedicated target, then exposed as a global).
            class PassData
            {
                public RendererListHandle list;
                public Vector4            prm;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData    = frameData.Get<UniversalCameraData>();
                var lightData     = frameData.Get<UniversalLightData>();

                // Character-only depth target. Inherits the camera descriptor (incl. XR array layout
                // for VR single-pass, which SAMPLE_TEXTURE2D_X in the receiver needs), downscaled by
                // the resolution divisor for mobile / low-end.
                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples        = 1;
                desc.graphicsFormat     = GraphicsFormat.None;
                desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                desc.useMipMap          = false;
                desc.bindMS             = false;
                int s = Mathf.Max(1, _scale);                       // value = divisor (Full=1 … Eighth=8)
                desc.width  = Mathf.Max(1, desc.width  / s);
                desc.height = Mathf.Max(1, desc.height / s);
                TextureHandle depthTex = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_ZLZCharDepthTex", true);

                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    k_ContactShadow, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _casterLayers);
                var listParams     = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "ZLZ Character Contact Shadow", out var data, profilingSampler))
                {
                    data.list = renderGraph.CreateRendererList(listParams);
                    data.prm  = _params;
                    builder.UseRendererList(data.list);
                    builder.SetRenderAttachmentDepth(depthTex, AccessFlags.Write);

                    // Publish for URP's own forward pass (the consumer lives outside this graph) and
                    // keep the pass alive: nothing inside the graph reads the handle, so without this
                    // RenderGraph would cull the capture.
                    builder.SetGlobalTextureAfterPass(depthTex, ID_DepthTex);
                    builder.AllowGlobalStateModification(true);     // render func sets a global vector
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalVector(ID_Params, d.prm);
                        ctx.cmd.DrawRendererList(d.list);
                    });
                }
            }

            public void Dispose() { }
#else
            // ═════════════════════════════════════════════════════════════════════
            // UNITY 2022 — Compatibility Mode
            // ═════════════════════════════════════════════════════════════════════
            RTHandle m_depthRT;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Copy the camera target descriptor so we inherit its width/height AND its XR layout
                // (volumeDepth / vrUsage / dimension). This keeps the depth a Tex2DArray under VR
                // single-pass instanced, which is what SAMPLE_TEXTURE2D_X in the receiver expects.
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.colorFormat     = RenderTextureFormat.Depth;
                desc.depthBufferBits  = desc.depthBufferBits == 0 ? 32 : desc.depthBufferBits;
                desc.msaaSamples      = 1;
                desc.useMipMap        = false;
                desc.bindMS           = false;
                int s = Mathf.Max(1, _scale);                       // downscale for mobile / low-end (value = divisor)
                desc.width  = Mathf.Max(1, desc.width  / s);
                desc.height = Mathf.Max(1, desc.height / s);
                RenderingUtils.ReAllocateIfNeeded(ref m_depthRT, desc,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_ZLZCharDepthTex");

                ConfigureTarget(m_depthRT);
                ConfigureClear(ClearFlag.Depth, Color.black);
            }

            public void Dispose() => m_depthRT?.Release();

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Target is bound + cleared by ConfigureTarget/ConfigureClear. Render the
                    // character's ZLZContactShadow pass (camera view) into it — no light matrices needed.
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var drawSettings   = CreateDrawingSettings(k_ContactShadow, ref renderingData, SortingCriteria.CommonOpaque);
                    var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _casterLayers);
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

                    cmd.SetGlobalTexture(ID_DepthTex, m_depthRT);
                    cmd.SetGlobalVector(ID_Params, _params);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#endif // UNITY_6000_0_OR_NEWER
        }
    }
}
