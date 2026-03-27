using UnityEngine;

public enum PickupCollectorRule
{
    Anyone,
    PlayerOnly,
    OwnerOnly,
    NonOwnerOnly,
    PlayerExceptOwner,
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

    void Awake()
    {
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

    void OnTriggerEnter(Collider other)
    {
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
            Destroy(gameObject);
    }

    GameObject ResolveTargetObject(Collider other)
    {
        if (other == null)
            return null;

        Transform root = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform.root
            : other.transform.root;

        return root != null ? root.gameObject : other.gameObject;
    }

    bool CanCollectorUsePickup(GameObject targetObject)
    {
        bool isOwner = _initialized &&
                       _context.SourceRoot != null &&
                       targetObject != null &&
                       targetObject.transform.root == _context.SourceRoot;

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

            case PickupCollectorRule.Anyone:
            default:
                return true;
        }
    }
}
