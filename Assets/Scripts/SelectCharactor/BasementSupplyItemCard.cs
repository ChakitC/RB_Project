using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BasementSupplyItemCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text fallback;
    [SerializeField] private GameObject selection;
    private BasementSupplyScreenController owner;
    public ItemDefinition Item { get; private set; }

    void Awake() => button.onClick.AddListener(Select);
    void OnDestroy() => button.onClick.RemoveListener(Select);

    public void Bind(BasementSupplyScreenController screen, ItemDefinition item)
    {
        owner = screen;
        Item = item;
        icon.sprite = item != null ? item.icon : null;
        icon.enabled = icon.sprite != null;
        fallback.gameObject.SetActive(!icon.enabled);
        fallback.text = item != null ? item.displayName : "—";
        SetSelected(false);
    }

    public void SetSelected(bool selected) => selection.SetActive(selected);
    void Select() => owner.SelectItem(Item);
}
