using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Typed gameplay fixture, invoked from the dedicated PlayMode test assembly.
public sealed class PlayerSlideTestFixture
{
    readonly List<Object> owned = new();
    GameObject root;
    PlayerContext ctx;
    PlayerMovementCC movement;
    DashSetting dashSettings;
    Camera camera;
    Quaternion oldCameraRotation;
    GlobalTimeScaleManager oldTime;
    TimeSlowManager oldSlow;
    Keyboard keyboard;
    InputSettings oldInputSettings;
    float originalTimeScale;
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    IEnumerator Setup(bool nested = false, bool production = false)
    {
        oldTime = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        oldSlow = Object.FindAnyObjectByType<TimeSlowManager>();
        originalTimeScale = Time.timeScale;
        camera = Camera.main;
        if (!camera)
        {
            var go = Own(new GameObject("Slide test camera"));
            go.tag = "MainCamera";
            camera = go.AddComponent<Camera>();
        }
        oldCameraRotation = camera.transform.rotation;
        camera.transform.rotation = Quaternion.identity;
        var parent = Own(new GameObject("Slide fixture"));
        parent.SetActive(false);
        if (production)
        {
            oldInputSettings = InputSystem.settings;
            var testInputSettings = Own(Object.Instantiate(oldInputSettings));
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputSettings;
            root = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Player/Player.prefab"), parent.transform);
            ctx = root.GetComponentInChildren<PlayerContext>(true);
            root.GetComponentInChildren<CharacterContextPartyLoader>(true).ConfigureRuntimeDefinitionOverride(
                UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterStats>(
                    "Assets/Scripts/CharacterStats/Asosiation/ChaDef.Roma.asset"));
            root.GetComponentInChildren<PlayerInput>(true).enabled = false;
            root.GetComponentInChildren<CharacterVerticalMotor>(true).enabled = false;
        }
        else
        {
            root = new GameObject("Slide actor");
            root.transform.SetParent(parent.transform);
            ctx = root.AddComponent<PlayerContext>();
            ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
            ctx.baseStats.maxHP = ctx.baseStats.maxStamina = 100;
            ctx.stateHub = root.AddComponent<StateHub>();
            ctx.HealthSystem = root.AddComponent<HealthSystem>();
            ctx.HealthSystem.CTX = ctx;
            ctx.HealthSystem.maximumHealth = ctx.HealthSystem.currentHealth = 100;
            GameObject body = root;
            if (nested)
            {
                body = new GameObject("Nested peers");
                body.transform.SetParent(root.transform, false);
            }
            ctx.cc = body.AddComponent<CharacterController>();
            ctx.cc.height = 2f;
            ctx.cc.center = Vector3.up;
            ctx.cc.radius = 0.3f;
            ctx.rb = root.AddComponent<Rigidbody>();
            ctx.rb.isKinematic = true;
            ctx.StaminaSystem = body.AddComponent<StaminaSystem>();
            ctx.DashSystem = body.AddComponent<DashSystem>();
            dashSettings = Own(ScriptableObject.CreateInstance<DashSetting>());
            dashSettings.dashCost = 0;
            dashSettings.dashDuration = 0.06f;
            dashSettings.dashDistance = 0.25f;
            dashSettings.obstacleMask = 0;
            dashSettings.playsPerfectDodgeFeedback = false;
            typeof(DashSystem).GetField("dashSetting", Hidden).SetValue(ctx.DashSystem, dashSettings);
            ctx.movement = body.AddComponent<PlayerMovementCC>();
            ctx.VerticalMotor = body.AddComponent<CharacterVerticalMotor>();
            ctx.VerticalMotor.enabled = false;
        }
        root.transform.position = new Vector3(100, 100, 100);
        parent.SetActive(true);
        yield return null;
        ctx.ResolveReferences();
        movement = ctx.movement;
        ctx.VerticalMotor.enabled = false;
        ctx.StaminaSystem.RecalculateMaximumStamina(true);
        yield return null;
    }

