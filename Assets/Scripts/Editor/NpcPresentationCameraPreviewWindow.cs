using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class NpcPresentationCameraPreviewWindow : EditorWindow
{
    const int RenderWidth = 1280;
    const int RenderHeight = 720;
    const float ReferenceWidth = 1920f;
    const float ReferenceHeight = 1080f;

    [SerializeField] NpcPresentationTarget previewTarget;
    Camera previewCamera;
    RenderTexture previewTexture;
    double nextRenderTime;

    public RenderTexture PreviewTexture => previewTexture;

    public static NpcPresentationCameraPreviewWindow Open(NpcPresentationTarget target)
    {
        NpcPresentationCameraPreviewWindow window = GetWindow<NpcPresentationCameraPreviewWindow>();
        window.titleContent = new GUIContent("NPC Camera Preview");
        window.minSize = new Vector2(640f, 400f);
        window.previewTarget = target;
        window.Show();
        window.Focus();
        window.RenderPreviewNow();
        return window;
    }

    void OnEnable()
    {
        titleContent = new GUIContent("NPC Camera Preview");
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        Cleanup();
    }

    void OnEditorUpdate()
    {
        if (previewTarget == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.timeSinceStartup < nextRenderTime)
            return;

        nextRenderTime = EditorApplication.timeSinceStartup + 0.1d;
        RenderPreviewNow();
        Repaint();
    }

    void OnGUI()
    {
        if (previewTarget == null)
        {
            EditorGUILayout.HelpBox(
                "Select an NPC with NpcPresentationTarget and click Open Camera Preview.",
                MessageType.Info);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Camera Preview is available in Edit Mode.", MessageType.Info);
            return;
        }

        Rect available = GUILayoutUtility.GetRect(
            0f,
            10000f,
            0f,
            10000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        Rect imageRect = FitAspect(available, (float)RenderWidth / RenderHeight);

        if (previewTexture != null)
            GUI.DrawTexture(imageRect, previewTexture, ScaleMode.StretchToFill, false);

        DrawShopGuide(imageRect);
        DrawCameraBadge(imageRect);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                RenderPreviewNow();
            if (GUILayout.Button("Close", GUILayout.Width(90f)))
                Close();
        }
    }

    public bool RenderPreviewNow()
    {
        if (previewTarget == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        EnsureCamera();
        EnsureRenderTexture();
        if (previewCamera == null || previewTexture == null)
            return false;

        previewTarget.GetCameraPose(out Vector3 position, out Quaternion rotation);
        previewCamera.transform.SetPositionAndRotation(position, rotation);
        previewCamera.fieldOfView = previewTarget.FieldOfView;
        previewCamera.aspect = (float)RenderWidth / RenderHeight;
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
        return true;
    }

    void EnsureCamera()
    {
        if (previewCamera != null)
            return;

        GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
            "NPC Presentation Preview Camera",
            HideFlags.HideAndDontSave,
            typeof(Camera));
        StageUtility.PlaceGameObjectInCurrentStage(cameraObject);
        previewCamera = cameraObject.GetComponent<Camera>();

        Camera sourceCamera = Camera.main;
        if (sourceCamera != null && sourceCamera != previewCamera)
            previewCamera.CopyFrom(sourceCamera);
        else
        {
            previewCamera.clearFlags = CameraClearFlags.Skybox;
            previewCamera.nearClipPlane = 0.03f;
            previewCamera.farClipPlane = 1000f;
        }

        previewCamera.enabled = false;
        previewCamera.cameraType = CameraType.Preview;
    }

    void EnsureRenderTexture()
    {
        if (previewTexture != null)
            return;

        previewTexture = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "NPC Presentation Camera Preview",
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 1
        };
        previewTexture.Create();
    }

    void DrawShopGuide(Rect imageRect)
    {
        float displayScale = imageRect.width / ReferenceWidth;
        float margin = Mathf.Clamp(previewTarget.UiMargin * displayScale, 0f, imageRect.width * 0.1f);
        float shopWidth = Mathf.Max(1f, imageRect.width * previewTarget.UiWidthRatio - margin * 2f);
        float shopHeight = imageRect.height * ResolveShopHeightRatio(previewTarget);
        Rect shopRect = new(
            imageRect.xMax - margin - shopWidth,
            imageRect.center.y - shopHeight * 0.5f,
            shopWidth,
            shopHeight);

        EditorGUI.DrawRect(shopRect, new Color(0.035f, 0.04f, 0.05f, 0.82f));
        DrawOutline(shopRect, new Color(0.3f, 0.75f, 0.65f, 1f), 2f);

        GUIStyle titleStyle = new(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 13,
            normal = { textColor = Color.white }
        };
        GUI.Label(
            new Rect(shopRect.x + 12f, shopRect.y + 10f, shopRect.width - 24f, 24f),
            $"SHOP UI  ({previewTarget.UiWidthRatio:P0})",
            titleStyle);
    }

    void DrawCameraBadge(Rect imageRect)
    {
        Rect badgeRect = new(imageRect.x + 10f, imageRect.y + 10f, 210f, 24f);
        EditorGUI.DrawRect(badgeRect, new Color(0f, 0f, 0f, 0.72f));
        GUIStyle badgeStyle = new(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.45f, 1f, 0.75f) }
        };
        GUI.Label(badgeRect, $"LIVE CAMERA RENDER  •  FOV {previewTarget.FieldOfView:0.#}", badgeStyle);
    }

    static float ResolveShopHeightRatio(NpcPresentationTarget target)
    {
        RectTransform panelRect = FindShopPanelRect(target);
        CanvasScaler scaler = panelRect != null ? panelRect.GetComponentInParent<CanvasScaler>(true) : null;
        float referenceHeight = scaler != null ? scaler.referenceResolution.y : ReferenceHeight;
        float authoredHeight = panelRect != null ? panelRect.sizeDelta.y : 640f;
        return Mathf.Clamp01(referenceHeight > 0f ? authoredHeight / referenceHeight : 0.6f);
    }

    static RectTransform FindShopPanelRect(NpcPresentationTarget target)
    {
        Transform current = target.transform;
        while (current != null)
        {
            ShopPanelUI panel = current.GetComponentInChildren<ShopPanelUI>(true);
            if (panel != null)
                return panel.transform as RectTransform;

            current = current.parent;
        }

        return null;
    }

    static Rect FitAspect(Rect available, float aspect)
    {
        float width = available.width;
        float height = width / aspect;
        if (height > available.height)
        {
            height = available.height;
            width = height * aspect;
        }

        return new Rect(
            available.center.x - width * 0.5f,
            available.center.y - height * 0.5f,
            width,
            height);
    }

    static void DrawOutline(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            Close();
    }

    void Cleanup()
    {
        if (previewCamera != null)
            DestroyImmediate(previewCamera.gameObject);

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
        }

        previewCamera = null;
        previewTexture = null;
    }
}
