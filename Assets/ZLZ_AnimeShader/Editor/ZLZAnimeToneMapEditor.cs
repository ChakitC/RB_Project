// Assets/ZLZ/Editor/ZLZAnimeToneMapEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using ZLZ.AnimeShader;

namespace ZLZ.AnimeShader.Editor
{
    [CustomEditor(typeof(ZLZ_AnimeToneMap))]
    public class ZLZAnimeToneMapEditor : VolumeComponentEditor
    {
        // -- SerializedDataParameters ----------------------------------
        SerializedDataParameter m_CurveMode;
        SerializedDataParameter m_Exposure;
        SerializedDataParameter m_ToneMapScale;
        SerializedDataParameter m_HighlightRolloff;
        SerializedDataParameter m_HighlightLift;
        SerializedDataParameter m_WhiteClip;
        SerializedDataParameter m_ShadowPedestal;
        SerializedDataParameter m_MidContrast;
        SerializedDataParameter m_SatRetention;
        SerializedDataParameter m_ShoulderStrength;
        SerializedDataParameter m_LinearStrength;
        SerializedDataParameter m_LinearAngle;
        SerializedDataParameter m_ToeStrength;
        SerializedDataParameter m_BlackLevel;
        SerializedDataParameter m_DenomBalance;
        SerializedDataParameter m_WhitePoint;
        SerializedDataParameter m_FilmicSaturation;
        SerializedDataParameter m_FilmicExposure;
        SerializedDataParameter m_FilmicToneMapScale;

        // -- Curve preview ---------------------------------------------
        private const int   CURVE_HEIGHT = 180;
        private const float CURVE_X_MAX  = 12f;
        private const float CURVE_Y_MAX  = 1.1f;
        private Texture2D   m_CurveTex;
        private bool        m_Dirty = true;

        // -- EditorPrefs keys ------------------------------------------
        // Anime
        private const string K_A_EXP   = "ZLZ.A.Exposure";
        private const string K_A_SCL   = "ZLZ.A.ToneMapScale";
        private const string K_A_ROLL  = "ZLZ.A.HighlightRolloff";
        private const string K_A_LIFT  = "ZLZ.A.HighlightLift";
        private const string K_A_CLIP  = "ZLZ.A.WhiteClip";
        private const string K_A_PED   = "ZLZ.A.ShadowPedestal";
        private const string K_A_CON   = "ZLZ.A.MidContrast";
        private const string K_A_SAT   = "ZLZ.A.SaturationRetention";
        // Filmic
        private const string K_F_EXP   = "ZLZ.F.Exposure";
        private const string K_F_SCL   = "ZLZ.F.ToneMapScale";
        private const string K_F_SH    = "ZLZ.F.ShoulderStrength";
        private const string K_F_LS    = "ZLZ.F.LinearStrength";
        private const string K_F_LA    = "ZLZ.F.LinearAngle";
        private const string K_F_TOE   = "ZLZ.F.ToeStrength";
        private const string K_F_BL    = "ZLZ.F.BlackLevel";
        private const string K_F_DB    = "ZLZ.F.DenominatorBalance";
        private const string K_F_WP    = "ZLZ.F.WhitePoint";
        private const string K_F_SAT   = "ZLZ.F.FilmicSaturation";
        private const string K_F_FEXP  = "ZLZ.F.FilmicExposure";
        private const string K_F_FSCL  = "ZLZ.F.FilmicToneMapScale";

        // -- Factory defaults (used as fallback before first save)
        private static class AnimeFactory
        {
            public const float Exposure           = 1.0f;
            public const float ToneMapScale       = 1.6f;
            public const float HighlightRolloff   = 0.05f;
            public const float HighlightLift      = 1.0f;
            public const float WhiteClip          = 1.0f;
            public const float ShadowPedestal     = 0.01f;
            public const float MidContrast        = 0.45f;
            public const float SaturationRetention= 0.243f;
        }

        private static class FilmicFactory
        {
            public const float Exposure           = 1.0f;   // unused — kept for compat
            public const float ToneMapScale       = 1.6f;   // unused — kept for compat
            public const float FilmicExposure     = 1.0f;
            public const float FilmicToneMapScale = 1.6f;
            public const float ShoulderStrength   = 0.22f;
            public const float LinearStrength     = 0.30f;
            public const float LinearAngle        = 0.10f;
            public const float ToeStrength        = 0.20f;
            public const float BlackLevel         = 0.01f;
            public const float DenominatorBalance = 0.30f;
            public const float WhitePoint         = 11.2f;
            public const float FilmicSaturation   = 1.15f;
        }

