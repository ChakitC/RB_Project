using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only supply browser. Never creates inventory instances or invokes purchases.</summary>
public sealed class BasementSupplyScreenController : MonoBehaviour
{
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private BasementSupplyItemCard cardPrefab;
    [SerializeField] private RectTransform content;
    [SerializeField] private ScrollRect itemScroll;
    [SerializeField] private ScrollRect descriptionScroll;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailFallback;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject[] categorySelections;
    [SerializeField] private RectTransform[] categoryTabs;
    [SerializeField] private Vector2[] categoryExpandedPositions;
    [SerializeField] private RectTransform categoryPaper;
    [SerializeField] private List<BasementSupplyItemCard> cards = new();
    private readonly List<ItemDefinition> visibleItems = new();
    public bool IsOpen => gameObject.activeSelf;
    public ItemDefinition SelectedItem { get; private set; }
    public int VisibleItemCount => visibleItems.Count;
    public int Category { get; private set; }
    public event System.Action CloseRequested;

    void OnEnable() => SetCategory(0);
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) RequestClose();
    }

    public void Open()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else SetCategory(0);
    }

    public void Close() => gameObject.SetActive(false);

    public void RequestClose()
    {
        if (CloseRequested != null) CloseRequested.Invoke();
        else Close();
    }

    // 0 = all, 1 = weapons, 2 = accessories. Preserve database ordering.
    public void SetCategory(int category)
    {
        Category = Mathf.Clamp(category, 0, 2);
        visibleItems.Clear();
        var seen = new HashSet<ItemDefinition>();
        if (itemDatabase != null)
        {
            foreach (var item in itemDatabase.items)
            {
                if (item == null || !seen.Add(item)) continue;
                if (Category == 1 && item.itemType != ItemType.Weapon) continue;
                if (Category == 2 && item.itemType != ItemType.Accessory) continue;
                visibleItems.Add(item);
            }
        }

        if (cardPrefab == null || content == null) return;
        cards.RemoveAll(card => card == null);
        while (cards.Count < visibleItems.Count)
            cards.Add(Instantiate(cardPrefab, content));
        for (int i = 0; i < cards.Count; i++)
        {
            bool visible = i < visibleItems.Count;
            cards[i].gameObject.SetActive(visible);
            if (visible) cards[i].Bind(this, visibleItems[i]);
        }
        for (int i = 0; i < categorySelections.Length; i++)
            categorySelections[i].SetActive(i == Category);
        if (categoryTabs != null && categoryExpandedPositions != null)
        {
            for (int i = 0; i < categoryTabs.Length && i < categoryExpandedPositions.Length; i++)
            {
                bool selected = i == Category;
                categoryTabs[i].anchoredPosition = categoryExpandedPositions[i] + (selected ? Vector2.zero : new Vector2(55, i == 0 ? 0 : -36));
                categoryTabs[i].localScale = Vector3.one * (selected ? 1f : 110f / 154f);
            }
            // Selected tab overlaps the other tabs, but stays behind the paper.
            if (categoryPaper != null && Category < categoryTabs.Length)
            {
                for (int i = categoryTabs.Length - 1; i >= 0; i--)
                    categoryTabs[i].SetSiblingIndex(categoryPaper.GetSiblingIndex() - (categoryTabs[i].GetSiblingIndex() < categoryPaper.GetSiblingIndex() ? 1 : 0));
                var selected = categoryTabs[Category];
                selected.SetSiblingIndex(categoryPaper.GetSiblingIndex() - (selected.GetSiblingIndex() < categoryPaper.GetSiblingIndex() ? 1 : 0));
            }
        }
        emptyState.SetActive(visibleItems.Count == 0);
        SelectItem(visibleItems.Count > 0 ? visibleItems[0] : null);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        itemScroll.StopMovement();
        itemScroll.verticalNormalizedPosition = 1f;
    }

    public void SelectItem(ItemDefinition item)
    {
        if (item != null && !visibleItems.Contains(item)) return;
        SelectedItem = item;
        foreach (var card in cards) card.SetSelected(item != null && card.Item == item);
        detailIcon.sprite = item != null ? item.icon : null;
        detailIcon.enabled = detailIcon.sprite != null;
        detailFallback.gameObject.SetActive(!detailIcon.enabled);
        detailFallback.text = item == null ? "—" : "ไม่มีรูป";
        nameText.text = item == null ? "ไม่มีไอเทมในหมวดนี้" :
            string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
        descriptionText.text = item == null ? string.Empty :
            string.IsNullOrWhiteSpace(item.description) ? "ยังไม่มีคำอธิบาย" : item.description;
        priceText.text = item == null ? string.Empty : $"ราคาพื้นฐาน  {Mathf.Max(0, item.baseBuyPrice):N0} GOLD";
        buyButton.interactable = false;
        Canvas.ForceUpdateCanvases();
        descriptionScroll.StopMovement();
        descriptionScroll.verticalNormalizedPosition = 1f;
    }
}
