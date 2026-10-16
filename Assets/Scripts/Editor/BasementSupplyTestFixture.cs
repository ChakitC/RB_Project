#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BasementSupplyTestFixture
{
    const string PrefabPath = "Assets/Prefab/User Interface/BasementSupplyScreen.prefab";
    readonly List<Object> owned = new();
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    ItemDefinition Item(string name, ItemType type)
    {
        var item = Own(ScriptableObject.CreateInstance<ItemDefinition>());
        item.name = item.displayName = item.itemId = name; item.itemType = type;
        item.description = "Description of " + name; item.baseBuyPrice = 123;
        return item;
    }
    BasementSupplyScreenController Screen(params ItemDefinition[] items)
    {
        var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
        var screen = root.GetComponentInChildren<BasementSupplyScreenController>(true);
        var database = Own(ScriptableObject.CreateInstance<ItemDatabase>());
        database.items.AddRange(items);
        Set(screen, "itemDatabase", database);
        screen.Open();
        return screen;
    }
    public void AuthoringIsReadOnlyAndReferencesAreComplete()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(root, Is.Not.Null);
        var screen = root.GetComponentInChildren<BasementSupplyScreenController>(true);
        var serialized = new SerializedObject(screen);
        foreach (var field in new[] { "itemDatabase", "cardPrefab", "content", "itemScroll", "descriptionScroll", "detailIcon", "detailFallback", "nameText", "descriptionText", "priceText", "emptyState", "buyButton" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var buy = screen.transform.Find("BuyDisabled").GetComponent<Button>();
        Assert.That(buy.interactable, Is.False);
        Assert.That(buy.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(root.GetComponentInChildren<ShopService>(true), Is.Null);
        Assert.That(root.GetComponentInChildren<PlayerInventory>(true), Is.Null);
        Assert.That(root.GetComponentInChildren<ShopPanelUI>(true), Is.Null);
        Assert.That(screen.GetComponentInChildren<GridLayoutGroup>(true).constraintCount, Is.EqualTo(7));
        Assert.That(screen.GetComponentInChildren<GridLayoutGroup>(true).padding.bottom, Is.GreaterThanOrEqualTo(200));
        Assert.That(root.GetComponent<CanvasScaler>().screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(EditorBuildSettings.scenes.Any(s => s.path == "Assets/Scenes/Test Supply.unity"), Is.False);
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Spain/UI/Basement Supply UI/Info Frame.png");
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.alphaIsTransparency, Is.True);
    }
    public IEnumerator SelectionFilteringAndReopen()
    {
        var weapon = Item("Weapon A", ItemType.Weapon);
        var accessory = Item("Accessory B", ItemType.Accessory);
        var material = Item("Material C", ItemType.Material);
        var screen = Screen(weapon, null, accessory, weapon, material);
        yield return null;
        Assert.That(screen.VisibleItemCount, Is.EqualTo(3));
        Assert.That(screen.SelectedItem, Is.SameAs(weapon));
        var cards = screen.GetComponentsInChildren<BasementSupplyItemCard>();
        CollectionAssert.AreEqual(new[] { weapon, accessory, material }, cards.Select(c => c.Item).ToArray());
        cards[1].GetComponent<Button>().onClick.Invoke();
        Assert.That(screen.SelectedItem, Is.SameAs(accessory));
        Assert.That(screen.transform.Find("LiveItemInfo/ItemName").GetComponent<TMP_Text>().text, Is.EqualTo("Accessory B"));
        screen.transform.Find("Category1").GetComponent<Button>().onClick.Invoke();
        Assert.That(screen.VisibleItemCount, Is.EqualTo(1));
        Assert.That(screen.SelectedItem, Is.SameAs(weapon));
        screen.SelectItem(accessory);
        Assert.That(screen.SelectedItem, Is.SameAs(weapon), "Hidden entries cannot be selected.");
        screen.transform.Find("Category2").GetComponent<Button>().onClick.Invoke();
        Assert.That(screen.SelectedItem, Is.SameAs(accessory));
        int pooled = screen.GetComponentsInChildren<BasementSupplyItemCard>(true).Length;
        for (int i = 0; i < 5; i++)
        {
            screen.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
            Assert.That(screen.IsOpen, Is.False);
            screen.transform.parent.Find("OpenSupply").GetComponent<Button>().onClick.Invoke();
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(screen.Category, Is.Zero);
            Assert.That(screen.SelectedItem, Is.SameAs(weapon));
        }
        Assert.That(screen.GetComponentsInChildren<BasementSupplyItemCard>(true).Length, Is.EqualTo(pooled));
        Assert.That(screen.transform.Find("Items").GetComponent<ScrollRect>().content.anchoredPosition.y, Is.EqualTo(0).Within(.01));
        Assert.That(weapon.baseBuyPrice, Is.EqualTo(123));
        Assert.That(accessory.baseBuyPrice, Is.EqualTo(123));
    }
    public IEnumerator EmptyMissingArtworkAndLongDescription()
    {
        var item = Item("Missing Artwork", ItemType.Material);
        item.description = string.Join("\n", Enumerable.Repeat("Long readable description line", 60));
        var screen = Screen(item);
        yield return null;
        Assert.That(screen.transform.Find("SelectedItemIcon").GetComponent<Image>().enabled, Is.False);
        Assert.That(screen.transform.Find("SelectedItemIcon/SelectedItemFallback").gameObject.activeSelf, Is.True);
        var scroll = screen.transform.Find("LiveItemInfo/Description").GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases();
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        scroll.verticalNormalizedPosition = 0;
        screen.SelectItem(item);
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01));
        screen.SetCategory(1);
        Assert.That(screen.VisibleItemCount, Is.Zero);
        Assert.That(screen.SelectedItem, Is.Null);
        Assert.That(screen.transform.Find("Items/EmptyState").gameObject.activeSelf, Is.True);
        Assert.That(screen.transform.Find("BuyDisabled").GetComponent<Button>().interactable, Is.False);
        Set(screen, "itemDatabase", null);
        screen.Open();
        Assert.That(screen.VisibleItemCount, Is.Zero);
    }
}
#endif