        // -- EditorPrefs helpers ---------------------------------------
        private static float Pref(string key, float fallback)
            => EditorPrefs.GetFloat(key, fallback);

        private static void SavePref(string key, float val)
            => EditorPrefs.SetFloat(key, val);

        private static void ResetPrefToFactory(bool isFilmic)
        {
            if (!isFilmic)
            {
                SavePref(K_A_EXP,  AnimeFactory.Exposure);
                SavePref(K_A_SCL,  AnimeFactory.ToneMapScale);
                SavePref(K_A_ROLL, AnimeFactory.HighlightRolloff);
                SavePref(K_A_LIFT, AnimeFactory.HighlightLift);
                SavePref(K_A_CLIP, AnimeFactory.WhiteClip);
                SavePref(K_A_PED,  AnimeFactory.ShadowPedestal);
                SavePref(K_A_CON,  AnimeFactory.MidContrast);
                SavePref(K_A_SAT,  AnimeFactory.SaturationRetention);
            }
            else
            {
                SavePref(K_F_EXP,  FilmicFactory.FilmicExposure);
                SavePref(K_F_SCL,  FilmicFactory.FilmicToneMapScale);
                SavePref(K_F_SH,   FilmicFactory.ShoulderStrength);
                SavePref(K_F_LS,   FilmicFactory.LinearStrength);
                SavePref(K_F_LA,   FilmicFactory.LinearAngle);
                SavePref(K_F_TOE,  FilmicFactory.ToeStrength);
                SavePref(K_F_BL,   FilmicFactory.BlackLevel);
                SavePref(K_F_DB,   FilmicFactory.DenominatorBalance);
                SavePref(K_F_WP,   FilmicFactory.WhitePoint);
                SavePref(K_F_SAT,  FilmicFactory.FilmicSaturation);
            }
        }

