using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BasementDragSwayRuntimeAudit
{
    const string Output = "P:/Game_RB_Project/BuildArtifacts/BasementDragSway/Runtime";
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }

    public static async Task<string> Slots()
    {
        Directory.CreateDirectory(Output);
        var slot = Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).Single(s => s.PartyIndex == 0);
        var preview = slot.GetComponent<PartySlotVisualPreview>();
        var selectable = slot.GetComponentInChildren<CharacterSelectable>(true);
        var manager = Object.FindFirstObjectByType<CharacterSelectManager>();
        var original = slot.Selected;
        var originalPosition = selectable.transform.position;
        var enabled = manager.enabled;
        manager.enabled = false;
        var lines = new System.Collections.Generic.List<string>();
        try
        {
            foreach (string name in new[] { "Roma", "Aires", "Feno", "Milano", "Abbygail", "Dorothy", "Noemi", "Roger" })
            {
                var def = AssetDatabase.FindAssets("t:CharacterStats").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.StartsWith("Assets/Tests/"))
                    .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>).Single(d => d.name.EndsWith("." + name));
                preview.BuildCharacter(def, slot.transform);
                var p = preview.Presentation;
                var sway = p.GetComponent<BasementDragSway>();
                foreach (float scale in new[] { 0.5f, 1f, 2f })
                {
                    p.transform.localScale = Vector3.one * scale;
                    selectable.transform.position = originalPosition + Vector3.up * 0.3f;
                    selectable.SetPicked(true);
                    Set(manager, "_selected", selectable.transform);
                    Set(manager, "_selectedSelectable", selectable);
                    selectable.BeginDrag(Camera.main, Vector2.zero);
                    float x = 0;
                    for (int i = 0; i < 24; i++)
                    {
                        x += Camera.main.pixelHeight * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                        selectable.UpdateDrag(new Vector2(x, 0));
                        await Task.Delay(16);
                        if (name == "Aires" && scale == 1 && i == 20)
                            ScreenCapture.CaptureScreenshot(Output + "/Aires-drag-right.png");
                    }
                    float angle = Get<float>(sway, "angle");
                    Require(angle < -0.1f && angle >= -20.001f, name + " right drag angle: " + angle);
                    lines.Add(name + " scale=" + scale + " height=" + p.Model.GetComponent<BasementDragRig>().Height + " frequency=" + Get<float>(sway, "dragFrequency") + " angle=" + angle);
                    manager.CancelDrag();
                    Require(Get<float>(sway, "angle") == 0 && !p.IsPicked, "Cancel did not reset " + name);
                }
                p.transform.localScale = Vector3.one;
            }
            return "24 live slot/model/scale traces and manager cancellation passed.";
        }
        finally
        {
            manager.CancelDrag();
            selectable.transform.position = originalPosition;
            slot.SetCharacterDef(original, false);
            preview.BuildCharacter(original, slot.transform);
            preview.Presentation.transform.localScale = Vector3.one;
            manager.enabled = enabled;
            File.WriteAllLines(Output + "/slots.txt", lines);
        }
    }

    public static async Task<string> Roster()
    {
        Directory.CreateDirectory(Output);
        Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(b => b.name == "change associates Buttom").onClick.Invoke();
        await Task.Delay(600);
        var b = Object.FindObjectsByType<SpawnOnDragOutButton>(FindObjectsSortMode.None).First();
        var rect = (RectTransform)b.transform;
        Vector2 start = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var e = new PointerEventData(EventSystem.current) { pointerId = -1, position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.beginDragHandler);
        e.position = start + Vector2.up * 400;
        ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.dragHandler);
        var drag = Get<GameObject>(b, "_current");
        Require(drag, "Roster drag did not spawn");
        var p = drag.GetComponent<CharacterDragVisualPreview>().Presentation;
        var sway = p.GetComponent<BasementDragSway>();
        try
        {
            for (int i = 0; i < 25; i++)
            {
                e.position += Vector2.right * b.worldCamera.pixelHeight * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.dragHandler);
                await Task.Delay(16);
            }
            Require(Get<float>(sway, "angle") < -0.1f, "Roster sway missing");
            ScreenCapture.CaptureScreenshot(Output + "/roster-drag.png");
            await Task.Delay(100);
            var slot = Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).Single(s => s.PartyIndex == 0);
            var def = p.CurrentDef;
            drag.transform.position = slot.transform.position;
            ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.endDragHandler);
            Require(slot.Selected == def, "Drop did not select expected character");
            Require(Get<GameObject>(b, "_current") == null, "Drag reference survived release");
            Require(b.selectManagerToDisable == null || b.selectManagerToDisable.enabled, "Selection did not recover");
            Require(slot.GetComponent<PartySlotVisualPreview>().Presentation.GetComponent<BasementDragSway>().enabled, "Dropped slot has no sway");
            await Task.Delay(150);
            File.WriteAllText(Output + "/roster.txt", "Real roster pointer down/begin/drag/end handlers: spawn, sway, nearest-slot drop and manager restore passed.\n");
            return "Roster event-handler drag and nearest-slot drop passed.";
        }
        finally
        {
            if (b && Get<GameObject>(b, "_current")) ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.endDragHandler);
        }
    }
}
