using UnityEngine;
using UnityEngine.UI;
using Animancer;

public class CharacterVisualController : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [SerializeField] private CharacteContext _ctx;
    [SerializeField] private GameObject prefabweapone;
    [Header("Slot")]
    [SerializeField] public bool IsSlot;

    [Header("RootModel")]
    [SerializeField] private Transform modelRoot;

    [Header("Model Build")]
    [SerializeField, Tooltip("Disable when a model is already placed under modelRoot and should not be recreated automatically.")]
    private bool buildModelAutomatically = true;

    [Header("Animancer Player (root)")]
    [SerializeField] public AnimancerComponent animancer;

    [Header("Bone (Humanoid preferred)")]
    [SerializeField] private string rightHandName = "Weapon.R";
    [SerializeField] private string leftHandName = "Weapon.L";
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

    [Header("Health Bar")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarHight = 2f;
    [SerializeField] private string healthBarBoneName = "c_traj";
    [SerializeField] private Vector3 healthBarLocalOffset;
    [SerializeField] private Vector3 healthBarLocalRotEuler;
    [SerializeField] private Vector3 healthBarLocalScale = Vector3.one;

    private CharacterContextPartyLoader _partyLoader;
    private WeaponSystem _weaponSystem;
    private HealthSystem _healthSystem;
    private Transform _defaultFirePointParent;
    private Transform _firePoint;
    private GameObject _healthBarInstance;
    private bool _loggedMissingFirePointBone;
    private bool _loggedMissingHealthBarBone;

    private GameObject _currentModel;
    private GameObject _rightObj;
    private GameObject _leftObj;

    public int LoadOrder => 100;

    private void Awake()
    {
        EnsureReferences();

        if (!animancer) Debug.LogWarning("[CharacterVisualController] Missing AnimancerComponent", this);
        if (!modelRoot) Debug.LogWarning("[CharacterVisualController] modelRoot Missing", this);

        TryBuildCurrentModelAutomatically(silent: true);
    }

    private void Start()
    {
        TryBuildCurrentModelAutomatically(silent: true);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ApplyFirePointOffset();
        ApplyHealthBarOffset();
    }

    public void OnSave(GameSaveData data) { }

    public void OnLoad(GameSaveData data)
    {
        EnsureReferences();
        TryBuildCurrentModelAutomatically(silent: false);
    }

    void EnsureReferences()
    {
        if (_ctx == null)
            _ctx = GetComponent<CharacteContext>();

        if (_ctx == null)
            _ctx = GetComponentInParent<CharacteContext>();

        _ctx?.ResolveReferences();

        if (_ctx != null && _ctx.Visual != this)
            _ctx.Visual = this;

        if (animancer == null)
            animancer = GetComponent<AnimancerComponent>();

        if (animancer == null && _ctx != null)
            animancer = _ctx.GetComponentInChildren<AnimancerComponent>(true);

        if (animancer == null)
            animancer = GetComponentInParent<AnimancerComponent>();

        if (animancer == null)
            animancer = GetComponentInChildren<AnimancerComponent>(true);

        if (_partyLoader == null)
            _partyLoader = _ctx != null ? _ctx.CharacterLoad : null;

        if (_partyLoader == null)
            _partyLoader = GetComponentInParent<CharacterContextPartyLoader>();

        if (_partyLoader == null)
        {
            Transform ownerRoot = _ctx ? _ctx.transform : transform.root;
            if (ownerRoot)
                _partyLoader = ownerRoot.GetComponentInChildren<CharacterContextPartyLoader>(true);
        }

        if (_ctx != null && _ctx.CharacterLoad == null && _partyLoader != null)
            _ctx.CharacterLoad = _partyLoader;

        if (_weaponSystem == null)
            _weaponSystem = _ctx != null ? _ctx.WeaponSystem : null;

        if (_weaponSystem == null)
            _weaponSystem = GetComponentInChildren<WeaponSystem>(true);

        if (_weaponSystem == null)
            _weaponSystem = GetComponentInParent<WeaponSystem>();

        if (_weaponSystem == null)
        {
            Transform ownerRoot = _ctx ? _ctx.transform : transform.root;
            if (ownerRoot)
                _weaponSystem = ownerRoot.GetComponentInChildren<WeaponSystem>(true);
        }

        if (_ctx != null && _ctx.WeaponSystem == null && _weaponSystem != null)
            _ctx.WeaponSystem = _weaponSystem;

        if (_weaponSystem != null && _weaponSystem.ctx == null && _ctx != null)
            _weaponSystem.ctx = _ctx;

        if (_healthSystem == null)
            _healthSystem = _ctx != null ? _ctx.HealthSystem : null;

        if (_healthSystem == null)
            _healthSystem = GetComponent<HealthSystem>();

        if (_healthSystem == null)
            _healthSystem = GetComponentInParent<HealthSystem>();

        if (_healthSystem == null)
        {
            Transform ownerRoot = _ctx ? _ctx.transform : transform.root;
            if (ownerRoot)
                _healthSystem = ownerRoot.GetComponentInChildren<HealthSystem>(true);
        }

        if (_ctx != null && _ctx.HealthSystem == null && _healthSystem != null)
            _ctx.HealthSystem = _healthSystem;

        if (_healthSystem != null && _healthSystem.CTX == null && _ctx != null)
            _healthSystem.CTX = _ctx;
    }

    void TryBuildCurrentModel(bool silent)
    {
        if (!TryGetCharacterPrefab(out var prefab, silent))
            return;

        BuildModel(prefab);
    }

    void TryBuildCurrentModelAutomatically(bool silent)
    {
        if (buildModelAutomatically)
        {
            TryBuildCurrentModel(silent);
            return;
        }

        SetupExistingModel(silent);
    }

    void SetupExistingModel(bool silent)
    {
        if (!TryResolveExistingAnimator(silent))
            return;

        _currentModel = ResolveExistingModelObject();
        if (!_currentModel)
        {
            if (!silent)
                Debug.LogWarning("[CharacterVisualController] Existing model root not found", this);
            return;
        }

        BindCurrentModelReferences();
        AttachFirePointToModelBone();
        CreateHealthBarOnModelBone();
        BuildModelFromWeaponDef();
        ConfigureAnimatorRuntime();
    }

    bool TryResolveExistingAnimator(bool silent)
    {
        if (animator)
            return true;

        if (animancer && animancer.Animator)
            animator = animancer.Animator;

        if (!animator && modelRoot)
            animator = modelRoot.GetComponentInChildren<Animator>(true);

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!animator && _ctx)
            animator = _ctx.GetComponentInChildren<Animator>(true);

        if (!animator)
            animator = GetComponentInParent<Animator>();

        if (!animator && !silent)
            Debug.LogWarning("[CharacterVisualController] Existing model Animator not found", this);

        return animator;
    }

    GameObject ResolveExistingModelObject()
    {
        if (animator)
        {
            if (modelRoot && animator.transform.IsChildOf(modelRoot))
                return GetTopChildUnderRoot(animator.transform, modelRoot).gameObject;

            return animator.gameObject;
        }

        if (modelRoot && modelRoot.childCount > 0)
            return modelRoot.GetChild(0).gameObject;

        return null;
    }

    static Transform GetTopChildUnderRoot(Transform child, Transform root)
    {
        Transform current = child;

        while (current.parent && current.parent != root && current.parent.IsChildOf(root))
            current = current.parent;

        return current;
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
                Debug.LogWarning("[CharacterVisualController] baseStats is missing", this);
            return false;
        }

        prefab = stats.CharacterPrefab;
        if (!prefab && !silent)
            Debug.LogWarning($"[CharacterVisualController] '{stats.name}' has no CharacterPrefab", this);

        return prefab;
    }

    private void BuildModel(GameObject prefab)
    {
        DetachFirePointFromCurrentModel();
        DestroyHealthBarInstance();
        ClearCurrentModelReferences();

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
            Debug.LogError("[CharacterVisualController] Animator not found in spawned model!", this);
            return;
        }

        BindCurrentModelReferences();
        AttachFirePointToModelBone();
        CreateHealthBarOnModelBone();
        BuildModelFromWeaponDef();

        ConfigureAnimatorRuntime();
    }

    void ClearCurrentModelReferences()
    {
        if (_ctx == null || _ctx.ColliderRefs == null)
            return;

        Transform colliderRefsTransform = _ctx.ColliderRefs.transform;
        bool belongsToCurrentModel = _currentModel != null && colliderRefsTransform.IsChildOf(_currentModel.transform);
        bool belongsToModelRoot = modelRoot != null && colliderRefsTransform.IsChildOf(modelRoot);

        if (belongsToCurrentModel || belongsToModelRoot)
            _ctx.ColliderRefs = null;
    }

    void BindCurrentModelReferences()
    {
        if (_ctx == null || _currentModel == null)
            return;

        CharacterColliderRefs colliderRefs = _currentModel.GetComponentInChildren<CharacterColliderRefs>(true);
        if (colliderRefs != null)
            _ctx.ColliderRefs = colliderRefs;
    }

    private void ConfigureAnimatorRuntime()
    {
        var stats = GetCurrentCharacterStats();
        if (!animator || !stats)
            return;

        animator.runtimeAnimatorController = stats.controller;
        animator.avatar = stats.characterAvatar;

        animator.enabled = false;
        animator.enabled = true;

      
        
        if (animancer) animancer.Animator = animator;

        EnsureRootMotionDriverOnAnimator();
    }

    private void EnsureRootMotionDriverOnAnimator()
    {
        if (!animator)
            return;

        var driver = animator.GetComponent<RootMotionCCDriver>();
        if (!driver)
            driver = animator.gameObject.AddComponent<RootMotionCCDriver>();

        CharacterAnimBrain animBrain = _ctx != null ? _ctx.AnimBrain : null;
        if (!animBrain && _ctx)
            animBrain = _ctx.GetComponentInChildren<CharacterAnimBrain>(true);

        CharacterController characterController = _ctx != null ? _ctx.cc : null;
        if (!characterController && _ctx)
            characterController = _ctx.GetComponent<CharacterController>();
        if (!characterController && _ctx)
            characterController = _ctx.GetComponentInChildren<CharacterController>(true);

        driver.Configure(animBrain, characterController, animator);
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
                _rightObj = WeaponModelMountUtility.SpawnWeapon(
                    _ctx.currentWeapon,
                    rHand,
                    rightHand: true,
                    CreateRightWeaponMountFallback(),
                    CreateLeftWeaponMountFallback(),
                    this);
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
                _leftObj = WeaponModelMountUtility.SpawnWeapon(
                    _ctx.currentWeapon,
                    lHand,
                    rightHand: false,
                    CreateRightWeaponMountFallback(),
                    CreateLeftWeaponMountFallback(),
                    this);
            }
        }
        else
        {
            if (_leftObj) Destroy(_leftObj);
            _leftObj = null;
        }
    }

    WeaponModelMountOffset CreateRightWeaponMountFallback()
    {
        return new WeaponModelMountOffset
        {
            localPosition = rightLocalPos,
            localRotationEuler = rightLocalRotEuler,
            localScale = rightLocalScale
        };
    }

    WeaponModelMountOffset CreateLeftWeaponMountFallback()
    {
        return new WeaponModelMountOffset
        {
            localPosition = leftLocalPos,
            localRotationEuler = leftLocalRotEuler,
            localScale = leftLocalScale
        };
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
                Debug.LogWarning($"[CharacterVisualController] Fire point bone '{firePointBoneName}' not found in '{_currentModel.name}'.", this);
                _loggedMissingFirePointBone = true;
            }

            return;
        }

        _loggedMissingFirePointBone = false;

        firePoint.SetParent(targetBone, false);
        ApplyFirePointOffset();
    }

    private void CreateHealthBarOnModelBone()
    {
        EnsureReferences();

        if (_healthBarInstance)
        {
            ApplyHealthBarOffset();

            if (_healthSystem != null)
            {
                var existingSlider = _healthBarInstance.GetComponentInChildren<Slider>(true);
                _healthSystem.SetHealthBarSlider(existingSlider);
            }

            return;
        }

        if (!_currentModel || _healthSystem == null || !healthBarPrefab || string.IsNullOrWhiteSpace(healthBarBoneName))
            return;

        var targetBone = FindChildByName(_currentModel.transform, healthBarBoneName);
        if (!targetBone)
        {
            if (!_loggedMissingHealthBarBone)
            {
                Debug.LogWarning($"[CharacterVisualController] Health bar bone '{healthBarBoneName}' not found in '{_currentModel.name}'.", this);
                _loggedMissingHealthBarBone = true;
            }

            return;
        }

        _loggedMissingHealthBarBone = false;

        _healthBarInstance = Instantiate(healthBarPrefab, targetBone, false);
        ApplyHealthBarOffset();

        var slider = _healthBarInstance.GetComponentInChildren<Slider>(true);
        _healthSystem.SetHealthBarSlider(slider);
    }

    private void DestroyHealthBarInstance()
    {
        if (!_healthBarInstance)
            return;

        if (_healthSystem != null)
        {
            var slider = _healthBarInstance.GetComponentInChildren<Slider>(true);
            _healthSystem.ClearHealthBarSlider(slider);
        }

        Destroy(_healthBarInstance);
        _healthBarInstance = null;
    }

    private void DetachFirePointFromCurrentModel()
    {
        var firePoint = GetFirePoint();
        if (!firePoint || !_defaultFirePointParent || !modelRoot || !firePoint.IsChildOf(modelRoot))
            return;

        firePoint.SetParent(_defaultFirePointParent, true);
        _weaponSystem?.RefreshFirePointReference();
    }

    private void ApplyFirePointOffset()
    {
        var firePoint = _firePoint ? _firePoint : GetFirePoint();
        if (!firePoint)
            return;

        firePoint.localPosition = firePointLocalPos;
        firePoint.localRotation = Quaternion.Euler(firePointLocalRotEuler);
        firePoint.localScale = firePointLocalScale;

        _weaponSystem?.RefreshFirePointReference();
    }

    private void ApplyHealthBarOffset()
    {
        if (!_healthBarInstance || _healthSystem == null)
            return;

        _healthBarInstance.transform.localPosition = Vector3.forward * healthBarHight + healthBarLocalOffset;
        _healthBarInstance.transform.localRotation = Quaternion.Euler(healthBarLocalRotEuler);
        Vector3 prefabScale = healthBarPrefab ? healthBarPrefab.transform.localScale : Vector3.one;
        _healthBarInstance.transform.localScale = Vector3.Scale(prefabScale, healthBarLocalScale);
    }

    private Transform GetFirePoint()
    {
        EnsureReferences();

        Transform firePoint = _firePoint ? _firePoint : null;

        if (!firePoint && _weaponSystem && IsValidExternalFirePoint(_weaponSystem.firePoint))
            firePoint = _weaponSystem.firePoint;

        if (!firePoint)
        {
            Transform ownerRoot = _ctx ? _ctx.transform : transform;
            firePoint = FindChildByNameExcluding(ownerRoot, firePointName, modelRoot);
        }

        if (!firePoint && _weaponSystem)
            firePoint = _weaponSystem.firePoint;

        if (!firePoint)
            return null;

        _firePoint = firePoint;

        if (_defaultFirePointParent == null)
            _defaultFirePointParent = firePoint.parent ? firePoint.parent : transform;

        if (_weaponSystem && _weaponSystem.firePoint != firePoint)
            _weaponSystem.firePoint = firePoint;

        return firePoint;
    }

    private bool IsValidExternalFirePoint(Transform firePoint)
    {
        return firePoint && (!modelRoot || !firePoint.IsChildOf(modelRoot));
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
        if (!anim)
            return null;

        var namedMount = FindChildByName(anim.transform, right ? rightHandName : leftHandName);
        if (namedMount)
            return namedMount;

        namedMount = FindChildByName(anim.transform, right ? "Weapon.R" : "Weapon.L");
        if (namedMount)
            return namedMount;

        namedMount = FindChildByName(anim.transform, right ? "hand.r" : "hand.l");
        if (namedMount)
            return namedMount;

        if (anim.isHuman)
            return anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

        return null;
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

    private static Transform FindChildByNameExcluding(Transform root, string targetName, Transform excludedRoot)
    {
        if (!root)
            return null;

        if (excludedRoot && root != excludedRoot && root.IsChildOf(excludedRoot))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByNameExcluding(root.GetChild(i), targetName, excludedRoot);
            if (found)
                return found;
        }

        return null;
    }
}
