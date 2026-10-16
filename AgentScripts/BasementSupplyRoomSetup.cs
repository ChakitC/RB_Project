using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BasementSupplyRoomSetup
{
    const string ScreenPath = "Assets/Prefab/User Interface/BasementSupplyScreen.prefab";
    public const string RoomPath = "Assets/Prefab/User Interface/BasementSupplyRoom.prefab";
    static void Set(Object obj, string field, Object value)
    { var so = new SerializedObject(obj); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Array(Object obj, string field, Object[] values)
    { var so = new SerializedObject(obj); var a = so.FindProperty(field); a.arraySize = values.Length; for (int i=0;i<values.Length;i++) a.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static string Build()
    {
        var source = SceneManager.GetSceneByPath("Assets/Scenes/Test Supply.unity");
        var basement = SceneManager.GetSceneByPath("Assets/Scenes/Basement/Basement.unity");
        if (!source.isLoaded || !basement.isLoaded) throw new Exception("Load Test Supply and Basement first.");
        if (basement.GetRootGameObjects().Any(g=>g.name=="Supply Room" || g.GetComponent<BasementSupplyRoomController>()!=null)) throw new Exception("Room already exists; inspect before updating.");
        var contents = PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            var screen=contents.GetComponentInChildren<BasementSupplyScreenController>(true);
            var back=screen.transform.Find("Back").GetComponent<Button>();
            for(int i=back.onClick.GetPersistentEventCount()-1;i>=0;i--) UnityEventTools.RemovePersistentListener(back.onClick,i);
            UnityEventTools.AddPersistentListener(back.onClick,screen.RequestClose);
            PrefabUtility.SaveAsPrefabAsset(contents,ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var sourceRoom = source.GetRootGameObjects().Single(g=>g.name=="Supply Room");
        var clone = Object.Instantiate(sourceRoom); clone.name="Supply Room";
        GameObject asset;
        try { asset=PrefabUtility.SaveAsPrefabAsset(clone,RoomPath); }
        finally { Object.DestroyImmediate(clone); }
        EditorSceneManager.SetActiveScene(basement);
        var roots=basement.GetRootGameObjects();
        var context=roots.SelectMany(g=>g.GetComponentsInChildren<BasementContext>(true)).Single();
        var cameras=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
        var canvases=roots.SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).ToArray();
        var toolbarScaler=roots.Single(g=>g.name=="UI").GetComponent<CanvasScaler>();
        toolbarScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        toolbarScaler.referenceResolution=new Vector2(1920,1080); toolbarScaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var environment=roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
        float maxX=environment.Max(r=>r.bounds.max.x);
        var room=(GameObject)PrefabUtility.InstantiatePrefab(asset,basement);
        room.name="Supply Room";
        float minX=room.GetComponentsInChildren<Renderer>(true).Min(r=>r.bounds.min.x);
        room.transform.position+=new Vector3(maxX+20-minX,0,0);
        var screenInstance=room.GetComponentInChildren<BasementSupplyScreenController>(true);
        screenInstance.Close();
        screenInstance.transform.parent.Find("OpenSupply").gameObject.SetActive(false);
        var supplyCamera=room.GetComponentInChildren<Camera>(true); supplyCamera.enabled=false;
        foreach(var listener in room.GetComponentsInChildren<AudioListener>(true)) listener.enabled=false;
        room.SetActive(false);

        var host=new GameObject("Supply Room Controller");
        var controller=host.AddComponent<BasementSupplyRoomController>();
        var fadeObject=new GameObject("Supply Transition Fade",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup));
        fadeObject.transform.SetParent(host.transform,false);
        var canvas=fadeObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=short.MaxValue;
        var group=fadeObject.GetComponent<CanvasGroup>(); group.alpha=0; group.interactable=false; group.blocksRaycasts=false;
        var imageObject=new GameObject("Black",typeof(RectTransform),typeof(Image)); imageObject.layer=5;
        var rect=(RectTransform)imageObject.transform; rect.SetParent(fadeObject.transform,false); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        imageObject.GetComponent<Image>().color=Color.black;
        Set(controller,"basement",context); Set(controller,"room",room); Set(controller,"screen",screenInstance);
        Set(controller,"supplyCamera",supplyCamera); Set(controller,"fade",group);
        Array(controller,"basementCameras",cameras); Array(controller,"basementCanvases",canvases);
        Set(context,"SupplyRoom",controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(room.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(room);
        PrefabUtility.RecordPrefabInstancePropertyModifications(screenInstance.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(screenInstance.transform.parent.Find("OpenSupply").gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(supplyCamera);
        EditorSceneManager.MarkSceneDirty(basement); EditorSceneManager.SaveScene(basement);
        // Source remains the separately editable preview scene, including the user's current arrangement.
        if(source.isDirty) EditorSceneManager.SaveScene(source);
        EditorSceneManager.CloseScene(source,true);
        AssetDatabase.SaveAssets();
        return "Placed Supply Room at "+room.transform.position+"; existing maxX="+maxX+"; gap=20; cameras="+cameras.Length+"; canvases="+canvases.Length;
    }
}
