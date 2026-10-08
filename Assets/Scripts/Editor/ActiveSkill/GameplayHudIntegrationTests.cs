using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class GameplayHudIntegrationTests
{
    const string HudPath = "Assets/Spain/UI/GameplayHUD.prefab";
    const string ConfigPath = "Assets/Data/Party/DefaultPartySpawnConfig.asset";

    [Test]
    public void SpawnedPlayerUiReferencesGameplayHud()
    {
        PartySpawnConfigSO config = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(ConfigPath);
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);

        Assert.That(config, Is.Not.Null);
        Assert.That(hud, Is.Not.Null);
        Assert.That(config.PlayerUIPrefab, Is.Not.Null);

        PlayerUIRuntimeBinder binder = config.PlayerUIPrefab.GetComponent<PlayerUIRuntimeBinder>();
        Assert.That(binder, Is.Not.Null);
        Assert.That(new SerializedObject(binder).FindProperty("gameplayHudPrefab").objectReferenceValue,
            Is.SameAs(hud));
    }

    [Test]
    public void BoundPlayerHealthUpdatesGameplayHud()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        Assert.That(prefab, Is.Not.Null);

        GameObject hud = Object.Instantiate(prefab);
        GameObject actor = new GameObject("HudTestPlayer");
        try
        {
            PlayerContext player = actor.AddComponent<PlayerContext>();
            HealthSystem health = actor.AddComponent<HealthSystem>();
            health.currentHealth = 25f;
            health.maximumHealth = 100f;
            player.HealthSystem = health;

            GameplayHudPresenter presenter = hud.AddComponent<GameplayHudPresenter>();
            presenter.Bind(player);

            Transform vital = hud.transform.Find("PlayerHpStamina");
            Assert.That(vital, Is.Not.Null);
            UnityEngine.UI.Image fill = FindChild(vital, "HP_Bar_Progress Bar Fill")
                .GetComponent<UnityEngine.UI.Image>();
            Assert.That(fill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Simple));
            SlantedBarFillEffect slantedFill = fill.GetComponent<SlantedBarFillEffect>();
            Assert.That(slantedFill, Is.Not.Null);
            Assert.That(slantedFill.FillAmount, Is.EqualTo(0.25f).Within(0.001f));
            AssertHealthMeshFollowsBar(fill, slantedFill);
            Assert.That(FindChild(vital, "Stamina_ProgressBar")
                .GetComponent<SlantedBarFillEffect>(), Is.Not.Null);
            Assert.That(FindChild(vital, "CurrentHPText").GetComponent<TMP_Text>().text, Is.EqualTo("25"));
            Assert.That(FindChild(vital, "MaxHPText").GetComponent<TMP_Text>().text, Is.EqualTo("100"));
            Assert.That(hud.transform.Find("AllyTop").gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void AllyPortraitTracksLateCharacterDataAndChangesOnSameContext()
    {
        GameObject hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        GameObject actor = new GameObject("HudTestPlayer");
        GameObject allyObject = new GameObject("HudTestAlly");
        CharacterStats first = ScriptableObject.CreateInstance<CharacterStats>();
        CharacterStats second = ScriptableObject.CreateInstance<CharacterStats>();
        Texture2D texture = new Texture2D(2, 2);
        Sprite firstIcon = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        Sprite secondIcon = Sprite.Create(texture, new Rect(1, 1, 1, 1), Vector2.zero);
        try
        {
            PlayerContext player = actor.AddComponent<PlayerContext>();
            player.fieldAllyManager = actor.AddComponent<FieldAllyManager>();
            AllyContext ally = allyObject.AddComponent<AllyContext>();
            FieldAllyMember member = allyObject.AddComponent<FieldAllyMember>();
            var serialized = new SerializedObject(member);
            serialized.FindProperty("actorRole").intValue = (int)ChainActorRole.PartySlot1;
            serialized.FindProperty("actorContext").objectReferenceValue = ally;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.fieldAllyManager.Register(member);
            GameplayHudPresenter presenter = hud.AddComponent<GameplayHudPresenter>();
            presenter.Bind(player);
            var portrait = hud.transform.Find("AllyTop/PortraitMask/PortraitIcon")
                .GetComponent<UnityEngine.UI.Image>();
            Assert.That(portrait.enabled, Is.False);

            first.icon = firstIcon;
            ally.baseStats = first;
            RefreshHud(presenter);
            Assert.That(portrait.sprite, Is.SameAs(firstIcon));
            Assert.That(portrait.enabled, Is.True);

            second.icon = secondIcon;
            ally.baseStats = second;
            RefreshHud(presenter);
            Assert.That(portrait.sprite, Is.SameAs(secondIcon));

            second.icon = null;
            RefreshHud(presenter);
            Assert.That(portrait.sprite, Is.Null);
            Assert.That(portrait.enabled, Is.False);

            second.icon = firstIcon;
            RefreshHud(presenter);
            Assert.That(portrait.sprite, Is.SameAs(firstIcon));
            Assert.That(portrait.enabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(allyObject);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(firstIcon);
            Object.DestroyImmediate(secondIcon);
            Object.DestroyImmediate(texture);
        }
    }

    static void RefreshHud(GameplayHudPresenter presenter) =>
        typeof(GameplayHudPresenter).GetMethod("Refresh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(presenter, null);

    static void AssertHealthMeshFollowsBar(UnityEngine.UI.Image image,
        SlantedBarFillEffect effect)
    {
        Rect rect = image.rectTransform.rect;
        Color32 color = new Color32(255, 255, 255, 255);
        using (var mesh = new UnityEngine.UI.VertexHelper())
        {
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);

            effect.ModifyMesh(mesh);
            var vertices = new List<UnityEngine.UIVertex>();
            mesh.GetUIVertexStream(vertices);
            Assert.That(vertices.Count, Is.GreaterThan(0));

            Vector2 axis = new Vector2(-0.47416f, 0.88044f).normalized;
            float limit = Mathf.Lerp(-0.22423f, 0.47894f, 0.25f);
            foreach (UnityEngine.UIVertex vertex in vertices)
            {
                var normalized = new Vector2(
                    (vertex.position.x - rect.xMin) / rect.width,
                    (vertex.position.y - rect.yMin) / rect.height);
                Assert.That(Vector2.Dot(normalized, axis), Is.LessThanOrEqualTo(limit + 0.001f));
            }
        }
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
