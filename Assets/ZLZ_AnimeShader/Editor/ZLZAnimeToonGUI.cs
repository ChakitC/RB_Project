#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using ZLZ.AnimeShader;

namespace ZLZ.AnimeShader.Editor
{
    public class ZLZAnimeToonGUI : ShaderGUI
    {
        // Header colors
        static readonly Color HEADER_OPEN_BG = new Color(0.55f, 0.38f, 0.1f, 1f);
        static readonly Color HEADER_CLOSED_BG = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color HEADER_OPEN_HOVER_BG = new Color(0.7f, 0.48f, 0.1f, 1f);
        static readonly Color HEADER_CLOSED_HOVER_BG = new Color(0.18f, 0.18f, 0.18f, 1f);
        static readonly Color FEATURE_MIX_BG = new Color(0.18f, 0.55f, 0.55f, 1f);
        static readonly Color FEATURE_MIX_HOVER_BG = new Color(0.22f, 0.65f, 0.65f, 1f);

        // ---------------- GUI Styles ----------------
        static GUIStyle _boxStyle;
        static GUIStyle _headerButtonStyle;
        static GUIStyle _featureStatusStyle;
        static GUIStyle _featureNameStyle;
        static GUIStyle _contentPadStyle;

        // Drop the cached editor styles when entering Play mode with Domain Reload
        // disabled (Unity 6.6 default). Without this the GUIStyle caches would
        // survive between sessions and could end up pointing at internal Unity
        // style state that has been invalidated.
        [InitializeOnEnterPlayMode]
        static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            if (!options.HasFlag(EnterPlayModeOptions.DisableDomainReload)) return;
            _boxStyle = null;
            _headerButtonStyle = null;
            _featureStatusStyle = null;
            _featureNameStyle = null;
            _contentPadStyle = null;
            _infoBoxTitle = null;
            _infoBoxBody = null;
            _infoBoxWarn = null;
        }

        // ---------------- Shader Keywords ----------------
        const string KW_FACESHADOW_ON = "_SWITCH_TO_FACE_MODE_ON";
        const string KW_FACESHADOW_OFF = "_SWITCH_TO_FACE_MODE_OFF";

        const string KW_SOFTLIGHT_ON = "_SOFTLIGHT_ON";
        const string KW_SOFTLIGHT_OFF = "_SOFTLIGHT_OFF";

        const string KW_RIMLIGHT_ON = "_RIMLIGHT_ON";
        const string KW_RIMLIGHT_OFF = "_RIMLIGHT_OFF";

        const string KW_USEMETALLIC_ON = "_USEMETALLIC_ON";
        const string KW_USEMETALLIC_OFF = "_USEMETALLIC_OFF";

        const string KW_HAIR_HIGHLIGHT_ON = "_HAIR_HIGHLIGHT_ON";
        const string KW_HAIR_HIGHLIGHT_OFF = "_HAIR_HIGHLIGHT_OFF";

        const string KW_SPECULAR_ON = "_SPECULAR_ON";
        const string KW_SPECULAR_OFF = "_SPECULAR_OFF";

        const string KW_EMISSIVEGLOW_ON = "_EMISSIVEGLOW_ON";
        const string KW_EMISSIVEGLOW_OFF = "_EMISSIVEGLOW_OFF";

        const string KW_DISSOLVE_ON = "_DISSOLVE_ON";
        const string KW_DISSOLVE_OFF = "_DISSOLVE_OFF";

        const string KW_TARGETDARKEN_ON = "_TARGETDARKEN_ON";
        const string KW_TARGETDARKEN_OFF = "_TARGETDARKEN_OFF";

        const string KW_INDICATOR_ON = "_INDICATOR_ON";
        const string KW_INDICATOR_OFF = "_INDICATOR_OFF";

        const string KW_GETHIT_ON = "_GETHIT_ON";
        const string KW_GETHIT_OFF = "_GETHIT_OFF";

        const string KW_LIGHTSWEEP_ON = "_LIGHTSWEEP_ON";
        const string KW_LIGHTSWEEP_OFF = "_LIGHTSWEEP_OFF";

        const string KW_UPGRADE_ON = "_UPGRADE_ON";
        const string KW_UPGRADE_OFF = "_UPGRADE_OFF";

        const string KW_HEAD_BACK_HIDE_ON = "_HEAD_BACK_HIDE_ON";
        const string KW_HEAD_BACK_HIDE_OFF = "_HEAD_BACK_HIDE_OFF";

        const string KW_EYE_BACKFACE_CLIP_ON = "_EYE_BACKFACE_CLIP_ON";
        const string KW_EYE_BACKFACE_CLIP_OFF = "_EYE_BACKFACE_CLIP_OFF";

        const string KW_HAIR_BACKFACE_FADE_ON = "_HAIR_BACKFACE_FADE_ON";
        const string KW_HAIR_BACKFACE_FADE_OFF = "_HAIR_BACKFACE_FADE_OFF";

        const string KW_USENORMAL_ON  = "_USENORMAL_ON";
        const string KW_USENORMAL_OFF = "_USENORMAL_OFF";

        const string KW_USE_MASKTEX1_ON  = "_USE_MASKTEX1_ON";
        const string KW_USE_MASKTEX1_OFF = "_USE_MASKTEX1_OFF";
        const string KW_USE_MASKTEX2_ON  = "_USE_MASKTEX2_ON";
        const string KW_USE_MASKTEX2_OFF = "_USE_MASKTEX2_OFF";

        const string KW_OUTLINEMASK_ON  = "_OUTLINEMASK_ON";
        const string KW_OUTLINEMASK_OFF = "_OUTLINEMASK_OFF";

        // Channel index -> display label. idx 0..7 = M1/M2 RGBA, idx 8 = no mask (whole mesh)
        static readonly string[] CHANNEL_LABELS_DROPDOWN = { "Mask 1 . R", "Mask 1 . G", "Mask 1 . B", "Mask 1 . A", "Mask 2 . R", "Mask 2 . G", "Mask 2 . B", "Mask 2 . A" };
        static readonly string[] CHANNEL_LABELS = { "Mask 1 . R", "Mask 1 . G", "Mask 1 . B", "Mask 1 . A", "Mask 2 . R", "Mask 2 . G", "Mask 2 . B", "Mask 2 . A", "Whole Mesh (no mask)" };
        const int CHANNEL_NONE = 8;

        // ---------------- MaterialProperty Cache : Rendering States ----------------
        MaterialProperty _renderQueueSelector;
        MaterialProperty _srcBlend;
        MaterialProperty _dstBlend;
        MaterialProperty _cullMode;
        MaterialProperty _zWrite;
        MaterialProperty _zTest;
        MaterialProperty _alphaClipping;
        MaterialProperty _cutoff;
        MaterialProperty _castShadow;

        // ---------------- MaterialProperty Cache : Stencil States ----------------
        MaterialProperty _stencilRef;
        MaterialProperty _stencilComp;
        MaterialProperty _stencilPass;
        MaterialProperty _stencilFail;
        MaterialProperty _stencilZFail;

        // ---------------- MaterialProperty Cache : Texture Character ----------------
        MaterialProperty _mainTex;
        MaterialProperty _rgba_Masking;
        MaterialProperty _rgba_Masking2;

        // ---------------- MaterialProperty Cache : Mask System ----------------
        MaterialProperty _useMaskTex1;
        MaterialProperty _useMaskTex2;
        MaterialProperty _metallicMaskCh;
        MaterialProperty _hairHighlightMaskCh;
        MaterialProperty _emissiveMaskCh;
        MaterialProperty _outlineMaskCh;
        MaterialProperty _specularMaskCh;

        // ---------------- MaterialProperty Cache : Base Character Colors ----------------
        MaterialProperty _texture_Brightness;
        MaterialProperty _baseColor;
        MaterialProperty _shadow_Color;

        // ---------------- MaterialProperty Cache : Face Shadow ----------------
        MaterialProperty _faceShadow;
        MaterialProperty _faceTex;
        MaterialProperty _debugUVFace;
        MaterialProperty _faceShadowUVScale;
        MaterialProperty _faceShadowUVOffsetX;
        MaterialProperty _faceShadowUVOffsetY;
        MaterialProperty _flipUVFace;

        // ---------------- MaterialProperty Cache : Base Character Lighting ----------------
        MaterialProperty _receiveShadow;
        MaterialProperty _shadowSoftness;
        MaterialProperty _shadowEdgeSmooth;
        MaterialProperty _rejectSelfShadow;
        MaterialProperty _selfShadowRejectDist;
        MaterialProperty _additionalLightIntensity;
        MaterialProperty _useContactShadow;

        // ---------------- MaterialProperty Cache : ToonRampShade ----------------
        MaterialProperty _toonRampSmoothness;
        MaterialProperty _shadowEdge;
        MaterialProperty _shadowEdgeColor;
        MaterialProperty _shadowEdgeIntensity;
        MaterialProperty _shadowEdgeWidth;

        // ---------------- MaterialProperty Cache : Outline ----------------
        MaterialProperty _oUTLINE_ZMODE;
        MaterialProperty _outlineWidth;
        MaterialProperty _outlineIntensity;
        MaterialProperty _outlineColor;
        MaterialProperty _outlineZOffset;
        MaterialProperty _outlineMask;

        // ---------------- MaterialProperty Cache : Transparency ----------------
        MaterialProperty _alphaValue;

        // ---------------- MaterialProperty Cache : Soft Light ----------------
        MaterialProperty _softLight;
        MaterialProperty _softLightHighlight;

        // ---------------- MaterialProperty Cache : Rim Light ----------------
        MaterialProperty _rimLight;
        MaterialProperty _rimColorMode;
        MaterialProperty _rimColor;
        MaterialProperty _intensityRimLight;
        MaterialProperty _stepRim;

        // ---------------- MaterialProperty Cache : Metallic ----------------
        MaterialProperty _useMetallic;
        MaterialProperty _gradientMetallic;
        MaterialProperty _metalNormalMap;
        MaterialProperty _metalIntensity;

        // ---------------- MaterialProperty Cache : Hair Highlight ----------------
        MaterialProperty _hair_Highlight;
        MaterialProperty _colorHair;
        MaterialProperty _hair_HighlightValue;

        // ---------------- MaterialProperty Cache : Specular ----------------
        MaterialProperty _specular;
        MaterialProperty _specularColor;
        MaterialProperty _specularIntensity;
        MaterialProperty _specularSharpness;
        MaterialProperty _specularThreshold;
        MaterialProperty _specularToonStep;

        // ---------------- MaterialProperty Cache : Emissive Glow ----------------
        MaterialProperty _emissiveGlow;
        MaterialProperty _emissive_Color;
        MaterialProperty _emissive_Intensity;

        // ---------------- MaterialProperty Cache : Dissolve Character ----------------
        MaterialProperty _dISSOLVE;
        MaterialProperty _texture2DDissolve;
        MaterialProperty _dissolveColor;
        MaterialProperty _dissolveValue;
        MaterialProperty _startDissolve;
        MaterialProperty _endDissolve;
        MaterialProperty _sizeGlowDissolve;

        // ---------------- MaterialProperty Cache : Character Darkening ----------------
        MaterialProperty _targetDarken;
        MaterialProperty _targetDarkenIntensity;
        MaterialProperty _targetDarkenLocal;

        // ---------------- MaterialProperty Cache : Indicator ----------------
        MaterialProperty _indicator;
        MaterialProperty _indicatorStrength;
        MaterialProperty _indicatorColor;
        MaterialProperty _fresnelPowerIndicator;

        // ---------------- MaterialProperty Cache : GetHit ----------------
        MaterialProperty _getHit;
        MaterialProperty _getHitStrength;
        MaterialProperty _getHitColor;
        MaterialProperty _fresnelPowerHit;

        // ---------------- MaterialProperty Cache : Light Sweep ----------------
        MaterialProperty _lIGHTSWEEP;
        MaterialProperty _lightSweepIntensity;
        MaterialProperty _lightSweepDuration;
        MaterialProperty _lightSweepDelay;
        MaterialProperty _lightSweepWidth;
        MaterialProperty _lightSweepSoftness;
        MaterialProperty _lightSweepStart;
        MaterialProperty _lightSweepEnd;
        MaterialProperty _lightSweepDirX;
        MaterialProperty _lightSweepDirY;
        MaterialProperty _lightSweepDirZ;

        // ---------------- MaterialProperty Cache : Upgrade Weapon ----------------
        MaterialProperty _uPGRADE;
        MaterialProperty _upgradeActive;
        MaterialProperty _upgradeColor;
        MaterialProperty _upgradeIntensity;
        MaterialProperty _upgradeMinBrightness;

