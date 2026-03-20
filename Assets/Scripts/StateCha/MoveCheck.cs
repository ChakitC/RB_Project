using UnityEngine;
using UnityEngine.AI;

public static class MoveCheck
{
    public static bool IsMoveIntent(CharacteContext ctx, float eps = 0.1f)
        => ctx != null && ctx.moveInput.sqrMagnitude > eps;

    public static bool IsMovingActual(CharacteContext ctx, float eps = 0.05f)
    {
        if (ctx == null || ctx.cc == null) return false;

        var v = ctx.cc.velocity;
        v.y = 0f;
        return v.sqrMagnitude > eps * eps;
    }

    public static bool IsMovingNav(NavMeshAgent agent, float eps = 0.05f)
    {
        if (agent == null) return false;

        Vector3 v = agent.velocity;
        v.y = 0f;

        if (agent.pathPending) return false;
        if (agent.remainingDistance <= agent.stoppingDistance && !agent.hasPath)
            return false;

        return v.sqrMagnitude > eps * eps;
    }
}
