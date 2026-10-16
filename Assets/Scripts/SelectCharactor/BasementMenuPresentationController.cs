using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CameraManager))]
public sealed class BasementMenuPresentationController : MonoBehaviour
{
    readonly List<(Behaviour component, bool enabled)> hiddenUI = new();
    readonly List<BasementAccessoryRendererFade> hiddenCharacters = new();
    CameraManager cameraManager;
    BasementMenuDefinitionSO menu;
    IBasementMenuView view;
    PartySlot slot;
    CharacterStats character;
    BasementCharacterPresentation presentation;
    bool selectionWasEnabled;
    bool closing;
    bool ready;
    float hiddenAmount;
    BasementCharacterMenuScreenController parentView;
    BasementMenuDefinitionSO parentMenu;
    readonly List<(Behaviour component, bool enabled)> childHiddenUI = new();
    bool modal;

    public bool IsOpen => view != null;
    public bool IsTransitioning => IsOpen && (!ready || closing);
    public bool CanInteract => IsOpen && ready && !closing && !modal;
    public bool Owns(IBasementMenuView candidate) => ReferenceEquals(view, candidate);
    public bool Contains(IBasementMenuView candidate) => Owns(candidate) || ReferenceEquals(parentView, candidate);

    public void SetModal(IBasementMenuView owner, bool value)
    {
        if (!Owns(owner)) return;
        modal = value;
        SetGroup(value || !ready ? 0 : 1, !value && ready, !value);
    }

    public static BasementMenuPresentationController Resolve(CameraManager manager)
    {
        if (manager == null) return null;
        var result = manager.GetComponent<BasementMenuPresentationController>();
        return result != null ? result : manager.gameObject.AddComponent<BasementMenuPresentationController>();
    }

