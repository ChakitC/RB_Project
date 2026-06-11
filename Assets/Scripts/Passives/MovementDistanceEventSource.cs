using System.Collections.Generic;
using UnityEngine;

public sealed class MovementDistanceEventSource : PassiveEventSource
{
    [SerializeField, Min(0.01f)] private float defaultDistanceStep = 2f;
    [SerializeField, Min(1)] private int maxEventsPerRequestPerFrame = 8;

    readonly Dictionary<string, DistanceRequestState> _statesByEventSourceId = new();
    readonly List<string> _removeBuffer = new();

    CharacteContext _ctx;
    CombatEventBus _combatEventBus;
    Vector3 _lastPosition;
    bool _hasLastPosition;

    public override PassiveEventSourceKind Kind => PassiveEventSourceKind.MovementDistance;

    public override void ApplyRequests(
        CharacteContext ctx,
        CombatEventBus combatEventBus,
        IReadOnlyList<PassiveEventSourceRequest> requests)
    {
        _ctx = ctx != null ? ctx : ResolveContext();
        _combatEventBus = combatEventBus != null ? combatEventBus : ResolveCombatEventBus();

        foreach (var pair in _statesByEventSourceId)
            pair.Value.Required = false;

        if (requests != null)
        {
            for (int i = 0; i < requests.Count; i++)
                ApplyRequest(requests[i]);
        }

        RemoveUnusedStates();

        if (_statesByEventSourceId.Count == 0)
        {
            ClearRequests();
            return;
        }

        _lastPosition = ResolveCurrentPosition();
        _hasLastPosition = true;
        enabled = true;
    }

    public override void ClearRequests()
    {
        _statesByEventSourceId.Clear();
        _removeBuffer.Clear();
        _hasLastPosition = false;
        enabled = false;
    }

    void LateUpdate()
    {
        if (_statesByEventSourceId.Count == 0)
            return;

        if (_ctx == null)
            _ctx = ResolveContext();

        if (_combatEventBus == null)
            _combatEventBus = ResolveCombatEventBus();

        if (_combatEventBus == null)
            return;

        Vector3 currentPosition = ResolveCurrentPosition();
        if (!_hasLastPosition)
        {
            _lastPosition = currentPosition;
            _hasLastPosition = true;
            return;
        }

        Vector3 delta = currentPosition - _lastPosition;
        _lastPosition = currentPosition;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return;

        foreach (var pair in _statesByEventSourceId)
            TickRequest(pair.Value, distance);
    }

    void ApplyRequest(PassiveEventSourceRequest request)
    {
        if (!request.IsValid || request.Kind != Kind)
            return;

        if (!_statesByEventSourceId.TryGetValue(request.EventSourceId, out DistanceRequestState state))
        {
            state = new DistanceRequestState
            {
                EventSourceId = request.EventSourceId
            };
            _statesByEventSourceId.Add(request.EventSourceId, state);
        }

        state.EventType = request.EventType;
        state.DistanceStep = ResolveDistanceStep(request.FloatValue);
        state.Required = true;
    }

    void RemoveUnusedStates()
    {
        _removeBuffer.Clear();

        foreach (var pair in _statesByEventSourceId)
        {
            if (!pair.Value.Required)
                _removeBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
            _statesByEventSourceId.Remove(_removeBuffer[i]);

        _removeBuffer.Clear();
    }

    void TickRequest(DistanceRequestState state, float distance)
    {
        if (state == null || state.EventType == PassiveEventType.None || state.DistanceStep <= 0f)
            return;

        state.AccumulatedDistance += distance;

        int emitted = 0;
        int maxEvents = Mathf.Max(1, maxEventsPerRequestPerFrame);
        while (state.AccumulatedDistance + 0.0001f >= state.DistanceStep && emitted < maxEvents)
        {
            state.AccumulatedDistance -= state.DistanceStep;
            PublishDistanceEvent(state);
            emitted++;
        }

        if (emitted >= maxEvents && state.AccumulatedDistance >= state.DistanceStep)
            state.AccumulatedDistance = Mathf.Repeat(state.AccumulatedDistance, state.DistanceStep);
    }

    void PublishDistanceEvent(DistanceRequestState state)
    {
        if (_combatEventBus == null || state == null)
            return;

        var context = _combatEventBus.CreateExternalContext(
            state.EventType,
            ResolveActorGameObject(),
            null,
            state.EventSourceId,
            null,
            state.DistanceStep);

        _combatEventBus.Publish(context);
    }

    float ResolveDistanceStep(float requestValue)
    {
        if (requestValue > 0.0001f)
            return requestValue;

        return Mathf.Max(0.01f, defaultDistanceStep);
    }

    Vector3 ResolveCurrentPosition()
    {
        return _ctx != null ? _ctx.transform.position : transform.position;
    }

    GameObject ResolveActorGameObject()
    {
        if (_ctx != null)
            return _ctx.gameObject;

        return gameObject;
    }

    CharacteContext ResolveContext()
    {
        if (_ctx != null)
            return _ctx;

        if (TryGetComponent(out CharacteContext localContext))
            return localContext;

        return GetComponentInParent<CharacteContext>();
    }

    CombatEventBus ResolveCombatEventBus()
    {
        if (_combatEventBus != null)
            return _combatEventBus;

        if (_ctx != null)
        {
            _ctx.ResolveReferences();
            if (_ctx.CombatEventBus != null)
                return _ctx.CombatEventBus;
        }

        if (TryGetComponent(out CombatEventBus localBus))
            return localBus;

        return GetComponentInParent<CombatEventBus>();
    }

    sealed class DistanceRequestState
    {
        public string EventSourceId;
        public PassiveEventType EventType;
        public float DistanceStep;
        public float AccumulatedDistance;
        public bool Required;
    }
}
