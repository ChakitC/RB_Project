#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BasementGunMenuTestFixture
{
    const string PrefabPath = "Assets/Prefab/User Interface/BasementGunMenuScreen.prefab";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    CameraManager manager;
    CinemachineBrain brain;
    BasementGunMenuScreenController screen;
    PartySlot slot;
    Canvas background;
    Canvas nested;
    CharacterSelectManager selection;
    UILoadLaval entry;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Hidden).GetValue(obj);

    public void Cleanup()
    {
        if (screen != null) screen.gameObject.SetActive(false);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    public void AuthoringUsesEquipmentButtonAndReferenceLens()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant));
        Assert.That(prefab.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(prefab.GetComponentsInChildren<Image>(true).Count(x => x.sprite != null), Is.EqualTo(12));
        var back = prefab.transform.Find("Artboard/Back/HitArea").GetComponent<Button>();
        Assert.That(back.onClick.GetPersistentMethodName(0), Is.EqualTo("Back"));
        Assert.That(back.onClick.GetPersistentTarget(0), Is.EqualTo(prefab.GetComponent<BasementGunMenuScreenController>()));
        var information = prefab.transform.Find("Artboard/Information Button/HitArea").GetComponent<Button>();
        Assert.That(information.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(information.interactable, Is.False);
        Assert.That(prefab.GetComponentInChildren<BasementGunMenuWeaponDropSlot>(true), Is.Not.Null);
        Assert.That(prefab.transform.Find("Artboard/Guninventory/Dummy_GunItem").gameObject.activeSelf, Is.False);
        Assert.That(prefab.GetComponent<BasementGunMenuWeapons>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<CanvasScaler>().screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(Get<CharacterDatabase>(prefab.GetComponent<BasementGunMenuWeapons>(), "characterDatabase"), Is.Not.Null);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Basement/Basement.unity");
        try
        {
            T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
            var view = Find<BasementGunMenuScreenController>();
            var level = Find<UILoadLaval>();
            var cameraManager = Find<CameraManager>();
            Assert.That(Get<BasementGunMenuScreenController>(level, "gunMenuScreen"), Is.EqualTo(view));
            Assert.That(Get<CameraManager>(view, "cameraManager"), Is.EqualTo(cameraManager));
            Assert.That(Get<GameObject[]>(view, "backgroundRoots").All(x => x != null), Is.True);
            var equipmentButtons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true))
                .Where(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i =>
                    b.onClick.GetPersistentTarget(i) == level && b.onClick.GetPersistentMethodName(i) == "OpenWeaponEquipment")).ToArray();
            Assert.That(equipmentButtons.Length, Is.EqualTo(1));
            var camera = Get<CinemachineCamera>(cameraManager, "gunMenuCamera");
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.gameObject.scene, Is.EqualTo(scene));
            Assert.That(camera.GetComponent<Camera>(), Is.Null);
            var referenceRoot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spain/UI/Gunmenu/Aires_camera_Gunmenumene.fbx");
            var reference = referenceRoot.GetComponentsInChildren<Camera>(true).Single();
            Assert.That(camera.Lens.IsPhysicalCamera, Is.True);
            Assert.That(camera.Lens.FieldOfView, Is.EqualTo(reference.fieldOfView).Within(.0001f));
            Assert.That(camera.Lens.PhysicalProperties.SensorSize, Is.EqualTo(reference.sensorSize));
            Assert.That(camera.Lens.PhysicalProperties.GateFit, Is.EqualTo(reference.gateFit));
            Assert.That(Vector3.Distance(Get<Vector3>(cameraManager, "gunMenuCameraOffset"), reference.transform.position), Is.LessThan(.00001f));
            var rules = Find<CinemachineBrain>().CustomBlends.CustomBlends.Where(b => b.From == "GunMenuCamera" || b.To == "GunMenuCamera").ToArray();
            Assert.That(rules.Length, Is.EqualTo(2));
            Assert.That(rules.All(b => Mathf.Approximately(b.Blend.Time, .8f)), Is.True);
            Assert.That(Find<CinemachineBrain>().DefaultBlend.Time, Is.EqualTo(2));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    void SetupRuntime()
    {
        var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
        screen = root.GetComponent<BasementGunMenuScreenController>();
        var output = Own(new GameObject("Gun test output", typeof(Camera), typeof(CinemachineBrain)));
        brain = output.GetComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .2f);
        var managerObject = Own(new GameObject("Gun test manager")); managerObject.SetActive(false);
        manager = managerObject.AddComponent<CameraManager>();
        CinemachineCamera Cam(string name) => Own(new GameObject(name)).AddComponent<CinemachineCamera>();
        manager.CharacterSelectCamera = Cam("Gun test character");
        manager.StadeSelectCamera = Cam("Gun test map");
        manager.StadeSelectCamera.Priority = -10;
        var gun = Cam("Gun test camera"); gun.Priority = -10;
        var accessory = Cam("Gun test accessory"); accessory.Priority = -10;
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spain/UI/Gunmenu/Aires_camera_Gunmenumene.fbx").GetComponentInChildren<Camera>(true);
        var lens = LensSettings.FromCamera(reference); lens.ModeOverride = LensSettings.OverrideModes.Physical;
        gun.Lens = lens; accessory.Lens = lens;
        Set(manager, "gunMenuCamera", gun); Set(manager, "accessoryCamera", accessory);
        Set(manager, "presentationBrain", brain);
        Set(manager, "gunMenuBlendDuration", .2f); Set(manager, "accessoryBlendDuration", .2f);
        Set(manager, "gunMenuCameraOffset", reference.transform.position);
        Set(manager, "gunMenuCameraEuler", reference.transform.eulerAngles);
        var menu = Own(Object.Instantiate(Get<BasementMenuDefinitionSO>(screen, "menuDefinition")));
        menu.blendDuration = .2f; menu.uiFadeDuration = .1f;
        Set(screen, "menuDefinition", menu);
        manager.RegisterMenu(menu, gun);
        managerObject.SetActive(true);
        Set(screen, "cameraManager", manager); Set(screen, "fadeInDuration", .1f);
        background = Own(new GameObject("Gun test background", typeof(Canvas), typeof(GraphicRaycaster))).GetComponent<Canvas>();
        nested = Own(new GameObject("Gun test nested", typeof(Canvas))).GetComponent<Canvas>();
        nested.transform.SetParent(background.transform); nested.enabled = false;
        Set(screen, "backgroundRoots", new[] { background.gameObject });
        selection = Own(new GameObject("Gun test selection")).AddComponent<CharacterSelectManager>();
        Set(screen, "characterSelection", selection);
        var slotObject = Own(new GameObject("Gun test slot")); slotObject.SetActive(false);
        slot = slotObject.AddComponent<PartySlot>(); slot.enabled = false;
        var stats = Own(ScriptableObject.CreateInstance<CharacterStats>()); stats.characterId = "GunMenuTest";
        Set(slot, "selected", stats); slot.IDCharacter = stats.characterId;
        slotObject.SetActive(true);
        var entryObject = Own(new GameObject("Gun test equipment entry")); entryObject.SetActive(false);
        entry = entryObject.AddComponent<UILoadLaval>();
        entry._slot = slot; Set(entry, "gunMenuScreen", screen);
    }

    static IEnumerator WaitFor(Func<bool> condition)
    {
        float deadline = Time.unscaledTime + 5f;
        while (!condition() && Time.unscaledTime < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for camera/UI transition");
    }

    public IEnumerator EquipmentOpenBackRestoreAndMutualExclusion()
    {
        SetupRuntime();
        yield return null; yield return null;
        var originalLens = manager.CharacterSelectCamera.Lens;
        var originalViewport = brain.OutputCamera.rect;
        var originalPriority = Get<CinemachineCamera>(manager, "gunMenuCamera").Priority;
        screen.Open(null); Assert.That(screen.IsOpen, Is.False);
        manager._inMapUI = true; entry.OpenWeaponEquipment(); Assert.That(screen.IsOpen, Is.False);
        manager._inMapUI = false;
        entry.OpenWeaponEquipment();
        Assert.That(screen.IsOpen, Is.True, "EQUIPMENT must route to the new screen");
        Assert.That(screen.CanInteract, Is.False);
        Assert.That(manager.BeginAccessoryView(slot.transform), Is.False);
        screen.Back(); Assert.That(screen.IsOpen, Is.True, "Back is gated during opening");
        entry.OpenWeaponEquipment();
        Assert.That(background.enabled, Is.False); Assert.That(selection.enabled, Is.False);
        float timeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            yield return WaitFor(() => screen.CanInteract);
            Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(brain.OutputCamera.usePhysicalProperties, Is.True);
            screen.transform.Find("Artboard/Back/HitArea").GetComponent<Button>().onClick.Invoke();
            entry.OpenWeaponEquipment();
            yield return WaitFor(() => !screen.IsOpen);
        }
        finally { Time.timeScale = timeScale; }
        Assert.That(background.enabled, Is.True); Assert.That(nested.enabled, Is.False);
        Assert.That(selection.enabled, Is.True);
        Assert.That(manager.CharacterSelectCamera.Lens.FieldOfView, Is.EqualTo(originalLens.FieldOfView));
        Assert.That(manager.CharacterSelectCamera.Lens.ModeOverride, Is.EqualTo(originalLens.ModeOverride));
        Assert.That(brain.OutputCamera.rect, Is.EqualTo(originalViewport));
        Assert.That(brain.IgnoreTimeScale, Is.False);
        Assert.That(Get<CinemachineCamera>(manager, "gunMenuCamera").Priority.Value, Is.EqualTo(originalPriority.Value));
        Assert.That(manager.BeginAccessoryView(slot.transform), Is.True);
        screen.Open(slot); Assert.That(screen.IsOpen, Is.False);
        manager.EndAccessoryView(true);
        yield return null;
        entry.OpenWeaponEquipment();
        yield return WaitFor(() => screen.CanInteract);
        entry.CloseWeaponEquipment();
        yield return WaitFor(() => !screen.IsOpen);
    }

    public IEnumerator InterruptedOpenAndLostTargetRestoreState()
    {
        SetupRuntime(); yield return null; yield return null;
        entry.OpenWeaponEquipment(); yield return null;
        screen.Close(); yield return WaitFor(() => !screen.IsOpen);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.Zero);
        entry.OpenWeaponEquipment(); yield return null;
        screen.gameObject.SetActive(false);
        Assert.That(screen.IsOpen, Is.False); Assert.That(background.enabled, Is.True);
        Assert.That(manager.IsGunMenuViewActive, Is.False);
        screen.gameObject.SetActive(true);
        entry.OpenWeaponEquipment(); yield return WaitFor(() => screen.CanInteract);
        Set(slot, "selected", null);
        yield return WaitFor(() => !screen.IsOpen);
        Assert.That(background.enabled, Is.True);
        var stats = Own(ScriptableObject.CreateInstance<CharacterStats>()); stats.characterId = "GunMenuOther";
        Set(slot, "selected", stats);
        entry.OpenWeaponEquipment(); yield return null;
        manager.enabled = false; yield return null;
        Assert.That(screen.IsOpen, Is.False); Assert.That(background.enabled, Is.True);
        Assert.That(selection.enabled, Is.True);
    }
    public IEnumerator PerCharacterMenuCameraAndLiveBlendTiming()
    {
        SetupRuntime(); yield return null; yield return null;
        var menu = Get<BasementMenuDefinitionSO>(screen, "menuDefinition");
        menu.blendDuration = .6f; menu.uiFadeDuration = .2f;
        var profile = Own(ScriptableObject.CreateInstance<BasementAnimationProfileSO>());
        var preset = Own(Object.Instantiate(menu.defaultCamera));
        preset.position += new Vector3(.5f, .25f, -.4f);
        var lens = preset.lens; lens.FieldOfView = 37; preset.lens = lens;
        profile.menus = new[] { new BasementAnimationProfileSO.MenuEntry { menu = menu, camera = preset } };
        slot.Selected.basementAnimationProfile = profile;
        slot.transform.SetPositionAndRotation(new Vector3(3, 0, 2), Quaternion.Euler(0, 90, 0));
        var camera = Get<CinemachineCamera>(manager, "gunMenuCamera");
        var originalLens = camera.Lens;
        var originalBlends = brain.CustomBlends;
        screen.Open(slot);
        Assert.That(camera.Lens.FieldOfView, Is.EqualTo(37));
        Assert.That(Vector3.Distance(camera.transform.position,
            slot.PresentationRoot.position + slot.PresentationRoot.rotation * preset.position), Is.LessThan(.0001f));
        Assert.That(brain.CustomBlends.GetBlendForVirtualCameras(manager.CharacterSelectCamera.name, camera.name,
            brain.DefaultBlend).Time, Is.EqualTo(.6f));
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(screen.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0).Within(.01f), "Reveal waits until the final portion of the actual blend.");
        yield return WaitFor(() => screen.CanInteract);
        screen.Close(); yield return WaitFor(() => !screen.IsOpen);
        Assert.AreSame(originalBlends, brain.CustomBlends);
        Assert.AreEqual(originalLens.FieldOfView, camera.Lens.FieldOfView);
        slot.Selected.basementAnimationProfile = null;
        screen.Open(slot); yield return WaitFor(() => screen.CanInteract);
        Assert.AreEqual(menu.defaultCamera.lens.FieldOfView, camera.Lens.FieldOfView);
        screen.Close(); yield return WaitFor(() => !screen.IsOpen);
    }
    public IEnumerator WeaponsEquipPersistAndRejectOtherOwner()
    {
        const int saveSlot = 907132;
        string folder = SaveSystem.CurrentDirectory;
        System.IO.Directory.CreateDirectory(folder);
        var saved = System.IO.Directory.GetFiles(folder, $"slot_{saveSlot}_*.json")
            .ToDictionary(p => p, System.IO.File.ReadAllBytes);
        var originalManager = SaveManager.Instance;
        try
        {
            foreach (var path in saved.Keys) System.IO.File.Delete(path);
            var saveObject = Own(new GameObject("Gun menu test saves")); saveObject.SetActive(false);
            var saves = saveObject.AddComponent<SaveManager>(); saves.currentSlot = saveSlot;
            typeof(SaveManager).GetProperty("Instance").SetValue(null, saves);
            SetupRuntime();
            var ownerPortrait = Own(Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f)));
            slot.Selected.icon = ownerPortrait;
            var inventoryObject = Own(new GameObject("Gun menu test inventory")); inventoryObject.SetActive(false);
            var inventory = inventoryObject.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetMethod("Awake", Hidden).Invoke(inventory, null);
            Get<List<IGameSaveAble>>(saves, "_participants").Add(inventory);
            var database = AssetDatabase.FindAssets("t:WeaponDatabase").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponDatabase>).First();
            Set(inventory, "weaponDatabase", database);
            var gun = AssetDatabase.FindAssets("t:GunConfig").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GunConfig>)
                .First(g => database.GetById(WeaponInstanceFactory.ResolveBaseWeaponId(g)) == g);
            for (int i = 0; i < 3; i++) inventory.slots[i].SetWeaponInstance(gun,
                new WeaponInstanceData {instanceId="gun-menu-test-" + i,baseWeaponId=WeaponInstanceFactory.ResolveBaseWeaponId(gun)});
            inventory.slots[5].CopyFrom(inventory.slots[2]);
            inventory.slots[2].Clear(); // Filtered grid indices differ from inventory indices.
            var characters = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/Scripts/System/SaveSystem/CharacterDatabase.asset");
            var otherCharacter = characters.Characters.First(c => c != null && c.icon != null);
            Assert.That(EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Weapon, CharacterEquipment.BuildCharacterOwnerId(otherCharacter.characterId), "gun-menu-test-2"), Is.True);
            yield return null; yield return null;
            screen.Open(slot, inventory);
            var view = screen.GetComponent<BasementGunMenuWeapons>();
            int changes = 0; screen.EquipmentChanged += () => changes++;
            Assert.That(view.Equip("gun-menu-test-0"), Is.False, "Gate input during camera blend");
            yield return WaitFor(() => screen.CanInteract);
            Assert.That(view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Length, Is.EqualTo(3));
            Assert.That(view.Equip("gun-menu-test-0"), Is.True);
            Assert.That(view.Equip("gun-menu-test-0"), Is.False, "Do not save/equip the current item twice");
            Assert.That(view.Equip("gun-menu-test-1"), Is.True, "Same definition, distinct instance");
            Assert.That(view.Equip("gun-menu-test-2"), Is.False, "Do not take another character's gun");
            Assert.That(view.Equip("missing-instance"), Is.False);
            Assert.That(changes, Is.EqualTo(2));
            string owner = CharacterEquipment.BuildCharacterOwnerId(slot.Selected.characterId);
            Assert.That(CharacterEquipment.TryFindEquipmentEntry(SaveSystem.LoadEquipment(saveSlot), owner, out string id), Is.True);
            Assert.That(id, Is.EqualTo("gun-menu-test-1"));
            var card = view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Single(c => Get<string>(c,"instanceId") == "gun-menu-test-0");
            var otherCard = view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Single(c => Get<string>(c,"instanceId") == "gun-menu-test-2");
            Assert.That(Get<Image>(otherCard,"equippedCharacterIcon").sprite, Is.EqualTo(otherCharacter.icon), "Database owner portrait, including owners outside the party");
            Assert.That(Get<Image>(otherCard,"equippedCharacterIcon").gameObject.activeSelf, Is.True);
            Assert.That(Get<Image>(card,"equippedCharacterIcon").gameObject.activeSelf, Is.False, "Free weapon has no owner badge");
            var hover = card.GetComponent<BasementGunMenuWeaponHover>();
            var pointer = new UnityEngine.EventSystems.PointerEventData(null) {position=new Vector2(500,300),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,pointerDrag=card.gameObject};
            hover.OnPointerEnter(pointer);
            Assert.That(card.GetComponentInChildren<BasementGunMenuDashedOutline>(), Is.Not.Null, "Hover outline appears immediately");
            Assert.That(card.GetComponentInChildren<BasementGunMenuDashedOutline>().GetComponent<CanvasRenderer>(), Is.Not.Null, "Hover mesh has a renderer");
            Assert.That(Get<bool>(view,"detailsVisible"), Is.False, "Tooltip is delayed");
            hover.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Get<bool>(view,"detailsVisible"), Is.False, "Leaving cancels pending tooltip");
            hover.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Get<bool>(view,"detailsVisible"), Is.True);
            Assert.That(Get<Component>(view,"detailsOwner"), Is.EqualTo(hover), "Inspect hovered gun, not equipped gun");
            card.OnBeginDrag(pointer);
            Assert.That(Get<bool>(view,"detailsVisible"), Is.False, "Dragging dismisses tooltip");
            Assert.That(Get<RectTransform>(view,"dragImage"), Is.Not.Null);
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.35f).Within(.001f));
            var ghost = Get<RectTransform>(view,"dragImage");
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ghost.parent, pointer.position, null, out var pointerLocal);
            Assert.That(Vector2.Distance(ghost.anchoredPosition, pointerLocal + new Vector2(20,-20)), Is.LessThan(.01f));
            card.OnEndDrag(pointer);
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(changes, Is.EqualTo(2), "Drop outside keeps equipment");
            var drop = view.GetComponentInChildren<BasementGunMenuWeaponDropSlot>();
            pointer.pointerDrag = otherCard.gameObject;
            otherCard.OnBeginDrag(pointer);
            Assert.That(Get<RectTransform>(view,"dragImage"), Is.Not.Null, "Owned weapons can be dragged");
            drop.OnDrop(pointer);
            Assert.That(changes, Is.EqualTo(2), "Dropping another owner's weapon on equip slot remains rejected");
            Assert.That(EquipmentAssignmentService.FindAssignedOwnerId(inventory, EquipmentItemKind.Weapon, "gun-menu-test-2"),
                Is.EqualTo(CharacterEquipment.BuildCharacterOwnerId(otherCharacter.characterId)));
            otherCard.OnBeginDrag(pointer);
            UnityEngine.EventSystems.ExecuteEvents.Execute(card.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.dropHandler);
            Assert.That(inventory.slots[0].weaponInstance.instanceId, Is.EqualTo("gun-menu-test-2"));
            Assert.That(inventory.slots[5].weaponInstance.instanceId, Is.EqualTo("gun-menu-test-0"));
            Assert.That(inventory.slots[2].IsEmpty, Is.True, "Do not use the filtered card index as the inventory index");
            Assert.That(SaveSystem.LoadInventory(saveSlot).slots[0].weaponInstance.instanceId, Is.EqualTo("gun-menu-test-2"));
            Assert.That(Get<Image>(card,"equippedCharacterIcon").sprite, Is.EqualTo(otherCharacter.icon), "Portrait follows the reordered instance");
            Assert.That(Get<Image>(otherCard,"equippedCharacterIcon").gameObject.activeSelf, Is.False);
            Assert.That(changes, Is.EqualTo(2), "Reordering does not change equipment");
            Assert.That(Get<RectTransform>(view,"dragImage"), Is.Null);
            Assert.That(otherCard.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            // Restore order, then exercise dragging the currently equipped, disabled-button card.
            otherCard.OnBeginDrag(pointer);
            card.OnDrop(pointer);
            var currentCard = view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Single(c => Get<string>(c,"instanceId") == "gun-menu-test-1");
            Assert.That(currentCard.GetComponent<Button>().interactable, Is.False);
            pointer.pointerDrag = currentCard.gameObject;
            currentCard.OnBeginDrag(pointer);
            currentCard.OnDrop(pointer);
            Assert.That(inventory.slots[1].weaponInstance.instanceId, Is.EqualTo("gun-menu-test-1"), "Self drop is a no-op");
            currentCard.OnBeginDrag(pointer);
            card.OnDrop(pointer);
            Assert.That(inventory.slots[0].weaponInstance.instanceId, Is.EqualTo("gun-menu-test-1"));
            Assert.That(Get<Image>(card,"equippedCharacterIcon").sprite, Is.EqualTo(ownerPortrait));
            Assert.That(CharacterEquipment.TryFindEquipmentEntry(SaveSystem.LoadEquipment(saveSlot), owner, out id), Is.True);
            Assert.That(id, Is.EqualTo("gun-menu-test-1"), "Reorder keeps equipped instance");
            currentCard.OnBeginDrag(pointer);
            card.OnDrop(pointer);
            Assert.That(changes, Is.EqualTo(2));
            pointer.pointerDrag = card.gameObject;
            card.OnBeginDrag(pointer);

            drop.OnDrop(pointer);
            card.OnEndDrag(pointer);
            Assert.That(changes, Is.EqualTo(3), "Dropping on slot equips exactly once");
            Assert.That(Get<RectTransform>(view,"dragImage"), Is.Null);
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "Successful drop restores source opacity");
            Assert.That(Get<Image>(card,"equippedCharacterIcon").sprite, Is.EqualTo(ownerPortrait));
            Assert.That(Get<Image>(card,"equippedCharacterIcon").gameObject.activeSelf, Is.True);
            Assert.That(CharacterEquipment.TryFindEquipmentEntry(SaveSystem.LoadEquipment(saveSlot), owner, out id), Is.True);
            Assert.That(id, Is.EqualTo("gun-menu-test-0"));
            view.ToggleDetails();
            Assert.That(Get<bool>(view,"detailsVisible"), Is.False, "Reserved Information action does nothing");
            hover.OnPointerEnter(pointer); yield return new WaitForSecondsRealtime(.65f);
            screen.Back(); Assert.That(screen.IsOpen, Is.True, "Back first dismisses details");
            screen.Back(); yield return WaitFor(() => !screen.IsOpen);
            screen.Open(slot, inventory); yield return WaitFor(() => screen.CanInteract);
            Assert.That(EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Weapon, owner).weaponInstance.instanceId, Is.EqualTo(id));
            Assert.That(view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Length, Is.EqualTo(3), "No duplicate cards on reopen");
            var freeCard = view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Single(c => Get<string>(c,"instanceId") == "gun-menu-test-1");
            freeCard.OnBeginDrag(pointer);
            screen.Close(); yield return WaitFor(() => !screen.IsOpen);
            Assert.That(freeCard.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "Close cancels drag visuals");
            Assert.That(Get<RectTransform>(view,"dragImage"), Is.Null);
            foreach (var data in inventory.slots) data.Clear();
            screen.Open(slot, inventory); yield return WaitFor(() => screen.CanInteract);
            Assert.That(view.GetComponentsInChildren<BasementGunMenuWeaponCard>().Length, Is.Zero);
            Assert.That(Get<TMPro.TMP_Text>(view,"message").text, Is.EqualTo("No weapons in inventory"));
            screen.Close(); yield return WaitFor(() => !screen.IsOpen);
        }
        finally
        {
            if (screen != null) screen.gameObject.SetActive(false);
            typeof(SaveManager).GetProperty("Instance").SetValue(null, originalManager);
            foreach (var path in System.IO.Directory.GetFiles(folder, $"slot_{saveSlot}_*.json")) System.IO.File.Delete(path);
            foreach (var pair in saved) System.IO.File.WriteAllBytes(pair.Key, pair.Value);
        }
    }}
#endif

