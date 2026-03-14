using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Lob To Aim Target")]
public class LobToAimTargetModule : ProjectileModule
{
    [Header("Aim Target")]
    public string aimTargetName = "Aim Target";

    [Tooltip("ล็อกตำแหน่งเป้าตั้งแต่ตอนยิง (แนะนำ: true)")]
    public bool lockTargetOnSpawn = true;

    [Header("Flight Time")]
    [Tooltip("0 = คำนวณจากระยะ/สปีด")]
    public float flightTime = 0f;

    [Header("Arc Shape")]
    [Tooltip("ความสูงของวิถี (แกน Y)")]
    public float maxHeight = 2.5f;

    [Tooltip("ความโค้งด้านข้างบนพื้น (XZ). 0 = ไม่โค้งด้านข้าง")]
    public float sideOffset = 0f;

    [Tooltip("สุ่มซ้าย/ขวา เมื่อ sideOffset > 0")]
    public bool randomLeftRight = true;

    [Tooltip("ถ้า randomLeftRight=false จะเลือกซ้าย (true) หรือขวา (false)")]
    public bool forceLeft = false;

    [Header("On Arrive")]
    [Tooltip("ถึงเป้าแล้วขอ despawn (เพื่อให้ GrenadeExplodeModule ระเบิดใน OnExpire)")]
    public bool requestDespawnOnArrive = true;

    class State : IProjectileModuleState
    {
        public bool inited;
        public float t;
        public float duration;

        public Vector3 start;
        public Vector3 target;
        public Vector3 control;
        public float sign;

        public Transform aimTf;
    }

    public override IProjectileModuleState CreateState() => new State();

    public override void OnSpawn(Projectile p, ProjectileContext ctx, IProjectileModuleState st)
    {
        var s = (State)st;
        s.inited = false;
        s.t = 0f;

        // หา Aim Target จาก owner root
        if (ctx.owner != null)
            s.aimTf = ctx.owner.root.Find(aimTargetName);
        if (s.aimTf == null)
        {
            // fallback: หาในฉาก (ถ้ามีอันเดียว)
            var go = GameObject.Find(aimTargetName);
            s.aimTf = go ? go.transform : null;
        }

        s.start = p.transform.position;

        // ถ้าไม่เจอ aim target -> ไม่ทำอะไร (ปล่อยเป็นเส้นตรง)
        if (s.aimTf == null) return;

        s.target = s.aimTf.position;

        // ให้ลงบน “ระดับเดียวกับจุดยิง” (กันไปชนพื้นแปลกๆ)
        // s.target.y = s.start.y;

        // เลือกทิศโค้งด้านข้าง
        if (randomLeftRight) s.sign = (Random.value < 0.5f) ? -1f : 1f;
        else s.sign = forceLeft ? -1f : 1f;

        // ตั้งเวลาเดินทาง
        float dist = Vector3.Distance(new Vector3(s.start.x, 0, s.start.z), new Vector3(s.target.x, 0, s.target.z));
        float speed = Mathf.Max(0.01f, ctx.stats.speed > 0f ? ctx.stats.speed : (p.config ? p.config.baseSpeed : 10f));
        s.duration = (flightTime > 0f) ? flightTime : (dist / speed);

        // จุดควบคุม (quadratic bezier)
        Vector3 mid = (s.start + s.target) * 0.5f;
        Vector3 fwd = (s.target - s.start);
        fwd.y = 0f;
        fwd = (fwd.sqrMagnitude > 0.0001f) ? fwd.normalized : p.transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

        s.control = mid
                    + side * (sideOffset * s.sign)
                    + Vector3.up * maxHeight;

        s.inited = true;

        // ตั้งทิศเริ่มต้นไปทางเป้า
        Vector3 dir = (s.target - s.start);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) p.SetDirection(dir.normalized);
    }

    public override void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState st, float dt)
    {
        var s = (State)st;
        if (!s.inited) return;

        // ถ้าไม่ล็อกเป้า ให้ตาม AimTarget และ “ไม่บังคับ y”
        if (!lockTargetOnSpawn && s.aimTf != null)
        {
            s.target = s.aimTf.position;          // ✅ ใช้ y ของ aim จริง
            RebuildControl(p, ctx, s);            // ✅ recompute control ถ้าเป้าขยับ
        }

        if (s.duration <= 0.0001f) return;

        s.t += dt;
        float u = Mathf.Clamp01(s.t / s.duration);

        Vector3 A = s.start;
        Vector3 B = s.control;
        Vector3 C = s.target;

        float one = 1f - u;

        // position on quadratic bezier
        Vector3 pos = (one * one) * A + (2f * one * u) * B + (u * u) * C;

        // tangent (derivative)
        Vector3 tan = 2f * one * (B - A) + 2f * u * (C - B);

        //  บังคับตำแหน่ง (นิ่งกว่า velocity มาก)
        p.OverridePosition(pos);

        //  ตั้งทิศบนพื้นจาก tangent (ไม่ flip)
        Vector3 planar = tan; planar.y = 0f;
        if (planar.sqrMagnitude > 0.0001f)
            p.SetDirection(planar.normalized);

        if (u >= 1f && requestDespawnOnArrive)
            p.RequestDespawn();
    }
    
    void RebuildControl(Projectile Projectile, ProjectileContext ctx, State s)
    {
        // duration
        float dist = Vector3.Distance(new Vector3(s.start.x, 0, s.start.z), new Vector3(s.target.x, 0, s.target.z));
        float speed = Mathf.Max(0.01f, ctx.stats.speed > 0f ? ctx.stats.speed : (Projectile.config ? Projectile.config.baseSpeed : 10f));
        s.duration = (flightTime > 0f) ? flightTime : (dist / speed);

        // control (ยกสูงแบบถูกต้อง)
        Vector3 mid = (s.start + s.target) * 0.5f;

        Vector3 fwd = (s.target - s.start);
        fwd.y = 0f;
        fwd = (fwd.sqrMagnitude > 0.0001f) ? fwd.normalized : Projectile.transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

        mid.y = Mathf.Max(s.start.y, s.target.y) + maxHeight; // ยกสูงเหนือ start/target
        s.control = mid + side * (sideOffset * s.sign);
    }
}
