#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ASP
{
    public partial class ASPShadowMapFeature
    {
        public partial class ASPShadowRenderPass
        {
            private bool SetupASPMainLightShadowForRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderTargetWidth = ShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (ShadowData.mainLightShadowCascadesCount == 2)
                    ? ShadowData.mainLightShadowmapHeight >> 1
                    : ShadowData.mainLightShadowmapHeight;

                if (!IsEmptyShadowMap && !IsNotActive)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _shadowMapTexture, renderTargetWidth, renderTargetHeight,
                        16, name: "_CustomMainLightShadowmapTexture");
                    return true;
                }

                ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                    name: "_ASPEmptyLightShadowmapTexture");
                return false;
            }

            private class EmptyShadowData
            {
                public TextureHandle EmptyShadowTarget;
            }

            private class MainLightShadowData
            {
                public TextureHandle ShadowTexture;
                public RendererListHandle[] RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var universalShadowData = frameData.Get<UniversalShadowData>();

                if (lightData.mainLightIndex < 0)
                {
                    return;
                }

                if (lightData.mainLightIndex >= universalShadowData.bias.Count)
                {
                    return;
                }

                var isMainLightShadowReady = SetupASPMainLightShadowForRenderGraph(renderGraph, frameData);
                if (!isMainLightShadowReady)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<EmptyShadowData>("ASP Empty Shadow Pass",
                               out var passData, profilingSampler))
                    {
                        passData.EmptyShadowTarget = renderGraph.ImportTexture(_emptyLightShadowmapTexture);

                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderAttachmentDepth(passData.EmptyShadowTarget, AccessFlags.Write);
                        builder.SetRenderFunc((EmptyShadowData data, RasterGraphContext rgContext) =>
                        {
                            rgContext.cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.black, 1, 0);
                            rgContext.cmd.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                        });
                        builder.SetGlobalTextureAfterPass(passData.EmptyShadowTarget, _customBufferNameId);
                    }

                    return;
                }

                using (var builder = renderGraph.AddRasterRenderPass<MainLightShadowData>("ASP Character Shadow Pass",
                           out var passData, profilingSampler))
                {
                    passData.ShadowTexture = renderGraph.ImportTexture(_shadowMapTexture);

                    var prevViewMatrix = cameraData.camera.worldToCameraMatrix;
                    var prevProjMatrix = cameraData.camera.projectionMatrix;
                    var visibleLight = lightData.visibleLights[lightData.mainLightIndex];

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(_shaderTagId, renderingData,
                        cameraData, lightData, SortingCriteria.CommonOpaque);
                    drawingSettings.perObjectData = PerObjectData.None;
                    var filteringSettings = new FilteringSettings(RenderQueueRange.all);

                    passData.RendererListHandle = new RendererListHandle[ShadowData.mainLightShadowCascadesCount];
                    for (int i = 0; i < ShadowData.mainLightShadowCascadesCount; i++)
                    {
                        passData.RendererListHandle[i] = renderGraph.CreateRendererList(new RendererListParams(
                            UseInjectCullResult ? CullResults[i] : renderingData.cullResults,
                            drawingSettings,
                            filteringSettings));
                        builder.UseRendererList(passData.RendererListHandle[i]);
                    }

                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderAttachmentDepth(passData.ShadowTexture, AccessFlags.Write);
                    builder.SetRenderFunc((MainLightShadowData renderData, RasterGraphContext rgContext) =>
                    {
                        rgContext.cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.black, 1, 0);
                        for (int i = 0; i < ShadowData.mainLightShadowCascadesCount; i++)
                        {
                            rgContext.cmd.SetGlobalDepthBias(1.0f, 3.5f);
                            rgContext.cmd.SetViewport(new Rect(ShadowSliceDatas[i].offsetX, ShadowSliceDatas[i].offsetY,
                                ShadowSliceDatas[i].resolution, ShadowSliceDatas[i].resolution));
                            rgContext.cmd.SetViewProjectionMatrices(LightViewMatrices[i], LightProjectionMatrices[i]);

                            Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                                lightData.mainLightIndex, universalShadowData,
                                LightProjectionMatrices[i], ShadowSliceDatas[i].resolution);
                            rgContext.cmd.SetGlobalVector(ASPShaderIDs.ASPShadowBias, shadowBias);

                            Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                            rgContext.cmd.SetGlobalVector(ASPShaderIDs.ASPLightDirection,
                                new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                            Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                            rgContext.cmd.SetGlobalVector(ASPShaderIDs.ASPLightPosition,
                                new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));

                            rgContext.cmd.DrawRendererList(renderData.RendererListHandle[i]);

                            rgContext.cmd.DisableScissorRect();
                            rgContext.cmd.SetGlobalDepthBias(0.0f, 0.0f);
                        }

                        rgContext.cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
                        rgContext.cmd.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 1);
                        ASPShadowUtil.SetupASPMainLightShadowReceiverConstants(rgContext.cmd, ref visibleLight,
                            universalShadowData, ShadowData, CustomWorldToShadowMatrices, CascadeSplitDistances);
                    });
                    builder.SetGlobalTextureAfterPass(passData.ShadowTexture, _customBufferNameId);
                }
            }
        }
    }
}
#endif
