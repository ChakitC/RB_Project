using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Wrappers;

public sealed partial class AICombatP0Checks
{
    public IEnumerator LegacyAllyTreesFollowMovingPlayer()
    {
        Setup(true);
        owner.gameObject.SetActive(false);
        opponent.gameObject.SetActive(false);
        var playerObject = Own(new GameObject("Legacy follow player"));
        playerObject.SetActive(false);
        playerObject.transform.position = origin + Vector3.forward * 14f;
        var playerContext = playerObject.AddComponent<PlayerContext>();
        playerContext.partyFormation = playerObject.AddComponent<PartyFormationController>();
        Set(playerContext.partyFormation, "playerContext", playerContext);

        var graphs = new[] { "Assets/Scripts/AI/Aires_Behavior.asset", "Assets/Scripts/AI/AllyAI.asset" };
        var allies = new AllyContext[graphs.Length];
        var trees = new BehaviorTree[graphs.Length];
        var initialPositions = new Vector3[graphs.Length];
        for (int i = 0; i < graphs.Length; i++)
        {
            var ally = Actor<AllyContext>(origin + Vector3.right * (i * 3f - 1.5f), i == 0);
            ally.NavigationAgent = ally.gameObject.AddComponent<NavMeshAgent>();
            ally.NavigationAgent.speed = 4f;
            ally.CharacterLoad = ally.gameObject.AddComponent<CharacterContextPartyLoader>();
            ally.CharacterLoad.ConfigurePartyIndex(i + 1);
            var tree = ally.TargetInfo.gameObject.AddComponent<BehaviorTree>();
            tree.StartWhenEnabled = false;
            tree.Subgraph = AssetDatabase.LoadAssetAtPath<Subtree>(graphs[i]);
            tree.UpdateMode = Opsive.BehaviorDesigner.Runtime.Components.UpdateMode.Manual;
            ally.gameObject.SetActive(true);
            ally.ResolveReferences();
            Assert.IsTrue(tree.StartBehavior(), graphs[i]);
            tree.GetVariable<GameObject>("player").Value = playerObject;
            allies[i] = ally;
            trees[i] = tree;
            initialPositions[i] = ally.transform.position;
        }
        float deadline = Time.time + 2f;
        while (Time.time < deadline)
        {
            foreach (var tree in trees) tree.Tick();
            yield return null;
        }
        for (int i = 0; i < trees.Length; i++)
        {
            Assert.AreSame(allies[i], trees[i].GetVariable<CharacteContext>("Ctx").Value, graphs[i]);
            Assert.Greater(Vector3.Distance(initialPositions[i], allies[i].transform.position), 1f,
                graphs[i] + " must initialize before following the distant player.");
            initialPositions[i] = allies[i].transform.position;
        }
        playerObject.transform.position += Vector3.right * 7f;
        deadline = Time.time + 2f;
        while (Time.time < deadline)
        {
            foreach (var tree in trees) tree.Tick();
            yield return null;
        }
        for (int i = 0; i < trees.Length; i++)
        {
            Assert.Greater(Vector3.Distance(initialPositions[i], allies[i].transform.position), 1f,
                graphs[i] + " must follow the player's changed position.");
            var task = trees[i].TreeLogicNodes.SelectMany(AIContextGraphAuthoring.Tasks)
                .OfType<MoveToPlayerOffsetNavMesh>().Single();
            Assert.AreSame(playerObject, task.Taget.Value);
            Assert.IsFalse(task.InCombat.Value);
            Assert.AreSame(allies[i], task.context.Value);
            trees[i].StopBehavior(false);
        }
    }
}