    GameObject Cube(Vector3 offset, Vector3 scale, float yaw = 0f)
    {
        var go = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        go.transform.position = root.transform.position + offset;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        Physics.SyncTransforms();
        return go;
    }

    IEnumerator BeginSlide()
    {
        ctx.moveInput = Vector2.up;
        movement.SetDashHeld(true);
        float until = Time.time + 2;
        while (ctx.DashSystem.IsDashing && Time.time < until) yield return null;
        yield return new WaitForSeconds(0.3f);
        Assert.That(movement.IsSprinting, Is.True);
        Vector3 before = movement.PlanarVelocityWorld;
        movement.SetCrouchHeld(true);
        Assert.That(movement.IsSliding, Is.True, $"Grounded Sprint must enter Slide: speed={before.magnitude}, " +
            $"base={ctx.GetMoveSpeedForCurrentLifeState()}, grounded={ctx.stateHub.IsGrounded}, move={ctx.stateHub.CanMove()}.");
        Assert.That(movement.IsSprinting, Is.False);
        Assert.That(movement.PlanarVelocityWorld, Is.EqualTo(before), "No entry boost or heading snap.");
        ctx.moveInput = Vector2.zero;
    }

    public IEnumerator Run(string scenario)
    {
        yield return Setup(scenario == "NestedPeers" || scenario == "DashEntryNested" || scenario == "StaminaRates",
            scenario == "RomaPrefab" || scenario == "DashEntryRoma" || scenario == "StaminaRoma" ||
            scenario == "ReloadRoma" || scenario == "ReloadEntryRoma");
        if (scenario == "ReloadRoma" || scenario == "ReloadEntryRoma")
        {
            var weapon = ctx.WeaponSystem;
            var gun = Own(Object.Instantiate(weapon.CurrentWeapon));
            gun.reloadTime = 0.6f;
            weapon.Equip(gun, null);
            weapon.magazine = 0;
            weapon.reserveAmmo = weapon.MagazineSize * 2;
            Assert.That(ctx.baseStats.animProfile.reloadBodyMode,
                Is.EqualTo(CharacterAnimProfileSO.ReloadBodyMode.UpperBody));
            if (scenario == "ReloadEntryRoma")
            {
                ctx.moveInput = Vector2.up;
                movement.SetDashHeld(true);
                ctx.stateHub.RequestReload();
                Assert.That(weapon.IsReloading, Is.True);
                movement.SetCrouchHeld(true);
                ctx.moveInput = Vector2.zero;
            }
            else
            {
                yield return BeginSlide();
                Vector3 momentum = movement.PlanarVelocityWorld;
                ctx.stateHub.RequestReload();
                Assert.That(movement.PlanarVelocityWorld, Is.EqualTo(momentum));
            }
            Assert.That(weapon.IsReloading && movement.IsSliding, Is.True);
            Vector3 start = root.transform.position;
            float stamina = ctx.StaminaSystem.Current;
            yield return new WaitForSeconds(0.15f);
            Assert.That(weapon.IsReloading && movement.IsSliding, Is.True);
            Assert.That(Vector3.Distance(root.transform.position, start), Is.GreaterThan(0.1f));
            Assert.That(ctx.StaminaSystem.Current, Is.LessThan(stamina));
            Assert.That(ctx.Visual.animancer.Layers[0].CurrentState.Clip.name, Is.EqualTo("Roma_Slide"));
            Assert.That(ctx.AnimBrain.RootMotionActive, Is.False);
            float deadline = Time.time + weapon.GetReloadAnimDuration() + 1f;
            while (weapon.IsReloading && Time.time < deadline) yield return null;
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.CurrentAmmo, Is.EqualTo(weapon.MagazineSize), "Slide must not cancel ammo refill.");
            yield break;
        }
        if (scenario.StartsWith("Stamina"))
        {
            // Test-specific rates must never mutate the production CharacterStats asset.
            ctx.baseStats = Own(Object.Instantiate(ctx.baseStats));
            if (scenario == "StaminaTinyCosts")
            {
                float before = ctx.StaminaSystem.Current;
                for (int i = 0; i < 10; i++) Assert.That(ctx.StaminaSystem.Spend(0.0001f), Is.True);
                Assert.That(before - ctx.StaminaSystem.Current, Is.EqualTo(0.001f).Within(0.00004f));
                yield break;
            }
            if (scenario == "StaminaEmptyEntry")
            {
                dashSettings.dashDistance = 3f;
                dashSettings.dashDuration = 0.3f;
                ctx.moveInput = Vector2.up;
                movement.SetDashHeld(true);
                ctx.StaminaSystem.Spend(ctx.StaminaSystem.Current);
                movement.SetCrouchHeld(true);
                Assert.That(movement.IsSliding, Is.False);
                Assert.That(ctx.DashSystem.IsDashing, Is.True, "Failed Slide must preserve the active Dash.");
                yield break;
            }
            if (scenario == "StaminaFree")
            {
                ctx.baseStats.slideStaminaPerUnit = 0f;
                ctx.StaminaSystem.Spend(ctx.StaminaSystem.Current);
            }
            yield return BeginSlide();
            float initial = ctx.StaminaSystem.Current;
            if (scenario == "StaminaExhaustion")
            {
                ctx.StaminaSystem.Spend(initial - 0.04f);
                Vector3 start = root.transform.position;
                for (int i = 0; i < 5 && movement.IsSliding; i++) yield return null;
                Assert.That(movement.IsSliding, Is.False);
                Assert.That(movement.IsCrouching, Is.True);
                Assert.That(ctx.StaminaSystem.Current, Is.LessThan(0.0001f));
                Assert.That(Vector3.ProjectOnPlane(root.transform.position - start, Vector3.up).magnitude,
                    Is.LessThanOrEqualTo(0.04f / ctx.baseStats.slideStaminaPerUnit + 0.0001f));
                yield return new WaitForSeconds(1.2f);
                Assert.That(ctx.StaminaSystem.Current, Is.GreaterThan(0f), "Regeneration resumes after its delay.");
                Assert.That(movement.IsSliding, Is.False, "Held Ctrl cannot restart after regeneration.");
            }
            else if (scenario == "StaminaWall")
            {
                Cube(Vector3.forward * 0.7f + Vector3.up, new Vector3(8, 4, 0.15f));
                Vector3 start = root.transform.position;
                float deadline = Time.time + 0.5f;
                while (movement.IsSliding && Time.time < deadline) yield return AssertStaminaDistance();
                Assert.That(movement.IsSliding, Is.False);
                float distance = Vector3.ProjectOnPlane(root.transform.position - start, Vector3.up).magnitude;
                Assert.That(initial - ctx.StaminaSystem.Current,
                    Is.EqualTo(distance * ctx.baseStats.slideStaminaPerUnit).Within(0.002f));
            }
            else
            {
                for (int i = 0; i < 8; i++) yield return AssertStaminaDistance();
                Assert.That(movement.IsSliding, Is.True);
                if (scenario == "StaminaRates")
                {
                    ctx.baseStats = Own(Object.Instantiate(ctx.baseStats));
                    ctx.baseStats.slideStaminaPerUnit = 5f;
                    for (int i = 0; i < 8; i++) yield return AssertStaminaDistance();
                }
                Assert.That(ctx.StaminaSystem.Current, scenario == "StaminaFree"
                    ? Is.EqualTo(initial) : Is.LessThan(initial));
                movement.SetCrouchHeld(false);
                float released = ctx.StaminaSystem.Current;
                yield return new WaitForSeconds(0.2f);
                Assert.That(ctx.StaminaSystem.Current, Is.EqualTo(released), "Walk/release blending is not Slide spending.");
            }
            yield break;
        }
        if (scenario.StartsWith("DashEntry"))
        {
            if (dashSettings != null)
            {
                dashSettings.dashDistance = 3f;
                dashSettings.dashDuration = 0.3f;
            }
            if (scenario == "DashEntryAir")
            {
                ctx.VerticalMotor.enabled = true;
                yield return new WaitForSeconds(0.1f);
                Assert.That(ctx.stateHub.IsGrounded, Is.False);
            }
            if (scenario == "DashEntryWall")
                Cube(Vector3.forward + Vector3.up, new Vector3(8, 4, 0.1f));
            ctx.moveInput = Vector2.up;
            int completed = 0;
            ctx.DashSystem.DashCompleted += _ => completed++;
            movement.SetDashHeld(true);
            if (scenario == "DashEntryWall") yield return new WaitForSeconds(0.15f);
            Assert.That(ctx.DashSystem.IsDashing, Is.True);
            Assert.That(ctx.DashSystem.TryGetPlanarVelocity(out Vector3 entry), Is.True);
            float stamina = ctx.StaminaSystem.Current;
            movement.SetCrouchHeld(true); // Same-frame Ctrl after the first Dash displacement.
            bool rejected = scenario == "DashEntryAir" || scenario == "DashEntryWall";
            Assert.That(movement.IsSliding, Is.EqualTo(!rejected));
            Assert.That(ctx.DashSystem.IsDashing, Is.EqualTo(rejected));
            Assert.That(ctx.StaminaSystem.Current, Is.EqualTo(stamina));
            if (rejected) yield break;
            Assert.That(movement.PlanarVelocityWorld, Is.EqualTo(entry));
            Assert.That(ctx.DashSystem.TryGetPlanarVelocity(out _), Is.False);
            Assert.That((int)typeof(DashSystem).GetField("_dashInvincibilityToken", Hidden).GetValue(ctx.DashSystem), Is.Zero);
            ctx.moveInput = Vector2.zero;
            yield return null;
            yield return null;
            Assert.That(movement.IsSliding, Is.True);
            Assert.That(movement.IsSprinting, Is.False);
            if (scenario == "DashEntryRoma")
            {
                Assert.That(ctx.AnimBrain.RootMotionActive, Is.False);
                Assert.That(ctx.Visual.animancer.Layers[0].CurrentState.Clip.name, Is.EqualTo("Roma_Slide"));
            }
            yield return new WaitForSeconds(0.35f);
            Assert.That(completed, Is.Zero, "Canceled Dash must not deliver a late completion or start Sprint.");
            Assert.That(movement.IsSprinting, Is.False);
            yield break;
        }
        if (scenario == "Crouch" || scenario == "Ceiling" || scenario == "ExternalShape" || scenario == "DashCeiling")
        {
            float height = ctx.cc.height;
            Vector3 foot = ctx.cc.transform.TransformPoint(ctx.cc.center - Vector3.up * height * 0.5f);
            movement.SetCrouchHeld(true);
            Assert.That(movement.IsSliding, Is.False);
            Assert.That(ctx.cc.height, Is.EqualTo(height * 0.6f).Within(0.001f));
            Assert.That(Vector3.Distance(foot, ctx.cc.transform.TransformPoint(ctx.cc.center -
                Vector3.up * ctx.cc.height * 0.5f)), Is.LessThan(0.001f));
            if (scenario == "ExternalShape")
            {
                ctx.cc.height = 0.9f;
                movement.SetCrouchHeld(false);
                Assert.That(ctx.cc.height, Is.EqualTo(0.9f), "Do not overwrite a newer collider owner.");
                yield break;
            }
            GameObject ceiling = null;
            if (scenario != "Crouch") ceiling = Cube(Vector3.up * 1.6f, new Vector3(8, 0.2f, 8));
            if (scenario == "DashCeiling")
            {
                movement.SetDashHeld(true);
                Assert.That(ctx.DashSystem.IsDashing, Is.False);
            }
            ctx.moveInput = Vector2.up;
            yield return new WaitForSeconds(0.1f);
            Assert.That(movement.PlanarVelocityWorld.magnitude,
                Is.LessThanOrEqualTo(ctx.GetMoveSpeedForCurrentLifeState() * 0.46f));
            movement.SetCrouchHeld(false);
            if (ceiling)
            {
                Assert.That(ctx.cc.height, Is.LessThan(height));
                ceiling.SetActive(false);
                Physics.SyncTransforms();
                yield return null;
            }
            Assert.That(ctx.cc.height, Is.EqualTo(height).Within(0.001f));
            yield break;
        }

