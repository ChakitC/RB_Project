using UnityEngine;
using UnityEngine.AI;

public class AgentMoveDriver : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] StateHub stateHub;
    [SerializeField] CharacteContext ctx;

    [Header("Ramp")]
    [SerializeField] float rampUpTime = 0.12f;
    [SerializeField] float rampDownTime = 0.18f;

    [Header("IsMoving (stable)")]
    [SerializeField] float move01Start = 0.10f;
    [SerializeField] float move01Stop = 0.05f;
    [SerializeField] float enterDelay = 0.06f;
    [SerializeField] float exitDelay = 0.10f;

    float _move01;
    float _aboveT;
    float _belowT;

    public bool agentismoving;

    void Awake()
    {
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
            if (!agent)
                agent = GetComponentInParent<NavMeshAgent>();
        }

        if (!stateHub)
        {
            stateHub = GetComponent<StateHub>();
            if (!stateHub)
                stateHub = GetComponentInParent<StateHub>();
        }

        if (!ctx)
        {
            ctx = GetComponent<CharacteContext>();
            if (!ctx)
                ctx = GetComponentInParent<CharacteContext>();
        }
    }

    void Update()
    {
        UpdateAIMoveAnimFromNavMesh(agent);
    }

    void UpdateAIMoveAnimFromNavMesh(NavMeshAgent agent)
    {
        if (!agent || !agent.enabled || !agent.isOnNavMesh)
        {
            agentismoving = false;
            _move01 = 0f;
            stateHub?.SetMoveSpeed01(0f);
            return;
        }

        float dt = UsesWorldSlow() ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;

        if (ctx != null)
        {
            float targetSpeed = ctx.GetMoveSpeedForCurrentLifeState();
            if (UsesWorldSlow())
                targetSpeed *= TimeSlowManager.Instance.WorldTimeScale;

            if (!Mathf.Approximately(agent.speed, targetSpeed))
                agent.speed = targetSpeed;
        }

        Vector3 vel = agent.velocity;
        vel.y = 0f;

        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;

        bool arrived = !agent.pathPending &&
                       agent.remainingDistance <= agent.stoppingDistance + 0.02f &&
                       (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);

        Vector3 moveWorldDir;
        float raw01;

        if (arrived)
        {
            moveWorldDir = Vector3.zero;
            raw01 = 0f;
        }
        else
        {
            Vector3 dirSrc = vel.sqrMagnitude > 0.0001f ? vel : desired;
            moveWorldDir = dirSrc.sqrMagnitude > 0.0001f ? dirSrc.normalized : Vector3.zero;

            float speedNow = vel.sqrMagnitude > 0.0001f ? vel.magnitude : desired.magnitude;
            float denom = Mathf.Max(agent.speed, 0.001f);
            raw01 = Mathf.Clamp01(speedNow / denom);
        }

        float upSpeed = rampUpTime <= 0.0001f ? 999f : 1f / rampUpTime;
        float downSpeed = rampDownTime <= 0.0001f ? 999f : 1f / rampDownTime;

        float target01 = raw01;
        float s = target01 > _move01 ? upSpeed : downSpeed;
        _move01 = Mathf.MoveTowards(_move01, target01, s * dt);

        stateHub?.SetMoveSpeed01(_move01);

        bool wantMove = !arrived && _move01 > move01Start && agent.hasPath && !agent.isStopped && !agent.pathPending;
        bool wantStop = arrived || _move01 < move01Stop || agent.isStopped || (!agent.hasPath && !agent.pathPending);

        if (!agentismoving)
        {
            _aboveT = wantMove ? _aboveT + dt : 0f;
            if (_aboveT >= enterDelay)
            {
                agentismoving = true;
                _aboveT = 0f;
            }
        }
        else
        {
            _belowT = wantStop ? _belowT + dt : 0f;
            if (_belowT >= exitDelay)
            {
                agentismoving = false;
                _belowT = 0f;
            }
        }

        if (ctx != null && ctx.AnimBrain != null)
        {
            if (moveWorldDir.sqrMagnitude < 0.0001f)
            {
                ctx.AnimBrain.MoveDirLocal = Vector2.zero;
            }
            else
            {
                Vector3 moveLocal3 = transform.InverseTransformDirection(moveWorldDir);
                Vector2 dirLocal = new Vector2(moveLocal3.x, moveLocal3.z);
                if (dirLocal.sqrMagnitude > 1.0001f)
                    dirLocal.Normalize();

                ctx.AnimBrain.MoveDirLocal = dirLocal;
            }
        }
    }

    bool UsesWorldSlow()
    {
        return !(ctx is PlayerContext);
    }
}
