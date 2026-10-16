using UnityEngine;

public class CharacterDragVisualPreview : MonoBehaviour
{
    public BasementCharacterPresentation presentationPrefab;

    [Header("Runtime")]
    [SerializeField] private CharacterDefHolder holder;
    [SerializeField] private Transform modelRoot;

    BasementCharacterPresentation presentation;

    public Animator Animator => presentation != null ? presentation.Animator : null;
    public BasementCharacterPresentation Presentation => presentation;
    public CharacterStats CurrentDef => presentation != null ? presentation.CurrentDef : null;

    public bool Build(CharacterStats selected)
    {
        if (!holder)
        {
            holder = GetComponent<CharacterDefHolder>();
            if (!holder)
                holder = gameObject.AddComponent<CharacterDefHolder>();
        }
        if (!selected)
        {
            Clear();
            return false;
        }
        if (!presentation)
        {
            if (!presentationPrefab)
            {
                Debug.LogError("[CharacterDragVisualPreview] Assign the shared presentation prefab.", this);
                return false;
            }
            presentation = Instantiate(presentationPrefab, modelRoot ? modelRoot : transform, false);
        }

        bool built = presentation.Build(selected, true);
        holder.def = built ? selected : null;
        var selectable = GetComponent<CharacterSelectable>();
        if (selectable) selectable.BindPresentation(presentation);
        return built;
    }

    public void SetPicked(bool picked)
    {
        if (presentation)
            presentation.SetPicked(picked);
    }

    public void Clear()
    {
        if (presentation)
            presentation.Clear();
        var selectable = GetComponent<CharacterSelectable>();
        if (selectable) selectable.BindPresentation(presentation);
        if (holder)
            holder.def = null;
    }
}
