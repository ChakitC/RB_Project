using System;
using System.Collections.Generic;

public interface IState<TCtx>
{
    void Enter(TCtx ctx);
    void Exit(TCtx ctx);
    void Tick(TCtx ctx, float dt);
    
}

public sealed class StateMachine<TId, TCtx> where TId : notnull
{
    private readonly Dictionary<TId, IState<TCtx>> _states = new();
    private readonly TCtx _ctx;

    public TId CurrentId { get; set; }
    public IState<TCtx> Current { get; private set; }

    public event Action<TId, TId> OnChanged; // (from, to)

    public StateMachine(TCtx ctx) => _ctx = ctx;

    public StateMachine<TId, TCtx> Add(TId id, IState<TCtx> state)
    {
        _states[id] = state;
        return this;
    }

    public void SetInitial(TId id)
    {
        CurrentId = id;
        Current = Get(id);
        Current.Enter(_ctx);
    }

    public bool TryChange(TId nextId)
    {
        if (Equals(CurrentId, nextId)) return false;

        var next = Get(nextId);

        var from = CurrentId;
        Current?.Exit(_ctx);

        CurrentId = nextId;
        Current = next;

        Current.Enter(_ctx);
        OnChanged?.Invoke(from, nextId);
        return true;
    }

    public void Tick(float dt) => Current?.Tick(_ctx, dt);
   

    private IState<TCtx> Get(TId id)
    {
        if (_states.TryGetValue(id, out var st)) return st;
        throw new KeyNotFoundException($"State '{id}' not registered.");
    }
}