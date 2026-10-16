using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BasementGunMenuWeaponDropSlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private BasementGunMenuWeapons owner;
    public void OnDrop(PointerEventData e)
    {
        if (owner != null && e.pointerDrag != null)
            owner.DropWeapon(e.pointerDrag.GetComponent<BasementGunMenuWeaponCard>());
    }
}
