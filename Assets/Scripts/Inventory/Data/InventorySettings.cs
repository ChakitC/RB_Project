using UnityEngine;

[CreateAssetMenu(fileName = "InventorySettings", menuName = "Game/Inventory/Settings")]
public sealed class InventorySettings : ScriptableObject
{
    public const int FallbackDefaultSlotCount = 40;

    const string ResourcesPath = "GameSettings/InventorySettings";

    [SerializeField, Min(1)] private int defaultSlotCount = FallbackDefaultSlotCount;

    static InventorySettings cachedInstance;
    static bool attemptedLoad;
    static bool loggedMissingSettings;

    public int DefaultSlotCount => Mathf.Max(1, defaultSlotCount);

    public static int ResolveDefaultSlotCount()
    {
        if (!attemptedLoad)
        {
            cachedInstance = Resources.Load<InventorySettings>(ResourcesPath);
            attemptedLoad = true;
        }

        if (cachedInstance != null)
            return cachedInstance.DefaultSlotCount;

        if (!loggedMissingSettings)
        {
            Debug.LogWarning(
                $"[InventorySettings] Missing Resources/{ResourcesPath}.asset. " +
                $"Using fallback capacity {FallbackDefaultSlotCount}.");
            loggedMissingSettings = true;
        }

        return FallbackDefaultSlotCount;
    }

    void OnValidate()
    {
        defaultSlotCount = Mathf.Max(1, defaultSlotCount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeCache()
    {
        cachedInstance = null;
        attemptedLoad = false;
        loggedMissingSettings = false;
    }
}
