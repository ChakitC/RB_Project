using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GameplayHudAimLayoutTests
{
    GameObject screen;
    GameObject actor;
    GameObject hud;
    PlayerContext player;
    GameplayHudPresenter presenter;
    GameplayHudAimLayout layout;
    RectTransform vital;

    [SetUp]
    public void SetUp()
    {
        screen = new GameObject("AimHudScreen", typeof(RectTransform));
        ((RectTransform)screen.transform).sizeDelta = new Vector2(1920f, 1080f);
        hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Spain/UI/GameplayHUD.prefab"), screen.transform);
        actor = new GameObject("AimHudPlayer");
        player = actor.AddComponent<PlayerContext>();
        player.WeaponSystem = actor.AddComponent<WeaponSystem>();
        player.HealthSystem = actor.AddComponent<HealthSystem>();
        player.HealthSystem.maximumHealth = 100f;
        player.HealthSystem.currentHealth = 10f;
        presenter = hud.AddComponent<GameplayHudPresenter>();
        presenter.Bind(player);
        layout = hud.GetComponent<GameplayHudAimLayout>();
        vital = (RectTransform)hud.transform.Find("PlayerHpStamina");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(screen);
        Object.DestroyImmediate(actor);
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1280f, 720f)]
    public void AimMovesOnlyPlayerStatusBesideAlliesAtOriginalSize(float width, float height)
    {
        ((RectTransform)screen.transform).sizeDelta = new Vector2(width, height);
        var unchanged = new System.Collections.Generic.Dictionary<RectTransform, string>();
        foreach (RectTransform child in hud.transform)
            if (child != vital)
                unchanged.Add(child, EditorJsonUtility.ToJson(child));
        Vector3 originalScale = vital.localScale;
        player.WeaponSystem.OnAim(true);
        Advance(0.09f);
        foreach (var entry in unchanged)
            Assert.That(EditorJsonUtility.ToJson(entry.Key), Is.EqualTo(entry.Value), entry.Key.name);
        Advance(0.18f);
        Rect area = ((RectTransform)hud.transform).rect;
        Bounds health = BoundsOf(vital);
        Assert.That(health.min.x, Is.GreaterThanOrEqualTo(area.xMin));
        Assert.That(vital.anchoredPosition, Is.EqualTo(new Vector2(380f, 240f)));
        Assert.That(vital.localScale, Is.EqualTo(originalScale));
        Assert.That(health.max.x, Is.LessThan(area.xMax));
        Assert.That(health.min.y, Is.GreaterThanOrEqualTo(area.yMin));
        Assert.That(vital.gameObject.activeSelf, Is.True, "Low HP must remain visible while aiming.");
        Assert.That(health.min.x, Is.GreaterThan(BoundsOf(hud.transform.Find("AllyBottom")).max.x));
        foreach (var entry in unchanged)
            Assert.That(EditorJsonUtility.ToJson(entry.Key), Is.EqualTo(entry.Value), entry.Key.name);
        player.WeaponSystem.OnAim(false);
        Advance(0.18f);
        foreach (var entry in unchanged)
            Assert.That(EditorJsonUtility.ToJson(entry.Key), Is.EqualTo(entry.Value), entry.Key.name);
    }

    [Test]
    public void AimReversalAndRebindingRestoreAuthoredPoseWithoutDrift()
    {
        Vector2 normal = vital.anchoredPosition;
        Vector3 scale = vital.localScale;
        player.WeaponSystem.OnAim(true);
        Advance(0.09f);
        Assert.That(vital.anchoredPosition.x, Is.LessThan(normal.x));
        Assert.That(vital.anchoredPosition.x, Is.GreaterThan(380f));
        player.WeaponSystem.OnAim(false);
        Advance(0.09f);
        Assert.That(vital.anchoredPosition, Is.EqualTo(normal));
        Assert.That(vital.localScale, Is.EqualTo(scale));
        player.WeaponSystem.OnAim(true);
        Advance(0.18f);
        presenter.Bind(player);
        presenter.Bind(player);
        Assert.That(vital.localScale, Is.EqualTo(scale));
        layout.enabled = false;
        // EditMode does not dispatch callbacks for ordinary runtime behaviours.
        typeof(GameplayHudAimLayout).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(layout, null);
        Assert.That(vital.anchoredPosition, Is.EqualTo(normal));
        Assert.That(vital.localScale, Is.EqualTo(scale));
        layout.enabled = true;
        layout.Bind(null);
        Assert.That(vital.anchoredPosition, Is.EqualTo(normal));
    }

    void Advance(float seconds) => typeof(GameplayHudAimLayout)
        .GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(layout, new object[] { seconds });

    Bounds BoundsOf(Transform transform)
    {
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);
        var bounds = new Bounds(hud.transform.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            bounds.Encapsulate(hud.transform.InverseTransformPoint(corners[i]));
        return bounds;
    }
}