        public override void OnEnable()
        {
            var o = new PropertyFetcher<ZLZ_AnimeToneMap>(serializedObject);
            m_CurveMode        = Unpack(o.Find(x => x.CurveMode));
            m_Exposure         = Unpack(o.Find(x => x.Exposure));
            m_ToneMapScale     = Unpack(o.Find(x => x.ToneMapScale));
            m_HighlightRolloff = Unpack(o.Find(x => x.HighlightRolloff));
            m_HighlightLift    = Unpack(o.Find(x => x.HighlightLift));
            m_WhiteClip        = Unpack(o.Find(x => x.WhiteClip));
            m_ShadowPedestal   = Unpack(o.Find(x => x.ShadowPedestal));
            m_MidContrast      = Unpack(o.Find(x => x.MidContrast));
            m_SatRetention     = Unpack(o.Find(x => x.SaturationRetention));
            m_ShoulderStrength = Unpack(o.Find(x => x.ShoulderStrength));
            m_LinearStrength   = Unpack(o.Find(x => x.LinearStrength));
            m_LinearAngle      = Unpack(o.Find(x => x.LinearAngle));
            m_ToeStrength      = Unpack(o.Find(x => x.ToeStrength));
            m_BlackLevel       = Unpack(o.Find(x => x.BlackLevel));
            m_DenomBalance     = Unpack(o.Find(x => x.DenominatorBalance));
            m_WhitePoint       = Unpack(o.Find(x => x.WhitePoint));
            m_FilmicSaturation   = Unpack(o.Find(x => x.FilmicSaturation));
            m_FilmicExposure     = Unpack(o.Find(x => x.FilmicExposure));
            m_FilmicToneMapScale = Unpack(o.Find(x => x.FilmicToneMapScale));
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            PropertyField(m_CurveMode, new GUIContent("Curve Mode"));
            bool isFilmic = (ZLZToneCurveMode)m_CurveMode.value.intValue == ZLZToneCurveMode.FilmicCurve;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Exposure", EditorStyles.boldLabel);

            if (!isFilmic)
            {
                PropertyField(m_Exposure,     new GUIContent("Exposure"));
                PropertyField(m_ToneMapScale, new GUIContent("Tone Map Scale"));
            }
            else
            {
                PropertyField(m_FilmicExposure,     new GUIContent("Exposure"));
                PropertyField(m_FilmicToneMapScale, new GUIContent("Tone Map Scale"));
            }

            if (!isFilmic)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Highlight", EditorStyles.boldLabel);
                PropertyField(m_HighlightRolloff, new GUIContent("Highlight Rolloff",
                    "Higher = softer rolloff, Lower = harder clip"));
                PropertyField(m_HighlightLift, new GUIContent("Highlight Lift",
                    "Lifts the highlight ceiling to prevent color desaturation"));
                PropertyField(m_WhiteClip, new GUIContent("White Clip",
                    "White point normalization target"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Shadow & Midtone", EditorStyles.boldLabel);
                PropertyField(m_ShadowPedestal, new GUIContent("Shadow Pedestal",
                    "Raises shadow floor to prevent crush"));
                PropertyField(m_MidContrast, new GUIContent("Mid Contrast",
                    "Contrast boost in the midtone range"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                PropertyField(m_SatRetention, new GUIContent("Saturation Retention",
                    "Retains saturation in highlight zones"));
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Filmic Curve", EditorStyles.boldLabel);
                PropertyField(m_ShoulderStrength, new GUIContent("Shoulder Strength"));
                PropertyField(m_LinearStrength,   new GUIContent("Linear Strength"));
                PropertyField(m_LinearAngle,      new GUIContent("Linear Angle"));
                PropertyField(m_ToeStrength,      new GUIContent("Toe Strength"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("White & Black", EditorStyles.boldLabel);
                PropertyField(m_BlackLevel,   new GUIContent("Black Level"));
                PropertyField(m_DenomBalance, new GUIContent("Denominator Balance"));
                PropertyField(m_WhitePoint,   new GUIContent("White Point"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                PropertyField(m_FilmicSaturation, new GUIContent("Saturation",
                    "1.0 = neutral, higher = more saturated, lower = desaturated"));
            }

            if (EditorGUI.EndChangeCheck()) m_Dirty = true;
            serializedObject.ApplyModifiedProperties();

            // -- Defaults Panel + Reset --------------------------------
            EditorGUILayout.Space(8);
            DrawDefaultsPanel(isFilmic);

            // -- Curve Preview -----------------------------------------
            EditorGUILayout.Space(6);
            DrawCurvePreview(isFilmic);
        }

        // -------------------------------------------------------------
        // Defaults Panel  (foldout)
        // -------------------------------------------------------------
        private static bool s_DefaultsFoldout = false;

        // Reset the foldout state when entering Play mode with Domain Reload
        // disabled (Unity 6.6 default). Without this the foldout would survive
        // across Play mode transitions and could mislead the user about state.
        [InitializeOnEnterPlayMode]
        static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            if (!options.HasFlag(EnterPlayModeOptions.DisableDomainReload)) return;
            s_DefaultsFoldout = false;
        }

        private void DrawDefaultsPanel(bool isFilmic)
        {
            // -- Foldout header ----------------------------------------
            var foldStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(0.85f, 0.75f, 0.3f) },
                onNormal  = { textColor = new Color(0.85f, 0.75f, 0.3f) },
            };
            s_DefaultsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(s_DefaultsFoldout,
                "  [ My Default Values ]", foldStyle);
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (!s_DefaultsFoldout) return;

            // -- Info box (English) ------------------------------------
            var infoStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 10,
                wordWrap = true,
                padding  = new RectOffset(8, 8, 6, 6)
            };
            EditorGUILayout.LabelField(
                "These are your personal saved defaults for this curve mode.\n"
                + "  - Edit values below directly, or adjust sliders above first.\n"
                + "  - Press [Save as My Default] to store current slider values.\n"
                + "  - Press [Reset to My Default] to apply saved values to the scene.\n"
                + "  - Press [Restore Factory] to revert to built-in ZLZ defaults.",
                infoStyle);

            EditorGUILayout.Space(4);

            if (!isFilmic) DrawAnimeDefaultsFields();
            else           DrawFilmicDefaultsFields();

            EditorGUILayout.Space(4);

            // -- Button row --------------------------------------------
            var prevBg = GUI.backgroundColor;
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.3f);
            if (GUILayout.Button("Save as My Default", GUILayout.Height(24)))
                SaveCurrentAsDefault(isFilmic);

            Color resetCol = isFilmic ? new Color(0.7f, 0.45f, 0.1f) : new Color(0.15f, 0.45f, 0.75f);
            GUI.backgroundColor = resetCol;
            if (GUILayout.Button("Reset to My Default", GUILayout.Height(24)))
                ApplyDefaultToScene(isFilmic);

            GUI.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
            if (GUILayout.Button("Restore Factory", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Restore Factory Defaults",
                    "Revert saved defaults to built-in ZLZ values? Your saved values will be cleared.",
                    "Confirm", "Cancel"))
                    ResetPrefToFactory(isFilmic);
            }

            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        // -- Fields Shows saved default values (read-only)
        private void DrawAnimeDefaultsFields()
        {
            var headerStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
            EditorGUILayout.LabelField("Saved values -- press [Save as My Default] above to update:", headerStyle);
            EditorGUILayout.Space(2);

            DrawDefaultField("Exposure",            Pref(K_A_EXP,  AnimeFactory.Exposure));
            DrawDefaultField("Tone Map Scale",      Pref(K_A_SCL,  AnimeFactory.ToneMapScale));
            DrawDefaultField("Highlight Rolloff",   Pref(K_A_ROLL, AnimeFactory.HighlightRolloff));
            DrawDefaultField("Highlight Lift",      Pref(K_A_LIFT, AnimeFactory.HighlightLift));
            DrawDefaultField("White Clip",          Pref(K_A_CLIP, AnimeFactory.WhiteClip));
            DrawDefaultField("Shadow Pedestal",     Pref(K_A_PED,  AnimeFactory.ShadowPedestal));
            DrawDefaultField("Mid Contrast",        Pref(K_A_CON,  AnimeFactory.MidContrast));
            DrawDefaultField("Saturation Retention",Pref(K_A_SAT,  AnimeFactory.SaturationRetention));
        }

        private void DrawFilmicDefaultsFields()
        {
            var headerStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
            EditorGUILayout.LabelField("Saved values -- press [Save as My Default] above to update:", headerStyle);
            EditorGUILayout.Space(2);

            DrawDefaultField("Exposure",            Pref(K_F_EXP,  FilmicFactory.FilmicExposure));
            DrawDefaultField("Tone Map Scale",      Pref(K_F_SCL,  FilmicFactory.FilmicToneMapScale));
            DrawDefaultField("Shoulder Strength",   Pref(K_F_SH,   FilmicFactory.ShoulderStrength));
            DrawDefaultField("Linear Strength",     Pref(K_F_LS,   FilmicFactory.LinearStrength));
            DrawDefaultField("Linear Angle",        Pref(K_F_LA,   FilmicFactory.LinearAngle));
            DrawDefaultField("Toe Strength",        Pref(K_F_TOE,  FilmicFactory.ToeStrength));
            DrawDefaultField("Black Level",         Pref(K_F_BL,   FilmicFactory.BlackLevel));
            DrawDefaultField("Denominator Balance", Pref(K_F_DB,   FilmicFactory.DenominatorBalance));
            DrawDefaultField("White Point",         Pref(K_F_WP,   FilmicFactory.WhitePoint));
            DrawDefaultField("Saturation",          Pref(K_F_SAT,  FilmicFactory.FilmicSaturation));
        }

        // float field compact: label + read-only value field
        private static void DrawDefaultField(string label, float current)
        {
            EditorGUILayout.BeginHorizontal();
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { fixedWidth = 160f };
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(160f));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField(current, GUILayout.Width(70f));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        // -- Save current slider values -> EditorPrefs -----------------
        private void SaveCurrentAsDefault(bool isFilmic)
        {
            if (!isFilmic)
            {
                SavePref(K_A_EXP,  m_Exposure.value.floatValue);
                SavePref(K_A_SCL,  m_ToneMapScale.value.floatValue);
                SavePref(K_A_ROLL, m_HighlightRolloff.value.floatValue);
                SavePref(K_A_LIFT, m_HighlightLift.value.floatValue);
                SavePref(K_A_CLIP, m_WhiteClip.value.floatValue);
                SavePref(K_A_PED,  m_ShadowPedestal.value.floatValue);
                SavePref(K_A_CON,  m_MidContrast.value.floatValue);
                SavePref(K_A_SAT,  m_SatRetention.value.floatValue);
            }
            else
            {
                SavePref(K_F_EXP,  m_FilmicExposure.value.floatValue);
                SavePref(K_F_SCL,  m_FilmicToneMapScale.value.floatValue);
                SavePref(K_F_SH,   m_ShoulderStrength.value.floatValue);
                SavePref(K_F_LS,   m_LinearStrength.value.floatValue);
                SavePref(K_F_LA,   m_LinearAngle.value.floatValue);
                SavePref(K_F_TOE,  m_ToeStrength.value.floatValue);
                SavePref(K_F_BL,   m_BlackLevel.value.floatValue);
                SavePref(K_F_DB,   m_DenomBalance.value.floatValue);
                SavePref(K_F_WP,   m_WhitePoint.value.floatValue);
                SavePref(K_F_SAT,  m_FilmicSaturation.value.floatValue);
            }
            EditorUtility.DisplayDialog("ZLZ Tone Mapping", "Saved successfully.", "OK");
        }

        // -- Apply saved defaults -> sliders ---------------------------
        private void ApplyDefaultToScene(bool isFilmic)
        {
            Undo.RecordObject(serializedObject.targetObject, "Reset to My Default");

            if (!isFilmic)
            {
                m_Exposure.value.floatValue         = Pref(K_A_EXP,  AnimeFactory.Exposure);
                m_ToneMapScale.value.floatValue      = Pref(K_A_SCL,  AnimeFactory.ToneMapScale);
                m_HighlightRolloff.value.floatValue  = Pref(K_A_ROLL, AnimeFactory.HighlightRolloff);
                m_HighlightLift.value.floatValue     = Pref(K_A_LIFT, AnimeFactory.HighlightLift);
                m_WhiteClip.value.floatValue         = Pref(K_A_CLIP, AnimeFactory.WhiteClip);
                m_ShadowPedestal.value.floatValue    = Pref(K_A_PED,  AnimeFactory.ShadowPedestal);
                m_MidContrast.value.floatValue       = Pref(K_A_CON,  AnimeFactory.MidContrast);
                m_SatRetention.value.floatValue      = Pref(K_A_SAT,  AnimeFactory.SaturationRetention);
            }
            else
            {
                m_FilmicExposure.value.floatValue     = Pref(K_F_EXP,  FilmicFactory.FilmicExposure);
                m_FilmicToneMapScale.value.floatValue = Pref(K_F_SCL,  FilmicFactory.FilmicToneMapScale);
                m_ShoulderStrength.value.floatValue  = Pref(K_F_SH,   FilmicFactory.ShoulderStrength);
                m_LinearStrength.value.floatValue    = Pref(K_F_LS,   FilmicFactory.LinearStrength);
                m_LinearAngle.value.floatValue       = Pref(K_F_LA,   FilmicFactory.LinearAngle);
                m_ToeStrength.value.floatValue       = Pref(K_F_TOE,  FilmicFactory.ToeStrength);
                m_BlackLevel.value.floatValue        = Pref(K_F_BL,   FilmicFactory.BlackLevel);
                m_DenomBalance.value.floatValue      = Pref(K_F_DB,   FilmicFactory.DenominatorBalance);
                m_WhitePoint.value.floatValue        = Pref(K_F_WP,   FilmicFactory.WhitePoint);
                m_FilmicSaturation.value.floatValue  = Pref(K_F_SAT,  FilmicFactory.FilmicSaturation);
            }

            serializedObject.ApplyModifiedProperties();
            m_Dirty = true;
        }

        // -------------------------------------------------------------
        // Curve Preview
        // -------------------------------------------------------------
        private void DrawCurvePreview(bool isFilmic)
        {
            EditorGUILayout.BeginHorizontal();
            string modeLabel = isFilmic ? "Filmic Curve Preview" : "Anime Curve Preview";
            EditorGUILayout.LabelField(modeLabel, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"x: 0\u201312  y: 0\u20131",
                EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            int texW = Mathf.Max((int)(EditorGUIUtility.currentViewWidth - 40f), 200);
            int texH = CURVE_HEIGHT;

            if (m_Dirty || m_CurveTex == null ||
                m_CurveTex.width != texW || m_CurveTex.height != texH)
            {
                RebuildTexture(texW, texH, isFilmic);
                m_Dirty = false;
            }

            Rect rect = GUILayoutUtility.GetRect(texW, texH,
                GUILayout.Width(texW), GUILayout.Height(texH));
            EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f));
            GUI.DrawTexture(rect, m_CurveTex, ScaleMode.StretchToFill, true);
            DrawOverlayLabels(rect);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            Color curveColor = isFilmic ? new Color(0.9f, 0.65f, 0.2f) : new Color(0.22f, 0.58f, 0.92f);
            string curveLabel = isFilmic ? "ZLZ Filmic Curve" : "ZLZ Anime Curve";
            LegendDot(curveColor, curveLabel);
            LegendDot(new Color(0.50f, 0.50f, 0.50f), "Linear (y = x)");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void RebuildTexture(int w, int h, bool isFilmic)
        {
            if (m_CurveTex != null && (m_CurveTex.width != w || m_CurveTex.height != h))
            { Object.DestroyImmediate(m_CurveTex); m_CurveTex = null; }
            if (m_CurveTex == null)
                m_CurveTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear };

            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
            DrawGrid(px, w, h);

            float[] curveY = new float[w];
            float[] linY   = new float[w];

            for (int i = 0; i < w; i++)
            {
                float x = (i / (float)(w - 1)) * CURVE_X_MAX;
                curveY[i] = isFilmic
                    ? ZLZFilmicCurve(x,
                        m_FilmicExposure.value.floatValue, m_FilmicToneMapScale.value.floatValue,
                        m_ShoulderStrength.value.floatValue, m_LinearStrength.value.floatValue,
                        m_LinearAngle.value.floatValue,      m_ToeStrength.value.floatValue,
                        m_BlackLevel.value.floatValue,       m_DenomBalance.value.floatValue,
                        m_WhitePoint.value.floatValue)
                    : ZLZAnimeCurve(x,
                        m_Exposure.value.floatValue, m_ToneMapScale.value.floatValue,
                        m_HighlightRolloff.value.floatValue, m_HighlightLift.value.floatValue,
                        m_WhiteClip.value.floatValue,        m_ShadowPedestal.value.floatValue,
                        m_MidContrast.value.floatValue);
                linY[i] = Mathf.Clamp01(x / CURVE_X_MAX);
            }

            PlotLine(px, w, h, linY, new Color(0.55f, 0.55f, 0.55f, 0.55f), dashed: true);
            Color col = isFilmic ? new Color(0.9f, 0.65f, 0.2f, 1f) : new Color(0.22f, 0.58f, 0.92f, 1f);
            PlotLine(px, w, h, curveY, col, dashed: false);

            m_CurveTex.SetPixels(px);
            m_CurveTex.Apply();
        }

        private static float ZLZAnimeCurve(float x, float exposure, float scale,
            float rolloff, float lift, float whiteClip, float pedestal, float contrast)
        {
            float exposed = x * exposure * scale;
            float lifted  = Mathf.Max(exposed - pedestal, 0f);
            float knee    = 0.5f * whiteClip;
            float cpow    = 1f + contrast * 0.6f;
            float midtone = Mathf.Pow(Mathf.Clamp01(knee > 0f ? lifted / knee : 0f), cpow) * knee;
            float kr      = rolloff * 3f + 0.5f;
            float hi      = knee + (1f - Mathf.Exp(-kr * Mathf.Max(lifted - knee, 0f)))
                            * (whiteClip - knee + lift);
            float t       = Mathf.Clamp01(knee > 0f ? (lifted - knee * 0.6f) / (knee * 0.6f) : 0f);
            float blend   = t * t * (3f - 2f * t);
            float curved  = Mathf.Lerp(midtone, hi, blend);
            float denom   = whiteClip + lift;
            return Mathf.Clamp(denom > 0f ? curved / denom : 0f, 0f, 1.2f);
        }

        private static float ZLZFilmicCurve(float x, float exposure, float scale,
            float sh, float ls, float la, float ts, float bl, float db, float wp)
        {
            System.Func<float, float> F = (v) =>
            {
                float numer = v * (sh * v + la * ls) + ts * bl;
                float denom = v * (sh * v + ls)      + ts * db;
                return denom != 0f ? numer / denom - bl / db : 0f;
            };
            float col   = scale * exposure * x;
            float white = F(wp);
            return white != 0f ? Mathf.Clamp01(F(col) / white) : 0f;
        }

        private static void DrawGrid(Color[] px, int w, int h)
        {
            var gMinor = new Color(0.18f, 0.18f, 0.18f, 1f);
            var gMajor = new Color(0.27f, 0.27f, 0.27f, 1f);
            for (float xv = 0; xv <= CURVE_X_MAX; xv += 1f)
            {
                int c2 = Mathf.Clamp(Mathf.RoundToInt(xv / CURVE_X_MAX * (w - 1)), 0, w - 1);
                var gc = Mathf.RoundToInt(xv) % 2 == 0 ? gMajor : gMinor;
                for (int r = 0; r < h; r++) px[r * w + c2] = gc;
            }
            for (float yv = 0; yv <= 1f; yv += 0.2f)
            {
                int row = Mathf.Clamp(Mathf.RoundToInt(yv / CURVE_Y_MAX * (h - 1)), 0, h - 1);
                for (int c2 = 0; c2 < w; c2++) px[row * w + c2] = gMajor;
            }
        }

        private static void PlotLine(Color[] px, int w, int h,
            float[] yValues, Color col, bool dashed)
        {
            for (int i = 0; i < w - 1; i++)
            {
                if (dashed && (i / 6) % 2 == 1) continue;
                int py0 = Mathf.Clamp(Mathf.RoundToInt(yValues[i]     / CURVE_Y_MAX * (h - 1)), 0, h - 1);
                int py1 = Mathf.Clamp(Mathf.RoundToInt(yValues[i + 1] / CURVE_Y_MAX * (h - 1)), 0, h - 1);
                int steps = Mathf.Max(Mathf.Abs(py1 - py0) + 1, 1);
                for (int s = 0; s <= steps; s++)
                {
                    float t2 = steps > 0 ? s / (float)steps : 0f;
                    int fx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(i, i + 1, t2)), 0, w - 1);
                    int fy = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(py0, py1, t2)), 0, h - 1);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int row = Mathf.Clamp(fy + dy, 0, h - 1);
                        int idx = row * w + fx;
                        float a = dy == 0 ? col.a : col.a * 0.3f;
                        px[idx] = Color.Lerp(px[idx], col, a);
                    }
                }
            }
        }

        private static void DrawOverlayLabels(Rect rect)
        {
            var numStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = 9,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.55f, 0.55f, 0.55f) }
            };
            var titleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Italic,
                normal    = { textColor = new Color(0.70f, 0.70f, 0.70f) }
            };
            for (float xv = 0; xv <= CURVE_X_MAX; xv += 1f)
            {
                float nx = xv / CURVE_X_MAX;
                GUI.Label(new Rect(rect.x + nx * rect.width - 6f, rect.yMax - 14f, 14f, 12f),
                    xv.ToString("0"), numStyle);
            }
            for (float yv = 0; yv <= 1f; yv += 0.2f)
            {
                float ny = yv / CURVE_Y_MAX;
                GUI.Label(new Rect(rect.x + 2f, rect.yMax - ny * rect.height - 7f, 26f, 12f),
                    yv.ToString("0.0"), numStyle);
            }
            GUI.Label(new Rect(rect.x + rect.width * 0.5f - 60f, rect.yMax - 14f, 160f, 12f),
                "HDR Input Luminance \u2192", titleStyle);
            GUI.Label(new Rect(rect.x + 28f, rect.y + 2f, 120f, 12f),
                "Display Output (0\u20131)", titleStyle);
        }

        private static void LegendDot(Color col, string label)
        {
            var r = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
            r.y += 4f; r.height = 3f;
            EditorGUI.DrawRect(r, col);
            GUILayout.Space(4f);
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Space(14f);
        }

        public override void OnDisable()
        {
            if (m_CurveTex != null) { Object.DestroyImmediate(m_CurveTex); m_CurveTex = null; }
        }
    }
}
#endif
