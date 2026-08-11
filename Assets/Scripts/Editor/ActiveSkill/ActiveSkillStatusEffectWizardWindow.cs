#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// หน้าต่างเดียวที่จบงาน "เพิ่มบัฟจาก Node": เลือก/สร้าง status, เลือกเป้าหมาย, ตั้งค่าตัวเลข, กด Create.
///
/// ตัว window ไม่เขียน asset เอง — ทุกการเขียนไปผ่าน
/// <see cref="ActiveSkillStatusEffectAuthoringService"/> เพื่อให้ validate/preview/undo เป็นชุดเดียวกับ
/// ที่ smoke test เรียก. Cancel = ปิดหน้าต่างเฉยๆ เพราะไม่มีอะไรถูกเขียนก่อนกดปุ่ม commit.
/// </summary>
public sealed class ActiveSkillStatusEffectWizardWindow : EditorWindow
{
    const float MinWidth = 560f;
    const float MinHeight = 640f;

    static readonly string[] IdentityModes = { "Use Existing", "Create New" };

    StatusEffectAuthoringRequest _request;
    List<SkillGemDefinition> _ownerCandidates = new();
    List<SkillStatusRoute> _routes = new();
    List<SkillStatusTarget> _targets = new();
    SkillStatusTarget _target;
    List<SkillUpgradeValidationIssue> _issues = new();
    Action _onApplied;
    Vector2 _scroll;
    bool _newUpgradeId;
    string _upgradeIdDraft = string.Empty;
    bool _validationDirty = true;

    public static ActiveSkillStatusEffectWizardWindow Open(
        SkillGemDefinition skill,
        IReadOnlyList<SkillGemDefinition> ownerCandidates,
        SkillUpgradeTreeDefinition tree,
        SkillUpgradeNodeData node,
        StatusEffectApplicationHandle existing,
        Action onApplied)
    {
        var window = CreateInstance<ActiveSkillStatusEffectWizardWindow>();
        window.titleContent = new GUIContent(existing == null ? "Add Status Effect" : "Edit Status Effect");
        window.minSize = new Vector2(MinWidth, MinHeight);
        window._onApplied = onApplied;
        window._ownerCandidates = ownerCandidates != null
            ? new List<SkillGemDefinition>(ownerCandidates)
            : new List<SkillGemDefinition>();
        window.Initialize(skill, tree, node, existing);
        window.ShowUtility();
        return window;
    }

    void Initialize(
        SkillGemDefinition skill,
        SkillUpgradeTreeDefinition tree,
        SkillUpgradeNodeData node,
        StatusEffectApplicationHandle existing)
    {
        _request = existing != null
            ? ActiveSkillStatusEffectAuthoringService.BuildEditRequest(skill, tree, node, existing)
            : new StatusEffectAuthoringRequest { Skill = skill, Tree = tree, Node = node };

        _request.NewEffect ??= new NewStatusEffectRequest();
        RebuildRoutes(existing == null);

        if (existing != null)
        {
            _newUpgradeId = false;
            _upgradeIdDraft = _request.UpgradeId ?? string.Empty;
        }
        else
        {
            List<string> granted = ResolveGrantedIds();
            _newUpgradeId = granted.Count == 0;
            _upgradeIdDraft = granted.Count > 0 ? granted[0] : SuggestUpgradeId();
            _request.UpgradeId = _upgradeIdDraft;
            _request.NewEffect.AssetName = SuggestEffectName();
        }

        _validationDirty = true;
    }

    void RebuildRoutes(bool selectDefaultRoute)
    {
        _routes = SkillStatusRouteResolver.Resolve(_request.Skill);
        _targets = SkillStatusRouteResolver.ResolveTargets(_routes);

        SkillStatusRoute current = _request.Route != null
            ? SkillStatusRouteResolver.FindByKey(_routes, _request.Route.RouteKey)
            : null;

        if (current != null)
        {
            _request.Route = current;
            _target = current.Target;
            return;
        }

        if (!selectDefaultRoute && _request.Route != null)
        {
            // Edit mode whose destination vanished: keep the stale route so Validate can explain it.
            _target = _request.Route.Target;
            return;
        }

        _target = _targets.Count > 0 ? _targets[0] : SkillStatusTarget.Self;
        List<SkillStatusRoute> forTarget = SkillStatusRouteResolver.FilterByTarget(_routes, _target);
        _request.Route = forTarget.Count > 0 ? forTarget[0] : null;
    }

