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

    [Header("Optional Offsets")]
    [SerializeField] private Vector3 rightLocalPos;
    [SerializeField] private Vector3 rightLocalRotEuler;
    [SerializeField] private Vector3 rightLocalScale = Vector3.one;

    [SerializeField] private Vector3 leftLocalPos;
    [SerializeField] private Vector3 leftLocalRotEuler;
    [SerializeField] private Vector3 leftLocalScale = Vector3.one;

    [Header("Fire Point")]
    [SerializeField] private string firePointName = "FirePoint";
    [SerializeField] private string firePointBoneName = "c_traj";
    [SerializeField] private Vector3 firePointLocalPos;
    [SerializeField] private Vector3 firePointLocalRotEuler;
    [SerializeField] private Vector3 firePointLocalScale = Vector3.one;

    private CharacterContextPartyLoader _partyLoader;
    private WeaponSystem _weaponSystem;
    private Transform _defaultFirePointParent;
    private bool _loggedMissingFirePointBone;

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

        if (_weaponSystem == null)
            _weaponSystem = GetComponent<WeaponSystem>();
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

        var stats = GetCurrentCharacterStats();
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
        DetachFirePointFromCurrentModel();

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

        AttachFirePointToModelBone();
        BuildModelFromWeaponDef();

        animator = GetComponent<Animator>();
        var stats = GetCurrentCharacterStats();
        if (!animator || !stats)
            return;

        animator.runtimeAnimatorController = stats.controller;
        animator.avatar = stats.characterAvatar;

        animator.enabled = false;
        animator.enabled = true;

        if (animancer) animancer.Animator = animator;
    }

    public void BuildModelFromWeaponDef()
    {
        if (!animator || _ctx == null || _ctx.currentWeapon == null)
            return;

        var stats = GetCurrentCharacterStats();
        bool useRightHand = ShouldUseRightHand(stats);
        bool useLeftHand = ShouldUseLeftHand(stats);

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

    private void AttachFirePointToModelBone()
    {
        if (!_currentModel || string.IsNullOrWhiteSpace(firePointBoneName))
            return;

        var firePoint = GetFirePoint();
        if (!firePoint)
            return;

        var targetBone = FindChildByName(_currentModel.transform, firePointBoneName);
        if (!targetBone)
        {
            if (!_loggedMissingFirePointBone)
            {
                Debug.LogWarning($"[PlayerVisual] Fire point bone '{firePointBoneName}' not found in '{_currentModel.name}'.", this);
                _loggedMissingFirePointBone = true;
            }

            return;
        }

        _loggedMissingFirePointBone = false;

        firePoint.SetParent(targetBone, false);
        firePoint.localPosition = firePointLocalPos;
        firePoint.localRotation = Quaternion.Euler(firePointLocalRotEuler);
        firePoint.localScale = firePointLocalScale;

        _weaponSystem?.RefreshFirePointReference();
    }

    private void DetachFirePointFromCurrentModel()
    {
        var firePoint = GetFirePoint();
        if (!firePoint || !_defaultFirePointParent || !modelRoot || !firePoint.IsChildOf(modelRoot))
            return;

        firePoint.SetParent(_defaultFirePointParent, true);
        _weaponSystem?.RefreshFirePointReference();
    }

    private Transform GetFirePoint()
    {
        EnsureReferences();

        Transform firePoint = _weaponSystem ? _weaponSystem.firePoint : null;
        if (!firePoint)
            firePoint = FindChildByName(transform, firePointName);

        if (!firePoint)
            return null;

        if (_defaultFirePointParent == null)
            _defaultFirePointParent = firePoint.parent ? firePoint.parent : transform;

        if (_weaponSystem && !_weaponSystem.firePoint)
            _weaponSystem.firePoint = firePoint;

        return firePoint;
    }

    private CharacterStats GetCurrentCharacterStats()
    {
        EnsureReferences();

        if (IsSlot)
        {
            if (_partyLoader && _partyLoader.CurrentContext)
                return _partyLoader.CurrentContext;

            return null;
        }

        return _ctx ? _ctx.baseStats : null;
    }

    private static bool ShouldUseRightHand(CharacterStats stats)
    {
        if (!stats)
            return true;

        return stats.weaponHandMode == CharacterWeaponHandMode.RightHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
    }

    private static bool ShouldUseLeftHand(CharacterStats stats)
    {
        if (!stats)
            return false;

        return stats.weaponHandMode == CharacterWeaponHandMode.LeftHand
            || stats.weaponHandMode == CharacterWeaponHandMode.BothHands;
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
