#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BasementCharacterMenuTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly BasementGunMenuTestFixture gunFixture = new();
    GameObject root, cameraObject;
    BasementMenuDefinitionSO definition;
    static T Get<T>(object o, string f) => (T)o.GetType().GetField(f, Hidden).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Hidden).SetValue(o,v);
    public void BlendTracksTargetAndPreservesEndpointRoll()
    {
        root=new GameObject("Blend target");root.transform.position=new Vector3(2,0,3);
        root.transform.localScale=Vector3.one*100;
        cameraObject=new GameObject("Blend camera");
        var camera=cameraObject.AddComponent<CinemachineCamera>();
        var extension=cameraObject.AddComponent<BasementMenuBlendTarget>();
        extension.Target=root.transform;extension.Offset=new Vector3(0,1.1f,0);
        var target=root.transform.position+extension.Offset;
        CameraState State(Vector3 position,float roll)
        {
            var state=CameraState.Default;state.RawPosition=position;
            state.RawOrientation=Quaternion.LookRotation(target-position,Vector3.up)*Quaternion.AngleAxis(roll,Vector3.forward);
            var original=state.GetFinalOrientation();
            extension.AuthoredOrientation=state.RawOrientation;
            extension.InvokePostPipelineStageCallback(camera,CinemachineCore.Stage.Finalize,ref state,0);
            Assert.That(Vector3.Distance(state.ReferenceLookAt,target),Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(original,state.GetFinalOrientation()),Is.LessThan(.05f));
            return state;
        }
        var a=State(new Vector3(-4,3,-6),-12);
        var b=State(new Vector3(5,2,-2),19);
        foreach(float t in new[]{.1f,.25f,.5f,.75f,.9f})
        {
            var blended=CameraState.Lerp(a,b,t);
            var direction=(target-blended.GetFinalPosition()).normalized;
            Assert.That(Vector3.Angle(direction,blended.GetFinalOrientation()*Vector3.forward),Is.LessThan(.05f));
        }
        root.transform.position+=Vector3.right;
        extension.InvokePostPipelineStageCallback(camera,CinemachineCore.Stage.Finalize,ref b,0);
        Assert.That(b.ReferenceLookAt,Is.EqualTo(target+Vector3.right));
    }

    public void TrackingRetainsRollAcrossCameraUpdates()
    {
        root=new GameObject("Persistent blend target");
        cameraObject=new GameObject("Persistent blend camera");
        var camera=cameraObject.AddComponent<CinemachineCamera>();
        var extension=cameraObject.AddComponent<BasementMenuBlendTarget>();
        extension.Target=root.transform;
        extension.AuthoredOrientation=Quaternion.Euler(5,170,13);
        camera.transform.rotation=extension.AuthoredOrientation;
        for(int i=0;i<5;i++)
        {
            camera.InternalUpdateCameraState(Vector3.up,.016f);
            Assert.That(Quaternion.Angle(camera.State.GetFinalOrientation(),extension.AuthoredOrientation),Is.LessThan(.05f),"Frame "+i);
        }
    }
    public void Cleanup()
    {
        if(root!=null)Object.DestroyImmediate(root);
        gunFixture.Cleanup();
        if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
        if(definition!=null)Object.DestroyImmediate(definition);
    }
    public void AuthoringHasStatusBindingsAndLoopingAires()
    {
        var s=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Basement/Basement.unity");
        try
        {
            T Find<T>() where T:Component => s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
            var owner=Find<UILoadLaval>();var screen=Find<BasementCharacterMenuScreenController>();
            Assert.That(Get<BasementCharacterMenuScreenController>(owner,"characterMenu"),Is.EqualTo(screen));
            Assert.That(Get<CameraManager>(screen,"cameraManager"),Is.EqualTo(Find<CameraManager>()));
            Assert.That(Get<UILoadLaval>(screen,"owner"),Is.EqualTo(owner));
            foreach(var field in new[]{"NameText","levelCurrentText","DamageText","ArmorText","MAXHPText","StaminaText","CritrateText","CritDamageText","EnagyText","SpeedText","GoldText","currentXpText","nextXpTonewLavelText"})
                Assert.That(Get<TMPro.TMP_Text>(owner,field),Is.Not.Null,field);
            var art=owner.transform.Find("CharacterMenuMind/Artboard");
            Assert.That(owner.transform,Is.InstanceOf<RectTransform>());
            Assert.That(art.parent.localScale,Is.EqualTo(Vector3.one));
            Assert.That(((RectTransform)art.parent).pivot,Is.EqualTo(new Vector2(.5f,.5f)));
            Assert.That(((RectTransform)art).sizeDelta,Is.EqualTo(new Vector2(1920,1080)));
            Assert.That(((RectTransform)art).anchorMin,Is.EqualTo(new Vector2(.5f,.5f)));
            Assert.That(((RectTransform)art).anchorMax,Is.EqualTo(new Vector2(.5f,.5f)));
            Assert.That(Get<RectTransform>(screen,"artboard"),Is.EqualTo(art));
            foreach(var pair in new[]{("EquipButton","OpenAccessoryScreen"),("GunButton","OpenWeaponEquipment"),("SkillButton","OpenActiveSkillTree"),("UplevelButton","ClickUplevel")})
            {
                var b=art.Find(pair.Item1).GetComponent<UnityEngine.UI.Button>();
                Assert.That(b.onClick.GetPersistentTarget(0),Is.EqualTo(owner));
                Assert.That(b.onClick.GetPersistentMethodName(0),Is.EqualTo(pair.Item2));
            }
            Assert.That(owner.transform.Find("Canvas UpgradUI").gameObject.activeSelf,Is.False);
            var profile=AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>("Assets/Data/Animation/Basement/Aires_BasementAnimation.asset");
            var menu=Get<BasementMenuDefinitionSO>(screen,"menuDefinition");
            Assert.That(profile.FindMenu(menu).animation.Clip.name,Is.EqualTo("Aires_CharacterMenu_Idle"));
            Assert.That(profile.FindMenu(menu).animation.Clip.isLooping,Is.True);
            var cameras=new System.Collections.Generic.HashSet<BasementMenuCameraPresetSO>();
            foreach(var name in new[]{"Aires","Roma","Feno","Milano","Abbygail","Dorothy","Noemi","Roger"})
            {
                var p=AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>("Assets/Data/Animation/Basement/"+name+"_BasementAnimation.asset");
                Assert.That(p.menus.Count(e=>e.menu==menu),Is.EqualTo(1),name);
                var entry=p.FindMenu(menu);
                Assert.That(entry.animation.Clip,Is.Not.Null,name);
                Assert.That(entry.camera,Is.Not.Null,name);
                Assert.That(cameras.Add(entry.camera),Is.True,"Camera must be independently editable: "+name);
            }
        }finally{EditorSceneManager.ClosePreviewScene(s);}
    }
    static IEnumerator Wait(Func<bool> condition)
    {
        float until=Time.unscaledTime+6;
        while(!condition()&&Time.unscaledTime<until)yield return null;
        Assert.That(condition(),Is.True,"Menu transition timed out");
    }
    public IEnumerator ParentChildAndModalRestoreWithoutFormationHop()
    {
        typeof(BasementGunMenuTestFixture).GetMethod("SetupRuntime",Hidden).Invoke(gunFixture,null);
        var manager=Get<CameraManager>(gunFixture,"manager");
        var child=Get<BasementGunMenuScreenController>(gunFixture,"screen");
        var slot=Get<PartySlot>(gunFixture,"slot");var background=Get<Canvas>(gunFixture,"background");
        root=new GameObject("Status test",typeof(RectTransform),typeof(CanvasGroup));root.SetActive(false);
        var status=root.AddComponent<BasementCharacterMenuScreenController>();
        Set(status,"screenGroup",root.GetComponent<CanvasGroup>());
        Set(status,"characterSelection",Get<CharacterSelectManager>(gunFixture,"selection"));
        Set(status,"backgroundRoots",new[]{background.gameObject});root.SetActive(true);
        definition=Object.Instantiate(Get<BasementMenuDefinitionSO>(child,"menuDefinition"));definition.name="Status test definition";
        cameraObject=new GameObject("Status test camera");var camera=cameraObject.AddComponent<CinemachineCamera>();camera.Priority=-15;
        manager.RegisterMenu(definition,camera);
        var coordinator=BasementMenuPresentationController.Resolve(manager);
        yield return null;yield return null;
        Assert.That(coordinator.TryOpen(definition,slot,status),Is.True);
        yield return Wait(()=>coordinator.CanInteract);
        Assert.That(camera.GetComponent<BasementMenuBlendTarget>().Target,Is.EqualTo(slot.PresentationRoot));
        Assert.That(manager.CharacterSelectCamera.GetComponent<BasementMenuBlendTarget>().Target,Is.EqualTo(slot.PresentationRoot));
        Assert.That(background.enabled,Is.False);
        coordinator.SetModal(status,true);child.Open(slot);Assert.That(child.IsOpen,Is.False);
        Assert.That(coordinator.CanInteract,Is.False);coordinator.SetModal(status,false);
        for(int i=0;i<2;i++)
        {
            child.Open(slot);Assert.That(child.IsOpen,Is.True);
            Assert.That(coordinator.Contains(status),Is.True);Assert.That(coordinator.Owns(status),Is.False);
            yield return Wait(()=>child.CanInteract);
            child.Close();Assert.That(child.IsOpen,Is.False);
            Assert.That(manager.IsMenuViewActive(definition),Is.True,"Child Back must return directly to Status");
            Assert.That(background.enabled,Is.False);
            yield return Wait(()=>coordinator.CanInteract);
            Assert.That(coordinator.Owns(status),Is.True);
        }
        coordinator.Close();yield return Wait(()=>!coordinator.IsOpen);
        Assert.That(background.enabled,Is.True);Assert.That(camera.Priority.Value,Is.EqualTo(-15));
        yield return null;
        Assert.That(camera.GetComponent<BasementMenuBlendTarget>(),Is.Null);
        Assert.That(manager.CharacterSelectCamera.GetComponent<BasementMenuBlendTarget>(),Is.Null);
        Assert.That(Get<CharacterSelectManager>(gunFixture,"selection").enabled,Is.True);
        Assert.That(coordinator.TryOpen(definition,slot,status),Is.True);yield return Wait(()=>coordinator.CanInteract);
        child.Open(slot);yield return Wait(()=>child.CanInteract);
        slot.gameObject.SetActive(false);yield return Wait(()=>!coordinator.IsOpen);
        Assert.That(child.IsOpen,Is.False);Assert.That(background.enabled,Is.True);
        Assert.That(camera.Priority.Value,Is.EqualTo(-15));
    }
}
#endif
