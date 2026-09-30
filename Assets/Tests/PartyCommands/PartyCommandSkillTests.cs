using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Default-assembly gameplay types cannot be referenced by an asmdef. Reflection keeps
// these tests in their own Test Runner assembly without changing gameplay assemblies.
public sealed class PartyCommandSkillTests
{
    static readonly Vector3 Origin = new Vector3(12000f, 0f, 12000f);
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    readonly List<Object> created = new List<Object>();
    NavMeshDataInstance navMesh;
    Component player, actor, targeting;
    ScriptableObject skill;

    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static object Get(object owner, string name) => owner.GetType().GetField(name, Flags)?.GetValue(owner)
        ?? owner.GetType().GetProperty(name, Flags)?.GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    GameObject Go(string name)
    {
        var go = new GameObject(name);
        go.transform.position = Origin;
        created.Add(go);
        return go;
    }

    [SetUp]
    public void SetUp()
    {
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
            shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.TRS(Origin + Vector3.down * .1f, Quaternion.identity, Vector3.one),
            size = new Vector3(40f, .2f, 40f), area = 0 } };
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Origin, new Vector3(44f, 8f, 44f)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);
        created.Add(data);
        navMesh = NavMesh.AddNavMeshData(data);
        GameObject floor = Go("Command test floor");
        floor.transform.position = Origin + Vector3.down * .1f;
        floor.AddComponent<BoxCollider>().size = new Vector3(40f, .2f, 40f);
        Physics.SyncTransforms();
        Go("Command test camera").AddComponent<Camera>().tag = "MainCamera";
        player = Go("Command player").AddComponent(T("PlayerContext"));
        targeting = player.gameObject.AddComponent(T("PlayerTargetingController"));
        Set(targeting, "owner", player);
        Set(player, "Targeting", targeting);
        actor = Go("Command ally").AddComponent(T("AllyContext"));
        actor.transform.position = Origin + Vector3.left * 10f;
        skill = ScriptableObject.CreateInstance(T("SkillGemDefinition"));
        created.Add(skill);
    }

    [TearDown]
    public void TearDown()
    {
        navMesh.Remove();
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    Component Enemy(Vector3 position)
    {
        Component enemy = Go("Command enemy").AddComponent(T("EnemyContext"));
        Call(enemy, "OnEnable");
        enemy.transform.position = position;
        Component health = enemy.gameObject.AddComponent(T("HealthSystem"));
        Set(health, "currentHealth", 100f);
        Set(enemy, "HealthSystem", health);
        return enemy;
    }

    object Plan(bool expected = true)
    {
        object[] args = { player, actor, skill, null };
        bool resolved = (bool)T("PartyCommandSkillExecution").GetMethod("TryCreatePlan").Invoke(null, args);
        Assert.That(resolved, Is.EqualTo(expected));
        return args[3];
    }

    Component PrepareCaster(string payloadType = "SucceedingProbePayload")
    {
        var stats = ScriptableObject.CreateInstance(T("CharacterStats"));
        created.Add(stats);
        Set(stats, "Enagy", 100f);
        Set(actor, "baseStats", stats);
        Component user = actor.gameObject.AddComponent(T("SkillUserSystem"));
        Component driver = actor.gameObject.AddComponent(T("AIAimTargetDriver"));
        var aim = Go("Aim Target").transform;
        aim.SetParent(actor.transform, false);
        Set(driver, "aimTarget", aim);
        Set(driver, "skillUserSystem", user);
        Component manager = actor.gameObject.AddComponent(T("CharacterSkillManager"));
        Set(actor, "SkillManager", manager);
        Set(actor, "AimTargetDriver", driver);
        ScriptableObject payload = ScriptableObject.CreateInstance(Type.GetType(payloadType + ", Assembly-CSharp-Editor", true));
        created.Add(payload);
        Set(skill, "payload", payload);
        Set(skill, "baseManaCost", 10f);
        Set(skill, "baseCooldown", 5f);
        return driver;
    }

    object Start(object plan) => T("PartyCommandSkillExecution").GetMethod("TryStart").Invoke(
        null, new object[] { plan, 0, false, skill, null, null, true });

    void PlacementMode(string mode, string fallback = "InFrontOfPlayer", float distance = 4f)
    {
        Set(skill, "partyCommandPlacementMode", Enum.Parse(T("PartyCommandPlacementMode"), mode));
        Set(skill, "partyCommandNoTargetPlacement", Enum.Parse(T("PartyCommandNoTargetPlacement"), fallback));
        Set(skill, "partyCommandPlayerDistance", distance);
    }

    [TestCase("AroundPlayer", false)]
    [TestCase("AroundPlayer", true)]
    [TestCase("InFrontOfPlayer", false)]
    [TestCase("InFrontOfPlayer", true)]
    public void PlayerRelativeMode_IgnoresEnemyAndUsesConfiguredDistanceAndFacing(string mode, bool hasTarget)
    {
        PrepareCaster();
        PlacementMode(mode);
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        if (hasTarget) Call(targeting, "CommitTarget", enemy);
        object plan = Plan();
        Vector3 start = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Vector3 offset = Vector3.ProjectOnPlane(start - Origin, Vector3.up);
        if (mode == "AroundPlayer")
        {
            Assert.That(offset.magnitude, Is.EqualTo(4f).Within(.05f));
            Assert.That(offset.x, Is.LessThan(-3.9f), "Prefer the free point nearest the caster.");
        }
        else Assert.That(offset, Is.EqualTo(Vector3.right * 4f));
        Assert.That((bool)Get(Get(plan, "Target"), "WasAssigned"), Is.False);
        Vector3 aim = (Vector3)Get(plan, "AimPoint");
        Assert.That(Vector3.Dot((aim - start).normalized, Vector3.right), Is.GreaterThan(.99f));
        // Player movement and an unrelated enemy's lifetime do not redirect/cancel this command.
        player.transform.position += Vector3.back * 8f;
        player.transform.rotation = Quaternion.identity;
        enemy.gameObject.SetActive(false);
        Assert.That((bool)Get(Start(plan), "Started"), Is.True, "The Helper execution route must use the same plan.");
        Assert.That(actor.transform.position, Is.EqualTo(start));
        Assert.That((Vector3)Get(plan, "AimPoint"), Is.EqualTo(aim));
    }

    [TestCase("AroundPlayer")]
    [TestCase("InFrontOfPlayer")]
    public void TargetMode_UsesPerSkillFallbackOnlyWhenNoTarget(string fallback)
    {
        PlacementMode("PlayerTarget", fallback);
        object plan = Plan();
        Vector3 offset = Vector3.ProjectOnPlane((Vector3)Get(Get(plan, "Placement"), "StartPosition") - Origin, Vector3.up);
        Assert.That(offset, Is.EqualTo((fallback == "AroundPlayer" ? Vector3.left : Vector3.forward) * 4f));
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        Assert.That((bool)Get(Get(Plan(), "Target"), "WasAssigned"), Is.True);
    }

    [TestCase("AroundPlayer", 2f)]
    [TestCase("InFrontOfPlayer", 4.2f)]
    public void PlayerRelativeMode_AlreadyInRegionSkipsWarpAndFade(string mode, float forwardOffset)
    {
        PrepareCaster();
        Component member = PrepareFieldMember();
        Component visibility = AddVisibility();
        PlacementMode(mode);
        actor.transform.position = Origin + new Vector3(0f, .03f, forwardOffset);
        Vector3 before = actor.transform.position;
        object plan = Plan();
        Assert.That((bool)Get(plan, "KeepCurrentPosition"), Is.True);
        Assert.That((bool)Get(StartField(plan, member), "Started"), Is.True);
        Assert.That(actor.transform.position, Is.EqualTo(before));
        Assert.That((bool)Get(visibility, "IsTransitioning"), Is.False);
    }

    [Test]
    public void AroundPlayer_BlockedNearestPointChoosesAnotherPointWithinRadius()
    {
        PlacementMode("AroundPlayer");
        GameObject wall = Go("Blocked nearest ring point");
        wall.transform.position = Origin + new Vector3(-4f, 1f, 0f);
        wall.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
        Physics.SyncTransforms();
        Vector3 start = (Vector3)Get(Get(Plan(), "Placement"), "StartPosition");
        Assert.That(Vector3.ProjectOnPlane(start - Origin, Vector3.up).magnitude, Is.LessThanOrEqualTo(4.05f));
        Assert.That(Vector3.Distance(start, Origin + Vector3.left * 4f), Is.GreaterThan(.6f));
    }

    [Test]
    public void AroundPlayer_AllPointsBlockedRejects()
    {
        PlacementMode("AroundPlayer");
        GameObject wall = Go("Blocked player region");
        wall.transform.position = Origin + Vector3.up;
        wall.AddComponent<BoxCollider>().size = new Vector3(10f, 2f, 10f);
        Physics.SyncTransforms();
        Plan(false);
    }

    Component PrepareFieldMember()
    {
        Component health = actor.gameObject.AddComponent(T("HealthSystem"));
        Set(health, "currentHealth", 100f);
        Set(actor, "HealthSystem", health);
        Component state = actor.gameObject.AddComponent(T("StateHub"));
        Set(actor, "stateHub", state);
        Call(state, "Awake");
        Component member = actor.gameObject.AddComponent(T("FieldAllyMember"));
        Set(member, "actorContext", actor);
        Set(member, "actorRoot", actor.transform);
        return member;
    }

    Component AddVisibility()
    {
        Component visibility = actor.gameObject.AddComponent(T("CharacterVisibilityController"));
        Set(actor, "Visibility", visibility);
        return visibility;
    }

    static void FinishFade(Component visibility)
    {
        Set(visibility, "_elapsed", 100f);
        Call(visibility, "LateUpdate");
    }

    object StartField(object plan, Component member) => T("PartyCommandSkillExecution").GetMethod("TryStart").Invoke(
        null, new object[] { plan, 0, true, skill, member, null, true });

    [TestCase(false)]
    [TestCase(true)]
    public void CancelDuringWarpFade_RestoresVisibilityPositionAndReleasesCaster(bool afterWarp)
    {
        Component driver = PrepareCaster();
        Component member = PrepareFieldMember();
        Component visibility = AddVisibility();
        Component enemy = Enemy(Origin + Vector3.forward * 8f);
        Call(targeting, "CommitTarget", enemy);
        Vector3 before = actor.transform.position;
        Assert.That((bool)Get(StartField(Plan(), member), "Started"), Is.True);
        Component execution = actor.GetComponent(T("PartyCommandSkillExecution"));
        if (afterWarp)
        {
            FinishFade(visibility);
            Call(execution, "Update");
            Assert.That(actor.transform.position, Is.Not.EqualTo(before));
        }
        enemy.gameObject.SetActive(false);
        Call(execution, "Update");
        Assert.That(actor.transform.position, Is.EqualTo(before));
        Assert.That((float)Get(visibility, "_currentAlpha"), Is.Zero);
        Assert.That((bool)Get(visibility, "IsTransitioning"), Is.False);
        Assert.That((bool)Get(driver, "HasOverride"), Is.False);
        Assert.That((bool)Get(execution, "running"), Is.False);
        Call(targeting, "CommitTarget", Enemy(Origin + Vector3.forward * 8f));
        Assert.That((bool)Get(StartField(Plan(), member), "Started"), Is.True,
            "Cancellation must release the member and placement without spending a skill charge.");
    }

    [Test]
    public void DisableDuringWarpFade_RestoresVisibilityAndAim()
    {
        Component driver = PrepareCaster();
        Component member = PrepareFieldMember();
        Component visibility = AddVisibility();
        StartField(Plan(), member);
        FinishFade(visibility);
        Component execution = actor.GetComponent(T("PartyCommandSkillExecution"));
        Call(execution, "OnDisable");
        Assert.That((float)Get(visibility, "_currentAlpha"), Is.Zero);
        Assert.That((bool)Get(driver, "HasOverride"), Is.False);
        Assert.That((bool)Get(execution, "running"), Is.False);
    }

    [Test]
    public void HelperCommand_DoesNotRestartItsSummonFade()
    {
        PrepareCaster();
        Component visibility = AddVisibility();
        Call(visibility, "SetHiddenImmediate");
        visibility.GetType().GetMethod("Appear", Type.EmptyTypes).Invoke(visibility, null);
        Assert.That((bool)Get(Start(Plan()), "Started"), Is.True);
        Assert.That((bool)Get(visibility, "IsTransitioning"), Is.True);
        Assert.That(Get(visibility, "_transition").ToString(), Is.EqualTo("Appear"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DirectedAnimatedCommand_PreservesVerticalRootMotion(bool explicitSkill)
    {
        PrepareCaster();
        // Reuse the real Animancer graph fixture; Edit Mode can verify the playback
        // policy, while live playback verifies the authored jump and landing.
        Type fixtureType = Type.GetType("CharacterAnimBrainSmokeTests, Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(fixtureType);
        Call(fixture, "SetUp");
        try
        {
            object rig = Call(fixture, "CreateRig", null, true);
            Component brain = (Component)Get(rig, "Brain");
            brain.transform.SetParent(actor.transform, false);
            Component driver = actor.gameObject.AddComponent(T("CharacterAnimDriver"));
            Set(driver, "brain", brain);
            Set(actor, "AnimBrain", brain);
            Set(actor, "AnimDriver", driver);
            object slot = Activator.CreateInstance(T("CharacterSkillLoadoutSlot"));
            Set(slot, "slotKind", Enum.Parse(T("CharacterSkillSlotKind"), "Active"));
            object option = Activator.CreateInstance(T("CharacterSkillLoadoutOption"));
            Set(option, "skillAsset", skill);
            ((System.Collections.IList)Get(slot, "options")).Add(option);
            ((System.Collections.IList)Get(Get(actor, "baseStats"), "skillSlots")).Add(slot);
            object manager = Get(actor, "SkillManager");
            Call(manager, "RebuildResolvedCommandSlots");
            object result = Call(manager, "TryStartDirectedPartyCommand", 0, null,
                new Func<bool>(() => true), explicitSkill ? skill : null, false);
            Assert.That((bool)Get(result, "Started"), Is.True);
            Assert.That((bool)Get(brain, "RootMotionActive"), Is.True);
            Assert.That((bool)Get(brain, "RootMotionPlanarOnly"), Is.False,
                "Player-directed casts must retain the clip's authored Y movement.");
        }
        finally { Call(fixture, "TearDown"); }
    }

    [TestCase(1, 0, false, "PlayerTarget")]
    [TestCase(1, 1, true, "PlayerTarget")]
    [TestCase(2, 0, true, "PlayerTarget")]
    [TestCase(2, 1, false, "PlayerTarget")]
    [TestCase(1, 0, true, "AroundPlayer")]
    [TestCase(2, 1, true, "InFrontOfPlayer")]
    public void HudAllyKeys_WarpAndConsumeTheSelectedSlot(int column, int kind, bool hasTarget, string mode)
    {
        PrepareCaster();
        PlacementMode(mode, distance: 3f);
        Component health = actor.gameObject.AddComponent(T("HealthSystem"));
        Set(health, "currentHealth", 100f);
        Set(actor, "HealthSystem", health);
        Component state = actor.gameObject.AddComponent(T("StateHub"));
        Set(actor, "stateHub", state);
        Call(state, "Awake");
        var slots = (System.Collections.IList)Get(Get(actor, "baseStats"), "skillSlots");
        for (int i = 0; i < 2; i++)
        {
            object slot = Activator.CreateInstance(T("CharacterSkillLoadoutSlot"));
            Set(slot, "slotId", "hud-test-" + i);
            Set(slot, "slotKind", Enum.Parse(T("CharacterSkillSlotKind"), i == 0 ? "Active" : "Ultimate"));
            object option = Activator.CreateInstance(T("CharacterSkillLoadoutOption"));
            Set(option, "skillAsset", skill);
            ((System.Collections.IList)Get(slot, "options")).Add(option);
            slots.Add(slot);
        }
        Component manager = (Component)Get(actor, "SkillManager");
        Call(manager, "RefreshCharacterOwnedLoadout");
        Call(manager, "RebuildResolvedCommandSlots");
        Component field = player.gameObject.AddComponent(T("FieldAllyManager"));
        Set(player, "fieldAllyManager", field);
        GameObject nestedMember = Go("ChianSystem");
        nestedMember.transform.SetParent(actor.transform, false);
        Component member = nestedMember.AddComponent(T("FieldAllyMember"));
        Set(member, "actorContext", actor);
        Set(member, "actorRoot", actor.transform);
        Set(member, "actorRole", Enum.Parse(T("ChainActorRole"), "PartySlot" + column));
        Call(field, "Register", member);
        if (hasTarget) Call(targeting, "CommitTarget", Enemy(Origin + Vector3.forward * 10f));
        object plan = Plan();
        Component visibility = AddVisibility();
        Component hud = Go("HUD command route").AddComponent(T("PartyHudPresenter"));
        Set(hud, "<Player>k__BackingField", player);
        Vector3 before = actor.transform.position;
        Call(hud, "Cast", column, kind);
        Component execution = actor.GetComponent(T("PartyCommandSkillExecution"));
        Assert.That(actor.transform.position, Is.EqualTo(before), "Departure fade must finish before warp.");
        Assert.That((bool)Call(manager, "CanStartCastSlot", kind), Is.True, "Fade must not consume a skill charge.");
        Assert.That((bool)Get(visibility, "IsTransitioning"), Is.True);
        Call(hud, "Cast", column, kind);
        FinishFade(visibility);
        Assert.That((bool)Get(visibility, "IsHidden"), Is.True);
        Call(execution, "Update");
        Assert.That((bool)Call(manager, "CanStartCastSlot", kind), Is.True, "Arrival fade must finish before cast.");
        Assert.That(Get(visibility, "_transition").ToString(), Is.EqualTo("Appear"));
        Call(execution, "Update");
        Assert.That((bool)Call(manager, "CanStartCastSlot", kind), Is.True);
        FinishFade(visibility);
        Call(execution, "Update");
        Assert.That(Vector3.Distance(actor.transform.position, before), Is.GreaterThan(1f));
        Assert.That(actor.transform.position, Is.EqualTo((Vector3)Get(Get(plan, "Placement"), "StartPosition")));
        Assert.That((bool)Call(manager, "CanStartCastSlot", kind), Is.False, "HUD must spend the actual slot charge.");
        Assert.That((bool)Get(Get(actor, "AimTargetDriver"), "HasOverride"), Is.False);
    }

    [Test]
    public void ImmediateCommand_WarpsCastsOnceAndRestoresAim_CooldownRejectsAnotherWarp()
    {
        Component driver = PrepareCaster();
        Type probe = Type.GetType("SucceedingProbePayload, Assembly-CSharp-Editor", true);
        probe.GetField("ExecutionCount").SetValue(null, 0);
        object plan = Plan();
        object result = Start(plan);
        Assert.That((bool)Get(result, "Started"), Is.True);
        Assert.That((int)probe.GetField("ExecutionCount").GetValue(null), Is.EqualTo(1));
        Assert.That((bool)Get(driver, "HasOverride"), Is.False);
        Assert.That(actor.transform.position, Is.EqualTo((Vector3)Get(Get(plan, "Placement"), "StartPosition")));
        Vector3 firstPosition = actor.transform.position;
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Assert.That((bool)Get(Start(Plan()), "Started"), Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(firstPosition));
        Assert.That((int)probe.GetField("ExecutionCount").GetValue(null), Is.EqualTo(1));
    }

    [Test]
    public void ExistingAimOverride_RejectsCommandWithoutMovingOrClearingOtherOwner()
    {
        Component driver = PrepareCaster();
        Call(driver, "SetOverridePoint", Origin + Vector3.left);
        Vector3 position = actor.transform.position;
        Assert.That((bool)Get(Start(Plan()), "Started"), Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(position));
        Assert.That((bool)Get(driver, "HasOverride"), Is.True);
    }

    [Test]
    public void InRangeTarget_PreservesExactPositionAndTurnsToCast()
    {
        PrepareCaster();
        Component member = PrepareFieldMember();
        Component visibility = AddVisibility();
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        actor.transform.position = enemy.transform.position + new Vector3(1.2f, .03f, 0f);
        actor.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        Vector3 before = actor.transform.position;
        object plan = Plan();
        Assert.That((bool)Get(plan, "KeepCurrentPosition"), Is.True);
        Assert.That((Vector3)Get(Get(plan, "Placement"), "StartPosition"), Is.EqualTo(before),
            "NavMesh validation must not snap the current position, including Y.");
        Assert.That((bool)Get(StartField(plan, member), "Started"), Is.True);
        Assert.That((bool)Get(visibility, "IsTransitioning"), Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(before));
        Assert.That(Vector3.Dot(actor.transform.forward, Vector3.left), Is.GreaterThan(.99f));
    }

    [Test]
    public void NearTargetBehindWall_DoesNotKeepCurrentPosition()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        actor.transform.position = enemy.transform.position + Vector3.back * 1.4f;
        GameObject wall = Go("Wall between actor and target");
        wall.transform.position = enemy.transform.position + new Vector3(0f, 1f, -.7f);
        wall.AddComponent<BoxCollider>().size = new Vector3(.6f, 2f, .1f);
        Physics.SyncTransforms();
        Assert.That((bool)Get(Plan(), "KeepCurrentPosition"), Is.False);
    }

    [Test]
    public void InactiveHelperNearTarget_StillUsesSummonPlacement()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        actor.transform.position = enemy.transform.position + Vector3.left;
        actor.gameObject.SetActive(false);
        Assert.That((bool)Get(Plan(), "KeepCurrentPosition"), Is.False);
    }

    [Test]
    public void TargetBeyondCurrentSkillRange_UsesWarpPlacement()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        actor.transform.position = enemy.transform.position + Vector3.back * 4f;
        object plan = Plan();
        Assert.That((bool)Get(plan, "KeepCurrentPosition"), Is.False);
        Assert.That(Vector3.Distance(actor.transform.position,
            (Vector3)Get(Get(plan, "Placement"), "StartPosition")), Is.GreaterThan(1f));
    }

    [Test]
    public void NoTarget_PlacesInFrontOfPlayer_AndCapturesWorldAim()
    {
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        object plan = Plan();
        Vector3 position = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(position - Origin, Vector3.up), Vector3.right * 3f), Is.LessThan(.05f));
        Assert.That((bool)Get(Get(plan, "Target"), "WasAssigned"), Is.False);
        Vector3 aim = (Vector3)Get(plan, "AimPoint");
        player.transform.rotation = Quaternion.identity;
        Assert.That((Vector3)Get(plan, "AimPoint"), Is.EqualTo(aim));
        Assert.That(Vector3.Dot((aim - position).normalized, Vector3.right), Is.GreaterThan(.99f));
    }

    [Test]
    public void Target_UsesAuthoredRange_AndDoesNotRetargetAfterSelectionChanges()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        Call(targeting, "CommitTarget", enemy);
        Set(skill, "partyCommandTargetDistance", 2.5f);
        object plan = Plan();
        object handle = Get(plan, "Target");
        Assert.That((bool)Get(handle, "WasAssigned"), Is.True);
        Vector3 position = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Assert.That(Vector3.ProjectOnPlane(position - enemy.transform.position, Vector3.up).magnitude,
            Is.EqualTo(2.5f).Within(.05f));
        Call(targeting, "CommitTarget", Enemy(Origin + Vector3.right * 5f));
        Assert.That((int)Get(handle, "OriginalInstanceId"), Is.EqualTo(enemy.GetInstanceID()));
    }

    [Test]
    public void RecycledTarget_InvalidatesCapturedCommand()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 8f);
        Call(targeting, "CommitTarget", enemy);
        object plan = Plan();
        Assert.That((bool)Get(plan, "TargetIsValid"), Is.True);
        enemy.gameObject.SetActive(false);
        Assert.That((bool)Get(plan, "TargetIsValid"), Is.False);
        enemy.gameObject.SetActive(true);
        Call(enemy, "OnEnable");
        Assert.That((bool)Get(plan, "TargetIsValid"), Is.False);
    }

    [Test]
    public void MissingNavMesh_RejectsWithoutMovingActor()
    {
        navMesh.Remove();
        Vector3 position = actor.transform.position;
        Plan(false);
        Assert.That(actor.transform.position, Is.EqualTo(position));
    }

    [Test]
    public void LargeTarget_LeavesRoomForStationaryCasterBody()
    {
        Component enemy = Enemy(Origin + Vector3.forward * 10f);
        CapsuleCollider body = enemy.gameObject.AddComponent<CapsuleCollider>();
        body.radius = 2f;
        body.height = 4f;
        body.center = Vector3.up * 2f;
        Component refs = enemy.gameObject.AddComponent(T("CharacterColliderRefs"));
        Set(refs, "CharacterPositionCollider", body);
        Set(enemy, "ColliderRefs", refs);
        Call(targeting, "CommitTarget", enemy);
        Physics.SyncTransforms();
        object plan = Plan();
        Vector3 start = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Assert.That(Vector3.ProjectOnPlane(start - enemy.transform.position, Vector3.up).magnitude,
            Is.GreaterThan(2.5f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActorRootBelowBakedSurface_StillFindsGroundedPlacement(bool hasTarget)
    {
        player.transform.position = Origin + Vector3.down * .15f;
        if (hasTarget)
            Call(targeting, "CommitTarget", Enemy(Origin + new Vector3(0f, -.15f, 8f)));
        CharacterController body = actor.gameObject.AddComponent<CharacterController>();
        body.center = Vector3.up;
        body.height = 2f;
        body.radius = .35f;
        Set(actor, "cc", body);
        GameObject animatedBody = Go("Animated position collider below floor");
        animatedBody.transform.SetParent(actor.transform, false);
        CapsuleCollider positionBody = animatedBody.AddComponent<CapsuleCollider>();
        positionBody.center = Vector3.up * .8f;
        positionBody.height = 2.4f;
        positionBody.radius = .6f;
        Component colliderRefs = actor.gameObject.AddComponent(T("CharacterColliderRefs"));
        Set(colliderRefs, "CharacterPositionCollider", positionBody);
        Set(actor, "ColliderRefs", colliderRefs);
        Physics.SyncTransforms();
        object plan = Plan();
        Vector3 position = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Assert.That(position.y, Is.GreaterThanOrEqualTo(Origin.y));
        Assert.That(NavMesh.SamplePosition(position, out NavMeshHit hit, .1f, NavMesh.AllAreas), Is.True);
        Assert.That(Vector3.Distance(position, hit.position), Is.LessThan(.01f));
    }

    [Test]
    public void BlockedFront_UsesAnotherFrontCandidate()
    {
        GameObject obstacle = Go("Command obstacle");
        obstacle.transform.position = Origin + new Vector3(0f, 1f, 3f);
        obstacle.AddComponent<BoxCollider>().size = new Vector3(.5f, 2f, .5f);
        Physics.SyncTransforms();
        object plan = Plan();
        Vector3 position = (Vector3)Get(Get(plan, "Placement"), "StartPosition");
        Assert.That(Mathf.Abs(position.x - Origin.x), Is.GreaterThan(.4f));
        Assert.That(position.z - Origin.z, Is.GreaterThan(2.5f));
    }

    [Test]
    public void AllFrontCandidatesBlocked_RejectsWithoutMovingActor()
    {
        GameObject obstacle = Go("Command blocking wall");
        obstacle.transform.position = Origin + new Vector3(0f, 1f, 3f);
        obstacle.AddComponent<BoxCollider>().size = new Vector3(8f, 2f, 2f);
        Physics.SyncTransforms();
        Vector3 position = actor.transform.position;
        Plan(false);
        Assert.That(actor.transform.position, Is.EqualTo(position));
    }
}
