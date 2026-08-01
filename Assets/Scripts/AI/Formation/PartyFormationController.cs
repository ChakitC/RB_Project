using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-107)]
public sealed class PartyFormationController : MonoBehaviour
{
    public enum FormationMode
    {
        Triangle = 0,
        SingleFile = 1,
    }

    const int FirstCompanionPartyIndex = 1;
    const int LastCompanionPartyIndex = 3;

    [SerializeField] private PlayerContext playerContext;

    [Header("Triangle Slots")]
    [SerializeField] private Vector3 leftSlot = new(-1.5f, 0f, -1.8f);
    [SerializeField] private Vector3 rightSlot = new(1.5f, 0f, -1.8f);
    [SerializeField] private Vector3 rearCenterSlot = new(0f, 0f, -3.2f);

    [Header("Single File Slots")]
    [SerializeField] private Vector3 firstLineSlot = new(0f, 0f, -1.8f);
    [SerializeField] private Vector3 secondLineSlot = new(0f, 0f, -3.2f);
    [SerializeField] private Vector3 thirdLineSlot = new(0f, 0f, -4.6f);

    [Header("Movement")]
    [SerializeField, Min(0f)] private float startMoveDistance = 1.25f;
    [SerializeField, Min(0f)] private float stopDistance = 0.6f;
    [SerializeField, Min(0.02f)] private float destinationUpdateInterval = 0.15f;
    [SerializeField, Min(0f)] private float destinationChangeThreshold = 0.25f;
    [SerializeField, Min(1f)] private float headingTurnSpeed = 180f;
    [SerializeField, Min(0f)] private float headingMoveThreshold = 0.05f;

    [Header("NavMesh")]
    [SerializeField, Min(0.05f)] private float slotSampleRadius = 1f;
    [SerializeField, Min(0.05f)] private float teleportSampleRadius = 1.5f;
    [SerializeField, Min(0.02f)] private float geometryCheckInterval = 0.1f;

    [Header("Mode Switching")]
    [SerializeField, Min(0f)] private float collapseDelay = 0.5f;
    [SerializeField, Min(0f)] private float expandDelay = 1.5f;

    [Header("Catch Up")]
    [SerializeField, Min(0f)] private float teleportDistance = 15f;
    [SerializeField, Min(0f)] private float invalidPathTimeout = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logFormationTransitions;

    readonly AllyContext[] _membersByPartyIndex = new AllyContext[LastCompanionPartyIndex + 1];

    FormationMode _mode;
    Vector3 _formationForward = Vector3.forward;
    Vector3 _targetForward = Vector3.forward;
    Vector3 _previousPlayerPosition;
    float _nextGeometryCheckTime;
    float _lastGeometryCheckTime;
    float _triangleBlockedDuration;
    float _triangleClearDuration;

    public FormationMode CurrentMode => _mode;
    public Vector3 FormationForward => _formationForward;
    public float StartMoveDistance => startMoveDistance;
    public float StopDistance => stopDistance;
    public float DestinationUpdateInterval => destinationUpdateInterval;
    public float DestinationChangeThreshold => destinationChangeThreshold;
    public float HeadingTurnSpeed => headingTurnSpeed;
    public float TeleportDistance => teleportDistance;
    public float InvalidPathTimeout => invalidPathTimeout;
    public int RegisteredMemberCount { get; private set; }

    void Awake()
    {
        ResolveReferences();
        InitializeHeading();
    }

    void OnEnable()
    {
        ResolveReferences();
        InitializeHeading();
    }

    void Update()
    {
        if (playerContext == null)
            return;

        TickHeading();

        float now = Time.time;
        if (now < _nextGeometryCheckTime)
            return;

        float elapsed = _lastGeometryCheckTime > 0f
            ? Mathf.Max(0f, now - _lastGeometryCheckTime)
            : geometryCheckInterval;
        _lastGeometryCheckTime = now;
        _nextGeometryCheckTime = now + geometryCheckInterval;
        TickFormationMode(elapsed);
    }

