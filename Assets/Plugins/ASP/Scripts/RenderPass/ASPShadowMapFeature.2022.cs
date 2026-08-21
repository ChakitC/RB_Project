#if UNITY_2022_1_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPShadowMapFeature
    {
        public partial class ASPShadowRenderPass
        {
            private RTHandle _shadowMapTexture;
            private RTHandle _emptyLightShadowmapTexture;

            partial void InitVersionSpecific()
            {
            }

            partial void DisposeVersionSpecific()
            {
                if (_emptyLightShadowmapTexture != null)
                {
                    Shader.SetGlobalTexture(_customBufferNameId, _emptyLightShadowmapTexture);
                }

                _shadowMapTexture?.Release();
                _emptyLightShadowmapTexture?.Release();
                _shadowMapTexture = null;
                _emptyLightShadowmapTexture = null;
            }

            private void SetupASPMainLightShadowForLegacy()
            {
                var renderTargetWidth = ShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (ShadowData.mainLightShadowCascadesCount == 2)
                    ? ShadowData.mainLightShadowmapHeight >> 1
                    : ShadowData.mainLightShadowmapHeight;

                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!IsEmptyShadowMap)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _shadowMapTexture, renderTargetWidth, renderTargetHeight,
                        16, name: "_CustomMainLightShadowmapTexture");
                    ConfigureTarget(_shadowMapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                }
                else
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                    ConfigureTarget(_emptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                }
            }

            public void DrawEmptyShadowMap()
            {
                if (_emptyLightShadowmapTexture == null)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                }

                Shader.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                Shader.SetGlobalTexture(_customBufferNameId, _emptyLightShadowmapTexture);
            }

            private void SetupForEmptyRendering()
            {
                IsEmptyShadowMap = true;
                ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                    name: "_ASPEmptyLightShadowmapTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                SetupASPMainLightShadowForLegacy();
            }

            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalTexture(_customBufferNameId, _emptyLightShadowmapTexture);
                cmd.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (IsEmptyShadowMap)
                {
                    IsEmptyShadowMap = false;
                    RenderEmpty(context);
                    return;
                }

                if (_shadowMapTexture == null)
                {
                    IsEmptyShadowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                if (renderingData.lightData.mainLightIndex < 0 ||
                    renderingData.lightData.mainLightIndex >= renderingData.shadowData.bias.Count)
                {
                    IsEmptyShadowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                var cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];

                if (PerformExtraCull &&
                    renderingData.cameraData.camera.TryGetCullingParameters(out _cullingParameters))
                {
                    _cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                    _cullingParameters.isOrthographic = true;
                }

                var drawSettings =
                    CreateDrawingSettings(_shaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                drawSettings.perObjectData = PerObjectData.None;

                using (new ProfilingScope(cmd, s_shadowMapExecuteSampler))
                {
                    for (int i = 0; i < ShadowData.mainLightShadowCascadesCount; i++)
                    {
                        cmd.SetGlobalDepthBias(1.0f, 3.5f);
                        cmd.SetViewport(new Rect(ShadowSliceDatas[i].offsetX, ShadowSliceDatas[i].offsetY,
                            ShadowSliceDatas[i].resolution, ShadowSliceDatas[i].resolution));
                        cmd.SetViewProjectionMatrices(LightViewMatrices[i], LightProjectionMatrices[i]);

                        Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                            renderingData.lightData.mainLightIndex, ref renderingData.shadowData,
                            LightProjectionMatrices[i], ShadowSliceDatas[i].resolution);
                        cmd.SetGlobalVector(ASPShaderIDs.ASPShadowBias, shadowBias);

                        Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                        cmd.SetGlobalVector(ASPShaderIDs.ASPLightDirection,
                            new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                        cmd.SetGlobalVector(ASPShaderIDs.ASPLightPosition,
                            new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));

                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        context.DrawRenderers(
                            UseInjectCullResult ? CullResults[i] : renderingData.cullResults,
                            ref drawSettings,
                            ref _filteringSettings,
                            ref _renderStateBlock);

                        cmd.DisableScissorRect();
                        cmd.SetGlobalDepthBias(0.0f, 0.0f);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }

                cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
                cmd.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 1);
                cmd.SetGlobalTexture(_customBufferNameId, _shadowMapTexture);
                ASPShadowUtil.SetupASPMainLightShadowReceiverConstants(cmd, ref visibleLight, ref renderingData,
                    ShadowData, CustomWorldToShadowMatrices, CascadeSplitDistances);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
