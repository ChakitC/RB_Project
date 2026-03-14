using UnityEngine;

public class XpManager : MonoBehaviour
{
    public static XpManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GrantXp(LevelSystem receiver, int amount)
    {
        if (receiver == null || amount <= 0) return;
        receiver.AddXp(amount);
    }
}