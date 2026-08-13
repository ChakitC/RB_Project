using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Dedicated Create/Edit window for node-centric ability authoring (plan section 13). Never
// writes to the skill/tree assets directly -- every commit goes through
// NodeAbilityAuthoringService so Undo, validation, and the smoke tests share one code path.
//
// Duplicate is intentionally not a wizard mode: NodeAbilityAuthoringService.DuplicateNodeAbility
// already clones a source step's payload and mints a new id server-side with no fields to review,
// so the node ability card (Phase 5) calls it directly as a one-click action.
//
// This window does not save the skill/tree assets on commit -- like
// ActiveSkillStatusEffectWizardWindow, Save/Discard belongs to the surrounding
// ActiveSkillTreeEditorWindow (plan section 15.5). Until that Save happens, a newly created
// ability's embedded payload will not yet report as a sub-asset through AssetDatabase.IsSubAsset.
public sealed class ActiveSkillAbilityWizardWindow : EditorWindow
{
    const float MinWidth = 560f;
    const float MinHeight = 620f;

    SkillUpgradeTreeDefinition _tree;
    SkillGemDefinition _skill;
    SkillUpgradeNodeData _node;
    PayloadStep _existingStep;
    SkillPayloadDef _draft;
    IPayloadDesignerDescriptor _descriptor;
    PayloadDesignerContext _context;
    List<PayloadAuthoringIssue> _issues = new();
    bool _issuesDirty = true;
    bool _advancedExpanded;
    UnityEditor.Editor _advancedEditor;
    Action _onApplied;
    Vector2 _scroll;

    public static ActiveSkillAbilityWizardWindow OpenCreate(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        Action onApplied)
    {
        var window = CreateInstance<ActiveSkillAbilityWizardWindow>();
        window.titleContent = new GUIContent("Add Ability");
        window.minSize = new Vector2(MinWidth, MinHeight);
        window._onApplied = onApplied;
        window._tree = tree;
        window._skill = skill;
        window._node = node;
        window._existingStep = null;
        window._context = new PayloadDesignerContext(tree, skill, node);
        window.ShowUtility();
        return window;
    }

    public static ActiveSkillAbilityWizardWindow OpenEdit(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        PayloadStep existingStep,
        Action onApplied)
    {
        return OpenEditInternal(tree, skill, node, existingStep, onApplied, "Edit Ability");
    }

    // Always-active (blank-gated) steps are not owned by any node (plan section 14.4), so they edit
    // with a null node -- PayloadDesignerContext already allows that, and the edit path only uses
    // the node to build that context. Titled differently so the designer is not told they are
    // editing a node "Ability" when this effect runs unconditionally.
    public static ActiveSkillAbilityWizardWindow OpenEditAlwaysActive(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        PayloadStep existingStep,
        Action onApplied)
    {
        return OpenEditInternal(tree, skill, null, existingStep, onApplied, "Edit Always-Active Effect");
    }

    static ActiveSkillAbilityWizardWindow OpenEditInternal(
        SkillUpgradeTreeDefinition tree,
        SkillGemDefinition skill,
        SkillUpgradeNodeData node,
        PayloadStep existingStep,
        Action onApplied,
        string title)
    {
        if (existingStep == null)
            throw new ArgumentNullException(nameof(existingStep));

        var window = CreateInstance<ActiveSkillAbilityWizardWindow>();
        window.titleContent = new GUIContent(title);
        window.minSize = new Vector2(MinWidth, MinHeight);
        window._onApplied = onApplied;
        window._tree = tree;
        window._skill = skill;
        window._node = node;
        window._existingStep = existingStep;
        window._context = new PayloadDesignerContext(tree, skill, node);
        window.BuildEditDraft();
        window.ShowUtility();
        return window;
    }

    void BuildEditDraft()
    {
        SkillPayloadDef real = _existingStep.Payload;
        if (real == null)
            return;

        PayloadDesignerDescriptorRegistry.TryGetDescriptor(real.GetType(), out _descriptor);
        _draft = (SkillPayloadDef)CreateInstance(real.GetType());
        _draft.hideFlags = HideFlags.HideAndDontSave;
        EditorUtility.CopySerialized(real, _draft);
        _issuesDirty = true;
    }

    void BuildCreateDraft(Type payloadType)
    {
        if (!PayloadDesignerDescriptorRegistry.TryGetDescriptor(payloadType, out _descriptor))
            return;

        _draft = (SkillPayloadDef)CreateInstance(payloadType);
        _draft.hideFlags = HideFlags.HideAndDontSave;
        _descriptor.ApplySafeDefaults(_draft, _context);
        _issuesDirty = true;
    }

    void OnGUI()
    {
        if (_draft == null)
        {
            DrawPicker();
            return;
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            DrawHeader();

            EditorGUI.BeginChangeCheck();
            _descriptor.DrawWizard(_draft, _context);
            if (EditorGUI.EndChangeCheck())
                _issuesDirty = true;

            DrawSummary();
            DrawIssues();
            DrawAdvancedSection();
        }

        DrawFooter();
    }

