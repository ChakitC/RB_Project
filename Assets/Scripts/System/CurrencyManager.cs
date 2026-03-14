using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetCurrentGold(PlayerInventory playerInventory)
    {
        if (!playerInventory) return 0;
        var currentGold = playerInventory.Gold;
        return currentGold;
    }

    public void GrantGold(PlayerInventory playerInventory, int amount)
    {
        if (playerInventory == null) return;
        playerInventory.AddGold(amount);
    }

    public void SpendGold(PlayerInventory playerInventory, int amount)
    {
        if (playerInventory == null) return;
        playerInventory.SpendGold(amount);
    }
}








