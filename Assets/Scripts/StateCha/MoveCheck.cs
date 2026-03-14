using UnityEngine;

public static class MoveCheck
{
    public static bool IsMoveIntent(CharacteContext ctx, float eps = 0.1f)
        => ctx != null && ctx.moveInput.sqrMagnitude > eps;

    public static bool IsMovingActual(CharacteContext ctx, float eps = 0.05f)
    {
        if (ctx == null || ctx.cc == null) return false;
        var v = ctx.cc.velocity; v.y = 0f;
        return v.sqrMagnitude > eps * eps;
    }
    public static bool IsMovingNav(AllyContext ctx, float eps = 0.05f)
    {
        if (ctx == null || ctx.agent == null) return false;

        // ความเร็วจริง (agent.velocity) หรือ desiredVelocity ก็ได้
        Vector3 v = ctx.agent.velocity;
        v.y = 0f;

        // กันเคสกำลังคำนวณ path / หยุดเพราะถึงปลายทาง
        if (ctx.agent.pathPending) return false;
        if (ctx.agent.remainingDistance <= ctx.agent.stoppingDistance && !ctx.agent.hasPath)
            return false;

        return v.sqrMagnitude > eps * eps;
    }
}