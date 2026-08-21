#if UNITY_2022_1_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPScreenSpaceOutlineFeature
    {
        public partial class ASPScreenSpaceOutlinePass
        {
            partial void DisposeVersionSpecific()
            {
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();
            }

#if UNITY_6000_0_OR_NEWER
            [Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_outlineEffectMaterial == null || _screenSpaceOutlineSetting == null ||
                    !_screenSpaceOutlineSetting.IsActive())
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    var cameraColorTarget = cameraData.renderer.cameraColorTargetHandle;
                    Vector2 viewportScale = cameraColorTarget.useScaling
                        ? new Vector2(cameraColorTarget.rtHandleProperties.rtHandleScale.x,
                            cameraColorTarget.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;
                    SetupOutlineFirstPassParam(_outlineEffectMaterial, _screenSpaceOutlineSetting);
                    _outlineEffectMaterial.SetColor(ASPShaderIDs.OutlineColor, _screenSpaceOutlineSetting.OutlineColor.value);
                    _outlineEffectMaterial.SetVector(ASPShaderIDs.BlitScaleBias, viewportScale);
                    ASPRenderUtil.DrawFullScreen(cmd, _outlineEffectMaterial, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
