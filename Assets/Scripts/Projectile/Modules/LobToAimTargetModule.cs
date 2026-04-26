using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Projectile Modules/Lob To Aim Target")]
public class LobToAimTargetModule : ProjectileModule
{
    [Header("Aim Target")]
    public string aimTargetName = "Aim Target";

    [Tooltip("Lock the aim target position when the projectile spawns.")]
    public bool lockTargetOnSpawn = true;

    [Header("Flight Time")]
    [Tooltip("0 = calculate from distance and speed")]
    public float flightTime = 0f;

    [Header("Arc Shape")]
    [Tooltip("Arc height on the Y axis")]
    public float maxHeight = 2.5f;

    [Tooltip("Sideways arc on XZ plane. 0 = no sideways curve")]
    public float sideOffset = 0f;

    [Tooltip("Randomize left/right when sideOffset > 0")]
    public bool randomLeftRight = true;

    [Tooltip("If randomLeftRight is false, choose left (true) or right (false)")]
    public bool forceLeft = false;

    [Header("On Arrive")]
    [Tooltip("Request expire on arrival so explode-on-expire modules can react")]
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

        s.aimTf = FindAimTarget(ctx);

        s.start = p.transform.position;

        if (s.aimTf == null)
            return;

        s.target = s.aimTf.position;

        if (randomLeftRight)
            s.sign = Random.value < 0.5f ? -1f : 1f;
        else
            s.sign = forceLeft ? -1f : 1f;

        float dist = Vector3.Distance(new Vector3(s.start.x, 0f, s.start.z), new Vector3(s.target.x, 0f, s.target.z));
        float speed = Mathf.Max(0.01f, ctx.stats.speed > 0f ? ctx.stats.speed : (p.config ? p.config.baseSpeed : 10f));
        s.duration = flightTime > 0f ? flightTime : dist / speed;

        Vector3 mid = (s.start + s.target) * 0.5f;
        Vector3 fwd = s.target - s.start;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : p.transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

        s.control = mid
                    + side * (sideOffset * s.sign)
                    + Vector3.up * maxHeight;

        s.inited = true;

        Vector3 dir = s.target - s.start;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            p.SetDirection(dir.normalized);
    }

    public override void Tick(Projectile p, ProjectileContext ctx, IProjectileModuleState st, float dt)
    {
        var s = (State)st;
        if (!s.inited)
            return;

        if (!lockTargetOnSpawn && s.aimTf != null)
        {
            s.target = s.aimTf.position;
            RebuildControl(p, ctx, s);
        }

        if (s.duration <= 0.0001f)
            return;

        s.t += dt;
        float u = Mathf.Clamp01(s.t / s.duration);

        Vector3 a = s.start;
        Vector3 b = s.control;
        Vector3 c = s.target;

        float one = 1f - u;

        Vector3 pos = (one * one) * a + (2f * one * u) * b + (u * u) * c;
        Vector3 tan = 2f * one * (b - a) + 2f * u * (c - b);

        p.OverridePosition(pos);

        Vector3 planar = tan;
        planar.y = 0f;
        if (planar.sqrMagnitude > 0.0001f)
            p.SetDirection(planar.normalized);

        if (u >= 1f && requestDespawnOnArrive)
            p.RequestExpire();
    }

    void RebuildControl(Projectile projectile, ProjectileContext ctx, State s)
    {
        float dist = Vector3.Distance(new Vector3(s.start.x, 0f, s.start.z), new Vector3(s.target.x, 0f, s.target.z));
        float speed = Mathf.Max(0.01f, ctx.stats.speed > 0f ? ctx.stats.speed : (projectile.config ? projectile.config.baseSpeed : 10f));
        s.duration = flightTime > 0f ? flightTime : dist / speed;

        Vector3 mid = (s.start + s.target) * 0.5f;

        Vector3 fwd = s.target - s.start;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : projectile.transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

        mid.y = Mathf.Max(s.start.y, s.target.y) + maxHeight;
        s.control = mid + side * (sideOffset * s.sign);
    }

    Transform FindAimTarget(ProjectileContext ctx)
    {
        if (ctx.aimTarget != null)
            return ctx.aimTarget;

        Transform aimTarget = FindAimTargetInHierarchy(ctx.sourceActor);
        if (aimTarget != null)
            return aimTarget;

        aimTarget = FindAimTargetInHierarchy(ctx.collisionIgnoreRoot);
        if (aimTarget != null)
            return aimTarget;

        var go = GameObject.Find(aimTargetName);
        return go ? go.transform : null;
    }

    Transform FindAimTargetInHierarchy(Transform root)
    {
        if (root == null)
            return null;

        Transform aimTarget = root.Find(aimTargetName);
        if (aimTarget != null)
            return aimTarget;

        var allChildren = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (allChildren[i] != null && allChildren[i].name == aimTargetName)
                return allChildren[i];
        }

        return null;
    }
}
