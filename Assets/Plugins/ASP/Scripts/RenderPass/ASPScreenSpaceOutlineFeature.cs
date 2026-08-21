/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP Screen Space Outline")]
    public partial class ASPScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("injectionPoint")]
        public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material _material;
        private ASPScreenSpaceOutlinePass _outlinePass;

        public override void Create()
        {
            _outlinePass = new ASPScreenSpaceOutlinePass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ASPRenderUtil.ShouldSkipCamera(renderingData.cameraData.cameraType))
                return;

            if (_material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/Outline");
                if (defaultShader != null)
                {
                    _material = new Material(defaultShader);
                }

                return;
            }

            var outlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();
            if (outlineSetting == null || !outlineSetting.IsActive())
                return;

            _outlinePass.renderPassEvent = (RenderPassEvent)InjectionPoint;
            _outlinePass.ConfigureInput(GetOutlinePassRequirements(outlineSetting));
            _outlinePass.SetupMembers(_material);
            renderer.EnqueuePass(_outlinePass);
        }

        private static ScriptableRenderPassInput GetOutlinePassRequirements(ASP.ASPSreenSpaceOutline outlineSetting)
        {
            var requirements = ScriptableRenderPassInput.Depth;
            if (outlineSetting.EnableNormalsEdge.value)
                requirements |= ScriptableRenderPassInput.Normal;
            return requirements;
        }

        protected override void Dispose(bool disposing)
        {
            _outlinePass.Dispose();
        }

        public partial class ASPScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private protected Material _outlineEffectMaterial;
            private protected ASP.ASPSreenSpaceOutline _screenSpaceOutlineSetting;

            public ASPScreenSpaceOutlinePass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                _outlineEffectMaterial = material;
            }

            public void Dispose()
            {
                DisposeVersionSpecific();
            }

            partial void DisposeVersionSpecific();

            private protected static void SetKeyword(Material material, string keyword, bool state)
            {
                if (state)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            private protected static void SetupOutlineFirstPassParam(Material mat, ASP.ASPSreenSpaceOutline volumeSetting)
            {
                SetKeyword(mat, "_IS_DEBUG_MODE", volumeSetting.EnableDebugMode.value);
                mat.SetColor(ASPShaderIDs.DebugBackgroundColor, volumeSetting.DebugBackground.value);
                mat.SetFloat(ASPShaderIDs.DebugEdgeType, (float)((int)volumeSetting.ScreenSpaceOutlineDebugMode.value));

                mat.SetFloat(ASPShaderIDs.OutlineWidth, volumeSetting.OutlineWidth.value);
                mat.SetFloat(ASPShaderIDs.MaterialThreshold, volumeSetting.MaterialEdgeThreshold.value);
                mat.SetFloat(ASPShaderIDs.LumaThreshold, volumeSetting.AlbedoEdgeThreshold.value);
                mat.SetFloat(ASPShaderIDs.DepthThreshold, volumeSetting.DepthEdgeThreshold.value);
                mat.SetFloat(ASPShaderIDs.NormalsThreshold, volumeSetting.NormalsEdgeThreshold.value);

                CoreUtils.SetKeyword(mat, "MATERIAL_EDGE", volumeSetting.EnableMaterialEdge.value);
                CoreUtils.SetKeyword(mat, "LUMA_EDGE", volumeSetting.EnableAlbedoEdge.value);
                CoreUtils.SetKeyword(mat, "NORMAL_EDGE", volumeSetting.EnableNormalsEdge.value);
                CoreUtils.SetKeyword(mat, "DEPTH_EDGE", volumeSetting.EnableDepthEdge.value);
                CoreUtils.SetKeyword(mat, "SCENE_OBJECT_OUTLINE", volumeSetting.EnableSceneObjectOutline.value);

                mat.SetFloat(ASPShaderIDs.EnableDistanceFalloff, volumeSetting.EnableDistanceFalloff.value ? 1.0f : 0);
                mat.SetVector(ASPShaderIDs.DistanceFalloffStartEnd, volumeSetting.DistanceFalloffStartEnd.value);
            }
        }
    }
}
