using System;
using System.Collections.Generic;

public sealed class PartyCombatEventRouter : IDisposable
{
    sealed class Subscription
    {
        public PartyRuntimeActor Actor;
        public CombatEventBus Bus;
        public Action<PassiveEventContext> Handler;
    }

    readonly List<Subscription> subscriptions = new List<Subscription>();
    readonly HashSet<CombatEventBus> subscribedBuses = new HashSet<CombatEventBus>();

    public event Action<PartyRuntimeActor, PassiveEventContext> EventRouted;

    public void Bind(PartyRuntime party)
    {
        Unbind();
        if (party == null)
            return;

        for (int i = 0; i < party.Actors.Count; i++)
        {
            PartyRuntimeActor actor = party.Actors[i];
            CharacteContext context = actor?.Context;
            context?.ResolveReferences();
            CombatEventBus bus = context != null ? context.CombatEventBus : null;
            if (bus == null || !subscribedBuses.Add(bus))
                continue;

            Action<PassiveEventContext> handler = fact => Route(actor, fact);
            bus.EventPublished += handler;
            subscriptions.Add(new Subscription { Actor = actor, Bus = bus, Handler = handler });
        }
    }

    public void Unbind()
    {
        for (int i = 0; i < subscriptions.Count; i++)
        {
            Subscription subscription = subscriptions[i];
            if (subscription.Bus != null)
                subscription.Bus.EventPublished -= subscription.Handler;
        }

        subscriptions.Clear();
        subscribedBuses.Clear();
    }

    public void Dispose()
    {
        Unbind();
        EventRouted = null;
    }

    void Route(PartyRuntimeActor actor, in PassiveEventContext context)
    {
        ulong factId = context.FactId != 0 ? context.FactId : CombatEventBus.NextFactId();
        ulong chainId = context.ChainId != 0 ? context.ChainId : CombatEventBus.NextChainId();
        PassiveEventContext normalized = context.FactId == factId && context.ChainId == chainId
            ? context
            : context.WithIdentity(factId, chainId);
        EventRouted?.Invoke(actor, normalized);
    }
}
