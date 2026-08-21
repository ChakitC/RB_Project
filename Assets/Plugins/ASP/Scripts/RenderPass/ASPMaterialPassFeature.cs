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
    [DisallowMultipleRendererFeature("ASP Material Pass")]
    public partial class ASPMaterialPassFeature : ScriptableRendererFeature
    {
        private int _renderingLayerMask;
        public RenderQueueRange Range = RenderQueueRange.opaque;
        private RenderPassEvent _event = RenderPassEvent.BeforeRenderingOpaques;
        private string _materialPassShaderTag = "ASPMaterialPass";
        private ASPMaterialPass _aspMaterialPass;

        public override void Create()
        {
            _aspMaterialPass = new ASPMaterialPass(name, _materialPassShaderTag, _event, Range,
                (uint)_renderingLayerMask, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_aspMaterialPass);
        }

        protected override void Dispose(bool disposing)
        {
            _aspMaterialPass.Dispose();
        }

        public partial class ASPMaterialPass : ScriptableRenderPass
        {
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId;

            public ASPMaterialPass(string profilerTag, string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, StencilState stencilState,
                int stencilReference)
            {
                profilingSampler = new ProfilingSampler("ASP Material Pass");
                renderPassEvent = evt;
                _filteringSettings = new FilteringSettings(renderQueueRange);
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
    }
}
