#if UNITY_2021
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPScreenSpaceOutlineFeature
    {
        public partial class ASPScreenSpaceOutlinePass
        {
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_outlineEffectMaterial == null)
                    return;
                if (_screenSpaceOutlineSetting == null || !_screenSpaceOutlineSetting.IsActive())
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    SetupOutlineFirstPassParam(_outlineEffectMaterial, _screenSpaceOutlineSetting);
                    _outlineEffectMaterial.SetColor(ASPShaderIDs.OutlineColor, _screenSpaceOutlineSetting.OutlineColor.value);
                    _outlineEffectMaterial.SetVector(ASPShaderIDs.BlitScaleBias, new Vector4(1, 1, 0, 0));
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
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
