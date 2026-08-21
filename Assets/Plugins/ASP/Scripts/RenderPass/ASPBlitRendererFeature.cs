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
    public partial class ASPBlitRendererFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("injectionPoint")]
        public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        [FormerlySerializedAs("requirements")]
        public ScriptableRenderPassInput Requirements = ScriptableRenderPassInput.None;
        [FormerlySerializedAs("passMaterial")]
        public Material PassMaterial;
        public bool UseHalfScale;
        public string OutputTextureName;
        [FormerlySerializedAs("bindDepthStencilAttachment")]
        public bool BindDepthStencilAttachment = false;
        private bool _fetchColorBuffer = true;

        private FullScreenRenderPass _fullScreenPass;

        public override void Create()
        {
            _fullScreenPass = new FullScreenRenderPass(name, OutputTextureName, UseHalfScale);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ASPRenderUtil.ShouldSkipCamera(renderingData.cameraData.cameraType))
                return;

            if (PassMaterial == null)
                return;

            _fullScreenPass.renderPassEvent = (RenderPassEvent)InjectionPoint;
            _fullScreenPass.ConfigureInput(Requirements);
            _fullScreenPass.SetupMembers(PassMaterial, _fetchColorBuffer, BindDepthStencilAttachment);

            renderer.EnqueuePass(_fullScreenPass);
        }

        protected override void Dispose(bool disposing)
        {
            _fullScreenPass.Dispose();
        }

        public partial class FullScreenRenderPass : ScriptableRenderPass
        {
            private protected string _outputTextureName;
            private protected Material _material;
            private protected bool _copyActiveColor;
            private protected bool _bindDepthStencilAttachment;
            private protected bool _useHalfScale;

            public FullScreenRenderPass(string passName, string outputTextureName, bool useHalfScale)
            {
                _outputTextureName = outputTextureName;
                profilingSampler = new ProfilingSampler(passName);
                _useHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material, bool copyActiveColor, bool bindDepthStencilAttachment)
            {
                _material = material;
                _copyActiveColor = copyActiveColor;
                _bindDepthStencilAttachment = bindDepthStencilAttachment;
            }

            public void Dispose()
            {
                DisposeVersionSpecific();
            }

            partial void DisposeVersionSpecific();
        }
    }
}
