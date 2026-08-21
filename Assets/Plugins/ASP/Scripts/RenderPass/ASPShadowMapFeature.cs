/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP ShadowMap")]
    public partial class ASPShadowMapFeature : ScriptableRendererFeature
    {
        [Tooltip("Expensive, but can prevent shadow missing when object outside camera view")]
        [FormerlySerializedAs("_performExtraCull")]
        private bool _performExtraCull = true;

        [FormerlySerializedAs("RenderQueueRange")]
        [FormerlySerializedAs("m_renderQueueRange")]
        public RenderQueueRange Range = RenderQueueRange.all;

        [FormerlySerializedAs("_layerMask")]
        private LayerMask _layerMask = -1;

        [FormerlySerializedAs("_customBufferName")]
        [FormerlySerializedAs("m_CustomBufferName")]
        [FormerlySerializedAs("m_customBufferName")]
        [SerializeField]
        private string _customBufferName = "_ASPShadowMap";

        [FormerlySerializedAs("m_characterShadowMapResolution")]
        [FormerlySerializedAs("_characterShadowMapResolution")]
        [SerializeField]
        private CharacterShadowMapResolution _characterShadowMapResolution = CharacterShadowMapResolution.SIZE_2048;

        [FormerlySerializedAs("ShadowDistance")]
        public float ClipDistance = 50;

        [Range(1, 4)]
        public int CascadeCount = 1;

        [FormerlySerializedAs("LastBorder")]
        [Range(0, 1)]
        [Tooltip("Shadow fade out ratio on last cascade, set to 0 means no fading")]
        public float ShadowFadeRatio = 0.2f;

        public bool UseScreenSpaceShadowPass;
        public Color ScreenSpaceShadowColor = new Color(0, 0, 0, 0.3f);

        [FormerlySerializedAs("_renderingLayerMask")]
        [FormerlySerializedAs("m_renderingLayerMask")]
        [SerializeField]
        private int _renderingLayerMask = -1;

        private ASPShadowRenderPass _scriptablePass;
        private ASPShadowData _shadowData;

        private Matrix4x4[] _customWorldToShadowMatrices;
        private List<Plane[]> _cascadeCullPlanes;
        private Matrix4x4[] _lightViewMatrices;
        private Matrix4x4[] _lightProjectionMatrices;
        private ShadowSliceData[] _shadowSliceDatas;
        private ScriptableCullingParameters _cullingParameters;
        private Vector4[] _cascadeSplitDistances;
        private Light _mainShadowLight;

        private static bool s_hasRenderPassEnqueued;

        private Material _screenSpaceShadowMapMat;
        private ASPFullScreenRenderPass _fullScreenPass;

        private const string k_ScreenSpaceShadowShaderName = "Hidden/ASP/ScreenSpaceShadows";

        private void ClearData()
        {
            _customWorldToShadowMatrices = new Matrix4x4[4 + 1];
            _cullingParameters = new ScriptableCullingParameters();
            _cascadeSplitDistances = new Vector4[4];

            _lightViewMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightViewMatrices[i] = Matrix4x4.identity;
            }

            _lightProjectionMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightProjectionMatrices[i] = Matrix4x4.identity;
            }

            _shadowSliceDatas = new ShadowSliceData[4];

            _cascadeCullPlanes = new List<Plane[]>();
            for (int i = 0; i < 4; i++)
            {
                _cascadeCullPlanes.Add(new Plane[6]);
            }
        }

        private static Light SelectMainShadowLight()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var maxLightIntensity = float.MinValue;
            Light selectedLight = null;

            foreach (var light in lights)
            {
                if (light.type != LightType.Directional || light.shadows == LightShadows.None)
                {
                    continue;
                }

                if (light.intensity > maxLightIntensity)
                {
                    selectedLight = light;
                    maxLightIntensity = light.intensity;
                }
            }

            return selectedLight;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var selectedLight = SelectMainShadowLight();
            if (selectedLight != null)
            {
                _mainShadowLight = selectedLight;
            }
        }

        public override void Create()
        {
#if UNITY_EDITOR
            EditorUtil.AddAlwaysIncludedShader(k_ScreenSpaceShadowShaderName);
#endif
            _mainShadowLight = SelectMainShadowLight();

            ClearData();
            int customBufferNameId = Shader.PropertyToID(_customBufferName);
            _scriptablePass = new ASPShadowRenderPass((uint)_renderingLayerMask, customBufferNameId,
                RenderPassEvent.AfterRenderingShadows, Range, _layerMask, _shadowData);

            if (!isActive)
            {
                _scriptablePass.IsNotActive = true;
                _scriptablePass.DrawEmptyShadowMap();
            }
            else
            {
                _scriptablePass.IsNotActive = false;
                Shader.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
            }

            if (!s_hasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
                s_hasRenderPassEnqueued = true;
            }

            _fullScreenPass = new ASPFullScreenRenderPass(name);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!UseScreenSpaceShadowPass)
            {
                return;
            }

            if (ASPRenderUtil.ShouldSkipScreenSpaceShadowPass(ref renderingData.cameraData))
            {
                return;
            }

            var passEvent = RenderPassEvent.AfterRenderingOpaques;
            SetScreenSpaceShadowPassEvent(ref passEvent);
            _fullScreenPass.renderPassEvent = passEvent;
            _fullScreenPass.ConfigureInput(ScriptableRenderPassInput.Depth);

            if (_screenSpaceShadowMapMat == null)
            {
                var shader = Shader.Find(k_ScreenSpaceShadowShaderName);
                if (shader == null)
                {
                    return;
                }

                _screenSpaceShadowMapMat = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave
                };
            }

            _screenSpaceShadowMapMat.SetColor(ASPShaderIDs.BaseColor, ScreenSpaceShadowColor);
            _fullScreenPass.SetupMembers(_screenSpaceShadowMapMat, 0, false, true);
            renderer.EnqueuePass(_fullScreenPass);
        }

        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (_mainShadowLight == null || _mainShadowLight.lightmapBakeType == LightmapBakeType.Baked)
            {
                return;
            }
