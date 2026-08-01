using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PartyRuntime
{
    readonly Dictionary<ChainActorRole, PartyRuntimeActor> actorsByRole = new();
    readonly List<PartyRuntimeActor> actors = new();

    public PartyRuntime(GameObject root)
    {
        Root = root != null ? root : throw new ArgumentNullException(nameof(root));
    }

    public GameObject Root { get; }
    public GameObject PlayerUIRoot { get; private set; }
    public PlayerUIContext PlayerUIContext { get; private set; }
    public IReadOnlyList<PartyRuntimeActor> Actors => actors;
    public PlayerContext Player => GetActor(ChainActorRole.Player)?.Context as PlayerContext;
    public AllyContext Helper => GetActor(ChainActorRole.Helper)?.Context as AllyContext;

    public PartyRuntimeActor GetActor(ChainActorRole role)
    {
        actorsByRole.TryGetValue(role, out PartyRuntimeActor actor);
        return actor;
    }

    internal void AddActor(PartyRuntimeActor actor)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (actorsByRole.ContainsKey(actor.Role))
            throw new InvalidOperationException($"Party role '{actor.Role}' already exists.");

        actorsByRole.Add(actor.Role, actor);
        actors.Add(actor);
    }

    internal void SetPlayerUI(GameObject root, PlayerUIContext context)
    {
        PlayerUIRoot = root;
        PlayerUIContext = context;
    }
}

public sealed class PartyRuntimeActor
{
    public PartyRuntimeActor(
        ChainActorRole role,
        int partyIndex,
        GameObject root,
        CharacteContext context,
        CharacterContextPartyLoader partyLoader,
        FieldAllyMember fieldMember)
    {
        Role = role;
        PartyIndex = partyIndex;
        Root = root;
        Context = context;
        PartyLoader = partyLoader;
        FieldMember = fieldMember;
    }

    public ChainActorRole Role { get; }
    public int PartyIndex { get; }
    public GameObject Root { get; }
    public CharacteContext Context { get; }
    public CharacterContextPartyLoader PartyLoader { get; }
    public FieldAllyMember FieldMember { get; }
}
