#if UNITY_2022_1_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPToneMappingFeature
    {
        public partial class ASPToneMappingPass
        {
            private RTHandle _copiedColor;

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, desc, name: "_CameraColorTexture");
                ConfigureTarget(_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);

                _toneMapComponent = VolumeManager.instance.stack.GetComponent<ASPToneMap>();
            }

            partial void DisposeVersionSpecific()
            {
                _copiedColor?.Release();
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
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
                    CoreUtils.SetRenderTarget(cmd, _copiedColor);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, _copiedColor);

                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);

                    var viewportScale = _copiedColor.useScaling
                        ? new Vector2(_copiedColor.rtHandleProperties.rtHandleScale.x,
                            _copiedColor.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;

                    _toneMapMaterial.SetVector(ASPShaderIDs.BlitScaleBias, viewportScale);
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
