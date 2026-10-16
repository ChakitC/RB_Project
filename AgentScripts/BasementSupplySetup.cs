using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BasementSupplySetup
{
    const string Art = "Assets/Spain/UI/Basement Supply UI/";
    const string Folder = "Assets/Prefab/User Interface/";
    static TMP_FontAsset body, heading;
    static Color Ink = new Color32(59, 58, 55, 255);
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h, float angle = 0)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        r.localEulerAngles = new Vector3(0, 0, angle);
        return r;
    }
    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
    static Image Image(string name, Transform parent, float x, float y, float w, float h, Color color, float angle = 0)
    {
        var i = Rect(name, parent, x, y, w, h, angle).gameObject.AddComponent<Image>();
        i.color = color; i.raycastTarget = false;
        return i;
    }
    static TMP_Text Text(string name, Transform parent, string text, float x, float y, float w, float h, float size, bool title = false)
    {
        var t = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = title ? heading : body;
        t.text = text; t.fontSize = size; t.color = Ink; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }
    static Button Button(string name, Transform parent, float x, float y, float w, float h, Color color, float angle = 0)
    {
        var i = Image(name, parent, x, y, w, h, color, angle);
        i.raycastTarget = true;
        var b = i.gameObject.AddComponent<Button>(); b.targetGraphic = i;
        var c = b.colors; c.highlightedColor = new Color(.95f, .85f, .63f); c.pressedColor = new Color(.78f, .67f, .42f); b.colors = c;
        return b;
    }
    static void Set(Object obj, string field, Object value)
    {
        var so = new SerializedObject(obj); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void ArtLayer(string path, Transform parent)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + path);
        if (sprite == null) throw new Exception("Missing sprite: " + path);
        Image(Path.GetFileNameWithoutExtension(path), parent, 0, 0, 1920, 1080, Color.white).sprite = sprite;
    }
    static GameObject Outline(Transform parent, float w, float h, Color color)
    {
        var r = Rect("Selection", parent, 0, 0, w, h);
        var g = r.gameObject.AddComponent<BasementGunMenuDashedOutline>(); g.color = color; g.raycastTarget = false;
        return r.gameObject;
    }
    static ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h, float angle, out RectTransform content)
    {
        var r = Rect(name, parent, x, y, w, h, angle);
        var scroll = r.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        var viewport = Image("Viewport", r, 0, 0, w, h, Color.white);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        content = Rect("Content", viewport.transform, 0, 0, w, h);
        scroll.viewport = viewport.rectTransform; scroll.content = content;
        return scroll;
    }

    public static string Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Test Supply.unity") throw new Exception("Open Test Supply before building.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Supply Preview UI")) throw new Exception("Supply Preview UI already exists; inspect before rebuilding.");
        var camera = GameObject.Find("Character/Camera Supply/Camera Supply").GetComponent<Camera>();
        if (camera == null) throw new Exception("Authored supply camera missing.");
        heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Novecento/Novecentosanswide-DemiBold SDF.asset");
        body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun/Sarabun-Regular SDF.asset");
        string[] layers = { "Base BG Color.png", "BG Frame.png", "BG Supply Text.png", "BG List Text.png", "Info Frame.png", "Item Text.png", "Info Text.png", "Category Icon Item Type.png", "Category Icon Dummy.png", "Category Icon Dummy 2.png", "BackButtle/Layer 29.png", "Buy Buttle/BUY.png", "Buy Buttle/Layer 26.png", "Buy Buttle/Layer 27.png", "Buy Buttle/Unnamed.png", "Base BG Pattle/Layer 28 Copy.png", "Base BG Pattle/Layer 28 Copy 2.png", "Base BG Pattle/Layer 28 Copy 3.png", "Base BG Pattle/Layer 28 Copy 4.png" };
        foreach (var path in layers)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        var cardRoot = Rect("BasementSupplyItemCard", null, 0, 0, 172, 172);
        var cb = cardRoot.gameObject.AddComponent<Image>(); cb.color = new Color32(215, 208, 188, 255);
        var button = cardRoot.gameObject.AddComponent<Button>(); button.targetGraphic = cb;
        var icon = Image("Icon", cardRoot, 3, 3, 166, 166, Color.white); icon.preserveAspect = true;
        var fallback = Text("Fallback", cardRoot, "ไม่มีรูป", 10, 45, 152, 90, 22); fallback.alignment = TextAlignmentOptions.Center;
        var outline = Outline(cardRoot, 172, 172, new Color32(213, 13, 41, 255));
        var card = cardRoot.gameObject.AddComponent<BasementSupplyItemCard>();
        Set(card, "button", button); Set(card, "icon", icon); Set(card, "fallback", fallback); Set(card, "selection", outline);
        var cardAsset = PrefabUtility.SaveAsPrefabAsset(cardRoot.gameObject, Folder + "BasementSupplyItemCard.prefab");
        Object.DestroyImmediate(cardRoot.gameObject);

        var canvasRoot = new GameObject("Supply Preview UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var board = Rect("Artboard", canvasRoot.transform, 0, 0, 1920, 1080);
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(.5f, .5f); board.anchoredPosition = Vector2.zero;
        Color barColor = new Color32(48, 48, 47, 255);
        Image("TopLetterbox", board, -5000, -5000, 11920, 5000, barColor);
        Image("BottomLetterbox", board, -5000, 1080, 11920, 5000, barColor);
        Image("LeftLetterbox", board, -5000, 0, 5000, 1080, barColor);
        Image("RightLetterbox", board, 1920, 0, 5000, 1080, barColor);
        var launch = Button("OpenSupply", board, 1460, 910, 350, 90, new Color32(199, 155, 60, 255));
        var launchLabel = Text("Label", launch.transform, "OPEN SUPPLY", 15, 15, 320, 60, 35, true); launchLabel.alignment = TextAlignmentOptions.Center;
        var screenRoot = Rect("SupplyScreen", board, 0, 0, 1920, 1080); screenRoot.gameObject.SetActive(false);
        var screenMask = screenRoot.gameObject.AddComponent<Image>(); screenMask.color = Color.white; screenMask.raycastTarget = false;
        screenRoot.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var screen = screenRoot.gameObject.AddComponent<BasementSupplyScreenController>();
        Image("SceneDimmer", screenRoot, 0, 0, 1920, 1080, new Color(0.12f, .12f, .12f, .32f)).raycastTarget = true;
        ArtLayer("Base BG Color.png", screenRoot);
        ArtLayer("Category Icon Item Type.png", screenRoot);
        ArtLayer("Category Icon Dummy.png", screenRoot);
        ArtLayer("Category Icon Dummy 2.png", screenRoot);
        Image("Paper", screenRoot, 463, 86, 1390, 1230, new Color32(234, 231, 222, 255), 8.1f);
        foreach (var l in layers.Where(p => p.StartsWith("Base BG Pattle"))) ArtLayer(l, screenRoot);
        foreach (var l in new[] { "BG Supply Text.png", "BG List Text.png", "BG Frame.png", "Info Frame.png", "Item Text.png", "Info Text.png" }) ArtLayer(l, screenRoot);
        var detail = Image("SelectedItemIcon", screenRoot, 580, 322, 228, 228, Color.white, 8.1f); detail.preserveAspect = true;
        var detailFallback = Text("SelectedItemFallback", detail.transform, "ไม่มีรูป", 0, 70, 228, 80, 28); detailFallback.alignment = TextAlignmentOptions.Center;
        var info = Rect("LiveItemInfo", screenRoot, 854, 231, 925, 280, 8.1f);
        var name = Text("ItemName", info, "", 15, 4, 860, 55, 36);
        RectTransform dc;
        var ds = Scroll("Description", info, 15, 62, 855, 128, 0, out dc);
        var desc = dc.gameObject.AddComponent<TextMeshProUGUI>(); desc.font = body; desc.fontSize = 25; desc.color = Ink; desc.raycastTarget = false;
        desc.textWrappingMode = TextWrappingModes.Normal;
        dc.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var price = Text("BasePrice", info, "", 15, 202, 600, 44, 26);
        Text("PreviewNotice", info, "หน้าดูตัวอย่าง • ยังไม่เปิดซื้อ", 15, 246, 580, 34, 20).color = new Color32(111, 100, 83, 255);
        foreach (var l in new[] { "Buy Buttle/Layer 26.png", "Buy Buttle/Unnamed.png", "Buy Buttle/Layer 27.png", "Buy Buttle/BUY.png" }) ArtLayer(l, screenRoot);
        var buy = Button("BuyDisabled", screenRoot, 1604, 333, 225, 105, Color.clear, 8.1f); buy.interactable = false;
        var buyOutline = screenRoot.Find("Unnamed").gameObject;
        Set(buy.gameObject.AddComponent<BasementSupplyBuyHover>(), "outline", buyOutline);
        buyOutline.SetActive(false);

        RectTransform items;
        var scroll = Scroll("Items", screenRoot, 610, 610, 1252, 640, 8.1f, out items);
        var grid = items.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(172, 172); grid.spacing = new Vector2(8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 7;
        // The rotated viewport extends below the artboard at its left edge.
        // Extra scroll travel brings the final row fully above that clipped edge.
        grid.padding.bottom = 200;
        items.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var empty = Text("EmptyState", scroll.transform, "ไม่มีไอเทมในหมวดนี้", 20, 40, 1100, 80, 36).gameObject;
        string[] labels = { "ทั้งหมด", "อาวุธ", "เครื่องประดับ" };
        Color[] colors = { new Color32(203, 141, 60, 255), new Color32(94, 164, 194, 255), new Color32(117, 105, 95, 255) };
        var selections = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            var tab = Button("Category" + i, screenRoot, 347 + i * 10, 213 + i * 91, 145, 81, colors[i], 8.1f);
            var label = Text("Label", tab.transform, labels[i], 6, 20, 133, 46, i == 2 ? 21 : 27); label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            selections[i] = Outline(tab.transform, 145, 81, Color.white);
            UnityEventTools.AddIntPersistentListener(tab.onClick, screen.SetCategory, i);
        }
        ArtLayer("BackButtle/Layer 29.png", screenRoot);
        var back = Button("Back", screenRoot, 14, 15, 175, 80, new Color32(195, 153, 61, 255));
        var backText = Text("Label", back.transform, "BACK", 0, 0, 175, 80, 58, true); backText.alignment = TextAlignmentOptions.Center;
        UnityEventTools.AddPersistentListener(back.onClick, screen.RequestClose);
        UnityEventTools.AddPersistentListener(launch.onClick, screen.Open);
        Set(screen, "itemDatabase", AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Scripts/System/SaveSystem/ItemDatabase.asset"));
        Set(screen, "cardPrefab", cardAsset.GetComponent<BasementSupplyItemCard>()); Set(screen, "content", items); Set(screen, "itemScroll", scroll);
        Set(screen, "descriptionScroll", ds); Set(screen, "detailIcon", detail); Set(screen, "detailFallback", detailFallback);
        Set(screen, "nameText", name); Set(screen, "descriptionText", desc); Set(screen, "priceText", price); Set(screen, "emptyState", empty); Set(screen, "buyButton", buy);
        var serialized = new SerializedObject(screen); var array = serialized.FindProperty("categorySelections"); array.arraySize = 3;
        for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = selections[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        screenRoot.gameObject.SetActive(true); screen.SetCategory(0);
        PrefabUtility.SaveAsPrefabAssetAndConnect(canvasRoot, Folder + "BasementSupplyScreen.prefab", InteractionMode.AutomatedAction);
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("Supply Preview EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        // The user's authored camera owns the view; retain its lens and pose.
        var unusedCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        unusedCamera.enabled = false;
        camera.enabled = true;
        if (camera.GetComponent<BasementSupplyPreviewCameraFrame>() == null)
            camera.gameObject.AddComponent<BasementSupplyPreviewCameraFrame>();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Built Supply UI in Test Supply with " + screen.VisibleItemCount + " unique items. Authored camera and Neria preserved.";
    }
}
