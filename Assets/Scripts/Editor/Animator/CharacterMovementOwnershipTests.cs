#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class CharacterMovementOwnershipTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new();
    readonly Vector3 origin = new(2200f, 600f, 2200f);
    NavMeshData data;
    NavMeshDataInstance mesh;
    GameObject actor;
    NavMeshAgent agent;
    CharacterMovementMotor motor;

    // NavMeshAgent only joins the navigation simulation in Play Mode. Run the lifecycle matrix
    // in one temporary empty scene, with a fresh mesh/actor per scenario and one domain reload.
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator NavigationAndAdapterLifecycleMatrix()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        yield return new UnityEngine.TestTools.EnterPlayMode();
        var scenarios = new System.Action[]
        {
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(0, 1, 2),
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(0, 2, 1),
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(1, 0, 2),
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(1, 2, 0),
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(2, 0, 1),
            () => OverlappingOwnersRestoreOnlyAfterLastRelease(2, 1, 0),
            OriginalStoppedAndAxisSettingsSurvive,
            StopOnlyPauseDoesNotTakeTransformAxes,
            AutonomyRequestWaitsForOtherOwners,
            () => DeferredRestoreSurvivesDisabledOrOffMeshAgent(false),
            () => DeferredRestoreSurvivesDisabledOrOffMeshAgent(true),
            InitiallyDisabledAgentCanBeSuspendedBeforeActivation,
            PoolingAndStaleReleaseDoNotReleaseNewOwner,
            RebindingNavAdapterRestoresCapturedAgent,
            GameplayAdaptersShareOneNavigationBaseline,
            RuntimeContextCreatesOneMotorOutsideReplaceableModel,
            () => RootMotionYOwnershipFollowsPolicyAndAdapterLifetime(false),
            () => RootMotionYOwnershipFollowsPolicyAndAdapterLifetime(true)
        };
        var failures = new List<string>();
        for (int i = 0; i < scenarios.Length; i++)
        {
            try { SetUp(); scenarios[i](); }
            catch (System.Exception error) { failures.Add($"Scenario {i}: {error}"); }
            finally { TearDown(); }
        }
        // Check before the reload; Unity's runner records failures and restores Edit Mode.
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

    public void SetUp()
    {
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(origin + Vector3.down * .1f, Quaternion.identity, Vector3.one),
                size = new Vector3(20f, .2f, 20f), area = 0 }
        };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(origin, new Vector3(24f, 8f, 24f)), Vector3.zero, Quaternion.identity);
        mesh = NavMesh.AddNavMeshData(data);
        actor = Create("Movement actor");
        agent = actor.AddComponent<NavMeshAgent>();
        agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        Assert.That(agent.Warp(origin), Is.True);
        Assert.That(agent.isOnNavMesh, Is.True);
        agent.isStopped = false;
        motor = actor.AddComponent<CharacterMovementMotor>();
        motor.Configure(null, actor.transform, agent: agent);
    }

    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i]) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        mesh.Remove();
        if (data) Object.DestroyImmediate(data);
    }

    public void OverlappingOwnersRestoreOnlyAfterLastRelease(int a, int b, int c)
    {
        int[] tokens = { motor.AcquireAgentSuspension(agent), motor.AcquireAgentSuspension(agent),
            motor.AcquireAgentSuspension(agent) };
        motor.ReleaseAgentSuspension(tokens[a]);
        AssertSuspended();
        motor.ReleaseAgentSuspension(tokens[b]);
        AssertSuspended();
        motor.ReleaseAgentSuspension(tokens[c]);
        AssertRestored(false, true, true);
    }

    public void OriginalStoppedAndAxisSettingsSurvive()
    {
        agent.isStopped = true;
        agent.updateRotation = false;
        int token = motor.AcquireAgentSuspension(agent);
        motor.ReleaseAgentSuspension(token);
        AssertRestored(true, true, false);
    }

    public void StopOnlyPauseDoesNotTakeTransformAxes()
    {
        int pause = motor.AcquireAgentSuspension(agent, false, false);
        AssertRestored(true, true, true);
        int motion = motor.AcquireAgentSuspension(agent);
        motor.ReleaseAgentSuspension(motion);
        AssertRestored(true, true, true);
        motor.ReleaseAgentSuspension(pause);
        AssertRestored(false, true, true);
    }

    public void AutonomyRequestWaitsForOtherOwners()
    {
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        int knockback = motor.AcquireAgentSuspension(agent);
        int airborne = motor.AcquireAgentSuspension(agent);
        motor.ReleaseAgentSuspension(knockback, true);
        AssertSuspended();
        motor.ReleaseAgentSuspension(airborne);
        AssertRestored(false, true, true);
    }

    public void DeferredRestoreSurvivesDisabledOrOffMeshAgent(bool offMesh)
    {
        int token = motor.AcquireAgentSuspension(agent);
        agent.enabled = false;
        if (offMesh)
        {
            actor.transform.position = origin + Vector3.up * 50f;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { agent.enabled = true; }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }
        motor.ReleaseAgentSuspension(token);
        Assert.That(agent.updatePosition, Is.True);
        agent.enabled = false;
        actor.transform.position = origin;
        agent.enabled = true;
        Assert.That(motor.PlaceOnNavMesh(origin), Is.True);
        AssertRestored(false, true, true);
    }

    public void InitiallyDisabledAgentCanBeSuspendedBeforeActivation()
    {
        agent.enabled = false;
        int token = motor.AcquireAgentSuspension(agent);
        agent.enabled = true;
        motor.PlaceOnNavMesh(origin);
        AssertSuspended();
        motor.ReleaseAgentSuspension(token);
        AssertRestored(false, true, true);
    }

    public void PoolingAndStaleReleaseDoNotReleaseNewOwner()
    {
        int stale = motor.AcquireAgentSuspension(agent);
        actor.SetActive(false);
        actor.SetActive(true);
        motor.PlaceOnNavMesh(origin);
        AssertRestored(false, true, true);
        int current = motor.AcquireAgentSuspension(agent);
        Assert.That(current, Is.Not.EqualTo(stale));
        motor.ReleaseAgentSuspension(stale);
        AssertSuspended();
        motor.ReleaseAgentSuspension(current);
        AssertRestored(false, true, true);
    }

    public void RebindingNavAdapterRestoresCapturedAgent()
    {
        var model = Create("Model");
        model.transform.SetParent(actor.transform);
        var adapter = model.AddComponent<RootMotionNavMeshDriver>();
        adapter.Configure(null, agent, null, null, actor.transform);
        Policy(adapter, new RootMotionPolicy(true, false, false, false));
        AssertSuspended();
        var replacementActor = Create("Replacement actor");
        replacementActor.AddComponent<CharacterMovementMotor>();
        var replacement = replacementActor.AddComponent<NavMeshAgent>();
        replacement.agentTypeID = agent.agentTypeID;
        Assert.That(replacement.Warp(origin), Is.True);
        adapter.Configure(null, replacement, null, null, replacementActor.transform);
        AssertRestored(false, true, true);
        Assert.That(replacement.updatePosition, Is.False);
        adapter.enabled = false;
        Assert.That(replacement.updatePosition, Is.True);
        Assert.That(replacement.isStopped, Is.False);
    }

    public void RootMotionYOwnershipFollowsPolicyAndAdapterLifetime(bool controllerAdapter)
    {
        var model = Create("Model");
        model.transform.SetParent(actor.transform);
        MonoBehaviour adapter;
        if (controllerAdapter)
        {
            var cc = actor.AddComponent<CharacterController>();
            var driver = model.AddComponent<RootMotionCCDriver>();
            driver.Configure(null, cc, null);
            adapter = driver;
        }
        else
        {
            var driver = model.AddComponent<RootMotionNavMeshDriver>();
            driver.Configure(null, agent, null, null, actor.transform);
            adapter = driver;
        }
        Policy(adapter, new RootMotionPolicy(true, false, false, false));
        Assert.That(motor.RootMotionOwnsY, Is.True);
        adapter.GetType().GetField("zeroY", Hidden).SetValue(adapter, true);
        Policy(adapter, new RootMotionPolicy(true, false, false, false));
        Assert.That(motor.RootMotionOwnsY, Is.False);
        adapter.GetType().GetField("zeroY", Hidden).SetValue(adapter, false);
        Policy(adapter, new RootMotionPolicy(true, true, false, false));
        Assert.That(motor.RootMotionOwnsY, Is.False);
        Policy(adapter, new RootMotionPolicy(true, false, false, false));
        adapter.enabled = false;
        Assert.That(motor.RootMotionOwnsY, Is.False);
        adapter.enabled = true;
        adapter.GetType().GetMethod("Update", Hidden).Invoke(adapter, null);
        Assert.That(motor.RootMotionOwnsY, Is.True);
        Object.DestroyImmediate(model);
        Assert.That(motor.RootMotionOwnsY, Is.False);
    }

    public void GameplayAdaptersShareOneNavigationBaseline()
    {
        var context = actor.AddComponent<EnemyContext>();
        var vertical = actor.AddComponent<CharacterVerticalMotor>();
        typeof(CharacterVerticalMotor).GetField("mode", Hidden).SetValue(vertical, CharacterVerticalMode.AgentDriven);
        vertical.Launch(5f);
        var knockback = actor.AddComponent<CharacterKnockbackMotor>();
        typeof(CharacterKnockbackMotor).GetMethod("EnableAgentOverride", Hidden).Invoke(knockback, null);
        var model = Create("Root-motion model");
        model.transform.SetParent(actor.transform);
        var adapter = model.AddComponent<RootMotionNavMeshDriver>();
        adapter.Configure(null, agent, null, context, actor.transform);
        Policy(adapter, new RootMotionPolicy(true, true, false, false));
        adapter.enabled = false;
        AssertSuspended();
        typeof(CharacterKnockbackMotor).GetMethod("RestoreAgentOverride", Hidden).Invoke(knockback, new object[] { false });
        Assert.That(agent.isStopped, Is.True);
        Assert.That(agent.updatePosition, Is.False);
        vertical.enabled = false;
        AssertRestored(false, true, true);
    }

    public void RuntimeContextCreatesOneMotorOutsideReplaceableModel()
    {
        var root = Create("Runtime context");
        root.SetActive(false);
        var context = root.AddComponent<EnemyContext>();
        context.ResolveReferences();
        var resolved = context.MovementMotor;
        Assert.That(resolved, Is.Not.Null);
        var model = Create("Temporary model");
        model.transform.SetParent(root.transform);
        context.ResolveReferences();
        Object.DestroyImmediate(model);
        context.ResolveReferences();
        Assert.That(context.MovementMotor, Is.SameAs(resolved));
        Assert.That(resolved.transform, Is.EqualTo(context.transform));
        Assert.That(root.GetComponents<CharacterMovementMotor>().Length, Is.EqualTo(1));
    }

    void AssertSuspended() => AssertRestored(true, false, false);
    void AssertRestored(bool stopped, bool position, bool rotation)
    {
        Assert.That(agent.isStopped, Is.EqualTo(stopped));
        Assert.That(agent.updatePosition, Is.EqualTo(position));
        Assert.That(agent.updateRotation, Is.EqualTo(rotation));
    }
    GameObject Create(string name)
    {
        var result = new GameObject(name);
        result.transform.position = origin;
        objects.Add(result);
        return result;
    }
    static void Policy(MonoBehaviour adapter, RootMotionPolicy policy) =>
        adapter.GetType().GetMethod("OnRootMotionPolicyChanged", Hidden).Invoke(adapter, new object[] { policy });
}
#endif
