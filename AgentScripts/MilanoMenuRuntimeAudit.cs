using Unity.Cinemachine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class MilanoMenuRuntimeAudit
{
    const string Output="P:/Game_RB_Project/BuildArtifacts/MilanoMenu/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static async Task Wait(Func<bool> f)
    {var until=DateTime.UtcNow.AddSeconds(10);while(!f()&&DateTime.UtcNow<until)await Task.Delay(40);if(!f())throw new Exception("Runtime state timed out");}
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Flags).GetValue(o);
    static void Click(Transform art,string name)
    {
        var b=art.Find(name).GetComponent<UnityEngine.UI.Button>();
        var r=(RectTransform)b.transform;
        var point=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        if(!hits.Any(h=>h.gameObject==b.gameObject||h.gameObject.transform.IsChildOf(b.transform)))throw new Exception("Button not raycastable: "+name+" at "+point+" hits: "+string.Join(",",hits.Select(h=>h.gameObject.name)));
        ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    public static async Task<string> VerifyProfileOverrides()
    {
        var character=AssetDatabase.FindAssets("t:CharacterStats").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>!p.StartsWith("Assets/Tests/")).Select(AssetDatabase.LoadAssetAtPath<CharacterStats>)
            .Single(c=>c.name.EndsWith(".Aires"));
        var original=character.basementAnimationProfile;
        var profile=UnityEngine.Object.Instantiate(original);
        var menu=AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>("Assets/Data/Animation/Basement/Menus/CharacterMenu.asset");
        var entry=profile.FindMenu(menu);
        var camera=UnityEngine.Object.Instantiate(entry.camera);
        entry.camera=camera;camera.position+=new Vector3(.12f,.08f,0);
        camera.rotation*=Quaternion.Euler(0,2,0);
        var lens=camera.lens;lens.FieldOfView+=3;camera.lens=lens;
        entry.animation.Clip=profile.menus.First(e=>e.menu!=menu).animation.Clip;
        entry.animation.Speed=.7f;
        try
        {
            character.basementAnimationProfile=profile;
            await Run("Aires",0,true);
            return "Changed Menus animation/speed and camera position/rotation/lens used on open and restored after both children; legacy pose ignored.";
        }
        finally
        {
            character.basementAnimationProfile=original;
            foreach(var slot in UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None))
                if(slot.Selected==character)slot.RefreshSelectedCharacterVisual();
            UnityEngine.Object.Destroy(profile);UnityEngine.Object.Destroy(camera);
        }
    }
    public static async Task<string> Run(string characterName,int slotIndex,bool exerciseMenus)
    {
        if(!Application.isPlaying)throw new Exception("Enter through GameSetup first");
        await Wait(()=>UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include)!=null &&
            UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>()!=null);
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var coordinator=UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>();
        if(coordinator.IsOpen)coordinator.Close(true);owner.gameObject.SetActive(false);
        var slot=UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).Single(s=>s.PartyIndex==slotIndex);
        var selected=typeof(PartySlot).GetField("selected",Flags);var previous=slot.Selected;string previousId=slot.IDCharacter;
        var character=AssetDatabase.FindAssets("t:CharacterStats").Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.StartsWith("Assets/Tests/"))
            .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>).Single(c=>c.name.EndsWith("."+characterName));
        try
        {
            selected.SetValue(slot,character);slot.IDCharacter=character.characterId;slot.RefreshSelectedCharacterVisual();
            owner.BindSlot(slot);owner.gameObject.SetActive(true);await Wait(()=>screen.CanInteract);
            var art=owner.transform.Find("CharacterMenuMind/Artboard");
            if(((RectTransform)art).rect.width<100)throw new Exception("Artboard collapsed");
            var graph=slot.GetComponent<PartySlotVisualPreview>().Presentation.GetComponentInChildren<Animancer.AnimancerComponent>(true);
            var menu=Get<BasementMenuDefinitionSO>(screen,"menuDefinition");
            var statusEntry=character.basementAnimationProfile.FindMenu(menu);
            if(graph.States.Current.Clip!=statusEntry.animation.Clip)throw new Exception("Status clip mismatch");
            var camera=Get<Unity.Cinemachine.CinemachineCamera>(Get<CameraManager>(screen,"cameraManager"),"activeCamera");
            var expected=slot.PresentationRoot.position+slot.PresentationRoot.rotation*statusEntry.camera.position;
            if(Vector3.Distance(camera.transform.position,expected)>.001f)throw new Exception("Status profile camera mismatch");
            var expectedRotation=slot.PresentationRoot.rotation*statusEntry.camera.rotation;
            if(Quaternion.Angle(camera.State.GetFinalOrientation(),expectedRotation)>.01f || Mathf.Abs(camera.Lens.FieldOfView-statusEntry.camera.lens.FieldOfView)>.001f)throw new Exception("Status camera rotation/lens mismatch");
            if(Math.Abs(graph.States.Current.Speed-statusEntry.animation.Speed)>.001)throw new Exception("Status animation speed mismatch");
            var current=graph.States.Current;float t=current.Time;await Task.Delay(250);if(current.Time<=t)throw new Exception("Animation stopped");
            if(Get<TMPro.TMP_Text>(owner,"NameText").text!=character.characterName)throw new Exception("Wrong name");
            Directory.CreateDirectory(Output);
            ScreenCapture.CaptureScreenshot(Output+characterName+"-Slot"+slotIndex+"-"+Screen.width+"x"+Screen.height+".png");await Task.Delay(180);
            if(exerciseMenus)
            {
                var accessory=UnityEngine.Object.FindFirstObjectByType<BasementAccessoryScreenController>();
                var gun=UnityEngine.Object.FindFirstObjectByType<BasementGunMenuScreenController>();
                foreach(var which in new[]{"EquipButton","GunButton"})
                {
                    Click(art,which);await Wait(()=>which=="EquipButton"?accessory.CanInteract:gun.CanInteract);
                    if(screen.CanInteract)throw new Exception("Status interactive below child"); var childMenu=AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>("Assets/Data/Animation/Basement/Menus/"+(which=="EquipButton"?"Accessory":"GunMenu")+".asset"); var childEntry=character.basementAnimationProfile.FindMenu(childMenu); if(graph.States.Current.Clip!=childEntry.animation.Clip || !childEntry.animation.Clip.isLooping)throw new Exception("Child clip mismatch"); var childCamera=Get<Unity.Cinemachine.CinemachineCamera>(Get<CameraManager>(screen,"cameraManager"),"activeCamera"); if(Vector3.Distance(childCamera.transform.position,slot.PresentationRoot.position+slot.PresentationRoot.rotation*childEntry.camera.position)>.001f || Quaternion.Angle(childCamera.State.GetFinalOrientation(),slot.PresentationRoot.rotation*childEntry.camera.rotation)>.05f)throw new Exception("Child camera mismatch"); ScreenCapture.CaptureScreenshot(Output+characterName+"-"+which+".png"); await Task.Delay(200);
                    if(which=="EquipButton")accessory.Close();else gun.Close();
                    await Wait(()=>screen.CanInteract);
                    if(graph.States.Current.Clip!=statusEntry.animation.Clip)throw new Exception("Status pose not restored");
                    if(Vector3.Distance(camera.transform.position,expected)>.001f)throw new Exception("Status camera not restored");
                }

            }
            Click(art,"BackButton");await Wait(()=>!screen.IsOpen&&!coordinator.IsOpen);
            if(owner.gameObject.activeSelf)throw new Exception("Status host did not close");
            if(graph.States.Current.Clip!=character.basementAnimationProfile.idle.Clip)throw new Exception("Formation pose not restored");
            var result=characterName+" slot "+slotIndex+": UI/name/pose/animation/raycast/Back"+(exerciseMenus?"/Equip/Gun":"")+" passed";
            File.AppendAllText(Output+"runtime.txt",result+Environment.NewLine);return result;
        }
        finally
        {
            if(coordinator.IsOpen)coordinator.Close(true);owner.gameObject.SetActive(false);
            selected.SetValue(slot,previous);slot.IDCharacter=previousId;slot.RefreshSelectedCharacterVisual();owner.BindSlot(slot);
        }
    }
}




