#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MK.TextureChannelPacker
{
    public class MKTextureChannelPacker : EditorWindow
    {
        private const string Version = "1.9";
        private const string DefaultOutputFolder = "Assets/MK/MKTextureChannelPacker";
        private const string DefaultOutputName = "NewTexture";
        private const string OutputPathControlName = "MKTextureChannelPacker.OutputPath";
        private const int MaxPreviewSize = 512;
        private const double OutputPathApplyDelay = 0.5d;

        private static readonly Vector2 MinimumWindowSize = new Vector2(360, 360);

        private string _outputAssetPath = DefaultOutputFolder + "/" + DefaultOutputName + ".png";
        private string _outputAssetPathEdit = DefaultOutputFolder + "/" + DefaultOutputName + ".png";
        private bool _outputAssetPathApplyPending = false;
        private bool _outputAssetPathFieldFocused = false;
        private double _outputAssetPathLastEditTime = 0d;
        private Vector2 _scrollPos = Vector2.zero;

        private OutputFormat _outputFormat = OutputFormat.PNG;
        private TextureSize _outputWidth = TextureSize.Size1024;
        private TextureSize _outputHeight = TextureSize.Size1024;

        private bool _dataTexture = true;
        private bool _generateMipMaps = true;

        private Texture2D _previewTexture;
        private bool _previewDirty = true;
        private bool _previewGenerationQueued = false;
        private bool _previewExpanded = true;
        private bool _autoPreview = true;
        private float _previewSizePercent = 25f;
        private PreviewMode _previewMode = PreviewMode.RGB;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _miniHelpStyle;
        private GUIStyle _slotStatusStyle;
        private GUIStyle _createButtonStyle;

        private bool IsNarrowLayout
        {
            get { return position.width < 560f; }
        }

        private enum OutputFormat
        {
            PNG = 0,
            #if UNITY_2018_3_OR_NEWER
            TGA = 1
            #endif
        }

        private enum TextureSize
        {
            Size32 = 32,
            Size64 = 64,
            Size128 = 128,
            Size256 = 256,
            Size512 = 512,
            Size1024 = 1024,
            Size2048 = 2048,
            Size4096 = 4096,
            Size8192 = 8192
        }

        private enum TextureChannel
        {
            Red = 0,
            Green = 1,
            Blue = 2,
            Alpha = 3
        }

        private enum ChannelColor
        {
            Black = 0,
            White = 1
        }

        private enum PreviewMode
        {
            RGB,
            Red,
            Green,
            Blue,
            Alpha
        }

        private struct SourceCache
        {
            public bool hasTexture;
            public Color32[] pixels;
            public int width;
            public int height;
            public TextureChannel sourceChannel;
            public TextureChannel targetChannel;
            public ChannelColor fallbackColor;
            public bool invert;
        }

        private Texture2D _sourceTexture0 = null;
        private bool _sourceChannel0Invert = false;
        private TextureChannel _sourceChannel0 = TextureChannel.Red;
        private TextureChannel _targetChannel0 = TextureChannel.Red;
        private ChannelColor _fallbackColor0 = ChannelColor.Black;

        private Texture2D _sourceTexture1 = null;
        private bool _sourceChannel1Invert = false;
        private TextureChannel _sourceChannel1 = TextureChannel.Green;
        private TextureChannel _targetChannel1 = TextureChannel.Green;
        private ChannelColor _fallbackColor1 = ChannelColor.Black;

        private Texture2D _sourceTexture2 = null;
        private bool _sourceChannel2Invert = false;
        private TextureChannel _sourceChannel2 = TextureChannel.Blue;
        private TextureChannel _targetChannel2 = TextureChannel.Blue;
        private ChannelColor _fallbackColor2 = ChannelColor.Black;

        private Texture2D _sourceTexture3 = null;
        private bool _sourceChannel3Invert = false;
        private TextureChannel _sourceChannel3 = TextureChannel.Alpha;
        private TextureChannel _targetChannel3 = TextureChannel.Alpha;
        private ChannelColor _fallbackColor3 = ChannelColor.White;

        private static readonly string[] TextureSizeLabels =
        {
            "32",
            "64",
            "128",
            "256",
            "512",
            "1024",
            "2048",
            "4096",
            "8192"
        };

        private static readonly TextureSize[] TextureSizeValues =
        {
            TextureSize.Size32,
            TextureSize.Size64,
            TextureSize.Size128,
            TextureSize.Size256,
            TextureSize.Size512,
            TextureSize.Size1024,
            TextureSize.Size2048,
            TextureSize.Size4096,
            TextureSize.Size8192
        };

        [MenuItem("Tools/MK/Texture Channel Packer")]
        private static void Init()
        {
            MKTextureChannelPacker window = GetWindow<MKTextureChannelPacker>();
            window.titleContent = new GUIContent("Texture Channel Packer");
            window.minSize = MinimumWindowSize;
            window.ApplyDefaultFloatingSizeOnce();
            window.Show();
        }

        private void OnEnable()
        {
            minSize = MinimumWindowSize;
            ResetOutputPathToDefault();
            MarkPreviewDirty();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= GenerateQueuedPreviewTexture;
            EditorApplication.update -= UpdatePendingOutputPathEdit;

            if(_previewTexture != null)
                DestroyImmediate(_previewTexture);
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawHeader();

            _scrollPos.x = 0f;
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, false, false);
            _scrollPos.x = 0f;

            EditorGUI.BeginChangeCheck();

            DrawInputSection();

            GUILayout.Space(8);

            DrawOutputSection();

            if(EditorGUI.EndChangeCheck())
                MarkPreviewDirty();

            GUILayout.Space(8);

            DrawPreviewSection();

            EditorGUILayout.EndScrollView();

            DrawValidationSection();
            DrawBottomBar();

            if(_previewExpanded && _autoPreview && _previewDirty && Event.current.type == EventType.Repaint)
                QueuePreviewGeneration();
        }

        private void ApplyDefaultFloatingSizeOnce()
        {
            string key = ProjectPrefsKey("DefaultWindowSizeApplied");

            if(EditorPrefs.GetBool(key, false))
                return;

            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();

            float width = Mathf.Clamp(mainWindow.width * 0.45f, 640f, 900f);
            float height = Mathf.Clamp(mainWindow.height * 0.70f, 520f, 820f);

            position = new Rect(
                mainWindow.x + (mainWindow.width - width) * 0.5f,
                mainWindow.y + (mainWindow.height - height) * 0.5f,
                width,
                height
            );

            EditorPrefs.SetBool(key, true);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            EditorGUILayout.LabelField("Texture Channel Packer", _titleStyle);

            EditorGUILayout.LabelField(
                "Pack channels from different textures into one mask texture.",
                _subtitleStyle
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawInputSection()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            EditorGUILayout.LabelField("Input Channels", _sectionTitleStyle);
            EditorGUILayout.LabelField("Each slot writes one source channel into one output channel. Empty texture slots write a constant black or white value.", _miniHelpStyle);

            GUILayout.Space(6);

            DrawPackerRow("Slot 1", ref _sourceTexture0, ref _sourceChannel0, ref _sourceChannel0Invert, ref _targetChannel0, ref _fallbackColor0);
            DrawPackerRow("Slot 2", ref _sourceTexture1, ref _sourceChannel1, ref _sourceChannel1Invert, ref _targetChannel1, ref _fallbackColor1);
            DrawPackerRow("Slot 3", ref _sourceTexture2, ref _sourceChannel2, ref _sourceChannel2Invert, ref _targetChannel2, ref _fallbackColor2);
            DrawPackerRow("Slot 4", ref _sourceTexture3, ref _sourceChannel3, ref _sourceChannel3Invert, ref _targetChannel3, ref _fallbackColor3);

            EditorGUILayout.EndVertical();
        }

        private void DrawPackerRow
        (
            string title,
            ref Texture2D texture,
            ref TextureChannel sourceChannel,
            ref bool invert,
            ref TextureChannel targetChannel,
            ref ChannelColor fallbackColor
        )
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = IsNarrowLayout ? 58f : 70f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            Rect colorRect = GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(18));
            EditorGUI.DrawRect(colorRect, GetChannelColor(targetChannel));

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();

            if(IsNarrowLayout)
                DrawPackerRowNarrow(ref texture, ref sourceChannel, ref invert, ref targetChannel, ref fallbackColor);
            else
                DrawPackerRowWide(ref texture, ref sourceChannel, ref invert, ref targetChannel, ref fallbackColor);

            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = oldLabelWidth;
        }

        private void DrawPackerRowWide
        (
            ref Texture2D texture,
            ref TextureChannel sourceChannel,
            ref bool invert,
            ref TextureChannel targetChannel,
            ref ChannelColor fallbackColor
        )
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(76));

            texture = (Texture2D)EditorGUILayout.ObjectField(
                texture,
                typeof(Texture2D),
                false,
                GUILayout.Width(72),
                GUILayout.Height(72)
            );

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(GUILayout.Height(72));

            EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if(texture != null)
            {
                sourceChannel = (TextureChannel)EditorGUILayout.EnumPopup(
                    new GUIContent("From", "Source channel read from the input texture."),
                    sourceChannel
                );

                invert = EditorGUILayout.ToggleLeft(
                    new GUIContent("Invert", "Invert the source value before writing it."),
                    invert,
                    GUILayout.Width(72)
                );
            }
            else
            {
                fallbackColor = (ChannelColor)EditorGUILayout.EnumPopup(
                    new GUIContent("Fill", "Constant value used when no texture is assigned."),
                    fallbackColor
                );

                using(new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ToggleLeft("Invert", false, GUILayout.Width(72));
            }

            EditorGUILayout.EndHorizontal();

            targetChannel = (TextureChannel)EditorGUILayout.EnumPopup(
                new GUIContent("Write To", "Output channel this slot writes into."),
                targetChannel
            );

            GUILayout.FlexibleSpace();

            DrawSlotStatus(texture);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackerRowNarrow(
            ref Texture2D texture,
            ref TextureChannel sourceChannel,
            ref bool invert,
            ref TextureChannel targetChannel,
            ref ChannelColor fallbackColor
        )
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(68));

            texture = (Texture2D)EditorGUILayout.ObjectField(
                texture,
                typeof(Texture2D),
                false,
                GUILayout.Width(64),
                GUILayout.Height(64)
            );

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical(GUILayout.Height(64));

            if(texture != null)
            {
                sourceChannel = (TextureChannel)EditorGUILayout.EnumPopup(
                    new GUIContent("From", "Source channel read from the input texture."),
                    sourceChannel
                );

                targetChannel = (TextureChannel)EditorGUILayout.EnumPopup(
                    new GUIContent("To", "Output channel this slot writes into."),
                    targetChannel
                );

                invert = EditorGUILayout.ToggleLeft(
                    new GUIContent("Invert", "Invert the source value before writing it."),
                    invert
                );
            }
            else
            {
                fallbackColor = (ChannelColor)EditorGUILayout.EnumPopup(
                    new GUIContent("Fill", "Constant value used when no texture is assigned."),
                    fallbackColor
                );

                targetChannel = (TextureChannel)EditorGUILayout.EnumPopup(
                    new GUIContent("To", "Output channel this slot writes into."),
                    targetChannel
                );

                GUILayout.Space(EditorGUIUtility.singleLineHeight);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            DrawSlotStatus(texture);
        }

        private void DrawSlotStatus(Texture2D texture)
        {
            string text = texture == null
                ? "No texture assigned - this slot writes a constant value."
                : texture.name + " | " + texture.width + " × " + texture.height + " px";

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                _slotStatusStyle,
                GUILayout.Height(18),
                GUILayout.ExpandWidth(true)
            );

            EditorGUI.LabelField(rect, text, _slotStatusStyle);
        }

        private void DrawOutputSection()
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = IsNarrowLayout ? 52f : 70f;

            EditorGUILayout.BeginVertical(_cardStyle);

            EditorGUILayout.LabelField("Output", _sectionTitleStyle);

            if(IsNarrowLayout)
            {
                EditorGUILayout.BeginHorizontal();

                DrawTextureSizePopup(new GUIContent("Width", "Output texture width."), ref _outputWidth);
                DrawTextureSizePopup(new GUIContent("Height", "Output texture height."), ref _outputHeight);

                EditorGUILayout.EndHorizontal();

                if(GUILayout.Button("Match First Input"))
                    MatchFirstInputSize();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                DrawTextureSizePopup(new GUIContent("Width", "Output texture width."), ref _outputWidth);
                DrawTextureSizePopup(new GUIContent("Height", "Output texture height."), ref _outputHeight);

                if(GUILayout.Button("Match First Input", GUILayout.Width(130)))
                    MatchFirstInputSize();

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            _dataTexture = EditorGUILayout.ToggleLeft(
                new GUIContent("Data Texture", "Disables sRGB on the output texture. Recommended for masks and packed data."),
                _dataTexture
            );

            _generateMipMaps = EditorGUILayout.ToggleLeft(
                new GUIContent("Mip Maps", "Generate mip maps for the output texture."),
                _generateMipMaps
            );

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            EditorGUILayout.LabelField(
                new GUIContent("Save As", "Project-relative output path. Existing files are overwritten."),
                EditorStyles.boldLabel
            );

            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName(OutputPathControlName);

            EditorGUI.BeginChangeCheck();

            _outputAssetPathEdit = EditorGUILayout.TextField(_outputAssetPathEdit);

            if(EditorGUI.EndChangeCheck())
                QueueOutputPathEditApply();

            bool outputPathFieldFocused = GUI.GetNameOfFocusedControl() == OutputPathControlName;

            if(_outputAssetPathFieldFocused && !outputPathFieldFocused)
                ApplyOutputPathEdit();

            _outputAssetPathFieldFocused = outputPathFieldFocused;

            if(GUILayout.Button("Browse", GUILayout.Width(72)))
                SelectOutputFile();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Format", GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();

            _outputFormat = (OutputFormat)EditorGUILayout.EnumPopup(_outputFormat);

            if(EditorGUI.EndChangeCheck())
            {
                ApplyOutputPathEdit();
                _outputAssetPath = ChangeOutputPathExtension(_outputAssetPath, _outputFormat);
                _outputAssetPathEdit = _outputAssetPath;
            }

            EditorGUILayout.EndHorizontal();

            string finalPath = GetOutputAssetPath();

            if(File.Exists(finalPath))
                EditorGUILayout.HelpBox("A texture already exists at this path and will be overwritten.", MessageType.Warning);
            else
                EditorGUILayout.LabelField("Output Path", finalPath, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = oldLabelWidth;
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            if(IsNarrowLayout)
            {
                _previewExpanded = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show Preview", "Show or hide the packed texture preview."),
                    _previewExpanded,
                    EditorStyles.boldLabel
                );

                using(new EditorGUI.DisabledScope(!_previewExpanded))
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();

                    _previewMode = (PreviewMode)EditorGUILayout.EnumPopup(_previewMode);
                    _autoPreview = GUILayout.Toggle(_autoPreview, "Auto", "Button", GUILayout.Width(52));

                    if(EditorGUI.EndChangeCheck())
                        MarkPreviewDirty();

                    if(GUILayout.Button("Refresh", GUILayout.Width(72)))
                        QueuePreviewGeneration();

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                _previewExpanded = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show Preview", "Show or hide the packed texture preview."),
                    _previewExpanded,
                    EditorStyles.boldLabel,
                    GUILayout.Width(130)
                );

                GUILayout.FlexibleSpace();

                using(new EditorGUI.DisabledScope(!_previewExpanded))
                {
                    EditorGUI.BeginChangeCheck();

                    _previewMode = (PreviewMode)EditorGUILayout.EnumPopup(_previewMode, GUILayout.Width(90));
                    _autoPreview = GUILayout.Toggle(_autoPreview, "Auto", "Button", GUILayout.Width(52));

                    if(EditorGUI.EndChangeCheck())
                        MarkPreviewDirty();

                    if(GUILayout.Button("Refresh", GUILayout.Width(72)))
                        QueuePreviewGeneration();
                }

                EditorGUILayout.EndHorizontal();
            }

            if(!_previewExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Preview of the packed output texture before saving.", _miniHelpStyle);

            EditorGUI.BeginChangeCheck();

            _previewSizePercent = EditorGUILayout.Slider(
                new GUIContent("Preview Size", "100% uses the available content width. Height keeps the texture aspect ratio."),
                _previewSizePercent,
                25f,
                100f
            );

            if(EditorGUI.EndChangeCheck())
                Repaint();

            if(_previewTexture == null && Event.current.type == EventType.Repaint)
                QueuePreviewGeneration();

            if(_previewTexture != null)
            {
                float aspect = _previewTexture.width / (float)_previewTexture.height;

                float contentWidth = Mathf.Max(64f, position.width - 52f);
                float previewWidth = Mathf.Floor(contentWidth * (_previewSizePercent / 100f));
                float previewHeight = Mathf.Floor(previewWidth / aspect);

                previewWidth = Mathf.Max(32f, previewWidth);
                previewHeight = Mathf.Max(32f, previewHeight);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                Rect rect = GUILayoutUtility.GetRect(
                    previewWidth,
                    previewHeight,
                    GUILayout.Width(previewWidth),
                    GUILayout.Height(previewHeight)
                );

                EditorGUI.DrawPreviewTexture(
                    rect,
                    _previewTexture,
                    null,
                    ScaleMode.ScaleToFit
                );

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void QueuePreviewGeneration()
        {
            if(_previewGenerationQueued)
                return;

            _previewGenerationQueued = true;
            EditorApplication.delayCall += GenerateQueuedPreviewTexture;
        }

        private void GenerateQueuedPreviewTexture()
        {
            EditorApplication.delayCall -= GenerateQueuedPreviewTexture;
            _previewGenerationQueued = false;

            if(this == null || !_previewExpanded)
                return;

            try
            {
                GeneratePreviewTexture();
            }
            catch(System.Exception exception)
            {
                _previewDirty = false;
                Debug.LogException(exception);
            }
        }

        private void GeneratePreviewTexture()
        {
            int outputWidth = (int)_outputWidth;
            int outputHeight = (int)_outputHeight;

            int previewWidth;
            int previewHeight;

            GetPreviewSize(outputWidth, outputHeight, out previewWidth, out previewHeight);

            Dictionary<string, bool> readableStates = new Dictionary<string, bool>();

            try
            {
                MakeReadableIfNeeded(_sourceTexture0, readableStates);
                MakeReadableIfNeeded(_sourceTexture1, readableStates);
                MakeReadableIfNeeded(_sourceTexture2, readableStates);
                MakeReadableIfNeeded(_sourceTexture3, readableStates);

                if(_previewTexture == null || _previewTexture.width != previewWidth || _previewTexture.height != previewHeight)
                {
                    if(_previewTexture != null)
                        DestroyImmediate(_previewTexture);

                    _previewTexture = new Texture2D(previewWidth, previewHeight, UnityEngine.TextureFormat.RGBA32, false, true);
                    _previewTexture.hideFlags = HideFlags.HideAndDontSave;
                }

                Color32[] pixels = GeneratePackedPixelsFast(previewWidth, previewHeight, false);

                for(int i = 0; i < pixels.Length; i++)
                    pixels[i] = ConvertPreviewColor(pixels[i]);

                _previewTexture.SetPixels32(pixels);
                _previewTexture.Apply(false, false);

                _previewDirty = false;
                Repaint();
            }
            finally
            {
                RestoreReadableStates(readableStates);
            }
        }

        private static void GetPreviewSize(int outputWidth, int outputHeight, out int previewWidth, out int previewHeight)
        {
            if(outputWidth >= outputHeight)
            {
                previewWidth = Mathf.Min(outputWidth, MaxPreviewSize);
                previewHeight = Mathf.Max(1, Mathf.RoundToInt(previewWidth * (outputHeight / (float)outputWidth)));
            }
            else
            {
                previewHeight = Mathf.Min(outputHeight, MaxPreviewSize);
                previewWidth = Mathf.Max(1, Mathf.RoundToInt(previewHeight * (outputWidth / (float)outputHeight)));
            }
        }

        private Color32 ConvertPreviewColor(Color32 color)
        {
            switch(_previewMode)
            {
                case PreviewMode.Red:
                    return new Color32(color.r, color.r, color.r, 255);

                case PreviewMode.Green:
                    return new Color32(color.g, color.g, color.g, 255);

                case PreviewMode.Blue:
                    return new Color32(color.b, color.b, color.b, 255);

                case PreviewMode.Alpha:
                    return new Color32(color.a, color.a, color.a, 255);

                default:
                    return new Color32(color.r, color.g, color.b, 255);
            }
        }

        private void MarkPreviewDirty()
        {
            _previewDirty = true;
        }

        private Color32[] GeneratePackedPixelsFast(int width, int height, bool showProgress)
        {
            SourceCache cache0 = CreateSourceCache(_sourceTexture0, _sourceChannel0, _targetChannel0, _fallbackColor0, _sourceChannel0Invert);
            SourceCache cache1 = CreateSourceCache(_sourceTexture1, _sourceChannel1, _targetChannel1, _fallbackColor1, _sourceChannel1Invert);
            SourceCache cache2 = CreateSourceCache(_sourceTexture2, _sourceChannel2, _targetChannel2, _fallbackColor2, _sourceChannel2Invert);
            SourceCache cache3 = CreateSourceCache(_sourceTexture3, _sourceChannel3, _targetChannel3, _fallbackColor3, _sourceChannel3Invert);

            Color32[] pixels = new Color32[width * height];

            for(int y = 0; y < height; y++)
            {
                if(showProgress && (y == 0 || y % 64 == 0 || y == height - 1))
                {
                    float progress = y / (float)height;
                    EditorUtility.DisplayProgressBar("Texture Channel Packer", "Packing raw pixels...", progress);
                }

                for(int x = 0; x < width; x++)
                {
                    byte r = 0;
                    byte g = 0;
                    byte b = 0;
                    byte a = 255;

                    PackRawSlot(cache0, x, y, width, height, ref r, ref g, ref b, ref a);
                    PackRawSlot(cache1, x, y, width, height, ref r, ref g, ref b, ref a);
                    PackRawSlot(cache2, x, y, width, height, ref r, ref g, ref b, ref a);
                    PackRawSlot(cache3, x, y, width, height, ref r, ref g, ref b, ref a);

                    pixels[y * width + x] = new Color32(r, g, b, a);
                }
            }

            return pixels;
        }

        private static SourceCache CreateSourceCache(
            Texture2D texture,
            TextureChannel sourceChannel,
            TextureChannel targetChannel,
            ChannelColor fallbackColor,
            bool invert
        )
        {
            SourceCache cache = new SourceCache();

            cache.hasTexture = texture != null;
            cache.sourceChannel = sourceChannel;
            cache.targetChannel = targetChannel;
            cache.fallbackColor = fallbackColor;
            cache.invert = invert;

            if(texture != null)
            {
                cache.width = texture.width;
                cache.height = texture.height;
                cache.pixels = texture.GetPixels32();
            }

            return cache;
        }

        private static void PackRawSlot(
            SourceCache cache,
            int outputX,
            int outputY,
            int outputWidth,
            int outputHeight,
            ref byte r,
            ref byte g,
            ref byte b,
            ref byte a
        )
        {
            byte value = ReadRawValue(cache, outputX, outputY, outputWidth, outputHeight);

            switch(cache.targetChannel)
            {
                case TextureChannel.Green:
                    g = value;
                    break;

                case TextureChannel.Blue:
                    b = value;
                    break;

                case TextureChannel.Alpha:
                    a = value;
                    break;

                default:
                    r = value;
                    break;
            }
        }

        private static byte ReadRawValue(
            SourceCache cache,
            int outputX,
            int outputY,
            int outputWidth,
            int outputHeight
        )
        {
            if(!cache.hasTexture || cache.pixels == null || cache.width <= 0 || cache.height <= 0)
            {
                byte fallback = cache.fallbackColor == ChannelColor.White ? (byte)255 : (byte)0;
                return cache.invert ? (byte)(255 - fallback) : fallback;
            }

            int sourceX = outputWidth <= 1
                ? 0
                : (int)((long)outputX * (cache.width - 1) / (outputWidth - 1));

            int sourceY = outputHeight <= 1
                ? 0
                : (int)((long)outputY * (cache.height - 1) / (outputHeight - 1));

            sourceX = Mathf.Clamp(sourceX, 0, cache.width - 1);
            sourceY = Mathf.Clamp(sourceY, 0, cache.height - 1);

            Color32 color = cache.pixels[sourceY * cache.width + sourceX];

            byte value;

            switch(cache.sourceChannel)
            {
                case TextureChannel.Green:
                    value = color.g;
                    break;

                case TextureChannel.Blue:
                    value = color.b;
                    break;

                case TextureChannel.Alpha:
                    value = color.a;
                    break;

                default:
                    value = color.r;
                    break;
            }

            return cache.invert ? (byte)(255 - value) : value;
        }

        private void DrawTextureSizePopup(GUIContent label, ref TextureSize value)
        {
            int index = System.Array.IndexOf(TextureSizeValues, value);

            if(index < 0)
                index = System.Array.IndexOf(TextureSizeValues, TextureSize.Size1024);

            index = EditorGUILayout.Popup(label, index, TextureSizeLabels);
            value = TextureSizeValues[index];
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            bool hasMessages = false;

            if(HasDuplicateOutputChannels())
            {
                hasMessages = true;
                EditorGUILayout.HelpBox("Every output channel must be unique. Two or more slots write into the same channel.", MessageType.Error);
            }

            if(!IsValidOutputAssetPath())
            {
                hasMessages = true;
                EditorGUILayout.HelpBox("Save As must point to a file inside the project's Assets folder.", MessageType.Error);
            }

            if(string.IsNullOrWhiteSpace(GetOutputFileName()))
            {
                hasMessages = true;
                EditorGUILayout.HelpBox("Enter a valid file name before creating the texture.", MessageType.Error);
            }

            if(!HaveMatchingAspectRatios())
            {
                hasMessages = true;
                EditorGUILayout.HelpBox("Input textures use different aspect ratios. Raw pixel packing still works, but different sizes/aspects will remap pixels without filtering.", MessageType.Warning);
            }

            if(!hasMessages)
                EditorGUILayout.HelpBox("Ready to create texture.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawBottomBar()
        {
            EditorGUILayout.BeginVertical(_cardStyle);

            using(new EditorGUI.DisabledScope(!CanCreateTexture()))
            {
                if(GUILayout.Button("Create Texture", _createButtonStyle, GUILayout.Height(38)))
                    CreateTexture();
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateTexture()
        {
            ApplyOutputPathEdit();

            int outputWidth = (int)_outputWidth;
            int outputHeight = (int)_outputHeight;

            string outputPath = GetOutputAssetPath();

            if(!IsValidOutputAssetPath())
            {
                EditorUtility.DisplayDialog("Invalid Output Path", "Please choose a file inside the project's Assets folder.", "OK");
                return;
            }

            Texture2D outputTexture = null;
            Dictionary<string, bool> readableStates = new Dictionary<string, bool>();

            try
            {
                EditorUtility.DisplayProgressBar("Texture Channel Packer", "Preparing source textures...", 0.05f);

                MakeReadableIfNeeded(_sourceTexture0, readableStates);
                MakeReadableIfNeeded(_sourceTexture1, readableStates);
                MakeReadableIfNeeded(_sourceTexture2, readableStates);
                MakeReadableIfNeeded(_sourceTexture3, readableStates);

                outputTexture = new Texture2D(outputWidth, outputHeight, UnityEngine.TextureFormat.RGBA32, false, true);
                outputTexture.SetPixels32(GeneratePackedPixelsFast(outputWidth, outputHeight, true));
                outputTexture.Apply(false, false);

                EditorUtility.DisplayProgressBar("Texture Channel Packer", "Saving texture...", 0.95f);

                SaveOutputTexture(outputTexture, outputPath, Mathf.Max(outputWidth, outputHeight));

                EditorUtility.FocusProjectWindow();

                Object savedAsset = AssetDatabase.LoadAssetAtPath<Object>(outputPath);
                Selection.activeObject = savedAsset;
                EditorGUIUtility.PingObject(savedAsset);
            }
            finally
            {
                RestoreReadableStates(readableStates);

                if(outputTexture != null)
                    DestroyImmediate(outputTexture);

                EditorUtility.ClearProgressBar();
            }
        }

        private bool CanCreateTexture()
        {
            return !HasDuplicateOutputChannels() && IsValidOutputAssetPath() && !string.IsNullOrWhiteSpace(GetOutputFileName());
        }

        private bool HasDuplicateOutputChannels()
        {
            return CountChannelUsage(_targetChannel0) > 1 ||
                   CountChannelUsage(_targetChannel1) > 1 ||
                   CountChannelUsage(_targetChannel2) > 1 ||
                   CountChannelUsage(_targetChannel3) > 1;
        }

        private int CountChannelUsage(TextureChannel channel)
        {
            int count = 0;

            if(_targetChannel0 == channel)
                count++;

            if(_targetChannel1 == channel)
                count++;

            if(_targetChannel2 == channel)
                count++;

            if(_targetChannel3 == channel)
                count++;

            return count;
        }

        private bool HaveMatchingAspectRatios()
        {
            List<Texture2D> textures = new List<Texture2D>();

            if(_sourceTexture0 != null)
                textures.Add(_sourceTexture0);

            if(_sourceTexture1 != null)
                textures.Add(_sourceTexture1);

            if(_sourceTexture2 != null)
                textures.Add(_sourceTexture2);

            if(_sourceTexture3 != null)
                textures.Add(_sourceTexture3);

            if(textures.Count <= 1)
                return true;

            float aspect = textures[0].width / (float)textures[0].height;

            for(int i = 1; i < textures.Count; i++)
            {
                float otherAspect = textures[i].width / (float)textures[i].height;

                if(Mathf.Abs(aspect - otherAspect) > 0.001f)
                    return false;
            }

            return true;
        }

        private void MatchFirstInputSize()
        {
            Texture2D texture = null;

            if(_sourceTexture0 != null)
                texture = _sourceTexture0;
            else if(_sourceTexture1 != null)
                texture = _sourceTexture1;
            else if(_sourceTexture2 != null)
                texture = _sourceTexture2;
            else if(_sourceTexture3 != null)
                texture = _sourceTexture3;

            if(texture == null)
                return;

            _outputWidth = ClosestTextureSize(texture.width);
            _outputHeight = ClosestTextureSize(texture.height);
            MarkPreviewDirty();
        }

        private static TextureSize ClosestTextureSize(int size)
        {
            TextureSize closest = TextureSize.Size1024;
            int bestDistance = int.MaxValue;

            for(int i = 0; i < TextureSizeValues.Length; i++)
            {
                int current = (int)TextureSizeValues[i];
                int distance = Mathf.Abs(current - size);

                if(distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = TextureSizeValues[i];
                }
            }

            return closest;
        }

        private void SelectOutputFile()
        {
            ApplyOutputPathEdit();

            string currentPath = GetOutputAssetPath();
            string currentFolder = Path.GetDirectoryName(currentPath);
            string currentName = Path.GetFileNameWithoutExtension(currentPath);
            string extension = GetFileExtension(_outputFormat).TrimStart('.');

            if(string.IsNullOrEmpty(currentFolder))
                currentFolder = DefaultOutputFolder;

            currentFolder = NormalizeAssetPath(currentFolder);

            if(!IsProjectRelativeAssetPath(currentFolder))
                currentFolder = DefaultOutputFolder;

            string selectedPath = EditorUtility.SaveFilePanelInProject(
                "Save Packed Texture",
                currentName,
                extension,
                "Choose where to save the packed texture.",
                currentFolder
            );

            if(string.IsNullOrEmpty(selectedPath))
                return;

            _outputAssetPath = NormalizeAssetPath(selectedPath);
            _outputAssetPathEdit = _outputAssetPath;
            CancelPendingOutputPathEdit();
            UpdateOutputFormatFromPathExtension();
        }

        private bool IsValidOutputAssetPath()
        {
            string path = GetOutputAssetPath();
            string folder = GetOutputFolderPath();

            return IsProjectRelativeAssetPath(path) &&
                   IsProjectRelativeAssetPath(folder) &&
                   !AssetDatabase.IsValidFolder(path);
        }

        private string GetOutputAssetPath()
        {
            string path = NormalizeAssetPath(_outputAssetPath);

            if(string.IsNullOrEmpty(path) || path == "Assets")
                path = DefaultOutputFolder + "/" + DefaultOutputName + GetFileExtension(_outputFormat);

            string extension = GetFileExtension(_outputFormat);

            if(string.IsNullOrEmpty(Path.GetExtension(path)))
                path += extension;
            else
                path = Path.ChangeExtension(path, extension).Replace("\\", "/");

            string folder = Path.GetDirectoryName(path);

            if(string.IsNullOrEmpty(folder))
                folder = DefaultOutputFolder;

            folder = NormalizeAssetPath(folder);

            string filename = FilterFilename(Path.GetFileNameWithoutExtension(path));

            if(string.IsNullOrEmpty(filename))
                filename = DefaultOutputName;

            return folder + "/" + filename + extension;
        }

        private string GetOutputFolderPath()
        {
            string path = GetOutputAssetPath();
            string folder = Path.GetDirectoryName(path);

            if(string.IsNullOrEmpty(folder))
                return DefaultOutputFolder;

            return NormalizeAssetPath(folder);
        }

        private string GetOutputFileName()
        {
            return FilterFilename(Path.GetFileNameWithoutExtension(GetOutputAssetPath()));
        }

        private static string ChangeOutputPathExtension(string path, OutputFormat format)
        {
            path = NormalizeAssetPath(path);

            if(string.IsNullOrEmpty(path))
                path = DefaultOutputFolder + "/" + DefaultOutputName;

            return Path.ChangeExtension(path, GetFileExtension(format)).Replace("\\", "/");
        }

        private void UpdateOutputFormatFromPathExtension()
        {
            string extension = Path.GetExtension(_outputAssetPath).ToLowerInvariant();

            if(extension == ".png")
                _outputFormat = OutputFormat.PNG;

            #if UNITY_2018_3_OR_NEWER
            if(extension == ".tga")
                _outputFormat = OutputFormat.TGA;
            #endif
        }

        private void QueueOutputPathEditApply()
        {
            _outputAssetPathLastEditTime = EditorApplication.timeSinceStartup;
            _outputAssetPathApplyPending = true;

            EditorApplication.update -= UpdatePendingOutputPathEdit;
            EditorApplication.update += UpdatePendingOutputPathEdit;
        }

        private void UpdatePendingOutputPathEdit()
        {
            if(!_outputAssetPathApplyPending)
            {
                EditorApplication.update -= UpdatePendingOutputPathEdit;
                return;
            }

            if(EditorApplication.timeSinceStartup - _outputAssetPathLastEditTime < OutputPathApplyDelay)
                return;

            ApplyOutputPathEdit();
            Repaint();
        }

        private void ApplyOutputPathEdit()
        {
            if(!_outputAssetPathApplyPending && _outputAssetPath == _outputAssetPathEdit)
                return;

            _outputAssetPath = NormalizeAssetPath(_outputAssetPathEdit);
            UpdateOutputFormatFromPathExtension();
            _outputAssetPath = GetOutputAssetPath();
            _outputAssetPathEdit = _outputAssetPath;

            CancelPendingOutputPathEdit();
        }

        private void CancelPendingOutputPathEdit()
        {
            _outputAssetPathApplyPending = false;
            EditorApplication.update -= UpdatePendingOutputPathEdit;
        }

        private void SaveOutputTexture(Texture2D texture, string outputPath, int maxSize)
        {
            outputPath = NormalizeAssetPath(outputPath);

            string folder = Path.GetDirectoryName(outputPath);

            if(!string.IsNullOrEmpty(folder))
                EnsureAssetFolderExists(folder.Replace("\\", "/"));

            #if UNITY_2018_3_OR_NEWER
            if(_outputFormat == OutputFormat.PNG)
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            else
                File.WriteAllBytes(outputPath, texture.EncodeToTGA());
            #else
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            #endif

            AssetDatabase.ImportAsset(outputPath);

            TextureImporter textureImporter = TextureImporter.GetAtPath(outputPath) as TextureImporter;

            if(textureImporter != null)
            {
                textureImporter.wrapMode = TextureWrapMode.Repeat;
                textureImporter.maxTextureSize = maxSize;
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                textureImporter.sRGBTexture = !_dataTexture;
                textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                textureImporter.mipmapEnabled = _generateMipMaps;
                textureImporter.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MakeReadableIfNeeded(Texture2D texture, Dictionary<string, bool> readableStates)
        {
            if(texture == null)
                return;

            string texturePath = AssetDatabase.GetAssetPath(texture);

            if(string.IsNullOrEmpty(texturePath))
                return;

            if(readableStates.ContainsKey(texturePath))
                return;

            TextureImporter textureImporter = TextureImporter.GetAtPath(texturePath) as TextureImporter;

            if(textureImporter == null)
                return;

            readableStates.Add(texturePath, textureImporter.isReadable);

            if(!textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                textureImporter.SaveAndReimport();
            }
        }

        private static void RestoreReadableStates(Dictionary<string, bool> readableStates)
        {
            foreach(KeyValuePair<string, bool> state in readableStates)
            {
                TextureImporter textureImporter = TextureImporter.GetAtPath(state.Key) as TextureImporter;

                if(textureImporter == null)
                    continue;

                if(textureImporter.isReadable != state.Value)
                {
                    textureImporter.isReadable = state.Value;
                    textureImporter.SaveAndReimport();
                }
            }
        }

        private static string GetFileExtension(OutputFormat format)
        {
            #if UNITY_2018_3_OR_NEWER
            return format == OutputFormat.PNG ? ".png" : ".tga";
            #else
            return ".png";
            #endif
        }

        private static string FilterFilename(string name)
        {
            List<char> invalidChars = new List<char>(Path.GetInvalidFileNameChars());
            List<char> filename = new List<char>();

            foreach(char c in name)
            {
                if(!invalidChars.Contains(c))
                    filename.Add(c);
            }

            return new string(filename.ToArray()).Trim();
        }

        private static bool IsProjectRelativeAssetPath(string path)
        {
            if(string.IsNullOrEmpty(path))
                return false;

            path = NormalizeAssetPath(path);

            return path == "Assets" || path.StartsWith("Assets/");
        }

        private static string NormalizeAssetPath(string path)
        {
            if(string.IsNullOrEmpty(path))
                return string.Empty;

            path = path.Replace("\\", "/").Trim();

            string dataPath = Application.dataPath.Replace("\\", "/");

            if(path.StartsWith(dataPath))
                path = "Assets" + path.Substring(dataPath.Length);

            return path.TrimEnd('/');
        }

        private static void EnsureAssetFolderExists(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);

            if(AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for(int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if(!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static Color GetChannelColor(TextureChannel channel)
        {
            switch(channel)
            {
                case TextureChannel.Red:
                    return new Color(0.85f, 0.2f, 0.2f, 1f);

                case TextureChannel.Green:
                    return new Color(0.25f, 0.75f, 0.25f, 1f);

                case TextureChannel.Blue:
                    return new Color(0.25f, 0.45f, 0.9f, 1f);

                case TextureChannel.Alpha:
                    return new Color(0.65f, 0.65f, 0.65f, 1f);

                default:
                    return Color.white;
            }
        }

        private void ResetOutputPathToDefault()
        {
            _outputAssetPath = DefaultOutputFolder + "/" + DefaultOutputName + GetFileExtension(_outputFormat);
            _outputAssetPath = NormalizeAssetPath(_outputAssetPath);
            _outputAssetPathEdit = _outputAssetPath;
            CancelPendingOutputPathEdit();
            UpdateOutputFormatFromPathExtension();
        }

        private static string ProjectPrefsKey(string key)
        {
            string projectPath = Application.dataPath.Replace("\\", "/");
            return "MK.TextureChannelPacker." + projectPath + "." + key;
        }

        private void EnsureStyles()
        {
            if(_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                wordWrap = true
            };

            _subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true
            };

            _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(8, 8, 6, 6)
            };

            _miniHelpStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _slotStatusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 18f,
                padding = new RectOffset(2, 2, 0, 0)
            };

            _createButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
        }
    }
}

#endif
