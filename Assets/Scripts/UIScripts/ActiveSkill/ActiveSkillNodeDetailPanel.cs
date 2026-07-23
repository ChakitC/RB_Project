using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillNodeDetailPanel : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text requirementText;
    [SerializeField] TMP_Text statPreviewText;
    [SerializeField] Button unlockButton;
    [SerializeField] TMP_Text unlockButtonText;

    Action _unlock;

    void Awake()
    {
        if (unlockButton != null)
            unlockButton.onClick.AddListener(HandleUnlock);
    }

    void OnDestroy()
    {
        if (unlockButton != null)
            unlockButton.onClick.RemoveListener(HandleUnlock);
    }

    public void Hide()
    {
        _unlock = null;
        gameObject.SetActive(false);
    }

    public void Show(
        SkillUpgradeNodeData node,
        bool unlocked,
        bool canUnlock,
        string reason,
        FinalSkillStats before,
        FinalSkillStats after,
        Action unlock)
    {
        if (node == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        _unlock = canUnlock ? unlock : null;

        if (titleText != null)
            titleText.text = node.ResolvedDisplayName;
        if (descriptionText != null)
            descriptionText.text = node.description;
        if (requirementText != null)
            requirementText.text = BuildRequirementText(node, unlocked, reason);
        if (statPreviewText != null)
            statPreviewText.text = BuildStatPreview(before, after);
        if (unlockButton != null)
            unlockButton.interactable = canUnlock;
        if (unlockButtonText != null)
            unlockButtonText.text = unlocked ? "Unlocked" : $"Unlock ({Mathf.Max(1, node.cost)})";
    }

    void HandleUnlock()
    {
        _unlock?.Invoke();
    }

    static string BuildRequirementText(SkillUpgradeNodeData node, bool unlocked, string reason)
    {
        if (unlocked)
            return "Unlocked";

        var builder = new StringBuilder();
        builder.Append("Character level ").Append(Mathf.Max(1, node.requiredCharacterLevel));
        if (node.requiredNodeIds != null && node.requiredNodeIds.Count > 0)
            builder.Append(" | Requires: ").Append(string.Join(", ", node.requiredNodeIds));
        if (!string.IsNullOrWhiteSpace(reason))
            builder.AppendLine().Append(reason);
        return builder.ToString();
    }

    static string BuildStatPreview(FinalSkillStats before, FinalSkillStats after)
    {
        if (before == null || after == null)
            return string.Empty;

        return
            $"Damage {before.damage:0.##} > {after.damage:0.##}\n" +
            $"Cooldown {before.cooldown:0.##}s > {after.cooldown:0.##}s\n" +
            $"Cost {before.manaCost:0.##} > {after.manaCost:0.##}\n" +
            $"Cast Time {before.castTime:0.##}s > {after.castTime:0.##}s\n" +
            $"Radius {before.areaRadius:0.##} > {after.areaRadius:0.##}\n" +
            $"Projectiles {before.projectileCount} > {after.projectileCount}\n" +
            $"Crit {before.critChance:0.##}% > {after.critChance:0.##}%\n" +
            $"Stagger {before.staggerPower:0.##} > {after.staggerPower:0.##}";
    }
}
