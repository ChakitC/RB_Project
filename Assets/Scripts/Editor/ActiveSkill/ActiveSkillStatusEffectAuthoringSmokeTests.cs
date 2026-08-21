#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Smoke tests ของ status-effect authoring flow.
///
/// รันบน asset จริงในโฟลเดอร์ชั่วคราว ไม่ใช่ ScriptableObject ใน memory เพราะสิ่งที่ต้องพิสูจน์
/// (embedded payload sub-asset, asset label, การเขียนผ่าน SerializedProperty ลง list ที่เป็น private)
/// มีอยู่จริงเฉพาะตอนที่ asset ถูกบันทึกลงดิสก์แล้วเท่านั้น.
/// </summary>
public static class ActiveSkillStatusEffectAuthoringSmokeTests
{
    const string TempFolder = "Assets/_StatusAuthoringSmokeTests";

    [MenuItem("Tools/RB/Skills/Run Status Effect Authoring Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        Fixture fixture = null;
        try
        {
            fixture = Fixture.Create();

            TestScopeLabelsClassifyGlobalUniqueAndLegacy(fixture);
            TestScopeValidatorIncludesUnreferencedStatuses(fixture);
            TestUniqueStatusIsRejectedForAnotherSkill(fixture);
            TestScopeFixesStayStagedUntilCommit(fixture);
            TestStagedScopeFixAppliesOnCommit(fixture);
            TestDuplicateEffectIdIsReported(fixture);
            TestRoutesResolveSelfAlliesAndTauntedEnemies(fixture);
            TestMultipleDestinationsAreDistinguishedByStep(fixture);
            TestRouteFieldsAreDiscoveredFromTheirDeclaration();
            TestDynamicTargetMemberDrivesTheRoute();
            TestMissingRouteAttributeIsBlocking();
            TestBrokenTargetMemberIsBlocking();
            TestTreeValidatorReportsRouteMetadataIssues();
            TestNonSerializedRouteIsBlocking();
            TestValidateAloneNeverWrites(fixture);
            TestCreateWithNewStatusAssetAndNewUpgradeId(fixture);
            TestOverrideFlagsFollowTheRequestedChannels(fixture);
            TestIndependentTickOverridesSurviveEdit(fixture);
            TestExistingUpgradeIdIsReusedWithoutRegranting(fixture);
            TestDuplicateApplicationIsBlocked(fixture);
            TestSourceNavigationResolvesTheOwningContainer(fixture);
            TestMovingTargetRelocatesTheApplication(fixture);
            TestScannerReadsTargetFromRouteMetadata(fixture);
            TestLostDestinationIsReported(fixture);
            TestSharedTreeRequiresAnOwningSkill(fixture);
            TestSharedTreeUsageChecksEveryOwner(fixture);
            TestUndoInvalidatesGameplayEffectCaches(fixture);
            TestRemoveDropsApplicationAndFreesTheUpgradeId(fixture);

            Debug.Log("[StatusAuthoringTests] All status effect authoring smoke tests passed.");
        }
        finally
        {
            fixture?.Dispose();
        }
    }

    #region Scope

    static void TestScopeLabelsClassifyGlobalUniqueAndLegacy(Fixture fixture)
    {
        Equal(StatusEffectScope.Global, StatusEffectScopeUtility.GetScope(fixture.GlobalStatus),
            "A StatusScope.Global label must classify as Global.");
        Equal(StatusEffectScope.Unique, StatusEffectScopeUtility.GetScope(fixture.UniqueStatus),
            "A StatusScope.Unique label must classify as Unique.");
        Equal(StatusEffectScope.Legacy, StatusEffectScopeUtility.GetScope(fixture.LegacyStatus),
            "An unlabelled status must classify as Legacy.");

        Equal(StatusEffectScopeUtility.GetAssetGuid(fixture.Skill),
            StatusEffectScopeUtility.GetOwnerGuid(fixture.UniqueStatus),
            "A unique status must carry its owning skill's GUID.");

        Expect(StatusEffectScopeUtility.IsUsableBy(fixture.GlobalStatus, fixture.Skill, out string globalReason) &&
               string.IsNullOrEmpty(globalReason),
            "A global status must be usable with no warning.");

        Expect(StatusEffectScopeUtility.IsUsableBy(fixture.LegacyStatus, fixture.Skill, out string legacyReason) &&
               !string.IsNullOrEmpty(legacyReason),
            "A legacy status must stay usable but report a warning reason.");

        Expect(StatusEffectScopeUtility.IsUsableBy(fixture.UniqueStatus, fixture.Skill, out _),
            "A unique status must be usable by its own owner.");
    }

    static void TestUniqueStatusIsRejectedForAnotherSkill(Fixture fixture)
    {
        Expect(!StatusEffectScopeUtility.IsUsableBy(fixture.UniqueStatus, fixture.OtherSkill, out string reason),
            "A unique status must be rejected for a skill that does not own it.");
        Expect(reason.Contains("unique to", StringComparison.OrdinalIgnoreCase),
            "The rejection must name the owning skill so the fix is obvious.");

        var request = fixture.NewRequest("scope.cross", fixture.UniqueStatus);
        request.Skill = fixture.OtherSkill;
        request.Route = SkillStatusRouteResolver.Resolve(fixture.OtherSkill).FirstOrDefault();
        List<SkillUpgradeValidationIssue> issues = ActiveSkillStatusEffectAuthoringService.Validate(request);
        Expect(ActiveSkillStatusEffectAuthoringService.HasErrors(issues),
            "Authoring a cross-skill unique status must be a blocking error, not a warning.");
    }

    static void TestScopeValidatorIncludesUnreferencedStatuses(Fixture fixture)
    {
        List<SkillUpgradeValidationIssue> issues = StatusEffectScopeValidator.Validate(
            new List<SkillDefinitionBase> { fixture.Skill, fixture.OtherSkill });

        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains(fixture.BrokenUniqueStatus.name, StringComparison.Ordinal) &&
                issue.Message.Contains("no StatusOwner", StringComparison.Ordinal)),
            "Project scope validation must report an unreferenced Unique status whose owner is missing.");
    }

    static void TestScopeFixesStayStagedUntilCommit(Fixture fixture)
    {
        var request = fixture.NewRequest("scope.staged", fixture.UniqueStatus);
        request.Skill = fixture.OtherSkill;
        request.Route = SkillStatusRouteResolver.Resolve(fixture.OtherSkill).FirstOrDefault();
        request.ExistingScopeResolution = ExistingStatusScopeResolution.PromoteToGlobal;

        int assetsBefore = AssetDatabase.FindAssets("t:StatusEffectDef", new[] { TempFolder }).Length;
        ActiveSkillStatusEffectAuthoringService.Validate(request);
        ActiveSkillStatusEffectAuthoringService.BuildPreview(request);

        Equal(StatusEffectScope.Unique, StatusEffectScopeUtility.GetScope(fixture.UniqueStatus),
            "Staging Promote To Global must not mutate the source status before commit.");
        Equal(assetsBefore, AssetDatabase.FindAssets("t:StatusEffectDef", new[] { TempFolder }).Length,
            "Validation/preview of a staged scope fix must not create an asset.");

        request.ExistingScopeResolution = ExistingStatusScopeResolution.DuplicateAsUnique;
        ActiveSkillStatusEffectAuthoringService.Validate(request);
        ActiveSkillStatusEffectAuthoringService.BuildPreview(request);
        Equal(assetsBefore, AssetDatabase.FindAssets("t:StatusEffectDef", new[] { TempFolder }).Length,
            "Staging Duplicate As Unique must not create the copy before commit.");
    }

    static void TestStagedScopeFixAppliesOnCommit(Fixture fixture)
    {
        var request = fixture.NewRequest("scope.commit", fixture.ScopeCommitStatus);
        request.ExistingScopeResolution = ExistingStatusScopeResolution.MakeUniqueToSkill;

        Expect(ActiveSkillStatusEffectAuthoringService.Apply(
                request, out List<SkillUpgradeValidationIssue> issues),
            $"A staged Make Unique action must apply during commit. Issues: {Describe(issues)}");
        Equal(StatusEffectScope.Unique, StatusEffectScopeUtility.GetScope(fixture.ScopeCommitStatus),
            "Commit must apply the staged scope label.");
        Equal(StatusEffectScopeUtility.GetAssetGuid(fixture.Skill),
            StatusEffectScopeUtility.GetOwnerGuid(fixture.ScopeCommitStatus),
            "Commit must assign the selected skill as the Unique owner.");
    }

    static void TestDuplicateEffectIdIsReported(Fixture fixture)
    {
        string original = fixture.LegacyStatus.effectId;
        fixture.LegacyStatus.effectId = fixture.GlobalStatus.effectId;
        EditorUtility.SetDirty(fixture.LegacyStatus);
        AssetDatabase.SaveAssetIfDirty(fixture.LegacyStatus);

        try
        {
            List<SkillUpgradeValidationIssue> issues = StatusEffectScopeValidator.Validate(
                new List<SkillDefinitionBase> { fixture.Skill });
            Expect(issues.Any(issue =>
                    issue.Severity == SkillUpgradeValidationSeverity.Error &&
                    issue.Message.Contains(fixture.GlobalStatus.effectId, StringComparison.Ordinal)),
                "Two status assets sharing an effect id must be a blocking error.");

            var request = fixture.NewRequest("scope.dupid", null);
            request.CreateNewEffect = true;
            request.NewEffect.AssetName = "DuplicateIdProbe";
            request.NewEffect.EffectId = fixture.GlobalStatus.effectId;
            request.NewEffect.FolderOverride = TempFolder;
            Expect(ActiveSkillStatusEffectAuthoringService
                    .Validate(request)
                    .Any(issue => issue.Message.Contains("already used by", StringComparison.Ordinal)),
                "Creating a status with an id that already exists must be blocked before any asset is written.");
        }
        finally
        {
            fixture.LegacyStatus.effectId = original;
            EditorUtility.SetDirty(fixture.LegacyStatus);
            AssetDatabase.SaveAssetIfDirty(fixture.LegacyStatus);
        }
    }

    #endregion

    #region Routes

    static void TestRoutesResolveSelfAlliesAndTauntedEnemies(Fixture fixture)
    {
        List<SkillStatusRoute> routes = SkillStatusRouteResolver.Resolve(fixture.Skill);
        List<SkillStatusTarget> targets = SkillStatusRouteResolver.ResolveTargets(routes);

        Expect(targets.Contains(SkillStatusTarget.Self), "Apply Status must expose a Self route.");
        Expect(targets.Contains(SkillStatusTarget.Allies), "HealArea(Allies) must expose an Allies route.");
        Expect(targets.Contains(SkillStatusTarget.TauntedEnemies),
            "The Taunt payload must expose a Taunted Enemies route.");

        SkillStatusRoute allies = SkillStatusRouteResolver
            .FilterByTarget(routes, SkillStatusTarget.Allies)
            .Single();
        var alliesComposite = (CompositeSkillPayloadDef)fixture.Skill.payload;
        var healAreaPayload = (HealAreaSkillPayloadDef)((PayloadStep)alliesComposite.Steps[1]).Payload;
        Expect(ReferenceEquals(allies.Container, healAreaPayload),
            "A Heal Area route stores its list on the embedded HealAreaSkillPayloadDef sub-asset, not the composite.");
        Expect(!allies.ListPropertyPath.StartsWith("steps.Array.data[", StringComparison.Ordinal),
            "A Heal Area route now addresses the list on its own payload object, not a composite step path.");

        // A skill with no status site must offer nothing rather than inventing a step.
        Equal(0, SkillStatusRouteResolver.Resolve(fixture.EmptySkill).Count,
            "A skill with no Apply Status / Heal Area / Taunt site must expose no routes.");
    }

    static void TestMultipleDestinationsAreDistinguishedByStep(Fixture fixture)
    {
        List<SkillStatusRoute> selfRoutes = SkillStatusRouteResolver.FilterByTarget(
            SkillStatusRouteResolver.Resolve(fixture.Skill), SkillStatusTarget.Self);

        Equal(2, selfRoutes.Count, "Two Apply Status steps must produce two separate Self destinations.");
        Expect(selfRoutes[0].RouteKey != selfRoutes[1].RouteKey,
            "Two destinations on the same target must not collapse onto one route key.");
        Expect(selfRoutes.All(route => route.DisplayLabel.Contains($"Step {route.StepIndex}")),
            "A destination label must carry its step number so the designer can tell them apart.");
    }

    /// <summary>
    /// Route discovery must come from the declaration alone. These probe types are never referenced
    /// by the resolver, the scanner or the wizard -- if a new payload/step needs an edit in any of
    /// those files to be seen, this test is the one that fails.
    /// </summary>
    static void TestRouteFieldsAreDiscoveredFromTheirDeclaration()
    {
        IReadOnlyList<SkillStatusRouteFieldInfo> fields =
            SkillStatusRouteMetadata.GetRouteFields(typeof(ProbeWithTwoSelfRoutes));

        Equal(2, fields.Count, "Every ConditionalStatusRoute field must be discovered by declaration.");
        Expect(fields.All(field => string.IsNullOrEmpty(field.MetadataError)),
            "A correctly declared route must produce no metadata error.");

        // Two routes with the same target on one owner must still address different lists, which is
        // what keeps their RouteKeys apart.
        Expect(fields[0].ApplicationsPropertyPath != fields[1].ApplicationsPropertyPath,
            "Two routes on one owner must address different lists so their route keys cannot collide.");

        var owner = new ProbeWithTwoSelfRoutes();
        Expect(SkillStatusRouteMetadata.TryResolveTarget(fields[0], owner, out SkillStatusTarget first, out _) &&
               SkillStatusRouteMetadata.TryResolveTarget(fields[1], owner, out SkillStatusTarget second, out _) &&
               first == SkillStatusTarget.Self && second == SkillStatusTarget.Self,
            "A fixed target must be read straight off the attribute.");
    }

    static void TestDynamicTargetMemberDrivesTheRoute()
    {
        SkillStatusRouteFieldInfo field = SkillStatusRouteMetadata.FindRouteField(
            typeof(ProbeWithDynamicTarget), nameof(ProbeWithDynamicTarget.route));
        Expect(field != null, "A route field must be findable by its serialized name.");

        var owner = new ProbeWithDynamicTarget { Mode = SkillStatusTarget.Allies };
        Expect(SkillStatusRouteMetadata.TryResolveTarget(field, owner, out SkillStatusTarget target, out _) &&
               target == SkillStatusTarget.Allies,
            "A behavior-driven target must be read from the owning instance, not guessed.");

        owner.Mode = SkillStatusTarget.Self;
        Expect(SkillStatusRouteMetadata.TryResolveTarget(field, owner, out target, out _) &&
               target == SkillStatusTarget.Self,
            "Changing the behavior must change the resolved target.");
    }

    static void TestMissingRouteAttributeIsBlocking()
    {
        SkillStatusRouteFieldInfo field = SkillStatusRouteMetadata.FindRouteField(
            typeof(ProbeWithoutAttribute), nameof(ProbeWithoutAttribute.route));

        Expect(field != null && !string.IsNullOrEmpty(field.MetadataError),
            "A route with no [SkillStatusRouteTarget] must report a metadata error.");
        Expect(!SkillStatusRouteMetadata.TryResolveTarget(field, new ProbeWithoutAttribute(), out _, out _),
            "An undeclared target must never silently fall back to Self.");
    }

    static void TestBrokenTargetMemberIsBlocking()
    {
        SkillStatusRouteFieldInfo missing = SkillStatusRouteMetadata.FindRouteField(
            typeof(ProbeWithBrokenTargetMember), nameof(ProbeWithBrokenTargetMember.missingMember));
        Expect(missing != null && !string.IsNullOrEmpty(missing.MetadataError),
            "A target member that does not exist must be reported as a metadata error.");

        SkillStatusRouteFieldInfo wrongType = SkillStatusRouteMetadata.FindRouteField(
            typeof(ProbeWithBrokenTargetMember), nameof(ProbeWithBrokenTargetMember.wrongTypeMember));
        Expect(wrongType != null && !string.IsNullOrEmpty(wrongType.MetadataError),
            "A target member of the wrong type must be reported as a metadata error.");
    }

    static void TestTreeValidatorReportsRouteMetadataIssues()
    {
        var tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        var payload = ScriptableObject.CreateInstance<ProbePayloadWithoutRouteAttribute>();
        try
        {
            tree.treeId = "smoke.route.metadata";
            skill.name = "Route Metadata Probe";
            skill.payload = payload;

            List<SkillUpgradeValidationIssue> issues = SkillUpgradeTreeValidator.Validate(
                tree,
                new List<SkillDefinitionBase> { skill });

            Expect(issues.Any(issue =>
                    issue.Severity == SkillUpgradeValidationSeverity.Error &&
                    issue.NodeId == null &&
                    issue.Message.Contains(nameof(ProbePayloadWithoutRouteAttribute), StringComparison.Ordinal)),
                "Active Skill Tree validation must surface route metadata errors as tree-level errors.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(payload);
            UnityEngine.Object.DestroyImmediate(skill);
            UnityEngine.Object.DestroyImmediate(tree);
        }
    }

    static void TestNonSerializedRouteIsBlocking()
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        var payload = ScriptableObject.CreateInstance<ProbePayloadWithNonSerializedRoute>();
        try
        {
            skill.payload = payload;
            SkillStatusRouteResolutionResult resolution = SkillStatusRouteResolver.ResolveDetailed(skill);

            Equal(0, resolution.Routes.Count,
                "A route whose applications list is not serialized must not become a destination.");
            Expect(resolution.Issues.Any(issue =>
                    issue.Contains(nameof(ProbePayloadWithNonSerializedRoute), StringComparison.Ordinal) &&
                    issue.Contains("does not serialize", StringComparison.Ordinal)),
                "A non-serialized route must produce a blocking resolution issue.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(payload);
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    static void TestScannerReadsTargetFromRouteMetadata(Fixture fixture)
    {
        Dictionary<string, List<UpgradeIdUsage>> usages = UpgradeIdUsageScanner.Scan(
            new List<SkillDefinitionBase> { fixture.Skill });

        Expect(usages.TryGetValue("maxhp.buff", out List<UpgradeIdUsage> maxHp) && maxHp.Count > 0,
            "The scanner must find the authored application by its gate id.");
        Expect(maxHp.Any(usage => usage.Summary.EndsWith(" to Allies", StringComparison.Ordinal)),
            "The scanner must label the target from the route metadata, not from the payload type.");
        Expect(!maxHp.Any(usage => usage.Summary.Contains("<unresolved target>", StringComparison.Ordinal)),
            "Every conditional application inside a declared route must resolve a target.");
    }

    #endregion

    #region Route metadata probes

    // Declared only here. Nothing in the resolver/scanner/wizard knows these types exist.
    sealed class ProbeWithTwoSelfRoutes
    {
        [SkillStatusRouteTarget(SkillStatusTarget.Self, "First")]
        public ConditionalStatusRoute first = new();

        [SkillStatusRouteTarget(SkillStatusTarget.Self, "Second")]
        public ConditionalStatusRoute second = new();
    }

    sealed class ProbeWithDynamicTarget
    {
        [SkillStatusRouteTarget(nameof(ResolvedTarget), "Dynamic")]
        public ConditionalStatusRoute route = new();

        public SkillStatusTarget Mode = SkillStatusTarget.Self;

        SkillStatusTarget ResolvedTarget => Mode;
    }

    sealed class ProbeWithoutAttribute
    {
        public ConditionalStatusRoute route = new();
    }

    sealed class ProbeWithBrokenTargetMember
    {
        [SkillStatusRouteTarget("NoSuchMember", "Missing")]
        public ConditionalStatusRoute missingMember = new();

        [SkillStatusRouteTarget(nameof(NotATarget), "Wrong Type")]
        public ConditionalStatusRoute wrongTypeMember = new();

        public int NotATarget => 0;
    }

    sealed class ProbePayloadWithoutRouteAttribute : SkillPayloadDef
    {
        [SerializeField] private ConditionalStatusRoute route = new();

        public override SkillExecutionResult ExecuteWithResult(SkillCastContext context) =>
            SkillExecutionResult.Succeeded;
    }

    sealed class ProbePayloadWithNonSerializedRoute : SkillPayloadDef
    {
        [SkillStatusRouteTarget(SkillStatusTarget.Self, "Non Serialized")]
        private ConditionalStatusRoute route = new();

        public override SkillExecutionResult ExecuteWithResult(SkillCastContext context) =>
            SkillExecutionResult.Succeeded;
    }

    #endregion

    #region Write

    static void TestValidateAloneNeverWrites(Fixture fixture)
    {
        var request = fixture.NewRequest("write.probe", fixture.GlobalStatus);
        int before = fixture.CountApplications(request.Route);
        int grantsBefore = fixture.Node.grantedUpgradeIds.Count;

        ActiveSkillStatusEffectAuthoringService.Validate(request);
        ActiveSkillStatusEffectAuthoringService.BuildPreview(request);

        Equal(before, fixture.CountApplications(request.Route),
            "Validating/previewing must not add an application -- that is what makes Cancel safe.");
        Equal(grantsBefore, fixture.Node.grantedUpgradeIds.Count,
            "Validating/previewing must not grant an upgrade id.");
    }

    static void TestCreateWithNewStatusAssetAndNewUpgradeId(Fixture fixture)
    {
        var request = fixture.NewRequest("maxhp.buff", null);
        request.CreateNewEffect = true;
        request.NewEffect.AssetName = "SmokeMaxHPBuff";
        request.NewEffect.EffectId = "smoke.maxhp.buff";
        request.NewEffect.Scope = StatusEffectScope.Global;
        request.NewEffect.Category = StatusEffectCategory.Buff;
        request.NewEffect.FolderOverride = TempFolder;
        request.OverrideModifiers = true;
        request.Modifiers = new List<StatusEffectModifier>
        {
            new() { statType = StatType.MaxHP, operation = ModifierOp.AddPercent, value = 10f },
        };
        request.OverrideDuration = true;
        request.Duration = 10f;

        Expect(ActiveSkillStatusEffectAuthoringService.Apply(request, out List<SkillUpgradeValidationIssue> issues),
            $"Authoring the Max HP example must succeed. Issues: {Describe(issues)}");

        var created = AssetDatabase.LoadAssetAtPath<StatusEffectDef>($"{TempFolder}/SmokeMaxHPBuff.asset");
        Expect(created != null, "Create New must write the status asset to the resolved folder.");
        Equal(StatusEffectScope.Global, StatusEffectScopeUtility.GetScope(created),
            "A newly created status must carry its scope label immediately.");
        Equal("smoke.maxhp.buff", created.effectId, "The new status must keep the authored effect id.");
        Equal(0, created.controlBlocks == ControlBlockFlags.None ? 0 : 1,
            "Create New authors a numeric status only -- it must not set control blocks.");
        Equal(0, created.triggerRules.Count,
            "Create New authors a numeric status only -- it must not invent trigger rules.");

        Expect(fixture.Node.grantedUpgradeIds.Contains("maxhp.buff"),
            "Authoring must grant the new upgrade id on the selected node, with no second trip to the Inspector.");

        StatusEffectApplicationHandle handle = fixture.FindApplication("maxhp.buff");
        Expect(handle != null, "The authored application must be discoverable as a Gameplay Effects card.");
        Equal(created, handle.Effect, "The card must point at the status asset that was just created.");
        Equal(SkillStatusTarget.Self, handle.Route.Target, "The Max HP example targets Self.");

        Equal("Add Skill Status Effect", Undo.GetCurrentGroupName(),
            "The whole operation must collapse into one named undo group.");
    }

    static void TestOverrideFlagsFollowTheRequestedChannels(Fixture fixture)
    {
        StatusEffectApplicationHandle handle = fixture.FindApplication("maxhp.buff");
        SerializedProperty spec = fixture.FindSpec(handle);

        Expect(spec.FindPropertyRelative("modifiersOverrideEnabled").boolValue,
            "An authored modifier list must enable the modifier override channel.");
        Equal(1, spec.FindPropertyRelative("modifiers").arraySize,
            "The authored modifier must be written to the application, not to the Def.");
        Equal((int)StatType.MaxHP,
            spec.FindPropertyRelative("modifiers").GetArrayElementAtIndex(0).FindPropertyRelative("statType").intValue,
            "The authored modifier must keep its stat type.");

        Expect(spec.FindPropertyRelative("durationOverrideEnabled").boolValue,
            "An authored duration must enable the duration override channel.");
        Approximately(10f, spec.FindPropertyRelative("durationOverride").floatValue,
            "The authored duration must be stored on the application.");

        Expect(!spec.FindPropertyRelative("tickDamageOverrideEnabled").boolValue &&
               !spec.FindPropertyRelative("tickIntervalOverrideEnabled").boolValue,
            "A channel the designer never touched must keep following the Status Effect Def.");
        Approximately(0f, spec.FindPropertyRelative("tickIntervalOverride").floatValue,
            "An unused override channel must not leave a stale value behind in the asset.");

        // Round-tripping through the edit request is how the wizard reopens an existing card.
        StatusEffectAuthoringRequest reopened = ActiveSkillStatusEffectAuthoringService.BuildEditRequest(
            fixture.Skill, fixture.Tree, fixture.Node, handle);
        Expect(reopened.OverrideModifiers && reopened.OverrideDuration &&
               !reopened.OverrideTickDamage && !reopened.OverrideTickInterval,
            "Edit mode must reload exactly the channels that were overridden.");
        Approximately(10f, reopened.Duration, "Edit mode must reload the stored duration.");
    }

    static void TestIndependentTickOverridesSurviveEdit(Fixture fixture)
    {
        StatusEffectApplicationHandle handle = fixture.FindApplication("maxhp.buff");
        var serialized = handle.Route.CreateSerializedObject();
        serialized.Update();
        SerializedProperty spec = handle.Route.FindList(serialized)
            .GetArrayElementAtIndex(handle.ElementIndex)
            .FindPropertyRelative("spec");
        spec.FindPropertyRelative("tickDamageOverride").floatValue = -5f;
        spec.FindPropertyRelative("tickDamageOverrideEnabled").boolValue = true;
        spec.FindPropertyRelative("tickIntervalOverrideEnabled").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        StatusEffectAuthoringRequest request = ActiveSkillStatusEffectAuthoringService.BuildEditRequest(
            fixture.Skill, fixture.Tree, fixture.Node, handle);
        Expect(request.OverrideTickDamage && !request.OverrideTickInterval,
            "Edit mode must preserve independent tick override flags.");

        Expect(ActiveSkillStatusEffectAuthoringService.Apply(
                request, out List<SkillUpgradeValidationIssue> issues),
            $"Saving a partial tick override must succeed. Issues: {Describe(issues)}");

        SerializedProperty reopened = fixture.FindSpec(fixture.FindApplication("maxhp.buff"));
        Expect(reopened.FindPropertyRelative("tickDamageOverrideEnabled").boolValue,
            "Saving must keep the tick damage override enabled.");
        Expect(!reopened.FindPropertyRelative("tickIntervalOverrideEnabled").boolValue,
            "Saving must not enable the tick interval override implicitly.");
    }

    static void TestExistingUpgradeIdIsReusedWithoutRegranting(Fixture fixture)
    {
        int grantsBefore = fixture.Node.grantedUpgradeIds.Count;

        var request = fixture.NewRequest("maxhp.buff", fixture.GlobalStatus);
        Expect(ActiveSkillStatusEffectAuthoringService.Apply(request, out List<SkillUpgradeValidationIssue> issues),
            $"Reusing an existing gate id must succeed. Issues: {Describe(issues)}");

        Equal(grantsBefore, fixture.Node.grantedUpgradeIds.Count,
            "Reusing an id the node already grants must not add a duplicate grant.");
        Equal(2, ActiveSkillStatusEffectAuthoringService
                .Collect(fixture.Skill, new List<string> { "maxhp.buff" }).Count,
            "One gate id may drive several applications.");
    }

    static void TestDuplicateApplicationIsBlocked(Fixture fixture)
    {
        var request = fixture.NewRequest("maxhp.buff", fixture.GlobalStatus);
        List<SkillUpgradeValidationIssue> issues = ActiveSkillStatusEffectAuthoringService.Validate(request);

        Expect(issues.Any(issue =>
                issue.Severity == SkillUpgradeValidationSeverity.Error &&
                issue.Message.Contains("already applied", StringComparison.Ordinal)),
            "Same status + same gate id + same destination must be blocked as a duplicate.");

        // The same status through a different destination is a real authoring choice, not a dupe.
        var second = fixture.NewRequest("maxhp.buff", fixture.GlobalStatus);
        second.Route = SkillStatusRouteResolver
            .FilterByTarget(SkillStatusRouteResolver.Resolve(fixture.Skill), SkillStatusTarget.Allies)
            .Single();
        Expect(!ActiveSkillStatusEffectAuthoringService.HasErrors(
                ActiveSkillStatusEffectAuthoringService.Validate(second)),
            "The same status applied to a different target must not be treated as a duplicate.");
    }

    static void TestSourceNavigationResolvesTheOwningContainer(Fixture fixture)
    {
        StatusEffectApplicationHandle handle = fixture.FindApplication("maxhp.buff");
        Expect(handle.Route.Container is ApplyStatusSkillPayloadDef,
            "Open Source on a Self application must reach the Apply Status payload sub-asset.");
        Expect(AssetDatabase.IsSubAsset(handle.Route.Container),
            "The navigation target must be the embedded payload that actually stores the application.");
        Expect(handle.SourceLabel.Contains("Apply Status", StringComparison.Ordinal),
            "The card must say where the application is configured.");
    }

    static void TestMovingTargetRelocatesTheApplication(Fixture fixture)
    {
        StatusEffectApplicationHandle handle = fixture.FindApplication("maxhp.buff");
        SkillStatusRoute source = handle.Route;
        SkillStatusRoute destination = SkillStatusRouteResolver
            .FilterByTarget(SkillStatusRouteResolver.Resolve(fixture.Skill), SkillStatusTarget.Allies)
            .Single();

        int sourceBefore = fixture.CountApplications(source);
        int destinationBefore = fixture.CountApplications(destination);

        StatusEffectAuthoringRequest request = ActiveSkillStatusEffectAuthoringService.BuildEditRequest(
            fixture.Skill, fixture.Tree, fixture.Node, handle);
        request.Route = destination;

        Expect(ActiveSkillStatusEffectAuthoringService.Apply(request, out List<SkillUpgradeValidationIssue> issues),
            $"Changing the target of an existing application must succeed. Issues: {Describe(issues)}");

        Equal(sourceBefore - 1, fixture.CountApplications(source),
            "Moving a target must remove the application from its old destination.");
        Equal(destinationBefore + 1, fixture.CountApplications(destination),
            "Moving a target must write the application to the new destination exactly once.");
    }

    static void TestLostDestinationIsReported(Fixture fixture)
    {
        var request = fixture.NewRequest("lost.destination", fixture.GlobalStatus);
        request.Route = new SkillStatusRoute
        {
            Target = SkillStatusTarget.Self,
            Container = fixture.Skill.payload,
            ListPropertyPath = "steps.Array.data[99].conditionalStatuses.applications",
            StepIndex = 99,
            DisplayLabel = "Step 99 — Deleted",
            RouteKey = "deleted|99|steps.Array.data[99].conditionalStatuses.applications",
        };

        Expect(ActiveSkillStatusEffectAuthoringService
                .Validate(request)
                .Any(issue => issue.Message.Contains("no longer exists", StringComparison.Ordinal)),
            "A destination whose step was deleted must be reported instead of silently writing nowhere.");
    }

    static void TestSharedTreeRequiresAnOwningSkill(Fixture fixture)
    {
        var request = fixture.NewRequest("shared.owner", fixture.GlobalStatus);
        request.Skill = null;

        List<SkillUpgradeValidationIssue> issues = ActiveSkillStatusEffectAuthoringService.Validate(request);
        Expect(issues.Any(issue => issue.Message.Contains("owning skill", StringComparison.OrdinalIgnoreCase)),
            "A shared tree must force an owning-skill choice before anything can be authored.");
        Expect(!ActiveSkillStatusEffectAuthoringService.Apply(request, out _),
            "Apply must refuse to run while the owning skill is unresolved.");
    }

    static void TestSharedTreeUsageChecksEveryOwner(Fixture fixture)
    {
        var request = fixture.NewRequest("shared.owner.use", fixture.GlobalStatus);
        request.Skill = fixture.OtherSkill;
        request.Route = SkillStatusRouteResolver.Resolve(fixture.OtherSkill).FirstOrDefault();

        Expect(ActiveSkillStatusEffectAuthoringService.Apply(
                request, out List<SkillUpgradeValidationIssue> issues),
            $"Setting up the second shared-tree owner must succeed. Issues: {Describe(issues)}");
        Expect(!ActiveSkillStatusEffectAuthoringService.IsUpgradeIdStillUsed(
                fixture.Skill, "shared.owner.use"),
            "The currently selected owner should not falsely claim another owner's usage.");
        Expect(ActiveSkillStatusEffectAuthoringService.IsUpgradeIdStillUsed(
                new List<SkillDefinitionBase> { fixture.Skill, fixture.OtherSkill },
                "shared.owner.use"),
            "A shared-tree grant must stay in use while any owner still consumes it.");
    }

    static void TestUndoInvalidatesGameplayEffectCaches(Fixture fixture)
    {
        var window = ScriptableObject.CreateInstance<ActiveSkillTreeEditorWindow>();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ActiveSkillTreeEditorWindow).GetField("_tree", flags)?.SetValue(window, fixture.Tree);
            typeof(ActiveSkillTreeEditorWindow).GetField("_cachedUsages", flags)?.SetValue(
                window, new Dictionary<string, List<UpgradeIdUsage>>());
            typeof(ActiveSkillTreeEditorWindow).GetField("_cachedStatusApplications", flags)?.SetValue(
                window, new List<StatusEffectApplicationHandle>());

            MethodInfo reload = typeof(ActiveSkillTreeEditorWindow).GetMethod("ReloadGraph", flags);
            Expect(reload != null, "The tree window must expose its undo reload callback internally.");
            reload.Invoke(window, null);

            Expect(typeof(ActiveSkillTreeEditorWindow).GetField("_cachedUsages", flags)?.GetValue(window) == null,
                "Undo/redo must invalidate the upgrade usage cache.");
            Expect(typeof(ActiveSkillTreeEditorWindow)
                       .GetField("_cachedStatusApplications", flags)?.GetValue(window) == null,
                "Undo/redo must invalidate the status application cache.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    static void TestRemoveDropsApplicationAndFreesTheUpgradeId(Fixture fixture)
    {
        var request = fixture.NewRequest("remove.me", fixture.GlobalStatus);
        Expect(ActiveSkillStatusEffectAuthoringService.Apply(request, out List<SkillUpgradeValidationIssue> issues),
            $"Setting up the removal case must succeed. Issues: {Describe(issues)}");

        StatusEffectApplicationHandle handle = fixture.FindApplication("remove.me");
        Expect(handle != null, "The application to remove must exist first.");
        Expect(ActiveSkillStatusEffectAuthoringService.IsUpgradeIdStillUsed(fixture.Skill, "remove.me"),
            "The gate id must read as used while its application still exists.");

        int before = fixture.CountApplications(handle.Route);
        ActiveSkillStatusEffectAuthoringService.Remove(handle, fixture.Skill);

        Equal(before - 1, fixture.CountApplications(handle.Route),
            "Remove must delete exactly the one application.");
        Expect(!ActiveSkillStatusEffectAuthoringService.IsUpgradeIdStillUsed(fixture.Skill, "remove.me"),
            "Once nothing listens for the id, the window must be able to offer removing the grant.");
        Expect(fixture.GlobalStatus != null,
            "Remove must never delete the Status Effect Def asset -- other skills may still use it.");

        ActiveSkillStatusEffectAuthoringService.RemoveGrantedUpgradeId(fixture.Tree, fixture.Node, "remove.me");
        Expect(!fixture.Node.grantedUpgradeIds.Contains("remove.me"),
            "Accepting the offer must drop the id from the node.");
    }

    #endregion

    #region Fixture

    sealed class Fixture : IDisposable
    {
        public SkillGemDefinition Skill;
        public SkillGemDefinition OtherSkill;
        public SkillGemDefinition EmptySkill;
        public SkillUpgradeTreeDefinition Tree;
        public SkillUpgradeNodeData Node;
        public StatusEffectDef GlobalStatus;
        public StatusEffectDef UniqueStatus;
        public StatusEffectDef LegacyStatus;
        public StatusEffectDef BrokenUniqueStatus;
        public StatusEffectDef ScopeCommitStatus;

        public static Fixture Create()
        {
            StatusEffectScopeUtility.EnsureFolder(TempFolder);

            var fixture = new Fixture
            {
                Skill = CreateSkill("SmokeSkill"),
                OtherSkill = CreateSkill("SmokeOtherSkill"),
                EmptySkill = CreateSkill("SmokeEmptySkill"),
            };

            fixture.Tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
            fixture.Tree.treeId = "smoke.status.tree";
            fixture.Node = new SkillUpgradeNodeData
            {
                nodeId = "smoke.node",
                displayName = "Smoke Node",
                cost = 1,
                requiredCharacterLevel = 1,
            };
            fixture.Tree.nodes = new List<SkillUpgradeNodeData> { fixture.Node };
            AssetDatabase.CreateAsset(fixture.Tree, $"{TempFolder}/SmokeTree.asset");

            fixture.Skill.upgradeTree = fixture.Tree;
            fixture.OtherSkill.upgradeTree = fixture.Tree;
            EditorUtility.SetDirty(fixture.Skill);
            EditorUtility.SetDirty(fixture.OtherSkill);

            BuildComposite(fixture.Skill, includeAllRoutes: true);
            BuildComposite(fixture.OtherSkill, includeAllRoutes: false);

            fixture.GlobalStatus = CreateStatus("SmokeGlobal", "smoke.global", StatusEffectScope.Global, null);
            fixture.UniqueStatus = CreateStatus("SmokeUnique", "smoke.unique", StatusEffectScope.Unique, fixture.Skill);
            fixture.LegacyStatus = CreateStatus("SmokeLegacy", "smoke.legacy", StatusEffectScope.Legacy, null);
            fixture.BrokenUniqueStatus = CreateStatus(
                "SmokeBrokenUnique", "smoke.broken.unique", StatusEffectScope.Unique, null);
            fixture.ScopeCommitStatus = CreateStatus(
                "SmokeScopeCommit", "smoke.scope.commit", StatusEffectScope.Legacy, null);

            AssetDatabase.SaveAssetIfDirty(fixture.Skill);
            AssetDatabase.SaveAssetIfDirty(fixture.OtherSkill);
            AssetDatabase.SaveAssetIfDirty(fixture.EmptySkill);
            AssetDatabase.SaveAssetIfDirty(fixture.Tree);
            AssetDatabase.SaveAssetIfDirty(fixture.GlobalStatus);
            AssetDatabase.SaveAssetIfDirty(fixture.UniqueStatus);
            AssetDatabase.SaveAssetIfDirty(fixture.LegacyStatus);
            AssetDatabase.SaveAssetIfDirty(fixture.BrokenUniqueStatus);
            AssetDatabase.SaveAssetIfDirty(fixture.ScopeCommitStatus);
            return fixture;
        }

        static SkillGemDefinition CreateSkill(string assetName)
        {
            var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            AssetDatabase.CreateAsset(skill, $"{TempFolder}/{assetName}.asset");
            return skill;
        }

        static StatusEffectDef CreateStatus(
            string assetName,
            string effectId,
            StatusEffectScope scope,
            SkillDefinitionBase owner)
        {
            var definition = ScriptableObject.CreateInstance<StatusEffectDef>();
            definition.effectId = effectId;
            definition.duration = 5f;
            AssetDatabase.CreateAsset(definition, $"{TempFolder}/{assetName}.asset");
            if (scope != StatusEffectScope.Legacy)
                StatusEffectScopeUtility.SetScope(definition, scope, owner);

            return definition;
        }

        // Step 0/3: Apply Status (two Self destinations), step 1: HealArea(Allies), step 2: Taunt.
        static void BuildComposite(SkillGemDefinition skill, bool includeAllRoutes)
        {
            var composite = SkillPayloadAssetUtility.ReplaceWithEmbedded(
                skill, typeof(CompositeSkillPayloadDef), recordUndo: false) as CompositeSkillPayloadDef;

            AddStep(composite, typeof(PayloadStep));
            SkillPayloadAssetUtility.CreateEmbeddedStepPayload(
                skill, composite, 0, typeof(ApplyStatusSkillPayloadDef), null);

            if (includeAllRoutes)
            {
                AddStep(composite, typeof(PayloadStep));
                var healAreaPayload = (HealAreaSkillPayloadDef)SkillPayloadAssetUtility.CreateEmbeddedStepPayload(
                    skill, composite, 1, typeof(HealAreaSkillPayloadDef), null);
                SetHealAreaTarget(healAreaPayload, HealTargetMode.Allies);

                AddStep(composite, typeof(PayloadStep));
                SkillPayloadAssetUtility.CreateEmbeddedStepPayload(
                    skill, composite, 2, typeof(TauntSkillPayloadDef), null);

                AddStep(composite, typeof(PayloadStep));
                SkillPayloadAssetUtility.CreateEmbeddedStepPayload(
                    skill, composite, 3, typeof(ApplyStatusSkillPayloadDef), null);
            }

            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssetIfDirty(skill);
        }

        static void AddStep(CompositeSkillPayloadDef composite, Type stepType)
        {
            var serialized = new SerializedObject(composite);
            serialized.Update();
            SerializedProperty steps = serialized.FindProperty("steps");
            int index = steps.arraySize;
            steps.InsertArrayElementAtIndex(index);
            steps.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(stepType);
            serialized.ApplyModifiedProperties();
        }

        static void SetHealAreaTarget(HealAreaSkillPayloadDef payload, HealTargetMode mode)
        {
            var serialized = new SerializedObject(payload);
            serialized.Update();
            serialized.FindProperty("target").enumValueIndex = (int)mode;
            serialized.ApplyModifiedProperties();
        }

        public StatusEffectAuthoringRequest NewRequest(string upgradeId, StatusEffectDef effect)
        {
            List<SkillStatusRoute> routes = SkillStatusRouteResolver.Resolve(Skill);
            return new StatusEffectAuthoringRequest
            {
                Skill = Skill,
                Tree = Tree,
                Node = Node,
                UpgradeId = upgradeId,
                ExistingEffect = effect,
                Route = SkillStatusRouteResolver.FilterByTarget(routes, SkillStatusTarget.Self).FirstOrDefault(),
            };
        }

        public StatusEffectApplicationHandle FindApplication(string upgradeId)
        {
            return ActiveSkillStatusEffectAuthoringService
                .Collect(Skill, new List<string> { upgradeId })
                .FirstOrDefault();
        }

        public int CountApplications(SkillStatusRoute route)
        {
            if (route == null)
                return 0;

            var serialized = route.CreateSerializedObject();
            SerializedProperty list = route.FindList(serialized);
            return list != null && list.isArray ? list.arraySize : 0;
        }

        public SerializedProperty FindSpec(StatusEffectApplicationHandle handle)
        {
            var serialized = handle.Route.CreateSerializedObject();
            SerializedProperty list = handle.Route.FindList(serialized);
            return list.GetArrayElementAtIndex(handle.ElementIndex).FindPropertyRelative("spec");
        }

        public void Dispose()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }
    }

    #endregion

    #region Assertions

    static string Describe(IReadOnlyList<SkillUpgradeValidationIssue> issues)
    {
        if (issues == null || issues.Count == 0)
            return "<none>";

        return string.Join(" | ", issues.Select(issue => $"{issue.Severity}: {issue.Message}"));
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    static void Approximately(float expected, float actual, string message)
    {
        if (!Mathf.Approximately(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    #endregion
}
#endif