    public void ConfigureRuntimeActors(IReadOnlyList<PartyRuntimeActor> actors)
    {
        Array.Clear(_membersByPartyIndex, 0, _membersByPartyIndex.Length);
        RegisteredMemberCount = 0;

        if (actors == null)
            throw new ArgumentNullException(nameof(actors));

        for (int i = 0; i < actors.Count; i++)
        {
            PartyRuntimeActor actor = actors[i];
            if (actor == null || !IsCompanionPartyIndex(actor.PartyIndex))
                continue;

            if (actor.Context is not AllyContext allyContext)
            {
                throw new InvalidOperationException(
                    $"Party index {actor.PartyIndex} must use AllyContext for formation binding.");
            }

            if (_membersByPartyIndex[actor.PartyIndex] != null)
            {
                throw new InvalidOperationException(
                    $"Party index {actor.PartyIndex} is assigned to more than one formation member.");
            }

            allyContext.ResolveReferences();
            _membersByPartyIndex[actor.PartyIndex] = allyContext;
            RegisteredMemberCount++;
        }
    }

    public bool TryGetRegisteredMember(int partyIndex, out AllyContext member)
    {
        if (IsCompanionPartyIndex(partyIndex))
        {
            member = _membersByPartyIndex[partyIndex];
            return member != null;
        }

        member = null;
        return false;
    }

    public bool TryGetSlotDestination(int partyIndex, NavMeshAgent agent, out Vector3 destination)
    {
        destination = Vector3.zero;
        if (!IsCompanionPartyIndex(partyIndex) || playerContext == null)
            return false;

        Vector3 localOffset = GetLocalSlotOffset(partyIndex, _mode);
        return TryResolveSlot(
            localOffset,
            agent,
            allowFallback: true,
            requireDirectLine: false,
            slotSampleRadius,
            out destination);
    }

    public bool TryGetTeleportDestination(int partyIndex, NavMeshAgent agent, out Vector3 destination)
    {
        destination = Vector3.zero;
        if (!IsCompanionPartyIndex(partyIndex) || playerContext == null)
            return false;

        Vector3 localOffset = GetLocalSlotOffset(partyIndex, _mode);
        return TryResolveSlot(
            localOffset,
            agent,
            allowFallback: true,
            requireDirectLine: false,
            teleportSampleRadius,
            out destination);
    }

    public Vector3 GetLocalSlotOffset(int partyIndex, FormationMode mode)
    {
        return mode switch
        {
            FormationMode.SingleFile when partyIndex == 1 => firstLineSlot,
            FormationMode.SingleFile when partyIndex == 2 => secondLineSlot,
            FormationMode.SingleFile when partyIndex == 3 => thirdLineSlot,
            FormationMode.Triangle when partyIndex == 1 => leftSlot,
            FormationMode.Triangle when partyIndex == 2 => rightSlot,
            FormationMode.Triangle when partyIndex == 3 => rearCenterSlot,
            _ => Vector3.zero,
        };
    }

    public static bool IsCompanionPartyIndex(int partyIndex)
    {
        return partyIndex >= FirstCompanionPartyIndex && partyIndex <= LastCompanionPartyIndex;
    }

