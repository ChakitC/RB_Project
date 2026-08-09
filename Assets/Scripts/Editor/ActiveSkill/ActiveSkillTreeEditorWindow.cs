using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditorInternal;
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
    List<SkillDefinitionBase> _cachedOwners;

    // Composite step panel state. Kept separate from the tree's own _dirty/save lifecycle
    // because it edits a different asset (the owning skill), on its own save/revert path.
    bool _showSkillSteps;
    CompositeSkillPayloadDef _stepsComposite;
    SerializedObject _serializedComposite;
    bool _skillDirty;
    SkillGemDefinition _skillDirtyTarget;
    Type _pendingStepType;
    Type _pendingStepPayloadType;
    static List<Type> _cachedStepTypes;
    readonly HashSet<SkillEffectStep> _expandedSteps = new();

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

        PromptSaveSkillSteps();
    }

    void PromptSaveSkillSteps()
    {
        if (!_skillDirty || _skillDirtyTarget == null)
            return;

        if (EditorUtility.DisplayDialog(
                "Unsaved Skill Steps",
                $"Save composite step changes to '{_skillDirtyTarget.name}' before closing?",
                "Save",
                "Discard"))
            AssetDatabase.SaveAssetIfDirty(_skillDirtyTarget);
        else
            RevertSkillFromDisk();

        _skillDirty = false;
        _skillDirtyTarget = null;
    }

    void RevertSkillFromDisk()
    {
        if (_skillDirtyTarget == null)
            return;

        string path = AssetDatabase.GetAssetPath(_skillDirtyTarget);
        if (!string.IsNullOrWhiteSpace(path))
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        // The revert-by-reimport above can replace the composite's in-memory instance; drop the
        // cached SerializedObject so the panel re-resolves everything from skill.payload.
        ClearStepsCache();
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

        _showSkillSteps = false;
        var skillStepsToggle = new ToolbarToggle { text = "Skill Steps" };
        skillStepsToggle.RegisterValueChangedCallback(evt =>
        {
            _showSkillSteps = evt.newValue;
            _inspector?.MarkDirtyRepaint();
        });
        toolbar.Add(skillStepsToggle);
        rootVisualElement.Add(toolbar);

        var split = new TwoPaneSplitView(0, 680f, TwoPaneSplitViewOrientation.Horizontal);
        _graph = new SkillTreeGraphView();
        _graph.NodeSelected = SelectNode;
        _graph.GraphMutated = MarkDirty;
        _graph.AddNodeRequested = AddNode;
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

        if (tree != _tree)
            PromptSaveSkillSteps();

        _tree = tree;
        _selectedNode = null;
        _serializedTree = _tree != null ? new SerializedObject(_tree) : null;
        _dirty = false;
        _issuesDirty = true;
        _cachedOwners = null;
        ClearStepsCache();
        _pendingStepType = null;
        _pendingStepPayloadType = null;
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

    void AddNode() => AddNode(_graph != null ? _graph.GetViewCenter() : Vector2.zero, null);

    void AddNode(Vector2 position, string grantedUpgradeId)
    {
        if (_tree == null)
            return;

        Undo.RecordObject(_tree, "Add Active Skill Node");
        _tree.nodes ??= new List<SkillUpgradeNodeData>();
        string seed = string.IsNullOrWhiteSpace(grantedUpgradeId) ? "node" : grantedUpgradeId;
        var node = new SkillUpgradeNodeData
        {
            nodeId = CreateUniqueNodeId(seed),
            displayName = string.IsNullOrWhiteSpace(grantedUpgradeId) ? "Upgrade Node" : grantedUpgradeId,
            uiPosition = position,
        };
        if (!string.IsNullOrWhiteSpace(grantedUpgradeId))
            node.grantedUpgradeIds.Add(grantedUpgradeId);
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

        if (_showSkillSteps)
        {
            DrawSkillStepsPanel();
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

    // FindOwningAssets scans every SkillDefinitionBase/CharacterStats asset in the project, so
    // callers that recompute per repaint/keystroke (validation cache, the steps panel) must share
    // this cache instead of re-scanning each time.
    List<SkillDefinitionBase> ResolveOwners()
    {
        if (_cachedOwners == null && _tree != null)
            _cachedOwners = new List<SkillDefinitionBase>(SkillUpgradeTreeValidator.FindOwningAssets(_tree));
        return _cachedOwners ?? new List<SkillDefinitionBase>();
    }

    void DrawNodeIssues()
    {
        if (_issuesDirty)
        {
            _cachedIssues = SkillUpgradeTreeValidator.Validate(_tree, ResolveOwners());
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

        // Explicit Validate click: refresh the owner cache so renamed/added skill assets are seen.
        _cachedOwners = new List<SkillDefinitionBase>(SkillUpgradeTreeValidator.FindOwningAssets(_tree));
        List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(_tree, _cachedOwners);
        _cachedIssues = issues;
        _issuesDirty = false;
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
        if (_tree != null)
        {
            EditorUtility.SetDirty(_tree);
            AssetDatabase.SaveAssetIfDirty(_tree);
            _dirty = false;
        }

        if (_skillDirty && _skillDirtyTarget != null)
        {
            AssetDatabase.SaveAssetIfDirty(_skillDirtyTarget);
            _skillDirty = false;
            _skillDirtyTarget = null;
        }
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

        // An undo/redo can destroy or replace the composite the steps panel was targeting;
        // drop the cache so DrawSkillStepsPanel re-resolves it from the owning skill's payload.
        ClearStepsCache();

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

    #region Skill Steps Panel

    void DrawSkillStepsPanel()
    {
        List<SkillDefinitionBase> owners = ResolveOwners();
        if (owners.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No skill or passive currently points its Upgrade Tree at this asset.",
                MessageType.Warning);
            return;
        }

        if (owners.Count > 1)
        {
            string names = string.Join(", ", owners.Select(o => o.name));
            EditorGUILayout.HelpBox(
                $"This tree is shared by {owners.Count} skills/passives ({names}). Composite step " +
                "editing is disabled here to avoid editing the wrong owner's steps -- open the " +
                "skill asset directly instead.",
                MessageType.Warning);
            return;
        }

        if (owners[0] is not SkillGemDefinition skill)
        {
            EditorGUILayout.HelpBox(
                $"This tree is owned by '{owners[0].name}', a Passive definition. Passives have " +
                "no execution steps to author.",
                MessageType.Info);
            return;
        }

        if (skill.payload == null)
        {
            DrawSkillStepsBlocked(skill, "This skill has no execution payload yet. Create one in the skill Inspector.");
            return;
        }

        if (!SkillPayloadAssetUtility.IsEmbedded(skill, skill.payload))
        {
            DrawSkillStepsBlocked(skill, "This skill's execution payload is not embedded correctly. Fix it in the skill Inspector.");
            return;
        }

        if (skill.payload is not CompositeSkillPayloadDef composite)
        {
            DrawSkillStepsBlocked(skill,
                $"This skill's execution is a single {SkillPayloadAssetUtility.GetPayloadDisplayName(skill.payload.GetType())}. " +
                "Change its Execution Type to Composite in the skill Inspector to author steps.");
            return;
        }

        DrawCompositeSteps(skill, composite);
    }

    void DrawSkillStepsBlocked(SkillGemDefinition skill, string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Info);
        if (GUILayout.Button("Select Skill Asset"))
        {
            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }
    }

    void EnsureStepsSerializedObject(CompositeSkillPayloadDef composite)
    {
        if (_stepsComposite == composite && _serializedComposite != null)
            return;

        _stepsComposite = composite;
        _serializedComposite = composite != null ? new SerializedObject(composite) : null;

        // A freshly constructed SerializedObject's first Update() can pull in load-time schema
        // normalization (e.g. legacy managed-reference data upgrading to the current class
        // shape -- the same phenomenon that silently added ConditionalStatus.spec blocks to
        // Aires_Skill_3.asset on an earlier resave). ApplyModifiedProperties() then reports that
        // as a real change even though nothing was edited. Absorb it once here, right after
        // acquiring the SerializedObject, so merely opening/viewing the panel after a domain
        // reload doesn't spuriously mark the skill dirty and trigger the save-on-close prompt.
        if (_serializedComposite != null)
        {
            _serializedComposite.Update();
            _serializedComposite.ApplyModifiedProperties();
        }
    }

    // Mutations can invalidate the SerializedObject's managed-reference view, so every structural
    // change drops the cache rather than trying to track step identity churn.
    void ClearStepsCache()
    {
        _stepsComposite = null;
        _serializedComposite = null;
        _expandedSteps.Clear();
    }

    void DrawCompositeSteps(SkillGemDefinition skill, CompositeSkillPayloadDef composite)
    {
        EnsureStepsSerializedObject(composite);
        _serializedComposite.Update();
        SerializedProperty stepsProp = _serializedComposite.FindProperty("steps");

        EditorGUILayout.LabelField($"Composite Steps ({stepsProp.arraySize})", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Add, reorder, remove, and gate steps here. Expand a step to edit its own fields " +
            "(e.g. HealArea's target/status lists). A PayloadStep's wrapped payload fields " +
            "(prefabs, numbers) are edited on the skill asset -- use the buttons below to jump " +
            "straight to it.",
            MessageType.None);

        if (GUILayout.Button("Select Skill Asset"))
        {
            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }

        EditorGUILayout.Space(4f);

        int stepCount = stepsProp.arraySize;
        for (int i = 0; i < stepCount; i++)
            DrawStepRow(skill, composite, stepsProp, i, stepCount);

        if (_serializedComposite.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            _skillDirty = true;
            _skillDirtyTarget = skill;
        }

        EditorGUILayout.Space(8f);
        DrawAddStepRow(skill, composite);
    }

    void DrawStepRow(
        SkillGemDefinition skill,
        CompositeSkillPayloadDef composite,
        SerializedProperty stepsProp,
        int index,
        int count)
    {
        SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(index);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var stepInstance = stepProp.managedReferenceValue as SkillEffectStep;
        if (stepInstance == null)
        {
            EditorGUILayout.HelpBox(
                $"Step {index} has no data (missing type or an aborted add). Remove it.",
                MessageType.Error);
        }
        else
        {
            string typeLabel = GetStepDisplayName(stepInstance.GetType());
            var payloadStep = stepInstance as PayloadStep;
            string extra = string.Empty;
            if (payloadStep != null)
            {
                extra = payloadStep.Payload != null
                    ? $" -> {SkillPayloadAssetUtility.GetPayloadDisplayName(payloadStep.Payload.GetType())}"
                    : " -> <no payload assigned>";
            }
            else if (stepInstance is HealAreaStep)
            {
                // Two HealAreaStep entries with the same collapsed label ("HealArea") are
                // otherwise indistinguishable without expanding both -- show which target they
                // heal right in the header, same as PayloadStep shows its wrapped payload type.
                SerializedProperty targetProp = stepProp.FindPropertyRelative("target");
                if (targetProp != null)
                    extra = $" ({(HealTargetMode)targetProp.enumValueIndex})";
            }

            bool expanded = _expandedSteps.Contains(stepInstance);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, $"Step {index}: {typeLabel}{extra}", true);
            if (nextExpanded != expanded)
            {
                if (nextExpanded)
                    _expandedSteps.Add(stepInstance);
                else
                    _expandedSteps.Remove(stepInstance);
                expanded = nextExpanded;
                // Nudge IMGUIContainer to re-measure promptly on the frame the content height
                // actually changes, rather than waiting for whatever next triggers a repaint.
                _inspector?.MarkDirtyRepaint();
            }

            if (!expanded)
            {
                SerializedProperty idProp = stepProp.FindPropertyRelative("requiredUpgradeId");
                if (idProp != null)
                    EditorGUILayout.PropertyField(idProp, new GUIContent("Required Upgrade Id"));
            }
            else
            {
                // Draws every visible field on the step, including requiredUpgradeId -- the
                // HideInInspector legacy fields on HealAreaStep.ConditionalStatus stay hidden
                // because PropertyField already honors that attribute.
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(stepProp, true);
                EditorGUI.indentLevel--;

                if (payloadStep != null)
                {
                    EditorGUILayout.Space(2f);
                    if (payloadStep.Payload == null)
                    {
                        EditorGUILayout.HelpBox("No payload assigned to this step.", MessageType.Warning);
                    }
                    else
                    {
                        var issues = new List<string>();
                        payloadStep.Payload.CollectValidationIssues(issues);
                        if (issues.Count > 0)
                            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Error);

                        // Deep payload fields (prefabs, numbers, status lists) are edited on the
                        // skill asset, not inline here -- a nested Editor.OnInspectorGUI() call
                        // drawn through an IMGUIContainer trips a still-open Unity engine bug
                        // (ScrollView resets its scroll offset when a sibling IMGUIContainer's
                        // measured height changes, which happens on every repaint while the
                        // window has focus). Point at the payload instead of embedding it.
                        SkillPayloadDef payload = payloadStep.Payload;
                        if (GUILayout.Button($"Select '{SkillPayloadAssetUtility.GetPayloadDisplayName(payload.GetType())}' Payload"))
                        {
                            Selection.activeObject = payload;
                            EditorGUIUtility.PingObject(payload);
                        }
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("Up", GUILayout.Width(40f)))
                {
                    SkillGemDefinition capturedSkill = skill;
                    CompositeSkillPayloadDef capturedComposite = composite;
                    int capturedIndex = index;
                    EditorApplication.delayCall += () => MoveStep(capturedSkill, capturedComposite, capturedIndex, -1);
                }
            }
            using (new EditorGUI.DisabledScope(index == count - 1))
            {
                if (GUILayout.Button("Down", GUILayout.Width(50f)))
                {
                    SkillGemDefinition capturedSkill = skill;
                    CompositeSkillPayloadDef capturedComposite = composite;
                    int capturedIndex = index;
                    EditorApplication.delayCall += () => MoveStep(capturedSkill, capturedComposite, capturedIndex, 1);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                SkillGemDefinition capturedSkill = skill;
                CompositeSkillPayloadDef capturedComposite = composite;
                int capturedIndex = index;
                EditorApplication.delayCall += () => RemoveStep(capturedSkill, capturedComposite, capturedIndex);
            }
        }

        EditorGUILayout.EndVertical();
    }

    void DrawAddStepRow(SkillGemDefinition skill, CompositeSkillPayloadDef composite)
    {
        List<Type> stepTypes = GetStepTypes();
        if (stepTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("No concrete SkillEffectStep types were found.", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Add Step", EditorStyles.boldLabel);

        _pendingStepType ??= stepTypes[0];
        int stepIndex = Mathf.Max(0, stepTypes.IndexOf(_pendingStepType));
        string[] stepNames = stepTypes.Select(GetStepDisplayName).ToArray();
        int nextStepIndex = EditorGUILayout.Popup("Step Type", stepIndex, stepNames);
        _pendingStepType = stepTypes[nextStepIndex];

        Type payloadTypeToCreate = null;
        bool needsPayload = typeof(PayloadStep).IsAssignableFrom(_pendingStepType);
        if (needsPayload)
        {
            List<Type> payloadTypes = SkillPayloadAssetUtility.GetPayloadTypes()
                .Where(t => t != typeof(CompositeSkillPayloadDef))
                .ToList();
            if (payloadTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No concrete SkillPayloadDef types were found.", MessageType.Error);
            }
            else
            {
                _pendingStepPayloadType ??= payloadTypes[0];
                int payloadIndex = Mathf.Max(0, payloadTypes.IndexOf(_pendingStepPayloadType));
                string[] payloadNames = payloadTypes.Select(SkillPayloadAssetUtility.GetPayloadDisplayName).ToArray();
                int nextPayloadIndex = EditorGUILayout.Popup("Payload Type", payloadIndex, payloadNames);
                _pendingStepPayloadType = payloadTypes[nextPayloadIndex];
                payloadTypeToCreate = _pendingStepPayloadType;
            }
        }

        using (new EditorGUI.DisabledScope(needsPayload && payloadTypeToCreate == null))
        {
            if (GUILayout.Button("Add"))
            {
                SkillGemDefinition capturedSkill = skill;
                Type capturedStepType = _pendingStepType;
                Type capturedPayloadType = payloadTypeToCreate;
                EditorApplication.delayCall += () => AddStep(capturedSkill, capturedStepType, capturedPayloadType);
            }
        }

        EditorGUILayout.EndVertical();
    }

    void AddStep(SkillGemDefinition skill, Type stepType, Type payloadType)
    {
        if (skill == null || skill.payload is not CompositeSkillPayloadDef composite || stepType == null)
            return;

        var so = new SerializedObject(composite);
        so.Update();
        SerializedProperty stepsProp = so.FindProperty("steps");
        int index = stepsProp.arraySize;
        stepsProp.InsertArrayElementAtIndex(index);
        // InsertArrayElementAtIndex on a managed-reference array aliases the previous element's
        // reference (or leaves null on an empty array) -- it does NOT create a fresh instance.
        // Overwriting managedReferenceValue is mandatory, not defensive.
        stepsProp.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(stepType);
        so.ApplyModifiedProperties();

        if (payloadType != null)
        {
            SkillPayloadDef newPayload = SkillPayloadAssetUtility.CreateEmbeddedStepPayload(skill, composite, index, payloadType, null);
            if (newPayload == null)
                Debug.LogError($"[ActiveSkillTree] Failed to create embedded payload '{payloadType.FullName}' for the new step.", skill);

            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            _skillDirty = true;
            _skillDirtyTarget = skill;
        }
        else
        {
            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(skill);
            _skillDirty = true;
            _skillDirtyTarget = skill;
        }

        InternalEditorUtility.RepaintAllViews();
        ClearStepsCache();
        _inspector?.MarkDirtyRepaint();
    }

    void RemoveStep(SkillGemDefinition skill, CompositeSkillPayloadDef composite, int index)
    {
        if (skill == null || composite == null)
            return;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        if (index < 0 || index >= steps.Count)
            return;

        SkillPayloadDef wrapped = steps[index] is PayloadStep payloadStep ? payloadStep.Payload : null;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Remove Composite Step");
        int group = Undo.GetCurrentGroup();

        var so = new SerializedObject(composite);
        so.Update();
        SerializedProperty stepsProp = so.FindProperty("steps");
        // Order matters: delete the array element BEFORE destroying the wrapped payload object.
        // Destroying first would leave the managed reference pointing at a destroyed object, and
        // if the array delete then failed, the composite would carry a PayloadStep with a
        // {fileID: 0} payload -- a blocking validation error.
        int before = stepsProp.arraySize;
        stepsProp.DeleteArrayElementAtIndex(index);
        if (stepsProp.arraySize == before)
            stepsProp.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();

        if (wrapped != null &&
            SkillPayloadAssetUtility.IsEmbedded(skill, wrapped) &&
            !SkillPayloadAssetUtility.IsReferencedByComposite(composite, wrapped))
        {
            Undo.DestroyObjectImmediate(wrapped);
        }

        EditorUtility.SetDirty(composite);
        EditorUtility.SetDirty(skill);
        _skillDirty = true;
        _skillDirtyTarget = skill;

        Undo.CollapseUndoOperations(group);
        InternalEditorUtility.RepaintAllViews();

        ClearStepsCache();
        _inspector?.MarkDirtyRepaint();
    }

    void MoveStep(SkillGemDefinition skill, CompositeSkillPayloadDef composite, int index, int delta)
    {
        if (skill == null || composite == null)
            return;

        IReadOnlyList<SkillEffectStep> steps = composite.Steps;
        int target = index + delta;
        if (index < 0 || index >= steps.Count || target < 0 || target >= steps.Count)
            return;

        var so = new SerializedObject(composite);
        so.Update();
        SerializedProperty stepsProp = so.FindProperty("steps");
        stepsProp.MoveArrayElement(index, target);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(composite);
        EditorUtility.SetDirty(skill);
        _skillDirty = true;
        _skillDirtyTarget = skill;

        InternalEditorUtility.RepaintAllViews();
        ClearStepsCache();
        _inspector?.MarkDirtyRepaint();
    }

    static List<Type> GetStepTypes()
    {
        if (_cachedStepTypes != null)
            return _cachedStepTypes;

        _cachedStepTypes = TypeCache.GetTypesDerivedFrom<SkillEffectStep>()
            .Where(type => type != null && !type.IsAbstract && !type.IsGenericTypeDefinition)
            .OrderBy(GetStepDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return _cachedStepTypes;
    }

    static string GetStepDisplayName(Type stepType)
    {
        if (stepType == null)
            return "None";

        string name = stepType.Name;
        const string suffix = "Step";
        if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - suffix.Length);

        return name;
    }

    #endregion

    internal sealed class SkillTreeGraphView : GraphView
    {
        readonly Dictionary<string, SkillGraphNode> _nodes = new(StringComparer.Ordinal);
        SkillUpgradeTreeDefinition _tree;
        bool _loading;

        public Action<SkillUpgradeNodeData> NodeSelected;
        public Action GraphMutated;

        // (position, grantedUpgradeId) -- grantedUpgradeId is null/blank for a blank node.
        public Action<Vector2, string> AddNodeRequested;

        public SkillTreeGraphView()
        {
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
            graphViewChanged = HandleGraphChange;
            RegisterCallback<ContextualMenuPopulateEvent>(BuildAddNodeMenu);
        }

        void BuildAddNodeMenu(ContextualMenuPopulateEvent evt)
        {
            if (_tree == null || AddNodeRequested == null)
                return;

            // Skip when right-clicking an existing node/port so its own default menu (Cut,
            // Copy, Delete, ...) isn't cluttered with node-creation entries.
            if (evt.target is VisualElement target && target.GetFirstAncestorOfType<Node>() != null)
                return;

            Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);

            evt.menu.AppendAction("Add Node/Blank", _ => AddNodeRequested(graphPosition, null));

            List<string> ungranted = SkillUpgradeTreeValidator.GetUngrantedUpgradeIds(_tree);
            if (ungranted.Count > 0)
            {
                evt.menu.AppendSeparator("Add Node/");
                for (int i = 0; i < ungranted.Count; i++)
                {
                    string upgradeId = ungranted[i];
                    evt.menu.AppendAction($"Add Node/Grants {upgradeId}", _ => AddNodeRequested(graphPosition, upgradeId));
                }
            }
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

        public Rect AuthoredPosition { get; private set; }

        // uiPosition is authored as a centre point, converted to a top-left corner using this
        // size. The rect a drag hands to SetPosition carries the node's *resolved* size instead,
        // so the write-back must re-derive the centre from AuthoredSize -- otherwise every
        // relayout nudges the node by half the difference between the two sizes.
        Vector2 AuthoredSize => BaseSize * Data.ResolvedVisualScale;

        public override void SetPosition(Rect newPos)
        {
            AuthoredPosition = newPos;
            userData = newPos;
            base.SetPosition(newPos);
        }

        public void WritePositionBack()
        {
            Data.uiPosition = AuthoredPosition.position + AuthoredSize * 0.5f;
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
