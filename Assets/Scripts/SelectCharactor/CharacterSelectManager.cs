using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Layer")]
    public LayerMask characterLayer;
    public LayerMask groundLayer;

    private Transform _selected;
    private CharacterSelectable _selectedSelectable;
    private float _startY = 0f;
    private Vector3 _offset;
    readonly Dictionary<CharacterEventVoiceLine, float> _voiceReadyAt = new();

    void Update()
    {
        HandleClickAndDrag();
    }

    void HandleClickAndDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI())
                return;

            TrySelectCharacter();
        }

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
            PlayPickupCharacterVoice(_selectedSelectable);
        }
    }

    void PlayPickupCharacterVoice(CharacterSelectable selectable)
    {
        CharacterStats stats = ResolveCharacterStats(selectable);
        CharacterVoiceProfile voiceProfile = stats != null ? stats.voiceProfile : null;
        CharacterVoicePlayback.TryPlayAtPosition(
            voiceProfile != null ? voiceProfile.pickupCharacterVoice : null,
            selectable != null ? selectable.transform.position : transform.position,
            _voiceReadyAt);
    }

    static CharacterStats ResolveCharacterStats(CharacterSelectable selectable)
    {
        if (selectable == null)
            return null;

        CharacterDefHolder holder = selectable.GetComponentInChildren<CharacterDefHolder>(true);
        if (holder != null && holder.def != null)
            return holder.def;

        PartySlot slot = selectable.GetComponentInParent<PartySlot>(true);
        return slot != null ? slot.Selected : null;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began &&
                    EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return true;
            }
        }

        return EventSystem.current.IsPointerOverGameObject();
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
