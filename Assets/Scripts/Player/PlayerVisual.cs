using UnityEngine;
using Animancer;

public class PlayerVisual : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [SerializeField] private CharacteContext _ctx;
    [SerializeField] private GameObject prefabweapone;
    [Header("Slot")]
    [SerializeField] public bool IsSlot;

    [Header("RootModel")]
    [SerializeField] private Transform modelRoot;

    [Header("Animancer Player (root)")]
    [SerializeField] public AnimancerComponent animancer;

    [Header("Bone (Humanoid preferred)")]
    [SerializeField] private string rightHandName = "hand.r";
    [SerializeField] private string leftHandName = "hand.l" ;
    public Animator animator;

    [SerializeField] private bool useRightHand = true;
    [SerializeField] private bool useLeftHand  = false;
    
    [Header("Optional Offsets")]
    [SerializeField] private Vector3 rightLocalPos;
    [SerializeField] private Vector3 rightLocalRotEuler;
    [SerializeField] private Vector3 rightLocalScale = Vector3.one;

    [SerializeField] private Vector3 leftLocalPos;
    [SerializeField] private Vector3 leftLocalRotEuler;
    [SerializeField] private Vector3 leftLocalScale = Vector3.one;


   
    private CharacterContextPartyLoader _partyLoader;

    private GameObject _currentModel;
    private GameObject _rightObj;
    private GameObject _leftObj;

    public int LoadOrder => 100;

    private void Awake()
    {
        _ctx = GetComponent<CharacteContext>();
        _partyLoader = GetComponent<CharacterContextPartyLoader>(); // สำคัญมากสำหรับ Slot

        if (!animancer) Debug.LogWarning("[PlayerVisual] Missing AnimancerComponent", this);
        if (!modelRoot) Debug.LogWarning("[PlayerVisual] modelRoot Missing", this);

        // fallback เฉพาะตอนไม่มี SaveManager
        if (SaveManager.Instance == null)
        {
            if (IsSlot) BuildModelFromSlot();
            else BuildModelFromCharacterDef();
         
        }
        
    }
    

    public void OnSave(GameSaveData data) { }

    public void OnLoad(GameSaveData data)
    {
        
        if (IsSlot) BuildModelFromSlot();
        else BuildModelFromCharacterDef();
        
    }

    private void BuildModelFromSlot()
    {
        if (!modelRoot) return;
        if (!_partyLoader || !_partyLoader.CurrentContext)
        {
            Debug.LogWarning("[PlayerVisual] Slot mode but missing CharacterContextPartyLoader/CurrentContext", this);
            return;
        }

        var prefab = _partyLoader.CurrentContext.CharacterPrefab;
        if (!prefab)
        {
            Debug.LogWarning("[PlayerVisual] Slot CurrentContext has no CharacterPrefab", this);
            return;
        }

        BuildModel(prefab);
    }

    private void BuildModelFromCharacterDef()
    {
        if (!modelRoot || _ctx == null) return;

        var stats = _ctx.baseStats;
        if (!stats)
        {
            Debug.LogWarning("[PlayerVisual] baseStats ว่าง", this);
            return;
        }

        if (!stats.CharacterPrefab)
        {
            Debug.LogWarning($"[PlayerVisual] '{stats.name}' ไม่มี CharacterPrefab", this);
            return;
        }
        
        
        BuildModel(stats.CharacterPrefab);
        
       
    }

    private void BuildModel(GameObject prefab)
    {
        // เคลียร์ของเดิม
        for (int i = modelRoot.childCount - 1; i >= 0; i--)
            Destroy(modelRoot.GetChild(i).gameObject);

        _rightObj = null;
        _leftObj = null;

        _currentModel = Instantiate(prefab, modelRoot);
        _currentModel.transform.localPosition = Vector3.zero;
        _currentModel.transform.localRotation = Quaternion.identity;
        _currentModel.transform.localScale = Vector3.one;
        
        
        animator = _currentModel.GetComponentInChildren<Animator>(true);
        if (!animator)
        {
            Debug.LogError("[PlayerVisual] Animator not found in spawned model!", this);
            return;
        }
        
        BuildModelFromWeaponDef();

        animator = GetComponent<Animator>();

        animator.runtimeAnimatorController = _ctx.baseStats.controller;
        animator.avatar = _ctx.baseStats.characterAvatar;
        
        animator.enabled = false;
        animator.enabled = true;
        
        
        if (animancer) animancer.Animator = animator;

        
      
    }

    public void BuildModelFromWeaponDef()
    {
        
        if (!animator) { return;}
        
        var weaponPrefab = _ctx.currentWeapon.WeaponPrefab;
        prefabweapone = weaponPrefab;
        // ถ้าไม่มี prefab -> เคลียร์ของที่ติดไว้ทั้งสองมือ
        if (!weaponPrefab)
        {
            if (_rightObj) Destroy(_rightObj);
            if (_leftObj) Destroy(_leftObj);
            _rightObj = null;
            _leftObj = null;
            Debug.Log("weaponPrefab missing ");
            return;
        }
        
        
        
        // ---------------- Right ----------------
        if (useRightHand)
        {
            var rHand = GetHandTransform(animator, true);
            if (!rHand)
            {
                Debug.LogWarning("Right hand not found", this);
                if (_rightObj) Destroy(_rightObj);
                _rightObj = null;
            }
            else
            {
                if (_rightObj) Destroy(_rightObj);
                _rightObj = Instantiate(weaponPrefab, rHand, false);
                _rightObj.transform.localPosition = rightLocalPos;
                _rightObj.transform.localRotation = Quaternion.Euler(rightLocalRotEuler);
                _rightObj.transform.localScale = rightLocalScale;
                
            }
        }
        else
        {
            if (_rightObj) Destroy(_rightObj);
            _rightObj = null;
            
        }

        // ---------------- Left ----------------
        if (useLeftHand)
        {
            var lHand = GetHandTransform(animator, false);
            if (!lHand)
            {
                Debug.LogWarning("Left hand not found", this);
                if (_leftObj) Destroy(_leftObj);
                _leftObj = null;
            }
            else
            {
                if (_leftObj) Destroy(_leftObj);
                _leftObj = Instantiate(weaponPrefab, lHand, false);
                _leftObj.transform.localPosition = leftLocalPos;
                _leftObj.transform.localRotation = Quaternion.Euler(leftLocalRotEuler);
                _leftObj.transform.localScale = leftLocalScale;
            }
        }
        else
        {
            if (_leftObj) Destroy(_leftObj);
            _leftObj = null;
        }
        
    }
    
    private Transform GetHandTransform(Animator anim, bool right)
    {
        // ถ้าเป็น Humanoid ใช้ BoneTransform จะดีที่สุด
        if (anim && anim.isHuman)
        {
            return anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        }

        // fallback เป็นค้นหาจากชื่อ
        return FindChildByName(anim.transform, right ? rightHandName : leftHandName);
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root) return null;
        if (root.name == targetName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), targetName);
            if (found) return found;
        }
        return null;
    }
}
