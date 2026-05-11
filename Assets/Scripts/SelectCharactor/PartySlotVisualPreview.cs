using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class PartySlotVisualPreview : MonoBehaviour
{
    [Header("Root Model")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private WeaponDatabase weaponDatabase;

    [Header("Weapon Preview")]
    [SerializeField] private string rightHandName = "hand.r";
    [SerializeField] private string leftHandName = "hand.l";

    [SerializeField] private Vector3 rightLocalPos;
    [SerializeField] private Vector3 rightLocalRotEuler;
    [SerializeField] private Vector3 rightLocalScale = Vector3.one;

    [SerializeField] private Vector3 leftLocalPos;
    [SerializeField] private Vector3 leftLocalRotEuler;
    [SerializeField] private Vector3 leftLocalScale = Vector3.one;

    [Header("Runtime")]
    [SerializeField] private Animator animator;

    private GameObject currentModel;
    private GameObject rightWeaponObj;
    private GameObject leftWeaponObj;
    private int currentSlot;
    private int partyIndex;

    public Animator Animator => animator;

    public void SetPartyContext(int saveSlot, int partyMemberIndex)
    {
        currentSlot = saveSlot;
        partyIndex = partyMemberIndex;
    }

    public void BuildCharacter(CharacterStats selected, Transform slotRoot)
    {
        ClearTemporarySelectObjects(slotRoot);

        if (modelRoot == null)
        {
            Debug.LogError("[PartySlotVisualPreview] modelRoot is null", this);
            return;
        }

        ClearModelPreview();
        animator = null;

        if (selected == null)
        {
            Debug.LogWarning("[PartySlotVisualPreview] selected is null -> skip model build", this);
            return;
        }

        if (selected.CharacterPrefabBasement == null)
        {
            Debug.LogError($"[PartySlotVisualPreview] CharacterPrefabBasement is null (selected: {selected.name})", this);
            return;
        }

        currentModel = Instantiate(selected.CharacterPrefabBasement, modelRoot, false);
        currentModel.name = $"{selected.name}_Preview";

        var t = currentModel.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        animator = currentModel.GetComponentInChildren<Animator>(true);
        if (!animator)
            animator = currentModel.GetComponent<Animator>();

        if (!animator)
        {
            Debug.LogError("[PartySlotVisualPreview] new model has no Animator", currentModel);
            SafeDestroy(currentModel);
            currentModel = null;
            return;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        ResetPickedState(animator);

        RefreshWeapon(selected);
    }

    public void RefreshWeapon(CharacterStats selected)
    {
        ClearWeaponPreview();

        if (!animator || selected == null)
            return;

        bool useRightHand = ShouldUseRightHand(selected);
        bool useLeftHand = ShouldUseLeftHand(selected);

        if (!useRightHand && !useLeftHand)
            return;

        if (!TryResolveSelectedCharacterWeapon(selected, out var weapon) || weapon == null)
            return;

        if (useRightHand)
            rightWeaponObj = SpawnWeaponOnHand(weapon, rightHand: true);

        if (useLeftHand)
            leftWeaponObj = SpawnWeaponOnHand(weapon, rightHand: false);
    }

    public void ClearPreview()
    {
        ClearModelPreview();
    }

    public void ClearTemporarySelectObjects(Transform slotRoot)
    {
        ClearTemporarySelectObjectsUnder(slotRoot);

        if (modelRoot != null && modelRoot != slotRoot)
            ClearTemporarySelectObjectsUnder(modelRoot);
    }

    void ClearWeaponPreview()
    {
        if (rightWeaponObj)
            SafeDestroy(rightWeaponObj);

        if (leftWeaponObj)
            SafeDestroy(leftWeaponObj);

        rightWeaponObj = null;
        leftWeaponObj = null;
    }

    void ClearModelPreview()
    {
        ClearWeaponPreview();

        if (currentModel)
        {
            SafeDestroy(currentModel);
            currentModel = null;
        }

        if (modelRoot == null)
            return;

        for (int i = modelRoot.childCount - 1; i >= 0; i--)
        {
            var child = modelRoot.GetChild(i);
            if (!IsGeneratedPreviewRoot(child))
                continue;

            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }
    }

    bool IsGeneratedPreviewRoot(Transform child)
    {
        if (!child)
            return false;

        if (currentModel && child == currentModel.transform)
            return true;

        if (!child.name.EndsWith("_Preview", StringComparison.Ordinal))
            return false;

        return child.GetComponentInChildren<CharacterSelectable>(true) == null;
    }

    void ClearTemporarySelectObjectsUnder(Transform root)
    {
        if (!root)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (!IsTemporarySelectRoot(child))
                continue;

            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }
    }

    bool IsTemporarySelectRoot(Transform child)
    {
        if (!child)
            return false;

        if (currentModel && child == currentModel.transform)
            return false;

        if (child.name.EndsWith("_Preview", StringComparison.Ordinal))
            return false;

        if (child.GetComponentInChildren<CharacterSelectable>(true) != null)
            return true;

        if (child.GetComponentInChildren<CharacterDefHolder>(true) != null)
            return true;

        return child.name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    GameObject SpawnWeaponOnHand(GunConfig weapon, bool rightHand)
    {
        var hand = GetHandTransform(animator, rightHand);
        if (!hand)
        {
            Debug.LogWarning($"[PartySlotVisualPreview] {(rightHand ? "Right" : "Left")} hand not found for weapon preview.", this);
            return null;
        }

        var weaponObj = WeaponModelMountUtility.SpawnWeapon(
            weapon,
            hand,
            rightHand,
            CreateRightFallbackOffset(),
            CreateLeftFallbackOffset(),
            this);

        if (weaponObj)
            weaponObj.name = $"{weaponObj.name}_Preview";

        return weaponObj;
    }

    WeaponModelMountOffset CreateRightFallbackOffset()
    {
        return new WeaponModelMountOffset
        {
            localPosition = rightLocalPos,
            localRotationEuler = rightLocalRotEuler,
            localScale = rightLocalScale
        };
    }

    WeaponModelMountOffset CreateLeftFallbackOffset()
    {
        return new WeaponModelMountOffset
        {
            localPosition = leftLocalPos,
            localRotationEuler = leftLocalRotEuler,
            localScale = leftLocalScale
        };
    }

    bool TryResolveSelectedCharacterWeapon(CharacterStats selected, out GunConfig weapon)
    {
        weapon = null;

        if (selected == null)
            return false;

        string ownerId = CharacterEquipment.BuildCharacterOwnerId(selected.characterId);
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        if (TryResolveCharacterWeaponFromSceneEquipment(ownerId, out weapon))
            return true;

        var data = LoadCurrentGameData();
        string equippedId = CharacterEquipment.FindEquipmentEntry(data?.equipment, ownerId);
        if (string.IsNullOrWhiteSpace(equippedId) && partyIndex == 0)
            equippedId = data?.weapon?.equippedWeaponInstanceId;

        if (string.IsNullOrWhiteSpace(equippedId))
            return false;

        if (TryResolveWeaponFromRuntimeInventory(equippedId, out weapon))
            return true;

        return TryResolveWeaponFromSave(data, equippedId, out weapon);
    }

    bool TryResolveCharacterWeaponFromSceneEquipment(string ownerId, out GunConfig weapon)
    {
        weapon = null;

        if (!CharacterEquipment.TryFindSceneEquipmentByOwner(ownerId, out var equipment) || equipment == null)
            return false;

        if (equipment.CurrentWeapon != null)
        {
            weapon = equipment.CurrentWeapon;
            return true;
        }

        string equippedId = equipment.EquippedWeaponInstanceId;
        if (string.IsNullOrWhiteSpace(equippedId))
            return false;

        return TryResolveWeaponFromRuntimeInventory(equippedId, out weapon);
    }

    bool TryResolveWeaponFromRuntimeInventory(string equippedId, out GunConfig weapon)
    {
        weapon = null;

        if (string.IsNullOrWhiteSpace(equippedId))
            return false;

        var inventories = Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inventories.Length; i++)
        {
            var inventory = inventories[i];
            if (!inventory)
                continue;

            string baseWeaponId = FindBaseWeaponId(inventory.Slots, equippedId);
            if (TryResolveWeaponDefinition(baseWeaponId, inventory, out weapon))
                return true;
        }

        return false;
    }

    bool TryResolveWeaponFromSave(GameSaveData data, string equippedId, out GunConfig weapon)
    {
        weapon = null;

        if (data == null || string.IsNullOrWhiteSpace(equippedId))
            return false;

        string baseWeaponId = FindBaseWeaponId(data.inventory?.slots, equippedId);
        return TryResolveWeaponDefinition(baseWeaponId, null, out weapon);
    }

    GameSaveData LoadCurrentGameData()
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : currentSlot;
        return SaveSystem.LoadGame(saveSlot);
    }

    bool TryResolveWeaponDefinition(string baseWeaponId, PlayerInventory sourceInventory, out GunConfig weapon)
    {
        weapon = null;

        if (string.IsNullOrWhiteSpace(baseWeaponId))
            return false;

        if (weaponDatabase != null)
        {
            weapon = weaponDatabase.GetById(baseWeaponId);
            if (weapon != null)
                return true;
        }

        if (sourceInventory != null && sourceInventory.itemDatabase != null)
        {
            weapon = sourceInventory.itemDatabase.GetItemById(baseWeaponId) as GunConfig;
            if (weapon != null)
                return true;
        }

        var loadedWeaponDatabases = Resources.FindObjectsOfTypeAll<WeaponDatabase>();
        for (int i = 0; i < loadedWeaponDatabases.Length; i++)
        {
            var loadedDb = loadedWeaponDatabases[i];
            if (!loadedDb)
                continue;

            weapon = loadedDb.GetById(baseWeaponId);
            if (weapon != null)
                return true;
        }

        var loadedItemDatabases = Resources.FindObjectsOfTypeAll<ItemDatabase>();
        for (int i = 0; i < loadedItemDatabases.Length; i++)
        {
            var loadedDb = loadedItemDatabases[i];
            if (!loadedDb)
                continue;

            weapon = loadedDb.GetItemById(baseWeaponId) as GunConfig;
            if (weapon != null)
                return true;
        }

        var loadedWeapons = Resources.FindObjectsOfTypeAll<GunConfig>();
        for (int i = 0; i < loadedWeapons.Length; i++)
        {
            var candidate = loadedWeapons[i];
            if (!candidate)
                continue;

            string candidateId = WeaponInstanceFactory.ResolveBaseWeaponId(candidate);
            if (!string.Equals(candidateId, baseWeaponId, StringComparison.Ordinal))
                continue;

            weapon = candidate;
            return true;
        }

        return false;
    }

    static string FindBaseWeaponId(IReadOnlyList<InventorySlotData> slots, string equippedInstanceId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(equippedInstanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var instance = slot?.weaponInstance;
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, equippedInstanceId, StringComparison.Ordinal))
                return instance.baseWeaponId;
        }

        return null;
    }

    static string FindBaseWeaponId(IReadOnlyList<InventorySlotSaveData> slots, string equippedInstanceId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(equippedInstanceId))
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var instance = slot?.weaponInstance;
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, equippedInstanceId, StringComparison.Ordinal))
                return instance.baseWeaponId;
        }

        return null;
    }

    static bool ShouldUseRightHand(CharacterStats stats)
    {
        if (!stats)
            return true;

        return stats.weaponHandMode == CharacterWeaponHandMode.RightHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
    }

    static bool ShouldUseLeftHand(CharacterStats stats)
    {
        if (!stats)
            return false;

        return stats.weaponHandMode == CharacterWeaponHandMode.LeftHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
    }

    Transform GetHandTransform(Animator anim, bool rightHand)
    {
        if (!anim)
            return null;

        if (anim.isHuman)
            return anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

        return FindChildByName(anim.transform, rightHand ? rightHandName : leftHandName);
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    static void ResetPickedState(Animator targetAnimator)
    {
        if (!targetAnimator)
            return;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            if (parameter.name != "IsPicked")
                continue;

            targetAnimator.SetBool(parameter.name, false);
            return;
        }
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
