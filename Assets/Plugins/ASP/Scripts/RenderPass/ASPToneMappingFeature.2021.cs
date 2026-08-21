#if UNITY_2021
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPToneMappingFeature
    {
        public partial class ASPToneMappingPass
        {
            private RenderTexture _copiedColor;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                if (_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(_copiedColor);
                    _copiedColor = null;
                }

                _copiedColor = RenderTexture.GetTemporary(desc);

                ConfigureTarget(_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);

                _toneMapComponent = VolumeManager.instance.stack.GetComponent<ASPToneMap>();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_toneMapMaterial == null)
                    return;
                if (_toneMapComponent == null)
                {
                    Debug.LogWarning("Need to enable ASP ToneMapping inside the camera volume component.");
                    return;
                }

                if (!_toneMapComponent.IsActive())
                    return;

                ref var postProcessingData = ref renderingData.postProcessingData;
                int lutHeight = postProcessingData.lutSize;
                int lutWidth = lutHeight * lutHeight;

                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blit(cmd, cameraData.renderer.cameraColorTarget, _copiedColor);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);

                    _toneMapMaterial.SetVector(ASPShaderIDs.BlitScaleBias, new Vector4(1, 1, 0, 0));
                    _toneMapMaterial.SetTexture(ASPShaderIDs.MainTex, _copiedColor);
                    _toneMapMaterial.SetTexture(ASPShaderIDs.BaseMap, _copiedColor);
                    SetToneMapParams(_toneMapMaterial, lutWidth, lutHeight);
                    ASPRenderUtil.DrawFullScreen(cmd, _toneMapMaterial,
                        (int)_toneMapComponent.ToneMapType.value);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
