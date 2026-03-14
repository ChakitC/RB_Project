using UnityEngine;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Layer")]
    public LayerMask characterLayer;
    public LayerMask groundLayer;

    private Transform _selected;
    private CharacterSelectable _selectedSelectable;
    private float _startY = 0f;
    private Vector3 _offset;

    void Update()
    {
        HandleClickAndDrag();
    }

    void HandleClickAndDrag()
    {
        if (Input.GetMouseButtonDown(0))
            TrySelectCharacter();

        if (Input.GetMouseButton(0) && _selected != null)
            DragFollowGround();

        if (Input.GetMouseButtonUp(0) && _selected != null)
        {
            ResetY();

            if (_selectedSelectable != null)
                _selectedSelectable.SetPicked(false);

            _selected = null;
            _selectedSelectable = null;
        }
    }

    void TrySelectCharacter()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, characterLayer))
        {
            var selectable = hit.collider.GetComponentInParent<CharacterSelectable>();
            if (selectable == null) return;

            _selected = selectable.transform;
            _selectedSelectable = selectable;

            

            if (Physics.Raycast(ray, out RaycastHit hitGround, 1000f, groundLayer))
            {
                Debug.Log("Set Offset");
                _offset = _selected.position - hitGround.point;
            }
            else
            {
                Debug.Log("Set OffsetXZ = zero");
                _offset = Vector3.zero;
            }

            _selectedSelectable.SetPicked(true);
        }
    }

    void DragFollowGround()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            float dragHoverY = 0.3f;
            Vector3 newPos = hit.point + _offset + new Vector3(0f, dragHoverY, 0f);
            _selected.position = newPos;
        }
    }

    void ResetY()
    {
        Vector3 pos = _selected.position;
        pos.y = _startY;
        _selected.position = pos;
    }
}
