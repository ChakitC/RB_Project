using System;
using SingularityGroup.HotReload;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Object = UnityEngine.Object;


public class PartySlot : MonoBehaviour
{
    [Header("REF")]
    public LevelSystem levelSystem;
    
    [Header("RootModel")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private CharacterDatabase db;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private int currentSlot;
    [SerializeField] private int partyIndex = 0;     // 0 = player
    [SerializeField] private string fallbackId = "";
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerVisual playerVisual;
    
    [Header("Runtime")]
    public string IDCharacter;
    
    
    
    private GameObject _currentModel;
    [SerializeField] CharacterStats selected;
    
    
    public int LoadOrder => -100;

    public CharacterStats Selected => selected;


    void Start()
    {
        LoadParty();
        levelSystem.SetState();
    }
    

    public void LoadParty()
    {
        
        if (SaveManager.Instance == null) { IDCharacter = fallbackId; return; }

        var id = SaveManager.Instance.LoadPartyMemberId(currentSlot, partyIndex);
        if (string.IsNullOrEmpty(id)) id = fallbackId;

        SetCharacterById(id, save:false);
        
        BuildModel();
        
    }
    
    public void SetCharacterById(string id, bool save)
    {
        if (db == null) { Debug.LogError("[PartySlot] db null", this); return; }

        var def = db.GetById(id);
        if (def == null) { Debug.LogError($"[PartySlot] id not found: {id}", this); return; }

        SetCharacterDef(def, save);
    }
    
    
    public void BuildModel()
    {
        
        
        if (modelRoot == null)
        {
            Debug.LogError("[PartySlot] modelRoot เป็น null");
            return;
        }
        
        for (int i = modelRoot.childCount - 1; i >= 0; i--)
        {
            var child = modelRoot.GetChild(i);
            if (!child) continue;
            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }

        _currentModel = null;
        animator = null;

        if (selected == null)
        {
            Debug.LogWarning("[PartySlot] selected เป็น null -> ไม่สร้างโมเดล");
            return;
        }

        if (selected.CharacterPrefabBasement == null)
        {
            Debug.LogError($"[PartySlot] CharacterPrefabBasement เป็น null (selected: {selected.name})");
            return;
        }

        _currentModel = Instantiate(selected.CharacterPrefabBasement, modelRoot, false);
        _currentModel.name = $"{selected.name}_Preview";

        var t = _currentModel.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        // อย่าทับ scale ถ้า prefab ตั้งมาแล้ว
        // t.localScale = Vector3.one;

        animator = _currentModel.GetComponentInChildren<Animator>(true);
        if (!animator) animator = _currentModel.GetComponent<Animator>();

        if (!animator)
        {
            Debug.LogError("[PartySlot] model ใหม่ไม่มี Animator", _currentModel);
            SafeDestroy(_currentModel);
            _currentModel = null;
            return;
        }

        // ทำให้การ rebind ชัวร์
        bool prev = animator.enabled;
        animator.enabled = false;

        animator.Rebind();
        animator.Update(0f);
        
        animator.enabled = prev;

        
      
       
        // playerVisual.animator = animator;
        // playerVisual.BuildModelFromWeaponDef();
    }
        
    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        
#if UNITY_EDITOR
        if (!Application.isPlaying) Object.DestroyImmediate(go);
        else Object.Destroy(go);
#else
        Object.Destroy(go);
#endif
    }
    
    
    public void SetCharacterDef(CharacterStats def, bool save)
    {
        selected = def;
        IDCharacter = def ? def.characterId : "";   // แนะนำให้เซ็ตไว้เสมอ

        if (save && SaveManager.Instance != null)
            SaveManager.Instance.SaveParty();
    }
  
}