    void ResolveReferences()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext != null && playerContext.partyFormation != this)
            playerContext.partyFormation = this;
    }

    void InitializeHeading()
    {
        if (playerContext == null)
            return;

        _previousPlayerPosition = playerContext.transform.position;
        Vector3 initialForward = Flatten(playerContext.transform.forward);
        if (initialForward.sqrMagnitude <= 0.0001f)
            initialForward = Vector3.forward;

        _formationForward = initialForward.normalized;
        _targetForward = _formationForward;
        _lastGeometryCheckTime = Time.time;
        _nextGeometryCheckTime = Time.time;
    }

    void TickHeading()
    {
        Vector3 playerPosition = playerContext.transform.position;
        Vector3 displacement = Flatten(playerPosition - _previousPlayerPosition);

        float thresholdSqr = headingMoveThreshold * headingMoveThreshold;
        if (displacement.sqrMagnitude >= thresholdSqr)
        {
            _targetForward = displacement.normalized;
            _previousPlayerPosition = playerPosition;
        }

        if (_targetForward.sqrMagnitude <= 0.0001f)
            return;

        Quaternion currentRotation = Quaternion.LookRotation(_formationForward, Vector3.up);
        Quaternion targetRotation = Quaternion.LookRotation(_targetForward, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            currentRotation,
            targetRotation,
            headingTurnSpeed * Time.deltaTime);
        _formationForward = Flatten(nextRotation * Vector3.forward).normalized;
    }

    void TickFormationMode(float elapsed)
    {
        bool triangleAvailable = IsTriangleAvailable();
        if (triangleAvailable)
        {
            _triangleBlockedDuration = 0f;
            if (_mode != FormationMode.SingleFile)
                return;

            _triangleClearDuration += elapsed;
            if (_triangleClearDuration >= expandDelay)
                SetMode(FormationMode.Triangle);
            return;
        }

        _triangleClearDuration = 0f;
        if (_mode != FormationMode.Triangle)
            return;

        _triangleBlockedDuration += elapsed;
        if (_triangleBlockedDuration >= collapseDelay)
            SetMode(FormationMode.SingleFile);
    }

    bool IsTriangleAvailable()
    {
        for (int partyIndex = FirstCompanionPartyIndex; partyIndex <= LastCompanionPartyIndex; partyIndex++)
        {
            NavMeshAgent agent = _membersByPartyIndex[partyIndex] != null
                ? _membersByPartyIndex[partyIndex].agent
                : null;
            Vector3 localOffset = GetLocalSlotOffset(partyIndex, FormationMode.Triangle);
            if (!TryResolveSlot(
                    localOffset,
                    agent,
                    allowFallback: false,
                    requireDirectLine: true,
                    slotSampleRadius,
                    out _))
                return false;
        }

        return true;
    }

    void SetMode(FormationMode nextMode)
    {
        if (_mode == nextMode)
            return;

        FormationMode previousMode = _mode;
        _mode = nextMode;
        _triangleBlockedDuration = 0f;
        _triangleClearDuration = 0f;

        if (logFormationTransitions)
            Debug.Log($"[PartyFormation] {previousMode} -> {nextMode}", this);
    }

    bool TryResolveSlot(
        Vector3 localOffset,
        NavMeshAgent agent,
        bool allowFallback,
        bool requireDirectLine,
        float sampleRadius,
        out Vector3 destination)
    {
        if (TryResolveCandidate(localOffset, agent, requireDirectLine, sampleRadius, out destination))
            return true;

        if (!allowFallback)
            return false;

        float side = Mathf.Sign(localOffset.x);
        if (Mathf.Abs(localOffset.x) > 0.01f)
        {
            Vector3 inward = new(localOffset.x * 0.7f, localOffset.y, localOffset.z - 0.35f);
            if (TryResolveCandidate(inward, agent, requireDirectLine, sampleRadius, out destination))
                return true;

            Vector3 outward = new(localOffset.x + side * 0.5f, localOffset.y, localOffset.z - 0.6f);
            if (TryResolveCandidate(outward, agent, requireDirectLine, sampleRadius, out destination))
                return true;
        }

        Vector3 fartherBack = localOffset + Vector3.back * 0.75f;
        return TryResolveCandidate(fartherBack, agent, requireDirectLine, sampleRadius, out destination);
    }

    bool TryResolveCandidate(
        Vector3 localOffset,
        NavMeshAgent agent,
        bool requireDirectLine,
        float sampleRadius,
        out Vector3 destination)
    {
        destination = Vector3.zero;
        if (playerContext == null)
            return false;

        Vector3 forward = _formationForward.sqrMagnitude > 0.0001f
            ? _formationForward.normalized
            : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 ideal = playerContext.transform.position +
                        right * localOffset.x +
                        Vector3.up * localOffset.y +
                        forward * localOffset.z;

        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(ideal, out NavMeshHit slotHit, sampleRadius, areaMask))
            return false;

        if (requireDirectLine &&
            NavMesh.SamplePosition(playerContext.transform.position, out NavMeshHit anchorHit, 2f, areaMask) &&
            NavMesh.Raycast(anchorHit.position, slotHit.position, out _, areaMask))
        {
            return false;
        }

        destination = slotHit.position;
        return true;
    }

    static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Transform anchor = playerContext != null ? playerContext.transform : transform;
        Vector3 forward = Application.isPlaying && _formationForward.sqrMagnitude > 0.0001f
            ? _formationForward.normalized
            : Flatten(anchor.forward).normalized;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Gizmos.color = _mode == FormationMode.SingleFile ? Color.yellow : Color.green;
        FormationMode drawMode = Application.isPlaying ? _mode : FormationMode.Triangle;
        for (int partyIndex = FirstCompanionPartyIndex; partyIndex <= LastCompanionPartyIndex; partyIndex++)
        {
            Vector3 localOffset = GetLocalSlotOffset(partyIndex, drawMode);
            Vector3 point = anchor.position + right * localOffset.x + forward * localOffset.z;
            Gizmos.DrawWireSphere(point, Mathf.Max(0.1f, stopDistance));
            Gizmos.DrawLine(anchor.position, point);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(anchor.position, anchor.position + forward * 2f);
    }
}
