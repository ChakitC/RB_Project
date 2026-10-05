using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using SubtreeAsset = Opsive.BehaviorDesigner.Runtime.Wrappers.Subtree;
using Task = Opsive.BehaviorDesigner.Runtime.Tasks.Task;

/// <summary>Explicit migration and validation. Existing nodes and their GUIDs are retained.</summary>
public static class AIContextGraphAuthoring
{
    public static SharedVariable<CharacteContext> Binding() => new SharedVariable<CharacteContext>
        { Name = "Ctx", Scope = SharedVariable.SharingScope.Graph };

    public static IEnumerable<Task> Tasks(ITreeLogicNode node)
    {
        if (node is StackedAction a) return a.Tasks ?? Array.Empty<Task>();
        if (node is StackedConditional c) return c.Tasks ?? Array.Empty<Task>();
        return node is Task t ? new[] { t } : Array.Empty<Task>();
    }

    public static bool BindTask(Task task)
    {
        var field = task?.GetType().GetField("context");
        if (field == null || field.FieldType != typeof(SharedVariable<CharacteContext>)) return false;
        field.SetValue(task, Binding());
        return true;
    }

    sealed class Node
    {
        public ITreeLogicNode Logic;
        public LogicNodeProperties Properties;
        public readonly List<Node> Children = new();
        public Node(ITreeLogicNode logic, LogicNodeProperties properties)
        { Logic = logic; Properties = properties; }
    }

    static Node NewNode(ITreeLogicNode logic, string name) => new(logic,
        new LogicNodeProperties { GuidString = Guid.NewGuid().ToString(), DisplayName = name, Width = 180f });

    public static bool Migrate(IGraph graph) => Migrate(graph, new HashSet<IGraph>());

