#if UNITY_2021
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public partial class ASPShadowMapFeature
    {
        partial void SetScreenSpaceShadowPassEvent(ref RenderPassEvent passEvent)
        {
            passEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public partial class ASPShadowRenderPass
        {
            private RenderTexture _shadowMapTexture;
            private RenderTexture _emptyLightShadowmapTexture;

            partial void InitVersionSpecific()
            {
            }

            partial void DisposeVersionSpecific()
            {
                if (_emptyLightShadowmapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_emptyLightShadowmapTexture);
                    _emptyLightShadowmapTexture = null;
                }

                if (_shadowMapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_shadowMapTexture);
                    _shadowMapTexture = null;
                }
            }

            public void DrawEmptyShadowMap()
            {
                if (_emptyLightShadowmapTexture == null)
                {
                    _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                }

                Shader.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                Shader.SetGlobalTexture(_customBufferNameId, _emptyLightShadowmapTexture);
            }

            private void SetupForEmptyRendering()
            {
                IsEmptyShadowMap = true;
                _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderTargetWidth = ShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (ShadowData.mainLightShadowCascadesCount == 2)
                    ? ShadowData.mainLightShadowmapHeight >> 1
                    : ShadowData.mainLightShadowmapHeight;

                int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                    ShadowData.mainLightShadowmapWidth,
                    ShadowData.mainLightShadowmapHeight,
                    ShadowData.mainLightShadowCascadesCount);

                int shadowLightIndex = renderingData.lightData.mainLightIndex;

                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!renderingData.shadowData.supportsMainLightShadows)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (shadowLightIndex == -1)
                {
                    SetupForEmptyRendering();
                    return;
                }

                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if (shadowLight.lightType != LightType.Directional)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (light.shadows == LightShadows.None)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!IsEmptyShadowMap)
                {
                    for (int cascadeIndex = 0; cascadeIndex < ShadowData.mainLightShadowCascadesCount; ++cascadeIndex)
                    {
                        ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                        var planes = CascadeCullPlanes[cascadeIndex];

                        var success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(
                            ref renderingData.cameraData, ref ShadowData, cascadeIndex, shadowLight.light,
                            shadowResolution, ShadowData.cascadeSplitArray, out Vector4 cullingSphere,
                            out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);

                        LightViewMatrices[cascadeIndex] = viewMatrix;
                        LightProjectionMatrices[cascadeIndex] = projMatrix;
                        CascadeCullPlanes[cascadeIndex] = planes;
                        CascadeSplitDistances[cascadeIndex] = cullingSphere;

                        if (!success)
                        {
                            SetupForEmptyRendering();
                            ConfigureTarget(_emptyLightShadowmapTexture);
                            ConfigureClear(ClearFlag.Depth, Color.black);
                            return;
                        }

                        CustomWorldToShadowMatrices[cascadeIndex] =
                            ASPShadowUtil.GetShadowTransform(LightProjectionMatrices[cascadeIndex],
                                LightViewMatrices[cascadeIndex]);

                        var offsetX = (cascadeIndex % 2) * shadowResolution;
                        var offsetY = (cascadeIndex / 2) * shadowResolution;

                        ASPShadowUtil.ApplySliceTransform(ref CustomWorldToShadowMatrices[cascadeIndex], offsetX,
                            offsetY, shadowResolution, renderTargetWidth, renderTargetHeight);

                        ShadowSliceDatas[cascadeIndex].projectionMatrix = LightProjectionMatrices[cascadeIndex];
                        ShadowSliceDatas[cascadeIndex].viewMatrix = LightViewMatrices[cascadeIndex];
                        ShadowSliceDatas[cascadeIndex].offsetX = offsetX;
                        ShadowSliceDatas[cascadeIndex].offsetY = offsetY;
                        ShadowSliceDatas[cascadeIndex].resolution = shadowResolution;
                        ShadowSliceDatas[cascadeIndex].shadowTransform = CustomWorldToShadowMatrices[cascadeIndex];
                        ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                    }

                    _shadowMapTexture =
                        ShadowUtils.GetTemporaryShadowTexture(renderTargetWidth, renderTargetHeight,
                            k_ShadowmapBufferBits);
                    ConfigureTarget(_shadowMapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
                else
                {
                    _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                    ConfigureTarget(_emptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
            }

            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                cmd.SetGlobalTexture(_customBufferNameId, _emptyLightShadowmapTexture);
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

                CommandBuffer cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];

                using (new ProfilingScope(cmd, s_shadowMapExecuteSampler))
                {
                    for (int i = 0; i < ShadowData.mainLightShadowCascadesCount; i++)
                    {
                        var drawSettings =
                            CreateDrawingSettings(_shaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                        drawSettings.perObjectData = PerObjectData.None;

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

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (_emptyLightShadowmapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_emptyLightShadowmapTexture);
                    _emptyLightShadowmapTexture = null;
                }

                if (_shadowMapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_shadowMapTexture);
                    _shadowMapTexture = null;
                }
            }
        }
    }
}
#endif
