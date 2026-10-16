using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

public static class CharacterMenuBlendStartAudit
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    public static async Task<string> Run(string label)
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var deadline=DateTime.UtcNow.AddSeconds(25);
        while(UnityEngine.Object.FindFirstObjectByType<CameraManager>()==null&&DateTime.UtcNow<deadline)await Task.Delay(100);
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var manager=UnityEngine.Object.FindFirstObjectByType<CameraManager>();
        var brain=Get<CinemachineBrain>(manager,"presentationBrain");
        var coordinator=UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>();
        var slot=owner._slot??UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).First(s=>s.Selected!=null);
        coordinator.Close(true);owner.gameObject.SetActive(false);owner.BindSlot(slot);
        await Task.Delay(500);
        var rows=new List<string>();
        var timer=Stopwatch.StartNew();double prior=0;
        Vector3 p=brain.OutputCamera.transform.position;Quaternion q=brain.OutputCamera.transform.rotation;
        float fov=brain.OutputCamera.fieldOfView;int phase=0;
        void Sample(CinemachineBrain b)
        {
            if(b!=brain)return;
            var c=b.OutputCamera;double now=timer.Elapsed.TotalMilliseconds;
            rows.Add($"{phase},{Time.frameCount},{now:F2},{now-prior:F2},{b.ActiveBlend?.BlendWeight??-1:F6},{Vector3.Distance(p,c.transform.position):F6},{Quaternion.Angle(q,c.transform.rotation):F6},{c.fieldOfView-fov:F6},{c.transform.eulerAngles.x:F3},{c.transform.eulerAngles.y:F3},{c.transform.eulerAngles.z:F3},{c.fieldOfView:F3},{c.projectionMatrix.m00:F6},{c.projectionMatrix.m11:F6}");
            prior=now;p=c.transform.position;q=c.transform.rotation;fov=c.fieldOfView;
        }
        CinemachineCore.CameraUpdatedEvent.AddListener(Sample);
        try
        {
            await Task.Delay(100);phase=1;owner.gameObject.SetActive(true);await Task.Delay(1200);
            phase=2;owner.OpenWeaponEquipment();await Task.Delay(1200);
            phase=3;UnityEngine.Object.FindFirstObjectByType<BasementGunMenuScreenController>().Close();await Task.Delay(1200);
        }
        finally{CinemachineCore.CameraUpdatedEvent.RemoveListener(Sample);}
        File.WriteAllLines("P:/Game_RB_Project/BuildArtifacts/CharacterMenu/blend-start-"+label+".csv",new[]{"phase,frame,ms,gap,weight,positionDelta,angleDelta,fovDelta,pitch,yaw,roll,fov,projectionX,projectionY"}.Concat(rows));
        return string.Join("\n",rows.Where(r=>!r.StartsWith("0,")).GroupBy(r=>r.Split(',')[0]).Select(g=>"Phase "+g.Key+"\n"+string.Join("\n",g.Take(12))));
    }
}

