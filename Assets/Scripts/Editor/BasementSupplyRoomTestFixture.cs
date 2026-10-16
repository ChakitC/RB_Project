#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public sealed class BasementSupplyRoomTestFixture
{
    readonly List<Object> owned = new();
    GameObject host, room;
    BasementContext context;
    BasementSupplyRoomController controller;
    BasementSupplyScreenController screen;
    Camera original, supply;
    Canvas background;
    CanvasGroup fade;
    EventSystem eventSystem;
    GameObject previousSelection;
    float timeScale;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    GameObject Own(GameObject value) { owned.Add(value); return value; }
    void Build()
    {
        timeScale = Time.timeScale;
        eventSystem = Own(new GameObject("Visit EventSystem", typeof(EventSystem))).GetComponent<EventSystem>();
        previousSelection = Own(new GameObject("Previous selection",typeof(RectTransform),typeof(Button)));
        eventSystem.SetSelectedGameObject(previousSelection);
        host = Own(new GameObject("Supply visit test")); host.SetActive(false);
        context = host.AddComponent<BasementContext>();
        context.characterSelectManager = host.AddComponent<CharacterSelectManager>();
        context.camManager = host.AddComponent<CameraManager>(); context.camManager.bct = context;
        controller = host.AddComponent<BasementSupplyRoomController>(); context.SupplyRoom = controller;
        var toolbar = host.AddComponent<UItoolbarManager>(); toolbar.bct = context; context.uiToolbarManager = toolbar;
        original = Own(new GameObject("Original camera", typeof(Camera))).GetComponent<Camera>();
        background = Own(new GameObject("Background", typeof(Canvas), typeof(GraphicRaycaster))).GetComponent<Canvas>();
        fade = Own(new GameObject("Fade", typeof(CanvasGroup))).GetComponent<CanvasGroup>(); fade.alpha = 0;
        room = Own(new GameObject("Room"));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/BasementSupplyScreen.prefab");
        var ui = Object.Instantiate(prefab, room.transform);
        screen = ui.GetComponentInChildren<BasementSupplyScreenController>(true); screen.Close();
        supply = new GameObject("Supply camera", typeof(Camera)).GetComponent<Camera>(); supply.transform.SetParent(room.transform); supply.enabled = false;
        room.SetActive(false);
        Set(controller,"basement",context); Set(controller,"room",room); Set(controller,"screen",screen);
        Set(controller,"supplyCamera",supply); Set(controller,"basementCameras",new[]{original}); Set(controller,"basementCanvases",new[]{background}); Set(controller,"fade",fade);
        Set(controller,"fadeToBlackDuration",.04f); Set(controller,"fadeFromBlackDuration",.04f);
        host.SetActive(true);
    }
    public void Cleanup()
    {
        Time.timeScale = timeScale;
        for(int i=owned.Count-1;i>=0;i--) if(owned[i]!=null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    IEnumerator Settled()
    {
        float deadline=Time.realtimeSinceStartup+3;
        while(controller.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.That(controller.IsTransitioning,Is.False,"Transition stuck");
    }
    public IEnumerator ShopFadeAndBackRestore()
    {
        Build();
        Time.timeScale=0;
        for(int visit=0;visit<3;visit++)
        {
            context.uiToolbarManager.OpenShopMenu();
            context.uiToolbarManager.OpenShopMenu(); controller.Close();
            Assert.That(controller.Phase,Is.EqualTo(BasementSupplyRoomController.VisitPhase.Opening));
            Assert.That(context.characterSelectManager.enabled,Is.False);
            Assert.That(eventSystem.sendNavigationEvents,Is.False);
            Assert.That(eventSystem.currentSelectedGameObject,Is.Null);
            Assert.That(background.GetComponent<GraphicRaycaster>().enabled,Is.False);
            while(original.enabled) { Assert.That(room.activeSelf,Is.False); yield return null; }
            Assert.That(fade.alpha,Is.EqualTo(1).Within(.001f),"Camera switched before black");
            yield return Settled();
            Assert.That(screen.IsOpen && supply.enabled && room.activeSelf,Is.True);
            Assert.That(background.enabled,Is.False);
            Assert.That(fade.blocksRaycasts,Is.False);
            context.camManager.MobilizClick(); Assert.That(context.camManager._inMapUI,Is.False);
            screen.SetCategory(2);
            screen.transform.Find("Back").GetComponent<Button>().onClick.Invoke();
            while(!original.enabled) yield return null;
            Assert.That(fade.alpha,Is.EqualTo(1).Within(.001f),"Camera restored before black");
            yield return Settled();
            Assert.That(room.activeSelf || screen.IsOpen || supply.enabled,Is.False);
            Assert.That(background.enabled && background.GetComponent<GraphicRaycaster>().enabled && context.characterSelectManager.enabled,Is.True);
            Assert.That(fade.alpha,Is.Zero); Assert.That(fade.blocksRaycasts,Is.False);
            Assert.That(eventSystem.sendNavigationEvents,Is.True);
            Assert.That(eventSystem.currentSelectedGameObject,Is.SameAs(previousSelection));
        }
    }
    public IEnumerator InterruptedVisitsRestoreExactState()
    {
        Build();
        background.enabled=false; context.characterSelectManager.enabled=false;
        foreach(int phase in new[]{0,1,2})
        {
            controller.enabled=true; controller.Open();
            if(phase>0) yield return Settled();
            if(phase==2) controller.Close();
            controller.enabled=false;
            Assert.That(controller.IsOpen,Is.False);
            Assert.That(original.enabled,Is.True);
            Assert.That(background.enabled || context.characterSelectManager.enabled || room.activeSelf,Is.False);
            Assert.That(background.GetComponent<GraphicRaycaster>().enabled,Is.True);
            Assert.That(fade.alpha,Is.Zero); Assert.That(fade.blocksRaycasts,Is.False);
            Assert.That(eventSystem.sendNavigationEvents,Is.True);
        }
        yield return null;
    }
    public IEnumerator InvalidBindingsAndMapViewRejectEntry()
    {
        Build();
        context.camManager._inMapUI=true; controller.Open();
        Assert.That(controller.IsOpen,Is.False);
        context.camManager._inMapUI=false;
        Set(controller,"supplyCamera",null);
        LogAssert.Expect(LogType.Error,"[Basement Supply] Room, screen, cameras, fade and Basement bindings are required.");
        controller.Open();
        Assert.That(controller.IsOpen || room.activeSelf,Is.False);
        Assert.That(original.enabled && context.characterSelectManager.enabled,Is.True);
        Assert.That(fade.alpha,Is.Zero);
        yield return null;
    }
}
#endif
