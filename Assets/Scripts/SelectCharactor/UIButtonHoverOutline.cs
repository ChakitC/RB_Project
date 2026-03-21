using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverOutline : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] public BasementContext bct;
    [SerializeField] private GameObject outline;
    [SerializeField] public string MapName;
    [SerializeField] private AudioCue hoverCue;
    [SerializeField] private AudioCue selectCue;
    
    public TextMeshProUGUI TextMeshMapname;
    
    private static UIButtonHoverOutline _currentSelected;

    private bool _isSelected = false;
    
    private void Awake()
    {
        TextMeshMapname.text = MapName;
        
        if (outline != null)
            outline.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!bct.camManager._inMapUI) return;

        if (outline != null)
            outline.SetActive(true);

        if (hoverCue != null)
            AudioService.Instance.Play(hoverCue);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (outline == null) return;

     
        if (!_isSelected)
            outline.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!bct.camManager._inMapUI) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (outline == null) return;

       
        if (_currentSelected != null && _currentSelected != this)
        {
            _currentSelected.SetSelected(false);
        }
        
        SetSelected(true);
        _currentSelected = this;

        if (selectCue != null)
            AudioService.Instance.Play(selectCue);
    }

    private void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (outline != null)
            outline.SetActive(selected);

        if (SceneLoaderSystem.Instance == null)
        {
            Debug.Log("SceneLoaderSystem Missing");
            return;
        }
        SceneLoaderSystem.Instance.SetMapToLoad(MapName);
    }

  
    public void Deselect()
    {
        Debug.Log("deselect");
        if (_currentSelected == this)
            _currentSelected = null;
        
        
        SetSelected(false);
        
    }
}
