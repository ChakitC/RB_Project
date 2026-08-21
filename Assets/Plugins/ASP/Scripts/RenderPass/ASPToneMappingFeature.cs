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
    [DisallowMultipleRendererFeature("ASP ToneMapping")]
    public partial class ASPToneMappingFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("injectionPoint")]
        public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material _material;
        private ASPToneMappingPass _aspToneMapPass;

        public override void Create()
        {
            _aspToneMapPass = new ASPToneMappingPass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ASPRenderUtil.ShouldSkipCamera(renderingData.cameraData.cameraType))
                return;

            if (_material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/ToneMapping");
                if (defaultShader != null)
                    _material = new Material(defaultShader);
                return;
            }

            _aspToneMapPass.renderPassEvent = (RenderPassEvent)InjectionPoint;
            _aspToneMapPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            _aspToneMapPass.SetupMembers(_material);

            renderer.EnqueuePass(_aspToneMapPass);
        }

        protected override void Dispose(bool disposing)
        {
            _aspToneMapPass.Dispose();
        }

        public partial class ASPToneMappingPass : ScriptableRenderPass
        {
            private Material _toneMapMaterial;
            private ASPToneMap _toneMapComponent;

            public ASPToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
                //requiresIntermediateTexture = true;
            }

            public void SetupMembers(Material material)
            {
                _toneMapMaterial = material;
            }

            public void Dispose()
            {
                DisposeVersionSpecific();
            }

            partial void DisposeVersionSpecific();

            private void SetToneMapParams(Material mat, int lutWidth, int lutHeight)
            {
                SetToneMapParams(mat, lutWidth, lutHeight,
                    _toneMapComponent.CharacterPixelsToneMapStrength.value,
                    _toneMapComponent.Exposure.value,
                    _toneMapComponent.IgnoreCharacterPixels.value);
            }

            private static void SetToneMapParams(Material mat, int lutWidth, int lutHeight, float toneMapLowerBound,
                float exposure, bool ignoreCharacterPixels)
            {
                mat.SetVector(ASPShaderIDs.LutParams,
                    new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f, 0));
                mat.SetFloat(ASPShaderIDs.ToneMapLowerBound, toneMapLowerBound);
                mat.SetFloat(ASPShaderIDs.Exposure, exposure);
                mat.SetFloat(ASPShaderIDs.IgnoreCharacterPixels,
                    ignoreCharacterPixels ? 1.0f : 0);
            }

        }
    }
}
