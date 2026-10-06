using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Typed fixture for the discoverable Tests assembly (predefined gameplay assembly).
public sealed class PlayerSprintTestFixture
{
    readonly List<Object> owned = new();
    GameObject root;
    PlayerContext ctx;
    PlayerMovementCC movement;
    DashSetting settings;
    Camera camera;
    Quaternion originalCameraRotation;
    GlobalTimeScaleManager oldTime;
    TimeSlowManager oldSlow;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    public IEnumerator Setup(bool nested)
    {
        oldTime = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        oldSlow = Object.FindAnyObjectByType<TimeSlowManager>();
        camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = Own(new GameObject("Sprint test camera"));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }
        originalCameraRotation = camera.transform.rotation;
        camera.transform.rotation = Quaternion.identity;
        root = Own(new GameObject("Sprint test actor"));
        root.SetActive(false);
        root.transform.position = new Vector3(2000, 1000, 2000);
        ctx = root.AddComponent<PlayerContext>();
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        ctx.baseStats.maxHP = 100;
        ctx.baseStats.maxStamina = 100;
        ctx.stateHub = root.AddComponent<StateHub>();
        ctx.HealthSystem = root.AddComponent<HealthSystem>();
        ctx.HealthSystem.CTX = ctx;
        ctx.HealthSystem.maximumHealth = ctx.HealthSystem.currentHealth = 100f;
        GameObject body = root;
        if (nested)
        {
            body = new GameObject("Nested movement peers");
            body.transform.SetParent(root.transform, false);
        }
        ctx.cc = body.AddComponent<CharacterController>();
        ctx.cc.center = Vector3.up;
        ctx.cc.radius = 0.3f;
        ctx.cc.height = 2f;
        ctx.rb = root.AddComponent<Rigidbody>();
        ctx.rb.isKinematic = true;
        ctx.StaminaSystem = body.AddComponent<StaminaSystem>();
        ctx.DashSystem = body.AddComponent<DashSystem>();
        settings = Own(ScriptableObject.CreateInstance<DashSetting>());
        settings.dashCost = 0;
        settings.dashDuration = 0.06f;
        settings.dashDistance = 0.25f;
        settings.dashInvincibleTime = 0.03f;
        settings.obstacleMask = 0;
        settings.playsPerfectDodgeFeedback = false;
        typeof(DashSystem).GetField("dashSetting", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(ctx.DashSystem, settings);
        movement = body.AddComponent<PlayerMovementCC>();
        ctx.movement = movement;
        root.SetActive(true);
        if (ctx.VerticalMotor != null) ctx.VerticalMotor.enabled = false;
        yield return null;
        ctx.moveInput = Vector2.up;
    }

    public void TearDown()
    {
        if (camera) camera.transform.rotation = originalCameraRotation;
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (!oldTime)
        {
            var time = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
            if (time) Object.DestroyImmediate(time.gameObject);
        }
        if (!oldSlow)
        {
            var slow = Object.FindAnyObjectByType<TimeSlowManager>();
            if (slow) Object.DestroyImmediate(slow.gameObject);
        }
    }

