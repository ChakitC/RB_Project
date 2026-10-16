using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public static class CharacterMenuExitBlendAudit
{
    public static async Task<string> Run()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var rows = new List<string>();
        foreach (var controller in UnityEngine.Object.FindObjectsByType<BasementAnimationController>(FindObjectsSortMode.None))
        {
            var profile = (BasementAnimationProfileSO)typeof(BasementAnimationController).GetField("profile", flags).GetValue(controller);
            var animancer = (AnimancerComponent)typeof(BasementAnimationController).GetField("animancer", flags).GetValue(controller);
            var entry = profile.menus.FirstOrDefault(e => e.menu != null && e.menu.name == "CharacterMenu");
            if (entry == null) continue;
            controller.EnterMenu(entry);
            await Task.Delay(700);
            var outgoing = animancer.States.Current;
            controller.ExitMenu();
            var incoming = animancer.States.Current;
            float start = incoming.Weight;
            await Task.Delay(90);
            float middle = incoming.Weight;
            float remaining = outgoing.Weight;
            await Task.Delay(350);
            if (!(start < middle && middle < 1 && remaining > 0 && incoming.Weight > .999f))
                throw new Exception(profile.name + ": not blended " + start + "/" + middle + "/" + incoming.Weight);
            rows.Add(profile.name + ": idle weights " + start + " -> " + middle + " -> " + incoming.Weight + "; menu mid-weight " + remaining);
        }
        return string.Join("\n", rows);
    }
}
