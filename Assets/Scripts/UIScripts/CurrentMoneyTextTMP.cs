using TMPro;
using UnityEngine;

public class CurrentMoneyTextTMP : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private TMP_Text text;
    [SerializeField] private string prefix = ""; // เช่น "Gold: "

    private void Awake()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!inventory) inventory = FindFirstObjectByType<PlayerInventory>(); // Unity 2022+
        // ถ้า Unity เวอร์ชันเก่า ใช้ FindObjectOfType<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (!inventory) return;
        inventory.OnGoldChanged += UpdateText;
        UpdateText(inventory.Gold); // อัปเดตทันทีตอนเปิด
    }

    private void OnDisable()
    {
        if (!inventory) return;
        inventory.OnGoldChanged -= UpdateText;
    }

    private void UpdateText(int gold)
    {
        if (!text) return;
        text.text = prefix + gold.ToString("N0"); // 1,000 / 10,000
    }
}