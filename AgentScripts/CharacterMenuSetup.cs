using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class CharacterMenuSetup
{
    const string ArtPath = "Assets/Spain/UI/Character Menu Mind/CharacterMenuMind.prefab";
    const string HostPath = "Assets/Prefab/User Interface/UpgradUI.prefab";
    const string Folder = "Assets/Data/Animation/Basement/Menus/";
    static T Ensure<T>(GameObject go) where T:Component { var c=go.GetComponent<T>(); return c!=null?c:go.AddComponent<T>(); }
    static void Set(UnityEngine.Object o, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(o); so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
    }
    static void Array(UnityEngine.Object o, string field, UnityEngine.Object[] values)
    {
        var so = new SerializedObject(o); var p = so.FindProperty(field); p.arraySize = values.Length;
        for (int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
    }
    static T Asset<T>(string name) where T:ScriptableObject
    {
        string p=Folder+name+".asset"; var a=AssetDatabase.LoadAssetAtPath<T>(p);
        if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,p);} return a;
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchorMin=new Vector2(x/1920f,1-(y+h)/1080f);
        r.anchorMax=new Vector2((x+w)/1920f,1-y/1080f);r.sizeDelta=Vector2.zero;r.anchoredPosition=Vector2.zero;return r;
    }
    static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size,TMP_FontAsset font)
    {
        var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.15f,.15f,.15f,1);t.raycastTarget=false;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;
        return t;
    }
    static UnityEngine.UI.Button Button(Transform board,string name,string title,float x,float y,float w,float h,TMP_FontAsset font)
    {
        var r=Rect(board,name,x,y,w,h);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=new Color(.94f,.74f,.46f);
        var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=im;
        var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.layer=5;label.transform.SetParent(r,false);
        var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.sizeDelta=Vector2.zero;
        var text=label.GetComponent<TextMeshProUGUI>();text.font=font;text.text=title;text.fontSize=30;text.color=new Color(.15f,.15f,.15f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        return b;
    }
    static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
    {
        while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);
        UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);
    }
    static GameObject Migrate(UILoadLaval owner,BasementMenuDefinitionSO menu)
    {
        var host=Ensure<RectTransform>(owner.gameObject);host.pivot=new Vector2(.5f,.5f);
        host.anchorMin=Vector2.zero;host.anchorMax=Vector2.one;host.offsetMin=host.offsetMax=Vector2.zero;
        host.localPosition=Vector3.zero;host.localScale=Vector3.one;host.localRotation=Quaternion.identity;
        var old=owner.transform.Cast<Transform>().Where(t=>t.name!="CharacterMenuMind").ToArray();
        foreach(var t in old)
        {
            t.gameObject.SetActive(false);
            foreach(var b in t.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if(b.name!="Skill_Tree_Button")while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);
        }
        var found=owner.transform.Find("CharacterMenuMind");
        var art=found!=null?found.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath),owner.transform);
        art.name="CharacterMenuMind";art.SetActive(true);
        var ar=(RectTransform)art.transform;ar.pivot=new Vector2(.5f,.5f);ar.anchorMin=Vector2.zero;ar.anchorMax=Vector2.one;
        ar.offsetMin=ar.offsetMax=Vector2.zero;ar.localScale=Vector3.one;ar.localPosition=Vector3.zero;
        art.GetComponent<Canvas>().overrideSorting=true;
        var screen=Ensure<BasementCharacterMenuScreenController>(owner.gameObject);
        Set(screen,"owner",owner);Set(screen,"screenGroup",art.GetComponent<CanvasGroup>());Set(screen,"menuDefinition",menu);
        Set(screen,"artboard",art.transform.Find("Artboard"));
        Set(owner,"characterMenu",screen);
        string[] fields={"NameText","levelCurrentText","currentXpText","nextXpTonewLavelText","GoldText","DamageText","ArmorText","MAXHPText","StaminaText","CritrateText","CritDamageText","EnagyText","SpeedText"};
        foreach(var f in fields)Set(owner,f,art.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name==f));
        var board=art.transform.Find("Artboard");
        board.GetComponent<UnityEngine.UI.AspectRatioFitter>().enabled=false;
        var boardRect=(RectTransform)board;boardRect.anchorMin=boardRect.anchorMax=new Vector2(.5f,.5f);
        boardRect.pivot=new Vector2(.5f,.5f);boardRect.sizeDelta=new Vector2(1920,1080);boardRect.anchoredPosition=Vector2.zero;boardRect.localScale=Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(boardRect);
        Bind(board.Find("EquipButton").GetComponent<UnityEngine.UI.Button>(),owner.OpenAccessoryScreen);
        Bind(board.Find("GunButton").GetComponent<UnityEngine.UI.Button>(),owner.OpenWeaponEquipment);
        Bind(board.Find("SkillButton").GetComponent<UnityEngine.UI.Button>(),owner.OpenActiveSkillTree);
        Bind(board.Find("BackButton").GetComponent<UnityEngine.UI.Button>(),screen.Back);
        Bind(board.Find("UplevelButton").GetComponent<UnityEngine.UI.Button>(),owner.ClickUplevel);
        Set(owner,"accessoryScreenButton",board.Find("EquipButton").GetComponent<UnityEngine.UI.Button>());
        Array(screen,"letterboxBars",new[]{"Left","Right","Bottom","Top"}.Select(n=>(UnityEngine.Object)art.transform.Find("Letterbox"+n).GetComponent<RectTransform>()).ToArray());
        EditorUtility.SetDirty(owner);PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        return art;
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Preserve dirty scene first.");
        var camera=Asset<BasementMenuCameraPresetSO>("Aires_CharacterMenuCamera");
        camera.sourceFbx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Aires/Aires_CharacterMenu_Camera.fbx");
        camera.cameraPath="Camera CharacterStatus Menu";BasementMenuCameraPresetEditor.Bake(camera);
        var menu=Asset<BasementMenuDefinitionSO>("CharacterMenu");menu.defaultCamera=camera;EditorUtility.SetDirty(menu);
        const string clipPath="Assets/Character/Aires/Aires_CharacterMenu_Idle.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(clipPath);var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
        foreach(var c in clips)c.loopTime=true;importer.clipAnimations=clips;importer.SaveAndReimport();
        var profile=AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>("Assets/Data/Animation/Basement/Aires_BasementAnimation.asset");
        var statusEntry=profile.FindMenu(menu);
        if(statusEntry==null)
        {
            statusEntry=new BasementAnimationProfileSO.MenuEntry { menu=menu, camera=camera };
            statusEntry.animation.Clip=AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__"));
            profile.menus=profile.menus.Concat(new[]{statusEntry}).ToArray();EditorUtility.SetDirty(profile);
        }
        const string modelPath="Assets/Prefab/CharactorSelect/AiresSelect.prefab";
        var model=PrefabUtility.LoadPrefabContents(modelPath);
        try
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if(source==null)continue;
                var materials=renderer.sharedMaterials;var originals=source.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length&&i<originals.Length;i++)
                    if(materials[i]==null&&originals[i]!=null){materials[i]=originals[i];changed=true;}
                if(changed)renderer.sharedMaterials=materials;
            }
            PrefabUtility.SaveAsPrefabAsset(model,modelPath);
        }finally{PrefabUtility.UnloadPrefabContents(model);}
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var root=PrefabUtility.LoadPrefabContents(ArtPath);
        try
        {
            var canvas=root.GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=50;
            var rr=(RectTransform)root.transform;rr.pivot=new Vector2(.5f,.5f);rr.localScale=Vector3.one;
            var group=Ensure<CanvasGroup>(root);group.alpha=0;group.interactable=group.blocksRaycasts=false;
            var board=root.transform.Find("Artboard");
            board.GetComponent<UnityEngine.UI.AspectRatioFitter>().enabled=false;
            var br=(RectTransform)board;br.anchorMin=br.anchorMax=new Vector2(.5f,.5f);
            br.pivot=new Vector2(.5f,.5f);br.sizeDelta=new Vector2(1920,1080);br.anchoredPosition=Vector2.zero;br.localScale=Vector3.one;
            if(board.Find("NameText")==null)
            {
                var name=Text(board,"NameText","AIRES",1295,233,305,72,58,font);name.fontStyle=FontStyles.Bold;name.rectTransform.localRotation=Quaternion.Euler(0,0,27);
                Text(board,"LevelLabel","LEVEL",1345,372,130,45,30,font);Text(board,"levelCurrentText","1",1535,372,140,45,30,font);
                Text(board,"XPLabel","XP",1345,415,85,35,24,font);Text(board,"currentXpText","0",1430,415,90,35,24,font);
                Text(board,"XPSlash","/",1515,415,25,35,24,font);Text(board,"nextXpTonewLavelText","0",1540,415,135,35,24,font);
                string[] labels={"DAMAGE","ARMOR","MAX HP","STAMINA","CRIT RATE","CRIT DMG","ENERGY","SPEED"};
                string[] fields={"DamageText","ArmorText","MAXHPText","StaminaText","CritrateText","CritDamageText","EnagyText","SpeedText"};
                for(int i=0;i<fields.Length;i++){Text(board,fields[i]+"Label",labels[i],1335,482+i*38,210,34,26,font);Text(board,fields[i],"-",1550,482+i*38,120,34,26,font);}
                Text(board,"GoldLabel","GOLD",1325,805,130,38,27,font);Text(board,"GoldText","0",1460,805,205,38,27,font);
                Button(board,"UplevelButton","UPLEVEL",1315,865,310,60,font);
                Button(board,"BackButton","BACK",100,30,170,60,font);
                foreach(string side in new[]{"Left","Right","Bottom","Top"})
                {var r=Rect(root.transform,"Letterbox"+side,0,0,0,0);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=Color.black;im.raycastTarget=true;}
            }
            PrefabUtility.SaveAsPrefabAsset(root,ArtPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        root=PrefabUtility.LoadPrefabContents(HostPath);
        try{Migrate(root.GetComponent<UILoadLaval>(),menu);PrefabUtility.SaveAsPrefabAsset(root,HostPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Basement/Basement.unity");
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        Migrate(owner,menu);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var manager=UnityEngine.Object.FindFirstObjectByType<CameraManager>();
        var camObject=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="CharacterMenuCamera")??new GameObject("CharacterMenuCamera");
        var cm=Ensure<CinemachineCamera>(camObject);cm.Priority=-10;cm.Lens=camera.lens;
        manager.RegisterMenu(menu,cm);EditorUtility.SetDirty(manager);
        Set(screen,"cameraManager",manager);Set(screen,"characterSelection",UnityEngine.Object.FindFirstObjectByType<CharacterSelectManager>());
        Array(screen,"backgroundRoots",new UnityEngine.Object[]{owner.transform.parent.gameObject,scene.GetRootGameObjects().First(g=>g.name=="CanvaCharactorInfo"),scene.GetRootGameObjects().First(g=>g.name=="MapUI")});
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        // Mark all scene instance edits, including overrides on the retained legacy controls.
        foreach(var component in owner.GetComponentsInChildren<Component>(true))if(component!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        foreach(var t in owner.GetComponentsInChildren<Transform>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "CharacterMenu UI, controls, scene bindings, baked camera and looping Aires pose saved.";
    }
}

