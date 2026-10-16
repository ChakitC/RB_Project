using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Owns the read-only Supply room visit and restores the Basement view on exit.</summary>
public sealed class BasementSupplyRoomController : MonoBehaviour
{
    public enum VisitPhase { Closed, Opening, Open, Closing }
    [SerializeField] private BasementContext basement;
    [SerializeField] private GameObject room;
    [SerializeField] private BasementSupplyScreenController screen;
    [SerializeField] private Camera supplyCamera;
    [SerializeField] private Camera[] basementCameras;
    [SerializeField] private Canvas[] basementCanvases;
    [SerializeField] private CanvasGroup fade;
    [SerializeField, Min(0)] private float fadeToBlackDuration = .25f;
    [SerializeField, Min(0)] private float fadeFromBlackDuration = .30f;
    private readonly List<(Behaviour component, bool enabled)> visualSnapshot = new();
    private readonly List<(Behaviour component, bool enabled)> inputSnapshot = new();
    private EventSystem eventSystem;
    private GameObject previousSelection;
    private bool navigationWasEnabled;
    public VisitPhase Phase { get; private set; }
    public bool IsOpen => Phase != VisitPhase.Closed;
    public bool IsTransitioning => Phase == VisitPhase.Opening || Phase == VisitPhase.Closing;

    private void OnEnable()
    {
        if (screen != null) screen.CloseRequested += Close;
    }

    public void Open()
    {
        if (!isActiveAndEnabled || IsOpen) return;
        if (basement == null || room == null || screen == null || supplyCamera == null || fade == null ||
            basementCameras == null || basementCameras.Length == 0 || System.Array.Exists(basementCameras, c => c == null))
        {
            Debug.LogError("[Basement Supply] Room, screen, cameras, fade and Basement bindings are required.", this);
            return;
        }
        var presentation = basement.camManager != null ? basement.camManager.GetComponent<BasementMenuPresentationController>() : null;
        if ((basement.camManager != null && basement.camManager._inMapUI) ||
            (presentation != null && presentation.IsOpen) || NpcPresentationController.IsActive) return;
        Phase = VisitPhase.Opening;
        eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            previousSelection = eventSystem.currentSelectedGameObject;
            navigationWasEnabled = eventSystem.sendNavigationEvents;
            eventSystem.sendNavigationEvents = false;
            eventSystem.SetSelectedGameObject(null);
        }
        if (basement.characterSelectManager != null)
        {
            basement.characterSelectManager.CancelDrag();
            Disable(basement.characterSelectManager, inputSnapshot);
        }
        if (basementCanvases != null)
            foreach (var canvas in basementCanvases)
                if (canvas != null)
                    foreach (var raycaster in canvas.GetComponents<GraphicRaycaster>()) Disable(raycaster, inputSnapshot);
        StartCoroutine(Enter());
    }

    public void Close()
    {
        if (Phase != VisitPhase.Open) return;
        Phase = VisitPhase.Closing;
        StartCoroutine(Leave());
    }

    private IEnumerator Enter()
    {
        yield return Fade(1, fadeToBlackDuration);
        if (room == null || screen == null || supplyCamera == null) { ResetVisit(); yield break; }
        foreach (var camera in basementCameras) Disable(camera, visualSnapshot);
        if (basementCanvases != null)
            foreach (var canvas in basementCanvases) Disable(canvas, visualSnapshot);
        room.SetActive(true);
        supplyCamera.enabled = true;
        screen.Open();
        yield return null;
        yield return Fade(0, fadeFromBlackDuration);
        Phase = VisitPhase.Open;
        fade.blocksRaycasts = false;
    }

    private IEnumerator Leave()
    {
        yield return Fade(1, fadeToBlackDuration);
        HideRoom();
        Restore(visualSnapshot);
        yield return null;
        yield return Fade(0, fadeFromBlackDuration);
        Restore(inputSnapshot);
        RestoreNavigation();
        Phase = VisitPhase.Closed;
        fade.blocksRaycasts = false;
    }

    private IEnumerator Fade(float target, float duration)
    {
        fade.blocksRaycasts = true;
        float start = fade.alpha;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        fade.alpha = target;
    }

    private static void Disable(Behaviour component, List<(Behaviour component, bool enabled)> snapshot)
    {
        if (component == null || snapshot.Exists(s => s.component == component)) return;
        snapshot.Add((component, component.enabled));
        component.enabled = false;
    }

    private static void Restore(List<(Behaviour component, bool enabled)> snapshot)
    {
        foreach (var state in snapshot) if (state.component != null) state.component.enabled = state.enabled;
        snapshot.Clear();
    }

    private void HideRoom()
    {
        if (screen != null) screen.Close();
        if (supplyCamera != null) supplyCamera.enabled = false;
        if (room != null) room.SetActive(false);
    }

    private void ResetVisit()
    {
        StopAllCoroutines();
        HideRoom();
        Restore(visualSnapshot);
        Restore(inputSnapshot);
        RestoreNavigation();
        if (fade != null) { fade.alpha = 0; fade.blocksRaycasts = false; }
        Phase = VisitPhase.Closed;
    }

    private void RestoreNavigation()
    {
        if (eventSystem != null)
        {
            eventSystem.sendNavigationEvents = navigationWasEnabled;
            eventSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        eventSystem = null;
        previousSelection = null;
    }

    private void OnDisable()
    {
        if (screen != null) screen.CloseRequested -= Close;
        ResetVisit();
    }
}
