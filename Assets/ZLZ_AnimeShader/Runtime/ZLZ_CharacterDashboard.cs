// Assets/ZLZ_AnimeShader/Runtime/ZLZ_CharacterDashboard.cs
using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace ZLZ.AnimeShader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ZLZ_HeadDirectionBinder))]
    public class ZLZ_CharacterDashboard : MonoBehaviour
    {
        // Owns headBone — synced to ZLZ_HeadDirectionBinder every frame in Editor
        public Transform headBone;

#if UNITY_EDITOR
        void OnValidate()
        {
            // Sync headBone to Binder whenever Inspector changes
            var binder = GetComponentInChildren<ZLZ_HeadDirectionBinder>();
            if (binder != null && headBone != null)
            {
                binder.headBone = headBone;
                UnityEditor.EditorUtility.SetDirty(binder);
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ZLZ_CharacterDashboard))]
    public class ZLZ_CharacterDashboardEditor : UnityEditor.Editor
    {
        // ── Palette ──────────────────────────────────────────────────────
        static readonly Color C_HEADER             = new Color(0.22f, 0.22f, 0.26f, 1f);
        static readonly Color C_ACCENT             = new Color(0.42f, 0.72f, 1.00f, 1f);
        static readonly Color C_OK                 = new Color(0.22f, 0.75f, 0.45f, 1f);
        static readonly Color C_WARN               = new Color(0.95f, 0.65f, 0.10f, 1f);
        static readonly Color C_ERR                = new Color(0.90f, 0.25f, 0.25f, 1f);
        static readonly Color C_BTN_PRIMARY        = new Color(0.25f, 0.55f, 0.95f, 1f);
        static readonly Color C_BTN_DANGER         = new Color(0.40f, 0.40f, 0.45f, 1f);
        static readonly Color C_SECTION_CLOSED     = new Color(0.17f, 0.17f, 0.20f, 1f);
        static readonly Color C_SECTION_OPEN       = new Color(0.20f, 0.18f, 0.32f, 1f);
        static readonly Color C_SECTION_OPEN_HOV   = new Color(0.27f, 0.24f, 0.42f, 1f);
        static readonly Color C_SECTION_CLOSED_HOV = new Color(0.23f, 0.23f, 0.27f, 1f);
        static readonly Color C_GOLD               = new Color(0.85f, 0.68f, 0.18f, 1f);

        // ── Styles ───────────────────────────────────────────────────────
        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _statusStyle;
        GUIStyle _rowStyle;
        GUIStyle _subtleStyle;
        GUIStyle _infoStyle;

        void InitFoldouts(ZLZ_CharacterDashboard db)
        {
            _initialized = true;

            var binder       = db.GetComponentInChildren<ZLZ_HeadDirectionBinder>();
            bool hasComponent = binder != null;
            bool hasHeadBone  = hasComponent && db.headBone != null;
            bool calibrated   = hasHeadBone && binder.axesCalibrated;
            _headBinderFoldout = !(hasComponent && hasHeadBone && calibrated);

            var infos = CollectMeshBakeInfo(db.gameObject);
            _smoothNormalFoldout = !(infos.Count > 0 && infos.All(m => m.isBaked));

            _toneMappingFoldout       = !(TM_HasRendererFeature() && TM_IsColorGradingHDR());
            _outlineFoldout           = !(OB_HasRendererFeature() || SSO_HasRendererFeature());
            _selectionOutlineFoldout  = !(SO_HasComponent(db) && SO_HasRendererFeature());
            _contactShadowFoldout     = !(CS_IsFeatureReady() && CS_AnyMaterialEnabled(db));
            _vfxFoldout               = VFX_NeedsSetup(db) || (VFX_HasComponent(db) && VFX_RendererCount(db) == 0);
            _ditherFoldout            = Dither_NeedsSetup(db);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            _titleStyle.normal.textColor = C_ACCENT;

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            _sectionStyle.normal.textColor = new Color(0.85f, 0.85f, 0.90f, 1f);

            _statusStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            _rowStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 0, 0, 0) };
            _rowStyle.normal.textColor = new Color(0.70f, 0.70f, 0.75f, 1f);

            _subtleStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            _subtleStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f, 1f);

            _infoStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true };
            _infoStyle.normal.textColor = new Color(0.65f, 0.65f, 0.70f, 1f);
        }

        bool _initialized              = false;
        bool _headBinderFoldout        = false;
        bool _smoothNormalFoldout      = false;
        bool _toneMappingFoldout       = false;
        bool _outlineFoldout           = false;
        bool _selectionOutlineFoldout  = false;
        bool _contactShadowFoldout     = false;
        bool _vfxFoldout               = false;
        bool _ditherFoldout            = false;
        bool _performanceFoldout       = true;

        // Editor-time preview state for the Dither panel's Preview Alpha slider.
        // Persists across repaints; cleared via Reset button or when foldout closes.
        float _ditherPreviewAlpha  = 0f;
        bool  _ditherPreviewActive = false;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        // ── Main draw ────────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            EnsureStyles();
            var db = (ZLZ_CharacterDashboard)target;
            if (!_initialized) InitFoldouts(db);

            DrawTitle(db);
            GUILayout.Space(6);
            DrawHeadBinderSection(db);
            GUILayout.Space(4);
            DrawSmoothNormalSection(db);
            GUILayout.Space(4);
            DrawToneMappingSection();
            GUILayout.Space(4);
            DrawOutlineSection(db);
            GUILayout.Space(4);
            DrawSelectionOutlineSection(db);
            GUILayout.Space(4);
            DrawCharacterContactShadowSection(db);
            GUILayout.Space(4);
            DrawDitherSection(db);
            GUILayout.Space(4);
            DrawVFXSection(db);
            GUILayout.Space(4);
            DrawPerformanceSection(db);
        }

        // ── Title ────────────────────────────────────────────────────────
        void DrawTitle(ZLZ_CharacterDashboard db)
        {
            Rect bar = GUILayoutUtility.GetRect(0, 38f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, C_HEADER);
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), C_ACCENT);
            EditorGUI.LabelField(
                new Rect(bar.x + 12, bar.y, bar.width - 12, bar.height),
                $"ZLZ  Character Dashboard  —  {db.gameObject.name}", _titleStyle);
        }

        // ── Section header ───────────────────────────────────────────────
        bool DrawSectionHeader(string title, bool foldout, string statusText = "", Color statusColor = default)
        {
            Rect h = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
            bool isHover = h.Contains(Event.current.mousePosition);
            Color bg = foldout
                ? (isHover ? C_SECTION_OPEN_HOV   : C_SECTION_OPEN)
                : (isHover ? C_SECTION_CLOSED_HOV : C_SECTION_CLOSED);

            EditorGUI.DrawRect(h, bg);
            if (foldout || isHover) EditorGUI.DrawRect(new Rect(h.x, h.y, 2f, h.height), C_GOLD);

            bool useGold = foldout || isHover;
            GUIStyle labelStyle = _sectionStyle;
            if (useGold) { labelStyle = new GUIStyle(_sectionStyle); labelStyle.normal.textColor = C_GOLD; }

            EditorGUI.LabelField(new Rect(h.x + 10, h.y, 16,         h.height), foldout ? "▾" : "▸", labelStyle);
            EditorGUI.LabelField(new Rect(h.x + 26, h.y, h.width-32, h.height), title, labelStyle);

            if (!string.IsNullOrEmpty(statusText))
            {
                var st = new GUIStyle(_subtleStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
                if (statusColor != default) st.normal.textColor = statusColor;
                EditorGUI.LabelField(new Rect(h.x, h.y, h.width - 10, h.height), statusText, st);
            }

            if (GUI.Button(h, GUIContent.none, GUIStyle.none)) foldout = !foldout;
            EditorGUIUtility.AddCursorRect(h, MouseCursor.Link);
            if (isHover) Repaint();
            return foldout;
        }

        // ── Section border ───────────────────────────────────────────────
        void DrawSectionBorder(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || rect.height <= 0) return;
            const float t = 1f;
            EditorGUI.DrawRect(new Rect(rect.x,          rect.y,         rect.width, t),          C_GOLD);
            EditorGUI.DrawRect(new Rect(rect.x,          rect.yMax - t,  rect.width, t),          C_GOLD);
            EditorGUI.DrawRect(new Rect(rect.x,          rect.y,         t,          rect.height), C_GOLD);
            EditorGUI.DrawRect(new Rect(rect.xMax - t,   rect.y,         t,          rect.height), C_GOLD);
        }

        // ── Coming soon stub ─────────────────────────────────────────────
        void DrawComingSoonStub(string name)
        {
            Rect h = GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(h, C_SECTION_CLOSED);
            EditorGUI.LabelField(new Rect(h.x + 10, h.y, 16,          h.height), "▸", _sectionStyle);
            EditorGUI.LabelField(new Rect(h.x + 26, h.y, h.width-100, h.height), name, _sectionStyle);
            var st = new GUIStyle(_subtleStyle) { alignment = TextAnchor.MiddleRight };
            EditorGUI.LabelField(new Rect(h.x, h.y, h.width - 8, h.height), "coming soon", st);
        }

        // ── Status banner ─────────────────────────────────────────────────
        void DrawBanner(Color bg, Color stripe, string icon, string msg, Color textColor)
        {
            Rect r = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, bg);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), stripe);
            var st = new GUIStyle(_statusStyle);
            st.normal.textColor = textColor;
            EditorGUI.LabelField(new Rect(r.x + 10, r.y, r.width - 12, r.height), $"{icon}  {msg}", st);
        }

        // ============================================================
        //  HEAD DIRECTION BINDER SECTION
        // ============================================================
        void DrawHeadBinderSection(ZLZ_CharacterDashboard db)
        {
            var binder = db.GetComponentInChildren<ZLZ_HeadDirectionBinder>();
            bool hasComponent    = binder != null;
            bool hasHeadBone     = hasComponent && db.headBone != null;
            if (hasHeadBone) binder.headBone = db.headBone; // keep in sync
            bool axesAreDefault  = hasHeadBone && !binder.axesCalibrated;
            bool isReady         = hasComponent && hasHeadBone && !axesAreDefault;

            string hStatus = !hasComponent  ? "⚠  Component missing" :
                             !hasHeadBone   ? "⚠  Head Bone not set" :
                             axesAreDefault ? "⚠  Axes not calibrated" :
                                              "✔  Ready";
            Color hColor = isReady ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _headBinderFoldout = DrawSectionHeader("ZLZ Head Direction Binder", _headBinderFoldout, hStatus, hColor);
            if (_headBinderFoldout)
            {
                GUILayout.Space(8);

                // ── Component missing ────────────────────────────────────
                if (!hasComponent)
                {
                    DrawBanner(new Color(0.35f,0.15f,0.10f,1f), C_ERR, "⛔",
                        "ZLZ_HeadDirectionBinder component is missing", C_ERR);
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField(
                        "  Add ZLZ_HeadDirectionBinder to this GameObject or any child.\n" +
                        "  Face Shadow and Hair Transparent will not work without it.",
                        _infoStyle);
                }
                else
                {
                    // ── Head Bone not set ────────────────────────────────────
                    if (!hasHeadBone)
                    {
                        DrawBanner(new Color(0.38f,0.22f,0.04f,1f), C_WARN, "⚠",
                            "Head Bone is not assigned", C_WARN);
                        GUILayout.Space(4);
                        EditorGUILayout.LabelField(
                            "  Drag the head bone from the character skeleton into the field below.\n" +
                            "  Face Shadow and Hair Transparent will not work without it.",
                            _infoStyle);
                    }
                    else
                    {
                        DrawBanner(new Color(0.12f,0.32f,0.18f,1f), C_OK, "✔",
                            "Head Direction Binder is active", C_OK);
                        if (axesAreDefault)
                        {
                            GUILayout.Space(2);
                            DrawBanner(new Color(0.38f,0.22f,0.04f,1f), C_WARN, "⚠",
                                "Axes are at default — run Auto Detect Axes to calibrate for this character", C_WARN);
                        }
                    }

                    GUILayout.Space(6);

                    // ── Head Bone field (owned by Dashboard) ─────────────────
                    var dbSo = new SerializedObject(db);
                    dbSo.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(dbSo.FindProperty("headBone"), new GUIContent("Head Bone"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        dbSo.ApplyModifiedProperties();
                        binder.headBone = db.headBone;
                        EditorUtility.SetDirty(binder);
                        EditorUtility.SetDirty(db);
                    }

                    // ── Auto Detect Head Bone button ──────────────────────────
                    GUILayout.Space(2);
                    EditorGUILayout.LabelField("  Auto Detect works with Humanoid rigs only.", _infoStyle);
                    GUILayout.Space(2);
                    var animator = db.GetComponentInChildren<Animator>();
                    bool isHumanoid = animator != null && animator.isHuman;
                    Color prev2 = GUI.backgroundColor;
                    GUI.backgroundColor = C_BTN_PRIMARY;
                    GUI.enabled = isHumanoid;
                    if (GUILayout.Button("Auto Detect Head Bone", GUILayout.Height(24)))
                    {
                        var detected = animator.GetBoneTransform(HumanBodyBones.Head);
                        if (detected != null)
                        {
                            Undo.RecordObject(db, "Auto Detect Head Bone");
                            db.headBone     = detected;
                            binder.headBone = detected;
                            EditorUtility.SetDirty(db);
                            EditorUtility.SetDirty(binder);
                            Debug.Log($"[ZLZ] Auto Detect Head Bone → {detected.name}", db);
                        }
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = prev2;
                    if (!isHumanoid)
                    {
                        GUILayout.Space(2);
                        EditorGUILayout.LabelField("  Humanoid rig not detected — assign Head Bone manually.", _infoStyle);
                    }

                    // ── Axis display (read-only) ──────────────────────────────
                    if (hasHeadBone)
                    {
                        GUILayout.Space(4);
                        Rect axRow = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
                        EditorGUI.DrawRect(axRow, new Color(0.20f,0.20f,0.24f,1f));
                        EditorGUI.LabelField(
                            new Rect(axRow.x+6, axRow.y, axRow.width-8, axRow.height),
                            $"Forward: {binder.forwardAxis}   |   Right: {binder.rightAxis}", _subtleStyle);
                    }

                    // ── Renderer list (auto-detected) ─────────────────────────
                    GUILayout.Space(6);
                    var renderers = db.GetComponentsInChildren<Renderer>(true)
                                      .Where(r => GetMesh(r) != null).ToArray();

                    Rect rHdr = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(rHdr, new Color(0.20f,0.20f,0.24f,1f));
                    EditorGUI.LabelField(new Rect(rHdr.x+6, rHdr.y, rHdr.width, rHdr.height),
                        $"Renderers receiving head data  ({renderers.Length} auto-detected)", _subtleStyle);

                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Rect row = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
                        EditorGUI.DrawRect(row, i % 2 == 0 ? new Color(0.16f,0.16f,0.19f,1f) : new Color(0.18f,0.18f,0.21f,1f));
                        EditorGUI.LabelField(new Rect(row.x+6, row.y, row.width-8, row.height),
                            renderers[i].gameObject.name, _rowStyle);
                    }

                    // ── Buttons (bottom) ─────────────────────────────────────
                    GUILayout.Space(8);
                    DrawHeadBinderButtons(binder, hasHeadBone);
                }

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_headBinderFoldout) DrawSectionBorder(sectionRect);
        }

        void DrawHeadBinderButtons(ZLZ_HeadDirectionBinder binder, bool hasHeadBone)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2);
            Color prev = GUI.backgroundColor;

            GUI.backgroundColor = C_BTN_PRIMARY;
            GUI.enabled = hasHeadBone;
            if (GUILayout.Button("Auto Detect Axes", GUILayout.Height(26)))
            {
                Undo.RecordObject(binder, "Auto Detect Axes");
                binder.AutoDetectAxes();
                EditorUtility.SetDirty(binder);
            }

            GUI.enabled = true;
            GUI.backgroundColor = C_BTN_DANGER;
            if (GUILayout.Button("Reset Axes", GUILayout.Width(90), GUILayout.Height(26)))
            {
                Undo.RecordObject(binder, "Reset Axes");
                binder.forwardAxis    = ZLZ_HeadDirectionBinder.HeadForwardAxis.Z_Positive;
                binder.rightAxis      = ZLZ_HeadDirectionBinder.HeadRightAxis.X_Positive;
                binder.axesCalibrated = false;
                EditorUtility.SetDirty(binder);
                Debug.Log("[ZLZ] ResetAxes → Forward=Z_Positive, Right=X_Positive", binder);
            }

            GUI.backgroundColor = prev;
            GUILayout.Space(2);
            EditorGUILayout.EndHorizontal();

            if (!hasHeadBone)
            {
                GUILayout.Space(2);
                EditorGUILayout.LabelField("  Assign Head Bone first to enable Auto Detect.", _infoStyle);
            }
        }

        // ============================================================
        //  TONE MAPPING SECTION
        // ============================================================
        void DrawToneMappingSection()
        {
            bool hasFeature     = TM_HasRendererFeature();
            bool isHDR          = TM_IsColorGradingHDR();
            bool allReady       = hasFeature && isHDR;
            bool noneReady      = !hasFeature && !isHDR;

            string hStatus = allReady  ? "✔  Ready" :
                             noneReady ? "⚠  Not configured" :
                                         "⚠  Partial";
            Color hColor = allReady ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _toneMappingFoldout = DrawSectionHeader("ZLZ Anime Tone Mapping", _toneMappingFoldout, hStatus, hColor);
            if (_toneMappingFoldout)
            {
                GUILayout.Space(8);

                if (allReady)
                    DrawBanner(new Color(0.12f,0.32f,0.18f,1f), C_OK, "✔", "Project is configured for ZLZ Anime Tone Mapping", C_OK);
                else if (noneReady)
                    DrawBanner(new Color(0.38f,0.22f,0.04f,1f), C_WARN, "⚠", "Tone Mapping is not configured — click Setup below", C_WARN);
                else
                    DrawBanner(new Color(0.35f,0.26f,0.04f,1f), C_WARN, "◑", "Partially configured — click Setup to complete", C_WARN);

                GUILayout.Space(6);

                // ── Status rows ──────────────────────────────────────────
                DrawTMStatusRow("Render Feature  (ZLZ_AnimeToneMappingFeature)", hasFeature, 0);
                DrawTMStatusRow("Color Grading Mode  →  HDR",                    isHDR,      1);

                // ── Setup button ─────────────────────────────────────────
                if (!allReady)
                {
                    GUILayout.Space(8);
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = C_BTN_PRIMARY;
                    if (GUILayout.Button("Setup Tone Mapping", GUILayout.Height(26)))
                        TM_SetupAll();
                    GUI.backgroundColor = prev;
                }

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_toneMappingFoldout) DrawSectionBorder(sectionRect);
        }

        void DrawTMStatusRow(string label, bool done, int rowIndex)
        {
            Rect row = GUILayoutUtility.GetRect(0, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, rowIndex % 2 == 0 ? new Color(0.16f,0.16f,0.19f,1f) : new Color(0.18f,0.18f,0.21f,1f));

            var iconStyle = new GUIStyle(_rowStyle) { fontStyle = FontStyle.Bold };
            iconStyle.normal.textColor = done ? C_OK : C_WARN;
            EditorGUI.LabelField(new Rect(row.x+6, row.y, 20, row.height), done ? "✔" : "⚠", iconStyle);
            EditorGUI.LabelField(new Rect(row.x+24, row.y, row.width-32, row.height), label, _rowStyle);

            var stateStyle = new GUIStyle(_rowStyle) { alignment = TextAnchor.MiddleRight };
            stateStyle.normal.textColor = done ? C_OK : new Color(0.55f,0.55f,0.60f,1f);
            EditorGUI.LabelField(new Rect(row.x, row.y, row.width-8, row.height), done ? "DONE" : "—", stateStyle);
        }

        // ============================================================
        //  VFX FEATURES SECTION
        // ============================================================
        void DrawVFXSection(ZLZ_CharacterDashboard db)
        {
            bool hasVFX      = VFX_HasComponent(db);
            int  rendCount   = hasVFX ? VFX_RendererCount(db) : 0;
            bool needsSetup  = VFX_NeedsSetup(db);
            bool noRenderers = hasVFX && !needsSetup && rendCount == 0;
            bool ready       = !needsSetup && !noRenderers;

            string hStatus = !hasVFX     ? "⚠  Not configured" :
                             needsSetup  ? "⚠  Needs setup"    :
                             noRenderers ? "⚠  No renderers"   :
                                           "✔  Ready";
            Color hColor = ready ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _vfxFoldout = DrawSectionHeader("ZLZ VFX Features", _vfxFoldout, hStatus, hColor);
            if (_vfxFoldout)
            {
                GUILayout.Space(8);

                if (ready)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK,   "✔", "VFX features are configured", C_OK);
                else if (!hasVFX)
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "VFX features are not configured — click Setup below", C_WARN);
                else if (needsSetup)
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "Some items need attention — click Setup VFX Features (auto-fixes renderers and Settings)", C_WARN);
                else // noRenderers
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "No child renderers found — add a Mesh / Skinned Mesh under this GameObject to enable VFX", C_WARN);

                GUILayout.Space(6);

                if (hasVFX)
                {
                    var vfx   = db.GetComponent<ZLZ_CharacterVFX>();
                    var vfxSo = new SerializedObject(vfx);
                    vfxSo.Update();
                    DrawUpgradeControls  (vfx, vfxSo);
                    DrawGetHitControls   (vfx, vfxSo);
                    DrawIndicatorControls(vfx, vfxSo);
                    DrawDissolveControls (vfx, vfxSo);
                    DrawDarkenControls   (vfx, vfxSo);
                    if (vfxSo.ApplyModifiedProperties())
                        EditorUtility.SetDirty(vfx);
                }

                GUILayout.Space(8);

                // Single unified action — auto-creates the component, fills missing
                // Settings, and refreshes renderers.
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = C_BTN_PRIMARY;
                GUI.enabled = needsSetup;
                if (GUILayout.Button("Setup VFX Features", GUILayout.Height(26)))
                    VFX_SetupAll(db);
                GUI.enabled = true;
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_vfxFoldout) DrawSectionBorder(sectionRect);
        }

        // ============================================================
        //  DITHER SECTION
        //  Separated from VFX Features because Dither is a configurable
        //  capability (Pattern + Camera Near Fade + manual alpha) rather
        //  than a one-shot trigger. Owns its own setup flow and preview.
        // ============================================================
        void DrawDitherSection(ZLZ_CharacterDashboard db)
        {
            bool hasVFX     = VFX_HasComponent(db);
            bool needsSetup = Dither_NeedsSetup(db);
            bool ready      = hasVFX && !needsSetup;

            string hStatus = !hasVFX    ? "⚠  Not configured" :
                             needsSetup ? "⚠  Needs setup"    :
                                          "✔  Ready";
            Color hColor = ready ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _ditherFoldout = DrawSectionHeader("ZLZ Dither", _ditherFoldout, hStatus, hColor);
            if (_ditherFoldout)
            {
                GUILayout.Space(8);

                if (ready)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK,   "✔", "Dither is configured", C_OK);
                else if (!hasVFX)
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "Add ZLZ_CharacterVFX (or click Setup) to enable Dither controls", C_WARN);
                else
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "Dither settings asset is missing — click Setup Dither below", C_WARN);

                GUILayout.Space(6);

                if (hasVFX)
                {
                    var vfx   = db.GetComponent<ZLZ_CharacterVFX>();
                    var vfxSo = new SerializedObject(vfx);
                    vfxSo.Update();
                    DrawDitherConfig(vfx, vfxSo);
                    if (vfxSo.ApplyModifiedProperties())
                        EditorUtility.SetDirty(vfx);
                }

                GUILayout.Space(8);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = C_BTN_PRIMARY;
                GUI.enabled = needsSetup;
                if (GUILayout.Button("Setup Dither", GUILayout.Height(26)))
                    Dither_SetupAll(db);
                GUI.enabled = true;
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
            }
            else if (_ditherPreviewActive)
            {
                // Foldout collapsed — clean up the preview MPB so the character
                // doesn't stay dithered when the user can no longer see the slider.
                var vfx = db.GetComponent<ZLZ_CharacterVFX>();
                if (vfx != null) ZLZ_DitherFX.ClearEditorPreview(vfx.TargetRenderers);
                _ditherPreviewActive = false;
                ForceEditorRepaint();
            }
            EditorGUILayout.EndVertical();
            if (_ditherFoldout) DrawSectionBorder(sectionRect);
        }

        void DrawDitherConfig(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(4);

            // Master switch — gates the runtime dither drivers (camera-near fade,
            // occlusion fade, Hide/Show). The dither shader path is always compiled.
            EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._enabled"), new GUIContent("Enable Dither",
                "Master switch for the whole Dither feature on this character. " +
                "When OFF, this character never dithers — camera-near fade, occlusion fade and Hide/Show are all disabled."));

            using (new EditorGUI.DisabledScope(!vfx.Dither.Enabled))
            {
                // Preview Alpha — writes MPB directly so the user can drag in BOTH edit
                // and play mode to see how the dither looks at any value (scout a good
                // occlusion level, check the fade at a given alpha, etc.).
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newAlpha = EditorGUILayout.Slider(new GUIContent("Preview Alpha",
                    "Live preview of the dither pattern at this alpha. 0 = fully visible, 1 = fully clipped. " +
                    "Use this to scout a Soft/Full level value that fits."),
                    _ditherPreviewAlpha, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    _ditherPreviewAlpha  = newAlpha;
                    _ditherPreviewActive = true;
                    ZLZ_DitherFX.SetEditorPreviewAlpha(vfx.TargetRenderers, newAlpha);
                    ForceEditorRepaint();
                }
                using (new EditorGUI.DisabledScope(!_ditherPreviewActive))
                {
                    if (GUILayout.Button("Reset", GUILayout.Width(58)))
                    {
                        _ditherPreviewAlpha  = 0f;
                        _ditherPreviewActive = false;
                        ZLZ_DitherFX.ClearEditorPreview(vfx.TargetRenderers);
                        ForceEditorRepaint();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._settings"), new GUIContent("Animation Settings",
                    "ZLZ_DitherSettings asset — defines Hide / Show / Spawn animation curves AND occlusion levels (Soft / Full)."));

                // ═══ Auto Camera Fade ═══════════════════════════════════════
                // In-shader distance-based fade. Useful for VRChat / first-person
                // (camera = head) where the camera physically intersects the model.
                DrawSubsectionHeader("Auto Camera Fade   (Main Character Only)");
                EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._enableCameraNearFade"), new GUIContent("Enabled",
                    "Fade this character automatically when the player camera gets close. " +
                    "Useful for VRChat and first-person collision."));

                using (new EditorGUI.DisabledScope(!vfx.Dither.EnableCameraNearFade))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._cameraNearDistance"), new GUIContent("Near Distance",
                        "Camera distance at which the character is fully dithered out (closest)."));
                    EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._cameraFarDistance"),  new GUIContent("Far Distance",
                        "Camera distance at which the fade starts (beyond this = fully visible)."));

                    DrawCameraDistanceReadout(vfx);
                    EditorGUI.indentLevel--;
                }

                // ═══ Receive Occlusion Fade ═════════════════════════════════
                // Raycast-based system driven by ZLZ_OcclusionFader (scene manager).
                // Modern anime-style "fade NPC when blocking camera→player line of sight".
                DrawSubsectionHeader("Receive Occlusion Fade");
                EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._receiveOcclusionFade"), new GUIContent("Enabled",
                    "When this character blocks the camera→player line of sight, fade it out so the player stays visible. " +
                    "Requires a ZLZ_OcclusionFader in the scene with the Player assigned."));

                using (new EditorGUI.DisabledScope(!vfx.Dither.ReceiveOcclusionFade))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(vfxSo.FindProperty("Dither._occlusionLevel"), new GUIContent("Level",
                        "Soft = ghost-through (default 0.9). Full = hide-through (default 1.0). " +
                        "Adjust the values themselves on the ZLZ_DitherSettings asset."));

                    // Manager reference (read-only) — click to ping in Hierarchy.
                    // When missing, show an inline "Create in Scene" button so the
                    // Manager is added explicitly per-scene by the user. Setup Dither
                    // no longer touches the scene — that would leave Manager GameObjects
                    // in every scene the user merely edits a character in.