        if (scenario == "NoAirQueue")
        {
            ctx.VerticalMotor.enabled = true;
            yield return new WaitForSeconds(0.15f);
            Assert.That(ctx.VerticalMotor.IsGrounded, Is.False);
            movement.SetCrouchHeld(true);
            Assert.That(movement.IsSliding, Is.False);
            Cube(Vector3.down * 0.5f, new Vector3(20, 1, 20));
            yield return new WaitForSeconds(0.15f);
            Assert.That(movement.IsSliding, Is.False);
            yield break;
        }

        if (scenario == "Step" || scenario == "Slope")
        {
            Cube(Vector3.down * 0.5f, new Vector3(50, 1, 50));
            ctx.VerticalMotor.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }
        yield return BeginSlide();
        Vector3 heading = movement.PlanarVelocityWorld.normalized;
        float startSpeed = movement.PlanarVelocityWorld.magnitude;
        if (scenario == "Step" || scenario == "Slope")
        {
            Vector3 start = root.transform.position;
            var surface = Cube(new Vector3(0, scenario == "Step" ? 0.075f : 0.25f, 2.5f),
                new Vector3(8, scenario == "Step" ? 0.15f : 0.2f, 4));
            if (scenario == "Slope") surface.transform.rotation = Quaternion.Euler(-10, 0, 0);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.25f);
            Assert.That(movement.IsSliding, Is.True, "Walkable terrain must not count as a wall.");
            Assert.That(root.transform.position.y, Is.GreaterThan(start.y + 0.04f));
            Assert.That(root.transform.position.z, Is.GreaterThan(start.z + 0.6f));
            movement.SetCrouchHeld(false);
            yield return null;
            Assert.That(ctx.cc.height, Is.EqualTo(2f).Within(0.001f), "Standing must work on a walkable surface.");
        }
        else if (scenario == "Pause")
        {
            var time = GlobalTimeScaleManager.Instance;
            int token = time.AcquirePauseToken();
            try
            {
                Vector3 start = root.transform.position;
                yield return null;
                yield return null;
                Assert.That(movement.IsSliding, Is.False);
                Assert.That(root.transform.position, Is.EqualTo(start));
            }
            finally { time.ReleasePauseToken(token); }
            movement.SetCrouchHeld(true);
            Assert.That(movement.IsSliding, Is.False);
        }
        else if (scenario == "Heading" || scenario == "NestedPeers")
        {
            camera.transform.rotation = Quaternion.Euler(40, 90, 0);
            yield return new WaitForSeconds(0.25f);
            Assert.That(Vector3.Angle(heading, movement.PlanarVelocityWorld), Is.LessThan(0.5f));
            Assert.That(movement.PlanarVelocityWorld.magnitude, Is.LessThan(startSpeed));
        }
        else if (scenario == "Steer" || scenario == "Brake" || scenario == "Diagonal")
        {
            ctx.moveInput = scenario == "Brake" ? Vector2.down :
                scenario == "Diagonal" ? Vector2.one : Vector2.right;
            yield return new WaitForSeconds(0.25f);
            Assert.That(movement.IsSliding, Is.True);
            float angle = Vector3.Angle(heading, movement.PlanarVelocityWorld);
            Assert.That(angle, scenario == "Brake" ? Is.LessThan(0.5f) : Is.GreaterThan(1f));
            Assert.That(angle, Is.LessThan(15f), "Steering is limited, not a snap.");
            float expectedMax = startSpeed - ctx.GetMoveSpeedForCurrentLifeState() *
                (scenario == "Brake" ? 1.5f : 0.6f) * 0.25f;
            Assert.That(movement.PlanarVelocityWorld.magnitude, Is.LessThan(expectedMax));
        }
        else if (scenario == "Duration")
        {
            float start = Time.time;
            while (movement.IsSliding && Time.time - start < 2f) yield return null;
            Assert.That(Time.time - start, Is.InRange(1.05f, 1.65f));
            Assert.That(movement.IsSliding, Is.False);
            Assert.That(movement.IsCrouching, Is.True);
            movement.SetCrouchHeld(true);
            Assert.That(movement.IsSliding, Is.False);
            Assert.That(movement.IsSprinting, Is.False);
        }
        else if (scenario == "Release")
        {
            movement.SetCrouchHeld(false);
            yield return null;
            Assert.That(movement.IsSliding, Is.False);
            Assert.That(movement.PlanarVelocityWorld.magnitude, Is.GreaterThan(0f));
            yield return new WaitForSeconds(0.2f);
            Assert.That(movement.PlanarVelocityWorld.magnitude, Is.LessThan(0.01f));
        }
        else if (scenario == "Dash" || scenario == "FailedDash")
        {
            movement.SetDashHeld(false);
            if (scenario == "FailedDash") dashSettings.dashCost = 10000;
            ctx.moveInput = Vector2.right;
            movement.SetDashHeld(true);
            Assert.That(movement.IsSliding, Is.EqualTo(scenario == "FailedDash"));
            if (scenario == "Dash")
            {
                Assert.That(ctx.cc.height, Is.EqualTo(2f));
                yield return new WaitForSeconds(0.2f);
                Assert.That(movement.IsCrouching, Is.True);
                Assert.That(movement.IsSprinting, Is.False);
            }
        }
        else if (scenario == "Wall" || scenario == "AngledWall")
        {
            Vector3 start = root.transform.position;
            Cube(Vector3.forward * 1.2f + Vector3.up, new Vector3(8, 4, 0.15f), scenario == "Wall" ? 0 : 35);
            yield return new WaitForSeconds(0.3f);
            Assert.That(movement.IsSliding, Is.False);
            Assert.That(Mathf.Abs(root.transform.position.x - start.x), Is.LessThan(0.05f));
        }
        else if (scenario == "Reload")
        {
            Vector3 momentum = movement.PlanarVelocityWorld;
            ctx.stateHub.RequestReload(); // No weapon: rejected requests must also preserve Slide.
            Assert.That(movement.IsSliding, Is.True);
            Assert.That(movement.PlanarVelocityWorld, Is.EqualTo(momentum));
            yield return null;
            Assert.That(movement.IsSliding, Is.True);
        }
        else if (scenario == "Fire")
        {
            ctx.stateHub.RequestOnFire();
            yield return null;
            Assert.That(movement.IsSliding, Is.True, "Fire request must not clear slide momentum.");
        }
        else if (scenario == "RomaPrefab")
        {
            ctx.WeaponSystem.OnAim(true);
            ctx.stateHub.RequestOnFire();
            yield return new WaitForSeconds(0.1f);
            Assert.That(movement.IsSliding, Is.True);
            Assert.That(ctx.AnimBrain.IsSliding, Is.True);
            Assert.That(ctx.AnimBrain.RootMotionActive, Is.False);
            var slideClip = ctx.baseStats.animProfile.slide;
            Assert.That(slideClip, Is.Not.Null);
            Assert.That(slideClip.Clip.name, Is.EqualTo("Roma_Slide"));
            Assert.That(slideClip.Clip.isLooping, Is.True);
            Assert.That(ctx.Visual.animancer.Layers[0].CurrentState.Clip, Is.SameAs(slideClip.Clip));
            var slideState = ctx.Visual.animancer.Layers[0].CurrentState;
            float slideTime = slideState.Time;
            yield return new WaitForSeconds(0.15f);
            Assert.That(ctx.Visual.animancer.Layers[0].CurrentState, Is.SameAs(slideState));
            Assert.That(slideState.Time, Is.GreaterThan(slideTime));
            var input = root.GetComponentInChildren<PlayerInput>(true);
            var action = input.actions.FindAction("Crouch", true);
            bool bound = false;
            foreach (var ev in input.actionEvents)
                if (ev.actionId == action.id.ToString())
                    bound = ev.GetPersistentMethodName(0) == "OnCrouch";
            Assert.That(bound, Is.True);
            movement.SetCrouchHeld(false);
            ctx.stateHub.RequestCanceledFire();
            ctx.WeaponSystem.OnAim(false);
            keyboard = InputSystem.AddDevice<Keyboard>();
            input.enabled = true;
            input.SwitchCurrentControlScheme(keyboard);
            input.ActivateInput();
            input.SwitchCurrentActionMap("Player");
            yield return null;
            Assert.That(input.actions.FindAction("Crouch", true).enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.Update();
            yield return null;
            Assert.That(movement.IsCrouching, Is.True, $"pressed={input.actions.FindAction("Crouch").IsPressed()}, " +
                $"held={typeof(PlayerMovementCC).GetField("crouchHeld", Hidden).GetValue(movement)}, " +
                $"needsRelease={typeof(PlayerMovementCC).GetField("crouchNeedsRelease", Hidden).GetValue(movement)}, " +
                $"canMove={ctx.stateHub.CanMove()}, UI={ctx.stateHub.GetActionFacts().UI}, " +
                $"blockedPlayback={ctx.stateHub.GetActionFacts().ShootBlockingPlayback}, height={ctx.cc.height}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            Assert.That(movement.IsCrouching, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightCtrl));
            InputSystem.Update();
            yield return null;
            Assert.That(movement.IsCrouching, Is.True);
        }
        else
        {
            if (scenario == "Suspend") movement.AcquireSuspensionToken();
            if (scenario == "Ui") ctx.stateHub.AcquireUIStateToken(UIStateId.Inventory, "Slide test");
            if (scenario == "Stun") ctx.stateHub.SetStatusEffectControlState(ControlBlockFlags.Move, true);
            if (scenario == "Down") ctx.HealthSystem.Down();
            if (scenario == "Dead") ctx.HealthSystem.Die();
            if (scenario == "Disable") { root.SetActive(false); root.SetActive(true); }
            if (scenario == "Focus") typeof(PlayerMovementCC).GetMethod("OnApplicationFocus", Hidden).Invoke(movement, new object[] { false });
            if (scenario == "Ledge") ctx.VerticalMotor.enabled = true;
            yield return null;
            yield return null;
            Assert.That(movement.IsSliding, Is.False, scenario);
            movement.SetCrouchHeld(true);
            Assert.That(movement.IsSliding, Is.False, "The same held press cannot restart slide.");
        }
    }

    IEnumerator AssertStaminaDistance()
    {
        float before = ctx.StaminaSystem.Current;
        float rate = ctx.baseStats.slideStaminaPerUnit;
        Vector3 start = root.transform.position;
        yield return null;
        float distance = Vector3.ProjectOnPlane(root.transform.position - start, Vector3.up).magnitude;
        Assert.That(before - ctx.StaminaSystem.Current, Is.EqualTo(distance * rate).Within(0.001f),
            "Every frame charges actual planar distance at the current character's rate.");
    }

    public void TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (oldInputSettings != null) InputSystem.settings = oldInputSettings;
        if (camera) camera.transform.rotation = oldCameraRotation;
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        if (!oldTime)
        {
            var value = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
            if (value) Object.DestroyImmediate(value.gameObject);
        }
        if (!oldSlow)
        {
            var value = Object.FindAnyObjectByType<TimeSlowManager>();
            if (value) Object.DestroyImmediate(value.gameObject);
        }
        Time.timeScale = originalTimeScale;
    }
}
