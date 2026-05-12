using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventorySlotTooltipUI : MonoBehaviour
{
    const float TooltipWidth = 280f;
    static readonly Vector2 PointerOffset = new(24f, -24f);
    static readonly Vector2 CanvasPadding = new(12f, 12f);

    static InventorySlotTooltipUI instance;

    RectTransform rectTransform;
    CanvasGroup canvasGroup;
    Image backgroundImage;
    VerticalLayoutGroup layoutGroup;
    ContentSizeFitter contentSizeFitter;
    TextMeshProUGUI titleText;
    TextMeshProUGUI bodyText;

    InventorySlotUI activeSlot;

    public static InventorySlotTooltipUI GetOrCreate(Canvas rootCanvas)
    {
        if (rootCanvas == null)
            return null;

        if (instance == null)
        {
            var tooltipObject = new GameObject(
                nameof(InventorySlotTooltipUI),
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(InventorySlotTooltipUI));

            instance = tooltipObject.GetComponent<InventorySlotTooltipUI>();
        }

        instance.AttachToCanvas(rootCanvas);
        return instance;
    }

    public static bool IsShowingForSlot(InventorySlotUI slot)
    {
        return instance != null &&
               instance.gameObject.activeSelf &&
               instance.activeSlot == slot;
    }

    public static void HideForSlot(InventorySlotUI slot)
    {
        if (instance == null)
            return;

        instance.HideFor(slot);
    }

    public static void RefreshForSlot(
        InventorySlotUI slot,
        InventorySlotData slotData,
        Vector2 screenPosition,
        Canvas rootCanvas,
        Camera eventCamera = null)
    {
        if (instance == null || !instance.gameObject.activeSelf || instance.activeSlot != slot)
            return;

        instance.ShowFor(slot, slotData, screenPosition, rootCanvas, eventCamera);
    }

    void Awake()
    {
        instance = this;

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        backgroundImage = GetComponent<Image>();
        layoutGroup = GetComponent<VerticalLayoutGroup>();
        contentSizeFitter = GetComponent<ContentSizeFitter>();

        ConfigureRoot();
        EnsureTextChildren();
        Hide();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void ShowFor(
        InventorySlotUI slot,
        InventorySlotData slotData,
        Vector2 screenPosition,
        Canvas rootCanvas,
        Camera eventCamera = null)
    {
        if (slot == null || slotData == null || slotData.IsEmpty || slotData.item == null || rootCanvas == null)
        {
            HideFor(slot);
            return;
        }

        AttachToCanvas(rootCanvas);
        activeSlot = slot;
        ApplySlot(slotData);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        canvasGroup.alpha = 1f;
        rectTransform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        PositionOnCanvas(screenPosition, rootCanvas, eventCamera);
    }

    public void HideFor(InventorySlotUI slot)
    {
        if (activeSlot != null && slot != null && activeSlot != slot)
            return;

        Hide();
    }

    void AttachToCanvas(Canvas rootCanvas)
    {
        if (rootCanvas == null)
            return;

        var canvasTransform = rootCanvas.transform;
        if (transform.parent != canvasTransform)
            transform.SetParent(canvasTransform, false);

        rectTransform.localScale = Vector3.one;
        rectTransform.SetAsLastSibling();
    }

    void ConfigureRoot()
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.sizeDelta = new Vector2(TooltipWidth, 0f);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        backgroundImage.color = new Color32(24, 27, 33, 235);
        backgroundImage.raycastTarget = false;

        layoutGroup.padding = new RectOffset(14, 14, 12, 12);
        layoutGroup.spacing = 8f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureTextChildren()
    {
        titleText = CreateTextChild("TitleText", 22f, FontStyles.Bold, new Color32(248, 241, 222, 255));
        bodyText = CreateTextChild("BodyText", 18f, FontStyles.Normal, new Color32(227, 231, 237, 255));
    }

    TextMeshProUGUI CreateTextChild(string childName, float fontSize, FontStyles fontStyle, Color color)
    {
        var child = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        child.transform.SetParent(transform, false);

        var text = child.GetComponent<TextMeshProUGUI>();
        var defaultFont = ResolveDefaultFont();
        if (defaultFont != null)
            text.font = defaultFont;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        var layoutElement = child.GetComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;

        return text;
    }

    static TMP_FontAsset ResolveDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    void ApplySlot(InventorySlotData slotData)
    {
        titleText.text = ResolveItemTitle(slotData.item);
        bodyText.text = BuildBody(slotData);
    }

    void PositionOnCanvas(Vector2 screenPosition, Canvas rootCanvas, Camera eventCamera)
    {
        if (rootCanvas == null || rootCanvas.transform is not RectTransform canvasRect)
            return;

        var cameraToUse = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cameraToUse, out var localPosition))
        {
            rectTransform.position = screenPosition;
            return;
        }

        rectTransform.anchoredPosition = localPosition + PointerOffset;
        ClampToCanvas(canvasRect);
    }

    void ClampToCanvas(RectTransform canvasRect)
    {
        Vector2 size = rectTransform.rect.size;
        Vector2 anchoredPosition = rectTransform.anchoredPosition;

        float halfWidth = canvasRect.rect.width * 0.5f;
        float halfHeight = canvasRect.rect.height * 0.5f;

        float minX = -halfWidth + CanvasPadding.x;
        float maxX = halfWidth - size.x - CanvasPadding.x;
        if (maxX < minX)
            maxX = minX;

        float minY = -halfHeight + size.y + CanvasPadding.y;
        float maxY = halfHeight - CanvasPadding.y;
        if (maxY < minY)
            minY = maxY;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    void Hide()
    {
        activeSlot = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    static string ResolveItemTitle(ItemDefinition item)
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName.Trim();

        return item.name;
    }

    static string BuildBody(InventorySlotData slotData)
    {
        if (slotData == null || slotData.IsEmpty || slotData.item == null)
            return string.Empty;

        var builder = new StringBuilder();
        var item = slotData.item;

        builder.Append("Type: ").AppendLine(item.itemType.ToString());

        if (!slotData.HasWeaponInstance && slotData.quantity > 1)
            builder.Append("Quantity: ").AppendLine(slotData.quantity.ToString());

        if (slotData.HasWeaponInstance && slotData.weaponInstance != null)
        {
            builder.Append("Rarity: ").AppendLine(slotData.weaponInstance.rarity.ToString());
            AppendWeaponUpgradeSummary(builder, slotData.weaponInstance);
        }

        if (item is GunConfig gunConfig)
        {
            builder.Append("Damage: ").AppendLine(FormatNumber(gunConfig.damage));
            builder.Append("Fire Rate: ").AppendLine(FormatNumber(gunConfig.fireRate));
            builder.Append("Magazine: ").AppendLine(ResolveMagazineText(slotData, gunConfig));
        }

        AppendWeaponAffixes(builder, slotData);
        AppendWeaponUpgradeMilestones(builder, slotData, item as GunConfig);

        if (!string.IsNullOrWhiteSpace(item.description))
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(item.description.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    static void AppendWeaponUpgradeSummary(StringBuilder builder, WeaponInstanceData weaponInstance)
    {
        if (weaponInstance == null)
            return;

        if (weaponInstance.upgradeLevel > 0)
            builder.Append("Upgrade: +").AppendLine(weaponInstance.upgradeLevel.ToString());

        if (weaponInstance.upgradeTier > 0)
            builder.Append("Tier: ").AppendLine(weaponInstance.upgradeTier.ToString());
    }

    static string ResolveMagazineText(InventorySlotData slotData, GunConfig gunConfig)
    {
        int maxMagazine = Mathf.Max(gunConfig.maxMagazine, gunConfig.magazine);
        if (slotData != null && slotData.weaponInstance != null && slotData.weaponInstance.currentMagazine >= 0)
            return $"{slotData.weaponInstance.currentMagazine}/{maxMagazine}";

        return maxMagazine.ToString();
    }

    static string FormatNumber(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");
    }

    static void AppendWeaponAffixes(StringBuilder builder, InventorySlotData slotData)
    {
        if (slotData == null || !slotData.HasWeaponInstance || slotData.weaponInstance == null)
            return;

        bool hasAffix = false;
        var weaponInstance = slotData.weaponInstance;

        if (weaponInstance.mainAffix != null && !weaponInstance.mainAffix.IsEmpty)
        {
            AppendAffixHeader(builder);
            AppendAffixLine(builder, "Main", weaponInstance.mainAffix);
            hasAffix = true;
        }

        if (weaponInstance.subAffixes == null)
            return;

        int subIndex = 0;
        for (int i = 0; i < weaponInstance.subAffixes.Count; i++)
        {
            var rolledAffix = weaponInstance.subAffixes[i];
            if (rolledAffix == null || rolledAffix.IsEmpty)
                continue;

            if (!hasAffix)
            {
                AppendAffixHeader(builder);
                hasAffix = true;
            }

            subIndex++;
            AppendAffixLine(builder, $"Sub {subIndex}", rolledAffix);
        }
    }

    static void AppendWeaponUpgradeMilestones(
        StringBuilder builder,
        InventorySlotData slotData,
        GunConfig gunConfig)
    {
        if (builder == null ||
            slotData == null ||
            !slotData.HasWeaponInstance ||
            slotData.weaponInstance == null ||
            !gunConfig ||
            gunConfig.upgradeCurve == null ||
            slotData.weaponInstance.upgradeLevel <= 0)
        {
            return;
        }

        var activeMilestones = new List<WeaponUpgradeMilestone>();
        gunConfig.upgradeCurve.GetActiveMilestones(
            activeMilestones,
            gunConfig.WeaponType,
            slotData.weaponInstance.upgradeLevel);

        if (activeMilestones.Count == 0)
            return;

        if (builder.Length > 0)
            builder.AppendLine();

        builder.AppendLine("Upgrade Milestones:");

        for (int i = 0; i < activeMilestones.Count; i++)
        {
            var milestone = activeMilestones[i];
            if (milestone == null)
                continue;

            string milestoneName = ResolveUpgradeMilestoneName(milestone, i);
            builder.Append("- ").Append(milestoneName);

            string milestoneSummary = BuildUpgradeMilestoneSummary(milestone);
            if (!string.IsNullOrWhiteSpace(milestoneSummary))
                builder.Append(" (").Append(milestoneSummary).Append(')');

            string description = ResolveUpgradeDescription(milestone.description);
            if (!string.IsNullOrWhiteSpace(description))
                builder.Append(" - ").Append(description);

            builder.AppendLine();

            AppendUpgradeMilestoneEffects(builder, milestone);
        }
    }

    static void AppendUpgradeMilestoneEffects(StringBuilder builder, WeaponUpgradeMilestone milestone)
    {
        if (builder == null || milestone == null || milestone.effects == null)
            return;

        for (int i = 0; i < milestone.effects.Count; i++)
        {
            var effect = milestone.effects[i];
            if (effect == null || effect.effectType == WeaponUpgradeEffectType.None)
                continue;

            string effectName = ResolveUpgradeEffectName(effect, milestone, i);
            string effectSummary = BuildUpgradeEffectSummary(effect);
            string effectDescription = ResolveUpgradeDescription(effect.description);

            builder.Append("  - ").Append(effectName);

            if (!string.IsNullOrWhiteSpace(effectSummary))
                builder.Append(": ").Append(effectSummary);

            if (!string.IsNullOrWhiteSpace(effectDescription))
                builder.Append(" - ").Append(effectDescription);

            builder.AppendLine();
        }
    }

    static string ResolveUpgradeMilestoneName(WeaponUpgradeMilestone milestone, int index)
    {
        if (milestone == null)
            return "Unknown Milestone";

        if (!string.IsNullOrWhiteSpace(milestone.displayName))
            return milestone.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(milestone.milestoneId))
            return milestone.milestoneId.Trim();

        return $"+{Mathf.Max(1, milestone.requiredLevel)}";
    }

    static string ResolveUpgradeEffectName(WeaponUpgradeEffect effect, WeaponUpgradeMilestone milestone, int index)
    {
        if (effect == null)
            return "Unknown Effect";

        if (!string.IsNullOrWhiteSpace(effect.displayName))
            return effect.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(effect.effectId))
            return effect.effectId.Trim();

        return MakeReadable(effect.effectType.ToString());
    }

    static string ResolveUpgradeDescription(string description)
    {
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    }

    static string BuildUpgradeMilestoneSummary(WeaponUpgradeMilestone milestone)
    {
        if (milestone == null)
            return string.Empty;

        if (!milestone.increaseTier)
            return string.Empty;

        return $"+{Mathf.Max(1, milestone.tierIncreaseAmount)} Tier";
    }

    static string BuildUpgradeEffectSummary(WeaponUpgradeEffect effect)
    {
        if (effect == null)
            return string.Empty;

        return effect.effectType switch
        {
            WeaponUpgradeEffectType.StatModifier => BuildUpgradeStatModifierSummary(effect),
            WeaponUpgradeEffectType.ExtraProjectileChance => $"{FormatNumber(effect.procChance * 100f)}% extra projectile chance",
            WeaponUpgradeEffectType.SpecialProjectileEveryNthShot => $"Every {Mathf.Max(1, effect.requiredShots)} shots",
            WeaponUpgradeEffectType.TimedBuffOnReload => $"On reload: {BuildUpgradeStatModifierSummary(effect)} for {FormatNumber(effect.buffDurationSeconds)}s",
            _ => string.Empty
        };
    }

    static string BuildUpgradeStatModifierSummary(WeaponUpgradeEffect effect)
    {
        if (effect == null)
            return string.Empty;

        string statName = MakeReadable(effect.statType.ToString());

        return effect.modifierOp switch
        {
            ModifierOp.Flat => $"{FormatSignedNumber(effect.value)} {statName}",
            ModifierOp.AddPercent => $"{FormatSignedNumber(effect.value)}% {statName}",
            ModifierOp.Multiply => $"x{FormatNumber(effect.value)} {statName}",
            _ => $"{FormatSignedNumber(effect.value)} {statName}"
        };
    }

    static void AppendAffixHeader(StringBuilder builder)
    {
        if (builder.Length > 0)
            builder.AppendLine();

        builder.AppendLine("Affixes:");
    }

    static void AppendAffixLine(StringBuilder builder, string slotLabel, RolledAffixData rolledAffix)
    {
        if (rolledAffix == null || rolledAffix.IsEmpty)
            return;

        var definition = WeaponAffixDatabase.GetLoadedAffixById(rolledAffix.affixId);
        string affixName = ResolveAffixName(definition, rolledAffix.affixId);
        string affixSummary = BuildAffixSummary(definition, rolledAffix);

        builder.Append("- ").Append(slotLabel).Append(": ").Append(affixName);

        if (!string.IsNullOrWhiteSpace(affixSummary))
            builder.Append(" (").Append(affixSummary).Append(')');

        string affixDescription = ResolveAffixDescription(definition);
        if (!string.IsNullOrWhiteSpace(affixDescription))
            builder.Append(" - ").Append(affixDescription);

        builder.AppendLine();
    }

    static string ResolveAffixName(WeaponAffixDefinition definition, string affixId)
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.displayName))
            return definition.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(affixId))
            return affixId.Trim();

        return "Unknown Affix";
    }

    static string ResolveAffixDescription(WeaponAffixDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.description))
            return string.Empty;

        string description = definition.description.Trim();
        if (!string.IsNullOrWhiteSpace(definition.displayName) &&
            string.Equals(description, definition.displayName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return description;
    }

    static string BuildAffixSummary(WeaponAffixDefinition definition, RolledAffixData rolledAffix)
    {
        if (rolledAffix == null)
            return string.Empty;

        if (definition == null)
        {
            if (rolledAffix.hasPrimaryValue)
                return FormatSignedNumber(rolledAffix.primaryValue);

            return string.Empty;
        }

        return definition.behaviorType switch
        {
            WeaponAffixBehaviorType.StatModifier => BuildStatModifierSummary(definition, rolledAffix),
            WeaponAffixBehaviorType.TimedBuffOnReload => $"On reload: {BuildStatModifierSummary(definition, rolledAffix)} for {FormatNumber(definition.buffDurationSeconds)}s",
            WeaponAffixBehaviorType.SpecialProjectileEveryNthShot => BuildSpecialProjectileSummary(definition),
            _ => rolledAffix.hasPrimaryValue ? FormatSignedNumber(rolledAffix.primaryValue) : string.Empty
        };
    }

    static string BuildStatModifierSummary(WeaponAffixDefinition definition, RolledAffixData rolledAffix)
    {
        float value = definition.ResolvePrimaryValue(rolledAffix);
        string statName = MakeReadable(definition.statType.ToString());

        return definition.modifierOp switch
        {
            ModifierOp.Flat => $"{FormatSignedNumber(value)} {statName}",
            ModifierOp.AddPercent => $"{FormatSignedNumber(value)}% {statName}",
            ModifierOp.Multiply => $"x{FormatNumber(value)} {statName}",
            _ => $"{FormatSignedNumber(value)} {statName}"
        };
    }

    static string BuildSpecialProjectileSummary(WeaponAffixDefinition definition)
    {
        string summary = $"Every {Mathf.Max(1, definition.requiredShots)} shots";
        if (definition.procChance > 0f)
            summary += $" ({FormatNumber(definition.procChance * 100f)}% proc)";

        return summary;
    }

    static string FormatSignedNumber(float value)
    {
        string prefix = value >= 0f ? "+" : string.Empty;
        return prefix + FormatNumber(value);
    }

    static string MakeReadable(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var builder = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char current = raw[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(raw[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}