#if UNITY_2022_2_OR_NEWER
                    var manager = Object.FindFirstObjectByType<ZLZ_OcclusionFader>(FindObjectsInactive.Include);
#else
                    var manager = Object.FindObjectOfType<ZLZ_OcclusionFader>(true);
#endif
                    bool inPrefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null;

                    EditorGUILayout.BeginHorizontal();
                    bool prev = GUI.enabled;
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("Manager", manager, typeof(ZLZ_OcclusionFader), allowSceneObjects: true);
                    GUI.enabled = prev;

                    if (manager == null && !inPrefabStage)
                    {
                        if (GUILayout.Button(new GUIContent("Create in Scene",
                                "Add a ZLZ_OcclusionFader to the current scene. " +
                                "Assign its Target Transform to the player after creation."),
                                GUILayout.Width(110)))
                        {
                            Dither_EnsureOcclusionFader();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }
            }
        }

        void DrawCameraDistanceReadout(ZLZ_CharacterVFX vfx)
        {
            Camera cam = Application.isPlaying ? Camera.main : null;
            if (cam == null && SceneView.lastActiveSceneView != null)
                cam = SceneView.lastActiveSceneView.camera;
            if (cam == null) return;

            float d  = Vector3.Distance(vfx.transform.position, cam.transform.position);
            float near = vfx.Dither.CameraNearDistance;
            float far  = vfx.Dither.CameraFarDistance;
            float autoAlpha = Mathf.Clamp01((far - d) / Mathf.Max(far - near, 1e-4f));

            var s = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            s.normal.textColor = autoAlpha > 0.01f ? C_OK : new Color(0.55f, 0.55f, 0.60f);
            EditorGUILayout.LabelField($"Distance: {d:F2}m   →   Auto Alpha: {autoAlpha:F2}", s);
        }

        void DrawUpgradeControls(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(vfxSo.FindProperty("Upgrade._settings"), new GUIContent("Upgrade"));
            GUI.enabled = Application.isPlaying;
            Color prevTest = GUI.backgroundColor;
            GUI.backgroundColor = vfx.Upgrade.IsActive() ? C_OK : C_BTN_PRIMARY;
            if (GUILayout.Button(vfx.Upgrade.IsActive() ? "■  Stop" : "▶  Play",
                    GUILayout.Width(64), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                vfx.Upgrade.ToggleUpgrade();
            GUI.backgroundColor = prevTest;
            GUI.enabled = true;
            var stateStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            stateStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f, 1f);
            EditorGUILayout.LabelField(Application.isPlaying ? vfx.Upgrade.CurrentState.ToString() : "—",
                stateStyle, GUILayout.Width(44));
            EditorGUILayout.EndHorizontal();
        }

        void DrawGetHitControls(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(vfxSo.FindProperty("GetHit._settings"), new GUIContent("GetHit"));
            GUI.enabled = Application.isPlaying;
            Color prevTest = GUI.backgroundColor;
            GUI.backgroundColor = vfx.GetHit.IsActive() ? C_OK : C_BTN_PRIMARY;
            if (GUILayout.Button("●  Hit",
                    GUILayout.Width(64), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                vfx.GetHit.Hit();
            GUI.backgroundColor = prevTest;
            GUI.enabled = true;
            var stateStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            stateStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f, 1f);
            EditorGUILayout.LabelField(Application.isPlaying ? vfx.GetHit.CurrentState.ToString() : "—",
                stateStyle, GUILayout.Width(44));
            EditorGUILayout.EndHorizontal();
        }

        void DrawIndicatorControls(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(vfxSo.FindProperty("Indicator._settings"), new GUIContent("Indicator"));
            GUI.enabled = Application.isPlaying;
            Color prevTest = GUI.backgroundColor;
            GUI.backgroundColor = vfx.Indicator.IsActive() ? C_OK : C_BTN_PRIMARY;
            if (GUILayout.Button(vfx.Indicator.IsActive() ? "■  Stop" : "▶  Play",
                    GUILayout.Width(64), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                vfx.Indicator.ToggleIndicator();
            GUI.backgroundColor = prevTest;
            GUI.enabled = true;
            var stateStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            stateStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f, 1f);
            EditorGUILayout.LabelField(Application.isPlaying ? vfx.Indicator.CurrentState.ToString() : "—",
                stateStyle, GUILayout.Width(44));
            EditorGUILayout.EndHorizontal();
        }

        void DrawDissolveControls(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(vfxSo.FindProperty("Dissolve._settings"), new GUIContent("Dissolve"));
            GUI.enabled = Application.isPlaying;
            Color prevTest = GUI.backgroundColor;
            GUI.backgroundColor = vfx.Dissolve.IsActive() ? C_OK : C_BTN_PRIMARY;
            if (GUILayout.Button(vfx.Dissolve.IsActive() ? "■  Restore" : "▶  Dissolve",
                    GUILayout.Width(74), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                if (vfx.Dissolve.IsActive()) vfx.Dissolve.Restore();
                else                         vfx.Dissolve.Dissolve();
            }
            GUI.backgroundColor = prevTest;
            GUI.enabled = true;
            var stateStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            stateStyle.normal.textColor = new Color(0.55f, 0.55f, 0.60f, 1f);
            EditorGUILayout.LabelField(Application.isPlaying ? vfx.Dissolve.CurrentState.ToString() : "—",
                stateStyle, GUILayout.Width(44));
            EditorGUILayout.EndHorizontal();
        }


        void DrawDarkenControls(ZLZ_CharacterVFX vfx, SerializedObject vfxSo)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            // Manager reference (read-only, click to ping in Hierarchy).
            // Tells the user that animation settings live on the scene Manager.
#if UNITY_2022_2_OR_NEWER
            var manager = Object.FindFirstObjectByType<ZLZ_DarkenManager>(FindObjectsInactive.Include);
#else
            var manager = Object.FindObjectOfType<ZLZ_DarkenManager>(true);
#endif
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.ObjectField(
                new GUIContent("Darken", "Animation settings live on the ZLZ_DarkenManager component in the scene. Click the field to ping it in the Hierarchy."),
                manager, typeof(ZLZ_DarkenManager), allowSceneObjects: true);
            GUI.enabled = wasEnabled;

            // Per-character Exclude toggle.
            var excludedProp = vfxSo.FindProperty("Darken._excluded");
            if (excludedProp != null)
            {
                EditorGUI.BeginChangeCheck();
                bool next = GUILayout.Toggle(excludedProp.boolValue,
                    new GUIContent(" Exclude", "Stay bright when global darken activates."),
                    GUILayout.Width(74));
                if (EditorGUI.EndChangeCheck())
                {
                    excludedProp.boolValue = next;
                    if (Application.isPlaying) vfx.Darken.SetExcluded(next);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── VFX Helpers ───────────────────────────────────────────────────
        const string k_FXSettingsFolder      = "Assets/ZLZ_AnimeShader/FX_Settings";
        const string k_UpgradeSettingsPath   = k_FXSettingsFolder + "/ZLZ_UpgradeSettings.asset";
        const string k_GetHitSettingsPath    = k_FXSettingsFolder + "/ZLZ_GetHitSettings.asset";
        const string k_IndicatorSettingsPath = k_FXSettingsFolder + "/ZLZ_IndicatorSettings.asset";
        const string k_DissolveSettingsPath  = k_FXSettingsFolder + "/ZLZ_DissolveSettings.asset";
        const string k_DitherSettingsPath    = k_FXSettingsFolder + "/ZLZ_DitherSettings.asset";

        static bool VFX_HasComponent(ZLZ_CharacterDashboard db)
            => db.GetComponent<ZLZ_CharacterVFX>() != null;

        static int VFX_RendererCount(ZLZ_CharacterDashboard db)
        {
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null) return 0;
            return new SerializedObject(vfx).FindProperty("_targetRenderers").arraySize;
        }

        static void VFX_SetupAll(ZLZ_CharacterDashboard db)
        {
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null)
            {
                vfx = Undo.AddComponent<ZLZ_CharacterVFX>(db.gameObject);
                Debug.Log("[ZLZ] Added ZLZ_CharacterVFX.");
            }

            // Smart-fill: only assign Settings when the field is null so we never
            // clobber a user-picked custom asset. This makes the button safe to
            // re-run after a package update that adds new FX blocks.
            var so = new SerializedObject(vfx);
            AssignSettingsIfNull(so, "Upgrade._settings",   VFX_GetOrCreateUpgradeSettings);
            AssignSettingsIfNull(so, "GetHit._settings",    VFX_GetOrCreateGetHitSettings);
            AssignSettingsIfNull(so, "Indicator._settings", VFX_GetOrCreateIndicatorSettings);
            AssignSettingsIfNull(so, "Dissolve._settings",  VFX_GetOrCreateDissolveSettings);
            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(vfx);

            // ZLZ_DarkenManager is intentionally NOT auto-created here. Darken is
            // fully decoupled: the Manager only drives the global shader uniform
            // _TargetDarkenGlobal, which every Darken-enabled character reads on its
            // own — there is no per-character reference to wire up. Creating it from a
            // character-setup action would dirty (and leave a Manager GameObject in)
            // every scene the user merely opens. Users add ZLZ_DarkenManager on demand
            // via Add Component ("ZLZ/Anime Shader/ZLZ_Darken Manager"); it runs on a
            // built-in animation fallback, so no Settings asset is required. Mirrors
            // the same decision for Dither's OcclusionFader in Dither_SetupAll.

            VFX_RefreshRenderers(db);
        }

        static void AssignSettingsIfNull(SerializedObject so, string propPath, System.Func<Object> factory)
        {
            var prop = so.FindProperty(propPath);
            if (prop == null || prop.objectReferenceValue != null) return;
            var asset = factory();
            if (asset != null) prop.objectReferenceValue = asset;
        }

        /// <summary>
        /// True if any of: component missing, renderer list out of sync with current
        /// children, or any Settings null. Used to drive the
        /// single "Setup VFX Features" button — when this returns true, the button
        /// enables and a one-click run brings everything back into a Ready state.
        /// </summary>
        static bool VFX_NeedsSetup(ZLZ_CharacterDashboard db)
        {
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null) return true;

            var so = new SerializedObject(vfx);

            // Renderer list out of sync with current children?
            // Catches both "user added a new mesh" and "user removed/swapped a mesh".
            var current = db.GetComponentsInChildren<Renderer>(true);
            var prop    = so.FindProperty("_targetRenderers");
            if (prop.arraySize != current.Length) return true;
            var stored = new HashSet<Object>();
            for (int i = 0; i < prop.arraySize; i++)
                stored.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var r in current)
                if (!stored.Contains(r)) return true;

            // Missing Settings?
            if (so.FindProperty("Upgrade._settings")  .objectReferenceValue == null) return true;
            if (so.FindProperty("GetHit._settings")   .objectReferenceValue == null) return true;
            if (so.FindProperty("Indicator._settings").objectReferenceValue == null) return true;
            if (so.FindProperty("Dissolve._settings") .objectReferenceValue == null) return true;

            // Darken Manager is intentionally NOT part of "Ready":
            //   - It's a scene-level GameObject — would re-trigger Needs Setup in every new scene.
            //   - It's optional infrastructure (only animates the scene-wide darken global);
            //     characters render correctly without it.
            // The Setup button still creates one when clicked, and the Darken row in the
            // inspector still shows "None" so the user can see Manager isn't present yet.

            return false;
        }

        /// <summary>
        /// Dark bar with bold title — visual divider between subsections inside a
        /// panel. Used in the Dither panel to separate Auto Camera Fade / Receive
        /// Occlusion Fade / Test, which would otherwise blur into one long form.
        /// </summary>
        static void DrawSubsectionHeader(string title)
        {
            GUILayout.Space(6);
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(20));
            EditorGUI.DrawRect(r, new Color(0.20f, 0.20f, 0.24f, 1f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), C_ACCENT);   // accent stripe on the left
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            style.normal.textColor = new Color(0.88f, 0.88f, 0.92f, 1f);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title, style);
            GUILayout.Space(4);
        }

        /// <summary>
        /// Force every editor view (including Scene view) to repaint NOW.
        ///
        /// <c>SceneView.RepaintAll()</c> only schedules a repaint for the next editor
        /// tick. While dragging a slider in Edit mode the editor may be idle between
        /// drag events — so the change isn't visible until the user mouses into the
        /// Scene view (which itself triggers a repaint). Combining RepaintAll with
        /// <c>QueuePlayerLoopUpdate</c> forces an immediate frame tick, eliminating
        /// the perceived lag on the Preview Alpha slider.
        /// </summary>
        static void ForceEditorRepaint()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        // ── Dither setup (separate from VFX setup because Dither has its own panel) ──
        static bool Dither_NeedsSetup(ZLZ_CharacterDashboard db)
        {
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null) return true;
            var so = new SerializedObject(vfx);
            if (so.FindProperty("Dither._settings").objectReferenceValue == null) return true;

            // ZLZ_OcclusionFader presence is intentionally NOT part of "Needs Setup":
            //   - It's a scene-level GameObject — would re-trigger Needs Setup in every
            //     scene the user opens a Receive-Occlusion-enabled character in.
            //   - It's optional infrastructure (only used when characters opt in to
            //     occlusion fade); characters render correctly without it.
            // The Receive Occlusion Fade section shows a "Create in Scene" button when
            // missing, so users can add it per-scene on demand.

            return false;
        }

        static void Dither_SetupAll(ZLZ_CharacterDashboard db)
        {
            // Reuses the VFX component (which owns the Dither block) — if it
            // doesn't exist yet, create it so the Settings field is reachable.
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null)
            {
                vfx = Undo.AddComponent<ZLZ_CharacterVFX>(db.gameObject);
                Debug.Log("[ZLZ] Added ZLZ_CharacterVFX (required by Dither).");
            }

            var so = new SerializedObject(vfx);
            AssignSettingsIfNull(so, "Dither._settings", VFX_GetOrCreateDitherSettings);
            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(vfx);

            // Note: ZLZ_OcclusionFader is intentionally NOT auto-created here.
            // Setup Dither configures the character only — touching the scene from
            // a character-config action would leave Manager GameObjects in every
            // scene the user merely opens. The "Create in Scene" button next to the
            // Manager field in the Receive Occlusion Fade panel adds it on demand.
        }

        static void Dither_EnsureOcclusionFader()
        {
            // Skip inside Prefab isolation — can't add scene objects there.
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return;

#if UNITY_2022_2_OR_NEWER
            var existing = Object.FindFirstObjectByType<ZLZ_OcclusionFader>(FindObjectsInactive.Include);
#else
            var existing = Object.FindObjectOfType<ZLZ_OcclusionFader>(true);
#endif
            if (existing != null) return;

            var go = new GameObject("ZLZ_OcclusionFader");
            Undo.RegisterCreatedObjectUndo(go, "Create ZLZ_OcclusionFader");
            Undo.AddComponent<ZLZ_OcclusionFader>(go);
            // Ping the new object so the user can immediately spot it in the Hierarchy
            // and assign Target Transform — same one-click feel as Setup VFX Features.
            EditorGUIUtility.PingObject(go);
            Debug.Log("[ZLZ] Created ZLZ_OcclusionFader in scene. Assign 'Target Transform' to your player to enable.");
        }

        static void VFX_RefreshRenderers(ZLZ_CharacterDashboard db)
        {
            var vfx = db.GetComponent<ZLZ_CharacterVFX>();
            if (vfx == null) return;

            var renderers = db.GetComponentsInChildren<Renderer>(true);
            var so        = new SerializedObject(vfx);
            var prop      = so.FindProperty("_targetRenderers");
            prop.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(vfx);

            // In Play mode, re-init the FX blocks so changes apply immediately.
            if (Application.isPlaying)
                vfx.RefreshRenderers();
        }

        static ZLZ_UpgradeSettings VFX_GetOrCreateUpgradeSettings()
        {
            var guids = AssetDatabase.FindAssets("t:ZLZ_UpgradeSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ZLZ_UpgradeSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(k_FXSettingsFolder))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "FX_Settings");

            var so = ScriptableObject.CreateInstance<ZLZ_UpgradeSettings>();
            AssetDatabase.CreateAsset(so, k_UpgradeSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZLZ] Created ZLZ_UpgradeSettings at {k_UpgradeSettingsPath}");
            return so;
        }

        static ZLZ_GetHitSettings VFX_GetOrCreateGetHitSettings()
        {
            var guids = AssetDatabase.FindAssets("t:ZLZ_GetHitSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ZLZ_GetHitSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(k_FXSettingsFolder))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "FX_Settings");

            var so = ScriptableObject.CreateInstance<ZLZ_GetHitSettings>();
            AssetDatabase.CreateAsset(so, k_GetHitSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZLZ] Created ZLZ_GetHitSettings at {k_GetHitSettingsPath}");
            return so;
        }

        static ZLZ_IndicatorSettings VFX_GetOrCreateIndicatorSettings()
        {
            var guids = AssetDatabase.FindAssets("t:ZLZ_IndicatorSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ZLZ_IndicatorSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(k_FXSettingsFolder))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "FX_Settings");

            var so = ScriptableObject.CreateInstance<ZLZ_IndicatorSettings>();
            AssetDatabase.CreateAsset(so, k_IndicatorSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZLZ] Created ZLZ_IndicatorSettings at {k_IndicatorSettingsPath}");
            return so;
        }

        static ZLZ_DissolveSettings VFX_GetOrCreateDissolveSettings()
        {
            var guids = AssetDatabase.FindAssets("t:ZLZ_DissolveSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ZLZ_DissolveSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(k_FXSettingsFolder))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "FX_Settings");

            var so = ScriptableObject.CreateInstance<ZLZ_DissolveSettings>();
            AssetDatabase.CreateAsset(so, k_DissolveSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZLZ] Created ZLZ_DissolveSettings at {k_DissolveSettingsPath}");
            return so;
        }

        static ZLZ_DitherSettings VFX_GetOrCreateDitherSettings()
        {
            var guids = AssetDatabase.FindAssets("t:ZLZ_DitherSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ZLZ_DitherSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!AssetDatabase.IsValidFolder(k_FXSettingsFolder))
                AssetDatabase.CreateFolder("Assets/ZLZ_AnimeShader", "FX_Settings");

            var so = ScriptableObject.CreateInstance<ZLZ_DitherSettings>();
            AssetDatabase.CreateAsset(so, k_DitherSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZLZ] Created ZLZ_DitherSettings at {k_DitherSettingsPath}");
            return so;
        }

        // ============================================================
        //  PERFORMANCE SECTION
        // ============================================================
        void DrawPerformanceSection(ZLZ_CharacterDashboard db)
        {
            Rect sectionRect = EditorGUILayout.BeginVertical();
            _performanceFoldout = DrawSectionHeader("ZLZ Shader Optimizer", _performanceFoldout, "", default);
            if (_performanceFoldout)
            {
                GUILayout.Space(8);

                // Quick stats
                var renderers  = db.GetComponentsInChildren<Renderer>(true);
                var mats       = renderers
                    .SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null)
                    .Distinct()
                    .ToArray();
                int zlzCount   = mats.Count(m => m.shader != null && m.shader.name.StartsWith("ZLZ/"));

                DrawBanner(new Color(0.14f, 0.18f, 0.28f, 1f), C_ACCENT, "◈",
                    $"{mats.Length} materials  ·  {zlzCount} ZLZ shader(s)  ·  {renderers.Length} renderer(s)", C_ACCENT);

                GUILayout.Space(6);
                EditorGUILayout.LabelField(
                    "  Open the optimizer to see per-material feature costs, texture resolutions,\n" +
                    "  formats, VRAM usage, and PC / Mobile ratings.",
                    _infoStyle);
                GUILayout.Space(8);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = C_BTN_PRIMARY;
                if (GUILayout.Button("Open Shader Optimizer  →", GUILayout.Height(28)))
                    ZLZ_ShaderOptimizerWindow.OpenForTarget(db.gameObject);
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_performanceFoldout) DrawSectionBorder(sectionRect);
        }

        // ============================================================
        //  OUTLINE SECTION  (Hull + Screen Space, unified)
        // ============================================================
        enum OutlineMode { None, Hull, ScreenSpace }

        void DrawOutlineSection(ZLZ_CharacterDashboard db)
        {
            var  hull   = OB_GetFeature();
            var  sso    = SSO_GetFeature();
            bool hullOn = hull != null && hull.isActive;
            bool ssoOn  = sso  != null && sso.isActive;
            bool bothOn = hullOn && ssoOn;

            OutlineMode current = hullOn ? OutlineMode.Hull
                                : ssoOn  ? OutlineMode.ScreenSpace
                                         : OutlineMode.None;

            string hStatus = bothOn                            ? "✔  Hull + Screen Space" :
                             current == OutlineMode.Hull        ? "✔  Hull" :
                             current == OutlineMode.ScreenSpace ? "✔  Screen Space" :
                                                                  "○  No outline";
            Color hColor = current == OutlineMode.None ? new Color(0.55f, 0.55f, 0.60f, 1f) : C_OK;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _outlineFoldout = DrawSectionHeader("ZLZ Outline", _outlineFoldout, hStatus, hColor);
            if (_outlineFoldout)
            {
                GUILayout.Space(8);

                // ── Mode selector ────────────────────────────────────────
                EditorGUI.BeginChangeCheck();
                int sel    = (int)current;
                int newSel = GUILayout.Toolbar(sel, new[] { "None", "Hull", "Screen Space" }, GUILayout.Height(26));
                if (EditorGUI.EndChangeCheck() && newSel != sel)
                    ApplyOutlineMode(db, (OutlineMode)newSel);

                GUILayout.Space(8);

                // ── Active-mode description ──────────────────────────────
                if (bothOn)
                    DrawBanner(new Color(0.14f, 0.18f, 0.28f, 1f), C_ACCENT, "◈",
                        "Hull + Screen Space both active — pick a mode above to set one exclusively", C_ACCENT);
                else if (current == OutlineMode.Hull)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK, "✔",
                        "Hull — the bold outer silhouette line. Art-directable per material (color, width, mask).", C_OK);
                else if (current == OutlineMode.ScreenSpace)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK, "✔",
                        "Screen Space — interior detail lines: creases and overlaps the hull can't draw.", C_OK);
                else
                    DrawBanner(new Color(0.20f, 0.20f, 0.24f, 1f), new Color(0.55f, 0.55f, 0.60f, 1f), "○",
                        "No outline. Pick Hull or Screen Space above.", new Color(0.65f, 0.65f, 0.70f, 1f));

                GUILayout.Space(6);

                DrawTMStatusRow("Hull Outline  (ZLZ_OutlineRendererFeature)",   hullOn, 0);
                DrawTMStatusRow("Screen Space  (ZLZ_ScreenSpaceOutlineFeature)", ssoOn,  1);

                GUILayout.Space(4);
                EditorGUILayout.LabelField(
                    "  Hull = the bold outer silhouette line.  Screen Space = interior\n" +
                    "  detail lines (creases, overlaps).  Tune each on its Renderer\n" +
                    "  Feature in the URP Renderer asset.",
                    _infoStyle);

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_outlineFoldout) DrawSectionBorder(sectionRect);
        }

        // Orchestrates the two outline renderer features into the chosen exclusive mode.
        // A feature is installed on demand the first time it is selected, then its active
        // state is toggled (disabled = the pass is never drawn = zero cost).
        static void ApplyOutlineMode(ZLZ_CharacterDashboard db, OutlineMode mode)
        {
            // Hull
            if (mode == OutlineMode.Hull)
            {
                if (OB_GetFeature() == null) OB_SetupFeature();
                SetFeatureActive(OB_GetFeature(), true);
            }
            else SetFeatureActive(OB_GetFeature(), false);

            // Screen Space
            if (mode == OutlineMode.ScreenSpace)
            {
                // The feature now ships pre-installed, so the "create it" branch is no longer the
                // only path that reaches it - the existing one still has to learn this character's
                // layer, or switching to Screen Space would just enable a feature masked to nothing.
                if (SSO_GetFeature() == null) SSO_AddRendererFeature(db);
                else MergeLayerMask(SSO_GetFeature(), "characterLayers", 1 << db.gameObject.layer);
                SetFeatureActive(SSO_GetFeature(), true);
            }
            else SetFeatureActive(SSO_GetFeature(), false);

            AssetDatabase.SaveAssets();
        }

        // ORs extra layers into a feature's LayerMask field. Used when a feature is already on the
        // renderer and another character needs to be covered by it.
        static void MergeLayerMask(ScriptableRendererFeature feature, string fieldName, int extraLayers)
        {
            if (feature == null) return;
            var so = new SerializedObject(feature);
            var p  = so.FindProperty(fieldName);
            if (p == null || (p.intValue & extraLayers) == extraLayers) return;

            p.intValue |= extraLayers;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);
        }

        // Adds a renderer feature to the active URP renderer if it is not there yet and returns it.
        // Generic because every ZLZ feature is attached the same way - only the display name and the
        // configuration afterwards differ.
        static T AddRendererFeature<T>(string displayName) where T : ScriptableRendererFeature
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) return null;

            var existing = rd.rendererFeatures.FirstOrDefault(f => f is T) as T;
            if (existing != null) return existing;

            var feature  = ScriptableObject.CreateInstance<T>();
            feature.name = displayName;
            AssetDatabase.AddObjectToAsset(feature, rd);

            var rdSo         = new SerializedObject(rd);
            var featuresProp = rdSo.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            rdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            return feature;
        }

        static void SetFeatureActive(ScriptableRendererFeature feature, bool active)
        {
            if (feature == null || feature.isActive == active) return;
            var so = new SerializedObject(feature);
            var p  = so.FindProperty("m_Active");
            if (p == null) return;
            p.boolValue = active;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);
        }

        // ============================================================
        //  SELECTION OUTLINE SECTION
        // ============================================================
        void DrawSelectionOutlineSection(ZLZ_CharacterDashboard db)
        {
            bool hasComponent = SO_HasComponent(db);
            // Present but switched off must not read as ready, or the Setup button greys out and
            // there is no way left to switch the pre-installed feature on from this panel.
            bool featureInstalled = SO_HasRendererFeature();
            bool hasFeature   = SO_IsFeatureReady();
            bool allReady     = hasComponent && hasFeature;

            // Same three-state wording as the Contact Shadow header : "disabled" when the only
            // missing piece is the pre-installed feature's checkbox, "not configured" when Setup
            // still has real work to do (controller missing or feature truly absent).
            string hStatus = allReady ? "✔  Ready"
                           : hasComponent && featureInstalled ? "⚠  Feature disabled"
                                                              : "⚠  Not configured";
            Color  hColor  = allReady ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _selectionOutlineFoldout = DrawSectionHeader("ZLZ Selection Outline", _selectionOutlineFoldout, hStatus, hColor);
            if (_selectionOutlineFoldout)
            {
                GUILayout.Space(8);

                if (allReady)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK,   "✔", "Selection Outline is configured", C_OK);
                else
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", featureInstalled && !hasFeature
                        ? "Selection Outline is installed but disabled — click Setup below"
                        : "Selection Outline is not configured — click Setup below", C_WARN);

                GUILayout.Space(6);

                DrawTMStatusRow("Selection Controller  (ZLZ_SelectionController)", hasComponent, 0);
                DrawTMStatusRow("Render Feature  (ZLZ_SelectionOutlineFeature)",    hasFeature,   1);

                if (hasComponent)
                {
                    GUILayout.Space(6);
                    var ctrl = db.GetComponent<ZLZ_SelectionController>();
                    var ctrlSo = new SerializedObject(ctrl);
                    ctrlSo.Update();
                    EditorGUILayout.PropertyField(ctrlSo.FindProperty("defaultType"),   new GUIContent("Selection Type"));
                    EditorGUILayout.PropertyField(ctrlSo.FindProperty("startSelected"), new GUIContent("Preview Outline"));
                    if (ctrlSo.ApplyModifiedProperties())
                        EditorUtility.SetDirty(ctrl);
                    if (ctrl.startSelected)
                        EditorGUILayout.HelpBox("Press Play to preview the outline.", MessageType.Info);
                }

                GUILayout.Space(8);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = C_BTN_PRIMARY;
                GUI.enabled = !allReady;
                if (GUILayout.Button("Setup Selection Outline", GUILayout.Height(26)))
                    SO_SetupAll(db);
                GUI.enabled = true;
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_selectionOutlineFoldout) DrawSectionBorder(sectionRect);
        }

        // ── SO Helpers ────────────────────────────────────────────────────
        static bool SO_HasComponent(ZLZ_CharacterDashboard db)
            => db.GetComponent<ZLZ_SelectionController>() != null;

        static bool SO_HasRendererFeature()
        {
            var rd = TM_GetActiveRendererData();
            return rd != null && rd.rendererFeatures.Any(f => f is ZLZ_SelectionOutlineFeature);
        }

        // Present AND switched on. Everything the UI gates on wants this, not mere presence.
        static bool SO_IsFeatureReady()
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) return false;
            var f = rd.rendererFeatures.FirstOrDefault(x => x is ZLZ_SelectionOutlineFeature);
            return f != null && f.isActive;
        }

        static void SO_SetupAll(ZLZ_CharacterDashboard db)
        {
            if (db.GetComponent<ZLZ_SelectionController>() == null)
            {
                Undo.AddComponent<ZLZ_SelectionController>(db.gameObject);
                Debug.Log("[ZLZ] Added ZLZ_SelectionController.");
            }

            var rd = TM_GetActiveRendererData();
            if (rd == null) { EditorUtility.DisplayDialog("ZLZ", "No active URP renderer found.", "OK"); return; }
            // The feature now ships pre-installed but switched OFF, so "already there" is no longer
            // the same as "ready" - clicking Setup is the customer asking for it, so switch it on.
            var existingSO = rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_SelectionOutlineFeature);
            if (existingSO != null)
            {
                SetFeatureActive(existingSO, true);
                AssetDatabase.SaveAssets();
                return;
            }

            var feature  = ScriptableObject.CreateInstance<ZLZ_SelectionOutlineFeature>();
            feature.name = "ZLZ Selection Outline";
            AssetDatabase.AddObjectToAsset(feature, rd);

            var rdSo         = new SerializedObject(rd);
            var featuresProp = rdSo.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            rdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZLZ] Selection Outline Renderer Feature added.");
        }

        // ============================================================
        //  CHARACTER CONTACT SHADOW SECTION
        // ============================================================
        void DrawCharacterContactShadowSection(ZLZ_CharacterDashboard db)
        {
            // Three states, not two : the feature ships pre-installed but switched OFF, so the UI has
            // to tell "missing" and "installed but disabled" apart or the customer cannot know which
            // problem they are looking at. Everything downstream gates on installed AND active.
            bool featureInstalled = CS_GetFeature() != null;
            bool hasFeature       = CS_IsFeatureReady();
            var  mats       = CS_CollectMaterials(db);
            int  totalMats  = mats.Count;
            int  enabledMats = 0;
            for (int i = 0; i < mats.Count; i++) if (mats[i].GetFloat("_UseContactShadow") > 0.5f) enabledMats++;
            bool anyMat   = enabledMats > 0;
            bool allReady = hasFeature && anyMat;

            string hStatus = allReady         ? "✔  Ready"
                           : hasFeature       ? "⚠  Enable on materials"
                           : featureInstalled ? "⚠  Feature disabled"
                                              : "⚠  Not installed";
            Color hColor = allReady ? C_OK : C_WARN;

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _contactShadowFoldout = DrawSectionHeader("ZLZ Character Contact Shadow", _contactShadowFoldout, hStatus, hColor);
            if (_contactShadowFoldout)
            {
                GUILayout.Space(8);

                if (allReady)
                    DrawBanner(new Color(0.12f, 0.32f, 0.18f, 1f), C_OK,   "✔", "Character Contact Shadow is active", C_OK);
                else if (!hasFeature)
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", featureInstalled
                        ? "Renderer Feature is installed but disabled — click Enable below"
                        : "Renderer Feature not installed in URP — click Add below", C_WARN);
                else
                    DrawBanner(new Color(0.38f, 0.22f, 0.04f, 1f), C_WARN, "⚠", "Enable 'Contact Self-Shadow' on the character materials", C_WARN);

                GUILayout.Space(6);

                DrawTMStatusRow("Render Feature  (ZLZ_CharacterContactShadowFeature)", hasFeature, 0);

                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Contact Self-Shadow   ({enabledMats} / {totalMats})", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(totalMats == 0))
                    {
                        if (GUILayout.Button("All",  GUILayout.Width(46))) foreach (var m in mats) CS_SetMaterial(m, true);
                        if (GUILayout.Button("None", GUILayout.Width(46))) foreach (var m in mats) CS_SetMaterial(m, false);
                    }
                }

                if (totalMats == 0)
                {
                    EditorGUILayout.HelpBox("No ZLZ character materials found under this root.", MessageType.None);
                }
                else
                {
                    // Per-material toggle — edits the material asset directly, so it stays in sync
                    // with the same toggle in the Material Inspector (Base Character Lighting).
                    for (int i = 0; i < mats.Count; i++)
                    {
                        CS_DrawMaterialRow(mats[i], i);
                        GUILayout.Space(2);
                    }
                }

                GUILayout.Space(8);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = C_BTN_PRIMARY;
                GUI.enabled = !hasFeature;
                if (GUILayout.Button(featureInstalled ? "Enable Renderer Feature" : "Add Renderer Feature", GUILayout.Height(26)))
                    CS_AddRendererFeature(db);
                GUI.enabled = true;
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_contactShadowFoldout) DrawSectionBorder(sectionRect);
        }

        // ── CS Helpers ────────────────────────────────────────────────────
        static ZLZ_CharacterContactShadowFeature CS_GetFeature()
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) return null;
            return rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_CharacterContactShadowFeature) as ZLZ_CharacterContactShadowFeature;
        }

        // Present AND switched on. Everything the UI gates on wants this, not mere presence.
        static bool CS_IsFeatureReady()
        {
            var f = CS_GetFeature();
            return f != null && f.isActive;
        }

        static bool CS_HasRendererFeature()
        {
            var rd = TM_GetActiveRendererData();
            return rd != null && rd.rendererFeatures.Any(f => f is ZLZ_CharacterContactShadowFeature);
        }

        static bool CS_AnyMaterialEnabled(ZLZ_CharacterDashboard db)
            => CS_CollectMaterials(db).Any(m => m.GetFloat("_UseContactShadow") > 0.5f);

        // Deduped list of ZLZ character materials under the root that expose Contact Self-Shadow.
        static List<Material> CS_CollectMaterials(ZLZ_CharacterDashboard db)
        {
            var list = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (var r in db.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || !seen.Add(m)) continue;
                    if (m.shader.name != "ZLZ/AnimeToon/Character") continue;
                    if (!m.HasProperty("_UseContactShadow")) continue;
                    list.Add(m);
                }
            }
            return list;
        }

        // One material row: striped background, checkbox + name (left), bold ON/OFF state (right).
        void CS_DrawMaterialRow(Material m, int rowIndex)
        {
            bool on = m.GetFloat("_UseContactShadow") > 0.5f;

            Rect row = GUILayoutUtility.GetRect(0, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, rowIndex % 2 == 0 ? new Color(0.16f, 0.16f, 0.19f, 1f)
                                                      : new Color(0.18f, 0.18f, 0.21f, 1f));

            // Checkbox + material name (left, padded; leaves room on the right for the ON/OFF text).
            Rect toggleRect = new Rect(row.x + 8, row.y + 3, row.width - 78, 18);
            bool newOn = EditorGUI.ToggleLeft(toggleRect, new GUIContent("  " + m.name), on);
            if (newOn != on) CS_SetMaterial(m, newOn);

            // ON / OFF state (right, bold, green when on / grey when off).
            var stateStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            stateStyle.normal.textColor = on ? C_OK : new Color(0.55f, 0.55f, 0.60f, 1f);
            EditorGUI.LabelField(new Rect(row.x, row.y, row.width - 12, row.height), on ? "ON" : "OFF", stateStyle);
        }

        // Sets the per-material toggle: float AND keyword together. A raw SetFloat alone would NOT
        // toggle _ZLZ_CONTACTSHADOW_ON, so the shader branch wouldn't compile in and nothing shows.
        static void CS_SetMaterial(Material m, bool on)
        {
            if (m == null) return;
            Undo.RecordObject(m, "Toggle Contact Self-Shadow");
            m.SetFloat("_UseContactShadow", on ? 1f : 0f);
            if (on) m.EnableKeyword("_ZLZ_CONTACTSHADOW_ON");
            else    m.DisableKeyword("_ZLZ_CONTACTSHADOW_ON");
            EditorUtility.SetDirty(m);
        }

        static void CS_AddRendererFeature(ZLZ_CharacterDashboard db)
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) { EditorUtility.DisplayDialog("ZLZ", "No active URP renderer found.", "OK"); return; }
            // Already there : widen its mask to cover this character too rather than bailing out.
            // Returning early meant a second character on another layer - or any character set up
            // after the feature was pre-installed - was silently left out of the capture.
            var existingCS = rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_CharacterContactShadowFeature);
            if (existingCS != null)
            {
                MergeLayerMask(existingCS, "casterLayers", 1 << db.gameObject.layer);
                SetFeatureActive(existingCS, true);   // pre-installed copies ship switched off
                AssetDatabase.SaveAssets();
                return;
            }

            var feature = ScriptableObject.CreateInstance<ZLZ_CharacterContactShadowFeature>();
            feature.name = "ZLZ Character Contact Shadow";
            feature.casterLayers = 1 << db.gameObject.layer;   // capture only this character's layer
            AssetDatabase.AddObjectToAsset(feature, rd);

            var rdSo         = new SerializedObject(rd);
            var featuresProp = rdSo.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            rdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZLZ] Character Contact Shadow Renderer Feature added.");
        }

        // ── SSO Helpers ───────────────────────────────────────────────────
        static bool SSO_HasRendererFeature()
        {
            var rd = TM_GetActiveRendererData();
            return rd != null && rd.rendererFeatures.Any(f => f is ZLZ_ScreenSpaceOutlineFeature);
        }

        static ZLZ_ScreenSpaceOutlineFeature SSO_GetFeature()
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) return null;
            return rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_ScreenSpaceOutlineFeature) as ZLZ_ScreenSpaceOutlineFeature;
        }

        static void SSO_AddRendererFeature(ZLZ_CharacterDashboard db)
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) { EditorUtility.DisplayDialog("ZLZ", "No active URP renderer found.", "OK"); return; }
            // Already there : widen its mask instead of bailing out. This one mattered most - the
            // feature ships pre-installed and disabled, so an early return would have left its
            // layer mask empty and the Screen Space outline mode would have drawn nothing at all.
            var existingSSO = rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_ScreenSpaceOutlineFeature);
            if (existingSSO != null)
            {
                MergeLayerMask(existingSSO, "characterLayers", 1 << db.gameObject.layer);
                AssetDatabase.SaveAssets();
                return;
            }

            var feature  = ScriptableObject.CreateInstance<ZLZ_ScreenSpaceOutlineFeature>();
            feature.name = "ZLZ Screen Space Outline";
            feature.characterLayers = 1 << db.gameObject.layer;   // outline only this character's layer
            AssetDatabase.AddObjectToAsset(feature, rd);

            var rdSo         = new SerializedObject(rd);
            var featuresProp = rdSo.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            rdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZLZ] Screen Space Outline Renderer Feature added.");
        }

        // ── OB Helpers ────────────────────────────────────────────────────
        static bool OB_HasRendererFeature()
        {
            var rd = TM_GetActiveRendererData();
            return rd != null && rd.rendererFeatures.Any(f => f is ZLZ_OutlineRendererFeature);
        }

        static ZLZ_OutlineRendererFeature OB_GetFeature()
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) return null;
            return rd.rendererFeatures.FirstOrDefault(f => f is ZLZ_OutlineRendererFeature) as ZLZ_OutlineRendererFeature;
        }

        static void OB_SetupFeature()
        {
            var rd = TM_GetActiveRendererData();
            if (rd == null) { EditorUtility.DisplayDialog("ZLZ", "No active URP renderer found.", "OK"); return; }
            if (rd.rendererFeatures.Any(f => f is ZLZ_OutlineRendererFeature)) return;

            var feature  = ScriptableObject.CreateInstance<ZLZ_OutlineRendererFeature>();
            feature.name = "ZLZ Hull Outline";
            AssetDatabase.AddObjectToAsset(feature, rd);

            var rdSo         = new SerializedObject(rd);
            var featuresProp = rdSo.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            rdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZLZ] Outline Batch Renderer Feature added.");
        }

        // ── TM Helpers ────────────────────────────────────────────────────
        static UniversalRendererData TM_GetActiveRendererData()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return null;
            var so = new SerializedObject(urpAsset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return null;
            int idx = so.FindProperty("m_DefaultRendererIndex")?.intValue ?? 0;
            return list.GetArrayElementAtIndex(idx).objectReferenceValue as UniversalRendererData;
        }

        static bool TM_HasRendererFeature()
        {
            var rd = TM_GetActiveRendererData();
            return rd != null && rd.rendererFeatures.Any(f => f is ZLZ_AnimeToneMappingFeature);
        }

        static bool TM_IsColorGradingHDR()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return false;
            var so = new SerializedObject(urpAsset);
            var prop = so.FindProperty("m_ColorGradingMode");
            return prop != null && prop.intValue == (int)ColorGradingMode.HighDynamicRange;
        }

        static void TM_SetupAll()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) { EditorUtility.DisplayDialog("ZLZ", "No active URP asset found.", "OK"); return; }

            // ── Add Render Feature ────────────────────────────────────
            var rd = TM_GetActiveRendererData();
            if (rd != null && !rd.rendererFeatures.Any(f => f is ZLZ_AnimeToneMappingFeature))
            {
                var feature = ScriptableObject.CreateInstance<ZLZ_AnimeToneMappingFeature>();
                feature.name = "ZLZ_AnimeToneMappingFeature";
                AssetDatabase.AddObjectToAsset(feature, rd);

                var rdSo = new SerializedObject(rd);
                var featuresProp = rdSo.FindProperty("m_RendererFeatures");
                featuresProp.arraySize++;
                featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
                rdSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rd);
            }

            // ── Set HDR Color Grading ─────────────────────────────────
            var urpSo = new SerializedObject(urpAsset);
            var colorProp = urpSo.FindProperty("m_ColorGradingMode");
            if (colorProp != null)
            {
                colorProp.intValue = (int)ColorGradingMode.HighDynamicRange;
                urpSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(urpAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZLZ] Tone Mapping setup complete — Render Feature added, Color Grading set to HDR");
        }

        // ============================================================
        //  SMOOTH NORMAL SECTION
        // ============================================================
        void DrawSmoothNormalSection(ZLZ_CharacterDashboard db)
        {
            var  infos     = CollectMeshBakeInfo(db.gameObject);
            int  total     = infos.Count;
            int  baked     = infos.Count(m => m.isBaked);
            bool allBaked  = total > 0 && baked == total;
            bool noneBaked = baked == 0;

            string hStatus = total == 0 ? "" :
                             allBaked   ? $"✔  {baked}/{total} baked" :
                             noneBaked  ? "⚠  Not baked" :
                                         $"◑  {baked}/{total} baked";

            Rect sectionRect = EditorGUILayout.BeginVertical();
            _smoothNormalFoldout = DrawSectionHeader("ZLZ Smooth Normal Bake", _smoothNormalFoldout, hStatus, allBaked ? C_OK : C_WARN);
            if (_smoothNormalFoldout)
            {
                GUILayout.Space(8);

                if      (allBaked)  DrawBanner(new Color(0.12f,0.32f,0.18f,1f), C_OK,   "✔", $"All meshes baked  ({baked}/{total})", C_OK);
                else if (noneBaked) DrawBanner(new Color(0.38f,0.22f,0.04f,1f), C_WARN, "⚠", $"Not baked — Bake for best outline quality  (0/{total})", C_WARN);
                else                DrawBanner(new Color(0.35f,0.26f,0.04f,1f), C_WARN, "◑", $"Partially baked  ({baked}/{total} meshes)", C_WARN);

                GUILayout.Space(6);

                if (total > 0) { DrawMeshList(infos); GUILayout.Space(8); }
                else { EditorGUILayout.LabelField("  No meshes found under this GameObject.", _subtleStyle); GUILayout.Space(8); }

                DrawBakeButtons(db, total);
                GUILayout.Space(8);
            }
            EditorGUILayout.EndVertical();
            if (_smoothNormalFoldout) DrawSectionBorder(sectionRect);
        }

        void DrawMeshList(List<MeshBakeInfo> infos)
        {
            Rect hr = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(hr, new Color(0.20f,0.20f,0.24f,1f));
            EditorGUI.LabelField(new Rect(hr.x+6, hr.y, 200, hr.height), "Mesh", _subtleStyle);
            EditorGUI.LabelField(new Rect(hr.x+hr.width-70, hr.y, 64, hr.height), "Status", _subtleStyle);

            for (int i = 0; i < infos.Count; i++)
            {
                Rect row = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row, i%2==0 ? new Color(0.16f,0.16f,0.19f,1f) : new Color(0.18f,0.18f,0.21f,1f));
                EditorGUI.LabelField(new Rect(row.x+6, row.y, row.width-80, row.height), infos[i].mesh.name, _rowStyle);
                var bs = new GUIStyle(_rowStyle) { alignment = TextAnchor.MiddleRight };
                bs.normal.textColor = infos[i].isBaked ? C_OK : new Color(0.50f,0.50f,0.55f,1f);
                EditorGUI.LabelField(new Rect(row.x, row.y, row.width-8, row.height), infos[i].isBaked ? "BAKED" : "—", bs);
            }
        }

        void DrawBakeButtons(ZLZ_CharacterDashboard db, int total)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2);
            Color prev = GUI.backgroundColor;

            GUI.backgroundColor = C_BTN_PRIMARY;
            if (GUILayout.Button("Bake All", GUILayout.Height(26))) ExecuteBakeAll(db.gameObject);

            if (total > 0)
            {
                GUI.backgroundColor = C_BTN_DANGER;
                if (GUILayout.Button("Reset All", GUILayout.Width(80), GUILayout.Height(26)))
                    if (EditorUtility.DisplayDialog("Reset Smooth Normals",
                        $"Clear UV{ZLZ_SmoothNormalBaker.UV_CHANNEL} on all {total} mesh(es) under '{db.gameObject.name}'?",
                        "Reset", "Cancel"))
                        ExecuteResetAll(db.gameObject);
            }

            GUI.backgroundColor = prev;
            GUILayout.Space(2);
            EditorGUILayout.EndHorizontal();
        }

        // ── Bake / Reset ─────────────────────────────────────────────────
        static void ExecuteBakeAll(GameObject root)
        {
            var meshes = GetAllMeshes(root);
            if (meshes.Count == 0) { EditorUtility.DisplayDialog("ZLZ", "No meshes found.", "OK"); return; }
            int done = 0;
            try   { foreach (var m in meshes) { EditorUtility.DisplayProgressBar("ZLZ — Baking", m.name, (float)done/meshes.Count); ZLZ_SmoothNormalBaker.BakeMesh(m); done++; } }
            finally { EditorUtility.ClearProgressBar(); }
            ZLZ_SmoothNormalBaker.SyncMaterialFlag(root, true);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[ZLZ] BakeAll — {done} mesh(es) under '{root.name}'");
        }

        static void ExecuteResetAll(GameObject root)
        {
            var meshes = GetAllMeshes(root);
            int done = 0;
            try   { foreach (var m in meshes) { EditorUtility.DisplayProgressBar("ZLZ — Resetting", m.name, (float)done/meshes.Count); ZLZ_SmoothNormalBaker.ResetMesh(m); done++; } }
            finally { EditorUtility.ClearProgressBar(); }
            ZLZ_SmoothNormalBaker.SyncMaterialFlag(root, false);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[ZLZ] ResetAll — {done} mesh(es) under '{root.name}'");
        }

        // ── Mesh discovery ───────────────────────────────────────────────
        struct MeshBakeInfo { public Mesh mesh; public bool isBaked; }

        static Mesh GetMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (r.TryGetComponent<MeshFilter>(out var mf)) return mf.sharedMesh;
            return null;
        }

        static List<MeshBakeInfo> CollectMeshBakeInfo(GameObject root)
        {
            var seen = new HashSet<int>(); var result = new List<MeshBakeInfo>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            { Mesh m = GetMesh(r); if (m != null && seen.Add(m.GetInstanceID())) result.Add(new MeshBakeInfo { mesh=m, isBaked=ZLZ_SmoothNormalBaker.IsMeshBaked(m) }); }
            return result;
        }

        static List<Mesh> GetAllMeshes(GameObject root)
        {
            var seen = new HashSet<int>(); var result = new List<Mesh>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            { Mesh m = GetMesh(r); if (m != null && seen.Add(m.GetInstanceID())) result.Add(m); }
            return result;
        }

        // ── Setup MenuItem ───────────────────────────────────────────────
        // Zero-click install (mirrors the Env package) : the two global pieces every setup needs -
        // the Tone Mapping feature (+ HDR grading) and the Hull Outline feature - are installed on
        // the active URP renderer. Character-specific steps (head bone, smooth normals, per-character
        // features) still run from Setup Character Dashboard.
        //
        // Two triggers :
        //   1. First editor load after the package's scripts first compile. Respects the once-flag.
        //      Mostly a safety net for "imported before the project had a URP renderer" : retries
        //      every load until a renderer exists, then never runs again.
        //   2. ZLZ_PackageImportSentinel below : fires when THIS script file itself lands in the
        //      project, i.e. exactly when our package is imported or updated. That is the customer
        //      explicitly asking for a working setup, so it bypasses the once-flag (covers delete +
        //      reinstall and version upgrades). It keys on the file's presence in the import batch -
        //      never on the .unitypackage name, which the Asset Store derives from the storefront
        //      title and we cannot rely on. Other publishers' packages never contain this file, so
        //      they can never restore a feature the customer deleted on purpose.
        // When a release adds a feature to the roster, bump the flag version : one deliberate,
        // author-controlled catch-up pass per project.
        [InitializeOnLoadMethod]
        static void AutoInstallOnImport()
        {
            EditorApplication.delayCall += () => AutoInstall(false);
        }

        internal static void AutoInstall(bool ownPackageImport)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // VERSIONED once-flag : bump the number whenever a new feature joins the roster below and
            // every existing project runs one catch-up pass on its next editor load. A plain flag can
            // never deliver a feature added in an update to a project installed before it - and it
            // lives in EditorPrefs (per machine, not per project), so a stale one is unreachable
            // from the UI. That is what the manual menu item below is for.
            string doneKey = "ZLZ_CharAutoInstall::v4::" + Application.dataPath;
            if (!ownPackageImport && EditorPrefs.GetBool(doneKey, false)) return;

            var rd = TM_GetActiveRendererData();
            if (rd == null)
            {
                // Silence here is what makes a failed install impossible to diagnose. The flag stays
                // unset, so this retries on the next editor load or package import.
                Debug.LogWarning("[ZLZ] Anime Shader : no active URP renderer found, so the Renderer Features were not installed. " +
                                 "Assign a URP asset in Project Settings > Graphics, then run Window > ZLZ > Install Renderer Features.");
                return;
            }

            if (!TM_HasRendererFeature()  || !OB_HasRendererFeature()
             || !CS_HasRendererFeature()  || !SSO_HasRendererFeature()
             || !SO_HasRendererFeature())
            {
                InstallRendererFeatures();
                Debug.Log("[ZLZ] Anime Shader setup : missing Renderer Features installed.");
            }

            EditorPrefs.SetBool(doneKey, true);
        }

        // The full Renderer Feature roster. Tone Mapping and Hull Outline go in switched ON because
        // every ZLZ character needs them. Contact Shadow and Screen Space Outline go in switched OFF :
        // they cost nothing until ticked, but the customer can now SEE that the package provides them
        // instead of having to know they exist and hunt for the Add Renderer Feature button.
        static void InstallRendererFeatures()
        {
            TM_SetupAll();
            OB_SetupFeature();

            var cs = AddRendererFeature<ZLZ_CharacterContactShadowFeature>("ZLZ Character Contact Shadow");
            if (cs != null) SetFeatureActive(cs, false);

            var sso = AddRendererFeature<ZLZ_ScreenSpaceOutlineFeature>("ZLZ Screen Space Outline");
            if (sso != null)
            {
                SetFeatureActive(sso, false);
                // characterLayers defaults to Nothing, which would make the feature draw nothing at
                // all if someone just ticks it by hand. Default is the layer a fresh character sits
                // on ; Setup Character Dashboard ORs in the real layer when it runs.
                MergeLayerMask(sso, "characterLayers", 1 << 0);
            }

            // Selection Outline enqueues three passes whenever it is active, so it stays off until
            // Setup Selection Outline switches it on for a character that actually needs it.
            var so = AddRendererFeature<ZLZ_SelectionOutlineFeature>("ZLZ Selection Outline");
            if (so != null) SetFeatureActive(so, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Manual fallback. Auto-install can legitimately do nothing (no URP renderer yet, a stale
        // once-flag from an earlier install on this machine), and without this the customer has no
        // way to trigger it.
        [MenuItem("Window/ZLZ/Install Renderer Features")]
        static void MenuInstallRendererFeatures()
        {
            if (TM_GetActiveRendererData() == null)
            {
                EditorUtility.DisplayDialog("ZLZ",
                    "No active URP renderer found.\n\nAssign a URP asset in Project Settings > Graphics, then run this again.",
                    "OK");
                return;
            }

            InstallRendererFeatures();
            EditorPrefs.SetBool("ZLZ_CharAutoInstall::v4::" + Application.dataPath, true);
            Debug.Log("[ZLZ] Anime Shader Renderer Features are installed.");
        }

        [MenuItem("GameObject/ZLZ/Setup Character Dashboard", false, 49)]
        static void MenuSetupDashboard(MenuCommand cmd)
        {
            var go = (cmd.context as GameObject) ?? Selection.activeGameObject;
            if (go == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup ZLZ Character Dashboard");

            // 1. Add Dashboard — [RequireComponent] auto-adds Binder first,
            //    then move Dashboard above it so it appears first in the Inspector.
            var db = go.GetComponent<ZLZ_CharacterDashboard>();
            if (db == null)
            {
                db = Undo.AddComponent<ZLZ_CharacterDashboard>(go);
                UnityEditorInternal.ComponentUtility.MoveComponentUp(db);
            }

            // 2. Auto Detect Head Bone (Humanoid only)
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    db.headBone = headBone;
                    EditorUtility.SetDirty(db);

                    // 3. Auto Detect Axes
                    var binder = go.GetComponent<ZLZ_HeadDirectionBinder>();
                    if (binder != null)
                    {
                        binder.headBone = headBone;
                        binder.AutoDetectAxes();
                        EditorUtility.SetDirty(binder);
                    }
                }
            }

            // 4. Setup Tone Mapping (Renderer Feature + HDR)
            TM_SetupAll();

            // 5. Setup Outline — default to Hull (portable; switch to Screen Space in the Dashboard)
            ApplyOutlineMode(db, OutlineMode.Hull);

            // 6. Setup Selection Outline (Component + Renderer Feature)
            SO_SetupAll(db);

            // 7. Setup VFX Features (Upgrade + GetHit Controllers)
            VFX_SetupAll(db);

            // 9. Setup Dither (own panel — separate from VFX Features)
            Dither_SetupAll(db);

            // 10. Bake Smooth Normals
            ExecuteBakeAll(go);

            Selection.activeGameObject = go;
            Debug.Log($"[ZLZ] Full setup complete on '{go.name}'");
        }

        [MenuItem("GameObject/ZLZ/Setup Character Dashboard", true)]
        static bool MenuSetupDashboard_Validate() => Selection.activeGameObject != null;
    }

    // Detects "our package was just imported / updated" without ever guessing the .unitypackage
    // name : the one thing every import of this package is guaranteed to contain is this very
    // script. When it appears in an import batch, run the auto-install with the once-flag
    // bypassed - a fresh import IS the customer asking for a working setup. Manually reimporting
    // the file/folder triggers this too, which reads as the same request.
    // (Postprocessor callbacks run after the imported scripts compile, so this also fires on the
    // very first import, where it simply races the delayCall trigger - both are idempotent.)
    class ZLZ_PackageImportSentinel : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.EndsWith("ZLZ_CharacterDashboard.cs", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!path.Replace('\\', '/').StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)) continue;

                // Defer : OnPostprocessAllAssets runs mid-import, when writing renderer assets is unsafe.
                EditorApplication.delayCall += () => ZLZ_CharacterDashboardEditor.AutoInstall(true);
                return;
            }
        }
    }
#endif
}
