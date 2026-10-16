using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run through Unity MCP; changes only the Supply card's decorative selection artwork.
public static class BasementSupplySelectionSetup
{
    const string Art = "Assets/Spain/UI/Basement Supply UI/Selectio Item Frame.png";
    const string Folder = "Assets/Prefab/User Interface/";

    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Sprite provider unavailable.");
        provider.InitSpriteEditorDataProvider();
        var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (edit == null) throw new InvalidOperationException("Sprite edit capability unavailable.");
        var caps = edit.GetEditCapability();
        if (!caps.HasCapability(EEditCapability.CreateAndDeleteSprite) ||
            !caps.HasCapability(EEditCapability.EditSpriteRect) ||
            !caps.HasCapability(EEditCapability.EditPivot))
            throw new InvalidOperationException("Importer cannot author the selection sprite.");
        var id = provider.GetSpriteRects().FirstOrDefault(r => r.name == "SupplyItemSelection")?.spriteID ?? GUID.Generate();
        provider.SetSpriteRects(new[] { new SpriteRect {
            name = "SupplyItemSelection", spriteID = id,
            rect = new Rect(1135,365,200,201),
            alignment = SpriteAlignment.Custom, pivot = new Vector2(.497855f,.50006f)
        }});
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names == null) throw new InvalidOperationException("Sprite ID provider unavailable.");
        names.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair("SupplyItemSelection",id) });
        provider.Apply(); importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().Single();
        int count = 0;
        foreach (var name in new[] { "BasementSupplyItemCard.prefab", "BasementSupplyScreen.prefab", "BasementSupplyRoom.prefab" })
        {
            var path = Folder + name;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var card in root.GetComponentsInChildren<BasementSupplyItemCard>(true))
                {
                    var selection = (GameObject)new SerializedObject(card).FindProperty("selection").objectReferenceValue;
                    var old = selection.GetComponent<BasementGunMenuDashedOutline>();
                    if (old != null) Object.DestroyImmediate(old);
                    var image = selection.GetComponent<Image>() ?? selection.AddComponent<Image>();
                    image.sprite = sprite; image.color = Color.white;
                    image.raycastTarget = false; image.type = Image.Type.Simple;
                    var rect = (RectTransform)selection.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(200,201) * (172f / 178.229126f);
                    // Source art already includes +8.1 degrees; the grid supplies that angle.
                    rect.localRotation = Quaternion.Euler(0,0,-8.1f);
                    rect.localScale = Vector3.one;
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "Applied supplied red selection frame to " + count + " cards; source PNG unchanged.";
    }
}
