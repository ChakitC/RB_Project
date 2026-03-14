using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Planar Arc")]
public class PlanarArcModule : ProjectileModule
{
    [Tooltip("ความเร็วการเลี้ยว (องศา/วินาที)")]
    public float turnRateDegPerSec = 180f;

    [Tooltip("เลี้ยวกี่วินาทีแล้วกลับไปตรง")]
    public float curveDuration = 0.35f;

    public bool randomLeftRight = true;
    [Tooltip("ถ้าไม่สุ่ม: -1=ซ้าย, 1=ขวา")]
    public int fixedDirection = 1;

    class State : IProjectileModuleState
    {
        public float t;
        public int sign;
    }

    public override IProjectileModuleState CreateState() => new State();

    public override void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        s.t = 0f;

        int dir = fixedDirection >= 0 ? 1 : -1;
        if (randomLeftRight) dir = Random.value < 0.5f ? -1 : 1;

        s.sign = dir;
    }

    public override void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState st, float dt)
    {
        var s = (State)st;
        if (s.t >= curveDuration) return;

        p.RotateYaw(s.sign * turnRateDegPerSec * dt);
        s.t += dt;
    }
}