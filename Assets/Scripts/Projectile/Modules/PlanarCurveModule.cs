using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Planar Curve")]
public class PlanarCurveModule : ProjectileModule
{
    [Header("Curve")]
    [Tooltip("องศาที่จะโค้งรวมทั้งหมด (เช่น 35 = โค้งไปขวา/ซ้าย 35 องศา)")]
    public float totalYawDegrees = 35f;

    [Tooltip("ใช้เวลาทำโค้งกี่วินาที หลังจากนั้นจะบินตรงต่อ")]
    public float curveDuration = 0.35f;

    [Tooltip("รูปแบบการโค้ง (0..1)")]
    public AnimationCurve curveShape = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Side")]
    public bool randomLeftRight = true;
    public bool forceLeft = false; // ถ้า random=false จะเลือกซ้าย/ขวาจากอันนี้

    class State : IProjectileModuleState
    {
        public float t;
        public float lastAngle;
        public float sign;
    }

    public override IProjectileModuleState CreateState() => new State();

    public override void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        s.t = 0f;
        s.lastAngle = 0f;

        if (randomLeftRight)
            s.sign = (Random.value < 0.5f) ? -1f : 1f;
        else
            s.sign = forceLeft ? -1f : 1f;
    }

    public override void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState st, float dt)
    {
        var s = (State)st;
        if (curveDuration <= 0f) return;
        if (s.t >= curveDuration) return;

        s.t += dt;
        float u = Mathf.Clamp01(s.t / curveDuration);

        float desired = s.sign * totalYawDegrees * curveShape.Evaluate(u);
        float delta = desired - s.lastAngle;
        s.lastAngle = desired;

        // หมุนทิศบนแกน Y (topdown = โค้งบนพื้น)
        p.RotateYaw(delta);
    }
}