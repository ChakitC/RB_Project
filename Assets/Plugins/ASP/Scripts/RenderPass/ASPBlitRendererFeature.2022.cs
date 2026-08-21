#if UNITY_2022_1_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPBlitRendererFeature
    {
        public partial class FullScreenRenderPass
        {
            private RTHandle _copiedColor;
            private RTHandle _outputRT;

            partial void DisposeVersionSpecific()
            {
                _copiedColor?.Release();
                _outputRT?.Release();
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, desc, name: "_CameraColorTexture");
                if (_outputTextureName != string.Empty)
                {
                    int width = _useHalfScale
                        ? renderingData.cameraData.cameraTargetDescriptor.width / 2
                        : renderingData.cameraData.cameraTargetDescriptor.width;
                    int height = _useHalfScale
                        ? renderingData.cameraData.cameraTargetDescriptor.height / 2
                        : renderingData.cameraData.cameraTargetDescriptor.height;
                    desc.width = width;
                    desc.height = height;
                    RenderingUtils.ReAllocateIfNeeded(ref _outputRT, desc, name: _outputTextureName);
                    ConfigureTarget(_outputRT);
                }
                else
                {
                    ConfigureTarget(_copiedColor);
                }

                ConfigureClear(ClearFlag.Color, Color.white);
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null)
                    return;

                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    if (_copyActiveColor)
                    {
                        CoreUtils.SetRenderTarget(cmd, _copiedColor);
                        Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, _copiedColor);
                    }

                    if (_bindDepthStencilAttachment)
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle,
                            cameraData.renderer.cameraDepthTargetHandle);
                    }
                    else
                    {
                        if (_outputRT != null)
                        {
                            CoreUtils.SetRenderTarget(cmd, _outputRT);
                        }
                        else
                        {
                            CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                        }
                    }

                    _material.SetVector(ASPShaderIDs.BlitScaleBias, new Vector4(1, 1, 0, 0));
                    _material.SetTexture(ASPShaderIDs.BaseMap, _copiedColor);
                    _material.SetTexture(ASPShaderIDs.BlitTexture, _copiedColor);
                    ASPRenderUtil.DrawFullScreen(cmd, _material, 0);
                    if (_outputRT != null)
                    {
                        cmd.SetGlobalTexture(_outputTextureName, _outputRT);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