    void OnGUI()
    {
        if (_request == null)
        {
            Close();
            return;
        }

        EditorGUI.BeginChangeCheck();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawContext();
        DrawIdentity();
        DrawApplication();
        DrawPreview();
        DrawIssues();
        EditorGUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck())
            _validationDirty = true;

        DrawFooter();
    }

    #region Sections

    void DrawContext()
    {
        EditorGUILayout.LabelField("Context", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (_ownerCandidates.Count > 1)
            {
                var names = new string[_ownerCandidates.Count];
                int selected = 0;
                for (int i = 0; i < _ownerCandidates.Count; i++)
                {
                    names[i] = _ownerCandidates[i] != null ? _ownerCandidates[i].name : "<missing>";
                    if (ReferenceEquals(_ownerCandidates[i], _request.Skill))
                        selected = i;
                }

                int next = EditorGUILayout.Popup("Owning Skill", selected, names);
                if (next != selected)
                {
                    _request.Skill = _ownerCandidates[next];
                    _request.Route = null;
                    RebuildRoutes(true);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Owning Skill", _request.Skill != null ? _request.Skill.name : "<none>");
            }

            EditorGUILayout.LabelField(
                "Selected Node",
                _request.Node != null ? _request.Node.ResolvedDisplayName : "<none>");

            DrawUpgradeGate();
        }
    }

    void DrawUpgradeGate()
    {
        List<string> granted = ResolveGrantedIds();
        var options = new List<string>(granted) { "<New upgrade id...>" };
        int index = _newUpgradeId
            ? options.Count - 1
            : Mathf.Max(0, granted.IndexOf(_request.UpgradeId?.Trim() ?? string.Empty));

        int next = EditorGUILayout.Popup("Upgrade Gate", index, options.ToArray());
        if (next != index)
        {
            _newUpgradeId = next == options.Count - 1;
            _upgradeIdDraft = _newUpgradeId ? SuggestUpgradeId() : granted[next];
            _request.UpgradeId = _upgradeIdDraft;
        }

        if (_newUpgradeId || granted.Count == 0)
        {
            _upgradeIdDraft = EditorGUILayout.TextField("New Upgrade Id", _upgradeIdDraft);
            _request.UpgradeId = _upgradeIdDraft;
            EditorGUILayout.HelpBox(
                "This id will be added to the selected node's granted upgrade ids automatically.",
                MessageType.None);
        }
        else
        {
            _request.UpgradeId = options[next];
        }
    }

    void DrawIdentity()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status Identity", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int mode = _request.CreateNewEffect ? 1 : 0;
            int nextMode = GUILayout.Toolbar(mode, IdentityModes);
            if (nextMode != mode)
                _request.CreateNewEffect = nextMode == 1;

            if (_request.CreateNewEffect)
                DrawNewEffectFields();
            else
                DrawExistingEffectFields();
        }
    }

    void DrawExistingEffectFields()
    {
        StatusEffectDef current = _request.ExistingEffect;
        StatusEffectDef next = EditorGUILayout.ObjectField(
            "Status Effect",
            current,
            typeof(StatusEffectDef),
            false) as StatusEffectDef;
        if (!ReferenceEquals(next, current))
        {
            _request.ExistingEffect = next;
            _request.ExistingScopeResolution = ExistingStatusScopeResolution.None;
            ActiveSkillStatusEffectAuthoringService.RefreshInheritedValues(_request, next);
        }

        if (_request.ExistingEffect == null)
        {
            EditorGUILayout.HelpBox(
                "Pick an existing status, or switch to Create New for a plain numeric buff/debuff.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Scope", StatusEffectScopeUtility.DescribeScope(_request.ExistingEffect));

        if (_request.ExistingScopeResolution != ExistingStatusScopeResolution.None)
        {
            EditorGUILayout.HelpBox(DescribeStagedScopeResolution(), MessageType.Info);
            if (GUILayout.Button("Clear Pending Scope Change"))
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.None;
            return;
        }

        if (StatusEffectScopeUtility.IsUsableBy(_request.ExistingEffect, _request.Skill, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                DrawLegacyScopeFixes(reason);
            return;
        }

        EditorGUILayout.HelpBox(reason, MessageType.Error);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Promote To Global"))
            {
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.PromoteToGlobal;
                _validationDirty = true;
            }

            if (GUILayout.Button("Duplicate As Unique"))
            {
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.DuplicateAsUnique;
                _validationDirty = true;
            }

            if (GUILayout.Button("Choose Another Status"))
            {
                _request.ExistingEffect = null;
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.None;
                _validationDirty = true;
            }
        }
    }

    void DrawLegacyScopeFixes(string reason)
    {
        EditorGUILayout.HelpBox(reason, MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Label As Global"))
            {
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.PromoteToGlobal;
                _validationDirty = true;
            }

            if (GUILayout.Button("Label As Unique To This Skill"))
            {
                _request.ExistingScopeResolution = ExistingStatusScopeResolution.MakeUniqueToSkill;
                _validationDirty = true;
            }
        }
    }

    string DescribeStagedScopeResolution()
    {
        switch (_request.ExistingScopeResolution)
        {
            case ExistingStatusScopeResolution.PromoteToGlobal:
                return "Pending: label this status Global when Save Changes/Create Effect is pressed.";
            case ExistingStatusScopeResolution.MakeUniqueToSkill:
                return $"Pending: label this status Unique to '{_request.Skill?.name ?? "<none>"}' on commit.";
            case ExistingStatusScopeResolution.DuplicateAsUnique:
                return $"Pending: duplicate this status as Unique to '{_request.Skill?.name ?? "<none>"}' on commit.";
            default:
                return string.Empty;
        }
    }

    void DrawNewEffectFields()
    {
        NewStatusEffectRequest settings = _request.NewEffect;
        settings.AssetName = EditorGUILayout.TextField("Asset Name", settings.AssetName);
        settings.EffectId = EditorGUILayout.TextField(
            new GUIContent("Effect Id", "Leave blank to reuse the asset name."),
            settings.EffectId);
        settings.Scope = (StatusEffectScope)EditorGUILayout.EnumPopup("Scope", settings.Scope);
        if (settings.Scope == StatusEffectScope.Legacy)
            settings.Scope = StatusEffectScope.Global;

        settings.Category = (StatusEffectCategory)EditorGUILayout.EnumPopup("Category", settings.Category);
        settings.Icon = EditorGUILayout.ObjectField("Icon", settings.Icon, typeof(Sprite), false) as Sprite;
        settings.VfxProfile = EditorGUILayout.ObjectField(
            "VFX Profile", settings.VfxProfile, typeof(StatusEffectVfxProfile), false) as StatusEffectVfxProfile;
        settings.Duration = Mathf.Max(0f, EditorGUILayout.FloatField("Default Duration", settings.Duration));
        settings.StackMode = (StackMode)EditorGUILayout.EnumPopup("Stack Mode", settings.StackMode);
        settings.MaxStacks = Mathf.Max(0, EditorGUILayout.IntField("Max Stacks", settings.MaxStacks));

        EditorGUILayout.LabelField(
            "Will Be Created At",
            ActiveSkillStatusEffectAuthoringService.ResolveNewEffectPath(_request));

        EditorGUILayout.HelpBox(
            "Create New authors a numeric status only (stat modifiers, duration, ticking). Control " +
            "blocks, taunt, and triggered stacking need an existing Status Effect Def — pick one under " +
            "Use Existing instead.",
            MessageType.Info);
    }

    void DrawApplication()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Application", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawTargetAndDestination();

            _request.Stacks = Mathf.Max(1, EditorGUILayout.IntField("Stacks", _request.Stacks));
            DrawModifiers();
            DrawDuration();
            DrawTick();
        }
    }

    void DrawTargetAndDestination()
    {
        if (_targets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "This skill has no Apply Status / Heal Area / Taunt site, so there is nowhere to " +
                "attach a status. Add the step to the skill first — the wizard will not insert one " +
                "for you, because step order is a design decision.",
                MessageType.Error);
            return;
        }

        var targetNames = new string[_targets.Count];
        int targetIndex = 0;
        for (int i = 0; i < _targets.Count; i++)
        {
            targetNames[i] = SkillStatusRoute.DescribeTarget(_targets[i]);
            if (_targets[i] == _target)
                targetIndex = i;
        }

        int nextTarget = EditorGUILayout.Popup("Target", targetIndex, targetNames);
        if (nextTarget != targetIndex)
        {
            _target = _targets[nextTarget];
            List<SkillStatusRoute> retargeted = SkillStatusRouteResolver.FilterByTarget(_routes, _target);
            _request.Route = retargeted.Count > 0 ? retargeted[0] : null;
        }

        List<SkillStatusRoute> candidates = SkillStatusRouteResolver.FilterByTarget(_routes, _target);
        if (candidates.Count <= 1)
        {
            EditorGUILayout.LabelField(
                "Destination",
                candidates.Count == 1 ? candidates[0].DisplayLabel : "<none>");
            if (candidates.Count == 1)
                _request.Route = candidates[0];
            return;
        }

        var names = new string[candidates.Count];
        int selected = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            names[i] = candidates[i].DisplayLabel;
            if (_request.Route != null && candidates[i].Matches(_request.Route))
                selected = i;
        }

        int next = EditorGUILayout.Popup("Destination", selected, names);
        _request.Route = candidates[next];
    }

    void DrawModifiers()
    {
        EditorGUILayout.Space(2f);
        _request.OverrideModifiers = EditorGUILayout.ToggleLeft(
            "Override Modifiers (otherwise the Status Effect Def's own modifiers apply)",
            _request.OverrideModifiers);

        if (!_request.OverrideModifiers)
            return;

        _request.Modifiers ??= new List<StatusEffectModifier>();
        EditorGUI.indentLevel++;
        for (int i = 0; i < _request.Modifiers.Count; i++)
        {
            StatusEffectModifier modifier = _request.Modifiers[i];
            if (modifier == null)
            {
                modifier = new StatusEffectModifier();
                _request.Modifiers[i] = modifier;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                modifier.statType = (StatType)EditorGUILayout.EnumPopup(modifier.statType);
                ModifierOp nextOperation = (ModifierOp)EditorGUILayout.EnumPopup(modifier.operation);
                if (nextOperation != modifier.operation &&
                    nextOperation == ModifierOp.Multiply &&
                    Mathf.Approximately(modifier.value, 0f))
                {
                    modifier.value = 1f;
                }

                modifier.operation = nextOperation;
                modifier.value = EditorGUILayout.FloatField(modifier.value);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    _request.Modifiers.RemoveAt(i);
                    _validationDirty = true;
                    break;
                }
            }
        }

        if (GUILayout.Button("Add Modifier"))
        {
            _request.Modifiers.Add(new StatusEffectModifier
            {
                statType = StatType.MaxHP,
                operation = ModifierOp.AddPercent,
                value = 0f,
            });
            _validationDirty = true;
        }

        EditorGUI.indentLevel--;
    }

    void DrawDuration()
    {
        EditorGUILayout.Space(2f);
        using (new EditorGUILayout.HorizontalScope())
        {
            _request.OverrideDuration = EditorGUILayout.ToggleLeft(
                "Override Duration", _request.OverrideDuration, GUILayout.Width(180f));
            using (new EditorGUI.DisabledScope(!_request.OverrideDuration))
            {
                _request.Duration = Mathf.Max(0f, EditorGUILayout.FloatField(_request.Duration));
            }

            EditorGUILayout.LabelField(
                _request.OverrideDuration
                    ? _request.Duration <= 0f ? "Permanent" : "seconds"
                    : "Skill/Taunt fallback, then Def",
                EditorStyles.miniLabel);
        }
    }

    void DrawTick()
    {
        EditorGUILayout.Space(2f);
        StatusEffectDef definition = _request.CreateNewEffect ? null : _request.ExistingEffect;
        bool definitionHasTick = definition != null && definition.HasTick;
        bool hasAnyOverride = _request.OverrideTickDamage || _request.OverrideTickInterval;

        if (!definitionHasTick && !hasAnyOverride)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Tick: Not Used", EditorStyles.boldLabel);
                if (GUILayout.Button("Add Tick Override", GUILayout.Width(150f)))
                {
                    _request.OverrideTickDamage = true;
                    _request.OverrideTickInterval = true;
                    _validationDirty = true;
                }
            }
            return;
        }

        EditorGUI.indentLevel++;
        using (new EditorGUILayout.HorizontalScope())
        {
            _request.OverrideTickDamage = EditorGUILayout.ToggleLeft(
                "Override Tick Damage", _request.OverrideTickDamage, GUILayout.Width(180f));
            using (new EditorGUI.DisabledScope(!_request.OverrideTickDamage))
                _request.TickDamage = EditorGUILayout.FloatField(_request.TickDamage);
            EditorGUILayout.LabelField(
                _request.OverrideTickDamage ? "Application Override" : "From Status Effect Def",
                EditorStyles.miniLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _request.OverrideTickInterval = EditorGUILayout.ToggleLeft(
                "Override Tick Interval", _request.OverrideTickInterval, GUILayout.Width(180f));
            using (new EditorGUI.DisabledScope(!_request.OverrideTickInterval))
                _request.TickInterval = Mathf.Max(0f, EditorGUILayout.FloatField(_request.TickInterval));
            EditorGUILayout.LabelField(
                _request.OverrideTickInterval ? "Application Override" : "From Status Effect Def",
                EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;
    }

    void DrawPreview()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(
            ActiveSkillStatusEffectAuthoringService.BuildPreview(_request),
            EditorStyles.helpBox,
            GUILayout.MinHeight(120f),
            GUILayout.ExpandHeight(true));
    }

    void DrawIssues()
    {
        EnsureValidation();
        for (int i = 0; i < _issues.Count; i++)
        {
            SkillUpgradeValidationIssue issue = _issues[i];
            EditorGUILayout.HelpBox(
                issue.Message,
                issue.Severity == SkillUpgradeValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
        }
    }

    void DrawFooter()
    {
        EnsureValidation();
        bool blocked = ActiveSkillStatusEffectAuthoringService.HasErrors(_issues);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100f)))
            {
                Close();
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(blocked))
            {
                if (GUILayout.Button(_request.IsEdit ? "Save Changes" : "Create Effect", GUILayout.Width(140f)))
                {
                    Commit();
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.Space(4f);
    }

    #endregion

    void Commit()
    {
        if (!ActiveSkillStatusEffectAuthoringService.Apply(
                _request, out List<SkillUpgradeValidationIssue> issues))
        {
            _issues = issues;
            _validationDirty = false;
            Repaint();
            return;
        }

        _onApplied?.Invoke();
        Close();
    }

    void EnsureValidation()
    {
        if (!_validationDirty)
            return;

        _issues = ActiveSkillStatusEffectAuthoringService.Validate(_request);
        _validationDirty = false;
    }

    List<string> ResolveGrantedIds()
    {
        var result = new List<string>();
        List<string> granted = _request.Node?.grantedUpgradeIds;
        for (int i = 0; granted != null && i < granted.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(granted[i]))
                result.Add(granted[i].Trim());
        }

        return result;
    }

    string SuggestUpgradeId()
    {
        string skillId = _request.Skill != null ? _request.Skill.SkillDefinitionId : null;
        string nodeId = _request.Node != null ? _request.Node.RuntimeNodeId : "node";
        if (string.IsNullOrWhiteSpace(skillId))
            skillId = _request.Skill != null ? _request.Skill.name : "skill";

        return $"{skillId.Trim()}.{nodeId}".ToLowerInvariant().Replace(' ', '_');
    }

    string SuggestEffectName()
    {
        string nodeName = _request.Node != null ? _request.Node.ResolvedDisplayName : "Status";
        return StatusEffectScopeUtility.SanitizeFolderName(nodeName).Replace(" ", string.Empty);
    }
}
#endif
