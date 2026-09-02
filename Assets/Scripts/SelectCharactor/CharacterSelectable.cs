using UnityEngine;
using System.Collections;
using ZLZ.AnimeShader;

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
    Coroutine outlineCo;

    CharacterDefHolder _defRef;
    PartySlot _parentSlot;
    CharacterStats _lastDef;
    ZLZ_SelectionController _outline;
    bool _picked;

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
        RefreshSelectionOutline();
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

        bool outlineInvalid = _outline == null || !_outline.transform.IsChildOf(transform);
        if (force || outlineInvalid)
            _outline = GetComponentInChildren<ZLZ_SelectionController>(true);
    }

    /// <summary>
    /// Re-scan the outline renderers and put the outline back in sync with the picked state.
    /// Child models and weapon previews are spawned after the controller's Awake, and some of
    /// them are authored with Rendering Layers = "Everything" — which opts them into the
    /// selection-outline layer and would leave the outline drawn permanently. Clearing it here
    /// keeps the outline tied to the pick-up gesture only.
    /// Call after any child model or weapon preview is (re)built.
    /// </summary>
    /// <summary>
    /// Same as <see cref="RefreshSelectionOutline"/> but one frame later, so callers can fire it
    /// while a model/weapon rebuild is still in flight and still land after the last spawn.
    /// </summary>
    public void RefreshSelectionOutlineDeferred()
    {
        if (!isActiveAndEnabled)
        {
            RefreshSelectionOutline();
            return;
        }

        if (outlineCo != null) StopCoroutine(outlineCo);
        outlineCo = StartCoroutine(RefreshOutlineNextFrame());
    }

    IEnumerator RefreshOutlineNextFrame()
    {
        yield return null;
        RefreshSelectionOutline();
        outlineCo = null;
    }

    public void RefreshSelectionOutline()
    {
        if (_outline == null || !_outline.transform.IsChildOf(transform))
            _outline = GetComponentInChildren<ZLZ_SelectionController>(true);

        if (_outline == null) return;

        _outline.RefreshRenderers();

        if (_picked)
            _outline.Select();
        else
            ClearAllSelectionLayers();
    }

    // "Everything" opts a renderer into every selection type at once (Character draws cyan,
    // Item draws green), so clearing only the default type leaves the other outlines behind.
    void ClearAllSelectionLayers()
    {
        _outline.Deselect(ZLZ_SelectionController.SelectionType.Character);
        _outline.Deselect(ZLZ_SelectionController.SelectionType.Enemy);
        _outline.Deselect(ZLZ_SelectionController.SelectionType.Item);
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

        _picked = isPicked;
        ApplySelectionOutline(isPicked);

        if (!animator)
        {
            Debug.LogWarning($"[CharacterSelectable] Animator not found under {name}.", this);
            return;
        }

        animator.SetBool(pickedBoolName, isPicked);
    }

    void ApplySelectionOutline(bool isPicked)
    {
        if (_outline == null || !_outline.transform.IsChildOf(transform))
            _outline = GetComponentInChildren<ZLZ_SelectionController>(true);

        if (_outline == null) return;

        // Weapon previews are attached after the controller cached its renderers, so re-scan
        // before showing the outline or they are left out of it.
        _outline.RefreshRenderers();

        if (isPicked)
        {
            ClearAllSelectionLayers();
            _outline.Select();
        }
        else
        {
            // Animated outro — do not hard-clear here or it would cut the fade off.
            _outline.Deselect();
        }
    }
}
