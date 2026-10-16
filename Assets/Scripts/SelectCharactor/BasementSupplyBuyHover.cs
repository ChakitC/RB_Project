using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BasementSupplyBuyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject outline;

    private void OnEnable() => SetVisible(false);
    private void OnDisable() => SetVisible(false);
    public void OnPointerEnter(PointerEventData eventData) => SetVisible(true);
    public void OnPointerExit(PointerEventData eventData) => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (outline != null) outline.SetActive(visible);
    }
}
