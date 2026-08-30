using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ZLZ.AnimeShader
{
    [DisallowMultipleRendererFeature("ZLZ Selection Outline")]
    public class ZLZ_SelectionOutlineFeature : ScriptableRendererFeature
    {
        // ── Per-type appearance ───────────────────────────────────────────
        [System.Serializable]
        public class TypeSettings
        {
            [ColorUsage(showAlpha: true, hdr: true)]
            public Color outlineColor = Color.white;
            [Range(0f, 10f)]
            public float outlineWidth = 2f;
        }

        // ── Shared animation curves (used by all types) ───────────────────
        [System.Serializable]
        public class AnimationSettings
        {
            [Header("Intro")]
            [Min(0f)] public float introDuration = 0.75f;
            [Tooltip("Width multiplier (0 = no outline → 1 = full width)")]
            public AnimationCurve introWidthCurve      = new AnimationCurve(
                new Keyframe(0f,   0f, 4.927862f,  4.927862f),
                new Keyframe(0.5f, 2f, 0.3385504f, 0.3385504f),
                new Keyframe(1f,   1f, 0f,         0f));
            [Tooltip("Brightness multiplier (0 = dark → 1 = full color)")]
            public AnimationCurve introBrightnessCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f));

            [Header("Loop")]
            [Tooltip("Duration of one pulse cycle in seconds. 0 = constant.")]
            [Min(0f)] public float loopPeriod = 1.5f;
            public AnimationCurve loopWidthCurve = new AnimationCurve(
                new Keyframe(0f,   1f,    0f, 0f),
                new Keyframe(0.5f, 0.25f, 0f, 0f),
                new Keyframe(1f,   1f,    0f, 0f));
            public AnimationCurve loopBrightnessCurve = new AnimationCurve(
                new Keyframe(0f,   1f,    0f, 0f),
                new Keyframe(0.5f, 0.75f, 0f, 0f),
                new Keyframe(1f,   1f,    0f, 0f));

            [Header("Outro")]
            [Min(0f)] public float outroDuration = 0.2f;
            [Tooltip("Width multiplier (1 = full width → 0 = no outline)")]
            public AnimationCurve outroWidthCurve      = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
            [Tooltip("Brightness multiplier (1 = full color → 0 = dark)")]
            public AnimationCurve outroBrightnessCurve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        [System.Serializable]
        public class Settings
        {
            // Defaults captured from the tuned production preset so a fresh Setup matches
            // the intended look instead of plain white / width 2: ally = cyan-blue,
            // enemy = red, item = green (HDR for bloom), width 5.
            public TypeSettings character = new TypeSettings { outlineColor = new Color(0f, 3.2931557f, 8f, 1f), outlineWidth = 5f };
            public TypeSettings enemy     = new TypeSettings { outlineColor = new Color(19.46857f, 0f, 0f, 1f), outlineWidth = 5f };
            public TypeSettings item      = new TypeSettings { outlineColor = new Color(0f, 8f, 0.69755554f, 1f), outlineWidth = 5f };
            public AnimationSettings animation = new AnimationSettings();
        }

        public Settings settings = new Settings();

        // ── Internals ─────────────────────────────────────────────────────
        static readonly ZLZ_SelectionController.SelectionType[] k_Types =
        {
            ZLZ_SelectionController.SelectionType.Character,
            ZLZ_SelectionController.SelectionType.Enemy,
            ZLZ_SelectionController.SelectionType.Item,
        };

        SelectionOutlinePass[]   _passes;
        Material[]               _materials;
        SelectionOutlineAnimPass _animPass;

        static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");

        // Registry of enabled feature instances — controllers query these via TryGetSettings.
        // Per-feature instance fields replace the previous static settings so multiple URP
        // Renderer assets / cameras no longer overwrite each other's state.
        static readonly List<ZLZ_SelectionOutlineFeature> s_Features =
            new List<ZLZ_SelectionOutlineFeature>();

        // ── ScriptableRendererFeature ─────────────────────────────────────
        public override void Create()
        {
            var shader = Shader.Find("ZLZ/SelectionOutline");
            if (shader == null)
            {
                Debug.LogWarning("[ZLZ] SelectionOutline shader not found.");
                return;
            }

            _materials = new Material[3];
            _passes    = new SelectionOutlinePass[3];
            for (int i = 0; i < 3; i++)
            {
                _materials[i] = CoreUtils.CreateEngineMaterial(shader);
                _passes[i]    = new SelectionOutlinePass(_materials[i]);
            }

            _animPass = new SelectionOutlineAnimPass();
            name = "ZLZ Selection Outline";

            if (!s_Features.Contains(this)) s_Features.Add(this);
        }

        // Per-type appearance lookup by index 0/1/2 — avoids allocating a temp array each frame.
        TypeSettings SettingsForIndex(int i) =>
            i == 0 ? settings.character : (i == 1 ? settings.enemy : settings.item);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_passes == null || _materials == null) return;

            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;

            for (int i = 0; i < 3; i++)
            {
                TypeSettings s = SettingsForIndex(i);
                _materials[i].SetColor(ID_OutlineColor, s.outlineColor);
                _materials[i].SetFloat(ID_OutlineWidth, s.outlineWidth);
                _passes[i].Setup(k_Types[i]);
                renderer.EnqueuePass(_passes[i]);
            }

            // Push appearance + shared animation curves to each animating controller.
            foreach (var ctrl in ZLZ_SelectionController.s_Animating)
            {
                if (ctrl == null) continue;
                int idx = Mathf.Clamp((int)ctrl.defaultType - 1, 0, 2);
                ctrl.SyncSettings(SettingsForIndex(idx), settings.animation);
            }

            renderer.EnqueuePass(_animPass);
        }

        protected override void Dispose(bool disposing)
        {
            s_Features.Remove(this);
            if (_materials == null) return;
            foreach (var m in _materials)
                CoreUtils.Destroy(m);
        }

        // Lookup helper: controllers call this to fetch live settings without touching static state.
        // Returns the first registered feature's settings — single URP renderer is the common case.
        internal static bool TryGetSettings(
            ZLZ_SelectionController.SelectionType type,
            out TypeSettings typeSettings,
            out AnimationSettings animSettings)
        {
            for (int i = 0; i < s_Features.Count; i++)
            {
                var f = s_Features[i];
                if (f == null) continue;
                int idx = Mathf.Clamp((int)type - 1, 0, 2);
                typeSettings = idx == 0 ? f.settings.character
                             : idx == 1 ? f.settings.enemy
                             :            f.settings.item;
                animSettings = f.settings.animation;
                return true;
            }
            typeSettings = null;
            animSettings = null;
            return false;
        }

        // ── Render Pass (instant selection, RendererList) ─────────────────
        class SelectionOutlinePass : ScriptableRenderPass
        {
            static readonly ShaderTagId k_Forward = new ShaderTagId("UniversalForward");

            readonly Material                     _mat;
            ZLZ_SelectionController.SelectionType _selectionType;

            public SelectionOutlinePass(Material mat)
            {
                _mat             = mat;
                renderPassEvent  = RenderPassEvent.AfterRenderingTransparents;
                profilingSampler = new ProfilingSampler("ZLZ Selection Outline");
            }

            public void Setup(ZLZ_SelectionController.SelectionType type) => _selectionType = type;

            FilteringSettings MakeFilter()
            {
                uint layerMask = 1u << (int)_selectionType;
                return new FilteringSettings(RenderQueueRange.all, -1, layerMask);
            }

            // =================================================================
            // UNITY 6 PATH — RenderGraph
            // =================================================================
#if UNITY_6000_0_OR_NEWER
            class PassData
            {
                public RendererListHandle stencilList;
                public RendererListHandle outlineList;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData    = frameData.Get<UniversalCameraData>();
                var lightingData  = frameData.Get<UniversalLightData>();
                var resourceData  = frameData.Get<UniversalResourceData>();
                var filter        = MakeFilter();

                var stencilDraw = RenderingUtils.CreateDrawingSettings(
                    k_Forward, renderingData, cameraData, lightingData, SortingCriteria.None);
                stencilDraw.overrideMaterial          = _mat;
                stencilDraw.overrideMaterialPassIndex = 0;

                var outlineDraw = RenderingUtils.CreateDrawingSettings(
                    k_Forward, renderingData, cameraData, lightingData, SortingCriteria.None);
                outlineDraw.overrideMaterial          = _mat;
                outlineDraw.overrideMaterialPassIndex = 1;

                var stencilParams = new RendererListParams(renderingData.cullResults, stencilDraw, filter);
                var outlineParams  = new RendererListParams(renderingData.cullResults, outlineDraw,  filter);

                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "ZLZ Selection Outline", out var passData, profilingSampler);

                passData.stencilList = renderGraph.CreateRendererList(stencilParams);
                passData.outlineList  = renderGraph.CreateRendererList(outlineParams);

                builder.UseRendererList(passData.stencilList);
                builder.UseRendererList(passData.outlineList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.stencilList);
                    ctx.cmd.DrawRendererList(data.outlineList);
                });
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

                    var filter = MakeFilter();

                    var stencilDraw = CreateDrawingSettings(k_Forward, ref renderingData, SortingCriteria.None);
                    stencilDraw.overrideMaterial          = _mat;
                    stencilDraw.overrideMaterialPassIndex = 0;
                    context.DrawRenderers(renderingData.cullResults, ref stencilDraw, ref filter);

                    var outlineDraw = CreateDrawingSettings(k_Forward, ref renderingData, SortingCriteria.None);
                    outlineDraw.overrideMaterial          = _mat;
                    outlineDraw.overrideMaterialPassIndex = 1;
                    context.DrawRenderers(renderingData.cullResults, ref outlineDraw, ref filter);
                }
                context.ExecuteCommandBuffer(cmd);       // EndSample
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#endif
        }

        // ── Animated Pass (per-object material, DrawRenderer) ─────────────
        class SelectionOutlineAnimPass : ScriptableRenderPass
        {
            // Reused to read each renderer's submesh count without the per-frame Material[]
            // allocation that r.sharedMaterials.Length incurs. Static so the RenderGraph
            // 'static' render func can reach it.
            static readonly List<Material> s_SharedMats = new List<Material>(8);

            public SelectionOutlineAnimPass()
            {
                renderPassEvent  = RenderPassEvent.AfterRenderingTransparents;
                profilingSampler = new ProfilingSampler("ZLZ Selection Outline");
            }

            // =================================================================
            // UNITY 6 PATH — RenderGraph
            // =================================================================
#if UNITY_6000_0_OR_NEWER
            class PassData { }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (ZLZ_SelectionController.s_Animating.Count == 0) return;

                var resourceData = frameData.Get<UniversalResourceData>();

                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "ZLZ Selection Outline", out _, profilingSampler);

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData _, RasterGraphContext ctx) =>
                {
                    var list = ZLZ_SelectionController.s_Animating;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        foreach (var ctrl in list)
                        {
                            if (ctrl == null || ctrl.OutlineMat == null) continue;
                            foreach (var r in ctrl.Renderers)
                            {
                                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                                r.GetSharedMaterials(s_SharedMats);
                                int subCount = s_SharedMats.Count;
                                for (int sub = 0; sub < subCount; sub++)
                                    ctx.cmd.DrawRenderer(r, ctrl.OutlineMat, sub, pass);
                            }
                        }
                    }
                });
            }
#else
            // =================================================================
            // UNITY 2022 PATH — Compatibility Mode
            // =================================================================
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var list = ZLZ_SelectionController.s_Animating;
                if (list.Count == 0) return;

                var cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        foreach (var ctrl in list)
                        {
                            if (ctrl == null || ctrl.OutlineMat == null) continue;
                            foreach (var r in ctrl.Renderers)
                            {
                                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                                r.GetSharedMaterials(s_SharedMats);
                                int subCount = s_SharedMats.Count;
                                for (int sub = 0; sub < subCount; sub++)
                                    cmd.DrawRenderer(r, ctrl.OutlineMat, sub, pass);
                            }
                        }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif
        }
    }
}
