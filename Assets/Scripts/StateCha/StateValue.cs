using System;
using System.Collections.Generic;

// Only the owner keeps this object. Consumers receive a separate view with no mutators.
internal sealed class StateValue<TId>
{
    sealed class View : IReadOnlyState<TId>
    {
        readonly StateValue<TId> owner;
        public View(StateValue<TId> owner) => this.owner = owner;
        public TId CurrentId => owner.current;
        public event Action<TId, TId> OnChanged
        {
            add => owner.changed += value;
            remove => owner.changed -= value;
        }
    }

    TId current;
    event Action<TId, TId> changed;
    public IReadOnlyState<TId> ReadOnly { get; }
    public StateValue(TId initial)
    {
        current = initial;
        ReadOnly = new View(this);
    }
    public bool Set(TId value)
    {
        if (EqualityComparer<TId>.Default.Equals(current, value)) return false;
        TId previous = current;
        current = value;
        CharacterStateNotifications.Publish(changed, previous, value);
        return true;
    }
}
