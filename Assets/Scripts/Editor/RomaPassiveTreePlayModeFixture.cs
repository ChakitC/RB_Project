using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RomaPassiveTreePlayModeFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly PlayerSlideTestFixture player = new();
    readonly List<Object> owned = new();
    readonly List<Projectile> projectiles = new();
    ActiveSkillScreenController screen;
    float oldScale;
    static float Value(PassiveRuntime runtime, string id) { runtime.TryGetVariable(id, out float value, out _); return value; }
    public IEnumerator Run()
    {
        oldScale = Time.timeScale;
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden).Invoke(player, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(player);
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(RomaCombatSurgeAuthoring.Path);
        var model = new ActiveSkillProgressModel(ctx.baseStats, new CharacterProgressData { skillPoints = 12, skillProgressInitialized = true }, 1);
        const string slot = "roma.passive.stamina-momentum", option = "roma.stamina-momentum";
        foreach (var node in definition.upgradeTree.nodes) Assert.That(model.TryUnlock(slot, option, definition.upgradeTree, node.nodeId, out _), Is.True);
        // Test-only in-memory progress. Do not write the user's character save.
        typeof(CharacterActiveSkillProgress).GetField("_model", Hidden).SetValue(ctx.ActiveSkillProgress, model);
        typeof(CharacterActiveSkillProgress).GetMethod("NotifyChanged", Hidden).Invoke(ctx.ActiveSkillProgress, new object[] { slot, option });
        var runtimes = new List<PassiveRuntime>(); ctx.PassiveController.AppendRuntimes(runtimes);
        var runtime = runtimes.Single(r => r.Grant.Definition == definition);
        Assert.That(runtime.Grant.Upgrades.UpgradeIds.Count, Is.EqualTo(9));

        var canvasObject = new GameObject("Roma tree preview", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler)); owned.Add(canvasObject);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/Active Skill/ActiveSkillScreen.prefab"), canvasObject.transform);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero; panelRect.localScale = Vector3.one;
        screen = panel.GetComponent<ActiveSkillScreenController>(); screen.BindRuntime(ctx); screen.Open();
        var session = (ActiveSkillLoadoutSession)typeof(ActiveSkillScreenController).GetField("_session", Hidden).GetValue(screen);
        int slotIndex = Enumerable.Range(0, session.Slots.Count).Single(i => session.Slots[i].SlotId == slot);
        typeof(ActiveSkillScreenController).GetMethod("SelectSlot", Hidden).Invoke(screen, new object[] { slotIndex });
        typeof(ActiveSkillScreenController).GetMethod("SelectNode", Hidden).Invoke(screen, new object[] { RomaPassiveTreeAuthoring.Pressure });
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(panel.GetComponentsInChildren<ActiveSkillUpgradeNodeView>().Length, Is.EqualTo(9));
        Assert.That(panelRect.rect.width, Is.GreaterThan(1000));
        var detail = panel.GetComponentInChildren<ActiveSkillNodeDetailPanel>();
        var description = (TMPro.TMP_Text)typeof(ActiveSkillNodeDetailPanel).GetField("descriptionText", Hidden).GetValue(detail);
        foreach (var node in definition.upgradeTree.nodes)
        {
            typeof(ActiveSkillScreenController).GetMethod("SelectNode", Hidden).Invoke(screen, new object[] { node.nodeId });
            description.ForceMeshUpdate();
            Assert.That(description.isTextOverflowing, Is.False, node.nodeId);
        }
        typeof(ActiveSkillScreenController).GetMethod("SelectNode", Hidden).Invoke(screen, new object[] { RomaPassiveTreeAuthoring.Pressure });
        yield return new WaitForEndOfFrame();
        string folder = Path.GetFullPath("../BuildArtifacts/RomaPassiveTree"); Directory.CreateDirectory(folder);
        var capture = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(folder, "Tree.png"), capture.EncodeToPNG()); Object.Destroy(capture);
        screen.Close(); canvasObject.SetActive(false);

        var enemy = new GameObject("Roma tree target"); owned.Add(enemy); enemy.AddComponent<EnemyContext>();
        var weapon = ctx.WeaponSystem;
        var sourceGun = weapon.CurrentWeapon != null ? weapon.CurrentWeapon : AssetDatabase.FindAssets("t:GunConfig")
            .Select(g => AssetDatabase.LoadAssetAtPath<GunConfig>(AssetDatabase.GUIDToAssetPath(g))).First(g => g.BulletPrefab != null);
        foreach (bool perBullet in new[] { false, true })
        {
            var gun = Object.Instantiate(sourceGun); owned.Add(gun);
            gun.magazine = gun.maxMagazine = 10; gun.maxReserveAmmo = 100; gun.infiniteReserveAmmo = false;
            gun.autoloader = false; gun.reloadPerBullet = perBullet; gun.magazineReload = !perBullet;
            gun.reloadTime = .7f; gun.startInsertDelay = .14f; gun.perBulletInsertTime = .07f; gun.endInsertDelay = .14f;
            weapon.Equip(gun, WeaponInstanceFactory.CreatePlainInstance(gun));
            ctx.StaminaSystem.RecalculateMaximumStamina(true);
            while (Value(runtime, "momentum") < 100)
            { ctx.StaminaSystem.Restore(ctx.StaminaSystem.Max); ctx.StaminaSystem.Spend(Mathf.Min(ctx.StaminaSystem.Max, 100 - Value(runtime, "momentum"))); }
            ctx.StaminaSystem.Restore(ctx.StaminaSystem.Max); ctx.StaminaSystem.Spend(ctx.StaminaSystem.Max * .6f);
            typeof(WeaponSystem).GetField("nextFireTime", Hidden).SetValue(weapon, 0f);
            var beforeProjectiles = new HashSet<Projectile>(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None));
            Assert.That(weapon.TryShoot(), Is.True);
            projectiles.AddRange(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Where(p => !beforeProjectiles.Contains(p)));
            Assert.That(Value(runtime, "redline-floor"), Is.EqualTo(36).Within(.01));
            Assert.That(ctx.StatsHub.GetReloadSpeedMultiplier(), Is.EqualTo(1.4f).Within(.001));
            Assert.That(ctx.StatsHub.GetAimMovementMultiplier(.6f), Is.EqualTo(.86f).Within(.001));
            weapon.magazine = 0; weapon.reserveAmmo = 20;
            weapon.TryReload(); Assert.That(weapon.IsReloading, Is.True);
            int beforeAmmo = weapon.magazine;
            ctx.CombatEventBus.Publish(ctx.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
                metadata: new CombatEventMetadata(dashExecutionId: perBullet ? 902UL : 901UL)));
            Assert.That(weapon.magazine, Is.EqualTo(beforeAmmo + 3));
            Assert.That(weapon.magazine + weapon.reserveAmmo, Is.EqualTo(20));
            Assert.That(Value(runtime, "slip-uses"), Is.EqualTo(1));
            for (int i = 0; i < 5; i++) ctx.CombatEventBus.Publish(ctx.CombatEventBus.CreateExternalContext(PassiveEventType.Hit,
                target: enemy, attackId: "production-" + i, metadata: new CombatEventMetadata(sourceKind: CombatSourceKind.Weapon, appliedDamage: 1)));
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out float duration), Is.GreaterThan(5)); Assert.That(duration, Is.EqualTo(6));
            ctx.StaminaSystem.Restore(ctx.StaminaSystem.Max);
            var modifiers = new List<RuntimeStatModifier>(); runtime.AppendModifiers(modifiers);
            Assert.That(modifiers.Where(m => m.StatType == StatType.Damage).Sum(m => m.Value), Is.EqualTo(36).Within(.01));
            float timeout = Time.realtimeSinceStartup + 5;
            while (weapon.IsReloading && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(weapon.magazine, Is.EqualTo(10)); Assert.That(weapon.reserveAmmo, Is.EqualTo(10));
            float remaining = runtime.GetVariableExpiryRemaining("surge-active", out _);
            Time.timeScale = 0; yield return null; yield return null;
            Assert.That(runtime.GetVariableExpiryRemaining("surge-active", out _), Is.EqualTo(remaining).Within(.001));
            Time.timeScale = .2f; yield return new WaitForSeconds(.1f);
            Assert.That(remaining - runtime.GetVariableExpiryRemaining("surge-active", out _), Is.InRange(.08f, .2f));
            Time.timeScale = 1; ctx.PassiveController.Tick(8);
            Assert.That(Value(runtime, "redline-floor"), Is.Zero);
        }
        ctx.PassiveController.enabled = false; ctx.PassiveController.enabled = true;
        runtimes.Clear(); ctx.PassiveController.AppendRuntimes(runtimes); runtime = runtimes.Single(r => r.Grant.Definition == definition);
        Assert.That(Value(runtime, "surge-active"), Is.Zero); Assert.That(Value(runtime, "momentum"), Is.Zero);
    }
    public void Cleanup()
    {
        if (screen != null) screen.Close();
        Time.timeScale = oldScale;
        foreach (var projectile in projectiles) if (projectile != null && projectile.gameObject.activeInHierarchy) projectile.DespawnForRoomTransition();
        projectiles.Clear();
        foreach (var value in owned) if (value != null) Object.DestroyImmediate(value); owned.Clear();
        player.TearDown();
    }
}
