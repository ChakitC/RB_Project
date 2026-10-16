#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BasementAccessoryTestFixture
{
    const int TestSlot = 907131;
    const string Owner = "character:AccessoryScreenTest";
    readonly List<Object> owned = new();
    readonly Dictionary<string, byte[]> savedFiles = new();
    SaveManager originalManager;
    PlayerInventory inventory;
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Hidden).GetValue(obj);
    static void Call(object obj, string method) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, null);

    public void DitherFadePreservesPropertyBlocksWithoutMaterialCopies()
    {
        var root = Own(new GameObject("Dither property block test"));
        var renderer = root.AddComponent<MeshRenderer>();
        var material = Own(new Material(Shader.Find("ZLZ/AnimeToon/Character")));
        material.SetFloat("_DitherAlpha", .2f);
        renderer.sharedMaterials = new[] { material, material };
        var global = new MaterialPropertyBlock();
        global.SetFloat("_DitherAlpha", .3f); global.SetFloat("_TestValue", 7f);
        renderer.SetPropertyBlock(global);
        var indexed = new MaterialPropertyBlock();
        indexed.SetFloat("_DitherAlpha", .4f); indexed.SetFloat("_TestValue", 9f);
        renderer.SetPropertyBlock(indexed, 1);
        var check = new MaterialPropertyBlock();
        using (var fade = new BasementAccessoryRendererFade(renderer))
        {
            fade.Apply(.5f);
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { material, material }));
            renderer.GetPropertyBlock(check, 0);
            Assert.That(check.GetFloat("_DitherAlpha"), Is.EqualTo(.65f).Within(.001f));
            Assert.That(check.GetFloat("_TestValue"), Is.EqualTo(7f));
            renderer.GetPropertyBlock(check, 1);
            Assert.That(check.GetFloat("_DitherAlpha"), Is.EqualTo(.7f).Within(.001f));
            Assert.That(check.GetFloat("_TestValue"), Is.EqualTo(9f));
            Assert.That(material.GetFloat("_DitherAlpha"), Is.EqualTo(.2f));
            fade.Apply(1); Assert.That(renderer.forceRenderingOff, Is.True);
        }
        renderer.GetPropertyBlock(check, 0); Assert.That(check.isEmpty, Is.True);
        renderer.GetPropertyBlock(check, 1);
        Assert.That(check.GetFloat("_DitherAlpha"), Is.EqualTo(.4f));
        Assert.That(check.GetFloat("_TestValue"), Is.EqualTo(9f));
        renderer.GetPropertyBlock(check);
        Assert.That(check.GetFloat("_DitherAlpha"), Is.EqualTo(.3f));
        Assert.That(renderer.forceRenderingOff, Is.False);
    }

    static float Dither(Renderer renderer)
    {
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
        return block.HasFloat("_DitherAlpha") ? block.GetFloat("_DitherAlpha") : renderer.sharedMaterial.GetFloat("_DitherAlpha");
    }

    public void Setup()
    {
        Directory.CreateDirectory(SaveSystem.CurrentDirectory);
        foreach (var path in Directory.GetFiles(SaveSystem.CurrentDirectory, $"slot_{TestSlot}_*.json"))
        { savedFiles[path] = File.ReadAllBytes(path); File.Delete(path); }
        originalManager = SaveManager.Instance;
        var managerObject = Own(new GameObject("Accessory test save manager")); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<SaveManager>(); manager.currentSlot = TestSlot;
        typeof(SaveManager).GetProperty("Instance").SetValue(null, manager);
        var inventoryObject = Own(new GameObject("Accessory test inventory")); inventoryObject.SetActive(false);
        inventory = inventoryObject.AddComponent<PlayerInventory>(); Call(inventory, "Awake");
    }

    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        typeof(SaveManager).GetProperty("Instance").SetValue(null, originalManager);
        foreach (var path in Directory.GetFiles(SaveSystem.CurrentDirectory, $"slot_{TestSlot}_*.json")) File.Delete(path);
        foreach (var pair in savedFiles) File.WriteAllBytes(pair.Key, pair.Value);
        savedFiles.Clear();
    }

    InventorySlotData Item(string id, bool unique = false, AccessoryDefinition definition = null)
    {
        if (definition == null)
        {
            definition = Own(ScriptableObject.CreateInstance<AccessoryDefinition>());
            definition.itemId = "test-" + id; definition.displayName = id;
            definition.itemType = ItemType.Accessory; definition.uniqueEquip = unique;
        }
        var slot = new InventorySlotData();
        slot.SetAccessoryInstance(definition, new AccessoryInstanceData {instanceId=id, accessoryId=definition.itemId});
        int index = inventory.slots.FindIndex(s => s.IsEmpty);
        inventory.slots[index] = slot;
        return slot;
    }

    public void EquipReplaceAndPersistAllFiveSlots()
    {
        for (int i = 0; i < 5; i++)
        {
            var item = Item("slot-" + i);
            Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, Owner, item.accessoryInstance.instanceId, i), Is.True);
            Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Accessory, Owner, i).accessoryInstance.instanceId, Is.EqualTo("slot-" + i));
        }
        var replacement = Item("replacement");
        Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, Owner, "replacement", 2), Is.True);
        var saved = AccessoryLoadout.FindLoadoutEntry(SaveSystem.LoadAccessories(TestSlot), Owner);
        Assert.That(saved.equippedAccessories[2].instanceId, Is.EqualTo("replacement"));
        Assert.That(saved.equippedAccessories[1].instanceId, Is.EqualTo("slot-1"));
        Assert.That(inventory.GetAccessoryInstance("slot-2"), Is.Not.Null);
        Assert.That(EquipmentAssignmentService.TryUnequip(inventory, EquipmentItemKind.Accessory, Owner, 2), Is.True);
        SaveManager.Instance.RefreshLoadedCacheFromDisk();
        saved = AccessoryLoadout.FindLoadoutEntry(SaveSystem.LoadAccessories(TestSlot), Owner);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.equippedAccessories[2] == null || saved.equippedAccessories[2].IsEmpty, Is.True);
        Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Accessory, Owner, 2).IsEmpty, Is.True);
    }

    public void RejectUniqueDuplicateAndOtherOwnerWithoutChangingAssignments()
    {
        var a = Item("unique-a", true);
        var b = Item("unique-b", true, (AccessoryDefinition)a.item);
        Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, Owner, "unique-a", 0), Is.True);
        Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, Owner, "unique-b", 1), Is.False);
        Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, "character:OtherTest", "unique-a", 0), Is.False);
        Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Accessory, Owner, 0).accessoryInstance.instanceId, Is.EqualTo("unique-a"));
        Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Accessory, Owner, 1).IsEmpty, Is.True);
    }

    public void PrefabAndSceneBindingsAreComplete()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/BasementAccessoryScreen.prefab");
        Assert.That(prefab, Is.Not.Null);
        var controller = prefab.GetComponent<BasementAccessoryScreenController>();
        var slots = Get<BasementAccessorySlotView[]>(controller, "slots");
        Assert.That(slots.Select(s => s.SlotIndex), Is.EqualTo(new[]{0,1,2,3,4}));
        Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<CanvasScaler>().screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(prefab.GetComponentsInChildren<Image>(true).Where(i => i.name.Contains("_Color") || i.name.Contains("BG_Disclipstion")).All(i => !i.raycastTarget), Is.True);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Basement/Basement.unity");
        try
        {
            T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
            var screen = Find<BasementAccessoryScreenController>();
            Assert.That(Get<GameObject[]>(screen,"backgroundRoots").All(r=>r!=null),Is.True);
            Assert.That(Get<CameraManager>(screen,"cameraManager"),Is.Not.Null);
            var level = Find<UILoadLaval>();
            Assert.That(Get<BasementAccessoryScreenController>(level,"accessoryScreen"),Is.EqualTo(screen));
            var button = Get<Button>(level,"accessoryScreenButton");
            Assert.That(button.onClick.GetPersistentMethodName(0),Is.EqualTo("OpenAccessoryScreen"));
            Assert.That(Get<GameObject>(level,"weaponEquipmentObject"),Is.Not.Null);
            var camera = scene.GetRootGameObjects().Single(r=>r.name=="AccessoryCamera");
            Assert.That(camera.GetComponent<Camera>(),Is.Null);
            var lens = camera.GetComponent<CinemachineCamera>().Lens;
            Assert.That(lens.FieldOfView,Is.EqualTo(53.1301f).Within(.001f));
            Assert.That(lens.PhysicalProperties.SensorSize,Is.EqualTo(new Vector2(36,24)));
            Assert.That(Find<CinemachineBrain>().DefaultBlend.Time,Is.EqualTo(2));
            Assert.That(Find<CinemachineBrain>().CustomBlends.CustomBlends.Where(b=>b.From=="AccessoryCamera"||b.To=="AccessoryCamera").All(b=>Mathf.Approximately(b.Blend.Time,.8f)),Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public IEnumerator OpenPickCancelEquipAndRestore()
    {
        var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/BasementAccessoryScreen.prefab")));
        var screen = root.GetComponent<BasementAccessoryScreenController>();
        var cameraObject=Own(new GameObject("Test output camera",typeof(Camera),typeof(CinemachineBrain)));
        var brain=cameraObject.GetComponent<CinemachineBrain>();
        var managerObject=Own(new GameObject("Camera manager"));managerObject.SetActive(false);
        var manager=managerObject.AddComponent<CameraManager>();
        CinemachineCamera Camera(string name) => Own(new GameObject(name)).AddComponent<CinemachineCamera>();
        manager.CharacterSelectCamera=Camera("Character");manager.StadeSelectCamera=Camera("Map");
        var accessory=Camera("Accessory");Set(manager,"accessoryCamera",accessory);Set(manager,"presentationBrain",brain);
        var originalLens = manager.CharacterSelectCamera.Lens;
        originalLens.FieldOfView = 42.561176f;
        manager.CharacterSelectCamera.Lens = originalLens;
        var physicalLens = accessory.Lens;
        physicalLens.ModeOverride = LensSettings.OverrideModes.Physical;
        physicalLens.FieldOfView = 53.1301f;
        physicalLens.PhysicalProperties.SensorSize = new Vector2(36, 24);
        physicalLens.PhysicalProperties.GateFit = UnityEngine.Camera.GateFitMode.Horizontal;
        accessory.Lens = physicalLens;
        Set(manager,"accessoryBlendDuration",0.8f);brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,0.8f);
        manager.RegisterMenu(Get<BasementMenuDefinitionSO>(screen, "menuDefinition"), accessory);
        managerObject.SetActive(true);
        var background=Own(new GameObject("Background",typeof(Canvas),typeof(GraphicRaycaster)));
        var nested=Own(new GameObject("Nested",typeof(Canvas)));nested.transform.SetParent(background.transform);
        nested.GetComponent<Canvas>().enabled=false;
        Set(screen,"backgroundRoots",new[]{background});Set(screen,"cameraManager",manager);
        var select=Own(new GameObject("Selection")).AddComponent<CharacterSelectManager>();Set(screen,"characterSelection",select);
        var slotObject=Own(new GameObject("Selected slot"));slotObject.SetActive(false);
        var slot=slotObject.AddComponent<PartySlot>();
        var stats=Own(ScriptableObject.CreateInstance<CharacterStats>());stats.characterId="AccessoryScreenTest";
        Set(slot,"selected",stats);slot.IDCharacter=stats.characterId;slot.enabled=false;
        slotObject.SetActive(true);
        var otherObject = Own(new GameObject("Other party member"));
        otherObject.SetActive(false);
        var otherSlot = otherObject.AddComponent<PartySlot>();
        Set(otherSlot, "selected", stats);
        otherSlot.enabled = false;
        otherObject.SetActive(true);
        var body = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        body.transform.SetParent(otherObject.transform);
        var bodyRenderer = body.GetComponent<Renderer>();
        var originalBodyMaterial = Own(new Material(Shader.Find("ZLZ/AnimeToon/Character")));
        originalBodyMaterial.SetFloat("_DitherAlpha", 0.2f);
        bodyRenderer.sharedMaterial = originalBodyMaterial;
        var selectedBody = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        selectedBody.transform.SetParent(slotObject.transform);
        selectedBody.GetComponent<Renderer>().sharedMaterial = originalBodyMaterial;
        var eye = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        eye.transform.SetParent(otherObject.transform);
        var eyeRenderer = eye.GetComponent<Renderer>();
        var originalEyeMaterial = Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));
        eyeRenderer.sharedMaterial = originalEyeMaterial;
        var hiddenMesh = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        hiddenMesh.transform.SetParent(otherObject.transform);
        var hiddenRenderer = hiddenMesh.GetComponent<Renderer>();
        hiddenRenderer.sharedMaterial = originalBodyMaterial;
        hiddenRenderer.forceRenderingOff = true;
        var a=Item("clicked-item");
        var group = root.GetComponent<CanvasGroup>();
        yield return null;
        yield return null;
        screen.Open(slot,inventory);
        Assert.That(bodyRenderer.forceRenderingOff, Is.False, "Other members must fade before disabling rendering.");
        Assert.That(selectedBody.GetComponent<Renderer>().sharedMaterial, Is.SameAs(originalBodyMaterial));
        Assert.That(manager.CharacterSelectCamera.Lens.IsPhysicalCamera, Is.True);
        Assert.That(brain.IgnoreTimeScale, Is.True);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(group.blocksRaycasts, Is.True);
        Assert.That(screen.CanInteract, Is.False);
        screen.SelectSlot(4);
        Assert.That(screen.IsPickerOpen, Is.False);
        yield return null;
        Assert.That(screen.IsOpen,Is.True);Assert.That(background.GetComponent<Canvas>().enabled,Is.False);Assert.That(select.enabled,Is.False);
        float deadline = Time.unscaledTime + 5f;
        bool sawPartialAlpha = false;
        bool sawFadeDuringBlend = false;
        bool sawCharacterFade = false;
        float originalTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            while (!screen.CanInteract && Time.unscaledTime < deadline)
            {
                sawPartialAlpha |= group.alpha > 0f && group.alpha < 1f;
                float dither = Dither(bodyRenderer);
                sawCharacterFade |= dither > 0.2f && dither < 1f && !bodyRenderer.forceRenderingOff;
                Assert.That(originalBodyMaterial.GetFloat("_DitherAlpha"), Is.EqualTo(0.2f));
                Assert.That(hiddenRenderer.forceRenderingOff, Is.True);
                sawFadeDuringBlend |= brain.IsBlending && group.alpha > 0f && group.alpha < 1f;
                if (manager.GetAccessoryRevealProgress(0.5f) <= 0f) Assert.That(group.alpha, Is.Zero);
                if (brain.IsBlending)
                    Assert.That(cameraObject.GetComponent<UnityEngine.Camera>().usePhysicalProperties, Is.True, "Projection mode must not switch at the blend midpoint.");
                Assert.That(group.interactable, Is.False);
                yield return null;
            }
        }
        finally { Time.timeScale = originalTimeScale; }
        Assert.That(sawPartialAlpha, Is.True, "Reveal must fade, including while game time is paused.");
        Assert.That(sawFadeDuringBlend, Is.True, "UI must overlap the final camera movement.");
        Assert.That(sawCharacterFade, Is.True, "Other members must gradually dither out on unscaled time.");
        Assert.That(bodyRenderer.forceRenderingOff, Is.True);
        Assert.That(eyeRenderer.sharedMaterial.GetColor("_BaseColor").a, Is.Zero);
        Assert.That(group.alpha, Is.EqualTo(1f));
        Assert.That(screen.CanInteract,Is.True);
        screen.SelectSlot(4);Assert.That(screen.IsPickerOpen,Is.True);
        screen.Back();Assert.That(screen.IsPickerOpen,Is.False);Assert.That(screen.IsOpen,Is.True);
        screen.SelectSlot(4);
        var picker=Get<UIEquipment>(screen,"picker");
        picker.HandleSlotClick(0,new PointerEventData(null){button=PointerEventData.InputButton.Left});
        Assert.That(screen.IsPickerOpen,Is.False);
        string owner=CharacterEquipment.BuildCharacterOwnerId(stats.characterId);
        Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory,EquipmentItemKind.Accessory,owner,4).accessoryInstance.instanceId,Is.EqualTo("clicked-item"));
        screen.SelectSlot(4);screen.UnequipSelected();
        Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory,EquipmentItemKind.Accessory,owner,4).IsEmpty,Is.True);
        screen.Close();
        deadline = Time.unscaledTime + 5f;
        bool sawCharacterReturn = false;
        while (screen.IsOpen && Time.unscaledTime < deadline)
        {
            float dither = Dither(bodyRenderer);
            sawCharacterReturn |= dither > 0.2f && dither < 1f && !bodyRenderer.forceRenderingOff;
            yield return null;
        }
        Assert.That(sawCharacterReturn, Is.True);
        Assert.That(bodyRenderer.sharedMaterial, Is.SameAs(originalBodyMaterial));
        Assert.That(eyeRenderer.sharedMaterial, Is.SameAs(originalEyeMaterial));
        Assert.That(bodyRenderer.forceRenderingOff, Is.False);
        Assert.That(hiddenRenderer.forceRenderingOff, Is.True);
        Assert.That(screen.IsOpen,Is.False);Assert.That(background.GetComponent<Canvas>().enabled,Is.True);
        Assert.That(nested.GetComponent<Canvas>().enabled,Is.False);Assert.That(select.enabled,Is.True);
        Assert.That(manager.CharacterSelectCamera.Lens.ModeOverride, Is.EqualTo(originalLens.ModeOverride));
        Assert.That(manager.CharacterSelectCamera.Lens.FieldOfView, Is.EqualTo(originalLens.FieldOfView));
        Assert.That(cameraObject.GetComponent<UnityEngine.Camera>().usePhysicalProperties, Is.False);
        Assert.That(brain.IgnoreTimeScale, Is.False);
        // Allow the completed camera return to settle before reopening.
        yield return null;
        screen.Open(slot,inventory);
        deadline = Time.unscaledTime + 5f;
        while (group.alpha <= 0f && Time.unscaledTime < deadline) yield return null;
        Assert.That(group.alpha, Is.GreaterThan(0f).And.LessThan(1f));
        screen.Close();
        while (screen.IsOpen && Time.unscaledTime < deadline) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(screen.IsOpen, Is.False);
        Assert.That(group.alpha, Is.Zero, "Cancelled fade must not reveal a closed screen.");
        screen.Open(slot,inventory);yield return null;
        root.SetActive(false);
        Assert.That(screen.IsOpen,Is.False);Assert.That(background.GetComponent<Canvas>().enabled,Is.True);Assert.That(select.enabled,Is.True);
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.That(group.alpha, Is.Zero);
        root.SetActive(true);
        screen.Open(slot, inventory);
        Object.DestroyImmediate(cameraObject);
        root.SetActive(false);
        Assert.That(screen.IsOpen, Is.False, "Scene teardown may destroy the output camera before the UI.");
    }
}
#endif
