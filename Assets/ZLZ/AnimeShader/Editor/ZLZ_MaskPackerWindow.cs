#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZLZ.AnimeShader.Editor
{
    // One-click tool that takes individual feature mask textures and packs them into
    // 1-2 RGBA textures (Feature Mask 1 / 2). Auto-assigns to the target material and
    // writes channel mapping properties + USE_MASKTEX keywords so the shader strips
    // any unused texture sample at build time.
    public class ZLZ_MaskPackerWindow : EditorWindow
    {
        const string SHADER_NAME = "ZLZ/AnimeToon/Character";

        const string PROP_MASK1 = "_RGBA_Masking";
        const string PROP_MASK2 = "_RGBA_Masking2";

        static readonly string[] FEATURE_NAMES    = { "Metallic", "Hair Highlight", "Emissive", "Outline", "Specular" };
        static readonly string[] FEATURE_KEYWORDS = { "_USEMETALLIC_ON", "_HAIR_HIGHLIGHT_ON", "_EMISSIVEGLOW_ON", "_OUTLINEMASK_ON", "_SPECULAR_ON" };
        static readonly string[] CHANNEL_PROPS    = { "_MetallicMaskCh", "_HairHighlightMaskCh", "_EmissiveMaskCh", "_OutlineMaskCh", "_SpecularMaskCh" };
        static readonly int[]    DEFAULT_CHANNELS = { 0, 1, 2, 3, 4 }; // M1.R / M1.G / M1.B / M1.A / M2.R
        // idx 0..7 = M1/M2 RGBA, idx 8 = no mask (whole mesh — checkbox unchecked)
        static readonly string[] CHANNEL_LABELS_DROPDOWN = { "Mask 1 . R", "Mask 1 . G", "Mask 1 . B", "Mask 1 . A", "Mask 2 . R", "Mask 2 . G", "Mask 2 . B", "Mask 2 . A" };
        // Used for collision error message
        static readonly string[] CHANNEL_LABELS = { "Mask 1 . R", "Mask 1 . G", "Mask 1 . B", "Mask 1 . A", "Mask 2 . R", "Mask 2 . G", "Mask 2 . B", "Mask 2 . A", "Whole Mesh" };
        const int CHANNEL_NONE = 8;

        Material _target;
        Texture2D[] _inputs = new Texture2D[5];
        int[] _channels = new int[5];
        int _resolution = 2048;
        bool _initialized;

        static readonly int[] RES_OPTIONS = { 256, 512, 1024, 2048, 4096 };
        static readonly string[] RES_LABELS = { "256", "512", "1024", "2048", "4096" };

        [MenuItem("Window/ZLZ/Mask Packer")]
        public static void ShowWindow()
        {
            // utility:false → regular dockable EditorWindow. Required because users need
            // to drag textures from the Project view into the slots; a utility window
            // floats on top and (in some Unity versions) closes when it loses focus,
            // which makes drag-and-drop from Project unreliable.
            var w = GetWindow<ZLZ_MaskPackerWindow>(false, "ZLZ Mask Packer", true);
            w.minSize = new Vector2(420, 520);
        }

        public static void ShowForMaterial(Material mat)
        {
            var w = GetWindow<ZLZ_MaskPackerWindow>(false, "ZLZ Mask Packer", true);
            w.minSize = new Vector2(420, 520);
            w._target = mat;
            w._initialized = false;
        }

        void OnEnable()
        {
            for (int i = 0; i < 5; i++) _channels[i] = DEFAULT_CHANNELS[i];
        }

        void InitFromMaterial()
        {
            if (_target == null) return;
            for (int i = 0; i < 5; i++)
            {
                if (_target.HasProperty(CHANNEL_PROPS[i]))
                    _channels[i] = Mathf.Clamp((int)_target.GetFloat(CHANNEL_PROPS[i]), 0, 8);
            }
            _initialized = true;
        }

        void OnGUI()
        {
            if (!_initialized && _target != null) InitFromMaterial();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pack individual feature masks into 1-2 RGBA textures.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            _target = (Material)EditorGUILayout.ObjectField("Target Material", _target, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                _initialized = false;
                if (_target != null && _target.shader != null && _target.shader.name != SHADER_NAME)
                    EditorUtility.DisplayDialog("Wrong Shader",
                        "Target material must use '" + SHADER_NAME + "' shader.", "OK");
            }

            if (_target == null)
            {
                EditorGUILayout.HelpBox("Assign a material that uses the ZLZ Anime Toon shader.", MessageType.Info);
                return;
            }

            // Input slots — only show features currently enabled on the target material
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Input Masks", EditorStyles.boldLabel);

            int enabledCount = 0;
            for (int i = 0; i < 5; i++)
            {
                if (!IsFeatureEnabled(_target, i)) continue;
                enabledCount++;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(FEATURE_NAMES[i], GUILayout.Width(110));

                    bool useMask = (_channels[i] < CHANNEL_NONE);
                    bool newUseMask = EditorGUILayout.ToggleLeft("Use Mask", useMask, GUILayout.Width(90));
                    if (newUseMask != useMask)
                    {
                        _channels[i] = newUseMask ? Mathf.Clamp(DEFAULT_CHANNELS[i], 0, 7) : CHANNEL_NONE;
                        useMask = newUseMask;
                        if (!useMask) _inputs[i] = null; // clear input when switching to whole-mesh mode
                    }

                    if (useMask)
                    {
                        _inputs[i] = (Texture2D)EditorGUILayout.ObjectField(_inputs[i], typeof(Texture2D), false);
                        _channels[i] = EditorGUILayout.Popup(_channels[i], CHANNEL_LABELS_DROPDOWN, GUILayout.Width(110));
                    }
                    else
                    {
                        GUILayout.Label("→ applies to whole mesh", EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                    }
                }
            }

            if (enabledCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No mask-using features are enabled on this material.\n" +
                    "Enable Metallic / Hair Highlight / Emissive / Outline / Specular in the Material Inspector first.",
                    MessageType.Info);
                return;
            }

            // Collision detection
            string collision = DetectChannelCollision();
            if (collision != null)
                EditorGUILayout.HelpBox(collision, MessageType.Warning);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            int resIdx = System.Array.IndexOf(RES_OPTIONS, _resolution);
            if (resIdx < 0) resIdx = 3;
            resIdx = EditorGUILayout.Popup("Resolution", resIdx, RES_LABELS);
            _resolution = RES_OPTIONS[resIdx];

            EditorGUILayout.LabelField("Output Folder", GetOutputFolder());

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(collision != null || _target == null))
            {
                if (GUILayout.Button("Bake & Apply to Material", GUILayout.Height(28)))
                    Bake();
            }
        }

        static bool IsFeatureEnabled(Material mat, int featureIdx)
        {
            if (mat == null) return false;
            return mat.IsKeywordEnabled(FEATURE_KEYWORDS[featureIdx]);
        }

        string DetectChannelCollision()
        {
            for (int i = 0; i < 5; i++)
            {
                if (!IsFeatureEnabled(_target, i)) continue;
                if (_channels[i] == CHANNEL_NONE) continue;
                if (_inputs[i] == null) continue;
                for (int j = i + 1; j < 5; j++)
                {
                    if (!IsFeatureEnabled(_target, j)) continue;
                    if (_channels[j] == CHANNEL_NONE) continue;
                    if (_inputs[j] == null) continue;
                    if (_channels[i] == _channels[j])
                        return string.Format("'{0}' and '{1}' both target {2} — change one.",
                            FEATURE_NAMES[i], FEATURE_NAMES[j], CHANNEL_LABELS[_channels[i]]);
                }
            }
            return null;
        }

        string GetOutputFolder()
        {
            if (_target == null) return "Assets";
            string path = AssetDatabase.GetAssetPath(_target);
            if (string.IsNullOrEmpty(path)) return "Assets";
            return Path.GetDirectoryName(path).Replace('\\', '/');
        }

        void Bake()
        {
            if (_target == null) return;

            string folder = GetOutputFolder();
            string baseName = _target.name;

            bool needM1 = AnyInputInBank(0);
            bool needM2 = AnyInputInBank(1);

            if (!needM1 && !needM2)
            {
                EditorUtility.DisplayDialog("Mask Packer",
                    "Nothing to bake — assign at least one input texture (features set to 'None' have no input slot).",
                    "OK");
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                string m1Path = null;
                string m2Path = null;

                if (needM1)
                    m1Path = BakeBank(folder, baseName + "_Mask1.png", bank: 0);
                if (needM2)
                    m2Path = BakeBank(folder, baseName + "_Mask2.png", bank: 1);

                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();

                Undo.RecordObject(_target, "Bake Masks");

                if (needM1 && m1Path != null)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(m1Path);
                    if (_target.HasProperty(PROP_MASK1)) _target.SetTexture(PROP_MASK1, t);
                }
                if (needM2 && m2Path != null)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(m2Path);
                    if (_target.HasProperty(PROP_MASK2)) _target.SetTexture(PROP_MASK2, t);
                }

                // Apply channel mapping to material (only for enabled features)
                for (int i = 0; i < 5; i++)
                {
                    if (!IsFeatureEnabled(_target, i)) continue;
                    if (_target.HasProperty(CHANNEL_PROPS[i]))
                        _target.SetFloat(CHANNEL_PROPS[i], _channels[i]);
                }

                ZLZAnimeToonGUI.RefreshMaskTextureKeywords(_target);
                EditorUtility.SetDirty(_target);

                EditorUtility.DisplayDialog("Mask Packer",
                    "Baked " + (needM1 ? "Mask 1" : "") + (needM1 && needM2 ? " + " : "") + (needM2 ? "Mask 2" : "") + " for '" + baseName + "'.",
                    "OK");
            }
            catch (System.Exception ex)
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.DisplayDialog("Mask Packer — Error", ex.Message, "OK");
                Debug.LogException(ex);
            }
        }

        bool AnyInputInBank(int bank)
        {
            int lo = bank * 4, hi = lo + 3;
            for (int i = 0; i < 5; i++)
            {
                if (!IsFeatureEnabled(_target, i)) continue;
                if (_inputs[i] == null) continue;
                int ch = _channels[i];
                if (ch >= CHANNEL_NONE) continue;
                if (ch >= lo && ch <= hi) return true;
            }
            return false;
        }

        // Bake a single RGBA texture for the given mask bank. Returns asset path.
        string BakeBank(string folder, string fileName, int bank)
        {
            int lo = bank * 4;
            int size = _resolution;

            // Default: black RGBA so unused channels read as 0
            Color[] pixels = new Color[size * size];
            for (int p = 0; p < pixels.Length; p++) pixels[p] = new Color(0, 0, 0, 0);

            // Pull each input texture into its assigned channel (enabled features only)
            for (int i = 0; i < 5; i++)
            {
                if (!IsFeatureEnabled(_target, i)) continue;
                if (_inputs[i] == null) continue;
                int ch = _channels[i];
                if (ch >= CHANNEL_NONE) continue;
                if (ch < lo || ch > lo + 3) continue;
                int local = ch - lo; // 0=R, 1=G, 2=B, 3=A

                Color[] src = ReadTexturePixels(_inputs[i], size, size);
                for (int p = 0; p < pixels.Length; p++)
                {
                    float v = src[p].r; // monochrome mask: read red channel of source
                    switch (local)
                    {
                        case 0: pixels[p].r = v; break;
                        case 1: pixels[p].g = v; break;
                        case 2: pixels[p].b = v; break;
                        case 3: pixels[p].a = v; break;
                    }
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply();

            string path = folder + "/" + fileName;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(path);

            return path;
        }

        // Read a texture's pixels via Blit so source can be compressed / non-readable.
        static Color[] ReadTexturePixels(Texture2D src, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tmp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tmp.Apply();
            var px = tmp.GetPixels();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(tmp);

            return px;
        }

        static void ConfigureMaskImporter(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
    }
}
#endif
