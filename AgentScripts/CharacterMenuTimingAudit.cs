using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class CharacterMenuTimingAudit
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static Task Frame()
    {
        var t=new TaskCompletionSource<bool>();
        void Tick(){EditorApplication.update-=Tick;t.SetResult(true);}
        EditorApplication.update+=Tick;return t.Task;
    }
    public static string Breakdown()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var rows=new List<string>();
        foreach(var method in new[]{"LoadLavel","Calculatestatusfromslot","UpdateStatTexts","SendWeaponEquipmentData","SendAccessoryEquipmentData"})
        {
            var sw=Stopwatch.StartNew();typeof(UILoadLaval).GetMethod(method,Flags|BindingFlags.Public).Invoke(owner,null);
            rows.Add(method+": "+sw.Elapsed.TotalMilliseconds.ToString("F2")+"ms");
        }
        return string.Join("\n",rows);
    }
    public static string PresentationBreakdown()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var slot=owner._slot;
        var sw=Stopwatch.StartNew();
        var fades=new List<BasementAccessoryRendererFade>();
        foreach(var member in UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None))
            if(member!=slot)foreach(var r in member.GetComponentsInChildren<Renderer>(true))fades.Add(new BasementAccessoryRendererFade(r));
        var fade=sw.Elapsed.TotalMilliseconds;foreach(var f in fades)f.Dispose();
        sw.Restart();
        var hidden=new List<(Behaviour component,bool enabled)>();
        foreach(var root in screen.BackgroundRoots)
            if(root!=null)foreach(var c in root.GetComponentsInChildren<Behaviour>(true))
            {
                if(c.transform.IsChildOf(screen.PresentationGroup.transform))continue;
                if(c is Canvas && screen.PresentationGroup.transform.IsChildOf(c.transform))continue;
                if(c is Canvas || c is UnityEngine.UI.GraphicRaycaster || c is UnityEngine.UI.Graphic)
                {hidden.Add((c,c.enabled));c.enabled=false;}
            }
        var ui=sw.Elapsed.TotalMilliseconds;foreach(var h in hidden)h.component.enabled=h.enabled;
        return $"Fade allocation {fade:F2}ms ({fades.Count} renderers); background traversal/hide {ui:F2}ms ({hidden.Count} components)";
    }
    public static async Task<string> Run(string label)
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        var screen=owner.GetComponent<BasementCharacterMenuScreenController>();
        var coordinator=UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>();
        var slot=owner._slot;
        if(slot==null||slot.Selected==null)throw new Exception("Select character first.");
        bool wasOpen=screen.IsOpen;
        var rows=new List<string>();
        try
        {
            for(int pass=0;pass<3;pass++)
            {
                coordinator.Close(true);owner.gameObject.SetActive(false);
                await Task.Delay(300);
                var timer=Stopwatch.StartNew();
                owner.BindSlot(slot);double bind=timer.Elapsed.TotalMilliseconds;
                owner.gameObject.SetActive(true);double enable=timer.Elapsed.TotalMilliseconds-bind;
                double beforeOpen=timer.Elapsed.TotalMilliseconds;
                typeof(BasementCharacterMenuScreenController).GetMethod("Update",Flags).Invoke(screen,null);
                double open=timer.Elapsed.TotalMilliseconds-beforeOpen;
                double prior=timer.Elapsed.TotalMilliseconds,maxGap=0,visible=-1;int frames=0;
                var group=screen.PresentationGroup;
                while(!screen.CanInteract&&timer.Elapsed.TotalSeconds<6)
                {
                    await Frame();double now=timer.Elapsed.TotalMilliseconds;
                    maxGap=Math.Max(maxGap,now-prior);prior=now;frames++;
                    if(visible<0&&group.alpha>.01f)visible=now;
                }
                rows.Add($"{label} pass {pass}: Bind={bind:F2}ms Enable={enable:F2}ms Open={open:F2}ms FirstVisible={visible:F1}ms Ready={timer.Elapsed.TotalMilliseconds:F1}ms MaxUpdateGap={maxGap:F1}ms Frames={frames}");
            }
        }
        finally
        {
            if(!wasOpen){coordinator.Close(true);owner.gameObject.SetActive(false);}
        }
        var result=string.Join("\n",rows);
        File.AppendAllText("P:/Game_RB_Project/BuildArtifacts/CharacterMenu/timing.txt",result+"\n");return result;
    }
}
