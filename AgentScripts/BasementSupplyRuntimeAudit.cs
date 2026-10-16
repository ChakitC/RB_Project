using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BasementSupplyRuntimeAudit
{
    const string Evidence = "P:/Game_RB_Project/BuildArtifacts/BasementSupply";
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void SetSize(int width, int height)
    {
        var a = typeof(Editor).Assembly;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var t = a.GetType("UnityEditor.GameView");
        var window = UnityEngine.Resources.FindObjectsOfTypeAll(t)[0];
        var sizesType = a.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", flags).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { t.GetProperty("currentSizeGroupType", flags).GetValue(window) });
        var names = (string[])group.GetType().GetMethod("GetDisplayTexts", flags).Invoke(group, null);
        string label = "Supply QA " + width + "x" + height;
        int index = Array.FindIndex(names, n => n.StartsWith(label));
        if (index < 0)
        {
            var sizeType = a.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"), new object[] { Enum.Parse(sizeType, "FixedResolution"), width, height, label });
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
            index = names.Length;
        }
        t.GetProperty("selectedSizeIndex", flags).SetValue(window, index);
        ((EditorWindow)window).Repaint();
    }
    static GameObject Hit(RectTransform rect)
    {
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Require(center.x > 0 && center.x < Screen.width && center.y > 0 && center.y < Screen.height, "Offscreen: " + rect.name);
        var data = new PointerEventData(EventSystem.current) { position = center, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        // Category tabs overlap by design; click their exposed strip, not their covered center.
        if (rect.name.StartsWith("Category"))
        {
            for (int y = 8; y < 150 && (hits.Count == 0 || hits[0].gameObject != rect.gameObject); y += 8)
            {
                data.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(15, -y, 0)));
                hits.Clear(); EventSystem.current.RaycastAll(data, hits);
            }
        }
        Require(hits.Count > 0, "No hit: " + rect.name);
        var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Require(target == rect.gameObject, "Raycast blocked: " + rect.name + " by " + hits[0].gameObject.name);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        return target;
    }
    static string Saves()
    {
        using var hash = SHA256.Create();
        return string.Join("\n", Directory.GetFiles("Savedata", "*", SearchOption.AllDirectories).OrderBy(p => p)
            .Select(p => p + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p)))));
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static async Task WaitFor(Func<bool> condition)
    {
        var deadline=DateTime.UtcNow.AddSeconds(10);
        while(!condition() && DateTime.UtcNow<deadline) await Task.Delay(20);
        Require(condition(),"Transition timed out");
    }
    public static async Task<string> RoomAspect(int width,int height)
    {
        Require(Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Basement","Boot through GameSetup first");
        SetSize(width,height); await WaitFor(()=>Screen.width==width && Screen.height==height);
        await Task.Delay(200); Canvas.ForceUpdateCanvases();
        var controller=Object.FindFirstObjectByType<BasementSupplyRoomController>();
        Require(controller!=null && !controller.IsOpen,"Supply must start closed");
        var context=Object.FindFirstObjectByType<BasementContext>();
        var screen=Field<BasementSupplyScreenController>(controller,"screen");
        var camera=Field<Camera>(controller,"supplyCamera");
        var original=Field<Camera[]>(controller,"basementCameras");
        var canvases=Field<Canvas[]>(controller,"basementCanvases");
        var canvasState=canvases.Select(c=>c.enabled).ToArray();
        var cameraState=original.Select(c=>c.enabled).ToArray();
        var cameraPositions=original.Select(c=>c.transform.position).ToArray();
        var cameraRotations=original.Select(c=>c.transform.rotation).ToArray();
        var fade=Field<CanvasGroup>(controller,"fade");
        string saves=Saves();
        var party=Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).OrderBy(s=>s.PartyIndex).ToArray();
        var selected=party.Select(s=>s.Selected).ToArray();
        int listeners=Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.enabled);
        var shop=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="OpenShopMenu"));
        Hit((RectTransform)shop.transform);
        Require(controller.IsTransitioning && fade.blocksRaycasts,"Shop did not begin blocked transition");
        context.uiToolbarManager.OpenShopMenu(); screen.RequestClose();
        Require(controller.Phase==BasementSupplyRoomController.VisitPhase.Opening,"Repeated command interrupted opening");
        await WaitFor(()=>!controller.IsTransitioning);
        Require(controller.IsOpen && screen.IsOpen && camera.enabled,"Supply did not open");
        Require(original.All(c=>!c.enabled) && canvases.All(c=>!c.enabled),"Basement still rendering");
        Require(!context.characterSelectManager.enabled,"Character input leaked");
        Require(!EventSystem.current.sendNavigationEvents,"Keyboard navigation leaked");
        Require(!screen.transform.parent.Find("OpenSupply").gameObject.activeSelf,"Preview launcher leaked");
        Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.enabled)==listeners,"Extra AudioListener");
        Require(Field<GameObject>(controller,"room").GetComponentsInChildren<Transform>(true).All(t=>t.GetComponents<Component>().All(c=>c!=null)),"Missing script in room");
        Hit((RectTransform)screen.transform.Find("Category1"));
        Hit((RectTransform)screen.transform.Find("Category2"));
        Hit((RectTransform)screen.transform.Find("Category0"));
        var scroll=screen.transform.Find("Items").GetComponent<ScrollRect>(); scroll.verticalNormalizedPosition=0;
        await Task.Delay(100);
        var cards=screen.GetComponentsInChildren<BasementSupplyItemCard>(); Hit((RectTransform)cards[cards.Length-1].transform);
        Require(screen.SelectedItem==cards[cards.Length-1].Item,"Last item unreachable");
        screen.SetCategory(0);
        Require(!screen.transform.Find("BuyDisabled").GetComponent<Button>().interactable,"BUY enabled");
        await Task.Delay(100);
        await WaitFor(()=>!ShaderUtil.anythingCompiling);
        ScreenCapture.CaptureScreenshot(Evidence+"/Basement-Room-"+width+"x"+height+".png"); await Task.Delay(150);
        Hit((RectTransform)screen.transform.Find("Back"));
        Require(controller.IsTransitioning,"BACK bypassed transition");
        await WaitFor(()=>!controller.IsOpen);
        Require(!camera.enabled && !screen.IsOpen && !fade.blocksRaycasts && fade.alpha==0,"Room did not close cleanly");
        for(int i=0;i<original.Length;i++)
        {
            Require(original[i].enabled==cameraState[i],"Camera state changed");
            Require(Vector3.Distance(original[i].transform.position,cameraPositions[i])<.01f && Quaternion.Angle(original[i].transform.rotation,cameraRotations[i])<.1f,"Return pose changed");
        }
        for(int i=0;i<canvases.Length;i++) Require(canvases[i].enabled==canvasState[i],"Canvas state changed");
        Require(context.characterSelectManager.enabled,"Input not restored");
        Require(party.Select(s=>s.Selected).SequenceEqual(selected),"Party changed");
        Require(Saves()==saves,"Save files changed");
        string report=width+"x"+height+": PASS GameSetup/Basement Shop raycast, locked fade, repeated commands, categories, final item, BACK fade, camera/UI/party restored, BUY disabled, unchanged saves.";
        File.AppendAllText(Evidence+"/room-runtime-audit.txt",report+"\n");
        return report;
    }
    public static async Task<string> Aspect(int width, int height)
    {
        Require(Application.isPlaying, "Run in Play Mode.");
        Directory.CreateDirectory(Evidence);
        string saves = Saves();
        SetSize(width, height);
        for (int i = 0; i < 100 && (Screen.width != width || Screen.height != height); i++) await Task.Delay(50);
        Require(Screen.width == width && Screen.height == height, "Game View size did not update.");
        var screen = Object.FindFirstObjectByType<BasementSupplyScreenController>(FindObjectsInactive.Include);
        screen.Open();
        await Task.Delay(200);
        Canvas.ForceUpdateCanvases();
        var cards = screen.GetComponentsInChildren<BasementSupplyItemCard>();
        Require(cards.Length == 23, "Unexpected production database count.");
        Hit((RectTransform)cards[1].transform);
        Require(screen.SelectedItem == cards[1].Item, "Card selection failed.");
        Hit((RectTransform)screen.transform.Find("Category1"));
        Require(screen.GetComponentsInChildren<BasementSupplyItemCard>().All(c => c.Item.itemType == ItemType.Weapon), "Weapon filtering failed.");
        Hit((RectTransform)screen.transform.Find("Category2"));
        Require(screen.GetComponentsInChildren<BasementSupplyItemCard>().All(c => c.Item.itemType == ItemType.Accessory), "Accessory filtering failed.");
        Hit((RectTransform)screen.transform.Find("Category0"));
        var scroll = screen.transform.Find("Items").GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition = 0;
        await Task.Delay(100);
        cards = screen.GetComponentsInChildren<BasementSupplyItemCard>();
        Hit((RectTransform)cards[cards.Length - 1].transform);
        Require(screen.SelectedItem == cards[cards.Length - 1].Item, "Final item selection failed.");
        Hit((RectTransform)screen.transform.Find("Back"));
        Require(!screen.IsOpen, "Back failed.");
        Hit((RectTransform)screen.transform.parent.Find("OpenSupply"));
        Require(screen.IsOpen && screen.Category == 0 && screen.SelectedItem == cards[0].Item, "Reopen failed.");
        Require(!screen.transform.Find("BuyDisabled").GetComponent<Button>().interactable, "Buy was enabled.");
        Require(Saves() == saves, "Preview changed save files.");
        await Task.Delay(200);
        string path = Evidence + "/Supply-" + width + "x" + height + ".png";
        ScreenCapture.CaptureScreenshot(path);
        for (int i = 0; i < 100 && !File.Exists(path); i++) await Task.Delay(50);
        Require(File.Exists(path), "Screenshot missing.");
        string report = width + "x" + height + ": PASS — actual raycast/click selection, three categories, last item, back/reopen, disabled buy, unchanged save hashes.";
        File.AppendAllText(Evidence + "/runtime-audit.txt", report + "\n");
        return report;
    }
}
