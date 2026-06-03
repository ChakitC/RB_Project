using System.Collections.Generic;
using UnityEngine;

public abstract class PassiveEventSource : MonoBehaviour
{
    public abstract PassiveEventSourceKind Kind { get; }

    public abstract void ApplyRequests(
        CharacteContext ctx,
        CombatEventBus combatEventBus,
        IReadOnlyList<PassiveEventSourceRequest> requests);

    public abstract void ClearRequests();
}
