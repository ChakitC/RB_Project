#if UNITY_2021
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPBlitRendererFeature
    {
        public partial class FullScreenRenderPass
        {
            private RenderTexture _copiedColor;
            private RenderTexture _outputRT;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                if (_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(_copiedColor);
                    _copiedColor = null;
                }

                _copiedColor = RenderTexture.GetTemporary(desc);

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

                    if (_copiedColor == null)
                    {
                        _outputRT = RenderTexture.GetTemporary(desc);
                    }

                    ConfigureTarget(_outputRT);
                }
                else
                {
                    ConfigureTarget(_copiedColor);
                }

                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(_copiedColor);
                    _copiedColor = null;
                }

                if (_outputRT != null)
                {
                    RenderTexture.ReleaseTemporary(_outputRT);
                    _outputRT = null;
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null)
                    return;

                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(null, profilingSampler))
                {
                    if (_copyActiveColor)
                    {
                        CoreUtils.SetRenderTarget(cmd, _copiedColor);
                        Blit(cmd, cameraData.renderer.cameraColorTarget, _copiedColor);
                    }

                    if (_bindDepthStencilAttachment)
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget, cameraData.renderer.cameraDepthTarget);
                    }
                    else
                    {
                        if (_outputRT != null)
                        {
                            CoreUtils.SetRenderTarget(cmd, _outputRT);
                        }
                        else
                        {
                            CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
                        }
                    }

                    _material.SetVector(ASPShaderIDs.BlitScaleBias, new Vector4(1, 1, 0, 0));
                    _material.SetTexture(ASPShaderIDs.BaseMap, _copiedColor);
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
