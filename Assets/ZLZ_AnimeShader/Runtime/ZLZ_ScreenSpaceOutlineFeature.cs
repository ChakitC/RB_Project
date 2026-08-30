using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

// Unity 6 (URP 17+): primary rendering path is RenderGraph.
// Unity 2022: Compatibility Mode (CommandBuffer + RenderingData).
// The correct compile-time path is selected via UNITY_6000_0_OR_NEWER.
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ZLZ.AnimeShader
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Screen Space Outline (character-only)
    //
    // Adds INTERIOR detail lines (creases, overlapping parts) that the per-object
    // inverted-hull outline cannot produce. Two steps, one pass:
    //   1) Render the character layers' "DepthNormals" pass into a private buffer
    //      (world-space normals + camera-view depth) — so only ZLZ characters are
    //      considered, the environment is never touched.
    //   2) A fullscreen overlay runs Roberts-cross edge detection on that buffer and
    //      blends the line over the camera color, optionally hiding it where the
    //      character is occluded by scene geometry.
    //
    // PC / console feature: it adds a character-depth-normals render + a fullscreen
    // pass, so it is opt-in and meant to be stripped for mobile / VR builds.
    // ─────────────────────────────────────────────────────────────────────────────
    [DisallowMultipleRendererFeature("ZLZ Screen Space Outline")]
    public class ZLZ_ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        // Full-screen single-channel visualization — an editor tuning aid so each
        // slider's effect is directly observable. Set to Off for normal rendering.
        public enum DebugView { Off, Coverage, NormalEdge, DepthEdge, CombinedEdge, DistanceFade, Occlusion }

        [Tooltip("Which layers are treated as characters. Set this to your character layer(s). " +
                 "The Character Dashboard's Setup assigns this automatically.")]
        public LayerMask characterLayers = 0;

        [Header("Line")]
        public Color outlineColor = Color.black;
        [Tooltip("Line width in pixels.")]
        [Range(0.5f, 4f)] public float thickness = 0.5f;
        [Range(0f, 1f)]   public float intensity = 0.75f;

        [Header("Edge Sources")]
        [Tooltip("Detect lines where surface orientation changes sharply (creases, folds).")]
        public bool normalEdge = true;
        [Range(0f, 1f)] public float normalSensitivity = 0.5f;
        [Tooltip("Detect lines where one part of the character overlaps another (depth gap).")]
        public bool depthEdge = true;
        [Range(0f, 1f)] public float depthSensitivity = 0.5f;

        [Header("Occlusion")]
        [Tooltip("Hide the line where the character is hidden behind scene geometry.")]
        public bool occlusionTest = true;
        [Range(0f, 0.2f)] public float occlusionBias = 0.02f;

        [Header("Distance Fade")]
        [Tooltip("Fade the outline out as the character moves away from the camera — keeps distant characters clean. Recommended for gameplay cameras.")]
        public bool distanceFade = true;
        [Tooltip("Fade range in metres: x = distance where fading starts, y = distance where the line is fully faded.")]
        public Vector2 fadeDistance = new Vector2(10f, 30f);
        [Tooltip("Line intensity at and beyond the far distance (0 = invisible).")]
        [Range(0f, 1f)] public float farIntensity = 0f;

        [Header("Debug")]
        [Tooltip("Visualize one channel full-screen to tune the sliders. Off = normal rendering. " +
                 "Each mode is masked to the character; enable the matching edge/feature to see it.")]
        public DebugView debugView = DebugView.Off;

        [Header("Injection")]
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

        Material _material;
        OutlinePass _pass;

        public override void Create()
        {
            EnsureMaterial();
            _pass = new OutlinePass();
            name  = "ZLZ Screen Space Outline";
        }

        bool EnsureMaterial()
        {
            if (_material != null) return true;
            var shader = Shader.Find("ZLZ/ScreenSpaceOutline");
            if (shader == null) return false;
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;
            // Hidden cameras (e.g. planar reflection mirror cams spawned by a
            // render feature) are owned by their spawner, which decides what
            // outline strategy to apply. The standard SSO pass opts out so
            // off-screen mirror renders do not pay the depth/normal sample cost
            // or fight with the host feature's culling state.
            // The Scene view IS allowed through — its camera object is hidden,
            // which the hideFlags test alone would wrongly reject.
            bool sceneView = camType == CameraType.SceneView;
            if (!sceneView && cam != null && cam.gameObject.hideFlags != HideFlags.None) return;
            if (!EnsureMaterial()) return;

            _pass.renderPassEvent = injectionPoint;
            // Scene depth is only needed for the occlusion test.
            _pass.ConfigureInput(occlusionTest ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            _pass.Setup(_material, characterLayers, BuildSettings());
            renderer.EnqueuePass(_pass);
        }

        OutlinePass.Settings BuildSettings() => new OutlinePass.Settings
        {
            color             = outlineColor,
            thickness         = thickness,
            intensity         = intensity,
            normalEdge        = normalEdge,
            normalSensitivity = normalSensitivity,
            depthEdge         = depthEdge,
            depthSensitivity  = depthSensitivity,
            occlusionTest     = occlusionTest,
            occlusionBias     = occlusionBias,
            distanceFade      = distanceFade,
            fadeDistance      = fadeDistance,
            farIntensity      = farIntensity,
            debugOn           = debugView != DebugView.Off,
            debugMode         = (int)debugView - 1,
        };

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            if (_material != null) { CoreUtils.Destroy(_material); _material = null; }
        }

        // ═════════════════════════════════════════════════════════════════════════
        class OutlinePass : ScriptableRenderPass
        {
            public struct Settings
            {
                public Color color;
                public float thickness, intensity;
                public bool  normalEdge;  public float normalSensitivity;
                public bool  depthEdge;   public float depthSensitivity;
                public bool  occlusionTest; public float occlusionBias;
                public bool  distanceFade; public Vector2 fadeDistance; public float farIntensity;
                public bool  debugOn; public float debugMode;
            }

            static readonly ShaderTagId k_DepthNormals = new ShaderTagId("DepthNormals");

            static readonly int ID_Normals  = Shader.PropertyToID("_ZLZSSO_Normals");
            static readonly int ID_Depth    = Shader.PropertyToID("_ZLZSSO_Depth");
            static readonly int ID_Color    = Shader.PropertyToID("_ZLZSSO_Color");
            static readonly int ID_Params   = Shader.PropertyToID("_ZLZSSO_Params");
            static readonly int ID_Params2  = Shader.PropertyToID("_ZLZSSO_Params2");
            static readonly int ID_FadeParams = Shader.PropertyToID("_ZLZSSO_FadeParams");
            static readonly int ID_DebugMode = Shader.PropertyToID("_ZLZSSO_DebugMode");
            static readonly int ID_ScaleBias = Shader.PropertyToID("_BlitScaleBias");

            const string KW_DEPTH     = "ZLZ_SSO_DEPTH_EDGE";
            const string KW_NORMAL    = "ZLZ_SSO_NORMAL_EDGE";
            const string KW_OCCLUSION = "ZLZ_SSO_OCCLUSION";
            const string KW_FADE      = "ZLZ_SSO_DISTANCE_FADE";
            const string KW_DEBUG     = "ZLZ_SSO_DEBUG";

            Material  _mat;
            LayerMask _layers = ~0;
            Settings  _s;

            public OutlinePass()
            {
                profilingSampler = new ProfilingSampler("ZLZ Screen Space Outline");
            }

            public void Setup(Material mat, LayerMask layers, Settings s)
            {
                _mat = mat; _layers = layers; _s = s;
            }

            void ApplyMaterial()
            {
                _mat.SetColor(ID_Color, _s.color);
                _mat.SetVector(ID_Params,  new Vector4(_s.thickness, _s.depthSensitivity, _s.normalSensitivity, _s.intensity));
                _mat.SetVector(ID_Params2, new Vector4(_s.occlusionBias, 0f, 0f, 0f));
                _mat.SetVector(ID_FadeParams, new Vector4(_s.fadeDistance.x, _s.fadeDistance.y, _s.farIntensity, 0f));
                _mat.SetFloat(ID_DebugMode, _s.debugMode);
                CoreUtils.SetKeyword(_mat, KW_NORMAL,    _s.normalEdge);
                CoreUtils.SetKeyword(_mat, KW_DEPTH,     _s.depthEdge);
                CoreUtils.SetKeyword(_mat, KW_OCCLUSION, _s.occlusionTest);
                CoreUtils.SetKeyword(_mat, KW_FADE,      _s.distanceFade);
                CoreUtils.SetKeyword(_mat, KW_DEBUG,     _s.debugOn);
            }

#if UNITY_6000_0_OR_NEWER
            // ═════════════════════════════════════════════════════════════════════
            // UNITY 6 — RenderGraph
            // ═════════════════════════════════════════════════════════════════════
            class CaptureData
            {
                public RendererListHandle list;
            }

            class ComposeData
            {
                public Material      mat;
                public TextureHandle normals;
                public TextureHandle depth;
                public Vector4 color, prm, prm2, fadeParams;
                public bool normalEdge, depthEdge, occlusion, distanceFade, debugOn;
                public float debugMode;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData    = frameData.Get<UniversalCameraData>();
                var lightData     = frameData.Get<UniversalLightData>();
                var resourceData  = frameData.Get<UniversalResourceData>();

                // ── Character-only normals + depth buffers ───────────────────
                var nDesc = cameraData.cameraTargetDescriptor;
                nDesc.msaaSamples        = 1;
                nDesc.graphicsFormat     = GraphicsFormat.R16G16B16A16_SFloat;
                nDesc.depthStencilFormat = GraphicsFormat.None;
                nDesc.depthBufferBits    = 0;
                // clear: true -> RGB cleared to 0, so the coverage test (length of xyz) reads
                // "no character" everywhere the DepthNormals draw does not write.
                TextureHandle normals = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, nDesc, "_ZLZSSO_Normals", true);

                var dDesc = cameraData.cameraTargetDescriptor;
                dDesc.msaaSamples        = 1;
                dDesc.graphicsFormat     = GraphicsFormat.None;
                dDesc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                TextureHandle depth = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, dDesc, "_ZLZSSO_Depth", true);

                // ── Pass 1: character DepthNormals capture ───────────────────
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    k_DepthNormals, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _layers);
                var listParams     = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

                using (var builder = renderGraph.AddRasterRenderPass<CaptureData>(
                    "ZLZ SSO Capture", out var data, profilingSampler))
                {
                    data.list = renderGraph.CreateRendererList(listParams);
                    builder.UseRendererList(data.list);
                    builder.SetRenderAttachment(normals, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetRenderFunc(static (CaptureData d, RasterGraphContext ctx) =>
                        ctx.cmd.DrawRendererList(d.list));
                }

                // ── Pass 2: edge detect + composite over active color ────────
                using (var builder = renderGraph.AddRasterRenderPass<ComposeData>(
                    "ZLZ SSO Compose", out var data, profilingSampler))
                {
                    data.mat        = _mat;
                    data.normals    = normals;
                    data.depth      = depth;
                    data.color      = _s.color;
                    data.prm        = new Vector4(_s.thickness, _s.depthSensitivity, _s.normalSensitivity, _s.intensity);
                    data.prm2       = new Vector4(_s.occlusionBias, 0f, 0f, 0f);
                    data.normalEdge = _s.normalEdge;
                    data.depthEdge  = _s.depthEdge;
                    data.occlusion  = _s.occlusionTest;
                    data.distanceFade = _s.distanceFade;
                    data.fadeParams = new Vector4(_s.fadeDistance.x, _s.fadeDistance.y, _s.farIntensity, 0f);
                    data.debugOn    = _s.debugOn;
                    data.debugMode  = _s.debugMode;

                    builder.UseTexture(normals, AccessFlags.Read);
                    builder.UseTexture(depth,   AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    // SampleSceneDepth reads the _CameraDepthTexture global (requested via ConfigureInput).
                    if (_s.occlusionTest) builder.UseAllGlobalTextures(true);

                    builder.SetRenderFunc(static (ComposeData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetColor(ID_Color,   d.color);
                        d.mat.SetVector(ID_Params,  d.prm);
                        d.mat.SetVector(ID_Params2, d.prm2);
                        d.mat.SetVector(ID_ScaleBias, new Vector4(1f, 1f, 0f, 0f)); // full attachment in RG
                        d.mat.SetTexture(ID_Normals, d.normals);
                        d.mat.SetTexture(ID_Depth,   d.depth);
                        d.mat.SetVector(ID_FadeParams, d.fadeParams);
                        d.mat.SetFloat(ID_DebugMode, d.debugMode);
                        CoreUtils.SetKeyword(d.mat, KW_NORMAL,    d.normalEdge);
                        CoreUtils.SetKeyword(d.mat, KW_DEPTH,     d.depthEdge);
                        CoreUtils.SetKeyword(d.mat, KW_OCCLUSION, d.occlusion);
                        CoreUtils.SetKeyword(d.mat, KW_FADE,      d.distanceFade);
                        CoreUtils.SetKeyword(d.mat, KW_DEBUG,     d.debugOn);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, d.mat, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            public void Dispose() { }
#else
            // ═════════════════════════════════════════════════════════════════════
            // UNITY 2022 — Compatibility Mode
            // ═════════════════════════════════════════════════════════════════════
            RTHandle m_normalsRT;
            RTHandle m_depthRT;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;

                // Normals: signed values -> fp16 (broadly supported, incl. WebGL2).
                var nDesc = desc;
                nDesc.graphicsFormat     = GraphicsFormat.R16G16B16A16_SFloat;
                nDesc.depthStencilFormat = GraphicsFormat.None;
                nDesc.depthBufferBits    = 0;
                RenderingUtils.ReAllocateIfNeeded(ref m_normalsRT, nDesc,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_ZLZSSO_Normals");

                // Depth target for the character DepthNormals draw.
                var dDesc = desc;
                dDesc.colorFormat     = RenderTextureFormat.Depth;   // sets graphicsFormat -> None internally
                dDesc.depthBufferBits = 32;
                dDesc.useMipMap       = false;
                dDesc.bindMS          = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_depthRT, dDesc,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_ZLZSSO_Depth");

                ConfigureTarget(m_normalsRT, m_depthRT);
                ConfigureClear(ClearFlag.All, Color.clear);
            }

            public void Dispose()
            {
                m_normalsRT?.Release();
                m_depthRT?.Release();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_mat == null) return;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Target (m_normalsRT + m_depthRT) is bound + cleared by ConfigureTarget/Clear.
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // ── Step 1: character-only DepthNormals capture ──────────────
                    var drawSettings   = CreateDrawingSettings(k_DepthNormals, ref renderingData, SortingCriteria.CommonOpaque);
                    var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _layers);
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

                    // ── Step 2: fullscreen edge detect + composite ───────────────
                    ApplyMaterial();
                    cmd.SetGlobalTexture(ID_Normals, m_normalsRT);
                    cmd.SetGlobalTexture(ID_Depth,   m_depthRT);

                    Vector2 scale = m_normalsRT.useScaling
                        ? new Vector2(m_normalsRT.rtHandleProperties.rtHandleScale.x,
                                      m_normalsRT.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;
                    _mat.SetVector(ID_ScaleBias, new Vector4(scale.x, scale.y, 0f, 0f));

                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    cmd.DrawProcedural(Matrix4x4.identity, _mat, 0, MeshTopology.Triangles, 3, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#endif // UNITY_6000_0_OR_NEWER
        }
    }
}
