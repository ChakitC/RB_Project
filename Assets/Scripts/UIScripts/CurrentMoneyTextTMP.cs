using TMPro;
using UnityEngine;

public enum CurrencyDisplayKind
{
    Gold,
    Scrap
}

public class CurrentMoneyTextTMP : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private TMP_Text text;
    [SerializeField] private CurrencyDisplayKind currency = CurrencyDisplayKind.Gold;
    [SerializeField] private string prefix = "";

    private void Awake()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (!inventory) return;

        switch (currency)
        {
            case CurrencyDisplayKind.Scrap:
                inventory.OnScrapChanged += UpdateText;
                break;

            default:
                inventory.OnGoldChanged += UpdateText;
                break;
        }

        UpdateText(GetCurrentAmount());
    }

    private void OnDisable()
    {
        if (!inventory) return;

        switch (currency)
        {
            case CurrencyDisplayKind.Scrap:
                inventory.OnScrapChanged -= UpdateText;
                break;

            default:
                inventory.OnGoldChanged -= UpdateText;
                break;
        }
    }

    private void UpdateText(int amount)
    {
        if (!text) return;
        text.text = prefix + amount.ToString("N0");
    }

    int GetCurrentAmount()
    {
        if (!inventory)
            return 0;

        return currency == CurrencyDisplayKind.Scrap ? inventory.Scrap : inventory.Gold;
    }
}
