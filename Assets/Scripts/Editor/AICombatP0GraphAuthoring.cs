using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
using Opsive.BehaviorDesigner.Runtime.Wrappers;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;

/// <summary>Explicit authoring command for the one P0 example; never runs automatically on import.</summary>
public static class AICombatP0GraphAuthoring
{
    public const string GraphPath = "Assets/Scripts/AI/Enemy_M_GR_01_PatrolOrbitShoot.asset";
    sealed class Node
    {
        public ITreeLogicNode Logic;
        public string Name;
        public Node[] Children;
        public Node(ITreeLogicNode logic, string name, params Node[] children) { Logic = logic; Name = name; Children = children; }
    }
    static SharedVariable<GameObject> Target() => new SharedVariable<GameObject>
        { Name = "CurrentTarget", Scope = SharedVariable.SharingScope.Graph };
    static SharedVariable<AICombatState> State() => new SharedVariable<AICombatState>
        { Name = "CombatState", Scope = SharedVariable.SharingScope.Graph };
    static Node Leaf(Task task, string name = null)
    {
        AIContextGraphAuthoring.BindTask(task);
        if (task is AICombatConditional c) { c.combatState = State(); c.target = Target(); }
        if (task is AICombatAction a) { a.combatState = State(); a.target = Target(); }
        if (task is Conditional) return new Node(new StackedConditional { Tasks = new[] { task } }, name ?? task.GetType().Name);
        return new Node(new StackedAction { Tasks = new[] { task } }, name ?? task.GetType().Name);
    }
    static Node Sequence(string name, ConditionalAbortType abort, params Node[] children) =>
        new Node(new Sequence { AbortType = abort }, name, children);
    static Node Selector(string name, params Node[] children) => new Node(new Selector(), name, children);
    static Node HasTarget() => Leaf(new HasEnemyFromSensor { currentEnemy = Target(), InCombat = new SharedVariable<bool>
        { Name = "InCombat", Scope = SharedVariable.SharingScope.Graph } });
    static Node Shoot() => Leaf(new AiShoot { target = Target(), fireDuration = 1.5f, waitDuration = .75f,
        faceTarget = true, turnSpeed = 720f, requireLineOfSight = true, minFireRange = .5f, maxFireRange = 10f,
        aimToleranceDegrees = 8f, returnSuccessWhenTargetLost = false });
    static Node CanShoot() => Leaf(new CanShootTarget { allowReloadCycle = true, checkAimAngle = false });
    static Node Wait() => Leaf(new AICombatWait());

    [MenuItem("Tools/RB/AI/Apply P0 to M_GR_01")]
    public static void Apply()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Subtree>(GraphPath);
        if (graph == null) throw new InvalidOperationException("P0 example Subtree is missing.");
        graph.Deserialize(true);
        var root = new Node(new Repeater { RepeatForever = true, EndOnFailure = false }, "P0 repeat",
            Sequence("Prepare owner context", ConditionalAbortType.None, Leaf(new InitializeAIContext()),
            Selector("Act or wait",
                Sequence("Autonomy", ConditionalAbortType.Both,
                    Leaf(new CanAct()),
                    Selector("Priorities",
                        Sequence("Recovery", ConditionalAbortType.Both, Leaf(new IsNavigationStuck()), Leaf(new RecoverNavigation())),
                        Sequence("Combat", ConditionalAbortType.Both,
                            HasTarget(), Leaf(new CanEngageTarget { target = Target() }),
                            Selector("Firing approach",
                                Sequence("Visible orbit and burst", ConditionalAbortType.Self,
                                    Leaf(new IsCurrentTargetVisible()), Leaf(new MoveToCombatRange()),
                                    Leaf(new TargetOrbitNavMesh { target = Target(), combatState = State(), minRadius = 5f, maxRadius = 7f,
                                        duration = 2.5f, turnSpeed = 720f, moveSpeed = 0f, repathInterval = .25f,
                                        lookAheadAngle = 30f, pathFailureTimeout = 3f, faceTarget = true }), CanShoot(), Shoot()),
                                Sequence("Reposition and burst", ConditionalAbortType.None,
                                    Leaf(new FindFiringPosition()), Leaf(new MoveToFiringPosition()), CanShoot(), Shoot()),
                                Wait())),
                        Sequence("Investigate then search", ConditionalAbortType.LowerPriority,
                            Leaf(new HasLastSeenPosition()), Leaf(new InvestigateLastSeenPosition()), Leaf(new SearchArea())),
                        Sequence("Retained target wait", ConditionalAbortType.None, HasTarget(), Wait()),
                        Leaf(new Patrol { combatState = State(), patrolRadius = 6f, lockPatrolCenterPosition = true,
                            navMeshSampleRadius = 2f, randomSampleAttempts = 8, stopDistance = .3f,
                            waitTime = 1f, randomizePatrolPoints = false, destination = Vector3.zero }), Wait())),
                Wait())));
        var nodes = new List<ITreeLogicNode>();
        var props = new List<LogicNodeProperties>();
        Flatten(root, ushort.MaxValue, 0, 0f, nodes, props);
        graph.TreeLogicNodes = nodes.ToArray();
        graph.LogicNodeProperties = props.ToArray();
        graph.SharedVariables = new SharedVariable[] { Target(), new SharedVariable<bool>
            { Name = "InCombat", Scope = SharedVariable.SharingScope.Graph }, State(), AIContextGraphAuthoring.Binding() };
        graph.DisabledLogicNodes = Array.Empty<ushort>();
        graph.GroupProperties = Array.Empty<GroupProperties>();
        graph.EventNodes = new IEventNode[] { new Opsive.BehaviorDesigner.Runtime.Tasks.Events.Start { Index = 0, ConnectedIndex = 0, Enabled = true } };
        graph.EventNodeProperties = new NodeProperties[] { new NodeProperties { GuidString = Guid.NewGuid().ToString(),
            Position = new Vector2(0, -100), ConnectionGuidStrings = new[] { props[0].GuidString } } };
        graph.Serialize();
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
    }
    static ushort Flatten(Node node, ushort parent, int depth, float x, List<ITreeLogicNode> nodes, List<LogicNodeProperties> props)
    {
        ushort index = (ushort)nodes.Count;
        node.Logic.Index = index;
        node.Logic.ParentIndex = parent;
        node.Logic.SiblingIndex = ushort.MaxValue;
        node.Logic.Enabled = true;
        nodes.Add(node.Logic);
        var p = new LogicNodeProperties { GuidString = Guid.NewGuid().ToString(), DisplayName = node.Name,
            Position = new Vector2(x, depth * 140), Width = 180f };
        props.Add(p);
        if (node.Children != null)
        {
            ushort previous = ushort.MaxValue;
            var connections = new List<string>();
            for (int i = 0; i < node.Children.Length; i++)
            {
                ushort child = Flatten(node.Children[i], index, depth + 1, x + (i - (node.Children.Length - 1) * .5f) * 260f, nodes, props);
                if (previous != ushort.MaxValue) nodes[previous].SiblingIndex = child;
                previous = child;
                connections.Add(props[child].GuidString);
            }
            p.ConnectionGuidStrings = connections.ToArray();
        }
        return index;
    }
}
