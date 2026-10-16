using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

public static class BasementMenuRuntimeAudit
{
    const string Evidence = "P:/Game_RB_Project/BuildArtifacts/BasementStatusIdleFix/Runtime";
    static async Task Wait(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(30);
        if (!condition()) throw new Exception("Presentation did not reach the expected state.");
    }
    public static async Task<string> Aspect(int width, int height)
    {
        SetSize(width, height);
        await Wait(() => Screen.width == width && Screen.height == height);
        string result = await Run(0);
        foreach (var menu in new[] { "Accessory", "GunMenu" })
            File.Copy(Evidence + "/Aires-Slot0-" + menu + ".png", Evidence + "/Aires-" + width + "x" + height + "-" + menu + ".png", true);
        return width + "x" + height + ": " + result;
    }

    public static string RestoreSize()
    {
        var t = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var v = UnityEngine.Resources.FindObjectsOfTypeAll(t)[0];
        t.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(v, 3);
        return "Restored Full HD";
    }

    static void SetSize(int width, int height)
    {
        var a = typeof(Editor).Assembly;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var t = a.GetType("UnityEditor.GameView");
        var view = UnityEngine.Resources.FindObjectsOfTypeAll(t)[0];
        var sizesType = a.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", flags).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { t.GetProperty("currentSizeGroupType", flags).GetValue(view) });
        var names = (string[])group.GetType().GetMethod("GetDisplayTexts", flags).Invoke(group, null);
        string label = "Menu QA " + width + "x" + height;
        int index = Array.FindIndex(names, n => n.StartsWith(label));
        if (index < 0)
        {
            var sizeType = a.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"), new object[] { Enum.Parse(sizeType, "FixedResolution"), width, height, label });
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
            index = names.Length;
        }
        t.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
        ((EditorWindow)view).Repaint();
    }
    public static async Task<string> Run(int index)
    {
        var names = new[] { "Aires", "Roma", "Feno", "Milano", "Abbygail", "Dorothy", "Noemi", "Roger" };
        var name = names[index];
        var definition = AssetDatabase.FindAssets("t:CharacterStats").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith("Assets/Tests/"))
            .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>).Single(d => d.name.EndsWith("." + name));
        var slot = UnityEngine.Object.FindObjectsByType<PartySlot>(FindObjectsSortMode.None).Single(s => s.PartyIndex == index % 4);
        var preview = slot.GetComponent<PartySlotVisualPreview>();
        var previous = slot.Selected;
        string previousId = slot.IDCharacter;
        var selected = typeof(PartySlot).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic);
        var accessory = UnityEngine.Object.FindFirstObjectByType<BasementAccessoryScreenController>();
        var gun = UnityEngine.Object.FindFirstObjectByType<BasementGunMenuScreenController>();
        var central = UnityEngine.Object.FindFirstObjectByType<BasementMenuPresentationController>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        var info = UnityEngine.Object.FindFirstObjectByType<UILoadLaval>(FindObjectsInactive.Include);
        bool infoWasActive = info.gameObject.activeSelf;
        var brain = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();
        var originalRect = brain.OutputCamera.rect;
        var originalBlends = brain.CustomBlends;
        Directory.CreateDirectory(Evidence);
        try
        {
            info.gameObject.SetActive(false);
            selected.SetValue(slot, definition); slot.IDCharacter = definition.characterId;
            preview.BuildCharacter(definition, slot.transform);
            var graph = preview.Presentation.GetComponentInChildren<Animancer.AnimancerComponent>(true);
            if (graph.States.Current.Clip.name != "Aires_Formation_Idle") throw new Exception("Formation idle missing.");
            if (index == 0)
            {
                var idle = graph.States.Current;
                float before = idle.Time;
                await Task.Delay(350);
                if (idle.Time <= before) throw new Exception("Formation idle did not advance.");
                ScreenCapture.CaptureScreenshot(Evidence + "/Aires-FormationIdle.png");
                await Task.Delay(120);
            }
            preview.Presentation.SetPicked(true);
            if (graph.States.Current.Clip.name != "Aires_Formation_Pickup") throw new Exception("Pickup missing.");
            preview.Presentation.SetPicked(false);
            info.gameObject.SetActive(true);
            info.BindSlot(slot);
            await Task.Delay(60);
            if (graph.States.Current.Clip.name != "Aires_Inmenu_Idle") throw new Exception("Status idle missing.");
            if (index == 0)
            {
                var status = graph.States.Current;
                float before = status.Time;
                await Task.Delay(350);
                if (status.Time <= before) throw new Exception("Status idle did not advance.");
                ScreenCapture.CaptureScreenshot(Evidence + "/Aires-StatusIdle.png");
                await Task.Delay(120);
            }
            foreach (bool isGun in new[] { false, true })
            {
                if (isGun) info.OpenWeaponEquipment(); else info.OpenAccessoryScreen();
                await Wait(() => isGun ? gun.CanInteract : accessory.CanInteract);
                if (preview.Presentation.CurrentDef != definition || preview.Animator == null)
                    throw new Exception("Wrong presentation owner or missing Animator.");
                var animancer = preview.Presentation.GetComponentInChildren<Animancer.AnimancerComponent>(true);
                var state = animancer.States.Current;
                string expected = isGun ? "Aires_GunMenu_Idle" : "Aires_AccessoryMenu_Idle";
                if (state.Clip.name != expected || !state.Clip.isLooping)
                    throw new Exception("Wrong menu clip or missing loop: " + state.Clip.name);
                float time = state.Time;
                await Task.Delay(250);
                if (state.Time <= time) throw new Exception("Menu animation did not advance.");
                if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c => c.name == "Camera Gun Menu"))
                    throw new Exception("A reference camera is rendering.");
                ScreenCapture.CaptureScreenshot(Evidence + "/" + name + "-Slot" + slot.PartyIndex + (isGun ? "-GunMenu.png" : "-Accessory.png"));
                await Task.Delay(120);
                if (isGun) gun.Close(); else accessory.Close();
                await Wait(() => !central.IsOpen);
                if (graph.States.Current.Clip.name != "Aires_Inmenu_Idle") throw new Exception("Back did not restore status idle.");
                if (brain.OutputCamera.rect != originalRect || brain.CustomBlends != originalBlends)
                    throw new Exception("Camera state was not restored.");
            }
            info.gameObject.SetActive(false);
            if (graph.States.Current.Clip.name != "Aires_Formation_Idle") throw new Exception("Closing status did not restore formation idle.");
            string result = name + ": slot " + slot.PartyIndex + ", formation idle/pickup/status idle/both submenus/Back/status close passed; viewport restored, no FBX camera.";
            File.AppendAllText(Evidence + "/audit.txt", result + Environment.NewLine);
            return result;
        }
        finally
        {
            central.Close(true);
            selected.SetValue(slot, previous); slot.IDCharacter = previousId;
            preview.BuildCharacter(previous, slot.transform);
            info.BindSlot(slot);
            info.gameObject.SetActive(infoWasActive);
        }
    }
}
