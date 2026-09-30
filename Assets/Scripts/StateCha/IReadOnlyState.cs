using System;

public interface IReadOnlyState<TId>
{
    TId CurrentId { get; }
    event Action<TId, TId> OnChanged;
}
