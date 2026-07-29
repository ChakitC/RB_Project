using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThirdPersonCameraSettingsPanel : MonoBehaviour
{
    Slider sensitivityX;
    Slider sensitivityY;
    Slider fieldOfView;
    Toggle invertY;
    Text sensitivityXLabel;
    Text sensitivityYLabel;
    Text fieldOfViewLabel;

    public static void EnsureExists(GameObject menuRoot)
    {
        if (menuRoot == null ||
            menuRoot.GetComponentInChildren<ThirdPersonCameraSettingsPanel>(true) != null)
        {
            return;
        }

        GameObject panelObject = new("Third Person Camera Settings", typeof(RectTransform));
        panelObject.transform.SetParent(menuRoot.transform, false);
        panelObject.AddComponent<ThirdPersonCameraSettingsPanel>();
    }

    void Awake()
    {
        BuildUi();
        LoadValues();
    }

    void OnEnable()
    {
        LoadValues();
    }

    void BuildUi()
    {
        RectTransform panel = GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-24f, -24f);
        panel.sizeDelta = new Vector2(340f, 250f);

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.045f, 0.065f, 0.92f);

        CreateText("Title", panel, "CAMERA", new Vector2(18f, -16f), new Vector2(300f, 28f), 20);
        sensitivityX = CreateSliderRow(
            panel,
            "Horizontal",
            0.01f,
            1f,
            new Vector2(18f, -58f),
            out sensitivityXLabel);
        sensitivityY = CreateSliderRow(
            panel,
            "Vertical",
            0.01f,
            1f,
            new Vector2(18f, -108f),
            out sensitivityYLabel);
        fieldOfView = CreateSliderRow(
            panel,
            "Field of View",
            35f,
            90f,
            new Vector2(18f, -158f),
            out fieldOfViewLabel);
        invertY = CreateToggleRow(panel, "Invert Y", new Vector2(18f, -214f));

        sensitivityX.onValueChanged.AddListener(OnSensitivityXChanged);
        sensitivityY.onValueChanged.AddListener(OnSensitivityYChanged);
        fieldOfView.onValueChanged.AddListener(OnFieldOfViewChanged);
        invertY.onValueChanged.AddListener(OnInvertYChanged);
    }

    void LoadValues()
    {
        if (sensitivityX == null)
            return;

        sensitivityX.SetValueWithoutNotify(ThirdPersonCameraSettings.SensitivityX);
        sensitivityY.SetValueWithoutNotify(ThirdPersonCameraSettings.SensitivityY);
        fieldOfView.SetValueWithoutNotify(ThirdPersonCameraSettings.FieldOfView);
        invertY.SetIsOnWithoutNotify(ThirdPersonCameraSettings.InvertY);
        RefreshLabels();
    }

    void OnSensitivityXChanged(float value)
    {
        ThirdPersonCameraSettings.SensitivityX = value;
        RefreshLabels();
    }

    void OnSensitivityYChanged(float value)
    {
        ThirdPersonCameraSettings.SensitivityY = value;
        RefreshLabels();
    }

    void OnFieldOfViewChanged(float value)
    {
        ThirdPersonCameraSettings.FieldOfView = value;
        GameplayCameraController.Instance?.SetFieldOfView(value);
        RefreshLabels();
    }

    void OnInvertYChanged(bool value)
    {
        ThirdPersonCameraSettings.InvertY = value;
    }

    void RefreshLabels()
    {
        if (sensitivityXLabel != null)
            sensitivityXLabel.text = $"Horizontal  {sensitivityX.value:0.00}";
        if (sensitivityYLabel != null)
            sensitivityYLabel.text = $"Vertical  {sensitivityY.value:0.00}";
        if (fieldOfViewLabel != null)
            fieldOfViewLabel.text = $"Field of View  {fieldOfView.value:0}";
    }

    static Slider CreateSliderRow(
        RectTransform parent,
        string label,
        float minimum,
        float maximum,
        Vector2 position,
        out Text valueLabel)
    {
        RectTransform row = CreateRect(label, parent, position, new Vector2(304f, 44f));
        valueLabel = CreateText("Label", row, label, Vector2.zero, new Vector2(304f, 20f), 14);

        RectTransform sliderRect = CreateRect("Slider", row, new Vector2(0f, -24f), new Vector2(304f, 16f));
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;

        RectTransform background = CreateRect("Background", sliderRect, Vector2.zero, new Vector2(304f, 5f));
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.2f);

        RectTransform fillArea = CreateRect("Fill Area", sliderRect, Vector2.zero, new Vector2(292f, 5f));
        RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, new Vector2(292f, 5f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.25f, 0.72f, 1f, 1f);

        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect, Vector2.zero, new Vector2(292f, 16f));
        RectTransform handle = CreateRect("Handle", handleArea, Vector2.zero, new Vector2(14f, 14f));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    static Toggle CreateToggleRow(RectTransform parent, string label, Vector2 position)
    {
        RectTransform row = CreateRect(label, parent, position, new Vector2(304f, 24f));
        RectTransform box = CreateRect("Background", row, Vector2.zero, new Vector2(20f, 20f));
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(1f, 1f, 1f, 0.25f);

        RectTransform check = CreateRect("Checkmark", box, Vector2.zero, new Vector2(12f, 12f));
        Image checkImage = check.gameObject.AddComponent<Image>();
        checkImage.color = new Color(0.25f, 0.72f, 1f, 1f);

        Text text = CreateText("Label", row, label, new Vector2(30f, 0f), new Vector2(260f, 22f), 14);
        text.alignment = TextAnchor.MiddleLeft;

        Toggle toggle = row.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        return toggle;
    }

    static Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize)
    {
        RectTransform rect = CreateRect(objectName, parent, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform CreateRect(
        string objectName,
        RectTransform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
