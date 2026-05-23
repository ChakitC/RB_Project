using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopSpawner : MonoBehaviour
{
    [Serializable]
    public sealed class ShopTierEntry
    {
        [Tooltip("หมายเลข Tier/Tire ของร้าน ใช้ดูใน Inspector และ Debug เท่านั้น")]
        [Min(1)] public int tier = 1;

        [Tooltip("Prefab ร้านของ Tier/Tire นี้ เลือกจาก Assets/Prefab/Shop")]
        public GameObject prefab;

        [Tooltip("น้ำหนักสำหรับสุ่ม Tier/Tire นี้ ยิ่งมากยิ่งมีโอกาสถูกเลือก")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("การเกิดร้าน")]
    [SerializeField, Tooltip("ให้สุ่มเกิดร้านอัตโนมัติเมื่อเริ่มด่าน")]
    private bool spawnOnStart = true;

    [SerializeField, Range(0f, 1f), Tooltip("โอกาสที่จะมีร้านในด่านนี้ 0 = ไม่เกิดเลย, 1 = เกิดแน่นอน")]
    private float spawnChance = 0.35f;

    [SerializeField, Tooltip("จุดที่จะใช้เป็นตำแหน่งและทิศทางเกิด ถ้าไม่ใส่จะใช้ Transform ของ ShopSpawner")]
    private Transform spawnPoint;

    [Header("ตำแหน่งที่บันทึกไว้")]
    [SerializeField, Tooltip("ใช้ตำแหน่งและทิศทาง local ที่บันทึกไว้ตอน spawn จริง ถ้าปิดจะใช้ Spawn Point หรือ Transform ของ ShopSpawner")]
    private bool useSavedSpawnTransform;

    [SerializeField, Tooltip("ตำแหน่ง local ที่บันทึกไว้จาก Shop ตัวอย่าง โดยอิงจาก ShopSpawner")]
    private Vector3 savedSpawnPosition;

    [SerializeField, Tooltip("ทิศทาง local ที่บันทึกไว้จาก Shop ตัวอย่าง โดยอิงจาก ShopSpawner")]
    private Vector3 savedSpawnEulerAngles;

    [SerializeField, Tooltip("Parent ของร้านที่ spawn ออกมา ถ้าไม่ใส่จะเป็นลูกของ ShopSpawner")]
    private Transform parentOverride;

    [SerializeField, Tooltip("ถ้ามีร้านเกิดจาก Spawner นี้แล้ว จะไม่ spawn ซ้ำ")]
    private bool preventDuplicateSpawn = true;

    [Header("สุ่ม Tier/Tire")]
    [SerializeField, Tooltip("รายการ Tier/Tire ของร้านและ prefab ที่จะสุ่ม")]
    private List<ShopTierEntry> tierEntries = new();

    [Header("Debug")]
    [SerializeField, Tooltip("แสดง log ผลการสุ่มร้านใน Console")]
    private bool logResult;

#if UNITY_EDITOR
    [Header("เครื่องมือจัดตำแหน่ง")]
    [SerializeField, Tooltip("Tier/Tire ที่จะใช้สร้าง Shop ตัวอย่างเพื่อจัดตำแหน่ง ถ้าไม่เจอ Tier นี้จะใช้รายการแรกที่ valid")]
    private int previewTier = 1;

    [SerializeField, Tooltip("Shop ตัวอย่างใน Scene ที่ใช้เลื่อน/หมุนเพื่อบันทึกตำแหน่ง ไม่ได้ใช้ตอนเล่นจริง")]
    private GameObject previewShop;
#endif

    private GameObject spawnedShop;
    private int spawnedTier;

    public GameObject SpawnedShop => spawnedShop;
    public int SpawnedTier => spawnedTier;
    public Transform ParentOverride => parentOverride;
#if UNITY_EDITOR
    public int EditorPreviewTier => previewTier;
#endif

    void Start()
    {
#if UNITY_EDITOR
        ClearEditorPreviewBeforeRuntime();
#endif

        if (spawnOnStart)
            TrySpawn();
    }

#if UNITY_EDITOR
    void ClearEditorPreviewBeforeRuntime()
    {
        if (!Application.isPlaying || previewShop == null)
            return;

        previewShop.SetActive(false);
        Destroy(previewShop);
        previewShop = null;
    }
#endif

    void OnValidate()
    {
        spawnChance = Mathf.Clamp01(spawnChance);

        if (tierEntries == null)
            return;

        for (int i = 0; i < tierEntries.Count; i++)
        {
            var entry = tierEntries[i];
            if (entry == null)
                continue;

            entry.tier = Mathf.Max(1, entry.tier);
            entry.weight = Mathf.Max(0f, entry.weight);
        }
    }

    [ContextMenu("Try Spawn Shop")]
    public void TrySpawnShopFromContextMenu()
    {
        TrySpawn();
    }

    public bool TrySpawn()
    {
        if (preventDuplicateSpawn && spawnedShop != null)
        {
            Log("ShopSpawner: มีร้านเกิดอยู่แล้ว จึงไม่ spawn ซ้ำ");
            return false;
        }

        if (UnityEngine.Random.value > spawnChance)
        {
            Log("ShopSpawner: สุ่มแล้วด่านนี้ไม่มีร้าน");
            return false;
        }

        if (!TryRollTier(out ShopTierEntry selectedEntry))
            return false;

        Transform spawnParent = ResolveSpawnParent();
        spawnedShop = Instantiate(selectedEntry.prefab, spawnParent);
        ApplySpawnTransform(spawnedShop.transform, spawnParent);

        spawnedTier = selectedEntry.tier;
        spawnedShop.name = $"{selectedEntry.prefab.name}_Spawned_Tier_{spawnedTier}";

        Log($"ShopSpawner: Spawn ร้าน Tier/Tire {spawnedTier} จาก prefab {selectedEntry.prefab.name}");
        return true;
    }

    bool TryRollTier(out ShopTierEntry selectedEntry)
    {
        selectedEntry = null;

        float totalWeight = 0f;
        if (tierEntries != null)
        {
            for (int i = 0; i < tierEntries.Count; i++)
            {
                var entry = tierEntries[i];
                if (IsValidEntry(entry))
                    totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("ShopSpawner: ไม่มี Tier/Tire ที่ตั้งค่า prefab และ weight มากกว่า 0", this);
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < tierEntries.Count; i++)
        {
            var entry = tierEntries[i];
            if (!IsValidEntry(entry))
                continue;

            currentWeight += entry.weight;
            if (roll <= currentWeight)
            {
                selectedEntry = entry;
                return true;
            }
        }

        selectedEntry = FindLastValidEntry();
        return selectedEntry != null;
    }

    public Vector3 ResolveSpawnPosition()
    {
        if (useSavedSpawnTransform)
            return transform.TransformPoint(savedSpawnPosition);

        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        return spawnTransform.position;
    }

    public Quaternion ResolveSpawnRotation()
    {
        if (useSavedSpawnTransform)
            return transform.rotation * Quaternion.Euler(savedSpawnEulerAngles);

        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        return spawnTransform.rotation;
    }

    void ApplySpawnTransform(Transform spawnedTransform, Transform spawnParent)
    {
        if (spawnedTransform == null)
            return;

        spawnedTransform.localPosition = ResolveLocalSpawnPosition(spawnParent);
        spawnedTransform.localRotation = ResolveLocalSpawnRotation(spawnParent);
    }

    Vector3 ResolveLocalSpawnPosition(Transform spawnParent)
    {
        if (useSavedSpawnTransform && spawnParent == transform)
            return savedSpawnPosition;

        Vector3 worldPosition = ResolveSpawnPosition();
        return spawnParent != null ? spawnParent.InverseTransformPoint(worldPosition) : worldPosition;
    }

    Quaternion ResolveLocalSpawnRotation(Transform spawnParent)
    {
        if (useSavedSpawnTransform && spawnParent == transform)
            return Quaternion.Euler(savedSpawnEulerAngles);

        Quaternion worldRotation = ResolveSpawnRotation();
        return spawnParent != null ? Quaternion.Inverse(spawnParent.rotation) * worldRotation : worldRotation;
    }

    public void SaveSpawnTransform(Vector3 position, Quaternion rotation)
    {
        savedSpawnPosition = transform.InverseTransformPoint(position);
        savedSpawnEulerAngles = (Quaternion.Inverse(transform.rotation) * rotation).eulerAngles;
        useSavedSpawnTransform = true;
    }

    public Transform ResolveSpawnParent()
    {
        return parentOverride != null ? parentOverride : transform;
    }

    public bool TryGetTierEntry(int tier, out ShopTierEntry selectedEntry)
    {
        selectedEntry = null;

        if (tierEntries == null)
            return false;

        for (int i = 0; i < tierEntries.Count; i++)
        {
            var entry = tierEntries[i];
            if (IsValidEntry(entry) && entry.tier == tier)
            {
                selectedEntry = entry;
                return true;
            }
        }

        return false;
    }

    public bool TryGetFirstValidTierEntry(out ShopTierEntry selectedEntry)
    {
        selectedEntry = null;

        if (tierEntries == null)
            return false;

        for (int i = 0; i < tierEntries.Count; i++)
        {
            var entry = tierEntries[i];
            if (IsValidEntry(entry))
            {
                selectedEntry = entry;
                return true;
            }
        }

        return false;
    }

    ShopTierEntry FindLastValidEntry()
    {
        if (tierEntries == null)
            return null;

        for (int i = tierEntries.Count - 1; i >= 0; i--)
        {
            var entry = tierEntries[i];
            if (IsValidEntry(entry))
                return entry;
        }

        return null;
    }

    static bool IsValidEntry(ShopTierEntry entry)
    {
        return entry != null && entry.prefab != null && entry.weight > 0f;
    }

    void Log(string message)
    {
        if (logResult)
            Debug.Log(message, this);
    }
}
