/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP Depth-Offset Shadow")]
    public partial class ASPDepthOffsetShadowFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("m_layer")]
        [SingleLayerMask] public int Layer;
        [FormerlySerializedAs("m_renderingLayerMask")]
        [RenderingLayerMask] public int RenderingLayerMask;
        private RenderQueueRange _range = RenderQueueRange.all;
        private RenderPassEvent _event = RenderPassEvent.BeforeRenderingOpaques;
        private const RenderPassEvent k_CleanUpEvent = RenderPassEvent.AfterRenderingTransparents;
        private string _materialPassShaderTag = "DepthOffsetShadow";
        private ASPDepthOffsetShadowPass _aspDepthOffsetShadowPass;
        private ASPDepthOffsetShadowCleanUpPass _aspDepthOffsetShadowCleanUpPass;

        public int GetLayer()
        {
            return RenderingLayerMask;
        }

        public void SetLayer(int value)
        {
            RenderingLayerMask = value;
        }

        public override void Create()
        {
            _aspDepthOffsetShadowPass = new ASPDepthOffsetShadowPass(_materialPassShaderTag, _event, _range,
                (uint)RenderingLayerMask, 1 << Layer, StencilState.defaultValue, 0);
            _aspDepthOffsetShadowCleanUpPass =
                new ASPDepthOffsetShadowCleanUpPass("ASP Clear DepthOffsetShadow Global",
                    ASPShaderIDs.ASPDepthOffsetShadowTexture);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _aspDepthOffsetShadowPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(_aspDepthOffsetShadowPass);
#if UNITY_6000_0_OR_NEWER
            _aspDepthOffsetShadowCleanUpPass.renderPassEvent = k_CleanUpEvent;
            renderer.EnqueuePass(_aspDepthOffsetShadowCleanUpPass);
#endif
        }

        protected override void Dispose(bool disposing)
        {
            _aspDepthOffsetShadowPass.Dispose();
        }

        public partial class ASPDepthOffsetShadowPass : ScriptableRenderPass
        {
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId;

            public ASPDepthOffsetShadowPass(string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState,
                int stencilReference)
            {
                profilingSampler = new ProfilingSampler("ASP DepthOffsetShadow Pass");
                renderPassEvent = evt;
                _filteringSettings = new FilteringSettings(renderQueueRange);
                _filteringSettings.layerMask = layerMask;
                _filteringSettings.renderingLayerMask = renderingLayerMask;
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _shaderTagId = new ShaderTagId(shaderTagId);
                InitVersionSpecific();
            }

            public void Dispose()
            {
                DisposeVersionSpecific();
            }

            partial void InitVersionSpecific();
            partial void DisposeVersionSpecific();
        }

        public partial class ASPDepthOffsetShadowCleanUpPass : ScriptableRenderPass
        {
            private readonly int _globalTextureId;

            public ASPDepthOffsetShadowCleanUpPass(string passName, int globalTextureId)
            {
                profilingSampler = new ProfilingSampler(passName);
                _globalTextureId = globalTextureId;
            }
        }
    }
}
