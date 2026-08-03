using System;
using System.Collections;
using UnityEngine;

public class Interactor : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;
    public CharacteContext OwnerContext => ctx;

    [Header("Ray")]
    [SerializeField] private bool _Debug;
    [SerializeField] private float maxDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private float originHeight = 1.2f;
    [SerializeField] private float selfOffset = 0.05f;
    [SerializeField] private bool useSphereCast = true;
    [SerializeField] private float sphereRadius = 0.18f;

    [Header("Debug/Optional")]
    public string currentPrompt;

    public event Action<IInteractable> FocusChanged;
    public event Action<float> HoldProgressChanged;
    public event Action HoldCompleted;

    public IInteractable FocusedInteractable => _focus;
    public Collider FocusedCollider => _focusCollider;
    public bool IsHolding => _holding;
    public float HoldProgress { get; private set; }

    private IInteractable _focus;
    private Collider _focusCollider;
    private IInteractable _lockedTarget;
    private IHoldInteractable _lockedHoldTarget;
    private Coroutine _holdRoutine;
    private bool _holding;

    void Awake()
    {
        if (!ctx) ctx = GetComponentInParent<CharacteContext>();
        ctx?.ResolveReferences();

        if (ctx != null && ctx.Interactor != this)
            ctx.Interactor = this;
    }

    void Update()
    {
        // สำคัญ: ระหว่าง hold อย่าอัปเดต focus ทับ
        if (_holding) return;

        UpdateFocus();
    }

    void UpdateFocus()
    {
        IInteractable newFocus = FindBestFocus(out Collider newFocusCollider);
        bool focusChanged = !ReferenceEquals(newFocus, _focus);

        if (focusChanged)
        {
            if (_focus is IFocusable oldF) oldF.OnFocusExit(this);
            _focus = newFocus;
            _focusCollider = newFocusCollider;
            if (_focus is IFocusable newF) newF.OnFocusEnter(this);
        }
        else
        {
            _focusCollider = newFocusCollider;
        }

        currentPrompt = (_focus != null) ? _focus.GetPrompt(this) : "";
        if (focusChanged)
            FocusChanged?.Invoke(_focus);
    }

    IInteractable FindBestFocus(out Collider focusCollider)
    {
        focusCollider = null;
        Vector3 interactionOrigin = transform.position + Vector3.up * originHeight;

        float r = (ctx != null && ctx.cc) ? ctx.cc.radius : 0.3f;
        float ownerReach = r + selfOffset + maxDistance;
        Vector3 origin = interactionOrigin + transform.forward * (r + selfOffset);

        Vector3 dir = transform.forward;
        float castDistance = maxDistance;
        bool usingAimRay = TryResolveAimCast(
            interactionOrigin,
            ownerReach,
            out Vector3 aimOrigin,
            out Vector3 aimDirection,
            out float aimCastDistance);

        if (usingAimRay)
        {
            origin = aimOrigin;
            dir = aimDirection;
            castDistance = aimCastDistance;
        }

        RaycastHit[] hits = useSphereCast
            ? Physics.SphereCastAll(origin, sphereRadius, dir, castDistance, interactMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastAll(origin, dir, castDistance, interactMask, QueryTriggerInteraction.Collide);

        if (_Debug)
            Debug.DrawRay(origin, dir * castDistance, hits.Length > 0 ? Color.green : Color.red);

        if (hits == null || hits.Length == 0)
            return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;

            var hitCtx = h.collider.GetComponentInParent<CharacteContext>();
            if (hitCtx != null && hitCtx == ctx)
                continue;

            if (usingAimRay && !IsWithinOwnerReach(h.collider, interactionOrigin, ownerReach))
                continue;

            var best = FindBestInteractableForHit(h.collider, hitCtx);

         
            if (best != null)
            {
                focusCollider = h.collider;
                return best;
            }
        }

        return null;
    }

    bool TryResolveAimCast(
        Vector3 interactionOrigin,
        float ownerReach,
        out Vector3 origin,
        out Vector3 direction,
        out float distance)
    {
        origin = default;
        direction = default;
        distance = 0f;

        if (ctx is not PlayerContext player ||
            player.WeaponSystem == null ||
            !player.WeaponSystem.IsAiming ||
            player.thirdPersonAim == null ||
            !player.thirdPersonAim.isActiveAndEnabled)
        {
            return false;
        }

        ThirdPersonAimController aim = player.thirdPersonAim;
        direction = aim.CameraRayDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        origin = aim.CameraRayOrigin;
        direction.Normalize();
        distance = Vector3.Distance(origin, interactionOrigin) + ownerReach;

        if (aim.HasCameraHit)
        {
            float hitDistance = Vector3.Distance(origin, aim.AimPoint);
            distance = Mathf.Min(distance, hitDistance + sphereRadius);
        }

        return distance > 0f;
    }

    static bool IsWithinOwnerReach(Collider candidate, Vector3 origin, float reach)
    {
        Vector3 closestPoint = candidate.ClosestPoint(origin);
        return (closestPoint - origin).sqrMagnitude <= reach * reach;
    }

    IInteractable FindBestInteractableForHit(Collider hitCollider, CharacteContext hitCtx)
    {
        if (!hitCollider)
            return null;

        var directLink = hitCollider.GetComponentInParent<InteractableLink>(true);
        var best = directLink ? directLink.GetBest(this) : null;
        if (best != null)
            return best;

        if (hitCtx != null)
            return FindBestFromLinks(hitCtx.GetComponentsInChildren<InteractableLink>(true));

        var ownerBody = hitCollider.attachedRigidbody;
        if (ownerBody != null)
            return FindBestFromLinks(ownerBody.GetComponentsInChildren<InteractableLink>(true));

        return null;
    }

    IInteractable FindBestFromLinks(InteractableLink[] links)
    {
        if (links == null || links.Length == 0)
            return null;

        IInteractable best = null;
        int bestPrio = int.MinValue;

        for (int i = 0; i < links.Length; i++)
        {
            var link = links[i];
            if (!link) continue;

            var candidate = link.GetBest(this);
            if (candidate == null) continue;

            if (candidate.Priority > bestPrio)
            {
                bestPrio = candidate.Priority;
                best = candidate;
            }
        }

        return best;
    }

    public void InteractPressed()
    {
        Debug.Log("[InteractPressed] called", this);

        UpdateFocus();

        if (_focus == null)
        {
            Debug.Log("[InteractPressed] _focus is NULL", this);
            return;
        }

        bool can = _focus.CanInteract(this);
        Debug.Log($"[InteractPressed] focus={(_focus as Component)?.name}, can={can}", this);

        if (!can) return;

        if (_focus is IHoldInteractable hold)
        {
            _holding = true;

            // ล็อก target ที่จะ hold ไว้
            _lockedTarget = _focus;
            _lockedHoldTarget = hold;
            SetHoldProgress(0f);

            Debug.Log($"[InteractPressed] Begin hold on {(_lockedTarget as Component)?.name}", this);
            hold.BeginHold(this);

            if (_holdRoutine != null) StopCoroutine(_holdRoutine);
            _holdRoutine = StartCoroutine(HoldRoutine(_lockedTarget, _lockedHoldTarget));
        }
        else
        {
            Debug.Log($"[InteractPressed] Direct interact on {(_focus as Component)?.name}", this);
            _focus.Interact(this);
        }
    }

    public void InteractReleased()
    {
        if (_holding) CancelInteractInternal();
    }

    void CancelInteractInternal(bool stopRoutine = true)
    {
        _holding = false;

        if (_holdRoutine != null)
        {
            if (stopRoutine)
                StopCoroutine(_holdRoutine);
            _holdRoutine = null;
        }

        if (_lockedHoldTarget != null)
            _lockedHoldTarget.CancelHold(this);

        _lockedTarget = null;
        _lockedHoldTarget = null;
        SetHoldProgress(0f);

        // ปล่อยปุ่มแล้วค่อยอัปเดตใหม่
        UpdateFocus();
    }

    IEnumerator HoldRoutine(IInteractable target, IHoldInteractable hold)
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, hold.HoldDuration);

        while (t < dur)
        {
            if (!_holding) yield break;
            if (!IsInteractableValid(target) || !target.CanInteract(this))
            {
                CancelInteractInternal(stopRoutine: false);
                yield break;
            }

            t += Time.deltaTime;
            SetHoldProgress(t / dur);
            yield return null;
        }

        _holding = false;
        _holdRoutine = null;

        hold.CompleteHold(this);

        bool interactionCompleted = false;
        if (IsInteractableValid(target) && target.CanInteract(this))
        {
            target.Interact(this);
            interactionCompleted = true;
        }

        _lockedTarget = null;
        _lockedHoldTarget = null;
        SetHoldProgress(0f);

        if (interactionCompleted)
            HoldCompleted?.Invoke();

        UpdateFocus();
    }

    static bool IsInteractableValid(IInteractable target)
    {
        if (target == null)
            return false;

        return target is not UnityEngine.Object unityObject || unityObject != null;
    }

    void SetHoldProgress(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        if (Mathf.Approximately(HoldProgress, normalized))
            return;

        HoldProgress = normalized;
        HoldProgressChanged?.Invoke(HoldProgress);
    }
}
