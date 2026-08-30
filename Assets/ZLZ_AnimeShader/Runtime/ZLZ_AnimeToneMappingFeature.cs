using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Unity 6 (URP 17+): primary rendering path is RenderGraph.
// Unity 2022: Compatibility Mode (CommandBuffer + RenderingData).
// The correct compile-time path is selected via UNITY_6000_0_OR_NEWER.
// Shader behavior, property values, and keywords are identical on both paths.
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ZLZ.AnimeShader
{
    [DisallowMultipleRendererFeature("ZLZ Anime ToneMapping")]
    public class ZLZ_AnimeToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private Material m_material;
        private ZLZAnimeToneMappingPass m_pass;

        // ── Shader keyword ────────────────────────────────────────────
        private const string KW_FILMIC = "ZLZ_CURVE_FILMIC";

        // ── Cached Property IDs ───────────────────────────────────────
        private static readonly int ID_BaseMap            = Shader.PropertyToID("_BaseMap");
        private static readonly int ID_BlitScaleBias      = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int ID_LutParams          = Shader.PropertyToID("_Lut_Params");
        // shared
        private static readonly int ID_Exposure           = Shader.PropertyToID("_Exposure");
        private static readonly int ID_ToneMapScale       = Shader.PropertyToID("_ToneMapScale");
        // anime curve
        private static readonly int ID_HighlightRolloff   = Shader.PropertyToID("_HighlightRolloff");
        private static readonly int ID_HighlightLift      = Shader.PropertyToID("_HighlightLift");
        private static readonly int ID_WhiteClip          = Shader.PropertyToID("_WhiteClip");
        private static readonly int ID_ShadowPedestal     = Shader.PropertyToID("_ShadowPedestal");
        private static readonly int ID_MidContrast        = Shader.PropertyToID("_MidContrast");
        private static readonly int ID_SatRetention       = Shader.PropertyToID("_SaturationRetention");
        // filmic curve
        private static readonly int ID_ShoulderStrength   = Shader.PropertyToID("_ShoulderStrength");
        private static readonly int ID_LinearStrength     = Shader.PropertyToID("_LinearStrength");
        private static readonly int ID_LinearAngle        = Shader.PropertyToID("_LinearAngle");
        private static readonly int ID_ToeStrength        = Shader.PropertyToID("_ToeStrength");
        private static readonly int ID_BlackLevel         = Shader.PropertyToID("_BlackLevel");
        private static readonly int ID_DenomBalance       = Shader.PropertyToID("_DenominatorBalance");
        private static readonly int ID_WhitePoint         = Shader.PropertyToID("_WhitePoint");
        private static readonly int ID_FilmicSaturation   = Shader.PropertyToID("_FilmicSaturation");
        private static readonly int ID_FilmicExposure     = Shader.PropertyToID("_FilmicExposure");
        private static readonly int ID_FilmicToneMapScale = Shader.PropertyToID("_FilmicToneMapScale");

        // ── Feature lifecycle ─────────────────────────────────────────

        public override void Create()
        {
            EnsureMaterial();
            m_pass = new ZLZAnimeToneMappingPass(name);
        }

        private bool EnsureMaterial()
        {
            if (m_material != null) return true;
            var shader = Shader.Find("ZLZ/AnimeToneMapping");
            if (shader == null) return false;
            m_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;
            if (!EnsureMaterial()) return;

            m_pass.renderPassEvent = InjectionPoint;
            m_pass.ConfigureInput(ScriptableRenderPassInput.None);
            m_pass.SetupMembers(m_material);
            renderer.EnqueuePass(m_pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_pass?.Dispose();
            if (m_material != null) { CoreUtils.Destroy(m_material); m_material = null; }
        }

        // ─────────────────────────────────────────────────────────────
        private class ZLZAnimeToneMappingPass : ScriptableRenderPass
        {
            private Material m_mat;

            public ZLZAnimeToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material mat) => m_mat = mat;

            // =================================================================
            // UNITY 6 PATH  —  RenderGraph (two RasterPasses, no warnings)
            // =================================================================
            //
            // Pass 1 "Copy"    : blit activeColorTexture → a temporary texture
            //                    using Blitter.BlitTexture (no custom shader).
            // Pass 2 "ToneMap" : read the temporary texture, apply the
            //                    ZLZ tone-mapping shader, write back to
            //                    activeColorTexture.
            //
            // All shader property values are copied into PassData as plain value
            // types before the render func runs. This avoids capturing managed
            // references (VolumeComponent, Material) inside static lambdas, which
            // would break RenderGraph's ability to defer and reorder passes.
            // =================================================================
#if UNITY_6000_0_OR_NEWER

            // -- Pass 1: plain color copy, no shader parameters needed.
            private class CopyPassData
            {
                public TextureHandle source;
            }

            // -- Pass 2: all data the tone-map shader needs, as value types.
            private class ToneMapPassData
            {
                public TextureHandle source;
                public TextureHandle destination;
                public Material      mat;
                // LUT
                public int   lutHeight;
                public int   lutWidth;
                // curve selection
                public bool  isFilmicMode;
                // anime curve
                public float exposure,      toneMapScale;
                public float highlightRolloff, highlightLift, whiteClip;
                public float shadowPedestal, midContrast,   satRetention;
                // filmic curve
                public float filmicExposure,     filmicToneMapScale;
                public float shoulderStrength,   linearStrength, linearAngle;
                public float toeStrength,        blackLevel,     denomBalance;
                public float whitePoint,         filmicSaturation;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_mat == null) return;

                var comp = VolumeManager.instance.stack.GetComponent<ZLZ_AnimeToneMap>();
                if (comp == null || !comp.IsActive()) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData   = frameData.Get<UniversalCameraData>();
                var ppData       = frameData.Get<UniversalPostProcessingData>();

                // Allocate a temporary texture for the intermediate copy.
                // UniversalRenderer.CreateRenderGraphTexture lets RenderGraph
                // manage the texture lifetime automatically.
                var copyDesc = cameraData.cameraTargetDescriptor;
                copyDesc.msaaSamples     = 1;
                copyDesc.depthBufferBits = 0;
                TextureHandle copyHandle = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, copyDesc, "_ZLZToneBuffer", false);

                // ── Pass 1: copy active color → copyHandle ──────────────────
                using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(
                    "ZLZ ToneMapping Copy", out var copyData, profilingSampler))
                {
                    copyData.source = resourceData.activeColorTexture;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(copyHandle, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext ctx) =>
                    {
                        // Full-screen blit with no mip bias, keeping the existing
                        // color data unchanged — equivalent to the legacy BlitCameraTexture.
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f),
                            0.0f, false);
                    });
                }

                // ── Pass 2: apply tone-mapping, write back to activeColorTexture ─
                using (var builder = renderGraph.AddRasterRenderPass<ToneMapPassData>(
                    "ZLZ ToneMapping Apply", out var toneData, profilingSampler))
                {
                    // Copy all values from the volume component into PassData.
                    // Plain structs / value types only — no managed object references
                    // stored on PassData to avoid deferred-execution pitfalls.
                    int lutH = ppData.lutSize;
                    toneData.source           = copyHandle;
                    toneData.destination      = resourceData.activeColorTexture;
                    toneData.mat              = m_mat;
                    toneData.lutHeight        = lutH;
                    toneData.lutWidth         = lutH * lutH;
                    toneData.isFilmicMode     = comp.IsFilmicMode;
                    toneData.exposure         = comp.Exposure.value;
                    toneData.toneMapScale     = comp.ToneMapScale.value;
                    toneData.highlightRolloff = comp.HighlightRolloff.value;
                    toneData.highlightLift    = comp.HighlightLift.value;
                    toneData.whiteClip        = comp.WhiteClip.value;
                    toneData.shadowPedestal   = comp.ShadowPedestal.value;
                    toneData.midContrast      = comp.MidContrast.value;
                    toneData.satRetention     = comp.SaturationRetention.value;
                    toneData.filmicExposure      = comp.FilmicExposure.value;
                    toneData.filmicToneMapScale  = comp.FilmicToneMapScale.value;
                    toneData.shoulderStrength    = comp.ShoulderStrength.value;
                    toneData.linearStrength      = comp.LinearStrength.value;
                    toneData.linearAngle         = comp.LinearAngle.value;
                    toneData.toeStrength         = comp.ToeStrength.value;
                    toneData.blackLevel          = comp.BlackLevel.value;
                    toneData.denomBalance        = comp.DenominatorBalance.value;
                    toneData.whitePoint          = comp.WhitePoint.value;
                    toneData.filmicSaturation    = comp.FilmicSaturation.value;

                    builder.UseTexture(copyHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (ToneMapPassData data, RasterGraphContext ctx) =>
                    {
                        var mat = data.mat;

                        // LUT params
                        mat.SetVector(ID_LutParams,
                            new Vector4(1f / data.lutWidth, 1f / data.lutHeight,
                                        data.lutHeight - 1f, 0f));

                        // Curve mode keyword
                        if (data.isFilmicMode) mat.EnableKeyword(KW_FILMIC);
                        else                   mat.DisableKeyword(KW_FILMIC);

                        // Anime curve properties
                        mat.SetFloat(ID_Exposure,         data.exposure);
                        mat.SetFloat(ID_ToneMapScale,     data.toneMapScale);
                        mat.SetFloat(ID_HighlightRolloff, data.highlightRolloff);
                        mat.SetFloat(ID_HighlightLift,    data.highlightLift);
                        mat.SetFloat(ID_WhiteClip,        data.whiteClip);
                        mat.SetFloat(ID_ShadowPedestal,   data.shadowPedestal);
                        mat.SetFloat(ID_MidContrast,      data.midContrast);
                        mat.SetFloat(ID_SatRetention,     data.satRetention);

                        // Filmic curve properties
                        mat.SetFloat(ID_FilmicExposure,     data.filmicExposure);
                        mat.SetFloat(ID_FilmicToneMapScale, data.filmicToneMapScale);
                        mat.SetFloat(ID_ShoulderStrength,   data.shoulderStrength);
                        mat.SetFloat(ID_LinearStrength,     data.linearStrength);
                        mat.SetFloat(ID_LinearAngle,        data.linearAngle);
                        mat.SetFloat(ID_ToeStrength,        data.toeStrength);
                        mat.SetFloat(ID_BlackLevel,         data.blackLevel);
                        mat.SetFloat(ID_DenomBalance,       data.denomBalance);
                        mat.SetFloat(ID_WhitePoint,         data.whitePoint);
                        mat.SetFloat(ID_FilmicSaturation,   data.filmicSaturation);

                        // Scale bias: RasterPass always renders to the full attachment
                        // so (1,1,0,0) is correct — no RTHandle scaling needed here
                        // because CreateRenderGraphTexture matches the camera resolution.
                        mat.SetVector(ID_BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                        mat.SetTexture(ID_BaseMap, data.source);

                        ctx.cmd.DrawProcedural(Matrix4x4.identity, mat, 0,
                            MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            // RenderGraph manages texture lifetimes; no RTHandle to release.
            public void Dispose() { }

#else
            // =================================================================
            // UNITY 2022 PATH  —  Compatibility Mode (original, unchanged)
            // =================================================================

            private RTHandle m_buffer;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.msaaSamples     = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_buffer, desc, name: "_ZLZToneBuffer");
                ConfigureTarget(m_buffer);
                ConfigureClear(ClearFlag.Color, Color.black);
            }

            public void Dispose() => m_buffer?.Release();

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_mat == null) return;

                var comp = VolumeManager.instance.stack.GetComponent<ZLZ_AnimeToneMap>();
                if (comp == null || !comp.IsActive()) return;

                ref var cameraData = ref renderingData.cameraData;
                ref var ppData     = ref renderingData.postProcessingData;

                int lutH = ppData.lutSize;
                int lutW = lutH * lutH;
                m_mat.SetVector(ID_LutParams, new Vector4(1f / lutW, 1f / lutH, lutH - 1f, 0f));

                if (comp.IsFilmicMode) m_mat.EnableKeyword(KW_FILMIC);
                else                   m_mat.DisableKeyword(KW_FILMIC);

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    CoreUtils.SetRenderTarget(cmd, m_buffer);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_buffer);

                    SetAllProperties(comp);

                    Vector2 scale = m_buffer.useScaling
                        ? new Vector2(m_buffer.rtHandleProperties.rtHandleScale.x,
                                      m_buffer.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;
                    m_mat.SetVector(ID_BlitScaleBias, new Vector4(scale.x, scale.y, 0f, 0f));
                    m_mat.SetTexture(ID_BaseMap, m_buffer);

                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    cmd.DrawProcedural(Matrix4x4.identity, m_mat, 0, MeshTopology.Triangles, 3, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            private void SetAllProperties(ZLZ_AnimeToneMap c)
            {
                m_mat.SetFloat(ID_Exposure,           c.Exposure.value);
                m_mat.SetFloat(ID_ToneMapScale,       c.ToneMapScale.value);
                m_mat.SetFloat(ID_FilmicExposure,     c.FilmicExposure.value);
                m_mat.SetFloat(ID_FilmicToneMapScale, c.FilmicToneMapScale.value);
                m_mat.SetFloat(ID_HighlightRolloff,   c.HighlightRolloff.value);
                m_mat.SetFloat(ID_HighlightLift,      c.HighlightLift.value);
                m_mat.SetFloat(ID_WhiteClip,          c.WhiteClip.value);
                m_mat.SetFloat(ID_ShadowPedestal,     c.ShadowPedestal.value);
                m_mat.SetFloat(ID_MidContrast,        c.MidContrast.value);
                m_mat.SetFloat(ID_SatRetention,       c.SaturationRetention.value);
                m_mat.SetFloat(ID_ShoulderStrength,   c.ShoulderStrength.value);
                m_mat.SetFloat(ID_LinearStrength,     c.LinearStrength.value);
                m_mat.SetFloat(ID_LinearAngle,        c.LinearAngle.value);
                m_mat.SetFloat(ID_ToeStrength,        c.ToeStrength.value);
                m_mat.SetFloat(ID_BlackLevel,         c.BlackLevel.value);
                m_mat.SetFloat(ID_DenomBalance,       c.DenominatorBalance.value);
                m_mat.SetFloat(ID_WhitePoint,         c.WhitePoint.value);
                m_mat.SetFloat(ID_FilmicSaturation,   c.FilmicSaturation.value);
            }

#endif // UNITY_6000_0_OR_NEWER
        }
    }
}