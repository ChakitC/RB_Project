using UnityEngine;

public enum PickupCollectorRule
{
    Anyone,
    PlayerOnly,
    OwnerOnly,
    NonOwnerOnly,
    PlayerExceptOwner,
    PlayerOrCompanionOnly,
    CompanionOnly,
}

[RequireComponent(typeof(Collider))]
public class SkillPickup : MonoBehaviour
{
    [SerializeField] private PickupCollectorRule collectorRule = PickupCollectorRule.PlayerOnly;
    [SerializeField] private PickupEffectDef[] effects;
    [SerializeField, Min(0f)] private float pickupDelaySeconds = 0.15f;
    [SerializeField, Min(0f)] private float lifetimeSeconds = 20f;

    PickupContext _context;
    bool _initialized;
    float _spawnTime;
    Collider _pickupCollider;
    PickupVisualMotion _pickupVisualMotion;
    bool _collecting;

    void Reset()
    {
        TryGetComponent(out _pickupVisualMotion);
    }

    void Awake()
    {
        _pickupCollider = GetComponent<Collider>();
        if (_pickupCollider != null && !_pickupCollider.isTrigger)
            _pickupCollider.isTrigger = true;

        if (_pickupVisualMotion == null)
            TryGetComponent(out _pickupVisualMotion);

        _spawnTime = Time.time;
        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }

    public void Initialize(PickupContext context)
    {
        _context = context;
        _initialized = true;
        _spawnTime = Time.time;
    }

    public void SetCollectorRule(PickupCollectorRule rule)
    {
        collectorRule = rule;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collecting)
            return;

        if (Time.time < _spawnTime + pickupDelaySeconds)
            return;

        if (effects == null || effects.Length == 0)
            return;

        var targetObject = ResolveTargetObject(other);
        if (targetObject == null || !CanCollectorUsePickup(targetObject))
            return;

        bool appliedAnyEffect = false;
        for (int i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            if (effect == null || !effect.CanApply(targetObject, _context))
                continue;

            if (effect.Apply(targetObject, _context))
                appliedAnyEffect = true;
        }

        if (appliedAnyEffect)
            CompletePickup(targetObject.transform);
    }

    GameObject ResolveTargetObject(Collider other)
    {
        if (other == null)
            return null;

        Transform hitTransform = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        Transform collectorTransform = ResolveCollectorTransform(hitTransform);
        return collectorTransform != null ? collectorTransform.gameObject : other.gameObject;
    }

    bool CanCollectorUsePickup(GameObject targetObject)
    {
        bool isOwner = IsOwnerCollector(targetObject);

        switch (collectorRule)
        {
            case PickupCollectorRule.PlayerOnly:
                return targetObject != null && targetObject.CompareTag("Player");

            case PickupCollectorRule.OwnerOnly:
                return isOwner;

            case PickupCollectorRule.NonOwnerOnly:
                return !isOwner;

            case PickupCollectorRule.PlayerExceptOwner:
                return targetObject != null && targetObject.CompareTag("Player") && !isOwner;

            case PickupCollectorRule.PlayerOrCompanionOnly:
                return IsPlayerOrCompanionCollector(targetObject);

            case PickupCollectorRule.CompanionOnly:
                return IsCompanionCollector(targetObject);

            case PickupCollectorRule.Anyone:
            default:
                return true;
        }
    }

    bool IsPlayerOrCompanionCollector(GameObject targetObject)
    {
        CharacteContext collectorContext = ResolveCollectorContext(targetObject);
        if (collectorContext != null)
        {
            return collectorContext.TargetIdentity == AITargetIdentity.Player ||
                   collectorContext.TargetIdentity == AITargetIdentity.Companion;
        }

        return targetObject != null && targetObject.CompareTag("Player");
    }

    bool IsCompanionCollector(GameObject targetObject)
    {
        CharacteContext collectorContext = ResolveCollectorContext(targetObject);
        return collectorContext != null && collectorContext.TargetIdentity == AITargetIdentity.Companion;
    }

    CharacteContext ResolveCollectorContext(GameObject targetObject)
    {
        if (targetObject == null)
            return null;

        CharacteContext context = targetObject.GetComponent<CharacteContext>();
        if (context != null)
            return context;

        context = targetObject.GetComponentInParent<CharacteContext>();
        if (context != null)
            return context;

        return targetObject.GetComponentInChildren<CharacteContext>(true);
    }

    Transform ResolveCollectorTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        for (Transform current = hitTransform; current != null; current = current.parent)
        {
            if (current.CompareTag("Player"))
                return current;

            if (current.GetComponent<StatusEffectController>() != null)
                return current;
        }

        return hitTransform.root != null ? hitTransform.root : hitTransform;
    }

    bool IsOwnerCollector(GameObject targetObject)
    {
        if (!_initialized || targetObject == null)
            return false;

        if (_context.SourceObject != null)
            return targetObject == _context.SourceObject;

        return _context.SourceRoot != null && targetObject.transform.root == _context.SourceRoot;
    }

    void CompletePickup(Transform collector)
    {
        _collecting = true;
        DisablePickupColliders();

        if (_pickupVisualMotion == null)
            TryGetComponent(out _pickupVisualMotion);

        if (_pickupVisualMotion == null)
        {
            Destroy(gameObject);
            return;
        }

        _pickupVisualMotion.enabled = true;
        _pickupVisualMotion.PlayCollectTo(collector, DestroySelf);
    }

    void DisablePickupColliders()
    {
        foreach (var pickupCollider in GetComponentsInChildren<Collider>())
            pickupCollider.enabled = false;
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }
}
