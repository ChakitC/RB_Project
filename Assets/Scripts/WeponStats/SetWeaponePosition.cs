using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

[ExecuteAlways]
public class SetWeaponePosition : MonoBehaviour
{
    enum HandParentNamePreset
    {
        PresetWeapon,
        PresetHand
    }

#if ODIN_INSPECTOR
    [TitleGroup("Config")]
    [BoxGroup("Config/Target")]
    [PropertyOrder(0)]
#endif
    [Header("Config")]
    [SerializeField] GunConfig targetConfig;

#if ODIN_INSPECTOR
    [BoxGroup("Config/Options")]
    [HorizontalGroup("Config/Options/Flags")]
    [PropertyOrder(1)]
#endif
    [SerializeField] bool enableOverrideOnSave = true;

#if ODIN_INSPECTOR
    [BoxGroup("Config/Options")]
    [HorizontalGroup("Config/Options/Flags")]
    [PropertyOrder(2)]
#endif
    [SerializeField] bool saveAssetsImmediately = true;

#if ODIN_INSPECTOR
    [TitleGroup("Current Models")]
    [LabelWidth(130)]
    [PropertyOrder(20)]
#endif
#if !ODIN_INSPECTOR
    [Header("Weapon Models")]
#endif
    [SerializeField] Transform rightHandWeapon;

#if ODIN_INSPECTOR
    [LabelWidth(130)]
    [PropertyOrder(21)]
#endif
    [SerializeField] Transform leftHandWeapon;

#if ODIN_INSPECTOR
    [TitleGroup("Hand Parents")]
    [LabelWidth(130)]
    [PropertyOrder(18)]
#endif
#if !ODIN_INSPECTOR
    [Header("Create From Config")]
#endif
    [SerializeField] Transform rightHandParent;

#if ODIN_INSPECTOR
    [TitleGroup("Hand Parents")]
    [LabelWidth(130)]
    [PropertyOrder(19)]
#endif
    [SerializeField] Transform leftHandParent;

#if ODIN_INSPECTOR
    [BoxGroup("Hand Parents/Search")]
    [PropertyOrder(12)]
#endif
    [SerializeField] Transform handSearchRoot;

#if ODIN_INSPECTOR
    [BoxGroup("Hand Parents/Search")]
    [LabelWidth(160)]
    [OnValueChanged(nameof(ApplyHandParentNamePreset))]
    [PropertyOrder(13)]
#endif
    [SerializeField] HandParentNamePreset handParentNamePreset = HandParentNamePreset.PresetHand;

#if ODIN_INSPECTOR
    [BoxGroup("Hand Parents/Search")]
    [LabelWidth(160)]
    [PropertyOrder(14)]
#endif
    [SerializeField] string rightHandParentName = "hand.r";

#if ODIN_INSPECTOR
    [BoxGroup("Hand Parents/Search")]
    [LabelWidth(160)]
    [PropertyOrder(15)]
#endif
    [SerializeField] string leftHandParentName = "hand.l";

#if ODIN_INSPECTOR
    [TitleGroup("Create Models")]
    [PropertyOrder(30)]
#endif
    [SerializeField] bool clearExistingModelOnCreate = true;

    public GunConfig TargetConfig
    {
        get => targetConfig;
        set => targetConfig = value;
    }

    public Transform RightHandWeapon
    {
        get => rightHandWeapon;
        set => rightHandWeapon = value;
    }

    public Transform LeftHandWeapon
    {
        get => leftHandWeapon;
        set => leftHandWeapon = value;
    }

#if ODIN_INSPECTOR
    [TitleGroup("Hand Parents")]
    [ButtonGroup("Hand Parents/Actions")]
    [Button("Find Hand Parents By Name")]
    [PropertyOrder(17)]
#endif
    [ContextMenu("Find Hand Parents By Name")]
    public void FindHandParentsByName()
    {
        var root = handSearchRoot ? handSearchRoot : transform;
        if (!root)
        {
            Debug.LogWarning("[SetWeaponePosition] Hand search root is missing.", this);
            return;
        }

        RecordToolChange("Find Hand Parents By Name");

        rightHandParent = FindChildByName(root, rightHandParentName);
        leftHandParent = FindChildByName(root, leftHandParentName);

        MarkToolDirty();

        if (!rightHandParent)
            Debug.LogWarning($"[SetWeaponePosition] Right hand parent not found: {rightHandParentName}", this);

        if (!leftHandParent)
            Debug.LogWarning($"[SetWeaponePosition] Left hand parent not found: {leftHandParentName}", this);
    }

