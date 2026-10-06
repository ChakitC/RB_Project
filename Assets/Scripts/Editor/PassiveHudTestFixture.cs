using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PassiveHudTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly PassiveCompositionTestFixture actors = new();
    readonly List<Object> owned = new();
    public void Setup() => actors.Setup();
    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); actors.Cleanup();
    }
    CharacteContext Actor() => (CharacteContext)typeof(PassiveCompositionTestFixture).GetMethod("Actor", Hidden).Invoke(actors, new object[] { false });
    ComposedPassiveDef Roma(bool withView = true)
    {
        var definition = RomaPassiveExample.Create(owned);
        if (withView) definition.hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PassiveHudPrefabBuilder.ViewPath).GetComponent<PassiveHudView>();
        return definition;
    }
    PassiveHudHost Host(CharacteContext actor)
    {
        var root = new GameObject("HUD test", typeof(RectTransform)); owned.Add(root);
        var host = root.AddComponent<PassiveHudHost>(); host.Bind(actor); return host;
    }
    static PassiveRuntime Runtime(CharacteContext actor)
    { var buffer = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(buffer); return buffer.Single(); }

    [Test] public void OptionalHudDoesNotCreateGameplayStateOrPolling()
    {
        var actor = Actor(); var host = Host(actor);
        Assert.That(host.ViewCount, Is.Zero);
        Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero);
        actor.PassiveController.SetRuntimePassives(new[] { Roma(false) });
        Assert.That(host.ViewCount, Is.Zero);
        var definition = Roma(); actor.PassiveController.SetRuntimePassives(new[] { definition });
        Assert.That(host.ViewCount, Is.EqualTo(1));
        Assert.That(actor.PassiveController.RuntimeStateCount, Is.Zero, "Reading initial UI values must not allocate variables.");
        foreach (var type in new[] { typeof(PassiveHudHost), typeof(RomaPassiveHudView) })
            Assert.That(type.GetMethod("Update", Hidden), Is.Null);
    }
    [Test] public void DuplicateGrantsAndOwnersRemainIndependentAndRefreshPreservesViews()
    {
        var actor = Actor(); var definition = Roma();
        var a = new PassiveGrant(definition, "test", "a"); var b = new PassiveGrant(definition, "test", "b");
        actor.PassiveController.SetRuntimeGrants(new[] { a, b }); var host = Host(actor);
        var grants = new List<PassiveRuntime>(); actor.PassiveController.AppendRuntimes(grants);
        Assert.That(host.ViewCount, Is.EqualTo(2));
        grants[0].WriteVariable("momentum", definition.recipe.variables.Single(), 25);
        var view = (RomaPassiveHudView)host.GetView(grants[0]);
        Assert.That(view.DisplayedMomentum, Is.EqualTo(25));
        Assert.That(((RomaPassiveHudView)host.GetView(grants[1])).DisplayedMomentum, Is.Zero);
        actor.PassiveController.RefreshPassiveLoadout(); Assert.That(host.GetView(grants[0]), Is.SameAs(view));
        var other = Actor(); other.PassiveController.SetRuntimeGrants(new[] { a }); host.Bind(other);
        Assert.That(host.ViewCount, Is.EqualTo(1));
        Assert.That(((RomaPassiveHudView)host.GetView(Runtime(other))).DisplayedMomentum, Is.Zero);
        grants[0].WriteVariable("momentum", definition.recipe.variables.Single(), 50);
        Assert.That(((RomaPassiveHudView)host.GetView(Runtime(other))).DisplayedMomentum, Is.Zero);
        other.PassiveController.SetRuntimeGrants(new PassiveGrant[0]); Assert.That(host.ViewCount, Is.Zero);
    }
    [Test] public void ReservationCancellationCommitAndActualRestoreAreDisplayed()
    {
        var actor = Actor(); actor.PassiveController.SetRuntimePassives(new[] { Roma() });
        var host = Host(actor); var view = (RomaPassiveHudView)host.GetView(Runtime(actor));
        actor.StaminaSystem.Spend(40);
        Assert.That(view.DisplayedMomentum, Is.EqualTo(40));
        Assert.That(view.DisplayedNextAttackPercent, Is.EqualTo(80));
        Assert.That(view.DisplayedDamagePercent, Is.EqualTo(20).Within(0.001f));
        using (var attack = actor.PassiveController.ReserveNextAttack("cancel"))
        {
            Assert.That(view.DisplayedMomentum, Is.Zero); Assert.That(view.Feedback, Does.Contain("RESERVED"));
            actor.StaminaSystem.Spend(5);
        }
        Assert.That(view.DisplayedMomentum, Is.EqualTo(45)); Assert.That(view.Feedback, Does.Contain("RETURNED"));
        using (var attack = actor.PassiveController.ReserveNextAttack("commit")) attack.Commit();
        Assert.That(view.DisplayedMomentum, Is.Zero); Assert.That(view.Feedback, Does.Contain("RELEASED"));
        actor.StaminaSystem.Restore(40); // 95/100: restore must report actual 5, not requested 10.
        actor.CombatEventBus.Publish(actor.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            metadata: new CombatEventMetadata(dashExecutionId: 771)));
        Assert.That(view.Feedback, Is.EqualTo("PERFECT DASH  +5 STA"));
        Assert.That(view.DisplayedStaminaRatio, Is.EqualTo(1));
        Assert.That(view.DisplayedDamagePercent, Is.Zero);
        // Non-ExecuteAlways components need explicit lifecycle calls in Edit Mode.
        host.gameObject.SetActive(false); Call(host, "OnDisable"); Assert.That(host.ViewCount, Is.Zero);
        host.gameObject.SetActive(true); Call(host, "OnEnable"); Assert.That(host.ViewCount, Is.EqualTo(1));
        actor.gameObject.SetActive(false); Call(actor.PassiveController, "OnDisable"); Assert.That(host.ViewCount, Is.Zero);
        actor.gameObject.SetActive(true);
        actor.PassiveController.RefreshPassiveLoadout();
        Assert.That(host.ViewCount, Is.EqualTo(1));
        Assert.That(((RomaPassiveHudView)host.GetView(Runtime(actor))).DisplayedMomentum, Is.Zero);
    }
    static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden).Invoke(target, null);
    [Test] public void ProductionDefinitionAndHudPrefabReferencesAreComplete()
    {
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(PassiveHudPrefabBuilder.DefinitionPath);
        Assert.That(definition.hudPrefab, Is.TypeOf<RomaPassiveHudView>());
        foreach (var ring in definition.hudPrefab.GetComponentsInChildren<PassiveGaugeArc>(true))
            Assert.That(ring.GetComponent<CanvasRenderer>(), Is.Not.Null, ring.name);
        var so = new SerializedObject(definition.hudPrefab);
        foreach (string field in new[] { "staminaRing", "momentumRing", "momentumText", "staminaText", "nextAttackText", "damageText", "feedbackText" })
            Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(PassiveHudPrefabBuilder.HudPath);
        Assert.That(hud.GetComponentsInChildren<PassiveHudHost>(true).Length, Is.EqualTo(1));
        Assert.That(hud.transform.Find("PassiveHUD")?.GetComponent<PassiveHudHost>(), Is.Not.Null,
            "The passive spawn point must be directly editable under GameplayHUD.");
        Assert.That(AssetDatabase.GetDependencies(PassiveHudPrefabBuilder.ViewPath).Any(p => p.Contains("/Tests/")), Is.False);
    }

    [Test] public void CombatSurgeHudShowsChargeReadyAndCountdown()
    {
        var actor = Actor();
        actor.PassiveController.SetRuntimePassives(new[] { AssetDatabase.LoadAssetAtPath<PassiveDefinition>(RomaCombatSurgeAuthoring.Path) });
        var runtime = Runtime(actor); var host = Host(actor);
        var view = (RomaPassiveHudView)host.GetView(runtime);
        actor.StaminaSystem.Spend(99);
        Assert.That(view.DisplayedMomentum, Is.EqualTo(99)); Assert.That(view.IsMomentumReady, Is.False);
        actor.StaminaSystem.Spend(1);
        Assert.That(view.IsMomentumReady, Is.True);
        var shot = new WeaponShotBuildContext { PassiveContext = actor.CombatEventBus.CreateExternalContext(PassiveEventType.ShotFired) };
        using (var phase = actor.PassiveController.PrepareWeaponShot(ref shot)) phase.Commit();
        Assert.That(view.IsMomentumReady, Is.False);
        Assert.That(view.DisplayedMomentum, Is.Zero);
        Assert.That(view.DisplayedSurgeRemaining, Is.EqualTo(5));
        actor.PassiveController.Tick(2);
        Assert.That(view.DisplayedSurgeRemaining, Is.EqualTo(3));
        actor.PassiveController.Tick(3);
        Assert.That(view.DisplayedSurgeRemaining, Is.Zero);
        actor.StaminaSystem.Restore(100); actor.StaminaSystem.Spend(25);
        Assert.That(view.DisplayedMomentum, Is.EqualTo(25));
    }
}
