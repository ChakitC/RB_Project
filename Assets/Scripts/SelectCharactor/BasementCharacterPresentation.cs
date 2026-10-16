using UnityEngine;

/// <summary>Shared model and animation owner for slot and drag previews.</summary>
[DisallowMultipleComponent]
public sealed class BasementCharacterPresentation : MonoBehaviour
{
    public void EnterMenu(BasementAnimationProfileSO.MenuEntry entry)
    {
        EndDrag();
        animationController?.EnterMenu(entry);
    }
    public void ExitMenu() => animationController?.ExitMenu();
    public void SwitchMenu(BasementAnimationProfileSO.MenuEntry entry) => animationController?.SwitchMenu(entry);
    public void SetInCharacterMenu(bool value)
    {
        if (value) EndDrag();
        animationController?.SetInCharacterMenu(value);
    }
    [SerializeField] private Transform modelRoot;
    [SerializeField] private BasementAnimationController animationController;
    [SerializeField] private BasementWeaponPresentation weapons;
    [SerializeField] private BasementDragSway dragSway;

    public GameObject Model { get; private set; }
    public CharacterStats CurrentDef { get; private set; }
    public BasementAnimationController Animation => animationController;
    public Animator Animator => animationController != null ? animationController.Animator : null;
    public bool IsPicked => animationController != null && animationController.IsPicked;

    public void SetPicked(bool picked)
    {
        if (!picked) EndDrag();
        animationController?.SetPicked(picked);
    }
    public void ResetToIdle()
    {
        EndDrag();
        animationController?.ResetToIdle();
    }
    public void BeginDrag(Camera camera, Vector2 screenPosition) => dragSway?.BeginDrag(camera, screenPosition);
    public void UpdateDrag(Vector2 screenPosition) => dragSway?.UpdateDrag(screenPosition);
    public void EndDrag() => dragSway?.EndDrag();
    void OnDisable() => EndDrag();

    public void ShowWeapon(GunConfig weapon)
    {
        if (weapons != null)
            weapons.Show(Animator, weapon, CurrentDef != null ? CurrentDef.weaponHandMode : CharacterWeaponHandMode.RightHand);
    }

    public void ClearWeapon() => weapons?.Clear();

    public bool Build(CharacterStats selected)
        => Build(selected, false);

    public bool Build(CharacterStats selected, bool startPicked)
    {
        if (!selected || !selected.CharacterPrefabBasement || !modelRoot)
        {
            Clear();
            return false;
        }

        if (CurrentDef == selected && Model)
        {
            return true;
        }

        Clear();
        CurrentDef = selected;
        Model = Instantiate(selected.CharacterPrefabBasement, modelRoot, false);
        Model.name = selected.name + "_Model";
        Model.transform.localPosition = Vector3.zero;
        Model.transform.localRotation = Quaternion.identity;
        foreach (var equipment in Model.GetComponentsInChildren<CharacterEquipment>(true))
            equipment.SetPlayerInventorySaveParticipation(false);

        var targetRoot = string.IsNullOrEmpty(selected.basementAnimatorPath)
            ? Model.transform : Model.transform.Find(selected.basementAnimatorPath);
        var target = targetRoot != null ? targetRoot.GetComponent<Animator>() : null;
        // Measure the authored body before the chosen initial animation changes its pose.
        dragSway?.Bind(Model);
        if (animationController != null)
            animationController.Bind(target, selected.basementAnimationProfile, startPicked);
        else
            Debug.LogWarning("[Basement Presentation] Assign the shared animation controller.", this);
        // Missing animation data must not prevent selection, outline or model display.
        return true;
    }

    public void Clear()
    {
        dragSway?.Unbind();
        ClearWeapon();
        animationController?.Unbind();
        if (Model)
        {
            Model.SetActive(false);
            if (Application.isPlaying)
                Destroy(Model);
            else
                DestroyImmediate(Model);
        }
        Model = null;
        CurrentDef = null;
    }
}
