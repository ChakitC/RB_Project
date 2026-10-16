using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

public static class CharacterMenuTrackingAudit
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    public static async Task<string> Run()
    {
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include)==null && DateTime.UtcNow<deadline)await Task.Delay(100);
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var manager=UnityEngine.Object.FindFirstObjectByType<CameraManager>();
        var brain=Get<CinemachineBrain>(manager,"presentationBrain");
        var coordinator=UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>();
        var slot=owner._slot ?? UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).First(s=>s.Selected!=null);
        bool wasOpen=screen.IsOpen;
        coordinator.Close(true);owner.gameObject.SetActive(false);
        await Task.Delay(150);
        owner.BindSlot(slot);
        var rows=new List<string>();
        async Task Observe(string label,Func<bool> ready)
        {
            int tracked=0;bool captured=false;
            var until=DateTime.UtcNow.AddSeconds(8);
            while(!ready()&&DateTime.UtcNow<until)
            {
                await Task.Delay(10);
                var blend=brain.ActiveBlend;
                if(blend!=null&&blend.BlendWeight>.05f&&blend.BlendWeight<.95f)
                {
                    if(!blend.State.HasLookAt())throw new Exception(label+": blend missing target");
                    tracked++;
                    if(!captured&&blend.BlendWeight>.4f&&blend.BlendWeight<.6f)
                    {ScreenCapture.CaptureScreenshot("P:/Game_RB_Project/BuildArtifacts/CharacterMenu/tracking-"+label+".png");captured=true;}
                }
            }
            if(!ready()||tracked==0)throw new Exception(label+": no completed tracked blend");
            rows.Add(label+": "+tracked+" target-aware blend samples");
        }
        try
        {
            owner.gameObject.SetActive(true);await Observe("Status",()=>screen.CanInteract);
            var active=Get<CinemachineCamera>(manager,"activeCamera");
            if(Quaternion.Angle(active.State.GetFinalOrientation(),active.transform.rotation*Quaternion.AngleAxis(active.Lens.Dutch,Vector3.forward))>.05f)throw new Exception("Endpoint orientation changed");
            var gun=UnityEngine.Object.FindFirstObjectByType<BasementGunMenuScreenController>();
            owner.OpenWeaponEquipment();await Observe("Gun",()=>gun.CanInteract);
            gun.Close();await Observe("GunBack",()=>screen.CanInteract);
            var accessory=UnityEngine.Object.FindFirstObjectByType<BasementAccessoryScreenController>();
            owner.OpenAccessoryScreen();await Observe("Equip",()=>accessory.CanInteract);
            accessory.Close();await Observe("EquipBack",()=>screen.CanInteract);
            screen.Back();await Observe("Formation",()=>!coordinator.IsOpen);
            await Task.Delay(50);
            if(UnityEngine.Object.FindObjectsByType<BasementMenuBlendTarget>(FindObjectsSortMode.None).Length!=0)throw new Exception("Tracking extensions leaked");
        }
        finally
        {
            coordinator.Close(true);
            if(wasOpen)owner.gameObject.SetActive(true);
        }
        string result=string.Join("\n",rows);
        File.WriteAllText("P:/Game_RB_Project/BuildArtifacts/CharacterMenu/tracking-runtime.txt",result);
        return result;
    }
}
