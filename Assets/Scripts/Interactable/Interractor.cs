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

    private IInteractable _focus;
    private IInteractable _lockedTarget;
    private IHoldInteractable _lockedHoldTarget;
    private Coroutine _holdRoutine;
    private bool _holding;

    void Awake()
    {
        if (!ctx) ctx = GetComponentInParent<CharacteContext>();
    }

    void Update()
    {
        // สำคัญ: ระหว่าง hold อย่าอัปเดต focus ทับ
        if (_holding) return;

        UpdateFocus();
    }

    void UpdateFocus()
    {
        IInteractable newFocus = FindBestFocus();

        if (!ReferenceEquals(newFocus, _focus))
        {
            if (_focus is IFocusable oldF) oldF.OnFocusExit(this);
            _focus = newFocus;
            if (_focus is IFocusable newF) newF.OnFocusEnter(this);
        }

        currentPrompt = (_focus != null) ? _focus.GetPrompt(this) : "";
    }

    IInteractable FindBestFocus()
    {
        Vector3 origin = transform.position + Vector3.up * originHeight;

        float r = (ctx != null && ctx.cc) ? ctx.cc.radius : 0.3f;
        origin += transform.forward * (r + selfOffset);

        Vector3 dir = transform.forward;

        RaycastHit[] hits = useSphereCast
            ? Physics.SphereCastAll(origin, sphereRadius, dir, maxDistance, interactMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastAll(origin, dir, maxDistance, interactMask, QueryTriggerInteraction.Collide);

        if (_Debug)
            Debug.DrawRay(origin, dir * maxDistance, hits.Length > 0 ? Color.green : Color.red);

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

            var link = h.collider.GetComponentInParent<InteractableLink>(true);
            if (!link) continue;

            var best = link.GetBest(this);

         
            if (best != null)
                return best;
        }

        return null;
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

    void CancelInteractInternal()
    {
        _holding = false;

        if (_holdRoutine != null)
        {
            StopCoroutine(_holdRoutine);
            _holdRoutine = null;
        }

        if (_lockedHoldTarget != null)
            _lockedHoldTarget.CancelHold(this);

        _lockedTarget = null;
        _lockedHoldTarget = null;

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
            if (target == null) yield break;
            if (!target.CanInteract(this)) yield break;

            t += Time.deltaTime;
            yield return null;
        }

        _holding = false;
        _holdRoutine = null;

        hold.CompleteHold(this);

        if (target != null && target.CanInteract(this))
            target.Interact(this);

        _lockedTarget = null;
        _lockedHoldTarget = null;

        UpdateFocus();
    }
}