        // ---------------- MaterialProperty Cache : Hair Transparent ----------------
        MaterialProperty _headBackHide;
        MaterialProperty _eyeBackfaceClip;
        MaterialProperty _hairBackfaceFade;
        MaterialProperty _headBackCutoff;
        MaterialProperty _headTopCutoff;
        MaterialProperty _hairFadeRange;

        // ---------------- MaterialProperty Cache : Normal Map ----------------
        MaterialProperty _useNormal;
        MaterialProperty _normalMap;
        MaterialProperty _normalStrength;

        bool _renderSettingsDirty;

        void CacheProperties(MaterialProperty[] properties)
        {
            _renderQueueSelector = FindProperty("_RenderQueueSelector", properties, false);

            _srcBlend = FindProperty("_SrcBlend", properties, false);
            _dstBlend = FindProperty("_DstBlend", properties, false);
            _cullMode = FindProperty("_CullMode", properties, false);
            _zWrite = FindProperty("_ZWrite", properties, false);
            _zTest = FindProperty("_ZTest", properties, false);
            _alphaClipping = FindProperty("_AlphaClipping", properties, false);
            _cutoff = FindProperty("_Cutoff", properties, false);
            _castShadow = FindProperty("_CastShadow", properties, false);

            _stencilRef = FindProperty("_StencilRef", properties, false);
            _stencilComp = FindProperty("_StencilComp", properties, false);
            _stencilPass = FindProperty("_StencilPass", properties, false);
            _stencilFail = FindProperty("_StencilFail", properties, false);
            _stencilZFail = FindProperty("_StencilZFail", properties, false);

            _mainTex = FindProperty("_MainTex", properties, false);
            _rgba_Masking  = FindProperty("_RGBA_Masking",  properties, false);
            _rgba_Masking2 = FindProperty("_RGBA_Masking2", properties, false);

            _useMaskTex1         = FindProperty("_USE_MASKTEX1",      properties, false);
            _useMaskTex2         = FindProperty("_USE_MASKTEX2",      properties, false);
            _metallicMaskCh      = FindProperty("_MetallicMaskCh",      properties, false);
            _hairHighlightMaskCh = FindProperty("_HairHighlightMaskCh", properties, false);
            _emissiveMaskCh      = FindProperty("_EmissiveMaskCh",      properties, false);
            _outlineMaskCh       = FindProperty("_OutlineMaskCh",       properties, false);
            _specularMaskCh      = FindProperty("_SpecularMaskCh",      properties, false);

            _texture_Brightness = FindProperty("_Texture_Brightness", properties, false);
            _baseColor = FindProperty("_BaseColor", properties, false);
            _shadow_Color = FindProperty("_Shadow_Color", properties, false);

            _faceShadow = FindProperty("_Switch_to_Face_Mode", properties, false);
            _faceTex = FindProperty("_FaceTex", properties, false);
            _debugUVFace = FindProperty("_DebugUvFace", properties, false);
            _faceShadowUVScale = FindProperty("_FaceShadowUVScale", properties, false);
            _faceShadowUVOffsetX = FindProperty("_FaceShadowUVOffsetX", properties, false);
            _faceShadowUVOffsetY = FindProperty("_FaceShadowUVOffsetY", properties, false);
            _flipUVFace = FindProperty("_FlipUvFace", properties, false);

            _receiveShadow = FindProperty("_ReceiveShadow", properties, false);
            _shadowSoftness = FindProperty("_ShadowSoftness", properties, false);
            _shadowEdgeSmooth = FindProperty("_ShadowEdgeSmooth", properties, false);
            _rejectSelfShadow = FindProperty("_RejectSelfShadow", properties, false);
            _selfShadowRejectDist = FindProperty("_SelfShadowRejectDist", properties, false);
            _additionalLightIntensity = FindProperty("_AdditionalLightIntensity", properties, false);
            _useContactShadow = FindProperty("_UseContactShadow", properties, false);

            _toonRampSmoothness = FindProperty("_ToonRampSmoothness", properties, false);
            _shadowEdge          = FindProperty("_ShadowEdge", properties, false);
            _shadowEdgeColor     = FindProperty("_ShadowEdgeColor", properties, false);
            _shadowEdgeIntensity = FindProperty("_ShadowEdgeIntensity", properties, false);
            _shadowEdgeWidth     = FindProperty("_ShadowEdgeWidth", properties, false);

            _oUTLINE_ZMODE = FindProperty("_OUTLINE_ZMODE", properties, false);
            _outlineWidth = FindProperty("_OutlineWidth", properties, false);
            _outlineIntensity = FindProperty("_OutlineIntensity", properties, false);
            _outlineColor = FindProperty("_OutlineColor", properties, false);
            _outlineZOffset = FindProperty("_OutlineZOffset", properties, false);
            _outlineMask = FindProperty("_OutlineMask", properties, false);

            _alphaValue = FindProperty("_AlphaValue", properties, false);

            _softLight = FindProperty("_SoftLight", properties, false);
            _softLightHighlight = FindProperty("_SoftLightHighlight", properties, false);

            _rimLight = FindProperty("_RimLight", properties, false);
            _rimColorMode = FindProperty("_RimColorMode", properties, false);
            _rimColor = FindProperty("_RimColor", properties, false);
            _intensityRimLight = FindProperty("_IntensityRimLight", properties, false);
            _stepRim = FindProperty("_StepRim", properties, false);

            _useMetallic = FindProperty("_UseMetallic", properties, false);
            _gradientMetallic = FindProperty("_GradientMetallic", properties, false);
            _metalNormalMap = FindProperty("_MetalNormalMap", properties, false);
            _metalIntensity = FindProperty("_MetalIntensity", properties, false);

            _hair_Highlight = FindProperty("_Hair_Highlight", properties, false);
            _colorHair = FindProperty("_ColorHair", properties, false);
            _hair_HighlightValue = FindProperty("_Hair_HighlightValue", properties, false);

            _specular           = FindProperty("_Specular",           properties, false);
            _specularColor      = FindProperty("_SpecularColor",      properties, false);
            _specularIntensity  = FindProperty("_SpecularIntensity",  properties, false);
            _specularSharpness  = FindProperty("_SpecularSharpness",  properties, false);
            _specularThreshold  = FindProperty("_SpecularThreshold",  properties, false);
            _specularToonStep   = FindProperty("_SpecularToonStep",   properties, false);

            _emissiveGlow = FindProperty("_EmissiveGlow", properties, false);
            _emissive_Color = FindProperty("_Emissive_Color", properties, false);
            _emissive_Intensity = FindProperty("_Emissive_Intensity", properties, false);

            _dISSOLVE = FindProperty("_DISSOLVE", properties, false);
            _texture2DDissolve = FindProperty("_Texture2DDissolve", properties, false);
            _dissolveColor = FindProperty("_DissolveColor", properties, false);
            _dissolveValue = FindProperty("_DissolveValue", properties, false);
            _startDissolve = FindProperty("_StartDissolve", properties, false);
            _endDissolve = FindProperty("_EndDissolve", properties, false);
            _sizeGlowDissolve = FindProperty("_SizeGlowDissolve", properties, false);

            _targetDarken = FindProperty("_TargetDarken", properties, false);
            _targetDarkenIntensity = FindProperty("_TargetDarkenIntensity", properties, false);
            _targetDarkenLocal = FindProperty("_TargetDarkenLocal", properties, false);

            _indicator = FindProperty("_Indicator", properties, false);
            _indicatorStrength = FindProperty("_IndicatorStrength", properties, false);
            _indicatorColor = FindProperty("_IndicatorColor", properties, false);
            _fresnelPowerIndicator = FindProperty("_FresnelPowerIndicator", properties, false);

            _getHit = FindProperty("_GetHit", properties, false);
            _getHitStrength = FindProperty("_GetHitStrength", properties, false);
            _getHitColor = FindProperty("_GetHitColor", properties, false);
            _fresnelPowerHit = FindProperty("_FresnelPowerHit", properties, false);

            _lIGHTSWEEP = FindProperty("_LIGHTSWEEP", properties, false);
            _lightSweepIntensity = FindProperty("_LightSweepIntensity", properties, false);
            _lightSweepDuration = FindProperty("_LightSweepDuration", properties, false);
            _lightSweepDelay = FindProperty("_LightSweepDelay", properties, false);
            _lightSweepWidth = FindProperty("_LightSweepWidth", properties, false);
            _lightSweepSoftness = FindProperty("_LightSweepSoftness", properties, false);
            _lightSweepStart = FindProperty("_LightSweepStart", properties, false);
            _lightSweepEnd = FindProperty("_LightSweepEnd", properties, false);
            _lightSweepDirX = FindProperty("_LightSweepDirX", properties, false);
            _lightSweepDirY = FindProperty("_LightSweepDirY", properties, false);
            _lightSweepDirZ = FindProperty("_LightSweepDirZ", properties, false);

            _uPGRADE = FindProperty("_UPGRADE", properties, false);
            _upgradeActive = FindProperty("_UpgradeActive", properties, false);
            _upgradeColor = FindProperty("_UpgradeColor", properties, false);
            _upgradeIntensity = FindProperty("_UpgradeIntensity", properties, false);
            _upgradeMinBrightness = FindProperty("_UpgradeMinBrightness", properties, false);

            _headBackHide = FindProperty("_HEAD_BACK_HIDE", properties, false);
            _eyeBackfaceClip = FindProperty("_EYE_BACKFACE_CLIP", properties, false);
            _hairBackfaceFade = FindProperty("_HAIR_BACKFACE_FADE", properties, false);
            _headBackCutoff = FindProperty("_HeadBackCutoff", properties, false);
            _headTopCutoff = FindProperty("_HeadTopCutoff", properties, false);
            _hairFadeRange = FindProperty("_HairFadeRange", properties, false);

            _useNormal       = FindProperty("_UseNormal",      properties, false);
            _normalMap       = FindProperty("_NormalMap",      properties, false);
            _normalStrength  = FindProperty("_NormalStrength", properties, false);
        }