    static bool Migrate(IGraph graph, HashSet<IGraph> visiting)
    {
        var old = graph.LogicNodes?.Cast<ITreeLogicNode>().ToArray();
        if (old == null || old.Length == 0) return false;
        if (!visiting.Add(graph)) throw new InvalidOperationException("Cyclic Subtree reference cannot initialize Ctx safely.");
        // Prepare referenced graphs before building native variable overrides in their parents.
        foreach (var reference in old.SelectMany(Tasks).OfType<SubtreeReference>())
            foreach (var child in reference.Subtrees ?? Array.Empty<Opsive.BehaviorDesigner.Runtime.Subtree>())
            {
                if (child == null || ReferenceEquals(child, graph)) continue;
                child.Deserialize(true);
                if (Migrate(child, visiting)) { child.Serialize(); EditorUtility.SetDirty(child); }
            }
        visiting.Remove(graph);
        if (!old.SelectMany(Tasks).Any(NeedsContext)) return false;
        if (old.SelectMany(Tasks).Any(t => t is InitializeAIContext))
        {
            bool changed = RepairInitializerOrder(graph);
            var errors = Validate(graph);
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            return changed;
        }
        var variables = (graph.SharedVariables ?? Array.Empty<SharedVariable>()).ToList();
        var existing = variables.FirstOrDefault(v => v.Name == "Ctx");
        if (existing != null && (existing.GetType() != typeof(SharedVariable<CharacteContext>) || existing.Scope != SharedVariable.SharingScope.Graph))
            throw new InvalidOperationException("Ctx already exists with an incompatible type or scope.");
        if (existing == null) variables.Add(Binding());
        else ((SharedVariable<CharacteContext>)existing).Value = null;
        graph.SharedVariables = variables.ToArray();
        var inCombat = old.SelectMany(Tasks).OfType<HasEnemyFromSensor>().Select(t => t.InCombat).FirstOrDefault(v => v != null);
        foreach (var task in old.SelectMany(Tasks))
        {
            BindTask(task);
            var combatField = task?.GetType().GetField("InCombat");
            if (task is MoveToFormationSlotNavMesh || task is MoveToPlayerOffsetNavMesh || task is RandomMoveAroundPlayerNavMesh)
                combatField?.SetValue(task, inCombat != null ? new SharedVariable<bool>
                    { Name = inCombat.Name, Scope = inCombat.Scope, Value = inCombat.Value } : new SharedVariable<bool> { Value = false });
            BindSubtreeOverride(task);
        }
        var entries = old.Select((n, i) => new Node(n, graph.LogicNodeProperties[i])).ToArray();
        var byOldIndex = entries.ToDictionary(n => n.Logic.Index);
        var roots = new List<Node>();
        foreach (var entry in entries)
        {
            if (entry.Logic.ParentIndex == ushort.MaxValue) roots.Add(entry);
            else byOldIndex[entry.Logic.ParentIndex].Children.Add(entry);
        }
        var eventRoots = new Dictionary<ushort, Node>();
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            var sequence = NewNode(new Sequence(), "Prepare owner context");
            sequence.Properties.Position = root.Properties.Position + Vector2.down * 120f;
            sequence.Children.Add(NewNode(new StackedAction { Tasks = new Task[]
                { new InitializeAIContext { context = Binding() } } }, "InitializeAIContext → Ctx"));
            if (root.Logic is Repeater)
            {
                sequence.Children.AddRange(root.Children);
                root.Children.Clear(); root.Children.Add(sequence);
                eventRoots[root.Logic.Index] = root;
            }
            else
            {
                sequence.Children.Add(root); roots[i] = sequence;
                eventRoots[root.Logic.Index] = sequence;
            }
            PositionInitializer(sequence);
        }
        var indices = new Dictionary<ushort, ushort>();
        var nodes = new List<ILogicNode>();
        var props = new List<LogicNodeProperties>();
        foreach (var root in roots) Flatten(root, ushort.MaxValue, nodes, props, indices, byOldIndex.Values);
        // Opsive's IGraph setter casts the array itself to ITreeLogicNode[]. Keep the runtime
        // array type; an ILogicNode[] containing valid tree nodes silently clears the graph.
        graph.LogicNodes = nodes.Cast<ITreeLogicNode>().ToArray(); graph.LogicNodeProperties = props.ToArray();
        if (graph.EventNodes != null)
            for (int i = 0; i < graph.EventNodes.Length; i++)
            {
                var evt = graph.EventNodes[i];
                var connected = evt.GetType().GetProperty("ConnectedIndex");
                if (connected == null) continue;
                var previous = (ushort)connected.GetValue(evt);
                if (!eventRoots.TryGetValue(previous, out var root)) continue;
                connected.SetValue(evt, root.Logic.Index);
                graph.EventNodeProperties[i].ConnectionGuidStrings = new[] { root.Properties.GuidString };
            }
        var disabledProperty = graph.GetType().GetProperty("DisabledLogicNodes");
        if (disabledProperty?.GetValue(graph) is ushort[] disabled)
            disabledProperty.SetValue(graph, disabled.Select(i => indices.TryGetValue(i, out var index) ? index : i).ToArray());
        var issues = Validate(graph);
        if (issues.Count != 0) throw new InvalidOperationException(string.Join("\n", issues));
        return true;
    }

    // Graph Designer orders tree siblings by horizontal position when editing/saving.
    // Keep initialization left of the existing behavior as well as first in the indices.
    static bool PositionInitializer(Node sequence)
    {
        var initializer = sequence.Children.FirstOrDefault(n => Tasks(n.Logic).Any(t => t is InitializeAIContext));
        var siblings = sequence.Children.Where(n => n != initializer).ToArray();
        if (initializer == null || siblings.Length == 0) return false;
        float left = siblings.Min(n => n.Properties.Position.x);
        if (initializer.Properties.Position.x < left) return false;
        initializer.Properties.Position = new Vector2(left - 260f, sequence.Properties.Position.y + 140f);
        return true;
    }

    static bool RepairInitializerOrder(IGraph graph)
    {
        var entries = graph.LogicNodes.Cast<ITreeLogicNode>()
            .Select((n, i) => new Node(n, graph.LogicNodeProperties[i])).ToArray();
        var byIndex = entries.ToDictionary(n => n.Logic.Index);
        var roots = new List<Node>();
        foreach (var entry in entries)
            if (entry.Logic.ParentIndex == ushort.MaxValue) roots.Add(entry);
            else byIndex[entry.Logic.ParentIndex].Children.Add(entry);
        bool changed = false;
        foreach (var parent in entries.Where(n => n.Logic is Sequence))
        {
            var initializer = parent.Children.FirstOrDefault(n => Tasks(n.Logic).Any(t => t is InitializeAIContext));
            if (initializer == null) continue;
            changed |= PositionInitializer(parent);
            if (parent.Children[0] == initializer) continue;
            parent.Children.Remove(initializer);
            parent.Children.Insert(0, initializer);
            changed = true;
        }
        if (!changed) return false;
        var indices = new Dictionary<ushort, ushort>();
        var nodes = new List<ILogicNode>();
        var props = new List<LogicNodeProperties>();
        foreach (var root in roots) Flatten(root, ushort.MaxValue, nodes, props, indices, entries);
        graph.LogicNodes = nodes.Cast<ITreeLogicNode>().ToArray();
        graph.LogicNodeProperties = props.ToArray();
        if (graph.EventNodes != null)
            for (int i = 0; i < graph.EventNodes.Length; i++)
            {
                var connected = graph.EventNodes[i].GetType().GetProperty("ConnectedIndex");
                if (connected == null || !indices.TryGetValue((ushort)connected.GetValue(graph.EventNodes[i]), out var index)) continue;
                connected.SetValue(graph.EventNodes[i], index);
                graph.EventNodeProperties[i].ConnectionGuidStrings = new[] { props[index].GuidString };
            }
        var disabledProperty = graph.GetType().GetProperty("DisabledLogicNodes");
        if (disabledProperty?.GetValue(graph) is ushort[] disabled)
            disabledProperty.SetValue(graph, disabled.Select(i => indices.TryGetValue(i, out var index) ? index : i).ToArray());
        return true;
    }

    static bool NeedsContext(Task task) => task?.GetType().GetField("context") != null ||
        (task is SubtreeReference reference && reference.Subtrees?.Any(g =>
            g != null && g.SharedVariables?.Any(v => v.Name == "Ctx") == true) == true);

    static void Flatten(Node node, ushort parent, List<ILogicNode> nodes,
        List<LogicNodeProperties> props, Dictionary<ushort, ushort> indices, IEnumerable<Node> original)
    {
        var oldIndex = node.Logic.Index;
        node.Logic.Index = (ushort)nodes.Count;
        if (original.Contains(node)) indices[oldIndex] = node.Logic.Index;
        node.Logic.ParentIndex = parent; node.Logic.SiblingIndex = ushort.MaxValue;
        nodes.Add(node.Logic); props.Add(node.Properties);
        Node previous = null;
        foreach (var child in node.Children)
        {
            Flatten(child, node.Logic.Index, nodes, props, indices, original);
            if (previous != null) previous.Logic.SiblingIndex = child.Logic.Index;
            previous = child;
        }
        node.Properties.ConnectionGuidStrings = node.Children.Select(c => c.Properties.GuidString).ToArray();
    }

    static void BindSubtreeOverride(Task task)
    {
        if (task is not SubtreeReference reference || reference.Subtrees == null) return;
        var overrides = (reference.SharedVariableOverrides ?? Array.Empty<SharedVariableOverride>()).ToList();
        foreach (var child in reference.Subtrees)
        {
            if (child == null) continue;
            child.Deserialize(true);
            var source = child.SharedVariables?.FirstOrDefault(v => v.Name == "Ctx");
            if (source == null) continue;
            overrides.RemoveAll(v => v.SourceGraph == child && v.Source?.Name == "Ctx");
            overrides.Add(new SharedVariableOverride { Source = source, Override = Binding(), SourceGraph = child });
        }
        reference.SharedVariableOverrides = overrides.ToArray();
    }

    public static List<string> Validate(IGraph graph)
    {
        var errors = new List<string>();
        var nodes = graph.LogicNodes?.Cast<ITreeLogicNode>().ToArray() ?? Array.Empty<ITreeLogicNode>();
        if (nodes.Length == 0 && graph.SharedVariables?.Any(v => v.Name == "Ctx") == true)
            errors.Add("Ctx graph has no logic nodes; serialization did not preserve its tree.");
        var tasks = nodes.SelectMany(Tasks).Where(t => t != null).ToArray();
        if (!tasks.Any(NeedsContext)) return errors;
        var variables = graph.SharedVariables?.Where(v => v.Name == "Ctx").ToArray() ?? Array.Empty<SharedVariable>();
        if (variables.Length != 1 || variables[0] is not SharedVariable<CharacteContext> || variables[0].Scope != SharedVariable.SharingScope.Graph)
            errors.Add("Declare exactly one Graph-scoped CharacteContext variable Ctx.");
        foreach (var task in tasks)
        {
            var field = task.GetType().GetField("context");
            if (field == null) continue;
            if (field.FieldType != typeof(SharedVariable<CharacteContext>) || !AIContextBinding.IsBound(field.GetValue(task) as SharedVariable<CharacteContext>))
                errors.Add(task.GetType().Name + ": context must bind to Graph Ctx.");
        }
        var children = nodes.GroupBy(n => n.ParentIndex).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var node in nodes.Where(n => Tasks(n).Any(t => t is InitializeAIContext)))
            if (graph.LogicNodeProperties != null && graph.LogicNodeProperties.Length == nodes.Length &&
                children.TryGetValue(node.ParentIndex, out var siblings) &&
                siblings.Any(s => s != node && graph.LogicNodeProperties[s.Index].Position.x <= graph.LogicNodeProperties[node.Index].Position.x))
                errors.Add("InitializeAIContext must be leftmost among its siblings so Graph Designer saves it first.");
        foreach (var root in nodes.Where(n => n.ParentIndex == ushort.MaxValue)) CheckPath(root, false, children, errors);
        return errors;
    }

    // Only a Sequence establishes ordering between siblings. Selector/parallel siblings cannot
    // satisfy another child's initialization; stacked tasks run in their authored order.
    static bool CheckPath(ITreeLogicNode node, bool initialized,
        Dictionary<ushort, ITreeLogicNode[]> children, List<string> errors)
    {
        foreach (var task in Tasks(node))
        {
            if (task is InitializeAIContext) initialized = true;
            else if (task?.GetType().GetField("context") != null && !initialized)
                errors.Add(task.GetType().Name + ": reachable before InitializeAIContext at node " + node.Index);
            if (task is SubtreeReference reference && reference.Subtrees != null)
            {
                if (NeedsContext(task) && !initialized)
                    errors.Add("SubtreeReference: reachable before InitializeAIContext at node " + node.Index);
                foreach (var child in reference.Subtrees)
                    if (child != null && child.SharedVariables?.Any(v => v.Name == "Ctx") == true &&
                        reference.SharedVariableOverrides?.Any(v => v.SourceGraph == child && v.Source?.Name == "Ctx" &&
                            AIContextBinding.IsBound(v.Override as SharedVariable<CharacteContext>)) != true)
                        errors.Add("SubtreeReference: explicitly override child Graph Ctx with parent Ctx.");
            }
        }
        if (children.TryGetValue(node.Index, out var descendants))
        {
            bool sequential = node is Sequence;
            bool allInitialized = initialized;
            foreach (var child in descendants)
            {
                var result = CheckPath(child, sequential ? allInitialized : initialized, children, errors);
                if (sequential) allInitialized = result;
            }
            if (sequential) initialized = allInitialized;
        }
        return initialized;
    }

    [MenuItem("Tools/RB/AI/Migrate Owner Context")]
    public static void ApplyAll()
    {
        foreach (var path in AssetDatabase.FindAssets("t:Subtree").Select(AssetDatabase.GUIDToAssetPath))
        {
            var graph = AssetDatabase.LoadAssetAtPath<SubtreeAsset>(path);
            graph.Deserialize(true);
            if (!Migrate(graph)) continue;
            graph.Serialize(); EditorUtility.SetDirty(graph);
        }
        foreach (var path in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset.GetComponentsInChildren<BehaviorTree>(true).Any(b => b.Subgraph == null)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var tree in root.GetComponentsInChildren<BehaviorTree>(true))
                {
                    if (tree.Subgraph != null) continue;
                    tree.Deserialize(true);
                    if (!Migrate(tree)) continue;
                    tree.Serialize(); changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Inspect scene authoring too, including overrides of prefab inline trees.
        foreach (var path in AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/Opsive/")))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                bool changed = false;
                foreach (var tree in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BehaviorTree>(true)))
                {
                    if (tree.Subgraph != null) continue;
                    tree.Deserialize(true);
                    if (!Migrate(tree)) continue;
                    tree.Serialize(); changed = true;
                }
                if (changed) EditorSceneManager.SaveScene(scene, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/RB/AI/Validate Owner Context")]
    public static void ValidateAll()
    {
        int failures = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Subtree").Select(AssetDatabase.GUIDToAssetPath))
        {
            var graph = AssetDatabase.LoadAssetAtPath<SubtreeAsset>(path); graph.Deserialize(true);
            foreach (var issue in Validate(graph)) { Debug.LogError(path + ": " + issue, graph); failures++; }
        }
        foreach (var path in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath))
            foreach (var tree in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<BehaviorTree>(true))
            {
                if (tree.Subgraph != null) continue;
                tree.Deserialize(true);
                foreach (var issue in Validate(tree)) { Debug.LogError(path + ": " + issue, tree); failures++; }
            }
        foreach (var path in AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/Opsive/")))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var tree in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BehaviorTree>(true)))
                {
                    if (tree.Subgraph != null) continue;
                    tree.Deserialize(true);
                    foreach (var issue in Validate(tree)) { Debug.LogError(path + ": " + issue, tree); failures++; }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        if (failures != 0) throw new InvalidOperationException($"Owner Context validation failed: {failures} issues.");
        Debug.Log("Owner Context bindings and initialization order passed.");
    }
}
