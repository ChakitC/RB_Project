using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StateHubArchitectureTests
{
    readonly List<Object> owned = new();
    GameObject root;
    PlayerContext ctx;
    StateHub hub;
    HealthSystem health;
    WeaponSystem weapon;
    GunConfig gun;

    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    public void SetUp()
    {
        root = Own(new GameObject("StateHub architecture test"));
        root.SetActive(false);
        ctx = root.AddComponent<PlayerContext>();
        hub = root.AddComponent<StateHub>();
        health = root.AddComponent<HealthSystem>();
        ctx.stateHub = hub; ctx.HealthSystem = health; health.CTX = ctx;
        health.maximumHealth = health.currentHealth = 100f;
    }
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; --i) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    // Only lifecycle setup is invoked in Edit Mode. State changes always use owner commands.
    void Initialize() => typeof(StateHub).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hub, null);
    static CharacterActionFacts Ready => new()
    {
        Initialized = true, Life = LifeStateId.Alive, UI = UIStateId.Normal,
        HasWeapon = true, HasDash = true, HasMelee = true, HasSkill = true
    };

    public void AdmissionMatrix()
    {
        foreach (CharacterAction action in Enum.GetValues(typeof(CharacterAction)))
        {
            var facts = Ready;
            Assert.That(CharacterActionPolicy.Evaluate(action, facts).Allowed, Is.True, action.ToString());
            facts.Initialized = false;
            Assert.That(CharacterActionPolicy.Evaluate(action, facts).Reason, Is.EqualTo(ActionBlockReason.NotInitialized));
            facts = Ready; facts.UI = UIStateId.Inventory;
            bool menuAllowed = action == CharacterAction.Reload || action == CharacterAction.Rotate;
            Assert.That(CharacterActionPolicy.Evaluate(action, facts).Allowed, Is.EqualTo(menuAllowed), action.ToString());
            facts = Ready; facts.Life = LifeStateId.Down;
            Assert.That(CharacterActionPolicy.Evaluate(action, facts).Allowed,
                Is.EqualTo(action == CharacterAction.Move || action == CharacterAction.Rotate), action.ToString());
        }
        var f = Ready;
        f.Blocks = ControlBlockFlags.Skill;
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.Melee, f).Allowed, Is.True);
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.Skill, f).Reason, Is.EqualTo(ActionBlockReason.ControlBlocked));
        f = Ready; f.Reloading = f.FullBodyReload = f.CanInterruptReload = true;
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.Move, f).Allowed, Is.False);
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.Dash, f).Allowed, Is.True);
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.FireRequest, f).Allowed, Is.True);
        Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.Shoot, f).Reason, Is.EqualTo(ActionBlockReason.Reloading));
        foreach (string restriction in new[] { "Stunned", "Dashing", "Knockback", "Melee", "EmptyMagazine" })
        {
            object boxed = f;
            typeof(CharacterActionFacts).GetField(restriction).SetValue(boxed, true);
            Assert.That(CharacterActionPolicy.Evaluate(CharacterAction.FireRequest, (CharacterActionFacts)boxed).Allowed, Is.False, restriction);
        }
    }

    public void TokensBeforeAwakeAndIndependentSources()
    {
        Assert.That(hub.CanMove(), Is.False);
        hub.RequestOnFire();
        Assert.That(hub.DesiredFireHeld, Is.True);
        Assert.That(hub.FireHeld, Is.False);
        int a = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Shoot, "A");
        int b = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move, "B");
        Assert.That(hub.AcquireExternalControlBlockToken(ControlBlockFlags.None), Is.Zero);
        hub.ReleaseExternalControlBlockToken(a);
        Initialize();
        Assert.That(hub.CanMove(), Is.False);
        hub.SetExternalControlState(ControlBlockFlags.Skill, false);
        hub.SetReactionControlState(ControlBlockFlags.Shoot, true);
        hub.SetReactionControlState(ControlBlockFlags.None, false);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Move | ControlBlockFlags.Skill));
        hub.ReleaseExternalControlBlockToken(b); hub.ReleaseExternalControlBlockToken(b); hub.ReleaseExternalControlBlockToken(0);
        Assert.That(hub.CanMove(), Is.True);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Skill));
    }

    public void ReadOnlyViewsAndQueriesHaveNoSideEffects()
    {
        Initialize();
        int count = root.GetComponentsInChildren<Component>(true).Length;
        var view = hub.MoveSM;
        Assert.That(view, Is.Not.InstanceOf<StateMachine<MoveStateId, CharacteContext>>());
        Assert.That(view.GetType().GetMethod("TryChange"), Is.Null);
        Assert.That(view.GetType().GetProperty("CurrentId").CanWrite, Is.False);
        Assert.That(typeof(StateMachine<MoveStateId, CharacteContext>).GetProperty("CurrentId").GetSetMethod(), Is.Null);
        for (int i = 0; i < 5; ++i)
            foreach (CharacterAction action in Enum.GetValues(typeof(CharacterAction))) hub.GetAdmission(action);
        Assert.That(root.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(count));
        Assert.That(hub.CanMove(), Is.True);
        Assert.That(hub.CanShoot(), Is.False, "Missing optional weapon only blocks weapon actions.");
    }

    public void LifeCommandsPublishOnceBeforeReturning()
    {
        Initialize();
        int down = 0, dead = 0, revived = 0, hubDead = 0, changes = 0;
        health.CharacterDown += () => down++;
        health.CharacterDead += () => dead++;
        health.CharacterRevive += () => revived++;
        hub.Died += () => hubDead++;
        hub.LifeStateChanged += (_, to) => { changes++; Assert.That(hub.LifeSM.CurrentId, Is.EqualTo(to)); Assert.That(health.LifeState, Is.EqualTo(to)); };
        health.Down(); health.Down();
        Assert.That(hub.Isdown && hub.CanMove(), Is.True);
        health.Revive(); health.Revive();
        Assert.That(health.currentHealth, Is.EqualTo(25f));
        health.Die(); health.Die();
        Assert.That(hub.LifeSM.CurrentId, Is.EqualTo(LifeStateId.Dead));
        Assert.That(health.IsDead && !health.IsAlive, Is.True);
        Assert.That(new[] { down, dead, revived, hubDead, changes }, Is.EqualTo(new[] { 1, 1, 1, 1, 3 }));
        health.Revive();
        Assert.That(health.IsDead, Is.True);
    }

    public void UiTokensComposeAndDoNotCreateTimeManagers()
    {
        Initialize();
        int inventory = hub.AcquireUIStateToken(UIStateId.Inventory, "Inventory");
        int pauseA = hub.AcquireUIStateToken(UIStateId.Pause, "A");
        int pauseB = hub.AcquireUIStateToken(UIStateId.Pause, "B");
        hub.ReleaseUIStateToken(pauseA);
        Assert.That(hub.UISM.CurrentId, Is.EqualTo(UIStateId.Pause));
        hub.ReleaseUIStateToken(pauseB);
        Assert.That(hub.UISM.CurrentId, Is.EqualTo(UIStateId.Inventory));
        hub.ReleaseUIStateToken(inventory); hub.ReleaseUIStateToken(inventory);
        Assert.That(hub.UISM.CurrentId, Is.EqualTo(UIStateId.Normal));
    }

    public void FaultingPresentationDoesNotAbortDeath()
    {
        Initialize();
        int ownerDeaths = 0, hubDeaths = 0, laterListeners = 0;
        hub.LifeSM.OnChanged += (_, _) => throw new InvalidOperationException("Faulting state view listener");
        hub.LifeStateChanged += (_, _) => throw new InvalidOperationException("Faulting life presentation");
        hub.LifeStateChanged += (_, _) => laterListeners++;
        hub.Died += () => hubDeaths++;
        health.CharacterDead += () => ownerDeaths++;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Faulting life presentation"));
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Faulting state view listener"));
        Assert.DoesNotThrow(health.Die);
        health.Die();
        Assert.That(health.IsDead, Is.True);
        Assert.That(health.currentHealth, Is.Zero);
        Assert.That(hub.LifeSM.CurrentId, Is.EqualTo(LifeStateId.Dead));
        Assert.That(new[] { ownerDeaths, hubDeaths, laterListeners }, Is.EqualTo(new[] { 1, 1, 1 }));
    }

    public void RootAndChildPeersPreserveExplicitBindings()
    {
        Object.DestroyImmediate(health);
        var child = new GameObject("Nested health"); child.transform.SetParent(root.transform);
        health = child.AddComponent<HealthSystem>();
        ctx.HealthSystem = null;
        ctx.ResolveReferences();
        Assert.That(ctx.HealthSystem, Is.SameAs(health));
        var other = root.AddComponent<HealthSystem>();
        ctx.HealthSystem = health;
        ctx.ResolveReferences();
        Assert.That(ctx.HealthSystem, Is.SameAs(health));
        Assert.That(other, Is.Not.SameAs(health));
    }

    public IEnumerator RuntimeReloadAndLifecycle()
    {
        Assert.That(Application.isPlaying, Is.True);
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        ctx.baseStats.maxHP = 100f;
        weapon = root.AddComponent<WeaponSystem>();
        ctx.WeaponSystem = weapon; weapon.ctx = ctx;
        gun = Own(ScriptableObject.CreateInstance<GunConfig>());
        gun.maxMagazine = 8; gun.magazine = 3; gun.maxReserveAmmo = 32;
        gun.magazineReload = true; gun.reloadTime = 0.08f; gun.shootInterruptsReload = true;
        gun.BulletPrefab = Own(new GameObject("Test projectile"));
        gun.BulletPrefab.SetActive(false);
        gun.BulletPrefab.AddComponent<SphereCollider>();
        gun.BulletPrefab.AddComponent<Projectile>();
        weapon.projectileConfig = Own(ScriptableObject.CreateInstance<ProjectileConfig>());
        weapon.projectileConfig.lifeTime = 0.05f;
        Own(ProjectilePool.Instance.gameObject);
        weapon.firePoint = root.transform;
        root.SetActive(true);
        yield return null;
        var view = root.GetComponent<StateHubDebugView>(); if (view) view.Configure(false, false, false, Vector2.zero);
        foreach (FiringMode mode in Enum.GetValues(typeof(FiringMode)))
        {
            gun.firingModes = mode;
            weapon.Equip(gun, null);
            weapon.magazine = 3; // Existing public ammo authoring mirror; commands synchronize it.
            int starts = 0; Action<float> onReload = _ => starts++;
            hub.ReloadStarted += onReload;
            hub.RequestReload(); hub.RequestReload();
            Assert.That(weapon.IsReloading, Is.True, mode.ToString());
            Assert.That(hub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Reloading));
            Assert.That(starts, Is.EqualTo(1));
            hub.RequestOnFire();
            Assert.That(weapon.IsReloading, Is.False);
            if (mode == FiringMode.Semi) Assert.That(weapon.magazine, Is.EqualTo(2), "Semi fires in the cancelling command.");
            hub.RequestCanceledFire();
            hub.ReloadStarted -= onReload;
        }
        gun.firingModes = FiringMode.Auto; weapon.Equip(gun, null); weapon.magazine = 3;
        weapon.projectilePrefab = null; // Observe resumed intent without draining the completed magazine.
        hub.RequestReload(); hub.SetDesiredFireHeld(true);
        yield return null;
        Assert.That(weapon.IsReloading, Is.True, "Held intent must not cancel reload.");
        yield return new WaitForSeconds(0.15f);
        Assert.That(weapon.IsReloading, Is.False);
        Assert.That(weapon.IsFiringHeld, Is.True, "Held intent resumes after reload finishes.");
        hub.RequestCanceledFire();
        // A subscriber cancels immediately: the routine handle must already exist and be stopped.
        weapon.magazine = 3;
        int callbacks = 0;
        Action<float> cancelInCallback = _ => { callbacks++; weapon.CancelReload(); };
        hub.ReloadStarted += cancelInCallback;
        hub.RequestReload();
        Assert.That(callbacks, Is.EqualTo(1));
        Assert.That(weapon.IsReloading, Is.False);
        Assert.That(hub.WeaponSM.CurrentId, Is.EqualTo(WeaponStateId.Ready));
        hub.ReloadStarted -= cancelInCallback;
        yield return new WaitForSeconds(0.12f);
        Assert.That(weapon.magazine, Is.EqualTo(3), "Cancelled coroutine must not refill later.");
        foreach (string restriction in new[] { "Config", "Empty", "Stun", "Menu", "ShootLock" })
        {
            gun.shootInterruptsReload = restriction != "Config";
            weapon.Equip(gun, null); weapon.magazine = restriction == "Empty" ? 0 : 3;
            hub.RequestReload();
            int uiToken = restriction == "Menu" ? hub.AcquireUIStateToken(UIStateId.Inventory) : 0;
            int lockToken = restriction == "ShootLock" ? hub.AcquireExternalControlBlockToken(ControlBlockFlags.Shoot) : 0;
            if (restriction == "Stun") hub.SetStatusEffectControlState(ControlBlockFlags.None, true);
            hub.RequestOnFire();
            Assert.That(weapon.IsReloading, Is.True, restriction);
            Assert.That(hub.FireHeld, Is.False, restriction);
            hub.RequestCanceledFire(); weapon.CancelReload();
            hub.ReleaseUIStateToken(uiToken); hub.ReleaseExternalControlBlockToken(lockToken);
            hub.SetStatusEffectControlState(ControlBlockFlags.None, false);
        }
        int hold = hub.AcquireExternalControlBlockToken(ControlBlockFlags.Move, "Persistent owner");
        health.Down(); root.SetActive(false); root.SetActive(true);
        Assert.That(health.IsDown, Is.True, "Enable must retain Down.");
        Assert.That(hub.DesiredFireHeld, Is.False);
        Assert.That(hub.ActiveControlBlockFlags, Is.EqualTo(ControlBlockFlags.Move));
        health.Revive(); hub.ReleaseExternalControlBlockToken(hold);
        health.Die(); root.SetActive(false); root.SetActive(true);
        Assert.That(health.IsDead && hub.LifeSM.CurrentId == LifeStateId.Dead, Is.True);
    }
    public IEnumerator RuntimeUiAndDashOwnership()
    {
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        ctx.baseStats.maxHP = 100; ctx.baseStats.maxStamina = 100;
        ctx.baseStats.animProfile = Own(ScriptableObject.CreateInstance<CharacterAnimProfileSO>());
        ctx.cc = root.AddComponent<CharacterController>();
        ctx.rb = root.AddComponent<Rigidbody>(); ctx.rb.isKinematic = true;
        ctx.StaminaSystem = root.AddComponent<StaminaSystem>();
        ctx.DashSystem = root.AddComponent<DashSystem>();
        var dashSettings = Own(ScriptableObject.CreateInstance<DashSetting>());
        dashSettings.obstacleMask = 0;
        typeof(DashSystem).GetField("dashSetting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ctx.DashSystem, dashSettings);
        weapon = root.AddComponent<WeaponSystem>(); ctx.WeaponSystem = weapon; weapon.ctx = ctx;
        weapon.firePoint = root.transform;
        gun = Own(ScriptableObject.CreateInstance<GunConfig>());
        gun.maxMagazine = 8; gun.magazine = 3; gun.maxReserveAmmo = 32;
        gun.magazineReload = true; gun.reloadTime = 5;
        root.SetActive(true); yield return null;
        var view = root.GetComponent<StateHubDebugView>(); if (view) view.Configure(false, false, false, Vector2.zero);
        weapon.Equip(gun, null); weapon.magazine = 3;
        ctx.baseStats.animProfile.reloadBodyMode = CharacterAnimProfileSO.ReloadBodyMode.FullBody;
        hub.RequestReload();
        Assert.That(hub.CanMove(), Is.False);
        dashSettings.dashCost = 1000;
        hub.RequestOnDash();
        Assert.That(weapon.IsReloading, Is.True, "Failed dash cannot cancel FullBody reload.");
        dashSettings.dashCost = 1;
        hub.RequestOnDash();
        Assert.That(ctx.DashSystem.IsDashing, Is.True);
        Assert.That(hub.MoveSM.CurrentId, Is.EqualTo(MoveStateId.Dash));
        Assert.That(weapon.IsReloading, Is.False);
        ctx.DashSystem.CancelDash();
        Assert.That(hub.MoveSM.CurrentId, Is.EqualTo(MoveStateId.Stand));
        ctx.baseStats.animProfile.reloadBodyMode = CharacterAnimProfileSO.ReloadBodyMode.UpperBody;
        hub.RequestReload(); hub.RequestOnDash();
        Assert.That(weapon.IsReloading, Is.True, "UpperBody reload survives dash.");
        ctx.DashSystem.CancelDash(); weapon.CancelReload();
        var first = Own(new GameObject("First screen")).AddComponent<ActiveSkillScreenController>();
        var second = Own(new GameObject("Second screen")).AddComponent<ActiveSkillScreenController>();
        var time = GlobalTimeScaleManager.Instance; Own(time.gameObject);
        int dialogue = time.AcquirePauseToken();
        first.BindRuntime(ctx); second.BindRuntime(ctx);
        first.Open(); second.Open();
        first.Close();
        Assert.That(time.IsPaused && hub.UISM.CurrentId == UIStateId.Pause, Is.True);
        second.gameObject.SetActive(false);
        Assert.That(time.IsPaused, Is.True, "Dialogue still owns a pause.");
        Assert.That(hub.UISM.CurrentId, Is.EqualTo(UIStateId.Normal));
        time.ReleasePauseToken(dialogue);
        Assert.That(time.IsPaused, Is.False);
        first.Open();
        Object.DestroyImmediate(root);
        Assert.DoesNotThrow(first.Close, "Screen cleanup accepts a destroyed actor.");
        Assert.That(time.IsPaused, Is.False);
    }

}