        // ---------- Styles / Section ----------
        static void EnsureStyles()
        {
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(20, 20, 14, 16),
                    margin = new RectOffset(6, 6, 6, 6),
                    stretchWidth = true
                };
            }

            if (_headerButtonStyle == null)
            {
                _headerButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 26f
                };
            }

            if (_featureStatusStyle == null)
            {
                _featureStatusStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };
                _featureStatusStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            }

            if (_featureNameStyle == null)
            {
                _featureNameStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _featureNameStyle.normal.textColor = Color.white;
                _featureNameStyle.clipping = TextClipping.Clip;
            }

            if (_contentPadStyle == null)
            {
                _contentPadStyle = new GUIStyle
                {
                    padding = new RectOffset(0, 0, 6, 0)
                };
            }
        }

        // ---------------- Documentation links ----------------
        // Section headers can carry a "?" badge that opens the matching docs page. Slugs are
        // appended to DOCS_BASE_URL, so the whole set moves with one edit if the site changes.
        const string DOCS_BASE_URL = "https://zlz-studio.github.io/";

        // Section title -> documentation page. Keys must match the title passed to DrawSection;
        // a section with no entry here simply gets no DOCS badge. Sections that exist only in the
        // Inspector (Rendering, Stencil Settings, Texture Character) have no page yet.
        static readonly Dictionary<string, string> DOCS_SLUGS = new Dictionary<string, string>
        {
            { "Mask Layout",             "features/" },                        // Mask Layout + ZLZ Mask Packer
            { "Base Character Colors",   "features/Base-Character-Colors/" },
            { "Base Character Lighting", "features/Base-Character-Lighting/" },
            { "Face Shadow",             "features/faceshadow/" },
            { "ToonRampShade",           "features/ToonRampSmooth/" },         // also covers ShadowEdge
            { "Outline",                 "features/Outline/" },
            { "Normal Map",              "features/NormalMap/" },
            { "Metallic",                "features/Metallic/" },
            { "Specular",                "features/Specular/" },
            { "Hair Highlight",          "features/Hair-Highlight/" },
            { "Emissive Glow",           "features/Emissive/" },
            { "SoftLight",               "features/Soft-Light/" },
            { "RimLight",                "features/RimLight/" },
            { "Transparency",            "features/Transparency/" },
            { "Hair Transparent",        "features/hair-system/" },            // Hair Transparent + Hair Shadow
            { "Dissolve",                "features/Dissolve/" },
            { "TargetDarken",            "features/Target-Darken/" },
            { "Indicator",               "features/Indicator/" },
            { "GetHit",                  "features/GetHit/" },
            { "Use Light Sweep",         "features/LightSweep/" },
            { "Use Upgrade Weapon",      "features/Upgrade/" },
        };

        static string LookupDocsSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            return DOCS_SLUGS.TryGetValue(title, out string slug) ? slug : null;
        }

        static void OpenDocs(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return;
            Application.OpenURL(DOCS_BASE_URL + slug);
        }

        static GUIStyle _docsBadgeStyle;
        static GUIStyle DocsBadgeStyle
        {
            get
            {
                if (_docsBadgeStyle == null)
                {
                    _docsBadgeStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize  = 10,
                        padding   = new RectOffset(7, 7, 0, 0),
                    };
                }
                return _docsBadgeStyle;
            }
        }

        // Spelled out rather than a "?" icon: the question mark is a Unity convention, and a
        // customer opening this Inspector has no reason to know it leads to documentation.
        const string DOCS_BADGE_LABEL = "DOCS";

        // Badge sitting inside a section header. It is drawn (and its click handled) before the
        // header button so the header toggle never swallows the link.
        static void DrawDocsBadge(Rect rect, string slug)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, hover ? 0.30f : 0.18f));

            Color line = new Color(1f, 1f, 1f, hover ? 0.85f : 0.45f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), line);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);

            Color old = DocsBadgeStyle.normal.textColor;
            DocsBadgeStyle.normal.textColor = new Color(1f, 1f, 1f, hover ? 1f : 0.75f);
            GUI.Label(rect, DOCS_BADGE_LABEL, DocsBadgeStyle);
            DocsBadgeStyle.normal.textColor = old;

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (GUI.Button(rect, new GUIContent(string.Empty, "Open documentation"), GUIStyle.none))
                OpenDocs(slug);
        }

        static bool DrawHeaderToggle(string prefKey, string title, bool defaultOpen, string docsSlug = null)
        {
            bool isOpen = EditorPrefs.GetBool(prefKey, defaultOpen);

            EnsureStyles();

            Rect r = GUILayoutUtility.GetRect(0f, _headerButtonStyle.fixedHeight, GUILayout.ExpandWidth(true));
            bool isHover = r.Contains(Event.current.mousePosition);

            Color baseCol = isOpen ? HEADER_OPEN_BG : HEADER_CLOSED_BG;
            Color hoverCol = isOpen ? HEADER_OPEN_HOVER_BG : HEADER_CLOSED_HOVER_BG;

            EditorGUI.DrawRect(r, isHover ? hoverCol : baseCol);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;

            // Must come before the header button — whichever control is drawn first claims the click.
            if (!string.IsNullOrEmpty(docsSlug))
            {
                float badgeW = DocsBadgeStyle.CalcSize(new GUIContent(DOCS_BADGE_LABEL)).x;
                DrawDocsBadge(new Rect(r.xMax - badgeW - 8f, r.y + (r.height - 18f) * 0.5f, badgeW, 18f), docsSlug);
            }

            string label = (isOpen ? "▼ " : "▶ ") + title;

            if (GUI.Button(r, label, _headerButtonStyle))
            {
                isOpen = !isOpen;
                EditorPrefs.SetBool(prefKey, isOpen);
                GUI.changed = true;
            }

            GUI.backgroundColor = oldBg;

            return isOpen;
        }

        static void DrawSection(string prefKey, string title, bool defaultOpen, System.Action drawContent, string docsSlug = null)
        {
            EnsureStyles();

            EditorGUILayout.BeginVertical(_boxStyle, GUILayout.ExpandWidth(true));

            bool isOpen = DrawHeaderToggle(prefKey, title, defaultOpen, docsSlug ?? LookupDocsSlug(title));
            if (isOpen)
            {
                EditorGUILayout.BeginVertical(_contentPadStyle);
                drawContent?.Invoke();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        // ---------------- ZLZ Tone Mapping Setup Check ----------------
        const string DOCS_TONEMAPPING_URL = "https://zlz-studio.github.io/setup-character/Tone-Mapping/";

        static bool HasToneMappingRenderFeature()
        {
            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urpAsset == null) return false;

            // Use reflection to access rendererDataList (internal field)
            var rendererDataList = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(urpAsset) as UnityEngine.Rendering.Universal.ScriptableRendererData[];

            if (rendererDataList == null) return false;

            foreach (var rendererData in rendererDataList)
            {
                if (rendererData == null) continue;
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature is ZLZ_AnimeToneMappingFeature)
                        return true;
                }
            }
            return false;
        }

        static bool HasToneMappingVolume()
        {
            // Only works when a Scene is open
            foreach (var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                         UnityEngine.FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                if (volume.sharedProfile.Has<ZLZ_AnimeToneMap>())
                    return true;
            }
            return false;
        }

        static void DrawToneMappingSetupWarning()
        {
            bool hasFeature = HasToneMappingRenderFeature();
            bool hasVolume  = hasFeature && HasToneMappingVolume();

            if (hasFeature && hasVolume)
                return; // Setup complete, nothing to display.

            EditorGUILayout.Space(4);

            Color bgColor  = hasFeature ? new Color(0.39f, 0.32f, 0.10f) : new Color(0.36f, 0.13f, 0.13f);
            Color barColor = hasFeature ? new Color(0.78f, 0.55f, 0.08f) : new Color(0.75f, 0.20f, 0.20f);
            string message = hasFeature
                ? "ZLZ Anime Tone Mapping Volume not found in this Scene.\nPlease create a Global Volume and add the ZLZ Anime Tone Mapping override."
                : "ZLZ Anime ToneMapping Render Feature not found.\nPlease add it to your URP Renderer Asset.";

            Rect rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, bgColor);

            // Left color bar
            Rect barRect = new Rect(rect.x, rect.y, 4f, rect.height);
            EditorGUI.DrawRect(barRect, barColor);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);
            GUIStyle msgStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.85f, 0.75f) }
            };
            EditorGUILayout.LabelField(message, msgStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);
            if (GUILayout.Button("How to setup Tone Mapping →"))
                Application.OpenURL(DOCS_TONEMAPPING_URL);

            EditorGUILayout.Space(4);
        }

        // ---------------- OnGUI ----------------
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            CacheProperties(properties);
            _renderSettingsDirty = false;

            EditorGUI.BeginChangeCheck();

            DrawToneMappingSetupWarning();

            DrawSection(
                MakePrefsKey(materialEditor, "FeatureSelector"),
                "Features",
                defaultOpen: true,
                () => DrawFeatureButtonGrid_UIOnly(materialEditor)
            );

            DrawRendering(materialEditor);
            DrawStencil(materialEditor);
            DrawTexture(materialEditor);
            DrawBaseColors(materialEditor);
            DrawFaceShadow(materialEditor);
            DrawBaseLighting(materialEditor);
            DrawToonRamp(materialEditor);
            DrawOutline(materialEditor);
            DrawTransparency(materialEditor);
            DrawSoftLight(materialEditor);
            DrawRimLight(materialEditor);
            DrawNormalMap(materialEditor);
            DrawMetallic(materialEditor);
            DrawHairHighlight(materialEditor);
            DrawSpecular(materialEditor);
            DrawEmissiveGlow(materialEditor);
            DrawDissolveCharacter(materialEditor);
            DrawCharacterDarkening(materialEditor);
            DrawIndicator(materialEditor);
            DrawGetHit(materialEditor);
            DrawLIGHTSWEEP(materialEditor);
            DrawUPGRADE(materialEditor);

            DrawHairTransparent(materialEditor);

            DrawOtherProperties(materialEditor, properties);

            bool anyChanged = EditorGUI.EndChangeCheck();

            if (_renderSettingsDirty)
            {
                foreach (Material m in materialEditor.targets)
                    UpdateRenderSettings(m);
            }

            if (anyChanged)
                RefreshMaskTextureKeywordsForTargets(materialEditor);
        }

        // ---------- Sections ----------
        void DrawRendering(MaterialEditor me)
        {
            if (_renderQueueSelector == null) return;

            DrawSection(MakePrefsKey(me, "Rendering"), "Rendering", true, () =>
            {
                EditorGUI.BeginChangeCheck();

                me.ShaderProperty(_renderQueueSelector, "Render Queue");
                if (_srcBlend != null) me.ShaderProperty(_srcBlend, "Source Blend");
                if (_dstBlend != null) me.ShaderProperty(_dstBlend, "Destination Blend");
                if (_cullMode != null) me.ShaderProperty(_cullMode, "Cull Mode");
                if (_zWrite != null) me.ShaderProperty(_zWrite, "Z Write (Depth Write)");
                if (_zTest != null) me.ShaderProperty(_zTest, "Z Test (Depth Test)");
                if (_alphaClipping != null) me.ShaderProperty(_alphaClipping, "Alpha Clipping");
                if (_cutoff != null) me.ShaderProperty(_cutoff, "Alpha Cutoff");
                if (_castShadow != null) me.ShaderProperty(_castShadow, "Cast Shadow");

                if (EditorGUI.EndChangeCheck())
                    _renderSettingsDirty = true;
            });
        }

        void DrawStencil(MaterialEditor me)
        {
            if (_stencilRef == null) return;

            DrawSection(MakePrefsKey(me, "Stencil"), "Stencil Settings", true, () =>
            {
                me.ShaderProperty(_stencilRef, "Stencil Ref");
                if (_stencilComp != null) me.ShaderProperty(_stencilComp, "Stencil Comp");
                if (_stencilPass != null) me.ShaderProperty(_stencilPass, "Stencil Pass");
                if (_stencilFail != null) me.ShaderProperty(_stencilFail, "Stencil Fail");
                if (_stencilZFail != null) me.ShaderProperty(_stencilZFail, "Stencil ZFail");
            });
        }

        void DrawTexture(MaterialEditor me)
        {
            if (_mainTex == null) return;

            DrawSection(MakePrefsKey(me, "TextureCharacter"), "Texture Character", true, () =>
            {
                me.ShaderProperty(_mainTex, "Main Texture (RGB)");
            });

            DrawMaskLayout(me);
        }

        // --- Mask Layout / Channel Mapping section ---
        void DrawMaskLayout(MaterialEditor me)
        {
            if (_rgba_Masking == null) return;

            DrawSection(MakePrefsKey(me, "MaskLayout"), "Mask Layout", true, () =>
            {
                bool needsM1 = AnyEnabledFeatureUsesMaskBank(me, 0);
                bool needsM2 = AnyEnabledFeatureUsesMaskBank(me, 1);

                // --- Mask texture slots ---
                if (needsM1)
                {
                    me.ShaderProperty(_rgba_Masking, "Feature Mask 1 (RGBA)");
                    if (IsTextureMissing(_rgba_Masking))
                    {
                        DrawHelpBox(
                            "Mask 1 is referenced by an active feature but not assigned. Features will sample 0 (no effect).",
                            MessageType.Warning);
                    }
                    DrawMaskChannelLegend(me, bank: 0);
                }
                else
                {
                    DrawHelpBox("Mask 1 not used by any active feature — texture sampling stripped.", MessageType.None);
                }

                EditorGUILayout.Space(6);

                if (needsM2 && _rgba_Masking2 != null)
                {
                    me.ShaderProperty(_rgba_Masking2, "Feature Mask 2 (RGBA)");
                    if (IsTextureMissing(_rgba_Masking2))
                    {
                        DrawHelpBox(
                            "Mask 2 is referenced by an active feature but not assigned. Features will sample 0 (no effect).",
                            MessageType.Warning);
                    }
                    DrawMaskChannelLegend(me, bank: 1);
                }
                else
                {
                    DrawHelpBox("Mask 2 not used by any active feature — texture sampling stripped.", MessageType.None);
                }

                // --- Channel Mapping (always visible for power users) ---
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Channel Mapping", InfoTitle);
                DrawChannelMappingRow(me, _useMetallic,    "Metallic",       _metallicMaskCh,      defaultChannelOnReenable: 0); // M1.R
                DrawChannelMappingRow(me, _hair_Highlight, "Hair Highlight", _hairHighlightMaskCh, defaultChannelOnReenable: 1); // M1.G
                DrawChannelMappingRow(me, _emissiveGlow,   "Emissive",       _emissiveMaskCh,      defaultChannelOnReenable: 2); // M1.B
                DrawChannelMappingRow(me, _outlineMask,    "Outline",        _outlineMaskCh,       defaultChannelOnReenable: 3, lockedOn: true, customSetter: idx => SetOutlineMaskChannel(me, idx)); // M1.A — locked-on (always available)
                DrawChannelMappingRow(me, _specular,       "Specular",       _specularMaskCh,      defaultChannelOnReenable: 4); // M2.R only once M1 is full

                // --- Pack Masks button ---
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Pack Masks...", GUILayout.Height(22), GUILayout.MaxWidth(240)))
                    {
                        var mat = (me.target is Material m) ? m : null;
                        if (mat != null) ZLZ_MaskPackerWindow.ShowForMaterial(mat);
                        else ZLZ_MaskPackerWindow.ShowWindow();
                    }
                }
            });
        }

        // Show "R: Metallic   G: Hair Highlight   ..." for the given bank (0 = M1, 1 = M2)
        void DrawMaskChannelLegend(MaterialEditor me, int bank)
        {
            int baseIdx = bank * 4;
            string[] slot = new string[4] { "-", "-", "-", "-" };
            TryFillSlot(slot, baseIdx, _useMetallic,    _metallicMaskCh,      "Metallic");
            TryFillSlot(slot, baseIdx, _hair_Highlight, _hairHighlightMaskCh, "Hair Highlight");
            TryFillSlot(slot, baseIdx, _emissiveGlow,   _emissiveMaskCh,      "Emissive");
            TryFillSlot(slot, baseIdx, _outlineMask,    _outlineMaskCh,       "Outline");
            TryFillSlot(slot, baseIdx, _specular,       _specularMaskCh,      "Specular");

            EditorGUI.indentLevel++;
            DrawMaskChannelHint("R", slot[0]);
            DrawMaskChannelHint("G", slot[1]);
            DrawMaskChannelHint("B", slot[2]);
            DrawMaskChannelHint("A", slot[3]);
            EditorGUI.indentLevel--;
        }

        static void TryFillSlot(string[] slot, int bankBaseIdx, MaterialProperty featureProp, MaterialProperty channelProp, string featureName)
        {
            if (!IsFeatureEnabled(featureProp)) return;
            if (channelProp == null) return;

            int idx = Mathf.Clamp((int)channelProp.floatValue, 0, 8);
            if (idx >= CHANNEL_NONE) return; // None - no slot to fill
            if (idx < bankBaseIdx || idx >= bankBaseIdx + 4) return;

            int local = idx - bankBaseIdx;
            if (slot[local] != "-")
                slot[local] = slot[local] + " + " + featureName;
            else
                slot[local] = featureName;
        }

        // Channel row: [ON pill] [feature name] [Use Mask checkbox] [channel dropdown or "whole mesh" hint]
        // lockedOn: feature is always-on (e.g. Outline) so the row never greys out.
        // customSetter: overrides SetChannelOnTargets when the feature needs extra keyword
        // syncing (Outline routes through SetOutlineMaskChannel to drive _OUTLINEMASK_ON).
        // defaultChannelOnReenable is only a preference — PickFreeChannel keeps the mask inside
        // Mask 1 while it still has room, so enabling a mask never pulls in Mask 2 needlessly.
        void DrawChannelMappingRow(MaterialEditor me, MaterialProperty featureProp, string featureName, MaterialProperty channelProp, int defaultChannelOnReenable, bool lockedOn = false, System.Action<int> customSetter = null)
        {
            if (channelProp == null) return;

            bool isEnabled = lockedOn || IsFeatureEnabled(featureProp);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Status pill
                string status = isEnabled ? "ON " : "OFF";
                Color old = GUI.color;
                GUI.color = isEnabled ? new Color(0.7f, 1f, 0.7f) : new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label(status, GUILayout.Width(34));
                GUI.color = old;

                using (new EditorGUI.DisabledScope(!isEnabled))
                {
                    GUILayout.Label(featureName, GUILayout.Width(110));

                    int idx = Mathf.Clamp((int)channelProp.floatValue, 0, 8);
                    // For a locked-on feature (Outline) the keyword is the source of truth — its
                    // channel index defaults to a real channel even while the mask is off.
                    bool useMask = lockedOn ? IsFeatureEnabled(featureProp) : (idx < CHANNEL_NONE);
                    if (useMask && idx >= CHANNEL_NONE) idx = PickFreeChannel(channelProp, defaultChannelOnReenable);

                    // Use Mask checkbox
                    EditorGUI.BeginChangeCheck();
                    bool newUseMask = EditorGUILayout.ToggleLeft("Use Mask", useMask, GUILayout.Width(90));
                    if (EditorGUI.EndChangeCheck() && newUseMask != useMask)
                    {
                        me.RegisterPropertyChangeUndo("Toggle " + featureName + " Mask");
                        int newIdx = newUseMask ? PickFreeChannel(channelProp, defaultChannelOnReenable) : CHANNEL_NONE;
                        if (customSetter != null) customSetter(newIdx);
                        else SetChannelOnTargets(me, channelProp, newIdx);
                        useMask = newUseMask;
                        idx = newIdx;
                    }

                    if (useMask)
                    {
                        // Channel dropdown
                        EditorGUI.BeginChangeCheck();
                        int dropIdx = Mathf.Clamp(idx, 0, 7);
                        int newDropIdx = EditorGUILayout.Popup(dropIdx, CHANNEL_LABELS_DROPDOWN);
                        if (EditorGUI.EndChangeCheck() && newDropIdx != dropIdx)
                        {
                            me.RegisterPropertyChangeUndo("Change " + featureName + " Mask Channel");
                            if (customSetter != null) customSetter(newDropIdx);
                            else SetChannelOnTargets(me, channelProp, newDropIdx);
                        }
                    }
                    else
                    {
                        GUILayout.Label("→ applies to whole mesh", InfoBody);
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        void SetChannelOnTargets(MaterialEditor me, MaterialProperty channelProp, int newIdx)
        {
            channelProp.floatValue = newIdx;
            foreach (Material mat in me.targets)
                if (mat != null && mat.HasProperty(channelProp.name))
                    mat.SetFloat(channelProp.name, newIdx);
            RefreshMaskTextureKeywordsForTargets(me);
        }

        // Chooses the channel a feature lands on when its mask is switched on. Mask 1 is filled
        // before Mask 2 is touched, so a material using four features or fewer never pays for a
        // second mask texture sample — and the Mask 2 slot never appears out of nowhere.
        // `preferred` still wins inside the first bank that has room, which preserves the legacy
        // layout (Metallic M1.R, Hair M1.G, Emissive M1.B, Outline M1.A) on a fresh material.
        // Only channels held by *enabled* features count as taken; a disabled feature's stored
        // channel is free to reuse, and the Mask Layout legend shows any sharing as "A + B".
        int PickFreeChannel(MaterialProperty selfChannelProp, int preferred)
        {
            bool[] taken = new bool[8];
            MarkChannelTaken(taken, selfChannelProp, _useMetallic,    _metallicMaskCh);
            MarkChannelTaken(taken, selfChannelProp, _hair_Highlight, _hairHighlightMaskCh);
            MarkChannelTaken(taken, selfChannelProp, _emissiveGlow,   _emissiveMaskCh);
            MarkChannelTaken(taken, selfChannelProp, _outlineMask,    _outlineMaskCh);
            MarkChannelTaken(taken, selfChannelProp, _specular,       _specularMaskCh);

            preferred = Mathf.Clamp(preferred, 0, 7);

            for (int bank = 0; bank < 2; bank++)
            {
                int lo = bank * 4;
                if (preferred >= lo && preferred < lo + 4 && !taken[preferred]) return preferred;
                for (int i = lo; i < lo + 4; i++)
                    if (!taken[i]) return i;
            }

            return preferred; // all 8 channels in use — sharing one is still valid
        }

        // Marks the channel held by featureProp, skipping the feature we are assigning for.
        static void MarkChannelTaken(bool[] taken, MaterialProperty selfChannelProp, MaterialProperty featureProp, MaterialProperty channelProp)
        {
            if (channelProp == null || channelProp == selfChannelProp) return;
            if (!IsFeatureEnabled(featureProp)) return;

            int idx = Mathf.Clamp((int)channelProp.floatValue, 0, 8);
            if (idx < CHANNEL_NONE) taken[idx] = true;
        }

        // Returns true if any enabled feature points to channel index in [bank*4, bank*4+3]
        bool AnyEnabledFeatureUsesMaskBank(MaterialEditor me, int bank)
        {
            int lo = bank * 4;
            int hi = lo + 3;
            return FeatureUsesBank(_useMetallic,    _metallicMaskCh,      lo, hi)
                || FeatureUsesBank(_hair_Highlight, _hairHighlightMaskCh, lo, hi)
                || FeatureUsesBank(_emissiveGlow,   _emissiveMaskCh,      lo, hi)
                || FeatureUsesBank(_outlineMask,    _outlineMaskCh,       lo, hi)
                || FeatureUsesBank(_specular,       _specularMaskCh,      lo, hi);
        }

        static bool FeatureUsesBank(MaterialProperty featureProp, MaterialProperty channelProp, int lo, int hi)
        {
            if (!IsFeatureEnabled(featureProp)) return false;
            if (channelProp == null) return false;
            int idx = Mathf.Clamp((int)channelProp.floatValue, 0, 8);
            if (idx >= CHANNEL_NONE) return false;
            return idx >= lo && idx <= hi;
        }

        void RefreshMaskTextureKeywordsForTargets(MaterialEditor me)
        {
            if (me == null) return;
            foreach (Material mat in me.targets)
            {
                if (mat == null) continue;
                RefreshMaskTextureKeywords(mat);
            }
        }

        public static void RefreshMaskTextureKeywords(Material mat)
        {
            if (mat == null) return;

            bool needsM1 = MaterialNeedsBank(mat, 0);
            bool needsM2 = MaterialNeedsBank(mat, 1);

            SetKeywordEnum_SingleMaterial(mat, "_USE_MASKTEX1", needsM1, KW_USE_MASKTEX1_ON, KW_USE_MASKTEX1_OFF);
            SetKeywordEnum_SingleMaterial(mat, "_USE_MASKTEX2", needsM2, KW_USE_MASKTEX2_ON, KW_USE_MASKTEX2_OFF);
        }

        static bool MaterialNeedsBank(Material mat, int bank)
        {
            int lo = bank * 4;
            int hi = lo + 3;
            return MatFeatureUsesBank(mat, "_USEMETALLIC_ON",    "_MetallicMaskCh",      lo, hi)
                || MatFeatureUsesBank(mat, "_HAIR_HIGHLIGHT_ON", "_HairHighlightMaskCh", lo, hi)
                || MatFeatureUsesBank(mat, "_EMISSIVEGLOW_ON",   "_EmissiveMaskCh",      lo, hi)
                || MatFeatureUsesBank(mat, "_OUTLINEMASK_ON",    "_OutlineMaskCh",       lo, hi)
                || MatFeatureUsesBank(mat, "_SPECULAR_ON",       "_SpecularMaskCh",      lo, hi);
        }

        static bool MatFeatureUsesBank(Material mat, string kwOn, string channelProp, int lo, int hi)
        {
            if (!mat.IsKeywordEnabled(kwOn)) return false;
            if (!mat.HasProperty(channelProp)) return false;
            int idx = Mathf.Clamp((int)mat.GetFloat(channelProp), 0, 8);
            if (idx >= CHANNEL_NONE) return false;
            return idx >= lo && idx <= hi;
        }


        void DrawBaseColors(MaterialEditor me)
        {
            if (_texture_Brightness == null) return;

            DrawSection(MakePrefsKey(me, "BaseCharacterColors"), "Base Character Colors", true, () =>
            {

                me.ShaderProperty(_texture_Brightness, "Texture Brightness");
                if (_baseColor != null) me.ShaderProperty(_baseColor, "BaseColor");
                if (_shadow_Color != null) me.ShaderProperty(_shadow_Color, "Shadow Color");
            });
        }

        void DrawFaceShadow(MaterialEditor me)
        {
            if (_faceShadow  == null) return;
            if (!IsFeatureEnabled(_faceShadow)) return;

            DrawSection(MakePrefsKey(me, "FaceShadow"), "Face Shadow", true, () =>
            {
                if (_faceTex != null) me.ShaderProperty(_faceTex, "Face Texture (RGB)");
                if (IsTextureMissing(_faceTex))
                {
                    DrawHelpBox(
                        "Face Texture is missing.\nAssign Face Texture to use Face Shadow.",
                        MessageType.Warning
                    );
                }
                if (_debugUVFace != null) me.ShaderProperty(_debugUVFace, "Debug UV Face");
                if (_faceShadowUVScale != null) me.ShaderProperty(_faceShadowUVScale, "Face Shadow UV Scale");
                if (_faceShadowUVOffsetX != null) me.ShaderProperty(_faceShadowUVOffsetX, "Face Shadow UV Offset X");
                if (_faceShadowUVOffsetY != null) me.ShaderProperty(_faceShadowUVOffsetY, "Face Shadow UV Offset Y");
                if (_flipUVFace != null) me.ShaderProperty(_flipUVFace, "Flip UV Face");
            });
        }

        void DrawBaseLighting(MaterialEditor me)
        {
            if (_receiveShadow == null) return;

            DrawSection(MakePrefsKey(me, "BaseCharacterLighting"), "Base Character Lighting", true, () =>
            {
                if (_receiveShadow != null) me.ShaderProperty(_receiveShadow, "Receive Shadow");

                bool receiveOn = _receiveShadow != null && _receiveShadow.floatValue > 0.5f;
                if (receiveOn)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        if (_shadowSoftness != null) me.ShaderProperty(_shadowSoftness, new GUIContent("Shadow Softness",
                            "Widens the shadow to light transition in value space (needs URP Soft Shadows enabled to do much). 0 passes the cascade sample through; higher values give a softer ramp instead of a near-binary edge."));
                        if (_shadowEdgeSmooth != null) me.ShaderProperty(_shadowEdgeSmooth, new GUIContent("Shadow Edge Smooth",
                            "Screen-space feather around the shadow boundary. 0 is a single-pixel anti-alias; raise it to stretch the boundary across several pixels. The wider band hides the cascade-texel jitter that shows when the character animates, and gives a softer painterly look."));

                        if (_rejectSelfShadow != null) me.ShaderProperty(_rejectSelfShadow, new GUIContent("Reject Self-Shadow",
                            "Drops the contribution from occluders that sit on the character itself (own hair, arms, cloth) while keeping shadows cast by the surrounding scene. Removes the cascade-texel jitter that is most visible on a detailed animated character."));

                        bool rejectOn = _rejectSelfShadow != null && _rejectSelfShadow.floatValue > 0.5f;
                        if (rejectOn)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                if (_selfShadowRejectDist != null) me.ShaderProperty(_selfShadowRejectDist, new GUIContent("Reject Distance",
                                    "Threshold in shadow normalized depth that separates self-occluders from external ones. Raise this if environment shadows close to the character get dropped; lower it if the character's own occluders still show through."));
                            }
                        }
                    }
                }

                if (_useContactShadow != null) me.ShaderProperty(_useContactShadow, new GUIContent("Contact Self-Shadow",
                    "Screen-space self-shadow (hair→face, arm→body). Requires the 'ZLZ Character Contact Shadow' Renderer Feature on the URP Renderer."));
                if (_additionalLightIntensity != null) me.ShaderProperty(_additionalLightIntensity, "AdditionalLightIntensity");
            });
        }

        void DrawToonRamp(MaterialEditor me)
        {
            if (_toonRampSmoothness == null) return;

            DrawSection(MakePrefsKey(me, "ToonRampShade"), "ToonRampShade", true, () =>
            {
                me.ShaderProperty(_toonRampSmoothness, "ToonRampSmooth");

                if (_shadowEdge != null)
                {
                    EditorGUILayout.Space(6);
                    me.ShaderProperty(_shadowEdge, new GUIContent("Shadow Edge",
                        "Core shadow — a darker band at the light/shadow boundary (terminator). Adds form and volume, strongest on hair and rounded surfaces."));

                    if (IsFeatureEnabled(_shadowEdge))
                    {
                        if (_shadowEdgeColor != null)     me.ShaderProperty(_shadowEdgeColor, "Shadow Edge Color");
                        if (_shadowEdgeIntensity != null) me.ShaderProperty(_shadowEdgeIntensity, "Shadow Edge Intensity");
                        if (_shadowEdgeWidth != null)     me.ShaderProperty(_shadowEdgeWidth, "Shadow Edge Width");
                    }
                }
            });
        }

        void DrawOutline(MaterialEditor me)
        {
            if (_outlineWidth == null) return;

            DrawSection(MakePrefsKey(me, "Outline"), "Outline", true, () =>
            {
                var state = DetectOutlineState();
                string stateLabel = state == OutlineState.Both       ? "Hull + Screen Space"
                                  : state == OutlineState.Hull        ? "Hull"
                                  : state == OutlineState.ScreenSpace ? "Screen Space"
                                  :                                     "None";

                // Current outline + where to switch it.
                DrawInfoBox(
                    $"Active outline:   <color=#EBA13C>{stateLabel}</color>",
                    "Choose the outline type in the Character Dashboard   ▸   ZLZ Outline.");

                // Screen-Space only: the width / color / Z settings below drive the Hull
                // outline, which isn't what's rendering — point to where those options live.
                // The Outline Mask still applies (Screen Space reads it too), so it is shown
                // below in every mode.
                if (state == OutlineState.ScreenSpace)
                {
                    EditorGUILayout.Space(5);
                    DrawInfoBox(
                        "Screen Space Outline is configured on its Renderer Feature.",
                        "Open it in the URP Renderer asset, or switch the outline type via " +
                        "Character Dashboard   ▸   ZLZ Outline.");
                }
                else
                {
                    EditorGUILayout.Space(6);

                    // ---- Hull outline material settings ----
                    if (_oUTLINE_ZMODE != null)
                        me.ShaderProperty(_oUTLINE_ZMODE, "Outline Z Mode");
                    me.ShaderProperty(_outlineWidth, "Outline Width");
                    if (_outlineIntensity != null)
                        me.ShaderProperty(_outlineIntensity, "Outline Intensity");
                    if (_outlineColor != null)
                        me.ShaderProperty(_outlineColor, "Outline Color");
                    if (_outlineZOffset != null && IsOutlineZModeLegacy(me))
                        me.ShaderProperty(_outlineZOffset, "Outline Z Offset");
                }

                // ---- Outline Mask (shared by Hull + Screen Space) ----
                // Outline is a locked / always-on feature, so its mask is reachable at all
                // times — no need to enable another feature first. One control drives the
                // shader keyword, the channel, and the mask-texture slot in Mask Layout.
                EditorGUILayout.Space(6);
                DrawOutlineMaskControl(me);
            });
        }

        // Single self-contained Outline Mask control. Because the outline is locked-on it is
        // always available, and it feeds BOTH the Hull pass and the Screen Space Outline
        // (which reads the mask from the DepthNormals alpha). Picking a channel turns the mask
        // on and reveals its texture slot in Mask Layout; "None" outlines the whole mesh.
        void DrawOutlineMaskControl(MaterialEditor me)
        {
            if (_outlineMaskCh == null) return;

            EditorGUILayout.LabelField("Outline Mask", InfoTitle);

            // The _OUTLINEMASK_ON keyword is the source of truth (the channel index defaults to
            // M1.A even when the mask is off, so it cannot be trusted on its own).
            bool useMask = IsFeatureEnabled(_outlineMask);
            int idx = Mathf.Clamp((int)_outlineMaskCh.floatValue, 0, 8);
            if (useMask && idx >= CHANNEL_NONE) idx = PickFreeChannel(_outlineMaskCh, 3); // keyword on but channel cleared -> prefer M1.A

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool newUseMask = EditorGUILayout.ToggleLeft("Use Mask", useMask, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck() && newUseMask != useMask)
                {
                    me.RegisterPropertyChangeUndo("Toggle Outline Mask");
                    int newIdx = newUseMask ? (idx < CHANNEL_NONE ? idx : PickFreeChannel(_outlineMaskCh, 3)) : CHANNEL_NONE; // keeps the previous channel, else prefers M1.A
                    SetOutlineMaskChannel(me, newIdx);
                    useMask = newUseMask;
                    idx = newIdx;
                }

                if (useMask)
                {
                    EditorGUI.BeginChangeCheck();
                    int dropIdx = Mathf.Clamp(idx, 0, 7);
                    int newDropIdx = EditorGUILayout.Popup(dropIdx, CHANNEL_LABELS_DROPDOWN);
                    if (EditorGUI.EndChangeCheck() && newDropIdx != dropIdx)
                    {
                        me.RegisterPropertyChangeUndo("Change Outline Mask Channel");
                        SetOutlineMaskChannel(me, newDropIdx);
                    }
                }
                else
                {
                    GUILayout.Label("→ outline on whole mesh", InfoBody);
                    GUILayout.FlexibleSpace();
                }
            }

            if (useMask)
            {
                DrawFeatureMaskReminder(_outlineMaskCh, "Outline");
                DrawHelpBox(
                    "Channel value:  1 (White) → Show Outline,  0 (Black) → Hide Outline.\n" +
                    "Applies to both Hull and Screen Space outlines.",
                    MessageType.None);
            }
        }

        // Sets the Outline mask channel AND syncs the _OUTLINEMASK_ON keyword (channel in 0..7
        // -> on, None -> off). The keyword gates the mask sample in both the Hull pass and the
        // DepthNormals pass, and drives the mask-texture slot visibility in Mask Layout.
        void SetOutlineMaskChannel(MaterialEditor me, int newIdx)
        {
            if (_outlineMaskCh == null) return;

            bool useMask = newIdx < CHANNEL_NONE;

            _outlineMaskCh.floatValue = newIdx;
            foreach (Material mat in me.targets)
                if (mat != null && mat.HasProperty(_outlineMaskCh.name))
                    mat.SetFloat(_outlineMaskCh.name, newIdx);

            SetKeywordEnum(me, _outlineMask, useMask, KW_OUTLINEMASK_ON, KW_OUTLINEMASK_OFF);
            RefreshMaskTextureKeywordsForTargets(me);
        }

        enum OutlineState { None, Hull, ScreenSpace, Both }

        // Reads the active URP renderer to report which outline feature(s) are live.
        // Mirrors the Character Dashboard's outline mode so the material inspector and
        // the Dashboard always agree on what's rendering.
        static OutlineState DetectOutlineState()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return OutlineState.None;

            var so   = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return OutlineState.None;
            int idx = so.FindProperty("m_DefaultRendererIndex")?.intValue ?? 0;
            idx = Mathf.Clamp(idx, 0, list.arraySize - 1);
            var rd = list.GetArrayElementAtIndex(idx).objectReferenceValue as ScriptableRendererData;
            if (rd == null) return OutlineState.None;

            bool hull = false, sso = false;
            foreach (var f in rd.rendererFeatures)
            {
                if (f == null || !f.isActive) continue;
                if      (f is ZLZ_OutlineRendererFeature)    hull = true;
                else if (f is ZLZ_ScreenSpaceOutlineFeature) sso  = true;
            }
            return hull && sso ? OutlineState.Both
                 : hull        ? OutlineState.Hull
                 : sso         ? OutlineState.ScreenSpace
                 :               OutlineState.None;
        }

        // ── Shared readable text for the Shader GUI ──────────────────────────
        // Larger than the default HelpBox / miniLabel text, with clear line spacing.
        static GUIStyle _infoBoxTitle, _infoBoxBody, _infoBoxWarn;

        static void EnsureInfoStyles()
        {
            if (_infoBoxTitle != null) return;
            _infoBoxTitle = new GUIStyle(EditorStyles.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true, richText = true };
            _infoBoxBody = new GUIStyle(EditorStyles.label)
            { fontSize = 12, wordWrap = true, richText = true };
            _infoBoxBody.normal.textColor = new Color(0.80f, 0.80f, 0.84f, 1f);
            _infoBoxWarn = new GUIStyle(EditorStyles.label)
            { fontSize = 12, wordWrap = true, richText = true };
            _infoBoxWarn.normal.textColor = new Color(0.96f, 0.76f, 0.36f, 1f);
        }

        // Larger bold sub-header (use instead of EditorStyles.boldLabel).
        static GUIStyle InfoTitle { get { EnsureInfoStyles(); return _infoBoxTitle; } }
        // Larger body label (use instead of EditorStyles.miniLabel / default info text).
        static GUIStyle InfoBody  { get { EnsureInfoStyles(); return _infoBoxBody; } }

        // Titled info box: bold title + wrapped body in a boxed background.
        static void DrawInfoBox(string title, string body)
        {
            EnsureInfoStyles();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);
            GUILayout.Label(title, _infoBoxTitle);
            GUILayout.Space(5);
            GUILayout.Label(body, _infoBoxBody);
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // Readable replacement for EditorGUILayout.HelpBox — same boxed look, larger
        // text; warnings / errors keep an amber tint so they still read as alerts.
        static void DrawHelpBox(string body, MessageType type = MessageType.None)
        {
            EnsureInfoStyles();
            var style = (type == MessageType.Warning || type == MessageType.Error) ? _infoBoxWarn : _infoBoxBody;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(3);
            GUILayout.Label(body, style);
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        bool IsOutlineZModeLegacy(MaterialEditor me)
        {
            foreach (Material mat in me.targets)
                if (mat.IsKeywordEnabled("_OUTLINE_ZMODE_PLANARSAFE"))
                    return false;

            return true;
        }

        void DrawTransparency(MaterialEditor me)
        {
            if (_alphaValue == null) return;

            DrawSection(MakePrefsKey(me, "Transparency"), "Transparency", true, () =>
            {
                me.ShaderProperty(_alphaValue, "AlphaValue");

                DrawInfoBox(
                    "How Transparency Works",
                    "•  <b>AlphaValue</b>  →  fades the whole mesh\n" +
                    "•  <b>Main Texture Alpha</b>  →  fades only where you paint\n" +
                    "      Black = transparent,   White = opaque\n" +
                    "•  Final alpha  =  Main Texture Alpha  ×  AlphaValue\n" +
                    "•  The Feature Mask textures are not used here");
            });
        }

        void DrawSoftLight(MaterialEditor me)
        {
            if (_softLight == null) return;

            if (!IsFeatureEnabled(_softLight)) return;

            DrawSection(MakePrefsKey(me, "SoftLight"), "SoftLight", true, () =>
            {
                if (_softLightHighlight != null) me.ShaderProperty(_softLightHighlight, "Soft Light Highlight");

            });
        }

        void DrawRimLight(MaterialEditor me)
        {
            if (_rimLight == null) return;

            if (!IsFeatureEnabled(_rimLight)) return;

            DrawSection(MakePrefsKey(me, "RimLight"), "RimLight", true, () =>
            {
                if (_rimColorMode != null) me.ShaderProperty(_rimColorMode, "Rim Color Mode");
                if (_rimColor != null) me.ShaderProperty(_rimColor, "RimColor");
                if (_intensityRimLight != null) me.ShaderProperty(_intensityRimLight, "IntensityRimLight");
                if (_stepRim != null) me.ShaderProperty(_stepRim, "Rim Steps (Toon)");
            });
        }

        void DrawNormalMap(MaterialEditor me)
        {
            if (_useNormal == null) return;
            if (!IsFeatureEnabled(_useNormal)) return;

            DrawSection(MakePrefsKey(me, "NormalMap"), "Normal Map", true, () =>
            {
                if (_normalMap != null)  me.ShaderProperty(_normalMap,  "Normal Map");
                if (_normalStrength != null) me.ShaderProperty(_normalStrength, "Normal Strength");
            });
        }

        void DrawMetallic(MaterialEditor me)
        {
            if (_useMetallic == null) return;

            if (!IsFeatureEnabled(_useMetallic)) return;

            DrawSection(MakePrefsKey(me, "Metallic"), "Metallic", true, () =>
            {
                DrawFeatureMaskReminder(_metallicMaskCh, "Metallic");

                if (_gradientMetallic != null) me.ShaderProperty(_gradientMetallic, "GradientMetallic");
                if (_metalNormalMap != null) me.ShaderProperty(_metalNormalMap, "MetalNormalMap");
                if (_metalIntensity != null) me.ShaderProperty(_metalIntensity, "IntensityMetal");
            });
        }

        void DrawHairHighlight(MaterialEditor me)
        {
            if (_hair_Highlight == null) return;

            if (!IsFeatureEnabled(_hair_Highlight)) return;

            DrawSection(MakePrefsKey(me, "Hair Highlight"), "Hair Highlight", true, () =>
            {
                DrawFeatureMaskReminder(_hairHighlightMaskCh, "Hair Highlight");

                if (_colorHair != null) me.ShaderProperty(_colorHair, "ColorHair");
                if (_hair_HighlightValue != null) me.ShaderProperty(_hair_HighlightValue, "Hair Highlight Value");
            });
        }

        void DrawSpecular(MaterialEditor me)
        {
            if (_specular == null) return;

            if (!IsFeatureEnabled(_specular)) return;

            DrawSection(MakePrefsKey(me, "Specular"), "Specular", true, () =>
            {
                DrawFeatureMaskReminder(_specularMaskCh, "Specular");

                if (_specularColor      != null) me.ShaderProperty(_specularColor,      "Color");
                if (_specularIntensity  != null) me.ShaderProperty(_specularIntensity,  "Intensity");
                if (_specularSharpness  != null) me.ShaderProperty(_specularSharpness,  "Sharpness");
                if (_specularThreshold  != null) me.ShaderProperty(_specularThreshold,  "Threshold");
                if (_specularToonStep   != null) me.ShaderProperty(_specularToonStep,   "Toon Step (0=Smooth, 1=Hard)");
            });
        }

        void DrawEmissiveGlow(MaterialEditor me)
        {
            if (_emissiveGlow == null) return;

            if (!IsFeatureEnabled(_emissiveGlow)) return;

            DrawSection(MakePrefsKey(me, "Emissive Glow"), "Emissive Glow", true, () =>
            {
                DrawFeatureMaskReminder(_emissiveMaskCh, "Emissive Glow");

                if (_emissive_Color != null) me.ShaderProperty(_emissive_Color, "Emissive Color");
                if (_emissive_Intensity != null) me.ShaderProperty(_emissive_Intensity, "Emissive Intensity");
            });
        }

        void DrawDissolveCharacter(MaterialEditor me)
        {
            if (_dISSOLVE == null) return;

            if (!IsFeatureEnabled(_dISSOLVE)) return;

            DrawSection(MakePrefsKey(me, "Dissolve"), "Dissolve", true, () =>
            {
                if (_texture2DDissolve != null) me.ShaderProperty(_texture2DDissolve, "Texture2DDissolve");
                if (_dissolveColor != null) me.ShaderProperty(_dissolveColor, "DissolveColor");
                if (_dissolveValue != null) me.ShaderProperty(_dissolveValue, "DissolveValue");
                if (_startDissolve != null) me.ShaderProperty(_startDissolve, "StartDissolve = 0 DissolveValue");
                if (_endDissolve != null) me.ShaderProperty(_endDissolve, "EndDissolve = 1 DissolveValue");
                if (_sizeGlowDissolve != null) me.ShaderProperty(_sizeGlowDissolve, "SizeGlowDissolve");
            });
        }

        void DrawCharacterDarkening(MaterialEditor me)
        {
            if (_targetDarken == null) return;

            if (!IsFeatureEnabled(_targetDarken)) return;

            DrawSection(MakePrefsKey(me, "TargetDarken"), "TargetDarken", true, () =>
            {
                if (_targetDarkenIntensity != null) me.ShaderProperty(_targetDarkenIntensity, "TargetDarkenIntensity");
                if (_targetDarkenLocal != null) me.ShaderProperty(_targetDarkenLocal, "TargetDarkenLocal");
            });
        }

        void DrawIndicator(MaterialEditor me)
        {
            if (_indicator == null) return;

            if (!IsFeatureEnabled(_indicator)) return;

            DrawSection(MakePrefsKey(me, "Indicator"), "Indicator", true, () =>
            {
                if (_indicatorStrength != null) me.ShaderProperty(_indicatorStrength, "Indicator Strength");
                if (_indicatorColor != null) me.ShaderProperty(_indicatorColor, "IndicatorColor");
                if (_fresnelPowerIndicator != null) me.ShaderProperty(_fresnelPowerIndicator, "FresnelPowerIndicator");
            });
        }

        void DrawGetHit(MaterialEditor me)
        {
            if (_getHit == null) return;

            if (!IsFeatureEnabled(_getHit)) return;

            DrawSection(MakePrefsKey(me, "GetHit"), "GetHit", true, () =>
            {
                if (_getHitStrength != null) me.ShaderProperty(_getHitStrength, "GetHit Strength");
                if (_getHitColor != null) me.ShaderProperty(_getHitColor, "GetHitColor");
                if (_fresnelPowerHit != null) me.ShaderProperty(_fresnelPowerHit, "FresnelPowerHit");
            });
        }

        void DrawLIGHTSWEEP(MaterialEditor me)
        {
            if (_lIGHTSWEEP == null) return;

            if (!IsFeatureEnabled(_lIGHTSWEEP)) return;

            DrawSection(MakePrefsKey(me, "Use Light Sweep"), "Use Light Sweep", true, () =>
            {
                if (_lightSweepIntensity != null) me.ShaderProperty(_lightSweepIntensity, "LightSweepIntensity");
                if (_lightSweepDuration != null) me.ShaderProperty(_lightSweepDuration, "LightSweepDuration (Seconds)");
                if (_lightSweepDelay != null) me.ShaderProperty(_lightSweepDelay, "LightSweepDelay (Seconds)");
                if (_lightSweepWidth != null) me.ShaderProperty(_lightSweepWidth, "LightSweepWidth");
                if (_lightSweepSoftness != null) me.ShaderProperty(_lightSweepSoftness, "LightSweepSoftness");
                if (_lightSweepStart != null) me.ShaderProperty(_lightSweepStart, "LightSweepStart");
                if (_lightSweepEnd != null) me.ShaderProperty(_lightSweepEnd, "LightSweepEnd");
                if (_lightSweepDirX != null) me.ShaderProperty(_lightSweepDirX, "LightSweepDirX");
                if (_lightSweepDirY != null) me.ShaderProperty(_lightSweepDirY, "LightSweepDirY");
                if (_lightSweepDirZ != null) me.ShaderProperty(_lightSweepDirZ, "LightSweepDirZ");
            });
        }

        void DrawUPGRADE(MaterialEditor me)
        {
            if (_uPGRADE == null) return;

            if (!IsFeatureEnabled(_uPGRADE)) return;

            DrawSection(MakePrefsKey(me, "Use Upgrade Weapon"), "Use Upgrade Weapon", true, () =>
            {
                if (_upgradeActive != null) me.ShaderProperty(_upgradeActive, "UpgradeActive");
                if (_upgradeColor != null) me.ShaderProperty(_upgradeColor, "UpgradeColor");
                if (_upgradeIntensity != null) me.ShaderProperty(_upgradeIntensity, "UpgradeIntensity");
                if (_upgradeMinBrightness != null) me.ShaderProperty(_upgradeMinBrightness, "UpgradeMinBrightness");
            });
        }

        void DrawHairTransparent(MaterialEditor me)
        {
            if (_headBackHide == null) return;
            if (!IsFeatureEnabled(_headBackHide)) return;

            DrawSection(MakePrefsKey(me, "HairTransparent"), "Hair Transparent", true, () =>
            {
                var mat = (me != null && me.target is Material m) ? m : null;
                if (mat != null)
                {
                    var parent = TryGetParentMaterial(mat);
                    if (parent == null)
                    {
                        DrawHelpBox(
                                "Use this material as a Variant of Hair_Main for proper color and FX sync.\n" +
                                "If duplicated, recreate the Variant before Setup.",
                            MessageType.Warning
                        );
                    }
                }

                // ---- Existing controls ----
                if (_eyeBackfaceClip != null) me.ShaderProperty(_eyeBackfaceClip, "EYE_BACKFACE_CLIP");
                if (_hairBackfaceFade != null) me.ShaderProperty(_hairBackfaceFade, "HAIR_BACKFACE_FADE");

                if (_headBackCutoff != null) me.ShaderProperty(_headBackCutoff, "HeadBackCutoff");
                if (_headTopCutoff != null) me.ShaderProperty(_headTopCutoff, "HeadTopCutOff(Top View Only)");
                if (_hairFadeRange != null) me.ShaderProperty(_hairFadeRange, "HairFadeRange");

                EditorGUILayout.Space(8);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Setup Hair Transparent", GUILayout.Height(22), GUILayout.MaxWidth(220)))
                    {
                        foreach (var t in me.targets)
                        {
                            var mm = t as Material;
                            if (mm == null) continue;
                            ApplyHairTransparentSetup_Single(mm);
                        }

                        GUI.changed = true;
                        me.Repaint();
                    }
                }
            });
        }

        void DrawOtherProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            foreach (var prop in properties)
            {
                if (prop == null) continue;

                // HideInInspector check (Unity version compatible)
                // MaterialProperty.propertyFlags was added in 6000.1; 6000.0 still only has .flags,
                // so the guard has to be 6000_1, not 6000_0.
#if UNITY_6000_1_OR_NEWER
            if ((prop.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                continue;
#else
                if ((prop.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
                    continue;
#endif

                string n = prop.name;
                if (IsSkippedProperty(n))
                    continue;

                materialEditor.ShaderProperty(prop, prop.displayName);
            }
        }


        static readonly HashSet<string> _skipProps = new HashSet<string>
    {
        // Rendering
        "_RenderQueueSelector","_RenderQueue","_SrcBlend","_DstBlend","_CullMode",
        "_ZWrite","_ZTest","_AlphaClipping","_Cutoff","_CastShadow",

        // Stencil
        "_StencilRef","_StencilComp","_StencilPass","_StencilFail","_StencilZFail",

        // Texture
        "_MainTex","_RGBA_Masking","_RGBA_Masking2",

        // Mask System (auto-managed by GUI)
        "_USE_MASKTEX1","_USE_MASKTEX2",
        "_MetallicMaskCh","_HairHighlightMaskCh","_EmissiveMaskCh","_OutlineMaskCh","_SpecularMaskCh",

        // Base colors
        "_Texture_Brightness","_BaseColor","_Shadow_Color",

        // Face Shadow
        "_Switch_to_Face_Mode","_FaceTex","_DebugUvFace","_FaceShadowUVScale","_FaceShadowUVOffsetX","_FaceShadowUVOffsetY","_FlipUvFace",

        // Base lighting
        "_ReceiveShadow","_ShadowSoftness","_ShadowEdgeSmooth","_RejectSelfShadow","_SelfShadowRejectDist","_AdditionalLightIntensity","_UseContactShadow",

        // Normal Map
        "_UseNormal", "_NormalMap", "_NormalStrength",

        // Toon ramp
        "_ToonRampSmoothness","_ShadowEdge","_ShadowEdgeColor","_ShadowEdgeIntensity","_ShadowEdgeWidth",

        // Outline
        "_OUTLINE_ZMODE","_OutlineWidth","_OutlineIntensity","_OutlineColor","_OutlineZOffset","_OutlineMask",

        // Transparency
        "_AlphaValue",

        // SoftLight
        "_SoftLight","_SoftLightHighlight",

        // Rim Light
        "_RimLight","_RimColorMode","_RimColor","_IntensityRimLight","_StepRim",

        // Metallic
        "_UseMetallic","_GradientMetallic","_MetalNormalMap","_MetalIntensity",

        // Hair Highlight
        "_Hair_Highlight","_ColorHair","_Hair_HighlightValue",

        // Specular
        "_Specular","_SpecularColor","_SpecularIntensity",
        "_SpecularSharpness","_SpecularThreshold","_SpecularToonStep",

        // Emissive Glow
        "_EmissiveGlow","_Emissive_Color","_Emissive_Intensity",

        // Dissolve
        "_DISSOLVE","_Texture2DDissolve","_DissolveColor","_DissolveValue","_StartDissolve","_EndDissolve","_SizeGlowDissolve",

        // Darken
        "_TargetDarken","_TargetDarkenIntensity","_TargetDarkenLocal",

        // Indicator
        "_Indicator","_IndicatorStrength","_IndicatorColor","_FresnelPowerIndicator",

        // GetHit
        "_GetHit","_GetHitStrength","_GetHitColor","_FresnelPowerHit",

        // Light Sweep
        "_LIGHTSWEEP","_LightSweepIntensity","_LightSweepDuration","_LightSweepDelay",
        "_LightSweepWidth","_LightSweepSoftness","_LightSweepStart","_LightSweepEnd",
        "_LightSweepDirX","_LightSweepDirY","_LightSweepDirZ",

        // Upgrade
        "_UPGRADE","_UpgradeActive","_UpgradeColor","_UpgradeIntensity","_UpgradeMinBrightness",

        // Hair Transparent
        "_HEAD_BACK_HIDE","_EYE_BACKFACE_CLIP","_HAIR_BACKFACE_FADE",
        "_HeadBackCutoff","_HeadTopCutoff","_HairFadeRange",
    };

        static bool IsSkippedProperty(string n)
        {
            return _skipProps.Contains(n);
        }

        private enum TriState { Off, On, Mixed }

        private static TriState GetTriState(MaterialProperty p)
        {
            if (p == null) return TriState.Off;
            if (p.hasMixedValue) return TriState.Mixed;
            return (p.floatValue > 0.5f) ? TriState.On : TriState.Off;
        }


        private struct FeatureBinding
        {
            public FeatureId id;
            public string label;
            public bool locked;

            public MaterialProperty prop;
            public string undoName;
            public string kwOn;
            public string kwOff;

            public bool HasProp => prop != null;
        }

        private bool ResolveIsOn(in FeatureBinding b)
        {
            if (b.HasProp) return IsFeatureEnabled(b.prop);
            return false;
        }

        private void ApplyToggle(MaterialEditor me, in FeatureBinding b, bool newState)
        {
            if (me == null) return;
            if (!b.HasProp) return;

            if (!string.IsNullOrEmpty(b.undoName))
                me.RegisterPropertyChangeUndo(b.undoName);

            SetKeywordEnum(me, b.prop, newState, b.kwOn, b.kwOff);

            GUI.changed = true;
            me.Repaint();
        }

        private void DrawFeatureButton(
            MaterialEditor me,
            Rect btnRect,
            FeatureBinding b,
            Color colOn, Color colOnHover,
            Color colOff, Color colOffHover,
            Color colLock, Color colLockHover,
            GUIStyle statusStyle,
            GUIStyle nameStyle
        )
        {
            TriState tri = b.HasProp ? GetTriState(b.prop)
                             : (ResolveIsOn(b) ? TriState.On : TriState.Off);

            bool isMixed = (tri == TriState.Mixed);
            bool isOn = (tri == TriState.On);

            bool isHover = btnRect.Contains(Event.current.mousePosition);

            Color bg, bgHover;

            if (b.locked)
            {
                bg = colLock; bgHover = colLockHover;
            }
            else if (isMixed)
            {
                bg = FEATURE_MIX_BG;
                bgHover = FEATURE_MIX_HOVER_BG;
            }
            else if (isOn)
            {
                bg = colOn; bgHover = colOnHover;
            }
            else
            {
                bg = colOff; bgHover = colOffHover;
            }


            EditorGUI.DrawRect(btnRect, isHover ? bgHover : bg);
            EditorGUIUtility.AddCursorRect(btnRect, b.locked ? MouseCursor.Arrow : MouseCursor.Link);

            using (new EditorGUI.DisabledScope(b.locked))
            {
                if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
                {
                    ApplyToggle(me, b, !isOn);
                }
            }

            // --- Status Text ---
            string statusTxt;

            if (b.locked)
            {
                statusTxt = "LOCK";
            }
            else
            {
                statusTxt = isMixed ? "MIX" : (isOn ? "ON" : "OFF");
            }

            string nameTxt = b.label;

            const float statusW = 40f;
            const float padL = 8f;
            const float padR = 6f;

            Rect statusRect = new Rect(btnRect.x + padL, btnRect.y, statusW, btnRect.height);
            Rect nameRect = new Rect(btnRect.x + padL + statusW, btnRect.y, btnRect.width - (padL + statusW + padR), btnRect.height);

            GUI.Label(statusRect, statusTxt, statusStyle);
            GUI.Label(nameRect, nameTxt, nameStyle);
        }

        private enum FeatureId
        {
            // Main
            Rendering, Stencil, Texture, Colors, FaceShadow, Lighting, ToonRamp, Outline, Transparency,
            SoftLight, RimLight, Metallic, HairHighlight, Specular, Emissive, Dissolve, Darken,
            Indicator, GetHit, LightSweep, Upgrade, NormalMap,

            // Hair System
            HairTransparent,
        }

        private enum FeatureGroup { Main, Hair }

        private struct FeatureDef
        {
            public FeatureId id;
            public FeatureGroup group;
            public string label;
            public bool locked;

            // binding
            public System.Func<ZLZAnimeToonGUI, MaterialProperty> propGetter;
            public string undoName;
            public string kwOn;
            public string kwOff;
        }

        private FeatureBinding BuildBinding(in FeatureDef def)
        {
            FeatureBinding b = new FeatureBinding
            {
                id = def.id,
                label = def.label,
                locked = def.locked,

                prop = null,
                undoName = def.undoName,
                kwOn = def.kwOn,
                kwOff = def.kwOff
            };

            if (def.propGetter != null)
                b.prop = def.propGetter(this);

            b.locked |= (b.prop == null);

            return b;
        }

        private static readonly FeatureDef[] _features = new FeatureDef[]
        {
        // -------- Main (Locked) --------
        new FeatureDef { id=FeatureId.Rendering,        group=FeatureGroup.Main, label="Rendering",         locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Stencil,          group=FeatureGroup.Main, label="Stencil",           locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Texture,          group=FeatureGroup.Main, label="Texture",           locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Colors,           group=FeatureGroup.Main, label="Colors",            locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Lighting,         group=FeatureGroup.Main, label="Lighting",          locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.ToonRamp,         group=FeatureGroup.Main, label="ToonRamp",          locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Outline,          group=FeatureGroup.Main, label="Outline",           locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },
        new FeatureDef { id=FeatureId.Transparency,     group=FeatureGroup.Main, label="Transparency",      locked=true,  propGetter=null,                      undoName=null,                  kwOn=null,                  kwOff=null },

        // -------- Main (Optional) --------
        new FeatureDef { id=FeatureId.FaceShadow,       group = FeatureGroup.Main,label = "Face Shadow",    locked=false, propGetter = (g)=> g._faceShadow,     undoName="Face Shadow",         kwOn=KW_FACESHADOW_ON,      kwOff=KW_FACESHADOW_OFF },
        new FeatureDef { id=FeatureId.SoftLight,        group=FeatureGroup.Main, label="SoftLight",         locked=false, propGetter = (g)=>g._softLight,       undoName="SoftLight",           kwOn=KW_SOFTLIGHT_ON,       kwOff=KW_SOFTLIGHT_OFF },
        new FeatureDef { id=FeatureId.NormalMap,        group=FeatureGroup.Main, label="Normal Map",        locked=false, propGetter = (g)=>g._useNormal,       undoName="Normal Map",          kwOn=KW_USENORMAL_ON,       kwOff=KW_USENORMAL_OFF },
        new FeatureDef { id=FeatureId.Metallic,         group=FeatureGroup.Main, label="Metallic",          locked=false, propGetter = (g)=>g._useMetallic,     undoName="Metallic",            kwOn=KW_USEMETALLIC_ON,     kwOff=KW_USEMETALLIC_OFF },
        new FeatureDef { id=FeatureId.RimLight,         group=FeatureGroup.Main, label="RimLight",          locked=false, propGetter = (g)=>g._rimLight,        undoName="RimLight",            kwOn=KW_RIMLIGHT_ON,        kwOff=KW_RIMLIGHT_OFF },
        new FeatureDef { id=FeatureId.HairHighlight,    group=FeatureGroup.Main, label="Hair Highlight",    locked=false, propGetter = (g)=>g._hair_Highlight,  undoName="Hair Highlight",      kwOn=KW_HAIR_HIGHLIGHT_ON,  kwOff=KW_HAIR_HIGHLIGHT_OFF },
        new FeatureDef { id=FeatureId.Specular,         group=FeatureGroup.Main, label="Specular",          locked=false, propGetter = (g)=>g._specular,        undoName="Specular",            kwOn=KW_SPECULAR_ON,        kwOff=KW_SPECULAR_OFF },
        new FeatureDef { id=FeatureId.Emissive,         group=FeatureGroup.Main, label="Emissive",          locked=false, propGetter = (g)=>g._emissiveGlow,    undoName="Emissive Glow",       kwOn=KW_EMISSIVEGLOW_ON,    kwOff=KW_EMISSIVEGLOW_OFF },
        new FeatureDef { id=FeatureId.Dissolve,         group=FeatureGroup.Main, label="Dissolve",          locked=false, propGetter = (g)=>g._dISSOLVE,        undoName="Dissolve",            kwOn=KW_DISSOLVE_ON,        kwOff=KW_DISSOLVE_OFF },
        new FeatureDef { id=FeatureId.Darken,           group=FeatureGroup.Main, label="Darken",            locked=false, propGetter = (g)=>g._targetDarken,    undoName="TargetDarken",        kwOn=KW_TARGETDARKEN_ON,    kwOff=KW_TARGETDARKEN_OFF },
        new FeatureDef { id=FeatureId.Indicator,        group=FeatureGroup.Main, label="Indicator",         locked=false, propGetter = (g)=>g._indicator,       undoName="Indicator",           kwOn=KW_INDICATOR_ON,       kwOff=KW_INDICATOR_OFF },
        new FeatureDef { id=FeatureId.GetHit,           group=FeatureGroup.Main, label="GetHit",            locked=false, propGetter = (g)=>g._getHit,          undoName="GetHit",              kwOn=KW_GETHIT_ON,          kwOff=KW_GETHIT_OFF },
        new FeatureDef { id=FeatureId.LightSweep,       group=FeatureGroup.Main, label="LightSweep",        locked=false, propGetter = (g)=>g._lIGHTSWEEP,      undoName="Use Light Sweep",     kwOn=KW_LIGHTSWEEP_ON,      kwOff=KW_LIGHTSWEEP_OFF },
        new FeatureDef { id=FeatureId.Upgrade,          group=FeatureGroup.Main, label="Upgrade",           locked=false, propGetter = (g)=>g._uPGRADE,         undoName="Use Upgrade",         kwOn=KW_UPGRADE_ON,         kwOff=KW_UPGRADE_OFF },

        // -------- Hair System --------
        new FeatureDef { id=FeatureId.HairTransparent,  group=FeatureGroup.Hair, label="Hair Transparent",  locked=false, propGetter = (g)=>g._headBackHide,    undoName="Hair Transparent",    kwOn=KW_HEAD_BACK_HIDE_ON,  kwOff=KW_HEAD_BACK_HIDE_OFF },
        };


        private void DrawFeatureGroup(
            MaterialEditor me,
            FeatureGroup group,
            int columns,
            float buttonHeight,
            float spacing,
            Color colOn, Color colOnHover,
            Color colOff, Color colOffHover,
            Color colLock, Color colLockHover)
        {
            int count = 0;
            for (int i = 0; i < _features.Length; i++)
                if (_features[i].group == group) count++;

            if (count == 0) return;

            int rows = Mathf.CeilToInt(count / (float)columns);
            int drawn = 0;
            int featureIndex = 0;

            for (int r = 0; r < rows; r++)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, buttonHeight);

                float buttonWidth = (rowRect.width - spacing * (columns - 1)) / columns;

                for (int c = 0; c < columns; c++)
                {
                    if (drawn >= count) break;

                    while (featureIndex < _features.Length && _features[featureIndex].group != group)
                        featureIndex++;

                    if (featureIndex >= _features.Length) break;

                    FeatureDef meta = _features[featureIndex];

                    Rect btnRect = new Rect(
                        rowRect.x + c * (buttonWidth + spacing),
                        rowRect.y,
                        buttonWidth,
                        buttonHeight
                    );

                    FeatureBinding b = BuildBinding(meta);

                    DrawFeatureButton(me, btnRect, b, colOn, colOnHover, colOff, colOffHover, colLock, colLockHover, _featureStatusStyle, _featureNameStyle);

                    featureIndex++;
                    drawn++;
                }
            }
        }

        void DrawFeatureButtonGrid_UIOnly(MaterialEditor materialEditor)
        {
            EnsureStyles();

            // Update Hover
            if (Event.current.type == EventType.MouseMove)
                materialEditor.Repaint();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("FEATURE TOGGLES (ON / OFF)", InfoTitle);
            EditorGUILayout.Space(4);

            int columns = 2;
            float buttonHeight = 24f;
            float spacing = 4f;

            Color colOn = HEADER_OPEN_BG;
            Color colOnHover = HEADER_OPEN_HOVER_BG;

            Color colOff = new Color(0.12f, 0.12f, 0.12f, 1f);
            Color colOffHover = new Color(0.16f, 0.16f, 0.16f, 1f);

            Color colLock = HEADER_OPEN_BG;
            Color colLockHover = HEADER_OPEN_HOVER_BG;

            // -------- Main --------
            DrawFeatureGroup(materialEditor, FeatureGroup.Main, columns, buttonHeight, spacing,
                colOn, colOnHover, colOff, colOffHover, colLock, colLockHover);

            // -------- Hair --------
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hair System", InfoTitle);
            EditorGUILayout.Space(4);

            DrawFeatureGroup(materialEditor, FeatureGroup.Hair, columns, buttonHeight, spacing, colOn, colOnHover, colOff, colOffHover, colLock, colLockHover);

            EditorGUILayout.Space(6);
        }


        static bool IsFeatureEnabled(MaterialProperty p)
        {
            if (p == null) return false;
            if (p.hasMixedValue) return true;

            return p.floatValue > 0.5f;
        }



        static bool IsTextureMissing(MaterialProperty texProp)
        {
            if (texProp == null) return true;
            return texProp.textureValue == null;
        }

        static void DrawMaskChannelHint(string channel, string meaning)
        {
            EditorGUILayout.LabelField($"• {channel} : {meaning}", InfoBody);
        }

        // Channel accent colours, reused by both mask banks. They match what the channel looks
        // like in an image editor, so a red "MASK 1 · R" chip reads as the red channel at a glance.
        static readonly Color[] CHANNEL_CHIP_COLORS =
        {
            new Color(1.00f, 0.42f, 0.42f), // R
            new Color(0.42f, 0.92f, 0.50f), // G
            new Color(0.45f, 0.68f, 1.00f), // B
            new Color(0.88f, 0.88f, 0.94f), // A
        };
        static readonly Color CHIP_NEUTRAL = new Color(0.62f, 0.64f, 0.70f); // no mask — whole mesh
        static readonly Color CHIP_ALERT   = new Color(0.96f, 0.76f, 0.36f); // mask texture missing

        static GUIStyle _chipStyle;
        static GUIStyle ChipStyle
        {
            get
            {
                if (_chipStyle == null)
                {
                    _chipStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize  = 11,
                        padding   = new RectOffset(9, 9, 0, 0),
                    };
                }
                return _chipStyle;
            }
        }

        // Tinted pill with a 1px border, drawn with DrawRect so it needs no background texture.
        static void DrawStatusChip(string text, Color accent)
        {
            var content = new GUIContent(text);
            float w = ChipStyle.CalcSize(content).x;
            Rect r = GUILayoutUtility.GetRect(w, 20f, GUILayout.Width(w), GUILayout.Height(20f));

            EditorGUI.DrawRect(r, new Color(accent.r, accent.g, accent.b, 0.16f));

            Color line = new Color(accent.r, accent.g, accent.b, 0.75f);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), line);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), line);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), line);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), line);

            Color old = ChipStyle.normal.textColor;
            ChipStyle.normal.textColor = accent;
            GUI.Label(r, content, ChipStyle);
            ChipStyle.normal.textColor = old;
        }

        // Per-feature mask status: which channel it reads, and whether that mask texture exists.
        void DrawFeatureMaskReminder(MaterialProperty channelProp, string featureName)
        {
            if (channelProp == null) return;

            int idx  = Mathf.Clamp((int)channelProp.floatValue, 0, 8);
            int bank = idx / 4;
            bool noMask     = (idx == CHANNEL_NONE);
            bool texMissing = !noMask && IsTextureMissing((bank == 0) ? _rgba_Masking : _rgba_Masking2);

            string chipText = noMask ? "WHOLE MESH" : $"MASK {bank + 1} · {"RGBA"[idx % 4]}";

            Color accent = noMask     ? CHIP_NEUTRAL
                         : texMissing ? CHIP_ALERT
                         :              CHANNEL_CHIP_COLORS[idx % 4];

            string message = noMask     ? $"{featureName} has no mask — it applies to the whole mesh."
                           : texMissing ? $"Mask {bank + 1} is not assigned — {featureName} will not show."
                           :              $"{featureName} reads its mask from this channel.";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatusChip(chipText, accent);
                GUILayout.Space(6);
                GUILayout.Label(message, InfoBody, GUILayout.ExpandWidth(true));
            }
            EditorGUILayout.EndVertical();

            if (texMissing)
            {
                DrawHelpBox(
                    $"• Assign a Mask {bank + 1} texture in Mask Layout, OR\n" +
                    $"• Uncheck 'Use Mask' to apply {featureName} on the whole mesh.",
                    MessageType.Warning);
            }
        }

        static void SetKeywordEnum(MaterialEditor me, MaterialProperty p, bool on, string kwOn, string kwOff)
        {
            if (p == null || me == null) return;

            float v = on ? 1f : 0f;

            p.floatValue = v;

            foreach (Material m in me.targets)
            {
                if (m == null) continue;

                if (m.HasProperty(p.name))
                    m.SetFloat(p.name, v);

                if (on)
                {
                    if (!string.IsNullOrEmpty(kwOff)) m.DisableKeyword(kwOff);
                    if (!string.IsNullOrEmpty(kwOn)) m.EnableKeyword(kwOn);
                }
                else
                {
                    if (!string.IsNullOrEmpty(kwOn)) m.DisableKeyword(kwOn);
                    if (!string.IsNullOrEmpty(kwOff)) m.EnableKeyword(kwOff);
                }
            }
        }

        static void SetKeywordEnum_SingleMaterial(Material mat, string propName, bool on, string kwOn, string kwOff)
        {
            if (mat == null) return;

            float v = on ? 1f : 0f;

            if (mat.HasProperty(propName))
                mat.SetFloat(propName, v);

            if (on)
            {
                if (!string.IsNullOrEmpty(kwOff)) mat.DisableKeyword(kwOff);
                if (!string.IsNullOrEmpty(kwOn)) mat.EnableKeyword(kwOn);
            }
            else
            {
                if (!string.IsNullOrEmpty(kwOn)) mat.DisableKeyword(kwOn);
                if (!string.IsNullOrEmpty(kwOff)) mat.EnableKeyword(kwOff);
            }
        }


        static string MakePrefsKey(MaterialEditor me, string section)
        {
            var m = (me != null && me.target is Material mat) ? mat : null;
            string shaderName = (m != null && m.shader != null) ? m.shader.name : "UnknownShader";
            return $"ZLZAnimeToonGUI:{shaderName}:{section}";
        }

        public override void ValidateMaterial(Material material)
        {
            UpdateRenderSettings(material);
            MigrateLegacyMaskSetup(material);
            RefreshMaskTextureKeywords(material);
        }

        // Migrate materials saved before v1.05's Mask Layout system.
        // - If old _SpecularMask had a texture but _RGBA_Masking2 is empty, copy it across.
        // - Channel index defaults from shader properties keep legacy R/G/B/A mapping intact.
        static void MigrateLegacyMaskSetup(Material mat)
        {
            if (mat == null) return;

            if (mat.HasProperty("_SpecularMask") && mat.HasProperty("_RGBA_Masking2"))
            {
                var legacy = mat.GetTexture("_SpecularMask");
                if (legacy != null && mat.GetTexture("_RGBA_Masking2") == null)
                    mat.SetTexture("_RGBA_Masking2", legacy);
            }
        }

        // ---------------- Hair Transparent : One-click Setup ----------------
        static Material TryGetParentMaterial(Material m)
        {
            if (m == null) return null;

            var so = new SerializedObject(m);
            var p = so.FindProperty("m_Parent");
            if (p == null) return null;

            return p.objectReferenceValue as Material;
        }


        static void ApplyHairTransparentSetup_Single(Material mat)
        {
            if (mat == null) return;

            Undo.RecordObject(mat, "Setup Hair Transparent");

            // ---- KeywordEnum toggles ----
            SetKeywordEnum_SingleMaterial(mat, "_EYE_BACKFACE_CLIP", false, KW_EYE_BACKFACE_CLIP_ON, KW_EYE_BACKFACE_CLIP_OFF);
            SetKeywordEnum_SingleMaterial(mat, "_HAIR_BACKFACE_FADE", true, KW_HAIR_BACKFACE_FADE_ON, KW_HAIR_BACKFACE_FADE_OFF);

            // ---- HairTransparent ----
            if (mat.HasProperty("_HeadBackCutoff")) mat.SetFloat("_HeadBackCutoff", 0.25f);
            if (mat.HasProperty("_HeadTopCutoff")) mat.SetFloat("_HeadTopCutoff", 0.5f);
            if (mat.HasProperty("_HairFadeRange")) mat.SetFloat("_HairFadeRange", 0.2f);

            //  ---- Alpha Channel ----
            if (mat.HasProperty("_AlphaValue")) mat.SetFloat("_AlphaValue", 0.5f);

            // ---- Render / Stencil states ----
            if (mat.HasProperty("_RenderQueueSelector")) mat.SetFloat("_RenderQueueSelector", 3000f);

            // Blend
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", 5f);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", 10f);

            // ZWrite On
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);

            // Stencil: Ref=1, Comp=Equal(3), Pass=Keep(0)
            if (mat.HasProperty("_StencilRef")) mat.SetFloat("_StencilRef", 1f);
            if (mat.HasProperty("_StencilComp")) mat.SetFloat("_StencilComp", 3f);
            if (mat.HasProperty("_StencilPass")) mat.SetFloat("_StencilPass", 0f);

            // Optional safety: keep RenderType tag correct
            UpdateRenderSettings(mat);

            EditorUtility.SetDirty(mat);
        }

        static void UpdateRenderSettings(Material material)
        {
            if (material == null || !material.HasProperty("_RenderQueueSelector"))
                return;

            int selectedQueue = Mathf.RoundToInt(material.GetFloat("_RenderQueueSelector"));

            // set only if different
            if (material.renderQueue != selectedQueue)
                material.renderQueue = selectedQueue;

            string renderType =
                selectedQueue == 2000 ? "Opaque" :
                selectedQueue == 2450 ? "TransparentCutout" :
                selectedQueue == 3000 ? "Transparent" :
                null;

            if (renderType != null)
                material.SetOverrideTag("RenderType", renderType);
        }
    }
}
#endif