    public void ApplyHandParentNamePreset()
    {
        RecordToolChange("Apply Hand Parent Name Preset");

        switch (handParentNamePreset)
        {
            case HandParentNamePreset.PresetWeapon:
                rightHandParentName = "Weapon.R";
                leftHandParentName = "Weapon.L";
                break;
            case HandParentNamePreset.PresetHand:
                rightHandParentName = "hand.r";
                leftHandParentName = "hand.l";
                break;
        }

        MarkToolDirty();
    }

#if ODIN_INSPECTOR
    [TitleGroup("Create Models")]
    [Button("Create Both")]
    [PropertyOrder(31)]
#endif
    [ContextMenu("Create Both Hand Models From Config")]
    public void CreateBothHandModelsFromConfig()
    {
        CreateWeaponModelFromConfig(true);
        CreateWeaponModelFromConfig(false);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Create Models")]
    [ButtonGroup("Create Models/Actions")]
    [Button("Create Right")]
    [PropertyOrder(32)]
#endif
    [ContextMenu("Create Right Hand Model From Config")]
    public void CreateRightHandModelFromConfig()
    {
        CreateWeaponModelFromConfig(true);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Create Models")]
    [ButtonGroup("Create Models/Actions")]
    [Button("Create Left")]
    [PropertyOrder(33)]
#endif
    [ContextMenu("Create Left Hand Model From Config")]
    public void CreateLeftHandModelFromConfig()
    {
        CreateWeaponModelFromConfig(false);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Save To Config")]
    [ButtonGroup("Save To Config/Actions")]
    [Button("Save Right")]
    [PropertyOrder(51)]
#endif
    [ContextMenu("Save Right Hand To Config")]
    public void SaveRightHandToConfig()
    {
        if (!CanSave(rightHandWeapon, "right hand"))
            return;

        RecordConfigChange("Save Right Hand Weapon Position");
        targetConfig.rightHandWeaponModelMount = ReadOffset(rightHandWeapon);
        ApplyOverrideFlag();
        MarkConfigDirty();
        Debug.Log($"[SetWeaponePosition] Saved right hand weapon position to {targetConfig.name}.", targetConfig);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Save To Config")]
    [ButtonGroup("Save To Config/Actions")]
    [Button("Save Left")]
    [PropertyOrder(52)]
#endif
    [ContextMenu("Save Left Hand To Config")]
    public void SaveLeftHandToConfig()
    {
        if (!CanSave(leftHandWeapon, "left hand"))
            return;

        RecordConfigChange("Save Left Hand Weapon Position");
        targetConfig.leftHandWeaponModelMount = ReadOffset(leftHandWeapon);
        ApplyOverrideFlag();
        MarkConfigDirty();
        Debug.Log($"[SetWeaponePosition] Saved left hand weapon position to {targetConfig.name}.", targetConfig);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Save To Config")]
    [Button("Save Both")]
    [PropertyOrder(53)]
#endif
    [ContextMenu("Save Both Hands To Config")]
    public void SaveBothHandsToConfig()
    {
        if (!targetConfig)
        {
            Debug.LogWarning("[SetWeaponePosition] Target config is missing.", this);
            return;
        }

        if (!rightHandWeapon && !leftHandWeapon)
        {
            Debug.LogWarning("[SetWeaponePosition] No weapon transform assigned.", this);
            return;
        }

        RecordConfigChange("Save Both Weapon Positions");

        if (rightHandWeapon)
            targetConfig.rightHandWeaponModelMount = ReadOffset(rightHandWeapon);

        if (leftHandWeapon)
            targetConfig.leftHandWeaponModelMount = ReadOffset(leftHandWeapon);

        ApplyOverrideFlag();
        MarkConfigDirty();
        Debug.Log($"[SetWeaponePosition] Saved weapon positions to {targetConfig.name}.", targetConfig);
    }

#if ODIN_INSPECTOR
    [TitleGroup("Mirror Transform")]
    [ButtonGroup("Mirror Transform/Actions")]
    [Button("Right To Left")]
    [PropertyOrder(41)]
#endif
    [ContextMenu("Mirror Right Transform To Left")]
    public void MirrorRightTransformToLeft()
    {
        MirrorTransformToOtherHand(rightHandWeapon, leftHandWeapon, "right hand", "left hand");
    }

#if ODIN_INSPECTOR
    [TitleGroup("Mirror Transform")]
    [ButtonGroup("Mirror Transform/Actions")]
    [Button("Left To Right")]
    [PropertyOrder(42)]
#endif
    [ContextMenu("Mirror Left Transform To Right")]
    public void MirrorLeftTransformToRight()
    {
        MirrorTransformToOtherHand(leftHandWeapon, rightHandWeapon, "left hand", "right hand");
    }

    bool CanSave(Transform source, string label)
    {
        if (!targetConfig)
        {
            Debug.LogWarning("[SetWeaponePosition] Target config is missing.", this);
            return false;
        }

        if (!source)
        {
            Debug.LogWarning($"[SetWeaponePosition] {label} weapon transform is missing.", this);
            return false;
        }

        return true;
    }

    void ApplyOverrideFlag()
    {
        if (enableOverrideOnSave)
            targetConfig.overrideWeaponModelMountOffset = true;
    }

    void CreateWeaponModelFromConfig(bool rightHand)
    {
        var label = rightHand ? "right hand" : "left hand";

        if (!CanCreate(label))
            return;

        var parent = ResolveCreateParent(rightHand);
        if (!parent)
        {
            Debug.LogWarning($"[SetWeaponePosition] {label} parent is missing.", this);
            return;
        }

        var oldModel = rightHand ? rightHandWeapon : leftHandWeapon;
        if (clearExistingModelOnCreate && oldModel)
            DestroyOldModel(oldModel);

        var weaponObject = InstantiateWeaponPrefab(parent);
        if (!weaponObject)
            return;

        weaponObject.name = $"{targetConfig.WeaponPrefab.name}_{(rightHand ? "Right" : "Left")}_EditorPreview";
        ApplyOffset(
            weaponObject.transform,
            rightHand ? targetConfig.rightHandWeaponModelMount : targetConfig.leftHandWeaponModelMount);

        if (rightHand)
            rightHandWeapon = weaponObject.transform;
        else
            leftHandWeapon = weaponObject.transform;

        MarkToolDirty();
        Debug.Log($"[SetWeaponePosition] Created {label} weapon model from {targetConfig.name}.", this);
    }

    void MirrorTransformToOtherHand(Transform source, Transform target, string sourceLabel, string targetLabel)
    {
        if (!source)
        {
            Debug.LogWarning($"[SetWeaponePosition] {sourceLabel} weapon transform is missing.", this);
            return;
        }

        if (!target)
        {
            Debug.LogWarning($"[SetWeaponePosition] {targetLabel} weapon transform is missing.", this);
            return;
        }

        RecordTransformChange(target, $"Mirror {sourceLabel} Weapon Transform");
        ApplyOffset(target, MirrorOffset(ReadOffset(source)));
        MarkToolDirty();
        Debug.Log($"[SetWeaponePosition] Mirrored {sourceLabel} weapon transform to {targetLabel}.", this);
    }

    bool CanCreate(string label)
    {
        if (!targetConfig)
        {
            Debug.LogWarning("[SetWeaponePosition] Target config is missing.", this);
            return false;
        }

        if (!targetConfig.WeaponPrefab)
        {
            Debug.LogWarning($"[SetWeaponePosition] WeaponPrefab missing in {targetConfig.name}.", targetConfig);
            return false;
        }

        return true;
    }

    Transform ResolveCreateParent(bool rightHand)
    {
        var explicitParent = rightHand ? rightHandParent : leftHandParent;
        if (explicitParent)
            return explicitParent;

        var currentModel = rightHand ? rightHandWeapon : leftHandWeapon;
        if (currentModel && currentModel.parent)
            return currentModel.parent;

        return transform;
    }

    GameObject InstantiateWeaponPrefab(Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var prefabObject = UnityEditor.PrefabUtility.InstantiatePrefab(targetConfig.WeaponPrefab, parent) as GameObject;
            if (prefabObject)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(prefabObject, "Create Weapon Model From Config");
                return prefabObject;
            }
        }
#endif

        return Instantiate(targetConfig.WeaponPrefab, parent, false);
    }

    void DestroyOldModel(Transform oldModel)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.DestroyObjectImmediate(oldModel.gameObject);
            return;
        }
#endif

        Destroy(oldModel.gameObject);
    }

    static WeaponModelMountOffset ReadOffset(Transform source)
    {
        return new WeaponModelMountOffset
        {
            localPosition = source.localPosition,
            localRotationEuler = source.localRotation.eulerAngles,
            localScale = source.localScale == Vector3.zero ? Vector3.one : source.localScale
        };
    }

    static WeaponModelMountOffset MirrorOffset(WeaponModelMountOffset source)
    {
        return new WeaponModelMountOffset
        {
            localPosition = new Vector3(-source.localPosition.x, source.localPosition.y, source.localPosition.z),
            localRotationEuler = new Vector3(
                NormalizeEuler(source.localRotationEuler.x + 180f),
                NormalizeEuler(-source.localRotationEuler.y),
                NormalizeEuler(-source.localRotationEuler.z)),
            localScale = source.SafeLocalScale
        };
    }

    static float NormalizeEuler(float value)
    {
        return Mathf.Repeat(value, 360f);
    }

    static void ApplyOffset(Transform target, WeaponModelMountOffset offset)
    {
        target.localPosition = offset.localPosition;
        target.localRotation = Quaternion.Euler(offset.localRotationEuler);
        target.localScale = offset.SafeLocalScale;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    void RecordConfigChange(string undoName)
    {
#if UNITY_EDITOR
        if (targetConfig)
            UnityEditor.Undo.RecordObject(targetConfig, undoName);
#endif
    }

    void RecordTransformChange(Transform target, string undoName)
    {
#if UNITY_EDITOR
        if (target)
            UnityEditor.Undo.RecordObject(target, undoName);
#endif
    }

    void RecordToolChange(string undoName)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, undoName);
#endif
    }

    void MarkConfigDirty()
    {
#if UNITY_EDITOR
        if (!targetConfig)
            return;

        UnityEditor.EditorUtility.SetDirty(targetConfig);

        if (saveAssetsImmediately)
            UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    void MarkToolDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);

        if (!Application.isPlaying && gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
