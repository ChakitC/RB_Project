using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpawnOnDragOutButton : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    const string SharedDragObjectName = "CharactorSelect_DragPooled";

    static GameObject sharedDragObject;
    static SpawnOnDragOutButton activeOwner;

    [Header("Spawn")]
    public GameObject prefab;
    public Transform worldParent;
    public Camera worldCamera;
    public Animator _Animator;

    [Header("Drag Pool")]
    [SerializeField] private Transform poolRoot;

    [Header("Match CharacterSelectManager Layers")]
    public LayerMask characterLayer;   
    public LayerMask groundLayer;

    [Header("Drag Feel (match manager)")]
    public float dragHoverY = 0.3f;
    public float maxRayDistance = 1000f;

    [Header("Scroll List Gesture")]
    [SerializeField] private ScrollRect parentScrollRect;
    [SerializeField] private bool forwardHorizontalDragToScrollRect = true;
    [SerializeField, Min(0f)] private float dragOutMinDistance = 24f;
    [SerializeField, Min(1f)] private float horizontalScrollBias = 1.15f;

    [Header("Integration")]
    public CharacterSelectManager selectManagerToDisable; 
    public bool disableManagerWhileDragging = true;
    [SerializeField] private UILoadLaval loadLevelUI;

    [Header("Character Binding")]
    [SerializeField] private CharacterStats characterOverride;

    [Header("Unlock UI")]
    [SerializeField] private PlayerInventory payerInventory;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private GameObject unlockedRoot;
    [SerializeField] private TMP_Text unlockCostText;
    [SerializeField] private TMP_Text unlockReasonText;
    [SerializeField] private Button unlockButton;
    
    [Header("Fixed Y")]
    public float fixedY = 0f;
    [SerializeField] float spawnYaw = 180f; 
    [SerializeField] float yOffsetDegrees = 0f; 
    
    [Header("What counts as old object")]
    public LayerMask replaceableLayer;
    public float checkRadius = 0.35f;
    public float yOffset = 0.1f;          
    
    RectTransform _rect;
    bool _pressed;
    bool _spawned;
    int _pointerId;
    Vector2 _pressScreenPos;
    bool _forwardingScrollDrag;
    bool _beganForwardedScrollDrag;
    bool _finishingRelease;

    GameObject _current;
    CharacterSelectable _selectable;
    CharacterDragVisualPreview _dragVisual;
    CharacterStats _currentDef;
    Vector3 _offset;
    float _startY;
    readonly Dictionary<CharacterEventVoiceLine, float> _voiceReadyAt = new();
    
    
    [Header("Slots (Nearest)")]
    public Transform slotsRoot;
    [SerializeField] bool autoFindSlotsRoot = true;
    [SerializeField] string slotsRootTag = "SlotsRoot";
    
    public float snapMaxDistance = 1.2f; 
    public bool useXZOnly = true;        
    public bool requireSelectableInSlot = true; 
    
    
    Transform[] _slots;
    
    void Awake()
    {
        {
            _rect = transform as RectTransform;
            if (!worldCamera) worldCamera = Camera.main;
            if (!parentScrollRect) parentScrollRect = GetComponentInParent<ScrollRect>();

            if (!slotsRoot && autoFindSlotsRoot)
            {
                var go = GameObject.FindWithTag(slotsRootTag);
                if (go) slotsRoot = go.transform;
            }

            CacheSlots();
            if (unlockButton)
                unlockButton.onClick.AddListener(HandleUnlockClicked);

            RefreshUnlockState();
        }
        
    }

    void OnEnable()
    {
        CharacterUnlockService.CharacterUnlocked += HandleCharacterUnlocked;
        RefreshUnlockState();
    }

    void OnDisable()
    {
        CharacterUnlockService.CharacterUnlocked -= HandleCharacterUnlocked;

        if (_current)
        {
            ReleaseDragObject(_current);
            _current = null;
            _selectable = null;
            _dragVisual = null;
            _currentDef = null;
            _Animator = null;
        }

        _pressed = false;
        _spawned = false;
        _forwardingScrollDrag = false;
        _beganForwardedScrollDrag = false;

        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = true;
    }

    void OnDestroy()
    {
        CharacterUnlockService.CharacterUnlocked -= HandleCharacterUnlocked;

        if (unlockButton)
            unlockButton.onClick.RemoveListener(HandleUnlockClicked);
    }

    public void OnPointerDown(PointerEventData e)
    {
        _pressed = true;
        _spawned = false;
        _pointerId = e.pointerId;
        _pressScreenPos = e.position;
        _forwardingScrollDrag = false;
        _beganForwardedScrollDrag = false;

        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = false;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId)
            return;

        ResolveParentScrollRect();

        if (CanForwardToScrollRect())
            ExecuteEvents.Execute(parentScrollRect.gameObject, e, ExecuteEvents.initializePotentialDrag);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId) return;

        if (TryForwardScrollDrag(e))
            return;

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            _rect, e.position, e.pressEventCamera
        );

        // ออกจากปุ่มครั้งแรก => สปอน
        if (!_spawned && !inside && CanSpawnFromDragOut(e))
        {
            _spawned = true;
            Spawn(e.position);
        }

        // ระหว่างลากให้ตามพื้นแบบเดียวกับ CharacterSelectManager
        if (_current)
            DragFollowGround(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId)
            return;

        FinishPointerRelease(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId)
            return;

        FinishPointerRelease(e);
    }

    void FinishPointerRelease(PointerEventData e)
    {
        if (_finishingRelease)
            return;

        _finishingRelease = true;
        GameObject releasing = _current;

        try
        {
            if (_forwardingScrollDrag)
            {
                EndForwardedScrollDrag(e);
                return;
            }

            if (_current)
            {
                ResetY();
                if (_selectable) _selectable.SetPicked(false);

                PlaceIntoNearestSlot(_current);
            }
        }
        finally
        {
            if (releasing && releasing.activeSelf)
                ReleaseDragObject(releasing);

            _current = null;
            _selectable = null;
            _dragVisual = null;
            _currentDef = null;
            _Animator = null;
            _finishingRelease = false;
            ResetInteractionState();
            RestoreSelectManager();
        }
    }

    void ResetInteractionState()
    {
        _pressed = false;
        _spawned = false;
        _forwardingScrollDrag = false;
        _beganForwardedScrollDrag = false;
    }

    void RestoreSelectManager()
    {
        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = true;
    }

    public void SetCharacterDef(CharacterStats characterDef)
    {
        characterOverride = characterDef;

        var holder = GetComponentInChildren<CharacterDefHolder>(true);
        if (!holder)
            holder = gameObject.AddComponent<CharacterDefHolder>();

        holder.def = characterDef;
        RefreshUnlockState();
    }

    public void SetParentScrollRect(ScrollRect scrollRect)
    {
        parentScrollRect = scrollRect;
    }

    public void CopyDragSettingsFrom(SpawnOnDragOutButton source)
    {
        if (!source)
            return;

        prefab = source.prefab;
        worldParent = source.worldParent;
        worldCamera = source.worldCamera;
        poolRoot = source.poolRoot;
        characterLayer = source.characterLayer;
        groundLayer = source.groundLayer;
        dragHoverY = source.dragHoverY;
        maxRayDistance = source.maxRayDistance;
        selectManagerToDisable = source.selectManagerToDisable;
        disableManagerWhileDragging = source.disableManagerWhileDragging;
        loadLevelUI = source.loadLevelUI;
        payerInventory = source.payerInventory;
        fixedY = source.fixedY;
        spawnYaw = source.spawnYaw;
        yOffsetDegrees = source.yOffsetDegrees;
        replaceableLayer = source.replaceableLayer;
        checkRadius = source.checkRadius;
        yOffset = source.yOffset;
        slotsRoot = source.slotsRoot;
        autoFindSlotsRoot = source.autoFindSlotsRoot;
        slotsRootTag = source.slotsRootTag;
        snapMaxDistance = source.snapMaxDistance;
        useXZOnly = source.useXZOnly;
        requireSelectableInSlot = source.requireSelectableInSlot;

        CacheSlots();
        RefreshUnlockState();
    }
    


    

    void Spawn(Vector2 screenPos)
    {
        if (!worldCamera) worldCamera = Camera.main;

        var selected = ResolveSelectedCharacterDef();
        if (!selected)
        {
            Debug.LogWarning("[SpawnOnDragOutButton] CharacterStats not found from this button or source prefab.", this);
            return;
        }

        _current = AcquireDragObject();
        if (!_current)
            return;

        _currentDef = selected;
        _dragVisual = _current.GetComponent<CharacterDragVisualPreview>();
        if (!_dragVisual || !_dragVisual.Build(selected))
        {
            ReleaseDragObject(_current);
            _current = null;
            _dragVisual = null;
            _currentDef = null;
            return;
        }

        _selectable = _current.GetComponent<CharacterSelectable>();
        _selectable?.SetPicked(true);


        _Animator = _dragVisual.Animator;
        if (_Animator != null)
        {
            _dragVisual.SetPicked(true);
            BuildDragWeaponPreview(selected);
        }
        else
        {
            Debug.LogWarning("Drag preview has no Animator.", _current);
        }
        
        // หา pos จาก raycast เหมือนเดิม
        Ray ray = worldCamera.ScreenPointToRay(screenPos);
        Vector3 pos;
        if (Physics.Raycast(ray, out var hitGround, maxRayDistance, groundLayer))
            pos = hitGround.point;
        else
            pos = worldCamera.transform.position + worldCamera.transform.forward * 3f;

       
        Quaternion rot = Quaternion.Euler(0f, spawnYaw + yOffsetDegrees, 0f);

        // ตั้งทีเดียว
        _current.transform.SetPositionAndRotation(pos, rot);
        PlaySelectCharacterVoice(selected, _current.transform.position);

        _startY = _current.transform.position.y;
        _offset = Vector3.zero;
    }

    GameObject AcquireDragObject()
    {
        if (activeOwner && activeOwner != this)
            activeOwner.ReleaseActiveDragObject();

        if (!sharedDragObject)
            sharedDragObject = CreateDragObject();

        if (!sharedDragObject)
            return null;

        sharedDragObject.name = SharedDragObjectName;
        sharedDragObject.transform.SetParent(worldParent, false);
        sharedDragObject.SetActive(true);
        activeOwner = this;
        return sharedDragObject;
    }

    GameObject CreateDragObject()
    {
        var dragObject = new GameObject(SharedDragObjectName);
        if (prefab)
            dragObject.layer = prefab.layer;

        dragObject.AddComponent<CharacterDefHolder>();
        dragObject.AddComponent<CharacterDragVisualPreview>();
        dragObject.AddComponent<CharacterDragWeaponPreview>();
        dragObject.transform.SetParent(GetPoolParent(), false);
        dragObject.SetActive(false);
        return dragObject;
    }

    void ReleaseDragObject(GameObject dragObject)
    {
        if (!dragObject)
            return;

        var selectable = dragObject.GetComponent<CharacterSelectable>();
        if (selectable)
            selectable.SetPicked(false);

        var visual = dragObject.GetComponent<CharacterDragVisualPreview>();
        if (visual)
            visual.SetPicked(false);

        dragObject.SetActive(false);

        if (dragObject == sharedDragObject)
            sharedDragObject = null;

        if (activeOwner == this)
            activeOwner = null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(dragObject);
        else
            Destroy(dragObject);
#else
        Destroy(dragObject);
#endif
    }

    Transform GetPoolParent()
    {
        if (poolRoot)
            return poolRoot;

        return worldParent;
    }

    void ReleaseActiveDragObject()
    {
        if (!_current)
            return;

        ReleaseDragObject(_current);
        _current = null;
        _selectable = null;
        _dragVisual = null;
        _currentDef = null;
        _Animator = null;
        _pressed = false;
        _spawned = false;
        _forwardingScrollDrag = false;
        _beganForwardedScrollDrag = false;

        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = true;
    }

    CharacterStats ResolveSelectedCharacterDef()
    {
        if (characterOverride)
            return characterOverride;

        var holder = GetComponentInChildren<CharacterDefHolder>(true);
        if (holder && holder.def)
            return holder.def;

        if (!prefab)
            return null;

        holder = prefab.GetComponentInChildren<CharacterDefHolder>(true);
        return holder ? holder.def : null;
    }

    void BuildDragWeaponPreview(CharacterStats selected)
    {
        if (!_current || !selected)
            return;

        var weaponPreview = _current.GetComponent<CharacterDragWeaponPreview>();
        if (!weaponPreview)
            weaponPreview = _current.AddComponent<CharacterDragWeaponPreview>();

        weaponPreview.SetAnimator(_Animator);
        weaponPreview.Build(selected, partyIndex: -1);
    }

    void PlaySelectCharacterVoice(CharacterStats selected, Vector3 position)
    {
        CharacterVoiceProfile voiceProfile = selected != null ? selected.voiceProfile : null;
        CharacterVoicePlayback.TryPlayAtPosition(
            voiceProfile != null ? voiceProfile.selectCharacterVoice : null,
            position,
            _voiceReadyAt);
    }


    void DragFollowGround(Vector2 screenPos)
    {
        Ray ray = worldCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out var hit, maxRayDistance, groundLayer))
        {
            Vector3 newPos = hit.point + _offset + new Vector3(0, dragHoverY, 0);
            _current.transform.position = newPos;
        }
       
    }

    bool TryForwardScrollDrag(PointerEventData e)
    {
        if (!_forwardingScrollDrag && ShouldStartForwardingScroll(e))
            BeginForwardedScrollDrag(e);

        if (!_forwardingScrollDrag)
            return false;

        ForwardScrollDrag(e);
        return true;
    }

    bool ShouldStartForwardingScroll(PointerEventData e)
    {
        if (_spawned || !CanForwardToScrollRect())
            return false;

        Vector2 delta = e.position - _pressScreenPos;
        float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 5f;
        threshold = Mathf.Max(1f, threshold);

        if (delta.sqrMagnitude < threshold * threshold)
            return false;

        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        return absX >= absY * horizontalScrollBias;
    }

    bool CanSpawnFromDragOut(PointerEventData e)
    {
        if (!CanForwardToScrollRect())
            return true;

        return (e.position - _pressScreenPos).magnitude >= dragOutMinDistance;
    }

    bool CanForwardToScrollRect()
    {
        ResolveParentScrollRect();
        return forwardHorizontalDragToScrollRect && parentScrollRect != null && parentScrollRect.gameObject.activeInHierarchy;
    }

    void ResolveParentScrollRect()
    {
        if (!parentScrollRect)
            parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    void BeginForwardedScrollDrag(PointerEventData e)
    {
        if (!CanForwardToScrollRect())
            return;

        _forwardingScrollDrag = true;
        _beganForwardedScrollDrag = true;
        ExecuteEvents.Execute(parentScrollRect.gameObject, e, ExecuteEvents.beginDragHandler);
    }

    void ForwardScrollDrag(PointerEventData e)
    {
        if (!parentScrollRect)
            return;

        ExecuteEvents.Execute(parentScrollRect.gameObject, e, ExecuteEvents.dragHandler);
    }

    void EndForwardedScrollDrag(PointerEventData e)
    {
        if (_beganForwardedScrollDrag && parentScrollRect)
            ExecuteEvents.Execute(parentScrollRect.gameObject, e, ExecuteEvents.endDragHandler);

        _forwardingScrollDrag = false;
        _beganForwardedScrollDrag = false;
    }

    void ResetY()
    {
        Vector3 pos = _current.transform.position;
        pos.y = fixedY;
        _current.transform.position = pos;
    }

  
    
    void PlaceIntoNearestSlot(GameObject newObj)
    {
        if (!newObj) return;
        try
        {
        if (_slots == null || _slots.Length == 0)
        {
            return;
        }

        Transform newRoot = newObj.transform;

        // หา slot ที่ใกล้ที่สุด
        Transform best = null;
        float bestSqr = float.PositiveInfinity;
        
        Vector3 p = newRoot.position;
        
        
        if (useXZOnly) p.y = 0f;

        foreach (var s in _slots)
        {
            if (!s) continue;

            Vector3 sp = s.position;
            if (useXZOnly) sp.y = 0f;

            float d = (p - sp).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = s;
            }
        }

        if (!best)
        {
            return;
        }

        // ถ้าไกลเกิน ไม่ถือว่าลง slot
        if (bestSqr > snapMaxDistance * snapMaxDistance)
        {
            return;
        }

        // ลบของเก่า "เฉพาะใน slot นี้"
        for (int i = best.childCount - 1; i >= 0; i--)
        {
            var child = best.GetChild(i);
            if (!child) continue;

            if (child == newRoot) continue; // กันไม่ลบของใหม่แบบชัวร์
            
            if (!IsReplaceableSlotChild(child, newRoot))
                continue;

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        SyncDroppedCharacterToSlot(best, newRoot);
        }
        finally
        {
            ReleaseDragObject(newObj);
        }
    }

    bool IsReplaceableSlotChild(Transform child, Transform newRoot)
    {
        if (!child)
            return false;

        if (child == newRoot || child.IsChildOf(newRoot))
            return false;

        if (!requireSelectableInSlot)
            return true;

        if (child.GetComponentInChildren<CharacterSelectable>(true) != null)
            return true;

        if (child.GetComponentInChildren<CharacterDefHolder>(true) != null)
            return true;

        string childName = child.name;
        return childName.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void SyncDroppedCharacterToSlot(Transform slotTransform, Transform characterRoot)
    {
        if (!slotTransform || !characterRoot)
            return;

        var slot = slotTransform.GetComponent<PartySlot>();
        if (!slot)
            slot = slotTransform.GetComponentInParent<PartySlot>();

        if (!slot)
            return;

        CharacterStats droppedDef = null;

        var holder = characterRoot.GetComponentInChildren<CharacterDefHolder>(true);
        if (holder && holder.def)
            droppedDef = holder.def;

        if (!droppedDef && _current && characterRoot == _current.transform)
            droppedDef = _currentDef;

        if (!droppedDef)
            return;

        slot.SetCharacterDef(droppedDef, true);
        if (slot.Selected != droppedDef)
            return;

        slot.RefreshSelectedCharacterVisual();
        BindLoadLevelUIToSlot(slot);
    }

    void BindLoadLevelUIToSlot(PartySlot slot)
    {
        if (!slot)
            return;

        if (!loadLevelUI && transform.root)
            loadLevelUI = transform.root.GetComponentInChildren<UILoadLaval>(true);

        if (!loadLevelUI)
            loadLevelUI = FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);

        loadLevelUI?.BindSlot(slot);
    }

    void HandleUnlockClicked()
    {
        CharacterStats selected = ResolveSelectedCharacterDef();
        if (!selected)
            return;

        ResolvePayerInventory();
        if (!CharacterUnlockService.TryUnlockForSelection(selected.characterId, payerInventory, out string reason))
        {
            ShowUnlockReason(reason);
            return;
        }

        RefreshUnlockState();
    }

    void HandleCharacterUnlocked(string characterId)
    {
        CharacterStats selected = ResolveSelectedCharacterDef();
        if (!selected || !string.Equals(selected.characterId, characterId, StringComparison.Ordinal))
            return;

        RefreshUnlockState();
    }

    void RefreshUnlockState()
    {
        CharacterStats selected = ResolveSelectedCharacterDef();
        string characterId = selected != null ? selected.characterId : string.Empty;
        bool hasCharacter = selected != null && !string.IsNullOrWhiteSpace(characterId);
        bool unlocked = hasCharacter && CharacterUnlockService.IsUnlockedForSelection(characterId);

        if (lockedRoot)
            lockedRoot.SetActive(hasCharacter && !unlocked);

        if (unlockedRoot)
            unlockedRoot.SetActive(unlocked);

        int cost = hasCharacter ? CharacterUnlockService.GetGoldCost(characterId) : 0;
        if (unlockCostText)
            unlockCostText.text = cost > 0 ? cost.ToString("N0") : string.Empty;

        ResolvePayerInventory();

        string reason = string.Empty;
        bool canUnlock = hasCharacter && !unlocked &&
                         CharacterUnlockService.CanUnlock(characterId, payerInventory, out reason);

        if (unlockButton)
            unlockButton.interactable = canUnlock;

        if (unlockReasonText)
            unlockReasonText.text = unlocked ? string.Empty : ResolveLockedReason(characterId, reason);
    }

    string ResolveLockedReason(string characterId, string fallback)
    {
        string message = CharacterUnlockService.GetLockedMessage(characterId);
        if (!string.IsNullOrWhiteSpace(message))
            return message;

        return fallback;
    }

    void ShowUnlockReason(string reason)
    {
        if (unlockReasonText)
            unlockReasonText.text = reason;
    }

    void ResolvePayerInventory()
    {
        if (!payerInventory)
            payerInventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }

    void CacheSlots()
    {
        if (!slotsRoot) { _slots = null; return; }

        int n = slotsRoot.childCount;
        _slots = new Transform[n];
        for (int i = 0; i < n; i++)
            _slots[i] = slotsRoot.GetChild(i);
    }
    
    void OnValidate()
    {
        CacheSlots();
    }
        
    
    #if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position + Vector3.up * yOffset, checkRadius);
        }
    #endif
    }
    
    

