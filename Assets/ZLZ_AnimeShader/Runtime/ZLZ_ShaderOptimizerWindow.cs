#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ZLZ.AnimeShader
{
    public class ZLZ_ShaderOptimizerWindow : EditorWindow
    {
        // ── Target / State ────────────────────────────────────────────────
        GameObject                  _target;
        Vector2                     _scroll;
        bool                        _dirty           = true;
        List<MatAnalysis>           _analyses        = new List<MatAnalysis>();
        Dictionary<int, bool>       _foldoutCache    = new Dictionary<int, bool>();
        PlatformMode                _platformMode    = PlatformMode.PC;
        int                         _selectedIndex   = -1;
        int                         _selectedMatId   = -1;
        HashSet<int>                _expandedTexIds  = new HashSet<int>();
        Dictionary<int, int>        _texPlatformTab  = new Dictionary<int, int>();
        float                       _topHeight       = 320f;
        float                       _chartScrollPos  = 0f;
        Rect                        _summaryBarRect, _barChartRect, _featureSummaryRect, _vramSummaryRect;
        float                       _detailStartY, _scrollViewEndY, _optPreviewStartY, _optPreviewEndY;
        bool                        _showSettings        = false;
        bool                        _prevShowSettings    = false;
        bool                        _prevHasSelection    = false;
        bool                        _showOptimizePreview = false;

        // ── Per-platform thresholds (loaded from EditorPrefs) ─────────────
        int _thPC_MatL, _thPC_MatH;
        int _thAnd_MatL, _thAnd_MatH;
        int _thIOS_MatL, _thIOS_MatH;
        int _thPC_TotL, _thPC_TotH;
        int _thAnd_TotL, _thAnd_TotH;
        int _thIOS_TotL, _thIOS_TotH;
        int _thPC_VramL,    _thPC_VramH;
        int _thAnd_VramL,   _thAnd_VramH;
        int _thIOS_VramL,   _thIOS_VramH;
        int _thPC_VramTotL, _thPC_VramTotH;
        int _thAnd_VramTotL,_thAnd_VramTotH;
        int _thIOS_VramTotL,_thIOS_VramTotH;

        const int k_D_PC_MatL   = 350, k_D_PC_MatH   = 480;
        const int k_D_And_MatL  = 200, k_D_And_MatH  = 320;
        const int k_D_iOS_MatL  = 220, k_D_iOS_MatH  = 350;
        const int k_D_PC_TotL   = 2000, k_D_PC_TotH  = 3500;
        const int k_D_And_TotL  = 900,  k_D_And_TotH = 1600;
        const int k_D_iOS_TotL  = 1100, k_D_iOS_TotH = 2000;
        const int k_D_PC_VramL     = 50,  k_D_PC_VramH     = 150;
        const int k_D_And_VramL    = 20,  k_D_And_VramH    = 60;
        const int k_D_iOS_VramL    = 20,  k_D_iOS_VramH    = 60;
        const int k_D_PC_VramTotL  = 100, k_D_PC_VramTotH  = 300;
        const int k_D_And_VramTotL = 50,  k_D_And_VramTotH = 150;
        const int k_D_iOS_VramTotL = 50,  k_D_iOS_VramTotH = 150;
        bool                        _optPC               = true;
        bool                        _optAndroid          = true;
        bool                        _optIOS              = true;
        List<TexInfo>               _cachedOptTex;
        int                         _optQuality          = 1;   // 0=Max Compress  1=Balanced  2=High Quality
        Dictionary<string, Dictionary<string, int>> _optSlotMaxSize;  // per-propName per-platform max size; null = use smart defaults

        // ── Texture import static data ────────────────────────────────────
        static readonly int[]    k_MaxSizes    = { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
        static readonly string[] k_MaxSizeStr  = { "32", "64", "128", "256", "512", "1024", "2048", "4096" };

        static readonly TextureImporterCompression[] k_Compressions =
        {
            TextureImporterCompression.Uncompressed,
            TextureImporterCompression.CompressedLQ,
            TextureImporterCompression.Compressed,
            TextureImporterCompression.CompressedHQ,
        };
        static readonly string[] k_CompressionStr =
            { "None", "Low Quality", "Normal Quality", "High Quality" };

        static readonly string[] k_PlatformNames  = { "Standalone", "Android", "iPhone" };
        static readonly string[] k_PlatformLabels = { "PC", "Android", "iOS" };

        // ── Palette ───────────────────────────────────────────────────────
        static readonly Color C_HEADER         = new Color(0.22f, 0.22f, 0.26f, 1f);
        static readonly Color C_ACCENT         = new Color(0.42f, 0.72f, 1.00f, 1f);
        static readonly Color C_OK             = new Color(0.22f, 0.75f, 0.45f, 1f);
        static readonly Color C_WARN           = new Color(0.95f, 0.65f, 0.10f, 1f);
        static readonly Color C_ERR            = new Color(0.90f, 0.25f, 0.25f, 1f);
        static readonly Color C_BTN_PRIMARY    = new Color(0.25f, 0.55f, 0.95f, 1f);
        static readonly Color C_CARD_OPEN      = new Color(0.20f, 0.18f, 0.32f, 1f);
        static readonly Color C_CARD_CLOSED    = new Color(0.17f, 0.17f, 0.20f, 1f);
        static readonly Color C_SUB_HEADER     = new Color(0.20f, 0.20f, 0.24f, 1f);
        static readonly Color C_FEATURE_ROW    = new Color(0.19f, 0.19f, 0.23f, 1f);
        static readonly Color C_ROW_BASE       = new Color(0.17f, 0.17f, 0.21f, 1f);
        static readonly Color C_ROW_SELECTED   = new Color(0.26f, 0.18f, 0.06f, 1f);
        static readonly Color C_ROW_SEL_ACCENT = new Color(1.00f, 0.68f, 0.18f, 1f);

        // ── Section border colors ─────────────────────────────────────────
        static readonly Color C_SEC_BUDGET  = new Color(0.30f, 0.55f, 0.90f, 1f);  // blue
        static readonly Color C_SEC_SUMMARY = new Color(0.55f, 0.33f, 0.85f, 1f);  // purple
        static readonly Color C_SEC_CHART   = new Color(0.22f, 0.70f, 0.52f, 1f);  // teal
        static readonly Color C_SEC_DETAIL  = new Color(0.18f, 0.75f, 0.88f, 1f);  // cyan
        static readonly Color C_SEC_BOTTOM  = new Color(0.72f, 0.28f, 0.60f, 1f);  // pink

        // ── Styles ────────────────────────────────────────────────────────
        GUIStyle _titleStyle, _boldStyle, _labelStyle, _subtleStyle, _infoStyle;
        bool     _stylesReady;

        // ── Platform Mode ─────────────────────────────────────────────────
        enum PlatformMode { PC, Android, iOS }

        struct PlatInfo { public PlatformMode mode; public string unityName; public string label; }

        static int DefaultMaxSizeFor(string propName, string platform = "")
        {
            string p      = propName.ToLowerInvariant();
            bool isMobile = platform == "Android" || platform == "iPhone";

            if (p.Contains("normal") || p.Contains("metallic"))
                return isMobile ? 512 : 1024;
            if (p.Contains("face") || p.Contains("dissolve") || p.Contains("gradient"))
                return isMobile ? 256 : 512;
            return isMobile ? 1024 : 2048;
        }

        int GetSlotMaxSize(string propName, string platform = "")
        {
            if (_optSlotMaxSize != null && _optSlotMaxSize.TryGetValue(propName, out var platDict))
                if (!string.IsNullOrEmpty(platform) && platDict.TryGetValue(platform, out int v)) return v;
            return DefaultMaxSizeFor(propName, platform);
        }

        bool IsMobilePlatform => _platformMode == PlatformMode.Android || _platformMode == PlatformMode.iOS;

        static readonly string[] k_PlatformModeNames  = { "Standalone", "Android", "iPhone" };
        static readonly string[] k_PlatformModeLabels = { "PC", "Android", "iOS" };

        // ── Feature Category ──────────────────────────────────────────────
        enum FeatureCat { HairSystem, SurfaceDetail, Lighting, Effects }

        static string CatLabel(FeatureCat c)
        {
            switch (c)
            {
                case FeatureCat.HairSystem:    return "Hair System";
                case FeatureCat.SurfaceDetail: return "Surface Detail";
                case FeatureCat.Lighting:      return "Lighting & Shadow";
                default:                       return "Visual Effects";
            }
        }

        // ── Feature Definition ────────────────────────────────────────────
        struct FeatureDef
        {
            public string      keyword;
            public string      name;
            public int         aluOps;       // ALU instruction count (from HLSL analysis)
            public int         texSamples;   // extra SAMPLE_TEXTURE2D calls this feature adds
            public bool        breaksEarlyZ; // uses clip() / discard — kills GPU Early-Z
            public FeatureCat  cat;
            public string      reason;
            public string      tip;
            public string[]    texProps;
            public bool        isRealtime;
        }

        // Score formula: ALU + TEX×10 + (EarlyZ ? 15 : 0)
        // TEX×10 reflects bandwidth cost; EarlyZ×15 reflects per-fragment overhead loss.
        static int FeatureScore(FeatureDef d)
            => d.aluOps + d.texSamples * 10 + (d.breaksEarlyZ ? 15 : 0);

        static readonly FeatureDef[] k_Features =
        {
            // ── Surface Detail ───────────────────────────────────────────
            new FeatureDef {
                keyword="_USENORMAL_ON", name="Normal Map",
                aluOps=35, texSamples=1, breaksEarlyZ=false,
                cat=FeatureCat.SurfaceDetail,
                reason="UnpackNormal(7) + strength mul f2(2) + TBN mul f3×3x3(15) + normalize(11)  =  35 ALU, 1 TEX",
                tip="Safe for body & armor. Disable on flat or hair surfaces to save 45 pts.",
                texProps=new[]{"_NormalMap"} },

            new FeatureDef {
                keyword="_USEMETALLIC_ON", name="Metallic",
                aluOps=53, texSamples=2, breaksEarlyZ=false,
                cat=FeatureCat.SurfaceDetail,
                reason="UnpackNormal(7) + TBN mul(15) + normalize×2(22) + add f3(3) + dot(5) + mul(1)  =  53 ALU, 2 TEX",
                tip="Heaviest feature — 2 TEX + double TBN transform. Use only on actual metal surfaces.",
                texProps=new[]{"_MetalNormalMap","_GradientMetallic"} },

            // ── Lighting & Shadow ────────────────────────────────────────
            new FeatureDef {
                keyword="_RECEIVESHADOW_ON", name="Receive Shadow",
                aluOps=3, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="lerp(shadowAtten, 1)(2) + mul(distAtten)(1)  =  3 ALU  (shadow map sampled in Base Core)",
                tip="Very cheap — shadow map is already fetched in Base Core. Worth it for lit scenes.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_RIMLIGHT_ON", name="Rim Light",
                aluOps=24, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="dot f3×2(10) + fresnel toon-step(3) + mask mul(1) + color mul×2 f3(6) + add f3(3)  =  24 ALU",
                tip="Uses toon floor-step, not pow() — cheaper than smooth Fresnel. Safe for hero chars.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_EMISSIVEGLOW_ON", name="Emissive Glow",
                aluOps=12, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="mul f3×3(9) + add f3(3)  =  12 ALU  (reads RGBAMask.b — no extra TEX sample)",
                tip="Zero TEX cost — RGBAMask is already sampled in Base Core. Use freely.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_HAIR_HIGHLIGHT_ON", name="Hair Highlight",
                aluOps=10, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="mul(ramp×mask)(1) + mul f3×3(9)  =  10 ALU  (reads RGBAMask.g — no extra TEX sample)",
                tip="Zero TEX cost — RGBAMask is already sampled in Base Core. Safe on all hair mats.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_SPECULAR_ON", name="Specular",
                aluOps=16, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="halfV normalize(3) + NdotH(2) + pow(2) + smoothstep(3) + mul/saturate chain(6)  =  ~16 ALU  (reads Feature Mask via channel picker — no extra TEX sample)",
                tip="Stylized Blinn-Phong with toon-step for eyes/lips/leather/weapons. Mask channel selectable via Mask Layout. Default Sharpness 80 = tight anime highlight; lower for softer skin.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_SOFTLIGHT_ON", name="Soft Light",
                aluOps=18, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="softDark: mul f3×4(12) + lerp f3(6)  =  18 ALU  (sqrt branch eliminated: compiler constant-folds step(0.5,0.2)=0)",
                tip="Compiler removes the sqrt/softBright path entirely. Only the dark-lerp path runs.",
                texProps=new string[0] },

            // ── Character Control ────────────────────────────────────────
            new FeatureDef {
                keyword="_SWITCH_TO_FACE_MODE_ON", name="Face Mode",
                aluOps=6, texSamples=1, breaksEarlyZ=false,
                cat=FeatureCat.Lighting,
                reason="FRAG only: step f2(2)+lerp(2)+saturate(1)+sub(1)  =  6 ALU, 1 TEX  (angle+side scalars precomputed in vertex via ZLZ_ComputeFaceScalars — ~26 VERT ops × ~1K verts vs 71 FRAG × ~2M pixels)",
                tip="Optimized: light/head direction math moved to vertex shader. Required for SDF face shadow — keep ON for all face materials.",
                texProps=new[]{"_FaceTex"} },

            new FeatureDef {
                keyword="_HEAD_BACK_HIDE_ON", name="Head Back Hide",
                aluOps=12, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.HairSystem,
                reason="2× dot f3 guard checks(10) + branch(2)  =  12 ALU  (system enable flag for EYE / HAIR sub-features)",
                tip="Required to enable Eye Clip or Hair Fade. Negligible cost alone.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_HAIR_BACKFACE_FADE_ON", name="Hair Backface Fade",
                aluOps=42, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.HairSystem,
                reason="normalize×2(22)+dot f3(5)+mad(1)+max(1)+smoothstep(7)+sub(1)+lerp(2)  =  42 ALU  (requires HEAD_BACK_HIDE_ON)",
                tip="Requires HeadDirectionBinder + Head Back Hide ON. Fades hair alpha — no clip, no EZ break.",
                texProps=new string[0] },

            new FeatureDef {
                keyword="_EYE_BACKFACE_CLIP_ON", name="Eye Backface Clip",
                aluOps=32, texSamples=0, breaksEarlyZ=true,
                cat=FeatureCat.HairSystem,
                reason="normalize×2(22)+dot f3(5)+mad(1)+sub+clip(2)  =  32 ALU  (requires HEAD_BACK_HIDE_ON) — clip breaks Early-Z",
                tip="Requires HeadDirectionBinder + Head Back Hide ON. Use on eye materials only.",
                texProps=new string[0] },

            // ── Visual Effects ───────────────────────────────────────────
            new FeatureDef {
                keyword="_ALPHACLIPPING_ON", name="Alpha Clipping",
                aluOps=2, texSamples=0, breaksEarlyZ=true,
                cat=FeatureCat.Effects,
                reason="sub(1) + clip(1)  =  2 ALU — clip() discards Early-Z on every fragment",
                tip="Needed for alpha-tested hair/edges. The +15 EZ penalty reflects total per-fragment overhead loss.",
                texProps=new string[0], isRealtime=false },

            new FeatureDef {
                keyword="_DISSOLVE_ON", name="Dissolve",
                aluOps=13, texSamples=1, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="lerp(2)+saturate(1)+step f2(2)+sub(1)+lerp f3(6)  =  13 ALU, 1 TEX",
                tip="Disable when dissolve is not playing to save the texture sample (10 pts).",
                texProps=new[]{"_Texture2DDissolve"}, isRealtime=true },

            new FeatureDef {
                keyword="_LIGHTSWEEP_ON", name="Light Sweep",
                aluOps=36, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="fmod(3)+clamp(1)+frac(1)+lerp(3)+dot f3(5)+smoothstep(7)+mul f3+lerp f3(7)  =  36 ALU",
                tip="Time-based UV math. Disable when sweep is not active — saves all 36 ALU.",
                texProps=new string[0], isRealtime=true },

            new FeatureDef {
                keyword="_UPGRADE_ON", name="Upgrade Weapon",
                aluOps=15, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="max f3(3) + mul f3×2(6) + lerp f3(6)  =  15 ALU",
                tip="Disable when upgrade is not active.",
                texProps=new string[0], isRealtime=true },

            new FeatureDef {
                keyword="_TARGETDARKEN_ON", name="Target Darken",
                aluOps=10, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="mul f3(3) + mul(1) + lerp f3(6)  =  10 ALU",
                tip="Disable when not targeting.",
                texProps=new string[0], isRealtime=true },

            new FeatureDef {
                keyword="_INDICATOR_ON", name="Indicator",
                aluOps=26, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="Shared Fresnel: dot(5)+sub(1)+pow f2(8)=14  +  mul f3(3)+add f3(3)+lerp f3(6)=12  =  26 ALU",
                tip="Shares Fresnel calc with GetHit — both ON costs 38 ALU total (not 52).",
                texProps=new string[0], isRealtime=true },

            new FeatureDef {
                keyword="_GETHIT_ON", name="Get Hit",
                aluOps=26, texSamples=0, breaksEarlyZ=false,
                cat=FeatureCat.Effects,
                reason="Shared Fresnel: dot(5)+sub(1)+pow f2(8)=14  +  mul f3(3)+add f3(3)+lerp f3(6)=12  =  26 ALU",
                tip="Shares Fresnel calc with Indicator — both ON costs 38 ALU total (not 52).",
                texProps=new string[0], isRealtime=true },
        };

        // ── Fixed / Base Cost ─────────────────────────────────────────────
        struct FixedCostDef { public string name, reason; public int aluOps, texSamples; }

        static readonly FixedCostDef[] k_FixedCosts =
        {
            new FixedCostDef { name = "ForwardLit Core", aluOps = 104, texSamples = 2,
                reason = "normalize×2 + MainLight+shadow + NdotL + ToonRamp + Diffuse + Metallic combine (SH moved to vertex)" },
            new FixedCostDef { name = "Outline Pass",    aluOps =  13, texSamples = 1,
                reason = "Always-rendered 2nd draw call: _MainLightColor uniform read + outlineColor compute + alpha" },
        };

        // Base score = ForwardLit(104 + 2×10) + Outline(13 + 1×10) = 147
        static int BaseScore() => k_FixedCosts.Sum(f => f.aluOps + f.texSamples * 10);

        enum CompressLevel { None, Low, Medium, High }

        // ── Texture Info ──────────────────────────────────────────────────
        struct TexInfo
        {
            public string        propName;
            public Texture       tex;
            public int           width, height;
            public TextureFormat format;
            public long          vramBytes;     // Default / editor runtime
            public long          vramPC;        // Standalone platform
            public long          vramAndroid;   // Android platform
            public long          vramIOS;       // iPhone platform
            public bool          isUncompressed;
            public bool          isOversized;
            public CompressLevel compress;
            public string        assetPath;
            public int           maxSize;

            public long VRAMFor(PlatformMode m)
            {
                switch (m)
                {
                    case PlatformMode.PC:      return vramPC      > 0 ? vramPC      : vramBytes;
                    case PlatformMode.Android: return vramAndroid > 0 ? vramAndroid : vramBytes;
                    case PlatformMode.iOS:     return vramIOS     > 0 ? vramIOS     : vramBytes;
                    default:                   return vramBytes;
                }
            }
        }

        // ── Feature Result ────────────────────────────────────────────────
        class FeatureResult
        {
            public FeatureDef    def;
            public bool          active;
            public List<TexInfo> textures = new List<TexInfo>();
        }

        // ── Material Analysis ─────────────────────────────────────────────
        class MatAnalysis
        {
            public Material            mat;
            public bool                isTransparent;
            public bool                isZLZ;
            public int                 score;        // feature score (base excluded)
            public List<FeatureResult> features = new List<FeatureResult>();
            public List<TexInfo>       baseTextures = new List<TexInfo>();
            public long                totalVRAM;    // Default / editor runtime
            public bool                foldout = false;

            public long TotalVRAMFor(PlatformMode m)
                => baseTextures.Sum(t => t.VRAMFor(m))
                 + features.Sum(f => f.textures.Sum(t => t.VRAMFor(m)));
        }

        // ═════════════════════════════════════════════════════════════════
        // Open
        // ═════════════════════════════════════════════════════════════════
        [MenuItem("Window/ZLZ/Shader Optimizer")]
        public static void OpenWindow()
        {
            var w = GetWindow<ZLZ_ShaderOptimizerWindow>("ZLZ Shader Optimizer");
            w.minSize = new Vector2(520, 400);
        }

        public static void OpenForTarget(GameObject go)
        {
            var w = GetWindow<ZLZ_ShaderOptimizerWindow>("ZLZ Shader Optimizer");
            w.minSize = new Vector2(520, 400);
            w._target = go;
            w._dirty  = true;
            w.Repaint();
        }

        // ═════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════
        void OnEnable()
        {
            titleContent = new GUIContent("ZLZ Shader Optimizer");
            Undo.postprocessModifications += OnPostprocessModifications;
            LoadThresholds();
        }

        void OnDisable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
        }

        UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (_target == null) return modifications;
            foreach (var mod in modifications)
            {
                if (mod.currentValue?.target is Material)
                {
                    _dirty = true;
                    Repaint();
                    break;
                }
            }
            return modifications;
        }

        // ═════════════════════════════════════════════════════════════════
        // OnGUI
        // ═════════════════════════════════════════════════════════════════
        void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            if (_showSettings) DrawSettingsPanel();

            if (_dirty && _target != null) Analyze();

            if (_target == null) { DrawNoTarget(); return; }

            DrawSummaryBar();
            DrawBarChart();

            if (Event.current.type == EventType.Repaint)
            {
                _topHeight    = GUILayoutUtility.GetLastRect().yMax;
                _detailStartY = _topHeight;
            }

            // One-frame lag compensation: _topHeight was measured last Repaint before PB toggled.
            // During the Layout pass of the same frame, correct it by the known panel height delta.
            if (_showSettings != _prevShowSettings)
            {
                const float rowH   = 26f;
                const float panelH = 10f + rowH + 3f + rowH + rowH * 4f + 10f; // must match DrawSettingsPanel
                float delta = _showSettings ? panelH : -panelH;
                _topHeight    += delta;
                _detailStartY += delta;
            }
            _prevShowSettings = _showSettings;

            bool hasSelection = _selectedIndex >= 0 && _selectedIndex < _analyses.Count;

            // One-frame lag compensation: _topHeight was measured last Repaint before selection changed.
            // During the Layout pass of the same frame, correct it by the known header height delta.
            if (hasSelection != _prevHasSelection)
            {
                const float detailHeaderH = 34f + 26f + 28f; // must match DrawDetailFixedHeader
                _topHeight += hasSelection ? detailHeaderH : -detailHeaderH;
            }
            _prevHasSelection = hasSelection;

            if (hasSelection)
            {
                DrawDetailFixedHeader(_analyses[_selectedIndex]);
                if (Event.current.type == EventType.Repaint)
                    _topHeight = GUILayoutUtility.GetLastRect().yMax;
            }

            float previewH = hasSelection ? CalcOptimizePreviewH() : 0f;
            float bottomH  = hasSelection ? 48f + previewH : 0f;
            float scrollH  = Mathf.Max(40f, position.height - _topHeight - bottomH);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(scrollH));
            if (hasSelection)
                DrawDetailPanel();
            else
            {
                GUILayout.Space(20);
                var hint = new GUIStyle(_subtleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
                EditorGUILayout.LabelField("Click a bar above to inspect its features and textures.", hint, GUILayout.ExpandWidth(true));
            }
            GUILayout.Space(8);
            DrawDisclaimer();
            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
                _scrollViewEndY = GUILayoutUtility.GetLastRect().yMax;

            if (hasSelection && _showOptimizePreview)
                DrawOptimizePreview();
            else
                _optPreviewStartY = _optPreviewEndY = 0f;
            if (hasSelection)
                DrawBottomBar(_analyses[_selectedIndex]);

            // ── Draw combined section borders (last → renders on top) ──────
            if (Event.current.type == EventType.Repaint)
            {
                // Group: Summary Bar + Bar Chart
                if (_summaryBarRect.width > 0 && _barChartRect.width > 0)
                    DrawBorder(Rect.MinMaxRect(_summaryBarRect.x, _summaryBarRect.y,
                                              _summaryBarRect.xMax, _barChartRect.yMax), C_SEC_CHART);

                // Group: Detail Fixed Header + Detail Panel (scroll area)
                if (hasSelection && _detailStartY > 0 && _scrollViewEndY > _detailStartY)
                    DrawBorder(new Rect(0, _detailStartY, position.width, _scrollViewEndY - _detailStartY), C_SEC_DETAIL);

                // Group: Bottom Bar (Feature Summary + VRAM Summary)
                if (hasSelection && _featureSummaryRect.width > 0 && _vramSummaryRect.width > 0)
                {
                    float topY  = Mathf.Min(_featureSummaryRect.y,    _vramSummaryRect.y);
                    float botY  = Mathf.Max(_featureSummaryRect.yMax, _vramSummaryRect.yMax);
                    DrawBorder(new Rect(0, topY, position.width, botY - topY), C_SEC_BOTTOM);
                }

                // Group: Character Texture Optimization (full preview)
                if (hasSelection && _optPreviewEndY > _optPreviewStartY)
                    DrawBorder(new Rect(0, _optPreviewStartY, position.width, _optPreviewEndY - _optPreviewStartY), C_WARN);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Toolbar
        // ─────────────────────────────────────────────────────────────────
        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            var next = (GameObject)EditorGUILayout.ObjectField(
                _target, typeof(GameObject), true, GUILayout.Width(220));
            if (EditorGUI.EndChangeCheck()) { _target = next; _dirty = true; }

            GUILayout.Space(12);

            for (int pi = 0; pi < k_PlatformModeLabels.Length; pi++)
            {
                var ps = new GUIStyle(EditorStyles.toolbarButton);
                if (_platformMode == (PlatformMode)pi) ps.fontStyle = FontStyle.Bold;
                float w = pi == 0 ? 36f : pi == 1 ? 56f : 36f;
                if (GUILayout.Button(k_PlatformModeLabels[pi], ps, GUILayout.Width(w))
                    && _platformMode != (PlatformMode)pi)
                { _platformMode = (PlatformMode)pi; Repaint(); }
            }

            GUILayout.FlexibleSpace();

            var gearStyle = new GUIStyle(EditorStyles.toolbarButton);
            if (_showSettings) gearStyle.fontStyle = FontStyle.Bold;
            if (GUILayout.Button("Budget Settings", gearStyle, GUILayout.Width(100)))
            {
                _showSettings = !_showSettings;
                Repaint();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                _dirty = true;

            EditorGUILayout.EndHorizontal();
        }

        void DrawSettingsPanel()
        {
            // Layout: header(24) + sep(3) + col-headers(24) + 4 data rows(4×26) + bottom(10) = 165f
            const float rowH = 26f;
            float panelH = 10f + rowH + 3f + rowH + rowH * 4f + 10f;

            Rect panel = GUILayoutUtility.GetRect(0, panelH, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(panel, new Color(0.13f, 0.13f, 0.17f, 1f));
            DrawBorder(panel, C_SEC_BUDGET);

            var hdrS = new GUIStyle(_boldStyle) { fontSize = 13, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0) };
            hdrS.normal.textColor = C_ACCENT;
            // secS and colS share the same style — section titles are column headers of equal weight
            var colS = new GUIStyle(_boldStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            var secS = new GUIStyle(colS);   // identical to colS — same size, same color
            var grpS = new GUIStyle(_boldStyle) { fontSize = 13, alignment = TextAnchor.MiddleCenter };

            // Proportional column widths — both halves fill the full panel width
            float halfW = (panel.width - 30f) * 0.5f;   // 30 = 10L + 10divider + 10R
            float grpW  = halfW * 0.20f;
            float lblW  = halfW * 0.16f;
            float valW  = (halfW - grpW - lblW) / 3f;

            // Left section: Shader Cost
            float lBase = panel.x + 10f;
            float lLbl  = lBase + grpW;
            float lPC   = lLbl  + lblW;
            float lAnd  = lPC   + valW;
            float lIOS  = lAnd  + valW;

            // Right section: VRAM — starts at exact midpoint + gap
            float rBase = panel.x + 10f + halfW + 10f;
            float rLbl  = rBase + grpW;
            float rPC   = rLbl  + lblW;
            float rAnd  = rPC   + valW;
            float rIOS  = rAnd  + valW;

            float y = panel.y + 10f;

            // ── Main header (centered across full width) ───────────────────
            EditorGUI.LabelField(new Rect(panel.x, y, panel.width, rowH), "Performance Budget", hdrS);
            y += rowH;

            EditorGUI.DrawRect(new Rect(panel.x, y, panel.width, 1f), new Color(0.22f, 0.22f, 0.28f));
            y += 3f;

            // ── Column headers ─────────────────────────────────────────────
            // Section titles span [grpW + lblW], centered → sit between group col and value cols
            EditorGUI.LabelField(new Rect(lBase, y, grpW + lblW, rowH), "Shader Cost", secS);
            EditorGUI.LabelField(new Rect(lPC,  y, valW, rowH), "PC",      colS);
            EditorGUI.LabelField(new Rect(lAnd, y, valW, rowH), "Android", colS);
            EditorGUI.LabelField(new Rect(lIOS, y, valW, rowH), "iOS",     colS);

            EditorGUI.LabelField(new Rect(rBase, y, grpW + lblW, rowH), "VRAM (MB)", secS);
            EditorGUI.LabelField(new Rect(rPC,  y, valW, rowH), "PC",      colS);
            EditorGUI.LabelField(new Rect(rAnd, y, valW, rowH), "Android", colS);
            EditorGUI.LabelField(new Rect(rIOS, y, valW, rowH), "iOS",     colS);

            // Vertical divider
            float divX = panel.x + 10f + halfW + 5f;
            EditorGUI.DrawRect(new Rect(divX, y, 1f, rowH * 5f), new Color(0.28f, 0.28f, 0.38f));
            y += rowH;

            // ── Left data rows: Shader Cost ───────────────────────────────
            float yL = y;
            EditorGUI.LabelField(new Rect(lBase, yL, grpW, rowH * 2f), "Per Material", grpS);
            DrawThreshFields(ref yL, "Light ≤", lLbl, lblW, lPC, lAnd, lIOS, valW, rowH,
                             ref _thPC_MatL, ref _thAnd_MatL, ref _thIOS_MatL);
            DrawThreshFields(ref yL, "Heavy >",  lLbl, lblW, lPC, lAnd, lIOS, valW, rowH,
                             ref _thPC_MatH, ref _thAnd_MatH, ref _thIOS_MatH);

            EditorGUI.LabelField(new Rect(lBase, yL, grpW, rowH * 2f), "Total All Mats", grpS);
            DrawThreshFields(ref yL, "Light ≤",  lLbl, lblW, lPC, lAnd, lIOS, valW, rowH,
                             ref _thPC_TotL, ref _thAnd_TotL, ref _thIOS_TotL);
            DrawThreshFields(ref yL, "Heavy >",  lLbl, lblW, lPC, lAnd, lIOS, valW, rowH,
                             ref _thPC_TotH, ref _thAnd_TotH, ref _thIOS_TotH);

            // ── Right data rows: VRAM ─────────────────────────────────────
            float yR = y;
            EditorGUI.LabelField(new Rect(rBase, yR, grpW, rowH * 2f), "Per Texture", grpS);
            DrawThreshFields(ref yR, "Light ≤", rLbl, lblW, rPC, rAnd, rIOS, valW, rowH,
                             ref _thPC_VramL, ref _thAnd_VramL, ref _thIOS_VramL);
            DrawThreshFields(ref yR, "Heavy >",  rLbl, lblW, rPC, rAnd, rIOS, valW, rowH,
                             ref _thPC_VramH, ref _thAnd_VramH, ref _thIOS_VramH);

            EditorGUI.LabelField(new Rect(rBase, yR, grpW, rowH * 2f), "Total VRAM", grpS);
            DrawThreshFields(ref yR, "Light ≤",  rLbl, lblW, rPC, rAnd, rIOS, valW, rowH,
                             ref _thPC_VramTotL, ref _thAnd_VramTotL, ref _thIOS_VramTotL);
            DrawThreshFields(ref yR, "Heavy >",  rLbl, lblW, rPC, rAnd, rIOS, valW, rowH,
                             ref _thPC_VramTotH, ref _thAnd_VramTotH, ref _thIOS_VramTotH);

            // ── Reset Defaults button ─────────────────────────────────────
            var btnS = new GUIStyle(EditorStyles.miniButton) { fontSize = 12 };
            btnS.normal.textColor = C_WARN;
            if (GUI.Button(new Rect(panel.xMax - 136f, panel.y + 10f, 126f, 22f),
                           "Reset Defaults", btnS))
            {
                GUIUtility.keyboardControl = 0;  // flush any active IntField edit buffer
                ResetThresholds(); SaveThresholds(); Repaint();
            }
        }

        void DrawThreshFields(ref float y, string rowLabel,
                              float lblX, float lblW,
                              float pcX, float andX, float iosX, float valW, float rowH,
                              ref int pc, ref int and, ref int ios)
        {
            var lblS   = new GUIStyle(_subtleStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            var fieldS = new GUIStyle(EditorStyles.numberField) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(lblX, y, lblW, rowH), rowLabel, lblS);
            EditorGUI.BeginChangeCheck();
            pc  = EditorGUI.IntField(new Rect(pcX  + 4f, y + 3f, valW - 12f, rowH - 6f), pc,  fieldS);
            and = EditorGUI.IntField(new Rect(andX + 4f, y + 3f, valW - 12f, rowH - 6f), and, fieldS);
            ios = EditorGUI.IntField(new Rect(iosX + 4f, y + 3f, valW - 12f, rowH - 6f), ios, fieldS);
            if (EditorGUI.EndChangeCheck()) SaveThresholds();
            y += rowH;
        }

        // ─────────────────────────────────────────────────────────────────
        // Bar Chart
        // ─────────────────────────────────────────────────────────────────
        void DrawBarChart()
        {
            if (_analyses.Count == 0) return;

            const float k_ChartH      = 110f;
            const float k_LabelH      =  60f;
            const float k_PadL        =  42f;   // left axis (Cost)
            const float k_PadR        =  46f;   // right axis (VRAM)
            const float k_BarMaxW     = 130f;   // max total pair width
            const float k_BarMinW     =  12f;
            const float k_BarGap      =   4f;   // gap between slots
            const float k_SubGap      =   8f;   // gap between Cost and VRAM bar within a slot
            const int   k_CostMax     = 600;    // left axis ceiling
            const float k_CostStep    = 100f;   // left axis label interval
            const int   k_VisibleSlots = 10;    // max visible materials at once

            int   n            = _analyses.Count;
            int   visibleCount = Mathf.Min(n, k_VisibleSlots);
            bool  needsScroll  = n > k_VisibleSlots;
            float scrollBarH   = needsScroll ? 18f : 0f;

            float totalH = k_ChartH + k_LabelH + 20f + scrollBarH;
            Rect  area   = GUILayoutUtility.GetRect(0, totalH, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.11f, 0.11f, 0.14f, 1f));
            _barChartRect = area;

            float plotX    = area.x + k_PadL;
            float plotW    = area.width - k_PadL - k_PadR;
            float slotW    = plotW / visibleCount;
            float pairW    = Mathf.Clamp(slotW - k_BarGap, k_BarMinW, k_BarMaxW);
            float subBarW  = (pairW - k_SubGap) * 0.5f;

            float chartTop  = area.y + 16f;
            float chartBot  = chartTop + k_ChartH;
            float labelY    = chartBot + 18f;
            float vramMaxMB = IsMobilePlatform ? 80f : 200f;
            float vramStep  = IsMobilePlatform ? 20f : 50f;

            // ── Clamp scroll ──────────────────────────────────────────────────
            int maxStart = Mathf.Max(0, n - k_VisibleSlots);
            _chartScrollPos = Mathf.Clamp(_chartScrollPos, 0f, maxStart);
            int startIdx = Mathf.RoundToInt(_chartScrollPos);

            // ── Left axis (Cost): grid lines + labels every 100, including 0 ──
            var axisLS = new GUIStyle(_subtleStyle) { fontSize = 11, alignment = TextAnchor.MiddleRight };
            axisLS.normal.textColor = new Color(0.50f, 0.50f, 0.58f);
            for (float g = 0f; g <= k_CostMax; g += k_CostStep)
            {
                float gy = chartBot - (g / k_CostMax) * (chartBot - chartTop);
                EditorGUI.DrawRect(new Rect(plotX, gy, plotW, 1f), new Color(0.24f, 0.24f, 0.30f, 0.85f));
                EditorGUI.LabelField(new Rect(area.x, gy - 9f, k_PadL - 3f, 18f), ((int)g).ToString(), axisLS);
            }

            // ── Right axis (VRAM): labels only, independent scale ─────────────
            var axisRS = new GUIStyle(_subtleStyle) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            axisRS.normal.textColor = new Color(0.30f, 0.68f, 0.48f);
            for (float v = 0f; v <= vramMaxMB + 0.01f; v += vramStep)
            {
                float vy    = chartBot - (v / vramMaxMB) * (chartBot - chartTop);
                string vLbl = $"{(int)v}MB";
                EditorGUI.LabelField(new Rect(plotX + plotW + 3f, vy - 9f, k_PadR - 3f, 18f), vLbl, axisRS);
            }

            // ── Light / Heavy threshold lines (Cost axis) ────────────────────
            float warnT = _platformMode == PlatformMode.PC      ? _thPC_MatL
                        : _platformMode == PlatformMode.Android ? _thAnd_MatL : _thIOS_MatL;
            float errT  = _platformMode == PlatformMode.PC      ? _thPC_MatH
                        : _platformMode == PlatformMode.Android ? _thAnd_MatH : _thIOS_MatH;
            float wy    = chartBot - (warnT / k_CostMax) * (chartBot - chartTop);
            float ey    = chartBot - (errT  / k_CostMax) * (chartBot - chartTop);
            EditorGUI.DrawRect(new Rect(plotX, wy, plotW, 2f), new Color(C_WARN.r, C_WARN.g, C_WARN.b, 0.85f));
            EditorGUI.DrawRect(new Rect(plotX, ey, plotW, 2f), new Color(C_ERR.r,  C_ERR.g,  C_ERR.b,  0.85f));

            var thLblS = new GUIStyle(_subtleStyle) { fontSize = 10, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            thLblS.normal.textColor = new Color(C_WARN.r, C_WARN.g, C_WARN.b, 0.90f);
            EditorGUI.LabelField(new Rect(plotX + 3f, wy - 14f, 60f, 14f), "Medium", thLblS);
            thLblS.normal.textColor = new Color(C_ERR.r, C_ERR.g, C_ERR.b, 0.90f);
            EditorGUI.LabelField(new Rect(plotX + 3f, ey - 14f, 60f, 14f), "Heavy", thLblS);

            // ── Baseline ──────────────────────────────────────────────────────
            EditorGUI.DrawRect(new Rect(plotX, chartBot, plotW, 1f), new Color(0.30f, 0.30f, 0.38f));

            // ── Axis titles: "Cost" under left "0", "VRAM" under right "0MB" ──
            var legLS = new GUIStyle(_boldStyle) { fontSize = 10, alignment = TextAnchor.UpperRight };
            legLS.normal.textColor = new Color(0.55f, 0.55f, 0.65f);
            var legRS = new GUIStyle(_boldStyle) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            legRS.normal.textColor = new Color(0.30f, 0.68f, 0.48f);
            EditorGUI.LabelField(new Rect(area.x,             chartBot + 8f,  k_PadL - 3f, 28f), "Shader\nCost", legLS);
            EditorGUI.LabelField(new Rect(plotX + plotW + 3f, chartBot + 16f, k_PadR - 3f, 14f), "VRAM", legRS);

            // ── Per-material grouped bars (visible window only) ───────────────
            for (int vi = 0; vi < visibleCount; vi++)
            {
                int   i     = startIdx + vi;
                if (i >= n) break;
                var   a     = _analyses[i];
                float slotX = plotX + vi * slotW;
                float pairX = slotX + (slotW - pairW) * 0.5f;
                float costX = pairX;
                float vramX = pairX + subBarW + k_SubGap;
                bool  sel   = i == _selectedIndex;

                // — Cost bar (left) — scale: 0..k_CostMax
                int   score = MatAluTotal(a);
                float cPct  = Mathf.Clamp01((float)score / k_CostMax);
                float cBarH = Mathf.Max(2f, cPct * (chartBot - chartTop));
                float cBarY = chartBot - cBarH;
                Color cCol  = TierColor(score, _platformMode);
                if (sel) cCol = Color.Lerp(cCol, Color.white, 0.25f);
                EditorGUI.DrawRect(new Rect(costX, cBarY, subBarW, cBarH), cCol);

                if (sel)
                {
                    EditorGUI.DrawRect(new Rect(costX - 1, cBarY - 1, subBarW + 2, 1),     Color.white);
                    EditorGUI.DrawRect(new Rect(costX - 1, cBarY - 1, 1, cBarH + 2),       Color.white);
                    EditorGUI.DrawRect(new Rect(costX + subBarW, cBarY - 1, 1, cBarH + 2), Color.white);
                }

                // Cost number — always above bar
                if (score > 0)
                {
                    var numS = new GUIStyle(_boldStyle) { fontSize = 20, alignment = TextAnchor.LowerCenter };
                    numS.normal.textColor = cCol;
                    float numY = Mathf.Max(chartTop, cBarY - 22f);
                    EditorGUI.LabelField(new Rect(costX, numY, subBarW, 22f), score.ToString(), numS);
                }

                // — VRAM bar (right) — independent scale: 0..vramMaxMB
                long  vramVal = a.TotalVRAMFor(_platformMode);
                float vMB   = vramVal / (1024f * 1024f);
                float vPct  = Mathf.Clamp01(vMB / vramMaxMB);
                float vBarH = Mathf.Max(2f, vPct * (chartBot - chartTop));
                float vBarY = chartBot - vBarH;
                Color vCol  = VRAMColor(vramVal, _platformMode);
                if (sel) vCol = Color.Lerp(vCol, Color.white, 0.25f);
                EditorGUI.DrawRect(new Rect(vramX, vBarY, subBarW, vBarH), vCol);

                // VRAM number — always above bar (show "0 MB" when no textures)
                {
                    var vNumS = new GUIStyle(_boldStyle) { fontSize = 16, alignment = TextAnchor.LowerCenter };
                    vNumS.normal.textColor = vramVal > 0 ? vCol : new Color(0.40f, 0.40f, 0.46f);
                    string vLabel = vramVal > 0 ? FmtBytes(vramVal) : "0 MB";
                    float vNumY = Mathf.Max(chartTop, vBarY - 22f);
                    EditorGUI.LabelField(new Rect(vramX, vNumY, subBarW, 22f), vLabel, vNumS);
                }

                // — Name label (split at '_' closest to middle for balanced 2 lines) —
                string rawName = a.mat.name;
                int mid2 = rawName.Length / 2, bi = -1, bestDist2 = int.MaxValue;
                for (int c = 0; c < rawName.Length; c++)
                {
                    if (rawName[c] == '_')
                    {
                        int d = Mathf.Abs(c - mid2);
                        if (d < bestDist2) { bestDist2 = d; bi = c; }
                    }
                }
                string dispName = bi > 0 && bi < rawName.Length - 1
                    ? rawName.Substring(0, bi + 1) + "\n" + rawName.Substring(bi + 1)
                    : rawName;
                var nameS = new GUIStyle(_subtleStyle) { fontSize = 11, alignment = TextAnchor.UpperCenter, wordWrap = true };
                if (sel) { nameS.fontStyle = FontStyle.Bold; nameS.normal.textColor = Color.white; }
                EditorGUI.LabelField(new Rect(slotX, labelY, slotW, k_LabelH), dispName, nameS);

                // — Hit rect —
                Rect hitRect = new Rect(slotX, chartTop, slotW, chartBot + k_LabelH - chartTop);
                if (Event.current.type == EventType.MouseDown && hitRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = (i == _selectedIndex) ? -1 : i;
                    _selectedMatId = _selectedIndex >= 0 ? _analyses[_selectedIndex].mat.GetInstanceID() : -1;
                    _scroll        = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }
                EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.Link);
            }

            // ── Horizontal scrollbar (only when n > k_VisibleSlots) ───────────
            if (needsScroll)
            {
                Rect sbRect = new Rect(plotX, area.yMax - scrollBarH + 2f, plotW, 14f);
                float newScroll = GUI.HorizontalScrollbar(sbRect, _chartScrollPos, k_VisibleSlots, 0, n);
                if (!Mathf.Approximately(newScroll, _chartScrollPos))
                {
                    _chartScrollPos = newScroll;
                    Repaint();
                }
            }
        }

        void DrawChartLine(Rect area, float padX, float top, float bot,
                           int maxScore, float threshold, Color col)
        {
            if (threshold >= maxScore) return;
            float y = bot - (threshold / maxScore) * (bot - top);
            EditorGUI.DrawRect(new Rect(area.x + padX, y, area.width - padX * 2f, 1f),
                new Color(col.r, col.g, col.b, 0.55f));
            var s = new GUIStyle(_subtleStyle) { fontSize = 9, alignment = TextAnchor.MiddleLeft };
            s.normal.textColor = col;
            EditorGUI.LabelField(new Rect(area.x + padX + 2f, y - 13f, 60f, 14f),
                threshold.ToString(), s);
        }

        static void DrawBorder(Rect r, Color c, float t = 2f)
        {
            EditorGUI.DrawRect(new Rect(r.x,          r.y,          r.width,   t),         c);
            EditorGUI.DrawRect(new Rect(r.x,          r.yMax - t,   r.width,   t),         c);
            EditorGUI.DrawRect(new Rect(r.x,          r.y,          t,         r.height),  c);
            EditorGUI.DrawRect(new Rect(r.xMax - t,   r.y,          t,         r.height),  c);
        }

        // ─────────────────────────────────────────────────────────────────
        // Detail Panel
        // ─────────────────────────────────────────────────────────────────
        void DrawDetailPanel()
        {
            var a = _analyses[_selectedIndex];

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.40f));
            DrawLeftFeatures(a);
            EditorGUILayout.EndVertical();

            Rect div = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(div, new Color(0.28f, 0.28f, 0.34f, 1f));

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawRightOverview(a);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

        }

        // ─────────────────────────────────────────────────────────────────
        // Fixed Header (pinned above scroll)
        // ─────────────────────────────────────────────────────────────────
        void DrawDetailFixedHeader(MatAnalysis a)
        {
            Rect hdr = GUILayoutUtility.GetRect(0, 34f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(hdr, C_CARD_OPEN);

            string typeLabel = IsMaterialVariant(a.mat) ? "Material Variant" : "Material";
            var nameS = new GUIStyle(_boldStyle) { fontSize = 13 };
            EditorGUI.LabelField(new Rect(hdr.x + 12, hdr.y, hdr.width * 0.55f, hdr.height),
                $"{typeLabel} : {a.mat.name}", nameS);


            int activeCount = a.features.Count(f => f.active);
            int texCount    = a.baseTextures.Count(t => t.tex != null)
                            + a.features.Sum(f => f.textures.Count(t => t.tex != null));

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.40f));
            DrawPanelHeader($"Features  ({activeCount} ON / {a.features.Count - activeCount} OFF)");
            DrawFeatureTableHeader();
            EditorGUILayout.EndVertical();

            Rect div = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(div, new Color(0.28f, 0.28f, 0.34f, 1f));

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawPanelHeader($"Textures  ({texCount})");
            DrawTexTableHeader();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        static bool IsMaterialVariant(Material mat)
        {
#if UNITY_2022_1_OR_NEWER
            return mat.parent != null;
#else
            return mat.name.IndexOf("variant", System.StringComparison.OrdinalIgnoreCase) >= 0;
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        // No Target
        // ─────────────────────────────────────────────────────────────────
        void DrawNoTarget()
        {
            GUILayout.FlexibleSpace();
            var s = new GUIStyle(EditorStyles.label)
                { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 11 };
            s.normal.textColor = new Color(0.55f, 0.55f, 0.60f);
            EditorGUILayout.LabelField(
                "Drag a character GameObject into the field above,\nor open from the ZLZ Character Dashboard.",
                s, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // Optimization Preview  (pinned above Bottom Bar)
        // ─────────────────────────────────────────────────────────────────
        void DrawOptimizePreview()
        {
            var allTex = CachedOptTex;
            var bySlot = allTex.GroupBy(t => t.propName).ToList();

            // Lazy-init per-slot per-platform max sizes
            if (_optSlotMaxSize == null)
                _optSlotMaxSize = new Dictionary<string, Dictionary<string, int>>();
            foreach (var g in bySlot)
                if (!_optSlotMaxSize.ContainsKey(g.Key))
                    _optSlotMaxSize[g.Key] = new Dictionary<string, int>();

            var plats = new List<PlatInfo>();
            if (_optPC)      plats.Add(new PlatInfo { mode = PlatformMode.PC,      unityName = "Standalone", label = "PC" });
            if (_optAndroid) plats.Add(new PlatInfo { mode = PlatformMode.Android, unityName = "Android",    label = "Android" });
            if (_optIOS)     plats.Add(new PlatInfo { mode = PlatformMode.iOS,     unityName = "iPhone",     label = "iOS" });

            int numPlat = plats.Count;

            // ── Header ────────────────────────────────────────────────────
            Rect hdr = GUILayoutUtility.GetRect(0, 38f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(hdr, new Color(0.13f, 0.17f, 0.23f, 1f));
            _optPreviewStartY = hdr.y;
            var hdrS = new GUIStyle(_boldStyle) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            hdrS.normal.textColor = C_WARN;
            EditorGUI.LabelField(new Rect(hdr.x + 10, hdr.y, hdr.width - 20f, hdr.height),
                "Character Texture Optimization", hdrS);

            _optQuality = 1; // Balanced (hardcoded)

            // ── Column layout ──────────────────────────────────────────────
            // [Slot] | per-platform: [MaxSize▼ | Current | Optimized]  (all values centered)
            float slotFrac  = numPlat == 1 ? 0.22f : numPlat == 2 ? 0.18f : 0.15f;
            float platFrac  = (1f - slotFrac) / numPlat;

            // Returns (x, width) for MaxSize (sub=0), Current (sub=1), or Optimized (sub=2) within platform pi
            (float x, float w) PlatCell(Rect r, int pi, int sub)
            {
                float px   = r.x + r.width * (slotFrac + platFrac * pi);
                float pw   = r.width * platFrac;
                float msW  = pw * 0.32f;
                float datW = (pw - msW) * 0.5f;
                switch (sub)
                {
                    case 0: return (px, msW);
                    case 1: return (px + msW, datW);
                    default: return (px + msW + datW, datW);
                }
            }

            Color[] platAccents = { C_ACCENT, C_OK, C_WARN };
            int[]    maxSizeOpts = { 256, 512, 1024, 2048, 4096 };
            string[] maxSizeLbls = { "256", "512", "1024", "2048", "4096" };

            // ── Platform group header ──────────────────────────────────────
            Rect phdr = GUILayoutUtility.GetRect(0, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(phdr, new Color(0.16f, 0.17f, 0.22f, 1f));
            var colHdrS = new GUIStyle(_subtleStyle) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(new Rect(phdr.x + 8f, phdr.y, phdr.width * slotFrac, phdr.height), "Slot", colHdrS);
            EditorGUI.DrawRect(new Rect(phdr.x + phdr.width * slotFrac, phdr.y, 1f, phdr.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));
            // Platform names centered over their full column width
            for (int pi = 0; pi < numPlat; pi++)
            {
                float px = phdr.x + phdr.width * (slotFrac + platFrac * pi);
                float pw = phdr.width * platFrac;
                if (pi > 0)
                    EditorGUI.DrawRect(new Rect(px, phdr.y, 1f, phdr.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));
                EditorGUI.DrawRect(new Rect(px, phdr.y, pw, 2f), platAccents[pi] * 0.7f);
                var platLblS = new GUIStyle(_boldStyle) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
                platLblS.normal.textColor = platAccents[pi];
                EditorGUI.LabelField(new Rect(px, phdr.y, pw, phdr.height), plats[pi].label, platLblS);
            }

            // ── Sub-column header ──────────────────────────────────────────
            Rect shdr = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(shdr, new Color(0.14f, 0.14f, 0.18f, 1f));
            EditorGUI.DrawRect(new Rect(shdr.x + shdr.width * slotFrac, shdr.y, 1f, shdr.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));
            var subHdrS = new GUIStyle(_subtleStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            for (int pi = 0; pi < numPlat; pi++)
            {
                var (msx,  msw) = PlatCell(shdr, pi, 0);
                var (curx, cw)  = PlatCell(shdr, pi, 1);
                var (optx, ow)  = PlatCell(shdr, pi, 2);
                if (pi > 0)
                    EditorGUI.DrawRect(new Rect(msx, shdr.y, 1f, shdr.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));
                EditorGUI.LabelField(new Rect(msx,  shdr.y, msw, shdr.height), "Max Size",  subHdrS);
                EditorGUI.LabelField(new Rect(curx, shdr.y, cw,  shdr.height), "Current",   subHdrS);
                EditorGUI.LabelField(new Rect(optx, shdr.y, ow,  shdr.height), "Optimized", subHdrS);
            }

            // ── Slot rows ─────────────────────────────────────────────────
            var slotLblS   = new GUIStyle(_labelStyle) { fontSize = 11 };
            var valCenterS = new GUIStyle(_boldStyle)  { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < bySlot.Count; i++)
            {
                var grp   = bySlot[i];
                var texes = grp.ToList();
                int  n    = texes.Count;

                Rect row = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row, i % 2 == 0
                    ? new Color(0.14f, 0.14f, 0.17f, 1f)
                    : new Color(0.12f, 0.12f, 0.15f, 1f));
                EditorGUI.DrawRect(new Rect(row.x + row.width * slotFrac, row.y, 1f, row.height), new Color(0.32f, 0.32f, 0.46f, 0.7f));

                // Slot name
                string slotLabel = n > 1 ? $"{grp.Key}  ×{n}" : grp.Key;
                EditorGUI.LabelField(new Rect(row.x + 8f, row.y, row.width * slotFrac - 8f, row.height),
                    slotLabel, slotLblS);

                // Per-platform MaxSize + Current + Optimized
                for (int pi = 0; pi < numPlat; pi++)
                {
                    var (msx,  msw2) = PlatCell(row, pi, 0);
                    var (curx, cw2)  = PlatCell(row, pi, 1);
                    var (optx, ow2)  = PlatCell(row, pi, 2);

                    if (pi > 0)
                        EditorGUI.DrawRect(new Rect(msx, row.y, 1f, row.height), new Color(0.32f, 0.32f, 0.46f, 0.7f));

                    // Max Size dropdown per platform (lazy-init)
                    string platKey = plats[pi].unityName;
                    if (!_optSlotMaxSize[grp.Key].ContainsKey(platKey))
                        _optSlotMaxSize[grp.Key][platKey] = DefaultMaxSizeFor(grp.Key, platKey);
                    float popY = row.y + (row.height - 18f) * 0.5f;
                    int curIdx = System.Array.IndexOf(maxSizeOpts, _optSlotMaxSize[grp.Key][platKey]);
                    if (curIdx < 0) curIdx = System.Array.IndexOf(maxSizeOpts, DefaultMaxSizeFor(grp.Key, platKey));
                    if (curIdx < 0) curIdx = 3;
                    var popStyle = new GUIStyle(EditorStyles.popup) { alignment = TextAnchor.MiddleCenter };
                    int newIdx = EditorGUI.Popup(new Rect(msx + 2f, popY, msw2 - 4f, 18f), curIdx, maxSizeLbls, popStyle);
                    if (newIdx != curIdx)
                        _optSlotMaxSize[grp.Key][platKey] = maxSizeOpts[newIdx];

                    long cur = 0, opt = 0;
                    foreach (var t in texes)
                    {
                        cur += t.VRAMFor(plats[pi].mode);
                        opt += SimulateOptimizedVRAM(t, plats[pi].mode);
                    }
                    bool improved = opt < cur;

                    var curS = new GUIStyle(valCenterS);
                    curS.normal.textColor = VRAMColor(cur, plats[pi].mode);
                    EditorGUI.LabelField(new Rect(curx, row.y, cw2, row.height),
                        cur == 0 ? "--" : FmtBytes(cur), curS);

                    var optS = new GUIStyle(valCenterS);
                    optS.normal.textColor = improved ? C_OK : new Color(0.55f, 0.75f, 0.55f);
                    EditorGUI.LabelField(new Rect(optx, row.y, ow2, row.height),
                        cur == 0 ? "--" : (improved ? FmtBytes(opt) : "✔"), optS);
                }
            }

            // ── Divider ───────────────────────────────────────────────────
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1f, GUILayout.ExpandWidth(true)),
                new Color(0.28f, 0.28f, 0.38f, 1f));

            // ── Summary + Apply row (single line) ─────────────────────────
            Rect sumRow = GUILayoutUtility.GetRect(0, 34f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(sumRow, new Color(0.12f, 0.12f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(sumRow.x + sumRow.width * slotFrac, sumRow.y, 1f, sumRow.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));
            var applyLblS = new GUIStyle(_subtleStyle) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            float sumSlotW = sumRow.width * slotFrac;
            EditorGUI.LabelField(new Rect(sumRow.x + 8f, sumRow.y, sumSlotW, sumRow.height), "Apply", applyLblS);

            for (int pi = 0; pi < numPlat; pi++)
            {
                long before = allTex.Sum(t => t.VRAMFor(plats[pi].mode));
                long after  = allTex.Sum(t => SimulateOptimizedVRAM(t, plats[pi].mode));
                long saved  = before - after;

                float px  = sumRow.x + sumRow.width * (slotFrac + platFrac * pi);
                float pw  = sumRow.width * platFrac;
                float cy2 = sumRow.y + sumRow.height * 0.5f;

                // vertical divider
                if (pi > 0)
                    EditorGUI.DrawRect(new Rect(px, sumRow.y, 1f, sumRow.height), new Color(0.38f, 0.38f, 0.52f, 0.9f));

                // VRAM text + Apply button centered as a unit within the platform column
                const float btnW = 86f, btnH = 22f, gap = 20f;
                var savS = new GUIStyle(_boldStyle) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
                savS.normal.textColor = saved > 0 ? C_WARN : new Color(0.55f, 0.75f, 0.55f);
                string infoStr = before == 0 ? "—" : $"{FmtBytes(before)} → {FmtBytes(after)}";
                float textW  = savS.CalcSize(new GUIContent(infoStr)).x;
                float unitW  = textW + gap + btnW;
                float startX = px + (pw - unitW) * 0.5f;

                EditorGUI.LabelField(new Rect(startX, cy2 - 12f, textW, 24f), infoStr, savS);

                Rect btnR = new Rect(startX + textW + gap, cy2 - btnH * 0.5f, btnW, btnH);
                var btnS = new GUIStyle(EditorStyles.miniButton) { fontSize = 10, fontStyle = FontStyle.Bold };
                btnS.normal.textColor = saved > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(btnR, saved > 0 ? $"Apply {plats[pi].label}" : "✔ OK", btnS) && saved > 0)
                    ApplyOptimization(plats[pi].mode, plats[pi].unityName);
            }

            // ── Apply All footer ──────────────────────────────────────────
            if (numPlat > 1)
            {
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1f, GUILayout.ExpandWidth(true)),
                    new Color(0.28f, 0.28f, 0.38f, 1f));
                Rect footer = GUILayoutUtility.GetRect(0, 38f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(footer, new Color(0.10f, 0.10f, 0.14f, 1f));

                long allSaved = plats.Sum(pl =>
                    allTex.Sum(t => t.VRAMFor(pl.mode) - SimulateOptimizedVRAM(t, pl.mode)));
                var footerS = new GUIStyle(_subtleStyle) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
                EditorGUI.LabelField(new Rect(footer.x + 12, footer.y, footer.width * 0.6f, footer.height),
                    $"Total savings across all platforms   {FmtBytes(allSaved)}", footerS);
                const float allW = 100f, allH = 24f;
                Rect allR = new Rect(footer.x + footer.width - allW - 8f,
                    footer.y + (footer.height - allH) * 0.5f, allW, allH);
                var allS = new GUIStyle(EditorStyles.miniButton) { fontSize = 12, fontStyle = FontStyle.Bold };
                allS.normal.textColor = allSaved > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(allR, "Apply All", allS) && allSaved > 0)
                    ApplyAllOptimization(plats);
            }

            _optPreviewEndY = GUILayoutUtility.GetLastRect().yMax;
        }

        void ApplyAllOptimization(List<PlatInfo> plats)
        {
            var texList = CachedOptTex
                .Where(t => t.tex != null && !string.IsNullOrEmpty(t.assetPath))
                .ToList();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var t in texList)
                {
                    var imp = AssetImporter.GetAtPath(t.assetPath) as TextureImporter;
                    if (imp == null) continue;
                    foreach (var pl in plats)
                    {
                        int slotCap       = GetSlotMaxSize(t.propName, pl.unityName);
                        int cap           = t.maxSize > 0 ? Mathf.Min(t.maxSize, slotCap) : slotCap;
                        var ps            = imp.GetPlatformTextureSettings(pl.unityName);
                        ps.overridden     = true;
                        ps.format         = RecommendedFormat(t, pl.mode);
                        ps.maxTextureSize = cap;
                        imp.SetPlatformTextureSettings(ps);
                    }
                    imp.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            _cachedOptTex = null;
            _dirty        = true;
            Repaint();
        }

        void ApplyOptimization(PlatformMode mode, string platform)
        {
            var texList = CachedOptTex
                .Where(t => t.tex != null && !string.IsNullOrEmpty(t.assetPath))
                .ToList();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var t in texList)
                {
                    var imp = AssetImporter.GetAtPath(t.assetPath) as TextureImporter;
                    if (imp == null) continue;
                    int slotCap       = GetSlotMaxSize(t.propName, platform);
                    int cap           = t.maxSize > 0 ? Mathf.Min(t.maxSize, slotCap) : slotCap;
                    var ps            = imp.GetPlatformTextureSettings(platform);
                    ps.overridden     = true;
                    ps.format         = RecommendedFormat(t, mode);
                    ps.maxTextureSize = cap;
                    imp.SetPlatformTextureSettings(ps);
                    imp.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            _cachedOptTex = null;
            _dirty        = true;
            Repaint();
        }

        void DrawDisclaimer()
        {
            GUILayout.Space(4);
            Rect area = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.14f, 0.14f, 0.17f, 1f));

            var s = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = false };
            s.normal.textColor = new Color(0.45f, 0.45f, 0.50f);
            EditorGUI.LabelField(
                new Rect(area.x + 10, area.y + 7, area.width - 20, 16),
                "Scores are relative complexity estimates, not actual GPU measurements. " +
                "Use Arm Mobile Studio, Snapdragon Profiler, or Xcode GPU Frame Capture for real performance data.",
                s);
        }

        // ─────────────────────────────────────────────────────────────────
        // Summary Bar
        // ─────────────────────────────────────────────────────────────────
        void DrawSummaryBar()
        {
            Rect bar = GUILayoutUtility.GetRect(0, 44f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, C_HEADER);
            _summaryBarRect = bar;

            int  matCount  = _analyses.Count;
            int  texCount  = _analyses.Sum(a =>
                a.baseTextures.Count(t => t.tex != null) +
                a.features.Sum(f => f.textures.Count(t => t.tex != null)));
            long totalVRAM = _analyses.Sum(a => a.TotalVRAMFor(_platformMode));

            // Left: title + material/texture count
            string titleText = $"ZLZ Shader Optimizer  —  {_target.name}";
            string statsText = $"  ·  {matCount} materials  ·  {texCount} textures";

            var titleS = new GUIStyle(_titleStyle) { fontSize = 15 };
            var statsS = new GUIStyle(_subtleStyle) { fontSize = 14 };

            float textY  = bar.y + (bar.height - 20f) * 0.5f;
            float titleW = titleS.CalcSize(new GUIContent(titleText)).x;
            float startX = bar.x + 12f;

            EditorGUI.LabelField(new Rect(startX,          textY, titleW + 4f,               20f), titleText, titleS);
            EditorGUI.LabelField(new Rect(startX + titleW, textY, bar.width * 0.45f - titleW, 20f), statsText, statsS);

            // Right: VRAM + score + tier (main summary)
            int total = TotalScore();
            DrawSummaryRating(bar, totalVRAM, k_PlatformModeLabels[(int)_platformMode],
                total, TotalTierStr(total, _platformMode),
                TotalTierColor(total, _platformMode));
        }

        void DrawSummaryRating(Rect bar, long totalVRAM, string platformLabel,
                               int score, string tierStr, Color tierCol)
        {
            // Layout (right-to-left): "Cost 992  ·  VRAM 78.7 MB  ·  iOS · All Materials"
            float right = bar.x + bar.width - 12f;
            float cy    = bar.y + bar.height * 0.5f;

            var numS = new GUIStyle(_boldStyle)   { fontSize = 15, alignment = TextAnchor.MiddleRight };
            var dotS = new GUIStyle(_subtleStyle)  { fontSize = 15, alignment = TextAnchor.MiddleRight };
            var lblS = new GUIStyle(_subtleStyle)  { fontSize = 15, alignment = TextAnchor.MiddleRight };

            // Cost XXX (rightmost, tier color)
            numS.normal.textColor = tierCol;
            string costText = $"Shader Cost  {score}";
            float  costW    = numS.CalcSize(new GUIContent(costText)).x;
            EditorGUI.LabelField(new Rect(right - costW, cy - 10f, costW, 20f), costText, numS);

            // · dot
            float dotW  = dotS.CalcSize(new GUIContent("  ·  ")).x;
            float dot1X = right - costW - dotW;
            EditorGUI.LabelField(new Rect(dot1X, cy - 10f, dotW, 20f), "  ·  ", dotS);

            // VRAM XXX (total VRAM color)
            numS.normal.textColor = VRAMTotalColor(totalVRAM, _platformMode);
            string vramText = $"VRAM  {FmtBytes(totalVRAM)}";
            float  vramW    = numS.CalcSize(new GUIContent(vramText)).x;
            EditorGUI.LabelField(new Rect(dot1X - vramW, cy - 10f, vramW, 20f), vramText, numS);

            // · dot
            float dot2X = dot1X - vramW - dotW;
            EditorGUI.LabelField(new Rect(dot2X, cy - 10f, dotW, 20f), "  ·  ", dotS);

            // iOS · All Materials (leftmost, subtle)
            string scopeText = $"{platformLabel}  ·  All Materials";
            float  scopeW    = lblS.CalcSize(new GUIContent(scopeText)).x;
            EditorGUI.LabelField(new Rect(dot2X - scopeW, cy - 10f, scopeW, 20f), scopeText, lblS);
        }

        void DrawLeftFeatures(MatAnalysis a)
        {
            if (!a.isZLZ)
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("  Non-ZLZ shader — no feature data.", _subtleStyle);
                GUILayout.Space(4);
                return;
            }

            DrawFeatureList(a.features, a.mat, a.isTransparent);
        }

        void DrawRightOverview(MatAnalysis a)
        {
            if (a.baseTextures.Count > 0)
            {
                DrawTexGroupLabel("Base  (always sampled)", new Color(0.42f, 0.72f, 1.00f));
                DrawTexTable(a.baseTextures);
            }

            foreach (var f in a.features)
            {
                if (!f.active || f.textures.Count == 0) continue;
                DrawTexGroupLabel($"Feature: {f.def.name}", ScoreColor(FeatureScore(f.def)));
                DrawTexTable(f.textures);
            }

            GUILayout.Space(4);
            DrawPanelHeader("Material Info");
            int rq = a.mat.renderQueue;
            string rqLabel = rq >= 3000 ? $"Transparent ({rq})" : rq >= 2450 ? $"Alpha Test ({rq})" : $"Opaque ({rq})";
            Color  rqColor = rq >= 3000 ? C_WARN : rq >= 2450 ? C_ACCENT : C_OK;
            DrawInfoRow("Render Queue", rqLabel, 0, rqColor);
            DrawInfoRow("Shader", a.isZLZ ? "ZLZ Anime Toon" : (a.mat.shader?.name ?? "—"), 1, a.isZLZ ? C_OK : _subtleStyle.normal.textColor);
        }

        void DrawTexTableHeader()
        {
            const float k_Thumb = 90f;
            Rect r = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.09f, 0.09f, 0.12f, 1f));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(0.30f, 0.30f, 0.40f, 1f));
            float x = r.x + 8f + k_Thumb + 6f;
            float w = r.width - 8f - k_Thumb - 6f;
            ColHdr(r.y, r.height, x, w, "Texture", 0.00f, 0.32f, 14);
            ColHdr(r.y, r.height, x, w, "Size",                     0.32f, 0.16f, 14);
            ColHdr(r.y, r.height, x, w, "Compress",                 0.48f, 0.20f, 14);
            ColHdr(r.y, r.height, x, w, "VRAM",                     0.68f, 0.14f, 14);
            ColHdr(r.y, r.height, x, w, "Status",                   0.82f, 0.18f, 14);
        }

        void DrawTexGroupLabel(string label, Color accentColor)
        {
            Rect r = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.22f, 0.19f, 0.30f, 1f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 4f, r.height), accentColor);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(accentColor.r * 0.5f, accentColor.g * 0.5f, accentColor.b * 0.5f, 0.6f));
            var s = new GUIStyle(_boldStyle) { fontSize = 14 };
            s.normal.textColor = accentColor;
            EditorGUI.LabelField(new Rect(r.x + 12, r.y, r.width - 12, r.height), label, s);
        }

        void DrawPanelHeader(string title)
        {
            Rect r = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, C_SUB_HEADER);
            var s = new GUIStyle(_boldStyle) { fontSize = 14 };
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title, s);
        }

        void DrawInfoRow(string label, string value, int idx, Color valueColor)
        {
            Rect r = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, C_ROW_BASE);

            var ls = new GUIStyle(_subtleStyle) { fontSize = 14 };
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width * 0.40f, r.height), label, ls);

            var vs = new GUIStyle(_labelStyle) { fontSize = 14 };
            vs.normal.textColor = valueColor;
            EditorGUI.LabelField(new Rect(r.x + r.width * 0.40f, r.y, r.width * 0.58f, r.height), value, vs);
        }

        // ─────────────────────────────────────────────────────────────────
        // Feature List
        // Column layout (% of row.width):
        //   STATE badge       : x+4  … x+46  (fixed ~42 px)
        //   FEATURE name      : x+66 … 50%
        //   Shader Cost       : 51%  … 70%
        //   Texture Sampling  : 71%  … 100%
        // ─────────────────────────────────────────────────────────────────
        void DrawFeatureList(List<FeatureResult> features, Material mat, bool isTransparent = false)
        {
            var categories = new[]
            {
                FeatureCat.HairSystem,
                FeatureCat.SurfaceDetail,
                FeatureCat.Lighting,
                FeatureCat.Effects,
            };

            DrawBaseCostSection(isTransparent);

            foreach (var cat in categories)
            {
                var group = features.Where(f => f.def.cat == cat).ToList();
                if (group.Count == 0) continue;

                DrawFeatureCatHeader(CatLabel(cat));

                foreach (var f in group)
                    DrawFeatureListRow(f, mat);
            }
        }

        void DrawFeatureTableHeader()
        {
            Rect r = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.09f, 0.09f, 0.12f, 1f));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(0.30f, 0.30f, 0.40f, 1f));

            var s = new GUIStyle(_boldStyle) { fontSize = 13 };
            s.normal.textColor = new Color(0.48f, 0.48f, 0.58f);
            var sc = new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };

            float x = r.x; float w = r.width;
            EditorGUI.LabelField(new Rect(x + 4,          r.y, 46,         r.height), "STATE",   s);
            EditorGUI.LabelField(new Rect(x + 66,         r.y, w * 0.30f,  r.height), "FEATURE", s);
            EditorGUI.LabelField(new Rect(x + w * 0.51f, r.y, w * 0.19f, r.height), "Shader Cost",       sc);
            EditorGUI.LabelField(new Rect(x + w * 0.71f, r.y, w * 0.29f, r.height), "Texture Sampling",  sc);
        }

        void DrawFeatureCatHeader(string label)
        {
            Rect r = GUILayoutUtility.GetRect(0, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.22f, 1f));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(0.28f, 0.28f, 0.36f, 1f));
            var s = new GUIStyle(_boldStyle) { fontSize = 12 };
            s.normal.textColor = new Color(0.50f, 0.65f, 0.95f, 1f);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width, r.height), label, s);
        }

        // ── Base Cost Section ─────────────────────────────────────────────
        void DrawBaseCostSection(bool isTransparent)
        {
            // Header
            Rect hdr = GUILayoutUtility.GetRect(0, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(hdr, new Color(0.12f, 0.20f, 0.22f, 1f));
            EditorGUI.DrawRect(new Rect(hdr.x, hdr.yMax - 1f, hdr.width, 1f), new Color(0.20f, 0.55f, 0.60f, 0.6f));
            var hs = new GUIStyle(_boldStyle) { fontSize = 12 };
            hs.normal.textColor = new Color(0.30f, 0.80f, 0.85f, 1f);
            EditorGUI.LabelField(new Rect(hdr.x + 8, hdr.y, hdr.width, hdr.height), "Base Cost  (always active)", hs);

            // One row per fixed cost entry
            for (int i = 0; i < k_FixedCosts.Length; i++)
            {
                var  fc        = k_FixedCosts[i];
                int  rowScore  = fc.aluOps + fc.texSamples * 10;
                bool lastRow   = i == k_FixedCosts.Length - 1;

                Rect row = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
                float x = row.x; float w = row.width;

                Color rowBg = i % 2 == 0
                    ? new Color(0.14f, 0.18f, 0.19f, 1f)
                    : new Color(0.12f, 0.16f, 0.17f, 1f);
                EditorGUI.DrawRect(row, rowBg);
                EditorGUI.DrawRect(new Rect(x, row.y, 2f, row.height), new Color(0.30f, 0.80f, 0.85f, 0.5f));
                if (!lastRow)
                    EditorGUI.DrawRect(new Rect(x, row.yMax - 1f, w, 1f), new Color(0.20f, 0.28f, 0.30f, 0.5f));

                // LOCK badge
                Rect badge = new Rect(x + 4, row.y + 4, 42, row.height - 8);
                EditorGUI.DrawRect(badge, new Color(0.14f, 0.24f, 0.26f, 1f));
                var badgeS = new GUIStyle(_subtleStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                badgeS.normal.textColor = new Color(0.30f, 0.75f, 0.80f);
                EditorGUI.LabelField(badge, "LOCK", badgeS);

                var nameS = new GUIStyle(_boldStyle) { fontSize = 13 };
                nameS.normal.textColor = new Color(0.60f, 0.80f, 0.82f);
                EditorGUI.LabelField(new Rect(x + 66, row.y, w * 0.30f, row.height), fc.name, nameS);

                var numS = new GUIStyle(_boldStyle) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                numS.normal.textColor = new Color(0.42f, 0.70f, 0.72f);
                var dashS = new GUIStyle(_subtleStyle) { fontSize = 13, alignment = TextAnchor.MiddleCenter };

                EditorGUI.LabelField(new Rect(x + w * 0.51f, row.y, w * 0.19f, row.height), fc.aluOps.ToString(), numS);
                EditorGUI.LabelField(new Rect(x + w * 0.71f, row.y, w * 0.29f, row.height), fc.texSamples.ToString(), numS);


            }
        }

        // ── Feature Summary ───────────────────────────────────────────────
        void DrawFeatureSummary(MatAnalysis a)
        {
            int baseScore  = BaseScore();
            int featScore  = a.score;   // already includes transparent +10 and texture penalties
            int totalScore = baseScore + featScore;

            int totalAlu = k_FixedCosts.Sum(f => f.aluOps)
                         + a.features.Where(f => f.active).Sum(f => f.def.aluOps);
            int totalTex = k_FixedCosts.Sum(f => f.texSamples)
                         + a.features.Where(f => f.active).Sum(f => f.def.texSamples);

            Rect r = GUILayoutUtility.GetRect(0, 48f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.16f, 1f));
            _featureSummaryRect = r;

            string modeLbl = k_PlatformModeLabels[(int)_platformMode];
            float  cy      = r.y + r.height * 0.5f;

            // Platform label (leftmost, subtle)
            const float k_PlatW = 50f;
            var platS = new GUIStyle(_subtleStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(r.x + 6, cy - 10f, k_PlatW, 20f), modeLbl, platS);

            // Title + subtitle (middle)
            float contentX = r.x + 6 + k_PlatW + 4f;
            var labelS = new GUIStyle(_boldStyle) { fontSize = 13 };
            EditorGUI.LabelField(new Rect(contentX, r.y + 2, r.width * 0.55f, 20f), "Shader Cost", labelS);

            var subS = new GUIStyle(_subtleStyle) { fontSize = 11 };
            EditorGUI.LabelField(new Rect(contentX, r.y + 22, r.width * 0.55f, 18f),
                $"{a.mat.name}  ·  Base {baseScore}  +  Features {featScore}", subS);

            // Column totals aligned with table above
            var aluTotS = new GUIStyle(_boldStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            aluTotS.normal.textColor = TierColor(totalAlu, _platformMode);
            EditorGUI.LabelField(new Rect(r.x + r.width * 0.51f, r.y, r.width * 0.19f, r.height), totalAlu.ToString(), aluTotS);

            var texTotS = new GUIStyle(_boldStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            texTotS.normal.textColor = ScoreColor(totalTex * 10);
            EditorGUI.LabelField(new Rect(r.x + r.width * 0.71f, r.y, r.width * 0.29f, r.height), totalTex.ToString(), texTotS);
        }

        // ── Bottom Bar ────────────────────────────────────────────────────
        void DrawBottomBar(MatAnalysis a)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.40f));
            DrawFeatureSummary(a);
            EditorGUILayout.EndVertical();

            Rect div = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(div, new Color(0.28f, 0.28f, 0.34f, 1f));

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawVRAMSummary(a);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // ── VRAM Summary ──────────────────────────────────────────────────
        void DrawVRAMSummary(MatAnalysis a)
        {
            int texCount = a.baseTextures.Count(t => t.tex != null)
                         + a.features.Sum(f => f.textures.Count(t => t.tex != null));

            Rect r = GUILayoutUtility.GetRect(0, 48f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.16f, 1f));
            _vramSummaryRect = r;

            long   platVRAM = a.TotalVRAMFor(_platformMode);
            string modeLbl  = k_PlatformModeLabels[(int)_platformMode];
            float  cy       = r.y + r.height * 0.5f;

            // Platform label (leftmost, subtle)
            const float k_PlatW = 50f;
            var platS = new GUIStyle(_subtleStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(r.x + 6, cy - 10f, k_PlatW, 20f), modeLbl, platS);

            // Title + subtitle (middle)
            float contentX = r.x + 6 + k_PlatW + 4f;
            var labelS = new GUIStyle(_boldStyle) { fontSize = 13 };
            EditorGUI.LabelField(new Rect(contentX, r.y + 4, r.width * 0.55f, 20f), "Texture VRAM", labelS);

            var subS = new GUIStyle(_subtleStyle) { fontSize = 11 };
            EditorGUI.LabelField(new Rect(contentX, r.y + 26, r.width * 0.55f, 18f),
                $"{a.mat.name}  ·  {texCount} textures", subS);

            // Value only (rightmost, VRAM color)
            float right  = r.x + r.width - 8f;
            var numS = new GUIStyle(_boldStyle) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            numS.normal.textColor = VRAMColor(platVRAM, _platformMode);
            string valText = FmtBytes(platVRAM);
            float  valW    = numS.CalcSize(new GUIContent(valText)).x;
            EditorGUI.LabelField(new Rect(right - valW, cy - 10f, valW, 20f), valText, numS);

            // Optimize Texture All button — left of VRAM value, vertically centered
            {
                bool hasIssues = CachedOptTex.Any(t =>
                    NeedsOptimization(t, PlatformMode.PC) ||
                    NeedsOptimization(t, PlatformMode.Android) ||
                    NeedsOptimization(t, PlatformMode.iOS));
                const float btnW = 168f, btnH = 22f;
                Rect btnR = new Rect(right - valW - 14f - btnW, cy - btnH * 0.5f, btnW, btnH);
                {
                    var btnS = new GUIStyle(EditorStyles.miniButton) { fontSize = 12, fontStyle = FontStyle.Bold };
                    btnS.normal.textColor = hasIssues ? C_WARN : C_OK;
                    string arrow = _showOptimizePreview ? "▼" : "▲";
                    string label = hasIssues ? $"{arrow}  Optimize Texture All" : $"{arrow}  Texture Optimized";
                    if (GUI.Button(btnR, label, btnS))
                    {
                        _showOptimizePreview = !_showOptimizePreview;
                        Repaint();
                    }
                }
            }
        }

        void DrawFeatureListRow(FeatureResult f, Material mat)
        {
            Rect row = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
            float x = row.x; float w = row.width;

            Color bg = f.active
                ? new Color(0.19f, 0.18f, 0.26f, 1f)
                : new Color(0.155f, 0.155f, 0.185f, 1f);
            EditorGUI.DrawRect(row, bg);

            if (f.active)
                EditorGUI.DrawRect(new Rect(x, row.y, 2f, row.height), ScoreColor(FeatureScore(f.def)));

            // STATE badge (clickable toggle)
            Rect badge = new Rect(x + 4, row.y + 4, 42, row.height - 8);
            Color badgeBg = f.active
                ? new Color(0.15f, 0.42f, 0.18f, 1f)
                : new Color(0.20f, 0.20f, 0.24f, 1f);
            EditorGUI.DrawRect(badge, badgeBg);
            var badgeS = new GUIStyle(_boldStyle) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            badgeS.normal.textColor = f.active
                ? new Color(0.55f, 1.0f, 0.60f)
                : new Color(0.38f, 0.38f, 0.44f);

            if (GUI.Button(badge, f.active ? "ON" : "OFF", badgeS))
            {
                if (f.active) mat.DisableKeyword(f.def.keyword);
                else          mat.EnableKeyword(f.def.keyword);
                f.active = mat.IsKeywordEnabled(f.def.keyword);
                EditorUtility.SetDirty(mat);
                foreach (var a in _analyses)
                    _foldoutCache[a.mat.GetInstanceID()] = a.foldout;
                _dirty = true;
            }
            EditorGUIUtility.AddCursorRect(badge, MouseCursor.Link);

            // Feature name
            var nameS = new GUIStyle(f.active ? _boldStyle : _subtleStyle) { fontSize = 14 };
            EditorGUI.LabelField(new Rect(x + 66, row.y, w * 0.30f, row.height), f.def.name, nameS);

            Color activeCol  = ScoreColor(FeatureScore(f.def));
            Color inactiveCol = new Color(0.25f, 0.25f, 0.28f);

            // Shader Cost column
            var aluS = new GUIStyle(_boldStyle) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            aluS.normal.textColor = f.active ? activeCol : inactiveCol;
            EditorGUI.LabelField(new Rect(x + w * 0.51f, row.y, w * 0.19f, row.height),
                f.def.aluOps.ToString(), aluS);

            // Texture Sampling column
            var texS = new GUIStyle(_boldStyle) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            texS.normal.textColor = f.active && f.def.texSamples > 0
                ? new Color(0.42f, 0.72f, 1f)
                : inactiveCol;
            EditorGUI.LabelField(new Rect(x + w * 0.71f, row.y, w * 0.29f, row.height),
                f.def.texSamples > 0 ? f.def.texSamples.ToString() : "—", texS);
        }

        // ─────────────────────────────────────────────────────────────────
        // Texture Table
        // ─────────────────────────────────────────────────────────────────
        void DrawTexTable(List<TexInfo> list, float indent = 0f)
        {
            bool needsRepaint = false;

            for (int i = 0; i < list.Count; i++)
            {
                var  t   = list[i];
                if (t.tex == null) continue;

                Rect row = GUILayoutUtility.GetRect(0, 96f, GUILayout.ExpandWidth(true));

                int  texId    = t.tex.GetInstanceID();
                bool expanded = _expandedTexIds.Contains(texId);

                EditorGUI.DrawRect(row, expanded ? C_ROW_SELECTED : C_ROW_BASE);
                if (expanded)
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 4f, row.height), C_ROW_SEL_ACCENT);

                float rx = row.x + indent + 8f;
                float rw = row.width - indent - 8f;

                const float k_Thumb = 90f;
                Rect thumbRect = new Rect(rx, row.y + 3f, k_Thumb, k_Thumb);

                Texture2D preview = AssetPreview.GetAssetPreview(t.tex);
                if (preview == null)
                {
                    if (t.tex is Texture2D tex2d)
                        EditorGUI.DrawPreviewTexture(thumbRect, tex2d, null, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.DrawRect(thumbRect, new Color(0.18f, 0.18f, 0.22f));

                    if (AssetPreview.IsLoadingAssetPreviews()) needsRepaint = true;
                }
                else
                {
                    GUI.DrawTexture(thumbRect, preview, ScaleMode.ScaleToFit);
                }

                float cx = rx + k_Thumb + 6f;
                float cw = rw - k_Thumb - 6f;

                string chevron = expanded ? "▾ " : "▸ ";
                var nameBtn = new GUIStyle(_labelStyle) { fontSize = 14 };
                if (expanded)
                {
                    nameBtn.fontStyle = FontStyle.Bold;
                    nameBtn.normal.textColor = C_ROW_SEL_ACCENT;
                }
                Rect nameRect = new Rect(cx, row.y + 6f, cw * 0.32f, 22f);
                EditorGUI.LabelField(nameRect, chevron + t.tex.name, nameBtn);
                EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    if (expanded) _expandedTexIds.Remove(texId);
                    else { _expandedTexIds.Add(texId); EditorGUIUtility.PingObject(t.tex); }
                    Event.current.Use();
                    Repaint();
                }

                var propS = new GUIStyle(_subtleStyle) { fontSize = 12 };
                EditorGUI.LabelField(new Rect(cx, row.y + 32f, cw * 0.32f, 18f), t.propName, propS);

                var resS = new GUIStyle(_labelStyle) { fontSize = 14 };
                if (t.isOversized) resS.normal.textColor = C_WARN;
                EditorGUI.LabelField(new Rect(cx + cw * 0.32f, row.y, cw * 0.16f, row.height),
                    $"{t.width}×{t.height}", resS);

                string cLabel; Color cColor;
                switch (t.compress)
                {
                    case CompressLevel.None:   cLabel = "None";   cColor = C_ERR;                       break;
                    case CompressLevel.Low:    cLabel = "Low";    cColor = C_WARN;                      break;
                    case CompressLevel.Medium: cLabel = "Medium"; cColor = C_OK;                        break;
                    default:                   cLabel = "High";   cColor = new Color(0.42f, 0.72f, 1f); break;
                }
                var cmpS = new GUIStyle(_boldStyle) { fontSize = 14 };
                cmpS.normal.textColor = cColor;
                EditorGUI.LabelField(new Rect(cx + cw * 0.48f, row.y, cw * 0.20f, row.height), cLabel, cmpS);

                var vramS = new GUIStyle(_labelStyle) { fontSize = 14 };
                EditorGUI.LabelField(new Rect(cx + cw * 0.68f, row.y, cw * 0.14f, row.height),
                    FmtBytes(t.VRAMFor(_platformMode)), vramS);

                string st; Color sc;
                if      (t.isUncompressed && t.isOversized) { st = "Compress + Resize"; sc = C_ERR;  }
                else if (t.isUncompressed)                   { st = "Need Compress";     sc = C_ERR;  }
                else if (t.isOversized)                      { st = "Oversized";         sc = C_WARN; }
                else                                         { st = "✔ OK";             sc = C_OK;   }
                var stS = new GUIStyle(_labelStyle) { fontSize = 14 };
                stS.normal.textColor = sc;
                EditorGUI.LabelField(new Rect(cx + cw * 0.82f, row.y, cw * 0.18f, row.height), st, stS);

                if (expanded && !string.IsNullOrEmpty(t.assetPath))
                    DrawTexPlatformPanel(t);
            }

            if (needsRepaint) Repaint();
        }

        // ─────────────────────────────────────────────────────────────────
        // Per-texture Platform Import Settings Panel
        // ─────────────────────────────────────────────────────────────────
        void DrawTexPlatformPanel(TexInfo t)
        {
            int texId = t.tex.GetInstanceID();
            if (!_texPlatformTab.ContainsKey(texId)) _texPlatformTab[texId] = 0;

            Rect tabBar = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(tabBar, new Color(0.11f, 0.11f, 0.15f));
            float tabW = tabBar.width / k_PlatformLabels.Length;

            for (int ti = 0; ti < k_PlatformLabels.Length; ti++)
            {
                Rect tr  = new Rect(tabBar.x + ti * tabW, tabBar.y, tabW, tabBar.height);
                bool sel = _texPlatformTab[texId] == ti;

                EditorGUI.DrawRect(tr, sel ? new Color(0.20f, 0.18f, 0.32f) : new Color(0.14f, 0.14f, 0.18f));
                if (sel)
                    EditorGUI.DrawRect(new Rect(tr.x, tr.yMax - 2f, tr.width, 2f), C_ACCENT);
                if (ti > 0)
                    EditorGUI.DrawRect(new Rect(tr.x, tr.y + 4f, 1f, tr.height - 8f),
                        new Color(0.28f, 0.28f, 0.34f));

                var ts = new GUIStyle(_boldStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                if (!sel) ts.normal.textColor = new Color(0.50f, 0.50f, 0.56f);
                EditorGUI.LabelField(tr, k_PlatformLabels[ti], ts);

                if (GUI.Button(tr, GUIContent.none, GUIStyle.none))
                    _texPlatformTab[texId] = ti;
                EditorGUIUtility.AddCursorRect(tr, MouseCursor.Link);
            }

            var imp = AssetImporter.GetAtPath(t.assetPath) as TextureImporter;
            if (imp == null)
            {
                var ns = new GUIStyle(_subtleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
                Rect nr = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(nr, new Color(0.13f, 0.13f, 0.17f));
                EditorGUI.LabelField(nr, "No TextureImporter (runtime/procedural texture)", ns);
                return;
            }

            int tab = _texPlatformTab[texId];
            bool changed;
            if (tab == 0)
                changed = DrawTexSettingsDefault(imp);
            else
                changed = DrawTexSettingsPlatform(imp, k_PlatformNames[tab]);

            if (changed)
            {
                AssetDatabase.ImportAsset(t.assetPath, ImportAssetOptions.ForceUpdate);
                foreach (var a in _analyses) _foldoutCache[a.mat.GetInstanceID()] = a.foldout;
                _dirty = true;
            }

            Rect sep = GUILayoutUtility.GetRect(0, 4f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(sep, new Color(0.10f, 0.10f, 0.13f));
        }

        bool DrawTexSettingsDefault(TextureImporter imp)
        {
            bool changed = false;
            int  row     = 0;

            {
                Rect cr = TexSettingRow("Max Size", row++);
                int v = EditorGUI.IntPopup(cr, imp.maxTextureSize, k_MaxSizeStr, k_MaxSizes);
                if (v != imp.maxTextureSize) { imp.maxTextureSize = v; changed = true; }
            }
            {
                Rect cr = TexSettingRow("Format", row++);
                EditorGUI.LabelField(cr, "Automatic  (set per platform below)", new GUIStyle(_subtleStyle) { fontSize = 11 });
            }
            {
                Rect cr  = TexSettingRow("Compression", row++);
                int  cur = System.Array.IndexOf(k_Compressions, imp.textureCompression);
                if (cur < 0) cur = 2;
                int next = EditorGUI.Popup(cr, cur, k_CompressionStr);
                if (next != cur) { imp.textureCompression = k_Compressions[next]; changed = true; }
            }
            {
                Rect cr = TexSettingRow("Use Crunch Compression", row++);
                bool v  = EditorGUI.Toggle(new Rect(cr.x, cr.y + 2f, 18f, cr.height - 4f),
                    imp.crunchedCompression);
                if (v != imp.crunchedCompression) { imp.crunchedCompression = v; changed = true; }
            }
            if (imp.crunchedCompression)
            {
                Rect cr = TexSettingRow("Compressor Quality", row++);
                int  v  = Mathf.RoundToInt(GUI.HorizontalSlider(
                    new Rect(cr.x, cr.y + 6f, cr.width - 34f, cr.height - 12f),
                    imp.compressionQuality, 0, 100));
                var qs = new GUIStyle(_boldStyle) { fontSize = 12, alignment = TextAnchor.MiddleRight };
                EditorGUI.LabelField(new Rect(cr.x + cr.width - 32f, cr.y, 32f, cr.height), v.ToString(), qs);
                if (v != imp.compressionQuality) { imp.compressionQuality = v; changed = true; }
            }

            return changed;
        }

        bool DrawTexSettingsPlatform(TextureImporter imp, string platform)
        {
            bool changed = false;
            TextureImporterPlatformSettings ps = imp.GetPlatformTextureSettings(platform);

            {
                Rect cr   = TexSettingRow($"Override for {platform}", 0);
                bool next = EditorGUI.Toggle(new Rect(cr.x, cr.y + 2f, 18f, cr.height - 4f), ps.overridden);
                if (next != ps.overridden) { ps.overridden = next; imp.SetPlatformTextureSettings(ps); changed = true; }
            }

            using (new EditorGUI.DisabledScope(!ps.overridden))
            {
                {
                    Rect cr = TexSettingRow("Max Size", 1);
                    int  v  = EditorGUI.IntPopup(cr, ps.maxTextureSize, k_MaxSizeStr, k_MaxSizes);
                    if (v != ps.maxTextureSize) { ps.maxTextureSize = v; imp.SetPlatformTextureSettings(ps); changed = true; }
                }
                {
                    Rect cr  = TexSettingRow("Resize Algorithm", 2);
                    var  opts = new[] { "Mitchell", "Bilinear" };
                    int  cur  = ps.resizeAlgorithm == TextureResizeAlgorithm.Bilinear ? 1 : 0;
                    int  next = EditorGUI.Popup(cr, cur, opts);
                    if (next != cur)
                    {
                        ps.resizeAlgorithm = next == 1 ? TextureResizeAlgorithm.Bilinear : TextureResizeAlgorithm.Mitchell;
                        imp.SetPlatformTextureSettings(ps);
                        changed = true;
                    }
                }
                {
                    Rect     cr     = TexSettingRow("Format", 3);
                    string[] fmtStr = PlatformFormatLabels(platform, out TextureImporterFormat[] fmtVals);
                    int      cur    = System.Array.IndexOf(fmtVals, ps.format);
                    if (cur < 0) cur = 0;
                    int next = EditorGUI.Popup(cr, cur, fmtStr);
                    if (next != cur) { ps.format = fmtVals[next]; imp.SetPlatformTextureSettings(ps); changed = true; }
                }
                {
                    Rect cr  = TexSettingRow("Compression", 4);
                    int  cur = System.Array.IndexOf(k_Compressions, ps.textureCompression);
                    if (cur < 0) cur = 2;
                    int next = EditorGUI.Popup(cr, cur, k_CompressionStr);
                    if (next != cur) { ps.textureCompression = k_Compressions[next]; imp.SetPlatformTextureSettings(ps); changed = true; }
                }
                {
                    Rect cr = TexSettingRow("Use Crunch Compression", 5);
                    bool v  = EditorGUI.Toggle(new Rect(cr.x, cr.y + 2f, 18f, cr.height - 4f), ps.crunchedCompression);
                    if (v != ps.crunchedCompression) { ps.crunchedCompression = v; imp.SetPlatformTextureSettings(ps); changed = true; }
                }
                if (ps.crunchedCompression)
                {
                    Rect cr = TexSettingRow("Compressor Quality", 6);
                    int  v  = Mathf.RoundToInt(GUI.HorizontalSlider(
                        new Rect(cr.x, cr.y + 6f, cr.width - 34f, cr.height - 12f),
                        ps.compressionQuality, 0, 100));
                    var qs = new GUIStyle(_boldStyle) { fontSize = 12, alignment = TextAnchor.MiddleRight };
                    EditorGUI.LabelField(new Rect(cr.x + cr.width - 32f, cr.y, 32f, cr.height), v.ToString(), qs);
                    if (v != ps.compressionQuality) { ps.compressionQuality = v; imp.SetPlatformTextureSettings(ps); changed = true; }
                }
            }

            return changed;
        }

        Rect TexSettingRow(string label, int rowIdx)
        {
            Rect r = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, rowIdx % 2 == 0
                ? new Color(0.15f, 0.14f, 0.19f)
                : new Color(0.13f, 0.12f, 0.17f));
            var ls = new GUIStyle(_subtleStyle) { fontSize = 12 };
            EditorGUI.LabelField(new Rect(r.x + 12f, r.y, r.width * 0.42f, r.height), label, ls);
            return new Rect(r.x + r.width * 0.42f, r.y + 3f, r.width * 0.55f, r.height - 6f);
        }

        static string[] PlatformFormatLabels(string platform, out TextureImporterFormat[] formats)
        {
            switch (platform)
            {
                case "Android":
                    formats = new[] {
                        TextureImporterFormat.Automatic,
                        TextureImporterFormat.ETC_RGB4,
                        TextureImporterFormat.ETC2_RGB4,
                        TextureImporterFormat.ETC2_RGBA8,
                        TextureImporterFormat.ASTC_4x4,
                        TextureImporterFormat.ASTC_6x6,
                        TextureImporterFormat.ASTC_8x8,
                        TextureImporterFormat.RGBA32,
                    };
                    return new[] { "Automatic", "ETC RGB 4", "ETC2 RGB 4", "ETC2 RGBA 8",
                                   "ASTC 4×4", "ASTC 6×6", "ASTC 8×8", "RGBA 32" };
                case "iPhone":
                    formats = new[] {
                        TextureImporterFormat.Automatic,
                        TextureImporterFormat.ASTC_4x4,
                        TextureImporterFormat.ASTC_6x6,
                        TextureImporterFormat.ASTC_8x8,
                        TextureImporterFormat.RGBA32,
                    };
                    return new[] { "Automatic", "ASTC 4×4", "ASTC 6×6", "ASTC 8×8", "RGBA 32" };
                default:
                    formats = new[] {
                        TextureImporterFormat.Automatic,
                        TextureImporterFormat.DXT1,
                        TextureImporterFormat.DXT5,
                        TextureImporterFormat.BC4,
                        TextureImporterFormat.BC5,
                        TextureImporterFormat.BC6H,
                        TextureImporterFormat.BC7,
                        TextureImporterFormat.RGBA32,
                    };
                    return new[] { "Automatic", "DXT1 (RGB)", "DXT5 (RGBA)",
                                   "BC4", "BC5", "BC6H (HDR)", "BC7", "RGBA 32" };
            }
        }

        void ColHdr(float y, float h, float x, float w, string label, float offPct, float widPct, int fontSize = 13)
        {
            var s = new GUIStyle(_subtleStyle) { fontSize = fontSize };
            EditorGUI.LabelField(new Rect(x + w * offPct, y, w * widPct, h), label, s);
        }

        // ═════════════════════════════════════════════════════════════════
        // Analysis
        // ═════════════════════════════════════════════════════════════════
        void Analyze()
        {
            _dirty = false;

            foreach (var old in _analyses)
                _foldoutCache[old.mat.GetInstanceID()] = old.foldout;

            _analyses  = new List<MatAnalysis>();

            var seen = new HashSet<int>();
            foreach (var r in _target.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;

                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (!seen.Add(mat.GetInstanceID())) continue;
                    var a = AnalyzeMat(mat);
                    if (_foldoutCache.TryGetValue(mat.GetInstanceID(), out bool fold))
                        a.foldout = fold;
                    _analyses.Add(a);
                }
            }

            _selectedIndex = _analyses.FindIndex(a => a.mat.GetInstanceID() == _selectedMatId);
            _cachedOptTex  = null;
        }

        MatAnalysis AnalyzeMat(Material mat)
        {
            var a = new MatAnalysis
            {
                mat           = mat,
                isTransparent = mat.renderQueue >= 3000,
                isZLZ         = mat.shader != null && mat.shader.name.StartsWith("ZLZ/"),
            };

            var featureTexProps = new HashSet<string>();

            if (a.isZLZ)
            {
                foreach (var def in k_Features)
                {
                    bool active = mat.IsKeywordEnabled(def.keyword);
                    var res = new FeatureResult { def = def, active = active };

                    if (active)
                    {
                        foreach (var p in def.texProps)
                        {
                            featureTexProps.Add(p);
                            res.textures.Add(SampleTex(mat, p));
                        }
                        a.score += FeatureScore(def);
                    }

                    a.features.Add(res);
                }
            }

            // Transparent overdraw: tile-based renderers re-shade every covered pixel
            if (a.isTransparent) a.score += 10;

            foreach (var p in new[] { "_MainTex", "_RGBA_Masking" })
            {
                if (featureTexProps.Contains(p)) continue;
                a.baseTextures.Add(SampleTex(mat, p));
            }

            // Texture bandwidth penalties
            foreach (var t in AllTex(a))
            {
                if (t.isUncompressed) a.score += 4;  // raw bandwidth hit
                if (t.isOversized)    a.score += 2;  // excess memory fetch
            }

            a.totalVRAM = a.baseTextures.Sum(t => t.vramBytes)
                        + a.features.Sum(f => f.textures.Sum(t => t.vramBytes));
            return a;
        }

        TexInfo SampleTex(Material mat, string prop)
        {
            var info = new TexInfo { propName = prop };
            if (!mat.HasProperty(prop)) return info;

            var tex = mat.GetTexture(prop);
            info.tex = tex;
            if (tex == null) return info;

            info.width     = tex.width;
            info.height    = tex.height;
            info.vramBytes = Profiler.GetRuntimeMemorySizeLong(tex);

            if (tex is Texture2D t2d)
            {
                info.format         = t2d.format;
                info.isUncompressed = IsUncompressed(t2d.format);
                info.compress       = GetCompressLevel(t2d.format);
            }

            info.isOversized = tex.width > 2048 || tex.height > 2048;
            info.assetPath   = AssetDatabase.GetAssetPath(tex);
            var importer     = AssetImporter.GetAtPath(info.assetPath) as TextureImporter;
            if (importer != null)
            {
                info.maxSize    = importer.maxTextureSize;
                bool hasMips    = importer.mipmapEnabled;
                info.vramPC     = CalcPlatformVRAM(importer, "Standalone", info.width, info.height, hasMips, info.vramBytes);
                info.vramAndroid = CalcPlatformVRAM(importer, "Android",   info.width, info.height, hasMips, info.vramBytes);
                info.vramIOS    = CalcPlatformVRAM(importer, "iPhone",     info.width, info.height, hasMips, info.vramBytes);
            }
            return info;
        }

        static long CalcPlatformVRAM(TextureImporter imp, string platform,
                                     int origW, int origH, bool hasMips, long fallback)
        {
            TextureImporterPlatformSettings ps = imp.GetPlatformTextureSettings(platform);

            // No override → Unity builds with Default settings on this platform, same as editor runtime
            if (!ps.overridden) return fallback;

            int  maxSize = ps.maxTextureSize;
            int  w       = Mathf.Min(origW, maxSize);
            int  h       = Mathf.Min(origH, maxSize);

            TextureImporterFormat fmt = ps.format;
            if (fmt == TextureImporterFormat.Automatic)
                fmt = DefaultPlatformFormat(platform);

            float bpp   = ImporterFormatBPP(fmt);
            long  bytes = (long)(w * h * bpp / 8f);
            if (hasMips) bytes = bytes * 4 / 3;
            return bytes > 0 ? bytes : fallback;
        }

        TextureImporterFormat RecommendedFormat(TexInfo t, PlatformMode m)
        {
            bool isNormal = t.propName.IndexOf("normal", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSmall  = t.width <= 256 && t.height <= 256;

            if (m == PlatformMode.Android || m == PlatformMode.iOS)
            {
                if (isNormal) return TextureImporterFormat.ASTC_4x4;
                switch (_optQuality)
                {
                    case 0: return TextureImporterFormat.ASTC_8x8;
                    case 2: return TextureImporterFormat.ASTC_4x4;
                    default: return isSmall ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
                }
            }
            else
            {
                if (isNormal) return TextureImporterFormat.BC5;
                switch (_optQuality)
                {
                    case 0: return isSmall ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
                    case 2: return TextureImporterFormat.BC7;
                    default: return isSmall ? TextureImporterFormat.DXT1 : TextureImporterFormat.BC7;
                }
            }
        }

        long SimulateOptimizedVRAM(TexInfo t, PlatformMode m)
        {
            if (t.tex == null || string.IsNullOrEmpty(t.assetPath)) return 0;
            var imp = AssetImporter.GetAtPath(t.assetPath) as TextureImporter;
            if (imp == null) return t.VRAMFor(m);

            int   slotCap = GetSlotMaxSize(t.propName, k_PlatformModeNames[(int)m]);
            int   cap     = t.maxSize > 0 ? Mathf.Min(t.maxSize, slotCap) : slotCap;
            int   w       = Mathf.Min(t.width,  cap);
            int   h       = Mathf.Min(t.height, cap);
            float bpp     = ImporterFormatBPP(RecommendedFormat(t, m));
            long  bytes   = (long)(w * h * bpp / 8f);
            if (imp.mipmapEnabled) bytes = bytes * 4 / 3;
            return bytes > 0 ? bytes : t.VRAMFor(m);
        }

        bool NeedsOptimization(TexInfo t, PlatformMode m)
        {
            if (t.tex == null || string.IsNullOrEmpty(t.assetPath)) return false;
            return SimulateOptimizedVRAM(t, m) < t.VRAMFor(m);
        }

        List<TexInfo> CachedOptTex
        {
            get
            {
                if (_cachedOptTex == null) _cachedOptTex = GatherAllCharacterTextures();
                return _cachedOptTex;
            }
        }

        List<TexInfo> GatherAllCharacterTextures()
        {
            var seen = new Dictionary<string, TexInfo>();
            foreach (var a in _analyses)
            {
                foreach (var t in AllTex(a))
                    if (t.tex != null && !string.IsNullOrEmpty(t.assetPath) && !seen.ContainsKey(t.assetPath))
                        seen[t.assetPath] = t;

                foreach (var def in k_Features)
                    foreach (var prop in def.texProps)
                    {
                        if (!a.mat.HasProperty(prop)) continue;
                        var tex = a.mat.GetTexture(prop);
                        if (tex == null) continue;
                        string path = AssetDatabase.GetAssetPath(tex);
                        if (string.IsNullOrEmpty(path) || seen.ContainsKey(path)) continue;
                        seen[path] = SampleTex(a.mat, prop);
                    }
            }
            return seen.Values.Where(t => t.tex != null).ToList();
        }

        float CalcOptimizePreviewH()
        {
            if (!_showOptimizePreview) return 0f;
            int numSlots = CachedOptTex.GroupBy(t => t.propName).Count();
            int numPlat  = (_optPC ? 1 : 0) + (_optAndroid ? 1 : 0) + (_optIOS ? 1 : 0);
            // header(38) + platGrpHdr(24) + subColHdr(20) + rows + divider(1) + sumRow(34) + footer
            return 38f + 24f + 20f + numSlots * 26f + 1f + 34f
                 + (numPlat > 1 ? 1f + 38f : 0f);
        }

        static TextureImporterFormat DefaultPlatformFormat(string platform)
        {
            switch (platform)
            {
                case "Standalone": return TextureImporterFormat.DXT5;
                case "Android":    return TextureImporterFormat.ASTC_6x6;
                case "iPhone":     return TextureImporterFormat.ASTC_4x4;
                default:           return TextureImporterFormat.DXT5;
            }
        }

        static float ImporterFormatBPP(TextureImporterFormat fmt)
        {
            switch (fmt)
            {
                case TextureImporterFormat.RGBA32:
                case TextureImporterFormat.ARGB32:              return 32f;
                case TextureImporterFormat.RGB24:               return 24f;
                case TextureImporterFormat.Alpha8:              return 8f;
                case TextureImporterFormat.RGBAHalf:            return 64f;
                case TextureImporterFormat.RGBAFloat:           return 128f;
                case TextureImporterFormat.RFloat:              return 32f;
                case TextureImporterFormat.DXT1:
                case TextureImporterFormat.DXT1Crunched:        return 4f;
                case TextureImporterFormat.DXT5:
                case TextureImporterFormat.DXT5Crunched:        return 8f;
                case TextureImporterFormat.BC4:                 return 4f;
                case TextureImporterFormat.BC5:                 return 8f;
                case TextureImporterFormat.BC6H:
                case TextureImporterFormat.BC7:                 return 8f;
                case TextureImporterFormat.ASTC_4x4:
                case TextureImporterFormat.ASTC_HDR_4x4:       return 8f;
                case TextureImporterFormat.ASTC_5x5:
                case TextureImporterFormat.ASTC_HDR_5x5:       return 5.12f;
                case TextureImporterFormat.ASTC_6x6:
                case TextureImporterFormat.ASTC_HDR_6x6:       return 3.56f;
                case TextureImporterFormat.ASTC_8x8:
                case TextureImporterFormat.ASTC_HDR_8x8:       return 2f;
                case TextureImporterFormat.ASTC_10x10:
                case TextureImporterFormat.ASTC_HDR_10x10:     return 1.28f;
                case TextureImporterFormat.ASTC_12x12:
                case TextureImporterFormat.ASTC_HDR_12x12:     return 0.89f;
                case TextureImporterFormat.ETC_RGB4:
                case TextureImporterFormat.ETC_RGB4Crunched:
                case TextureImporterFormat.ETC2_RGB4:          return 4f;
                case TextureImporterFormat.ETC2_RGBA8:
                case TextureImporterFormat.ETC2_RGBA8Crunched: return 8f;
#pragma warning disable CS0618
                case TextureImporterFormat.PVRTC_RGB2:
                case TextureImporterFormat.PVRTC_RGBA2:        return 2f;
                case TextureImporterFormat.PVRTC_RGB4:
                case TextureImporterFormat.PVRTC_RGBA4:        return 4f;
#pragma warning restore CS0618
                default:                                        return 32f;
            }
        }

        static bool IsUncompressed(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.RGBA64:
                case TextureFormat.RGBAFloat:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RHalf:
                case TextureFormat.ARGB32:
                    return true;
                default:
                    return false;
            }
        }

        static CompressLevel GetCompressLevel(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.RGBA64:
                case TextureFormat.RGBAFloat:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RHalf:
                case TextureFormat.ARGB32:
                    return CompressLevel.None;

                case TextureFormat.DXT1:
                case TextureFormat.ETC_RGB4:
#pragma warning disable CS0618
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGB4:
#pragma warning restore CS0618
                case TextureFormat.ETC2_RGB:
                    return CompressLevel.Low;

                case TextureFormat.DXT5:
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                case TextureFormat.ETC2_RGBA8:
#pragma warning disable CS0618
                case TextureFormat.PVRTC_RGBA4:
#pragma warning restore CS0618
                case TextureFormat.ETC2_RGBA1:
                    return CompressLevel.Medium;

                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                    return CompressLevel.High;

                default:
                    return f.ToString().StartsWith("ASTC") ? CompressLevel.High : CompressLevel.Medium;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Rating & Color Helpers
        // ═════════════════════════════════════════════════════════════════

        static IEnumerable<TexInfo> AllTex(MatAnalysis a)
            => a.baseTextures.Concat(a.features.SelectMany(f => f.textures));

        void LoadThresholds()
        {
            _thPC_MatL   = EditorPrefs.GetInt("ZLZ_Th_PC_ML",    k_D_PC_MatL);
            _thPC_MatH   = EditorPrefs.GetInt("ZLZ_Th_PC_MH",    k_D_PC_MatH);
            _thAnd_MatL  = EditorPrefs.GetInt("ZLZ_Th_And_ML",   k_D_And_MatL);
            _thAnd_MatH  = EditorPrefs.GetInt("ZLZ_Th_And_MH",   k_D_And_MatH);
            _thIOS_MatL  = EditorPrefs.GetInt("ZLZ_Th_iOS_ML",   k_D_iOS_MatL);
            _thIOS_MatH  = EditorPrefs.GetInt("ZLZ_Th_iOS_MH",   k_D_iOS_MatH);
            _thPC_TotL   = EditorPrefs.GetInt("ZLZ_Th_PC_TL",    k_D_PC_TotL);
            _thPC_TotH   = EditorPrefs.GetInt("ZLZ_Th_PC_TH",    k_D_PC_TotH);
            _thAnd_TotL  = EditorPrefs.GetInt("ZLZ_Th_And_TL",   k_D_And_TotL);
            _thAnd_TotH  = EditorPrefs.GetInt("ZLZ_Th_And_TH",   k_D_And_TotH);
            _thIOS_TotL  = EditorPrefs.GetInt("ZLZ_Th_iOS_TL",   k_D_iOS_TotL);
            _thIOS_TotH  = EditorPrefs.GetInt("ZLZ_Th_iOS_TH",   k_D_iOS_TotH);
            _thPC_VramL     = EditorPrefs.GetInt("ZLZ_Th_PC_VL",    k_D_PC_VramL);
            _thPC_VramH     = EditorPrefs.GetInt("ZLZ_Th_PC_VH",    k_D_PC_VramH);
            _thAnd_VramL    = EditorPrefs.GetInt("ZLZ_Th_And_VL",   k_D_And_VramL);
            _thAnd_VramH    = EditorPrefs.GetInt("ZLZ_Th_And_VH",   k_D_And_VramH);
            _thIOS_VramL    = EditorPrefs.GetInt("ZLZ_Th_iOS_VL",   k_D_iOS_VramL);
            _thIOS_VramH    = EditorPrefs.GetInt("ZLZ_Th_iOS_VH",   k_D_iOS_VramH);
            _thPC_VramTotL  = EditorPrefs.GetInt("ZLZ_Th_PC_VTL",   k_D_PC_VramTotL);
            _thPC_VramTotH  = EditorPrefs.GetInt("ZLZ_Th_PC_VTH",   k_D_PC_VramTotH);
            _thAnd_VramTotL = EditorPrefs.GetInt("ZLZ_Th_And_VTL",  k_D_And_VramTotL);
            _thAnd_VramTotH = EditorPrefs.GetInt("ZLZ_Th_And_VTH",  k_D_And_VramTotH);
            _thIOS_VramTotL = EditorPrefs.GetInt("ZLZ_Th_iOS_VTL",  k_D_iOS_VramTotL);
            _thIOS_VramTotH = EditorPrefs.GetInt("ZLZ_Th_iOS_VTH",  k_D_iOS_VramTotH);
        }

        void SaveThresholds()
        {
            EditorPrefs.SetInt("ZLZ_Th_PC_ML",   _thPC_MatL);
            EditorPrefs.SetInt("ZLZ_Th_PC_MH",   _thPC_MatH);
            EditorPrefs.SetInt("ZLZ_Th_And_ML",  _thAnd_MatL);
            EditorPrefs.SetInt("ZLZ_Th_And_MH",  _thAnd_MatH);
            EditorPrefs.SetInt("ZLZ_Th_iOS_ML",  _thIOS_MatL);
            EditorPrefs.SetInt("ZLZ_Th_iOS_MH",  _thIOS_MatH);
            EditorPrefs.SetInt("ZLZ_Th_PC_TL",   _thPC_TotL);
            EditorPrefs.SetInt("ZLZ_Th_PC_TH",   _thPC_TotH);
            EditorPrefs.SetInt("ZLZ_Th_And_TL",  _thAnd_TotL);
            EditorPrefs.SetInt("ZLZ_Th_And_TH",  _thAnd_TotH);
            EditorPrefs.SetInt("ZLZ_Th_iOS_TL",  _thIOS_TotL);
            EditorPrefs.SetInt("ZLZ_Th_iOS_TH",  _thIOS_TotH);
            EditorPrefs.SetInt("ZLZ_Th_PC_VL",   _thPC_VramL);
            EditorPrefs.SetInt("ZLZ_Th_PC_VH",   _thPC_VramH);
            EditorPrefs.SetInt("ZLZ_Th_And_VL",  _thAnd_VramL);
            EditorPrefs.SetInt("ZLZ_Th_And_VH",  _thAnd_VramH);
            EditorPrefs.SetInt("ZLZ_Th_iOS_VL",  _thIOS_VramL);
            EditorPrefs.SetInt("ZLZ_Th_iOS_VH",  _thIOS_VramH);
            EditorPrefs.SetInt("ZLZ_Th_PC_VTL",  _thPC_VramTotL);
            EditorPrefs.SetInt("ZLZ_Th_PC_VTH",  _thPC_VramTotH);
            EditorPrefs.SetInt("ZLZ_Th_And_VTL", _thAnd_VramTotL);
            EditorPrefs.SetInt("ZLZ_Th_And_VTH", _thAnd_VramTotH);
            EditorPrefs.SetInt("ZLZ_Th_iOS_VTL", _thIOS_VramTotL);
            EditorPrefs.SetInt("ZLZ_Th_iOS_VTH", _thIOS_VramTotH);
        }

        void ResetThresholds()
        {
            _thPC_MatL   = k_D_PC_MatL;   _thPC_MatH   = k_D_PC_MatH;
            _thAnd_MatL  = k_D_And_MatL;  _thAnd_MatH  = k_D_And_MatH;
            _thIOS_MatL  = k_D_iOS_MatL;  _thIOS_MatH  = k_D_iOS_MatH;
            _thPC_TotL   = k_D_PC_TotL;   _thPC_TotH   = k_D_PC_TotH;
            _thAnd_TotL  = k_D_And_TotL;  _thAnd_TotH  = k_D_And_TotH;
            _thIOS_TotL  = k_D_iOS_TotL;  _thIOS_TotH  = k_D_iOS_TotH;
            _thPC_VramL     = k_D_PC_VramL;     _thPC_VramH     = k_D_PC_VramH;
            _thAnd_VramL    = k_D_And_VramL;    _thAnd_VramH    = k_D_And_VramH;
            _thIOS_VramL    = k_D_iOS_VramL;    _thIOS_VramH    = k_D_iOS_VramH;
            _thPC_VramTotL  = k_D_PC_VramTotL;  _thPC_VramTotH  = k_D_PC_VramTotH;
            _thAnd_VramTotL = k_D_And_VramTotL; _thAnd_VramTotH = k_D_And_VramTotH;
            _thIOS_VramTotL = k_D_iOS_VramTotL; _thIOS_VramTotH = k_D_iOS_VramTotH;
        }

        // ALU total = same number shown in the ALU column of the detail panel
        static int MatAluTotal(MatAnalysis a)
            => k_FixedCosts.Sum(f => f.aluOps)
             + a.features.Where(f => f.active).Sum(f => f.def.aluOps);

        int TotalScore() => _analyses.Sum(a => MatAluTotal(a));

        string TierStr(int s, PlatformMode p)
        {
            int light = p == PlatformMode.PC ? _thPC_MatL
                      : p == PlatformMode.Android ? _thAnd_MatL : _thIOS_MatL;
            int heavy = p == PlatformMode.PC ? _thPC_MatH
                      : p == PlatformMode.Android ? _thAnd_MatH : _thIOS_MatH;
            return s <= light ? "✔ Light" : s <= heavy ? "⚠ Medium" : "🔴 Heavy";
        }

        string TotalTierStr(int s, PlatformMode p)
        {
            int ok    = p == PlatformMode.PC ? _thPC_TotL
                      : p == PlatformMode.Android ? _thAnd_TotL : _thIOS_TotL;
            int heavy = p == PlatformMode.PC ? _thPC_TotH
                      : p == PlatformMode.Android ? _thAnd_TotH : _thIOS_TotH;
            return s <= ok ? "✔ Light" : s <= heavy ? "⚠ Watch" : "🔴 High";
        }

        Color TierColor(int s, PlatformMode p)
        {
            string t = TierStr(s, p);
            return t.Contains("Light") ? C_OK : t.Contains("Med") ? C_WARN : C_ERR;
        }

        Color TotalTierColor(int s, PlatformMode p)
        {
            string t = TotalTierStr(s, p);
            return t.Contains("Light") ? C_OK : t.Contains("Wat") ? C_WARN : C_ERR;
        }

        // Color by feature score magnitude (for accent bars and group labels)
        static Color ScoreColor(int score)
        {
            if (score < 30) return new Color(0.22f, 0.75f, 0.45f);
            if (score < 80) return new Color(0.95f, 0.65f, 0.10f);
            return new Color(0.90f, 0.25f, 0.25f);
        }

        string VRAMTierStr(long bytes, PlatformMode p)
        {
            float mb    = bytes / (1024f * 1024f);
            int   light = p == PlatformMode.PC ? _thPC_VramL : p == PlatformMode.Android ? _thAnd_VramL : _thIOS_VramL;
            int   heavy = p == PlatformMode.PC ? _thPC_VramH : p == PlatformMode.Android ? _thAnd_VramH : _thIOS_VramH;
            return mb <= light ? "✔ Light" : mb <= heavy ? "⚠ Medium" : "🔴 Heavy";
        }

        string VRAMTotalTierStr(long bytes, PlatformMode p)
        {
            float mb    = bytes / (1024f * 1024f);
            int   light = p == PlatformMode.PC ? _thPC_VramTotL : p == PlatformMode.Android ? _thAnd_VramTotL : _thIOS_VramTotL;
            int   heavy = p == PlatformMode.PC ? _thPC_VramTotH : p == PlatformMode.Android ? _thAnd_VramTotH : _thIOS_VramTotH;
            return mb <= light ? "✔ Light" : mb <= heavy ? "⚠ Medium" : "🔴 Heavy";
        }

        Color VRAMColor(long bytes, PlatformMode p)
        {
            string t = VRAMTierStr(bytes, p);
            return t.Contains("Light") ? C_OK : t.Contains("Med") ? C_WARN : C_ERR;
        }

        Color VRAMTotalColor(long bytes, PlatformMode p)
        {
            string t = VRAMTotalTierStr(bytes, p);
            return t.Contains("Light") ? C_OK : t.Contains("Med") ? C_WARN : C_ERR;
        }

        static string FmtBytes(long b)
        {
            if (b < 0)           return "—";
            if (b == 0)          return "0 B";
            if (b < 1024)        return $"{b} B";
            if (b < 1024 * 1024) return $"{b / 1024f:F1} KB";
            return $"{b / (1024f * 1024f):F1} MB";
        }

        // ═════════════════════════════════════════════════════════════════
        // Styles
        // ═════════════════════════════════════════════════════════════════
        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _titleStyle.normal.textColor = C_ACCENT;

            _boldStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            _boldStyle.normal.textColor = new Color(0.85f, 0.85f, 0.90f);

            _labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            _labelStyle.normal.textColor = new Color(0.70f, 0.70f, 0.75f);

            _subtleStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            _subtleStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f);

            _infoStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true };
            _infoStyle.normal.textColor = new Color(0.65f, 0.65f, 0.70f);
        }
    }
}
#endif