#endif

            var camera = Camera.main;
            if (camera == null || _mainShadowLight == null)
            {
                Shader.SetGlobalInt(ASPShaderIDs.ASPShadowMapValid, 0);
                return;
            }

            _shadowData = ASPShadowUtil.SetupCascadesData((int)_characterShadowMapResolution, CascadeCount,
                ClipDistance, ShadowFadeRatio);

            var renderTargetWidth = _shadowData.mainLightShadowmapWidth;
            var renderTargetHeight = (_shadowData.mainLightShadowCascadesCount == 2)
                ? _shadowData.mainLightShadowmapHeight >> 1
                : _shadowData.mainLightShadowmapHeight;

            var shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                _shadowData.mainLightShadowmapWidth,
                _shadowData.mainLightShadowmapHeight,
                _shadowData.mainLightShadowCascadesCount);

            var isEmptyShadowMap = (_mainShadowLight.type != LightType.Directional ||
                                    _mainShadowLight.shadows == LightShadows.None);

            camera.TryGetCullingParameters(out _cullingParameters);
            _cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
            _cullingParameters.isOrthographic = true;

            for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
            {
                _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;

                var planes = _cascadeCullPlanes[i];
                bool success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(camera, ref _shadowData,
                    i, _mainShadowLight, shadowResolution, _shadowData.cascadeSplitArray, out Vector4 cullingSphere,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);

                _lightViewMatrices[i] = viewMatrix;
                _lightProjectionMatrices[i] = projMatrix;
                _cascadeCullPlanes[i] = planes;
                _cascadeSplitDistances[i] = cullingSphere;

                if (!success)
                {
                    isEmptyShadowMap = true;
                }

                _customWorldToShadowMatrices[i] = ASPShadowUtil.GetShadowTransform(
                    _lightProjectionMatrices[i], _lightViewMatrices[i]);

                var offsetX = (i % 2) * shadowResolution;
                var offsetY = (i / 2) * shadowResolution;

                ASPShadowUtil.ApplySliceTransform(ref _customWorldToShadowMatrices[i], offsetX, offsetY,
                    shadowResolution, renderTargetWidth, renderTargetHeight);

                _shadowSliceDatas[i].projectionMatrix = _lightProjectionMatrices[i];
                _shadowSliceDatas[i].viewMatrix = _lightViewMatrices[i];
                _shadowSliceDatas[i].offsetX = offsetX;
                _shadowSliceDatas[i].offsetY = offsetY;
                _shadowSliceDatas[i].resolution = shadowResolution;
                _shadowSliceDatas[i].shadowTransform = _customWorldToShadowMatrices[i];
                _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;
            }

            var cullResults = new CullingResults[_shadowData.mainLightShadowCascadesCount];
            for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
            {
                _cullingParameters.cullingMatrix = _lightProjectionMatrices[i] * _lightViewMatrices[i];
                for (int cullPlaneIndex = 0; cullPlaneIndex < 6; cullPlaneIndex++)
                {
                    _cullingParameters.SetCullingPlane(cullPlaneIndex, _cascadeCullPlanes[i][cullPlaneIndex]);
                }

                cullResults[i] = context.Cull(ref _cullingParameters);
            }

            _scriptablePass.CullResults = cullResults;
            _scriptablePass.UseInjectCullResult = true;
            _scriptablePass.PerformExtraCull = _performExtraCull;
            _scriptablePass.IsEmptyShadowMap = isEmptyShadowMap;
            _scriptablePass.ShadowData = _shadowData;
            _scriptablePass.CustomWorldToShadowMatrices = _customWorldToShadowMatrices;
            _scriptablePass.CascadeCullPlanes = _cascadeCullPlanes;
            _scriptablePass.LightViewMatrices = _lightViewMatrices;
            _scriptablePass.LightProjectionMatrices = _lightProjectionMatrices;
            _scriptablePass.CascadeSplitDistances = _cascadeSplitDistances;
            _scriptablePass.ShadowSliceDatas = _shadowSliceDatas;

            pipeline.scriptableRenderer.EnqueuePass(_scriptablePass);
        }

        protected override void Dispose(bool disposing)
        {
            if (s_hasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
                s_hasRenderPassEnqueued = false;
            }

            _scriptablePass?.Dispose();
            _fullScreenPass?.Dispose();
        }

        partial void SetScreenSpaceShadowPassEvent(ref RenderPassEvent passEvent);
    }
}
