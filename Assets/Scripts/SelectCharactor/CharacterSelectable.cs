using UnityEngine;
using System.Collections;

public class CharacterSelectable : MonoBehaviour
{
    [Header("Important Reference")]
    public Animator animator;
    public CapsuleCollider capsuleCollider;

    [Header("Animator Conditions Name")]
    public string pickedBoolName = "IsPicked";

    [Header("Definition Sync")]
    public bool syncDefToParentSlot = true;

    [Header("LoadParty")]
    public string IDCharacter;
    [SerializeField] private string fallbackId = "";
    [SerializeField] private int currentSlot;
    [SerializeField] private int partyIndex = 0;

    Coroutine cacheCo;

    CharacterDefHolder _defRef;
    PartySlot _parentSlot;
    CharacterStats _lastDef;

    void OnEnable() => ScheduleCache(true);

    void OnTransformChildrenChanged() => ScheduleCache(true);
    void OnTransformParentChanged() => ScheduleCache(true);

    void ScheduleCache(bool force)
    {
        if (cacheCo != null) StopCoroutine(cacheCo);
        cacheCo = StartCoroutine(CacheNextFrame(force));
    }

    IEnumerator CacheNextFrame(bool force)
    {
        yield return null;
        CacheRefs(force);
        if (syncDefToParentSlot) SyncDefToSlot(force);
        cacheCo = null;
    }

    void CacheRefs(bool force)
    {
        bool animatorInvalid = animator == null || !animator.transform.IsChildOf(transform);
        bool colliderInvalid = capsuleCollider == null || !capsuleCollider.transform.IsChildOf(transform);

        if (force || animatorInvalid)
            animator = GetComponentInChildren<Animator>(true);

        if (force || colliderInvalid)
            capsuleCollider = GetComponentInChildren<CapsuleCollider>(true);

        if (force || _defRef == null || !_defRef.transform.IsChildOf(transform))
            _defRef = GetComponentInChildren<CharacterDefHolder>(true);

        if (force || _parentSlot == null)
            _parentSlot = GetComponentInParent<PartySlot>(true);
    }

    void SyncDefToSlot(bool force)
    {
        if (_parentSlot == null) return;

        var def = (_defRef != null) ? _defRef.def : null;

        if (!force && def == _lastDef) return;

        if (def == null)
        {
            _lastDef = def;
            _parentSlot.SetCharacterDef(null, true);
            return;
        }

        _lastDef = def;
        _parentSlot.SetCharacterDef(def, true);

        if (_parentSlot.Selected == def && _parentSlot.levelSystem != null)
            _parentSlot.levelSystem.SetState();
    }

    public void SetPicked(bool isPicked)
    {
        if (!animator || !animator.transform.IsChildOf(transform))
            CacheRefs(force: true);

        if (!animator)
        {
            Debug.LogWarning($"[CharacterSelectable] Animator not found under {name}.", this);
            return;
        }

        animator.SetBool(pickedBoolName, isPicked);
    }
}
