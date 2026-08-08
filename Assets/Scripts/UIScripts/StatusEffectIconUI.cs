using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StatusEffectIconUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI fallbackLabelText;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private TextMeshProUGUI durationText;

    [Header("Style")]
    [SerializeField] private Color buffColor = new Color(0.21f, 0.56f, 0.29f, 0.95f);
    [SerializeField] private Color debuffColor = new Color(0.68f, 0.24f, 0.24f, 0.95f);
    [SerializeField] private Color neutralColor = new Color(0.25f, 0.27f, 0.31f, 0.95f);
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Color metaTextColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private string permanentText = "INF";

    StatusEffectInstance _instance;
    int? _stackCountOverride;

    void Awake()
    {
        EnsureRuntimeVisualTree();
        ApplyStaticStyle();
    }

    void Update()
    {
        if (_instance == null || _instance.Definition == null)
            return;

        RefreshDynamicTexts();
    }

    /// <summary>
    /// Bind แสดง instance เดียว (ค่า default) หรือแบบ group หลาย instance ที่ effectId เดียวกัน (StatusEffectDef.separatePerSource)
    /// — representative คือ instance ที่ TimeLeft เหลือนานสุด, stackCountOverride คือผลรวม stacks ของทุก instance ในกลุ่ม
    /// </summary>
    public void Bind(StatusEffectInstance representative, int? stackCountOverride = null)
    {
        _instance = representative;
        _stackCountOverride = stackCountOverride;

        if (!EnsureRuntimeVisualTree())
            return;

        ApplyStaticStyle();
        RefreshAll();
    }

    public void Clear()
    {
        _instance = null;
        _stackCountOverride = null;

        if (iconImage)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (fallbackLabelText)
        {
            fallbackLabelText.text = string.Empty;
            fallbackLabelText.enabled = false;
        }

        if (stackText)
            stackText.text = string.Empty;

        if (durationText)
            durationText.text = string.Empty;
    }

    void RefreshAll()
    {
        if (_instance == null || _instance.Definition == null)
        {
            Clear();
            return;
        }

        var definition = _instance.Definition;
        bool hasIcon = definition.icon != null;

        if (backgroundImage)
            backgroundImage.color = GetCategoryColor(definition.category);

        if (iconImage)
        {
            iconImage.sprite = definition.icon;
            iconImage.enabled = hasIcon;
        }

        if (fallbackLabelText)
        {
            fallbackLabelText.text = hasIcon ? string.Empty : BuildFallbackLabel(definition);
            fallbackLabelText.enabled = !hasIcon;
        }

        RefreshDynamicTexts();
    }

    void RefreshDynamicTexts()
    {
        if (_instance == null || _instance.Definition == null)
            return;

        int displayStacks = _stackCountOverride ?? _instance.CurrentStacks;
        if (stackText)
            stackText.text = displayStacks > 1 ? displayStacks.ToString() : string.Empty;

        if (durationText)
        {
            durationText.text = _instance.IsPermanent
                ? permanentText
                : Mathf.Max(0f, _instance.TimeLeft).ToString("0.0");
        }
    }

    bool EnsureRuntimeVisualTree()
    {
        if (!root)
            root = transform as RectTransform;

        if (!root)
        {
            Debug.LogWarning($"{nameof(StatusEffectIconUI)} requires a RectTransform root.", this);
            return false;
        }

        if (!backgroundImage)
            backgroundImage = GetOrAddComponent<Image>(gameObject);

        if (!iconImage)
            iconImage = CreateOrFindImage("Icon");

        if (!fallbackLabelText)
            fallbackLabelText = CreateOrFindText("FallbackLabel");

        if (!stackText)
            stackText = CreateOrFindText("Stacks");

        if (!durationText)
            durationText = CreateOrFindText("Duration");

        return true;
    }

    void ApplyStaticStyle()
    {
        if (!root)
            return;

        if (backgroundImage)
        {
            backgroundImage.raycastTarget = false;
            backgroundImage.type = Image.Type.Simple;
        }

        if (iconImage)
        {
            iconImage.color = iconColor;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            RectTransform rect = iconImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
        }

        if (fallbackLabelText)
        {
            ConfigureCenteredText(fallbackLabelText, 18f, labelColor);

            RectTransform rect = fallbackLabelText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }

        if (stackText)
        {
            ConfigureMetaText(stackText, 15f, TextAlignmentOptions.BottomRight);

            RectTransform rect = stackText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(1f, 0.45f);
            rect.offsetMin = new Vector2(0f, 2f);
            rect.offsetMax = new Vector2(-3f, 0f);
        }

        if (durationText)
        {
            ConfigureMetaText(durationText, 12f, TextAlignmentOptions.Top);

            RectTransform rect = durationText.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.6f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(3f, 0f);
            rect.offsetMax = new Vector2(-3f, -2f);
        }
    }

    void ConfigureCenteredText(TextMeshProUGUI text, float fontSize, Color color)
    {
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        text.raycastTarget = false;
    }

    void ConfigureMetaText(TextMeshProUGUI text, float fontSize, TextAlignmentOptions alignment)
    {
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = metaTextColor;
        text.raycastTarget = false;
    }

    Image CreateOrFindImage(string childName)
    {
        Transform existing = root.Find(childName);
        GameObject childObject;

        if (existing == null)
        {
            childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(root, false);
        }
        else
        {
            childObject = existing.gameObject;
        }

        return GetOrAddComponent<Image>(childObject);
    }

    TextMeshProUGUI CreateOrFindText(string childName)
    {
        Transform existing = root.Find(childName);
        GameObject childObject;

        if (existing == null)
        {
            childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            childObject.transform.SetParent(root, false);
        }
        else
        {
            childObject = existing.gameObject;
        }

        return GetOrAddComponent<TextMeshProUGUI>(childObject);
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        if (!target.TryGetComponent(out T component))
            component = target.AddComponent<T>();

        return component;
    }

    Color GetCategoryColor(StatusEffectCategory categoryType)
    {
        return categoryType switch
        {
            StatusEffectCategory.Buff => buffColor,
            StatusEffectCategory.Debuff => debuffColor,
            _ => neutralColor
        };
    }

    static string BuildFallbackLabel(StatusEffectDef definition)
    {
        string source = !string.IsNullOrWhiteSpace(definition.effectId)
            ? definition.effectId
            : definition.name;

        if (string.IsNullOrWhiteSpace(source))
            return "?";

        StringBuilder builder = new StringBuilder(3);
        char previous = '\0';

        for (int i = 0; i < source.Length; i++)
        {
            char current = source[i];
            if (!char.IsLetterOrDigit(current))
            {
                previous = current;
                continue;
            }

            bool isTokenStart = builder.Length == 0 ||
                                !char.IsLetterOrDigit(previous) ||
                                (char.IsUpper(current) && char.IsLetter(previous) && char.IsLower(previous));

            if (!isTokenStart)
            {
                previous = current;
                continue;
            }

            builder.Append(char.ToUpperInvariant(current));
            if (builder.Length >= 3)
                break;

            previous = current;
        }

        if (builder.Length > 0)
            return builder.ToString();

        for (int i = 0; i < source.Length && builder.Length < 3; i++)
        {
            char current = source[i];
            if (!char.IsLetterOrDigit(current))
                continue;

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.Length > 0 ? builder.ToString() : "?";
    }
}
