using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BasementGunMenuWeaponHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject highlight;
    [SerializeField] private float delay = 0.55f;
    BasementGunMenuWeapons owner;
    InventorySlotData data;
    Coroutine pending;

    public void Bind(BasementGunMenuWeapons view, InventorySlotData value)
    {
        Clear();
        owner = view;
        data = value;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        Clear();
        if (owner == null || !owner.CanHover || data == null || !data.HasWeaponInstance) return;
        if (highlight != null) highlight.SetActive(true);
        pending = StartCoroutine(ShowAfterDelay(e));
    }

    IEnumerator ShowAfterDelay(PointerEventData e)
    {
        yield return new WaitForSecondsRealtime(delay);
        pending = null;
        if (owner != null && owner.CanHover)
            owner.ShowHoverDetails(this, data, e.position, e.enterEventCamera);
    }

    public void OnPointerExit(PointerEventData e) => Clear();
    public void Clear()
    {
        if (pending != null) StopCoroutine(pending);
        pending = null;
        if (highlight != null) highlight.SetActive(false);
        if (owner != null) owner.HideHoverDetails(this);
    }
    void OnDisable() => Clear();
}