    void DrawPicker()
    {
        EditorGUILayout.LabelField("Choose an ability type", EditorStyles.boldLabel);

        IReadOnlyList<IPayloadDesignerDescriptor> descriptors = PayloadDesignerDescriptorRegistry.GetPickerDescriptors();
        if (descriptors.Count == 0)
        {
            EditorGUILayout.HelpBox("No descriptor-backed payload types are registered.", MessageType.Warning);
        }
        else
        {
            string currentCategory = null;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                for (int i = 0; i < descriptors.Count; i++)
                {
                    IPayloadDesignerDescriptor descriptor = descriptors[i];
                    if (!string.Equals(descriptor.Category, currentCategory, StringComparison.Ordinal))
                    {
                        currentCategory = descriptor.Category;
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField(currentCategory, EditorStyles.miniBoldLabel);
                    }

                    if (GUILayout.Button(new GUIContent(descriptor.DisplayName, descriptor.Description), GUILayout.Height(28f)))
                        BuildCreateDraft(descriptor.PayloadType);
                }
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Cancel", GUILayout.Width(100f)))
        {
            Close();
            GUIUtility.ExitGUI();
        }
    }

    void DrawHeader()
    {
        EditorGUILayout.LabelField(_descriptor.DisplayName, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(_descriptor.Description))
            EditorGUILayout.HelpBox(_descriptor.Description, MessageType.None);
        EditorGUILayout.Space();
    }

    void DrawSummary()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gameplay Summary", EditorStyles.boldLabel);

        PayloadGameplaySummary summary = _descriptor.BuildSummary(_draft, _context);
        EditorGUILayout.LabelField(summary.Headline, EditorStyles.wordWrappedLabel);
        for (int i = 0; i < summary.Details.Count; i++)
            EditorGUILayout.LabelField("- " + summary.Details[i], EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < summary.Warnings.Count; i++)
            EditorGUILayout.HelpBox(summary.Warnings[i], MessageType.Warning);
    }

    void DrawIssues()
    {
        EnsureValidation();

        for (int i = 0; i < _issues.Count; i++)
        {
            PayloadAuthoringIssue issue = _issues[i];
            MessageType messageType = issue.Severity switch
            {
                PayloadAuthoringSeverity.Error => MessageType.Error,
                PayloadAuthoringSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info,
            };
            EditorGUILayout.HelpBox(issue.Message, messageType);
        }
    }

    void DrawAdvancedSection()
    {
        EditorGUILayout.Space();
        _advancedExpanded = EditorGUILayout.Foldout(_advancedExpanded, "Advanced", true);
        if (!_advancedExpanded)
            return;

        // Safe here (unlike the tree window's node inspector) because this is a dedicated
        // EditorWindow, not an IMGUIContainer nested inside a ScrollView -- see plan section 8
        // and ActiveSkillTreeEditorWindow's documented scroll-reset workaround.
        if (_advancedEditor == null || _advancedEditor.target != _draft)
        {
            if (_advancedEditor != null)
                DestroyImmediate(_advancedEditor);
            _advancedEditor = UnityEditor.Editor.CreateEditor(_draft);
        }

        EditorGUILayout.HelpBox(
            "Raw serialized fields. Ownership, embedding, and upgrade bindings are never edited " +
            "here -- only NodeAbilityAuthoringService can change those.",
            MessageType.Info);
        _advancedEditor?.OnInspectorGUI();
    }

    void DrawFooter()
    {
        EnsureValidation();
        bool blocked = _issues.HasErrors();

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100f)))
            {
                DestroyDraft();
                Close();
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(blocked))
            {
                if (GUILayout.Button(_existingStep == null ? "Create Ability" : "Apply Changes", GUILayout.Width(140f)))
                {
                    Commit();
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.Space(4f);
    }

    void Commit()
    {
        EnsureValidation();

        if (_issues.HasWarnings())
        {
            string warningList = string.Join(
                "\n",
                _issues.Where(issue => issue.Severity == PayloadAuthoringSeverity.Warning).Select(issue => "- " + issue.Message));

            if (!EditorUtility.DisplayDialog(
                    "Ability Has Warnings",
                    $"This ability has warnings and can still be created:\n\n{warningList}",
                    "Continue",
                    "Cancel"))
            {
                return;
            }
        }

        var commitIssues = new List<PayloadAuthoringIssue>();
        bool success = _existingStep == null
            ? NodeAbilityAuthoringService.CreateNodeAbility(_tree, _skill, _node, _draft, commitIssues).Success
            : NodeAbilityAuthoringService.ApplyEditedAbility(_tree, _skill, _node, _existingStep.Payload, _draft, commitIssues);

        if (!success)
        {
            _issues = commitIssues;
            _issuesDirty = false;
            Repaint();
            return;
        }

        DestroyDraft();
        _onApplied?.Invoke();
        Close();
    }

    void EnsureValidation()
    {
        if (!_issuesDirty || _draft == null || _descriptor == null)
            return;

        _issues = new List<PayloadAuthoringIssue>();
        _descriptor.CollectAuthoringIssues(_draft, _context, _issues);
        _issuesDirty = false;
    }

    void DestroyDraft()
    {
        if (_advancedEditor != null)
        {
            DestroyImmediate(_advancedEditor);
            _advancedEditor = null;
        }

        if (_draft != null)
            DestroyImmediate(_draft);
        _draft = null;
    }

    // Catches every exit path CreateInstance-and-forget could otherwise leak through: the
    // designer clicking the OS window-close button, a domain reload while the wizard is open, or
    // an exception elsewhere in the editor tearing the window down.
    void OnDestroy()
    {
        DestroyDraft();
    }
}
