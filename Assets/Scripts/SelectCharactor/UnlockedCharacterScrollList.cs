using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class UnlockedCharacterScrollList : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterDatabase database;
    [SerializeField] private bool rebuildOnEnable = true;

    [Header("UI")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private CharacterRosterSlotUI slotPrefab;

    [Header("Drag Settings")]
    [SerializeField] private SpawnOnDragOutButton dragSettingsSource;

    readonly List<CharacterRosterSlotUI> spawnedSlots = new();

    void Awake()
    {
        EnsureRefs();
    }

    void OnEnable()
    {
        CharacterUnlockService.CharacterUnlocked += HandleCharacterUnlocked;

        if (rebuildOnEnable)
            Rebuild();
    }

    void Start()
    {
        if (spawnedSlots.Count == 0)
            Rebuild();
    }

    void OnDisable()
    {
        CharacterUnlockService.CharacterUnlocked -= HandleCharacterUnlocked;
    }

    public void Rebuild()
    {
        EnsureRefs();
        ClearSpawnedSlots();

        if (database == null)
        {
            Debug.LogWarning("[UnlockedCharacterScrollList] CharacterDatabase is missing.", this);
            return;
        }

        if (contentRoot == null)
        {
            Debug.LogWarning("[UnlockedCharacterScrollList] Content root is missing.", this);
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogWarning("[UnlockedCharacterScrollList] Slot prefab is missing.", this);
            return;
        }

        IReadOnlyList<CharacterStats> characters = database.characters;
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterStats character = characters[i];
            if (character == null)
                continue;

            if (!CharacterUnlockService.IsUnlockedForSelection(character))
                continue;

            CharacterRosterSlotUI slot = Instantiate(slotPrefab, contentRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(character, dragSettingsSource, scrollRect);
            spawnedSlots.Add(slot);
        }

        if (contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    void EnsureRefs()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (contentRoot == null && scrollRect != null)
            contentRoot = scrollRect.content;

        if (contentRoot == null)
            contentRoot = transform as RectTransform;

        if (database == null)
            database = ResolveLoadedDatabase();
    }

    void ClearSpawnedSlots()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            CharacterRosterSlotUI slot = spawnedSlots[i];
            if (slot != null)
                SafeDestroy(slot.gameObject);
        }

        spawnedSlots.Clear();

        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child == null)
                continue;

            CharacterRosterSlotUI slot = child.GetComponent<CharacterRosterSlotUI>();
            if (slot == null)
                continue;

            SafeDestroy(child.gameObject);
        }
    }

    void HandleCharacterUnlocked(string characterId)
    {
        Rebuild();
    }

    static CharacterDatabase ResolveLoadedDatabase()
    {
        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase candidate = databases[i];
            if (candidate != null && candidate.characters != null && candidate.characters.Count > 0)
                return candidate;
        }

        return null;
    }

    static void SafeDestroy(GameObject go)
    {
        if (go == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(go);
        else
            Object.Destroy(go);
#else
        Object.Destroy(go);
#endif
    }
}
