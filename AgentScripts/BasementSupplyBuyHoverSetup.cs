using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class BasementSupplyBuyHoverSetup
{
    const string Path = "Assets/Prefab/User Interface/BasementSupplyScreen.prefab";
    public static string Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var buy = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "BuyDisabled");
            var outline = root.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(i => i.sprite != null && AssetDatabase.GetAssetPath(i.sprite).EndsWith("Buy Buttle/Unnamed.png"));
            var hover = buy.GetComponent<BasementSupplyBuyHover>() ?? buy.gameObject.AddComponent<BasementSupplyBuyHover>();
            var so = new SerializedObject(hover);
            so.FindProperty("outline").objectReferenceValue = outline.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            return "Saved hover-only BUY outline; purchase remains disabled.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string Verify()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<BasementSupplyScreenController>(FindObjectsInactive.Include);
        screen.Open();
        var hover = screen.GetComponentInChildren<BasementSupplyBuyHover>(true);
        var outline = new SerializedObject(hover).FindProperty("outline").objectReferenceValue as GameObject;
        var buy = hover.GetComponent<UnityEngine.UI.Button>();
        if (outline.activeSelf) throw new Exception("Outline visible before hover");
        var data = new PointerEventData(EventSystem.current);
        data.position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)buy.transform).TransformPoint(((RectTransform)buy.transform).rect.center));
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0 || hits[0].gameObject != buy.gameObject) throw new Exception("BUY raycast mismatch");
        ExecuteEvents.Execute(buy.gameObject, data, ExecuteEvents.pointerEnterHandler);
        if (!outline.activeSelf) throw new Exception("Hover missing");
        ExecuteEvents.Execute(buy.gameObject, data, ExecuteEvents.pointerExitHandler);
        if (outline.activeSelf) throw new Exception("Exit failed");
        ExecuteEvents.Execute(buy.gameObject, data, ExecuteEvents.pointerEnterHandler);
        screen.Close(); screen.Open();
        if (outline.activeSelf || buy.interactable || buy.onClick.GetPersistentEventCount() != 0) throw new Exception("Reopen or disabled purchase failed");
        return "PASS: actual BUY raycast, enter/exit, close/reopen reset, purchase disabled and unwired.";
    }
}
