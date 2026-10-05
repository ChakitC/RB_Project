using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public sealed class PlayerPrefabGroupingFixture
{
    const string PrefabPath = "Assets/Prefab/Player/Player.prefab";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    readonly PlayerSlideTestFixture runtime = new();
    bool runtimeStarted;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    PlayerContext CreatePlayer()
    {
        var parent = Own(new GameObject("Grouping fixture"));
        parent.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent.transform);
        var ctx = root.GetComponent<PlayerContext>();
        ctx.ResolveReferences();
        return ctx;
    }

    public void Cleanup()
    {
        if (runtimeStarted) runtime.TearDown();
        runtimeStarted = false;
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    public void LayoutAndInputBindings()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var systems = root.transform.Find("------------ Player_System ------------");
        Assert.That(root.GetComponents<Component>().Length, Is.EqualTo(8));
        Assert.That(systems.Cast<Transform>().Select(t => t.name), Is.EquivalentTo(new[] {
            "GamePlayStats_System", "Movement_System", "Combat_System", "CharacterVisual_System", "Character_Equipment", "Party_System" }));
        Assert.That(root.GetComponentsInChildren<Component>(true).All(c => c != null), Is.True);
        var ctx = root.GetComponent<PlayerContext>();
        var input = root.GetComponentInChildren<PlayerInput>(true);
        Assert.That(input.gameObject, Is.SameAs(ctx.inputHandler.gameObject));
        Assert.That(input.notificationBehavior, Is.EqualTo(PlayerNotifications.InvokeUnityEvents));
        int calls = 0;
        foreach (var action in input.actionEvents)
            for (int i = 0; i < action.GetPersistentEventCount(); i++)
            {
                Assert.That(action.GetPersistentTarget(i), Is.SameAs(ctx.inputHandler));
                Assert.That(typeof(PlayerInputHandler).GetMethod(action.GetPersistentMethodName(i)), Is.Not.Null);
                calls++;
            }
        Assert.That(calls, Is.EqualTo(18));
        Assert.That(ctx.stateHub.GetComponent<StateHubDebugView>(), Is.Not.Null);
        Assert.That(ctx.partyFollowUpController.GetComponent<PartyFollowUpSkillExecutor>(), Is.SameAs(ctx.partyFollowUpExecutor));
    }

    public void ContextResolvesWithoutSerializedPeerBindings()
    {
        var ctx = CreatePlayer();
        var peers = new Component[] { ctx.StatsHub, ctx.stateHub, ctx.SkillManager, ctx.EnegySystem, ctx.inventory,
            ctx.StatusEffects, ctx.PassiveController, ctx.CharacterLoad, ctx.Visibility, ctx.AimRig, ctx.AnimBrain,
            ctx.AnimDriver, ctx.partyFormation, ctx.partyFollowUpController, ctx.partyFollowUpExecutor,
            ctx.thirdPersonAim, ctx.Targeting, ctx.inputHandler, ctx.interruptionController, ctx.DefensiveBlock };
        foreach (var field in typeof(PlayerContext).GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (typeof(Component).IsAssignableFrom(field.FieldType)) field.SetValue(ctx, null);
        ctx.ResolveReferences();
        foreach (var peer in peers)
        {
            Assert.That(peer, Is.Not.Null);
            Assert.That(typeof(PlayerContext).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Any(f => ReferenceEquals(f.GetValue(ctx), peer)), Is.True, peer.GetType().Name);
        }
        Assert.That(ctx.GetComponentsInChildren<CharacterMovementMotor>(true).Length, Is.EqualTo(1));
    }

    public void PickupResolvesRootAndNestedBodies()
    {
        var ctx = CreatePlayer();
        var pickup = Own(new GameObject("Item lookup")).AddComponent<ItemPickup>();
        var method = typeof(ItemPickup).GetMethod("ResolveInventory", Hidden);
        Assert.That(method.Invoke(pickup, new object[] { ctx.cc }), Is.SameAs(ctx.inventory));
        var child = new GameObject("Nested body"); child.transform.SetParent(ctx.transform, false);
        var collider = child.AddComponent<BoxCollider>();
        Assert.That(method.Invoke(pickup, new object[] { collider }), Is.SameAs(ctx.inventory));
        Assert.That(CharacterContextModuleLookup.ResolveStatusEffects(ctx.gameObject), Is.SameAs(ctx.StatusEffects));
        Assert.That(CharacterContextModuleLookup.ResolveStatusEffects(child), Is.SameAs(ctx.StatusEffects));
    }

    public void StatsIncludeSiblingProvidersAndExcludeOtherActors()
    {
        var ctx = CreatePlayer();
        var neighbour = new GameObject("Different actor"); neighbour.transform.SetParent(ctx.transform, false);
        neighbour.AddComponent<EnemyContext>();
        var foreign = neighbour.AddComponent<StatusEffectController>();
        typeof(StatsHub).GetMethod("ResolveReferences", Hidden).Invoke(ctx.StatsHub, null);
        ctx.StatsHub.RebuildModifierProviders();
        var providers = (IList)typeof(StatsHub).GetField("_modifierProviders", Hidden).GetValue(ctx.StatsHub);
        foreach (var provider in new Component[] { ctx.PassiveController, ctx.StatusEffects, ctx.AccessoryLoadout,
            ctx.WeaponSystem.GetComponent<WeaponAffixRuntimeController>(), ctx.WeaponSystem.GetComponent<WeaponUpgradeRuntimeController>() })
            Assert.That(providers.Contains(provider), Is.True, provider.GetType().Name);
        Assert.That(providers.Contains(foreign), Is.False);
    }

    public void GuardUsesActorPoseWithOffsetHost()
    {
        var ctx = CreatePlayer();
        ctx.transform.SetPositionAndRotation(new Vector3(11, 2, 5), Quaternion.Euler(0, 72, 0));
        var block = ctx.DefensiveBlock;
        typeof(DefensiveBlockController).GetMethod("Awake", Hidden).Invoke(block, null);
        Vector3 center = block.GuardCenter;
        block.transform.localPosition = new Vector3(4, 6, 2);
        block.transform.localRotation = Quaternion.Euler(0, 110, 0);
        Assert.That(Vector3.Distance(block.GuardCenter, center), Is.LessThan(.0001f));
    }

    public IEnumerator RuntimeBindingsAndVisibility()
    {
        runtimeStarted = true;
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden)
            .Invoke(runtime, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(runtime);
        Assert.That(ctx.inputHandler, Is.Not.Null);
        Assert.That(ctx.interruptionController, Is.Not.Null);
        Assert.That(typeof(InterruptionCommandController).GetField("playerInterruptionController", Hidden)
            .GetValue(ctx.interruptionCommand), Is.SameAs(ctx.interruptionController));
        Assert.That(ctx.partyFormation, Is.Not.Null);
        Assert.That(ctx.partyFollowUpController, Is.Not.Null);
        Assert.That(ctx.partyFollowUpExecutor, Is.Not.Null);
        Assert.That(ctx.AnimBrain.GetComponent<Animancer.AnimancerComponent>(), Is.Not.Null);
        Assert.That(typeof(CharacterVisibilityController).GetField("characterVfx", Hidden).GetValue(ctx.Visibility), Is.Not.Null);
        ctx.Visibility.SetHiddenImmediate();
        yield return null;
        Assert.That(ctx.gameObject.activeSelf, Is.True);
        ctx.Visibility.SetVisibleImmediate();
        var effect = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        effect.effectId = "grouping.runtime-pickup";
        var pickup = Own(new GameObject("Status pickup")).AddComponent<StatusEffectPickUp>();
        typeof(StatusEffectPickUp).GetField("effect", Hidden).SetValue(pickup, effect);
        typeof(StatusEffectPickUp).GetMethod("OnTriggerEnter", Hidden).Invoke(pickup, new object[] { ctx.cc });
        Assert.That(ctx.StatusEffects.HasEffect(effect), Is.True);
        ctx.gameObject.SetActive(false);
        ctx.gameObject.SetActive(true);
        yield return null;
        Assert.That(ctx.GetComponentsInChildren<StateHubDebugView>(true).Length, Is.EqualTo(1));
        Assert.That(ctx.GetComponentsInChildren<PartyFollowUpSkillExecutor>(true).Length, Is.EqualTo(1));
        Assert.That(ctx.Visibility.gameObject.name, Is.EqualTo("CharacterVisual_System"));
    }
}