    IEnumerator WaitForDash()
    {
        float until = Time.realtimeSinceStartup + 2f;
        while (ctx.DashSystem.IsDashing && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(ctx.DashSystem.IsDashing, Is.False);
        yield return null;
    }

    public IEnumerator Run(string scenario)
    {
        yield return Setup(scenario == "NestedPeers");
        if (scenario == "FailedDash") settings.dashCost = 10000;
        movement.SetDashHeld(true);
        if (scenario == "Tap") movement.SetDashHeld(false);
        if (scenario == "CanceledDash") ctx.DashSystem.CancelDash();
        yield return WaitForDash();
        if (scenario == "Tap" || scenario == "FailedDash" || scenario == "CanceledDash")
        {
            Assert.That(movement.IsSprinting, Is.False);
            yield break;
        }
        Assert.That(movement.IsSprinting, Is.True, "A successful held dash must enter Sprint.");
        Assert.That(ctx.IsSprinting && ctx.stateHub.IsSprinting, Is.True);
        Assert.That(ctx.stateHub.MoveSM.CurrentId, Is.EqualTo(MoveStateId.Sprint));
        Vector3 direction = movement.SprintDirectionWorld;

        if (scenario == "Direction" || scenario == "NestedPeers")
        {
            ctx.moveInput = Vector2.left;
            yield return new WaitForSeconds(0.12f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, direction), Is.LessThan(0.1f),
                "Movement keys must not steer Sprint.");
            Vector3 start = root.transform.position;
            camera.transform.rotation = Quaternion.Euler(45, 90, 0);
            float until = Time.time + 0.25f;
            while (Time.time < until) yield return null;
            Vector3 delta = Vector3.ProjectOnPlane(root.transform.position - start, Vector3.up);
            Assert.That(delta.x, Is.GreaterThan(0f));
            Assert.That(delta.z, Is.GreaterThan(0f), "Camera steering should produce a curved path.");
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, Vector3.right), Is.InRange(0.01f, 85f),
                "Turning should progress toward the camera without snapping.");
            Assert.That(Vector3.Angle(root.transform.forward, movement.SprintDirectionWorld), Is.LessThan(0.1f));
            Assert.That(movement.SprintDirectionWorld.y, Is.EqualTo(0f).Within(0.0001f));
            yield return new WaitForSeconds(0.8f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, Vector3.right), Is.LessThan(1f));
            camera.transform.rotation = Quaternion.Euler(0, -90, 0);
            yield return new WaitForSeconds(1.5f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, Vector3.left), Is.LessThan(1f),
                "A 180-degree reversal must converge without getting stuck.");
            movement.SetDashHeld(false);
            Assert.That(movement.IsSprinting, Is.False);
            yield break;
        }

        if (scenario == "RotationLock")
        {
            int token = ctx.stateHub.AcquireExternalControlBlockToken(ControlBlockFlags.Rotate, "Sprint test");
            camera.transform.rotation = Quaternion.Euler(0, 90, 0);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, direction), Is.LessThan(0.1f));
            ctx.stateHub.ReleaseExternalControlBlockToken(token);
            yield return new WaitForSeconds(0.8f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, Vector3.right), Is.LessThan(3f));
            yield break;
        }

        if (scenario == "YawWrap")
        {
            camera.transform.rotation = Quaternion.Euler(0, 179, 0);
            yield return new WaitForSeconds(1.5f);
            Vector3 before = movement.SprintDirectionWorld;
            camera.transform.rotation = Quaternion.Euler(0, -179, 0);
            yield return new WaitForSeconds(0.4f);
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, before), Is.LessThan(3f));
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, camera.transform.forward), Is.LessThan(1f));
            yield break;
        }

        if (scenario == "Wall" || scenario == "AngledWall")
        {
            ctx.moveInput = Vector2.zero; // Prevent ordinary walking after Sprint has stopped.
            var wall = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = root.transform.position + direction * 1.2f + Vector3.up;
            wall.transform.localScale = new Vector3(8, 4, 0.15f);
            if (scenario == "AngledWall") wall.transform.rotation = Quaternion.Euler(0, 35, 0);
            Physics.SyncTransforms();
            float until = Time.time + 1f;
            while (movement.IsSprinting && Time.time < until) yield return null;
            Assert.That(movement.IsSprinting, Is.False, "Wall must stop sprint including oblique contact.");
            Assert.That(Mathf.Abs(root.transform.position.x - 2000), Is.LessThan(0.05f));
            yield break;
        }

        if (scenario == "Fire")
        {
            ctx.stateHub.RequestOnFire(); // This rig has no usable weapon.
            ctx.stateHub.RequestReload();
            ctx.stateHub.RequestOnMelee();
            yield return null;
            Assert.That(movement.IsSprinting, Is.True, "Rejected combat requests preserve Sprint.");
            yield break;
        }
        if (scenario == "Down") ctx.HealthSystem.Down();
        if (scenario == "Dead") ctx.HealthSystem.Die();
        if (scenario == "Release")
        {
            movement.SetDashHeld(false);
            Vector3 start = root.transform.position;
            ctx.moveInput = Vector2.right;
            yield return null;
            yield return null;
            Assert.That(root.transform.position.x, Is.GreaterThan(start.x));
            Assert.That(Mathf.Abs(root.transform.position.z - start.z), Is.LessThan(0.01f));
            Assert.That(movement.IsSprinting, Is.False);
            yield break;
        }
        if (scenario == "Suspend") movement.AcquireSuspensionToken();
        if (scenario == "Ui") ctx.stateHub.AcquireUIStateToken(UIStateId.Inventory, "Sprint test");
        if (scenario == "Stun") ctx.stateHub.SetStatusEffectControlState(ControlBlockFlags.Move, true);
        if (scenario == "Disable") { root.SetActive(false); root.SetActive(true); }
        yield return null;
        Assert.That(movement.IsSprinting, Is.False);
        movement.SetDashHeld(true);
        yield return null;
        Assert.That(movement.IsSprinting, Is.False, "Interruption cannot auto-resume while still held.");
    }

    public IEnumerator RomaPrefab()
    {
        oldTime = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        oldSlow = Object.FindAnyObjectByType<TimeSlowManager>();
        var parent = Own(new GameObject("Sprint production fixture"));
        parent.SetActive(false);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player/Player.prefab");
        root = Object.Instantiate(prefab, parent.transform);
        root.transform.position = new Vector3(2000, 1000, 2000);
        ctx = root.GetComponentInChildren<PlayerContext>(true);
        var roma = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterStats>(
            "Assets/Scripts/CharacterStats/Asosiation/ChaDef.Roma.asset");
        root.GetComponentInChildren<CharacterContextPartyLoader>(true).ConfigureRuntimeDefinitionOverride(roma);
        var playerInput = root.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true);
        playerInput.enabled = false;
        camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = Own(new GameObject("Sprint production camera"));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }
        originalCameraRotation = camera.transform.rotation;
        camera.transform.rotation = Quaternion.identity;
        parent.SetActive(true);
        yield return null;
        yield return null;
        ctx.ResolveReferences();
        if (ctx.VerticalMotor) ctx.VerticalMotor.enabled = false;
        movement = ctx.movement;
        Assert.That(ctx.baseStats, Is.SameAs(roma));
        Assert.That(ctx.AnimBrain, Is.Not.Null);
        Assert.That(ctx.Visual.animancer.Animator, Is.Not.Null);
        ctx.StaminaSystem.RecalculateMaximumStamina(true);
        ctx.moveInput = Vector2.up;
        movement.SetDashHeld(true);
        Assert.That(ctx.DashSystem.IsDashing, Is.True);
        yield return WaitForDash();
        float until = Time.time + 0.25f;
        while (Time.time < until) yield return null;
        Assert.That(movement.IsSprinting, Is.True);
        Assert.That(ctx.Visual.animancer.Layers[0].CurrentState.Clip, Is.EqualTo(roma.animProfile.sprint.Clip));
        Assert.That(ctx.AnimBrain.RootMotionActive, Is.False);
        float start = ctx.Visual.animancer.Layers[0].CurrentState.Time;
        yield return new WaitForSeconds(0.6f);
        Assert.That(ctx.Visual.animancer.Layers[0].CurrentState.Time, Is.GreaterThan(start + 0.4f));
        var runState = ctx.Visual.animancer.Layers[0].CurrentState;
        foreach (float yaw in new[] { 90f, 180f, 270f })
        {
            camera.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 before = ctx.transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.That(movement.IsSprinting, Is.True);
            Assert.That(ctx.Visual.animancer.Layers[0].CurrentState, Is.SameAs(runState));
            Assert.That(Vector3.Dot(ctx.transform.position - before, camera.transform.forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Angle(movement.SprintDirectionWorld, camera.transform.forward), Is.LessThan(10f));
            Assert.That(Vector3.Angle(ctx.transform.forward, movement.SprintDirectionWorld), Is.LessThan(0.1f));
        }
        movement.SetDashHeld(false);
        yield return null;
        yield return null;
        Assert.That(ctx.Visual.animancer.Layers[0].CurrentState, Is.InstanceOf<Animancer.Vector2MixerState>());
    }
}
