using System;
using System.Collections.Generic;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using UnityEngine;

public static class PartyRuntimeBinder
{
    const string PlayerVariableName = "player";

    public static bool TryBind(PartyRuntime party, out string error)
    {
        if (party == null)
        {
            error = "Party runtime is null.";
            return false;
        }

        PlayerContext player = party.Player;
        if (player == null)
        {
            error = "Party runtime has no PlayerContext for the Player role.";
            return false;
        }

        player.ResolveReferences();

        FieldAllyManager fieldManager = player.fieldAllyManager;
        if (fieldManager == null)
        {
            error = "Player prefab is missing FieldAllyManager.";
            return false;
        }

        AllyHelperManager helperManager = player.allyHelper;
        if (helperManager == null)
        {
            error = "Player prefab is missing AllyHelperManager.";
            return false;
        }

        var members = new List<FieldAllyMember>(party.Actors.Count);
        for (int i = 0; i < party.Actors.Count; i++)
        {
            PartyRuntimeActor actor = party.Actors[i];
            if (actor.PartyLoader == null)
            {
                error = $"Role '{actor.Role}' is missing CharacterContextPartyLoader.";
                return false;
            }

            if (actor.FieldMember == null)
            {
                error = $"Role '{actor.Role}' is missing FieldAllyMember.";
                return false;
            }

            actor.PartyLoader.ConfigurePartyIndex(actor.PartyIndex);
            actor.FieldMember.ConfigureRuntime(actor.Role, fieldManager);
            members.Add(actor.FieldMember);

            actor.Context.ResolveReferences();
            if (actor.Role != ChainActorRole.Player &&
                !TryBindBehaviorTreePlayer(actor.Root, player.gameObject, out error))
            {
                return false;
            }
        }

        fieldManager.ConfigureRuntimeMembers(members);

        PartyFormationController formationController = player.partyFormation;
        if (formationController == null)
        {
            error = "Player prefab is missing PartyFormationController.";
            return false;
        }

        try
        {
            formationController.ConfigureRuntimeActors(party.Actors);
        }
        catch (Exception exception)
        {
            error = $"Party formation binding failed: {exception.Message}";
            return false;
        }

        AllyContext helper = party.Helper;
        if (helper == null)
        {
            error = "Party runtime has no AllyContext for the Helper role.";
            return false;
        }

        helperManager.BindHelper(helper);

        if (party.PlayerUIRoot == null || party.PlayerUIContext == null)
        {
            error = "Party runtime is missing Player UI.";
            return false;
        }

        PlayerUIRuntimeBinder uiBinder =
            party.PlayerUIRoot.GetComponentInChildren<PlayerUIRuntimeBinder>(true);
        if (uiBinder == null)
        {
            error = "Player UI prefab is missing PlayerUIRuntimeBinder.";
            return false;
        }

        return uiBinder.TryBind(player, out error);
    }

    static bool TryBindBehaviorTreePlayer(GameObject actorRoot, GameObject player, out string error)
    {
        BehaviorTree[] behaviorTrees = actorRoot.GetComponentsInChildren<BehaviorTree>(true);
        bool foundPlayerVariable = false;

        for (int i = 0; i < behaviorTrees.Length; i++)
        {
            BehaviorTree tree = behaviorTrees[i];
            if (tree == null || tree.GetVariable(PlayerVariableName) == null)
                continue;

            foundPlayerVariable = true;
            if (!tree.SetVariableValue(PlayerVariableName, player))
            {
                error = $"Behavior Tree '{tree.name}' rejected shared variable '{PlayerVariableName}'.";
                return false;
            }
        }

        GameObjectSharedVariables[] variableContainers =
            actorRoot.GetComponentsInChildren<GameObjectSharedVariables>(true);
        for (int containerIndex = 0; containerIndex < variableContainers.Length; containerIndex++)
        {
            SharedVariable[] variables = variableContainers[containerIndex].SharedVariables;
            if (variables == null)
                continue;

            for (int variableIndex = 0; variableIndex < variables.Length; variableIndex++)
            {
                SharedVariable variable = variables[variableIndex];
                if (variable == null ||
                    !string.Equals(variable.Name.ToString(), PlayerVariableName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (variable is not SharedVariable<GameObject> playerVariable)
                {
                    error = $"Actor '{actorRoot.name}' shared variable '{PlayerVariableName}' is not a GameObject.";
                    return false;
                }

                playerVariable.Value = player;
                foundPlayerVariable = true;
            }
        }

        if (!foundPlayerVariable)
        {
            error = $"Actor '{actorRoot.name}' has no Behavior Tree shared variable '{PlayerVariableName}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
