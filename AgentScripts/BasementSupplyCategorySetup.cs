using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class BasementSupplyCategorySetup
{
    const string Art = "Assets/Spain/UI/Basement Supply UI/";
    const string Prefab = "Assets/Prefab/User Interface/BasementSupplyScreen.prefab";
    static Image Layer(string name, Transform parent, string asset, Vector2 offset, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.sizeDelta = new Vector2(1920, 1080) * scale;
        r.localRotation = Quaternion.Euler(0, 0, -8.1f);
        r.anchoredPosition = Quaternion.Euler(0, 0, -8.1f) * (offset * scale);
        var image = go.GetComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset);
        image.raycastTarget = false;
        return image;
    }
    public static string Apply()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "Category Selection Frame.png");
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var screen = root.GetComponentInChildren<BasementSupplyScreenController>(true);
            var paper = screen.transform.Find("Paper");
            string[] assets = { "Category Icon Item Type", "Category Icon Dummy", "Category Icon Dummy 2" };
            Vector2[] origins = { new Vector2(347,213), new Vector2(412,315), new Vector2(425,376) };
            var so = new SerializedObject(screen);
            var tabs = so.FindProperty("categoryTabs"); tabs.arraySize = 3;
            var positions = so.FindProperty("categoryExpandedPositions"); positions.arraySize = 3;
            var selections = so.FindProperty("categorySelections"); selections.arraySize = 3;
            so.FindProperty("categoryPaper").objectReferenceValue = paper;
            for (int i = 0; i < 3; i++)
            {
                var old = screen.transform.Find(assets[i]); if (old != null) old.gameObject.SetActive(false);
                var tab = (RectTransform)screen.transform.Find("Category" + i);
                foreach (Transform child in tab.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                tab.localRotation = Quaternion.Euler(0, 0, 8.1f); tab.localScale = Vector3.one;
                tab.anchoredPosition = new Vector2(347 + i * 9, -213 - i * 66); tab.sizeDelta = new Vector2(154,154);
                tab.SetSiblingIndex(paper.GetSiblingIndex());
                // Keep the existing Button and persistent category binding; use only its transparent hit area.
                tab.GetComponent<Image>().color = Color.clear;
                tab.GetComponent<Button>().transition = Selectable.Transition.None;
                Layer("Artwork", tab, assets[i] + ".png", new Vector2(-origins[i].x,origins[i].y), i == 0 ? 1 : 154f/110f);
                var frame = Layer("Selection", tab, "Category Selection Frame.png", new Vector2(-347,213), 1);
                selections.GetArrayElementAtIndex(i).objectReferenceValue = frame.gameObject;
                tabs.GetArrayElementAtIndex(i).objectReferenceValue = tab;
                positions.GetArrayElementAtIndex(i).vector2Value = tab.anchoredPosition;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); screen.SetCategory(0);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            return "Applied supplied category artwork and selection frame, expanded/contracted tabs behind paper.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static async Task<string> Verify()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode");
        var screen = Object.FindFirstObjectByType<BasementSupplyScreenController>(FindObjectsInactive.Include); screen.Open();
        for (int index = 0; index < 3; index++)
        {
            // The unselected tab's exposed left strip remains clickable even when the selected tab overlaps its center.
            var tab = (RectTransform)screen.transform.Find("Category" + index);
            var data = new PointerEventData(EventSystem.current);
            bool clicked = false;
            for (int y = 8; y < 150 && !clicked; y += 8)
            {
                data.position = RectTransformUtility.WorldToScreenPoint(null, tab.TransformPoint(new Vector3(15, -y, 0)));
                var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                if (hits.Count > 0 && hits[0].gameObject == tab.gameObject)
                { ExecuteEvents.Execute(tab.gameObject, data, ExecuteEvents.pointerClickHandler); clicked = true; }
            }
            if (!clicked || screen.Category != index) throw new Exception("Category raycast failed " + index);
            for (int i = 0; i < 3; i++)
            {
                var other = screen.transform.Find("Category" + i);
                if (other.Find("Selection").gameObject.activeSelf != (i == index)) throw new Exception("Selection frame mismatch");
                if (Mathf.Abs(other.localScale.x - (i == index ? 1f : 110f/154f)) > .001f) throw new Exception("Tab scale mismatch");
            }
            await Task.Delay(150);
            ScreenCapture.CaptureScreenshot("P:/Game_RB_Project/BuildArtifacts/BasementSupply/Category-" + index + ".png");
            await Task.Delay(200);
        }
        screen.Close(); screen.Open();
        if (screen.Category != 0) throw new Exception("Reopen failed");
        return "PASS: all three exposed-tab raycasts, selection frame, expansion, reopen; screenshots Category-0/1/2.png.";
    }
}
