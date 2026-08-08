using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ActiveSkillTreeEditorWindow : EditorWindow
{
    SkillTreeGraphView _graph;
    IMGUIContainer _inspector;
    ObjectField _assetField;
    SkillUpgradeTreeDefinition _tree;
    SkillUpgradeNodeData _selectedNode;
    SerializedObject _serializedTree;
    bool _dirty;
    List<SkillUpgradeValidationIssue> _cachedIssues = new();
    bool _issuesDirty = true;

    [MenuItem("Tools/RB/Skills/Active Skill Tree Editor")]
    public static ActiveSkillTreeEditorWindow Open()
    {
        var window = GetWindow<ActiveSkillTreeEditorWindow>();
        window.titleContent = new GUIContent("Active Skill Tree");
        window.minSize = new Vector2(900f, 560f);
        return window;
    }

    public static void Open(SkillUpgradeTreeDefinition tree)
    {
        ActiveSkillTreeEditorWindow window = Open();
        window.SetTree(tree);
        window.Focus();
    }

    void OnEnable()
    {
        BuildUi();
        Undo.undoRedoPerformed += ReloadGraph;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= ReloadGraph;
        if (_dirty && _tree != null)
        {
            if (EditorUtility.DisplayDialog(
                    "Unsaved Active Skill Tree",
                    $"Save changes to '{_tree.name}' before closing?",
                    "Save",
                    "Discard"))
                SaveTree();
            else
                RevertTreeFromDisk();
        }
    }

    void BuildUi()
    {
        rootVisualElement.Clear();

        var toolbar = new Toolbar();
        _assetField = new ObjectField("Tree")
        {
            objectType = typeof(SkillUpgradeTreeDefinition),
            allowSceneObjects = false,
            value = _tree,
        };
        _assetField.RegisterValueChangedCallback(evt => SetTree(evt.newValue as SkillUpgradeTreeDefinition));
        toolbar.Add(_assetField);
        toolbar.Add(new ToolbarButton(CreateTree) { text = "New" });
        toolbar.Add(new ToolbarButton(AddNode) { text = "Add Node" });
        toolbar.Add(new ToolbarButton(DuplicateSelected) { text = "Duplicate" });
        toolbar.Add(new ToolbarButton(() => _graph?.FrameAll()) { text = "Frame All" });
        toolbar.Add(new ToolbarButton(ValidateTree) { text = "Validate" });
        toolbar.Add(new ToolbarButton(SaveTree) { text = "Save" });
        rootVisualElement.Add(toolbar);

        var split = new TwoPaneSplitView(0, 680f, TwoPaneSplitViewOrientation.Horizontal);
        _graph = new SkillTreeGraphView();
        _graph.NodeSelected = SelectNode;
        _graph.GraphMutated = MarkDirty;
        split.Add(_graph);

        _inspector = new IMGUIContainer(DrawInspector);
        _inspector.style.minWidth = 280f;
        split.Add(_inspector);
        rootVisualElement.Add(split);

        if (_tree != null)
            SetTree(_tree);
    }

    void SetTree(SkillUpgradeTreeDefinition tree)
    {
        if (_dirty && _tree != null && tree != _tree)
        {
            if (EditorUtility.DisplayDialog("Unsaved Changes", $"Save changes to '{_tree.name}'?", "Save", "Discard"))
                SaveTree();
            else
                RevertTreeFromDisk();
        }

        _tree = tree;
        _selectedNode = null;
        _serializedTree = _tree != null ? new SerializedObject(_tree) : null;
        _dirty = false;
        _issuesDirty = true;
        if (_assetField != null)
            _assetField.SetValueWithoutNotify(_tree);
        _graph?.Load(_tree);
        _inspector?.MarkDirtyRepaint();
    }

    void CreateTree()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Active Skill Tree",
            "NewActiveSkillTree",
            "asset",
            "Choose where to save the tree asset.");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var tree = CreateInstance<SkillUpgradeTreeDefinition>();
        tree.treeId = System.IO.Path.GetFileNameWithoutExtension(path);
        tree.displayName = tree.treeId;
        AssetDatabase.CreateAsset(tree, path);
        AssetDatabase.SaveAssets();
        SetTree(tree);
    }

    void AddNode()
    {
        if (_tree == null)
            return;

        Undo.RecordObject(_tree, "Add Active Skill Node");
        _tree.nodes ??= new List<SkillUpgradeNodeData>();
        var node = new SkillUpgradeNodeData
        {
            nodeId = CreateUniqueNodeId("node"),
            displayName = "Upgrade Node",
            uiPosition = _graph != null ? _graph.GetViewCenter() : Vector2.zero,
        };
        _tree.nodes.Add(node);
        MarkDirty();
        _graph.Load(_tree);
        _graph.SelectData(node);
    }

    void DuplicateSelected()
    {
        if (_tree == null || _selectedNode == null)
            return;

        Undo.RecordObject(_tree, "Duplicate Active Skill Node");
        string json = JsonUtility.ToJson(_selectedNode);
        var duplicate = new SkillUpgradeNodeData();
        JsonUtility.FromJsonOverwrite(json, duplicate);
        duplicate.nodeId = CreateUniqueNodeId(_selectedNode.RuntimeNodeId + "_copy");
        duplicate.uiPosition += new Vector2(40f, 40f);
        duplicate.requiredNodeIds = duplicate.requiredNodeIds != null
            ? new List<string>(duplicate.requiredNodeIds)
            : new List<string>();
        _tree.nodes.Add(duplicate);
        MarkDirty();
        _graph.Load(_tree);
        _graph.SelectData(duplicate);
    }

    void SelectNode(SkillUpgradeNodeData node)
    {
        _selectedNode = node;
        _inspector?.MarkDirtyRepaint();
    }

    void DrawInspector()
    {
        if (_tree == null || _serializedTree == null)
        {
            EditorGUILayout.HelpBox("Select or create an Active Skill Tree.", MessageType.Info);
            return;
        }

        _serializedTree.Update();
        if (_selectedNode == null)
        {
            EditorGUILayout.PropertyField(_serializedTree.FindProperty("treeId"));
            EditorGUILayout.PropertyField(_serializedTree.FindProperty("displayName"));
            EditorGUILayout.PropertyField(_serializedTree.FindProperty("description"));
            // ApplyModifiedProperties returns true only when a value actually changed, so
            // foldout toggles (which set GUI.changed) no longer count as edits.
            if (_serializedTree.ApplyModifiedProperties())
            {
                MarkDirty();
                _issuesDirty = true;
            }
            return;
        }

        int nodeIndex = _tree.nodes != null ? _tree.nodes.IndexOf(_selectedNode) : -1;
        SerializedProperty nodesProperty = _serializedTree.FindProperty("nodes");
        if (nodeIndex < 0 || nodeIndex >= nodesProperty.arraySize)
            return;

        SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(nodeIndex);
        EditorGUILayout.PropertyField(nodeProperty, true);
        if (_serializedTree.ApplyModifiedProperties())
        {
            MarkDirty();
            _issuesDirty = true;
            _graph.RefreshTitles();
        }

        DrawNodeIssues();
    }

    void DrawNodeIssues()
    {
        if (_issuesDirty)
        {
            _cachedIssues = SkillUpgradeTreeValidator.Validate(_tree);
            _issuesDirty = false;
        }

        if (_cachedIssues.Count == 0)
            return;

        string marker = $"'{_selectedNode.RuntimeNodeId}'";
        int otherCount = 0;
        EditorGUILayout.Space();
        for (int i = 0; i < _cachedIssues.Count; i++)
        {
            SkillUpgradeValidationIssue issue = _cachedIssues[i];
            if (!issue.Message.Contains(marker))
            {
                otherCount++;
                continue;
            }

            MessageType messageType = issue.Severity == SkillUpgradeValidationSeverity.Error
                ? MessageType.Error
                : MessageType.Warning;
            EditorGUILayout.HelpBox(issue.Message, messageType);
        }

        if (otherCount > 0)
            EditorGUILayout.HelpBox($"{otherCount} other issue(s) in this tree.", MessageType.None);
    }

    void ValidateTree()
    {
        if (_tree == null)
            return;

        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(_tree);
        if (issues.Count == 0)
        {
            Debug.Log($"[ActiveSkillTree] '{_tree.name}' is valid.", _tree);
            return;
        }

        for (int i = 0; i < issues.Count; i++)
        {
            SkillUpgradeValidationIssue issue = issues[i];
            if (issue.Severity == SkillUpgradeValidationSeverity.Error)
                Debug.LogError($"[ActiveSkillTree] {issue.Message}", _tree);
            else
                Debug.LogWarning($"[ActiveSkillTree] {issue.Message}", _tree);
        }
    }

    void SaveTree()
    {
        if (_tree == null)
            return;

        EditorUtility.SetDirty(_tree);
        AssetDatabase.SaveAssetIfDirty(_tree);
        _dirty = false;
    }

    void RevertTreeFromDisk()
    {
        if (_tree == null)
            return;

        string path = AssetDatabase.GetAssetPath(_tree);
        _dirty = false;
        if (!string.IsNullOrWhiteSpace(path))
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    void MarkDirty()
    {
        if (_tree == null)
            return;

        EditorUtility.SetDirty(_tree);
        _dirty = true;
    }

    void ReloadGraph()
    {
        if (_tree == null)
            return;

        _serializedTree = new SerializedObject(_tree);
        _issuesDirty = true;
        _graph?.Load(_tree);
        _inspector?.MarkDirtyRepaint();
    }

    string CreateUniqueNodeId(string seed)
    {
        string baseId = string.IsNullOrWhiteSpace(seed) ? "node" : seed.Trim();
        string candidate = baseId;
        int suffix = 2;
        while (_tree.nodes != null && _tree.nodes.Exists(node =>
                   node != null && string.Equals(node.RuntimeNodeId, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    internal sealed class SkillTreeGraphView : GraphView
    {
        readonly Dictionary<string, SkillGraphNode> _nodes = new(StringComparer.Ordinal);
        SkillUpgradeTreeDefinition _tree;
        bool _loading;

        public Action<SkillUpgradeNodeData> NodeSelected;
        public Action GraphMutated;

        public SkillTreeGraphView()
        {
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
            graphViewChanged = HandleGraphChange;
        }

        public void Load(SkillUpgradeTreeDefinition tree)
        {
            _loading = true;
            _tree = tree;
            DeleteElements(graphElements.ToList());
            _nodes.Clear();
            if (_tree == null || _tree.nodes == null)
            {
                _loading = false;
                return;
            }

            for (int i = 0; i < _tree.nodes.Count; i++)
            {
                SkillUpgradeNodeData data = _tree.nodes[i];
                if (data == null || string.IsNullOrWhiteSpace(data.RuntimeNodeId))
                    continue;

                var node = new SkillGraphNode(data);
                node.RegisterCallback<MouseDownEvent>(_ => NodeSelected?.Invoke(data));
                node.RefreshLayout();
                AddElement(node);
                _nodes[data.RuntimeNodeId] = node;
            }

            for (int i = 0; i < _tree.nodes.Count; i++)
            {
                SkillUpgradeNodeData childData = _tree.nodes[i];
                if (childData == null || childData.requiredNodeIds == null ||
                    !_nodes.TryGetValue(childData.RuntimeNodeId, out SkillGraphNode child))
                {
                    continue;
                }

                for (int dependencyIndex = 0; dependencyIndex < childData.requiredNodeIds.Count; dependencyIndex++)
                {
                    string parentId = childData.requiredNodeIds[dependencyIndex];
                    if (string.IsNullOrWhiteSpace(parentId) || !_nodes.TryGetValue(parentId.Trim(), out SkillGraphNode parent))
                        continue;

                    AddElement(parent.Output.ConnectTo(child.Input));
                }
            }
            _loading = false;
        }

        public Vector2 GetViewCenter()
        {
            return contentViewContainer.WorldToLocal(worldBound.center);
        }

        public void SelectData(SkillUpgradeNodeData data)
        {
            ClearSelection();
            if (data != null && _nodes.TryGetValue(data.RuntimeNodeId, out SkillGraphNode node))
            {
                AddToSelection(node);
                FrameSelection();
                NodeSelected?.Invoke(data);
            }
        }

        public void RefreshTitles()
        {
            foreach (SkillGraphNode node in _nodes.Values)
            {
                node.RefreshTitle();
                node.RefreshIcon();
                node.RefreshLayout();
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            if (startPort == null)
                return new List<Port>();

            return ports.ToList().Where(candidate =>
                candidate != startPort &&
                candidate.node != startPort.node &&
                candidate.direction != startPort.direction &&
                candidate.portType == startPort.portType &&
                !AreAlreadyConnected(startPort, candidate)).ToList();
        }

        static bool AreAlreadyConnected(Port first, Port second)
        {
            Port output = first.direction == Direction.Output ? first : second;
            Port input = first.direction == Direction.Input ? first : second;
            return output.connections.Any(edge => edge.input == input);
        }

        internal GraphViewChange HandleGraphChange(GraphViewChange change)
        {
            if (_tree == null || _loading)
                return change;

            bool mutated = false;
            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(_tree, "Move Active Skill Nodes");
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    if (change.movedElements[i] is SkillGraphNode node)
                        node.WritePositionBack();
                }
                mutated = true;
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Undo.RecordObject(_tree, "Connect Active Skill Nodes");
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    Edge edge = change.edgesToCreate[i];
                    if (edge.output?.node is not SkillGraphNode parent || edge.input?.node is not SkillGraphNode child)
                        continue;

                    child.Data.requiredNodeIds ??= new List<string>();
                    if (!child.Data.requiredNodeIds.Contains(parent.Data.RuntimeNodeId))
                        child.Data.requiredNodeIds.Add(parent.Data.RuntimeNodeId);
                }
                mutated = true;
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(_tree, "Delete Active Skill Graph Elements");
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    GraphElement element = change.elementsToRemove[i];
                    if (element is Edge edge && edge.output?.node is SkillGraphNode parent && edge.input?.node is SkillGraphNode child)
                    {
                        child.Data.requiredNodeIds?.Remove(parent.Data.RuntimeNodeId);
                    }
                    else if (element is SkillGraphNode node)
                    {
                        _tree.nodes.Remove(node.Data);
                        foreach (SkillUpgradeNodeData candidate in _tree.nodes)
                            candidate?.requiredNodeIds?.Remove(node.Data.RuntimeNodeId);
                    }
                }
                mutated = true;
            }

            if (mutated)
                GraphMutated?.Invoke();
            return change;
        }

    }

    sealed class SkillGraphNode : Node
    {
        static readonly Vector2 BaseSize = new(220f, 120f);
        const float BaseIconSize = 72f;

        readonly Image _icon;

        public SkillGraphNode(SkillUpgradeNodeData data)
        {
            Data = data;
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "Requires";
            inputContainer.Add(Input);
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = "Unlocks";
            outputContainer.Add(Output);

            _icon = new Image
            {
                name = "skill-node-icon",
                scaleMode = ScaleMode.ScaleToFit,
            };
            _icon.style.alignSelf = Align.Center;
            _icon.style.marginTop = 4f;
            _icon.style.marginBottom = 4f;
            extensionContainer.Add(_icon);

            RefreshTitle();
            RefreshIcon();
            RefreshExpandedState();
            RefreshPorts();
        }

        public SkillUpgradeNodeData Data { get; }
        public Port Input { get; }
        public Port Output { get; }

        // uiPosition is authored as a centre point, converted to a top-left corner with this
        // size. GetPosition() reports the *resolved* node size instead, so both directions must
        // use AuthoredSize or every relayout nudges the node by half the size difference.
        Vector2 AuthoredSize => BaseSize * Data.ResolvedVisualScale;

        public override void SetPosition(Rect newPos)
        {
            userData = newPos;
            base.SetPosition(newPos);
        }

        public void WritePositionBack()
        {
            Data.uiPosition = GetPosition().position + AuthoredSize * 0.5f;
        }

        public void RefreshTitle()
        {
            title = string.IsNullOrWhiteSpace(Data.ResolvedDisplayName)
                ? Data.RuntimeNodeId
                : Data.ResolvedDisplayName;
        }

        public void RefreshIcon()
        {
            _icon.sprite = Data.icon;
            _icon.style.display = Data.icon != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RefreshLayout()
        {
            float scale = Data.ResolvedVisualScale;
            Vector2 size = AuthoredSize;
            SetPosition(new Rect(Data.uiPosition - size * 0.5f, size));
            _icon.style.width = BaseIconSize * scale;
            _icon.style.height = BaseIconSize * scale;
        }
    }
}

public static class ActiveSkillTreeAssetOpener
{
    [OnOpenAsset]
    public static bool OpenAsset(int instanceId, int line)
    {
        if (EditorUtility.InstanceIDToObject(instanceId) is not SkillUpgradeTreeDefinition tree)
            return false;

        ActiveSkillTreeEditorWindow.Open(tree);
        return true;
    }
}
