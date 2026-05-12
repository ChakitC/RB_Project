using UnityEngine;
using Object = UnityEngine.Object;

public class CharacterDragVisualPreview : MonoBehaviour
{
    const string ModelRootName = "DragModelRoot";

    [Header("Runtime")]
    [SerializeField] private CharacterDefHolder holder;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Animator animator;

    GameObject currentModel;
    CharacterStats currentDef;

    public Animator Animator => animator;
    public CharacterStats CurrentDef => currentDef;

    public bool Build(CharacterStats selected)
    {
        EnsureRefs();

        if (holder)
            holder.def = selected;

        if (!selected)
        {
            Clear();
            return false;
        }

        if (currentDef != selected || !currentModel)
        {
            ClearModel();

            if (!selected.CharacterPrefabBasement)
            {
                Debug.LogError($"[CharacterDragVisualPreview] CharacterPrefabBasement is null (selected: {selected.name})", this);
                return false;
            }

            currentModel = Instantiate(selected.CharacterPrefabBasement, modelRoot, false);
            currentModel.name = $"{selected.name}_DragModel";
            DisablePreviewEquipmentSaveParticipation(currentModel);

            var t = currentModel.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            currentDef = selected;
        }

        ResolveAnimatorFromCurrentModel();
        if (!animator)
        {
            Debug.LogError($"[CharacterDragVisualPreview] model has no Animator (selected: {selected.name})", currentModel);
            return false;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        SetPicked(false);
        return true;
    }

    public void SetPicked(bool picked)
    {
        if (!animator)
            return;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            if (parameter.name != "IsPicked")
                continue;

            animator.SetBool(parameter.name, picked);
            return;
        }
    }

    public void Clear()
    {
        ClearModel();
        currentDef = null;

        if (holder)
            holder.def = null;
    }

    void EnsureRefs()
    {
        if (!holder)
        {
            holder = GetComponent<CharacterDefHolder>();
            if (!holder)
                holder = gameObject.AddComponent<CharacterDefHolder>();
        }

        if (!modelRoot)
        {
            var existing = transform.Find(ModelRootName);
            if (existing)
            {
                modelRoot = existing;
            }
            else
            {
                var rootObject = new GameObject(ModelRootName);
                modelRoot = rootObject.transform;
                modelRoot.SetParent(transform, false);
            }
        }
    }

    void ResolveAnimatorFromCurrentModel()
    {
        animator = null;

        if (!currentModel)
            return;

        animator = currentModel.GetComponentInChildren<Animator>(true);
        if (!animator)
            animator = currentModel.GetComponent<Animator>();
    }

    void ClearModel()
    {
        animator = null;

        if (currentModel)
        {
            SafeDestroy(currentModel);
            currentModel = null;
            return;
        }

        if (!modelRoot)
            return;

        for (int i = modelRoot.childCount - 1; i >= 0; i--)
        {
            var child = modelRoot.GetChild(i);
            if (!child)
                continue;

            child.gameObject.SetActive(false);
            SafeDestroy(child.gameObject);
        }
    }

    static void DisablePreviewEquipmentSaveParticipation(GameObject root)
    {
        if (!root)
            return;

        var equipments = root.GetComponentsInChildren<CharacterEquipment>(true);
        for (int i = 0; i < equipments.Length; i++)
            equipments[i].SetPlayerInventorySaveParticipation(false);
    }

    static void SafeDestroy(GameObject go)
    {
        if (!go)
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
