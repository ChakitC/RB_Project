using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    [Header("Physics")]
    public float gravity = -9.81f;
    public float lifeTime = 5f;
    public float bounciness = 0.2f; // ใช้ PhysicMaterial ก็ได้

    AbilityContext _ctx;
    Vector3 _vel;
    float _t;

    public void Launch(AbilityContext ctx, float speed)
    {
        _ctx = ctx;
        _vel = ctx.CastDirection.normalized * speed;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;

        _vel += Vector3.up * gravity * dt;
        transform.position += _vel * dt;

        if (_t >= lifeTime) Explode();
    }

    void OnCollisionEnter(Collision _) => Explode();

    void Explode()
    {
        var t = _ctx.Ability.targeting;
        var hits = Physics.OverlapSphere(transform.position, t.explodeRadius, t.hitMask);

        // อัปเดตจุดระเบิดลงใน ctx เพื่อให้ DamageEffect รู้ตำแหน่ง (ไว้คำนวณระยะ)
        var explodeCtx = new AbilityContext(_ctx.Ability, _ctx.Caster, transform.position, _ctx.CastDirection, _ctx.Combat);

        // ข้ามตัวเอง (SpawnGrenadeEffect) แล้วรันเอฟเฟกต์ถัดๆ ไป
        bool passedSpawn = false;
        foreach (var e in _ctx.Ability.effects)
        {
            if (!passedSpawn)
            {
                if (e is SpawnGrenadeEffectDef) passedSpawn = true;
                continue;
            }
            e.Apply(explodeCtx, hits);
        }

        Destroy(gameObject);
    }
}