    public bool TryOpen(BasementMenuDefinitionSO definition, PartySlot target, IBasementMenuView screen)
    {
        if (!isActiveAndEnabled || definition == null || target == null || target.Selected == null ||
            !target.gameObject.activeInHierarchy || screen == null || screen.PresentationGroup == null) return false;
        if (IsOpen) return TryOpenChild(definition, target, screen);
        cameraManager = GetComponent<CameraManager>();
        var entry = target.Selected.basementAnimationProfile?.FindMenu(definition);
        var preset = entry?.camera != null ? entry.camera : definition.defaultCamera;
        if (!cameraManager.BeginMenuView(definition, target.PresentationRoot, preset)) return false;

        menu = definition;
        view = screen;
        slot = target;
        character = target.Selected;
        closing = ready = false;
        hiddenAmount = 0;
        presentation = target.GetComponent<PartySlotVisualPreview>()?.Presentation;
        presentation?.EnterMenu(entry);
        if (view.CharacterSelection != null)
        {
            selectionWasEnabled = view.CharacterSelection.enabled;
            view.CharacterSelection.CancelDrag();
            view.CharacterSelection.enabled = false;
        }
        slot.SelectedChanged += OnCharacterChanged;
        if (view.BackgroundRoots != null)
            foreach (var root in view.BackgroundRoots)
            {
                if (root == null) continue;
                foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (component.transform.IsChildOf(view.PresentationGroup.transform)) continue;
                    if (component is Canvas && view.PresentationGroup.transform.IsChildOf(component.transform)) continue;
                    if (component is Canvas || component is GraphicRaycaster || component is Graphic) Hide(component);
                }
            }
        if (menu.hideOtherCharacters)
            foreach (var member in FindObjectsByType<PartySlot>(FindObjectsSortMode.None))
                if (member != slot)
                    foreach (var renderer in member.GetComponentsInChildren<Renderer>(true))
                        hiddenCharacters.Add(new BasementAccessoryRendererFade(renderer));
        SetGroup(0, false, true);
        UpdateLetterbox();
        return true;
    }

    bool TryOpenChild(BasementMenuDefinitionSO definition, PartySlot target, IBasementMenuView screen)
    {
        if (!CanInteract || parentView != null || !(view is BasementCharacterMenuScreenController status) ||
            target != slot || target.Selected != character || ReferenceEquals(screen, view)) return false;
        var entry = character.basementAnimationProfile?.FindMenu(definition);
        if (!cameraManager.SwitchMenuView(definition, entry?.camera != null ? entry.camera : definition.defaultCamera)) return false;
        SetGroup(0, false, false);
        parentView = status;
        parentMenu = menu;
        view = screen;
        menu = definition;
        ready = false;
        presentation?.SwitchMenu(entry);
        if (screen.BackgroundRoots != null)
            foreach (var root in screen.BackgroundRoots)
            {
                if (root == null || screen.PresentationGroup.transform.IsChildOf(root.transform)) continue;
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                    if ((behaviour is Canvas || behaviour is GraphicRaycaster) &&
                        !childHiddenUI.Exists(s => s.component == behaviour))
                    { childHiddenUI.Add((behaviour, behaviour.enabled)); behaviour.enabled = false; }
            }
        SetGroup(0, false, true);
        return true;
    }

    void RestoreChildUI()
    {
        foreach (var state in childHiddenUI) if (state.component != null) state.component.enabled = state.enabled;
        childHiddenUI.Clear();
    }

    void Hide(Behaviour component)
    {
        foreach (var state in hiddenUI) if (state.component == component) return;
        hiddenUI.Add((component, component.enabled));
        component.enabled = false;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (cameraManager == null || !cameraManager.isActiveAndEnabled || view.PresentationGroup == null || menu == null)
        { Close(true); return; }
        if (!closing && (slot == null || !slot.gameObject.activeInHierarchy || slot.Selected != character))
        { Close(true); return; }
        if (!closing && !cameraManager.IsMenuViewActive(menu))
        { Close(true); return; }

        float duration = menu.otherCharactersFadeDuration;
        hiddenAmount = Mathf.MoveTowards(hiddenAmount, closing ? 0 : 1,
            duration > 0 ? Time.unscaledDeltaTime / duration : 1);
        foreach (var renderer in hiddenCharacters) renderer.Apply(Mathf.SmoothStep(0, 1, hiddenAmount));
        if (!closing)
        {
            float progress = cameraManager.GetMenuRevealProgress(menu.uiFadeDuration);
            ready = progress >= 1 && !cameraManager.IsMenuTransitioning;
            SetGroup(modal ? 0 : Mathf.SmoothStep(0, 1, progress), ready && !modal, !modal);
        }
        UpdateLetterbox();
        if (closing && !cameraManager.IsMenuTransitioning && hiddenAmount <= 0) Restore();
    }

    void OnCharacterChanged(CharacterStats value)
    {
        if (value != character) Close(true);
    }

    public void Close(bool immediate = false)
    {
        if (!IsOpen) return;
        if (!immediate && parentView != null)
        {
            var entry = character.basementAnimationProfile?.FindMenu(parentMenu);
            if (!cameraManager.SwitchMenuView(parentMenu, entry?.camera != null ? entry.camera : parentMenu.defaultCamera))
            { Close(true); return; }
            var child = view;
            child.PresentationClosing();
            SetGroup(0, false, false);
            RestoreChildUI();
            view = parentView;
            menu = parentMenu;
            parentView = null;
            parentMenu = null;
            ready = false;
            presentation?.SwitchMenu(entry);
            child.PresentationClosed();
            ((BasementCharacterMenuScreenController)view).Resume();
            return;
        }
        if (!closing)
        {
            closing = true;
            ready = false;
            view.PresentationClosing();
            SetGroup(0, false, false);
            presentation?.ExitMenu();
            cameraManager?.EndMenuView(immediate);
        }
        if (immediate)
        {
            cameraManager?.EndMenuView(true);
            Restore();
        }
    }

    void Restore()
    {
        if (slot != null) slot.SelectedChanged -= OnCharacterChanged;
        RestoreChildUI();
        foreach (var state in hiddenUI) if (state.component != null) state.component.enabled = state.enabled;
        hiddenUI.Clear();
        foreach (var renderer in hiddenCharacters) renderer.Dispose();
        hiddenCharacters.Clear();
        var selection = parentView != null ? parentView.CharacterSelection : view.CharacterSelection;
        if (selection != null) selection.enabled = selectionWasEnabled;
        SetGroup(0, false, false);
        var previous = view;
        var parent = parentView;
        parentView = null;
        parentMenu = null;
        view = null;
        slot = null;
        character = null;
        presentation = null;
        menu = null;
        hiddenAmount = 0;
        ready = closing = modal = false;
        previous.PresentationClosed();
        if (parent != null) parent.PresentationClosed();
    }

    void SetGroup(float alpha, bool interactable, bool raycasts)
    {
        var group = view.PresentationGroup;
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = raycasts;
    }

    void UpdateLetterbox()
    {
        var bars = view.LetterboxBars;
        if (bars == null || bars.Length != 4) return;
        Rect r = cameraManager.MenuViewport;
        SetBar(bars[0], Vector2.zero, new Vector2(r.xMin, 1));
        SetBar(bars[1], new Vector2(r.xMax, 0), Vector2.one);
        SetBar(bars[2], new Vector2(r.xMin, 0), new Vector2(r.xMax, r.yMin));
        SetBar(bars[3], new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, 1));
    }

    static void SetBar(RectTransform bar, Vector2 min, Vector2 max)
    {
        if (bar == null) return;
        bar.anchorMin = min;
        bar.anchorMax = max;
        bar.offsetMin = bar.offsetMax = Vector2.zero;
    }

    void OnDisable() => Close(true);
}
