using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnOnDragOutButton : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Spawn")]
    public GameObject prefab;
    public Transform worldParent;
    public Camera worldCamera;
    public Animator _Animator;

    [Header("Match CharacterSelectManager Layers")]
    public LayerMask characterLayer;   
    public LayerMask groundLayer;

    [Header("Drag Feel (match manager)")]
    public float dragHoverY = 0.3f;
    public float maxRayDistance = 1000f;

    [Header("Integration")]
    public CharacterSelectManager selectManagerToDisable; 
    public bool disableManagerWhileDragging = true;
    
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

    GameObject _current;
    CharacterSelectable _selectable;
    Vector3 _offset;
    float _startY;
    
    
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

            if (!slotsRoot && autoFindSlotsRoot)
            {
                var go = GameObject.FindWithTag(slotsRootTag);
                if (go) slotsRoot = go.transform;
            }

            CacheSlots();
        }
        
    }
    

    public void OnPointerDown(PointerEventData e)
    {
        _pressed = true;
        _spawned = false;
        _pointerId = e.pointerId;

        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId) return;

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            _rect, e.position, e.pressEventCamera
        );

        // ออกจากปุ่มครั้งแรก => สปอน
        if (!_spawned && !inside)
        {
            _spawned = true;
            Spawn(e.position);
        }

        // ระหว่างลากให้ตามพื้นแบบเดียวกับ CharacterSelectManager
        if (_current)
            DragFollowGround(e.position);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_pressed || e.pointerId != _pointerId) return;
        _pressed = false;

        if (_current)
        {
            ResetY();
            if (_selectable) _selectable.SetPicked(false);

            PlaceIntoNearestSlot(_current);  

            _current = null;
            _selectable = null;
        }

        if (disableManagerWhileDragging && selectManagerToDisable)
            selectManagerToDisable.enabled = true;
    }
    


    

    void Spawn(Vector2 screenPos)
    {
        if (!prefab) return;
        if (!worldCamera) worldCamera = Camera.main;

        _current = Instantiate(prefab, worldParent);

        _selectable = _current.GetComponent<CharacterSelectable>();
        _selectable?.SetPicked(true);


        _Animator = _current.GetComponentInChildren<Animator>(true);
        if (_Animator != null)
        {
            _Animator.SetBool("IsPicked", true); 
        }
        else
        {
            Debug.LogWarning("Spawned prefab has no Animator (or it's not on root).");
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

        _startY = _current.transform.position.y;
        _offset = Vector3.zero;
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

    void ResetY()
    {
        Vector3 pos = _current.transform.position;
        pos.y = fixedY;
        _current.transform.position = pos;
    }

  
    
    void PlaceIntoNearestSlot(GameObject newObj)
    {
        if (!newObj) return;
        if (_slots == null || _slots.Length == 0) return;

        Transform newRoot = newObj.transform.root;

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

        if (!best) return;

        // ถ้าไกลเกิน ไม่ถือว่าลง slot
        if (bestSqr > snapMaxDistance * snapMaxDistance)
            return;

        // ลบของเก่า "เฉพาะใน slot นี้"
        for (int i = best.childCount - 1; i >= 0; i--)
        {
            var child = best.GetChild(i);
            if (!child) continue;

            if (child == newRoot) continue; // กันไม่ลบของใหม่แบบชัวร์
            
            _Animator.SetBool("IsPicked",false);
            
            Destroy(child.gameObject);
        }

       
        newRoot.SetParent(best, false);
        newRoot.localPosition = Vector3.zero;
        newRoot.localRotation = Quaternion.Euler(0f, 0f + 0f, 0f);
        newRoot.localScale = Vector3.one;

        
        var wp = newRoot.position;
        wp.y = fixedY;
        newRoot.position = wp;
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
    
    

