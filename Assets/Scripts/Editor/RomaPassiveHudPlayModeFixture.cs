using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RomaPassiveHudPlayModeFixture
{
    readonly PlayerSlideTestFixture player = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject canvasObject;
    public IEnumerator Run()
    {
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden).Invoke(player, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(player);
        canvasObject = new GameObject("Roma HUD Runtime Check", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PassiveHudPrefabBuilder.HudPath), canvas.transform);
        var presenter = hud.GetComponent<GameplayHudPresenter>();
        if (presenter == null) presenter = hud.AddComponent<GameplayHudPresenter>();
        presenter.Bind(ctx);
        var host = hud.GetComponentInChildren<PassiveHudHost>(true);
        Assert.That(host.ViewCount, Is.EqualTo(1));
        var grants = new List<PassiveRuntime>(); ctx.PassiveController.AppendRuntimes(grants);
        var view = (RomaPassiveHudView)host.GetView(grants.Single(g => g.Grant.Definition.hudPrefab != null));
        ctx.StaminaSystem.RecalculateMaximumStamina(true);
        ctx.StaminaSystem.Spend(ctx.StaminaSystem.Max * 0.4f);
        Assert.That(view.DisplayedStaminaRatio, Is.EqualTo(0.6f).Within(0.001));
        Assert.That(view.DisplayedDamagePercent, Is.EqualTo(20).Within(0.001));
        Assert.That(view.DisplayedNextAttackPercent, Is.EqualTo(ctx.StaminaSystem.Max * 0.8f).Within(0.001));
        yield return null;
        Canvas.ForceUpdateCanvases();
        foreach (var ring in view.GetComponentsInChildren<PassiveGaugeArc>())
        {
            var mesh = ring.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, ring.name);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), ring.name);
        }
        var corners = new Vector3[4]; ((RectTransform)view.transform).GetWorldCorners(corners);
        Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(0)); Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width));
        Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(0)); Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height));
        string folder = Path.GetFullPath("../BuildArtifacts/PassiveHud"); Directory.CreateDirectory(folder);
        yield return new WaitForEndOfFrame();
        var capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(folder, "RomaPassiveHUD-runtime.png"), capture.EncodeToPNG()); Object.Destroy(capture);
        var attack = ctx.PassiveController.ReserveNextAttack("hud-production-cancel");
        Assert.That(view.DisplayedMomentum, Is.Zero);
        ctx.StaminaSystem.Spend(2); attack.Dispose();
        Assert.That(view.DisplayedMomentum, Is.EqualTo(ctx.StaminaSystem.Max * 0.4f + 2).Within(0.001));
        canvasObject.SetActive(false); Assert.That(host.ViewCount, Is.Zero);
        canvasObject.SetActive(true); Assert.That(host.ViewCount, Is.EqualTo(1));
    }
    public void Cleanup()
    { if (canvasObject != null) Object.DestroyImmediate(canvasObject); player.TearDown(); }
}
