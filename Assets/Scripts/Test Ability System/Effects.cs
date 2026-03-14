using UnityEngine;

// 4.1 Burn DOT แบบคอมโพเนนต์เล็กๆ
public class BurnDotRuntime : MonoBehaviour
{
    public float tickDamage = 10f;
    public float tickInterval = 1f;
    public float timeLeft = 5f;

    float acc;
    void Update()
    {
        float dt = Time.deltaTime;
        timeLeft -= dt;
        acc += dt;

        if (acc >= tickInterval)
        {
            acc = 0f;
            if (TryGetComponent<IDamageable>(out var d) && d.IsAlive)
                d.TakeDamage(tickDamage);
        }
        if (timeLeft <= 0f) Destroy(this);
    }

    // เรียกซ้ำเพื่อรีเฟรช
    public void Refresh(float duration, float dmg, float interval)
    { timeLeft = duration; tickDamage = dmg; tickInterval = interval; }
}

[CreateAssetMenu(menuName="Ability/Effects/Apply Burn")]
public class ApplyBurnEffectDef : EffectDef
{
    public float duration = 5f;
    public float tickDamage = 10f;
    public float tickInterval = 1f;

    public override void Apply(AbilityContext ctx, Collider[] targets)
    {
        foreach (var col in targets)
        {
            if (!col) continue;
            var go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
            var burn = go.GetComponent<BurnDotRuntime>();
            if (!burn) burn = go.AddComponent<BurnDotRuntime>();
            burn.Refresh(duration, tickDamage, tickInterval);
        }
    }
}

// 4.2 Slow แบบคูณความเร็ว (ต้องมี MovementModifier ง่ายๆ ให้ระบบวิ่งอ่าน)
public class MovementModifier : MonoBehaviour
{
    public float SpeedMultiplier = 1f;
}

public class TimedSlowRuntime : MonoBehaviour
{
    public float multiplier = 0.6f;
    public float duration = 4f;
    float timeLeft; float original = 1f; MovementModifier mm;

    void OnEnable()
    {
        mm = GetComponent<MovementModifier>();
        if (mm){ original = mm.SpeedMultiplier; mm.SpeedMultiplier *= multiplier; }
        timeLeft = duration;
    }
    void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { if (mm) mm.SpeedMultiplier = original; Destroy(this); }
    }

    public void Refresh(float d) { timeLeft = d; } // ถ้าถูกใส่ซ้ำ ให้รีเฟรช
}

[CreateAssetMenu(menuName="Ability/Effects/Apply Slow")]
public class ApplySlowEffectDef : EffectDef
{
    [Range(0f,1f)] public float speedMultiplier = 0.6f;
    public float duration = 4f;

    public override void Apply(AbilityContext ctx, Collider[] targets)
    {
        foreach (var col in targets)
        {
            if (!col) continue;
            var go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
            var slow = go.GetComponent<TimedSlowRuntime>();
            if (!slow) slow = go.AddComponent<TimedSlowRuntime>();
            slow.multiplier = speedMultiplier;
            slow.duration = duration;
            slow.Refresh(duration);
        }
    }
}
