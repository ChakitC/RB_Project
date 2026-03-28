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
    [SerializeField] private string leftHandName = "hand.l";
    public Animator animator;

    [SerializeField] private bool useRightHand = true;
    [SerializeField] private bool useLeftHand = false;

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
        EnsureReferences();

        if (!animancer) Debug.LogWarning("[PlayerVisual] Missing AnimancerComponent", this);
        if (!modelRoot) Debug.LogWarning("[PlayerVisual] modelRoot Missing", this);

        TryBuildCurrentModel(silent: true);
    }

    private void Start()
    {
        TryBuildCurrentModel(silent: true);
    }

    public void OnSave(GameSaveData data) { }

    public void OnLoad(GameSaveData data)
    {
        EnsureReferences();
        TryBuildCurrentModel(silent: false);
    }

    void EnsureReferences()
    {
        if (_ctx == null)
            _ctx = GetComponent<CharacteContext>();

        if (_partyLoader == null)
            _partyLoader = GetComponent<CharacterContextPartyLoader>();
    }

    void TryBuildCurrentModel(bool silent)
    {
        if (!TryGetCharacterPrefab(out var prefab, silent))
            return;

        BuildModel(prefab);
    }

    bool TryGetCharacterPrefab(out GameObject prefab, bool silent)
    {
        prefab = null;
        if (!modelRoot)
            return false;

        EnsureReferences();

        if (IsSlot)
        {
            if (!_partyLoader || !_partyLoader.CurrentContext)
            {
                if (!silent)
                    Debug.LogWarning("[PlayerVisual] Slot mode but missing CharacterContextPartyLoader/CurrentContext", this);
                return false;
            }

            prefab = _partyLoader.CurrentContext.CharacterPrefab;
            if (!prefab && !silent)
                Debug.LogWarning("[PlayerVisual] Slot CurrentContext has no CharacterPrefab", this);

            return prefab;
        }

        if (_ctx == null)
            return false;

        var stats = _ctx.baseStats;
        if (!stats)
        {
            if (!silent)
                Debug.LogWarning("[PlayerVisual] baseStats is missing", this);
            return false;
        }

        prefab = stats.CharacterPrefab;
        if (!prefab && !silent)
            Debug.LogWarning($"[PlayerVisual] '{stats.name}' has no CharacterPrefab", this);

        return prefab;
    }

    private void BuildModel(GameObject prefab)
    {
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
        if (!animator || _ctx == null || _ctx.baseStats == null)
            return;

        animator.runtimeAnimatorController = _ctx.baseStats.controller;
        animator.avatar = _ctx.baseStats.characterAvatar;

        animator.enabled = false;
        animator.enabled = true;

        if (animancer) animancer.Animator = animator;
    }

    public void BuildModelFromWeaponDef()
    {
        if (!animator || _ctx == null || _ctx.currentWeapon == null)
            return;

        var weaponPrefab = _ctx.currentWeapon.WeaponPrefab;
        prefabweapone = weaponPrefab;
        if (!weaponPrefab)
        {
            if (_rightObj) Destroy(_rightObj);
            if (_leftObj) Destroy(_leftObj);
            _rightObj = null;
            _leftObj = null;
            Debug.Log("weaponPrefab missing ");
            return;
        }

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
        if (anim && anim.isHuman)
            return anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

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
