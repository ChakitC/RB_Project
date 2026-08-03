using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class InteractionIndicatorPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InteractionIndicatorView indicatorPrefab;
    [SerializeField] private Camera gameplayCamera;

    [Header("Input")]
    [SerializeField] private string interactActionName = "Interract";
    [SerializeField] private string fallbackBindingLabel = "F";

    [Header("World Scale")]
    [SerializeField, Min(0.01f)] private float referenceDistance = 3f;
    [SerializeField, Min(0.01f)] private float minimumScaleMultiplier = 0.5f;
    [SerializeField, Min(0.01f)] private float maximumScaleMultiplier = 2f;

    PlayerContext player;
    PlayerInput playerInput;
    Interactor interactor;
    InteractionIndicatorView indicator;
    IInteractable focusedInteractable;
    InteractionIndicatorAnchor anchorOverride;
    Vector3 baseScale;
    string lastBindingLabel;
    bool subscribed;

    public void Bind(PlayerContext context)
    {
        Unsubscribe();

        player = context;
        player?.ResolveReferences();
        interactor = player != null ? player.Interactor : null;
        playerInput = player != null ? player.GetComponentInChildren<PlayerInput>(true) : null;

        EnsureIndicator();
        Subscribe();
        OnFocusChanged(interactor != null ? interactor.FocusedInteractable : null);
    }

    void LateUpdate()
    {
        if (indicator == null || focusedInteractable == null)
            return;

        Component focusedComponent = focusedInteractable as Component;
        if (focusedComponent == null)
        {
            OnFocusChanged(null);
            return;
        }

        ResolveCamera();
        UpdateIndicatorTransform(focusedComponent);
        UpdateBindingLabel();
    }

    void OnEnable()
    {
        Subscribe();
        if (interactor != null)
            OnFocusChanged(interactor.FocusedInteractable);
    }

    void OnDisable()
    {
        Unsubscribe();
        indicator?.HideImmediate();
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (indicator != null)
            Destroy(indicator.gameObject);
    }

    void EnsureIndicator()
    {
        if (indicator != null || indicatorPrefab == null)
            return;

        indicator = Instantiate(indicatorPrefab, transform);
        indicator.name = indicatorPrefab.name;
        baseScale = indicator.transform.localScale;
        indicator.HideImmediate();
    }

    void Subscribe()
    {
        if (subscribed || interactor == null || !isActiveAndEnabled)
            return;

        interactor.FocusChanged += OnFocusChanged;
        interactor.HoldProgressChanged += OnHoldProgressChanged;
        interactor.HoldCompleted += OnHoldCompleted;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || interactor == null)
            return;

        interactor.FocusChanged -= OnFocusChanged;
        interactor.HoldProgressChanged -= OnHoldProgressChanged;
        interactor.HoldCompleted -= OnHoldCompleted;
        subscribed = false;
    }

    void OnFocusChanged(IInteractable target)
    {
        focusedInteractable = target;
        anchorOverride = ResolveAnchor(target);

        EnsureIndicator();
        if (indicator == null)
            return;

        if (focusedInteractable == null)
        {
            indicator.Hide();
            return;
        }

        lastBindingLabel = ResolveBindingLabel();
        indicator.SetProgress(interactor != null ? interactor.HoldProgress : 0f);
        indicator.Show(lastBindingLabel, interactor != null ? interactor.currentPrompt : string.Empty);
    }

    void OnHoldProgressChanged(float progress)
    {
        indicator?.SetProgress(progress);
    }

    void OnHoldCompleted()
    {
        indicator?.PlayCompletion();
    }

    void UpdateIndicatorTransform(Component focusedComponent)
    {
        Vector3 worldPosition = ResolveWorldPosition(focusedComponent);
        indicator.transform.position = worldPosition;

        if (gameplayCamera == null)
            return;

        indicator.transform.rotation = gameplayCamera.transform.rotation;

        float scaleMultiplier = 1f;
        if (!gameplayCamera.orthographic)
        {
            float distance = Vector3.Distance(gameplayCamera.transform.position, worldPosition);
            scaleMultiplier = distance / Mathf.Max(0.01f, referenceDistance);
        }

        scaleMultiplier = Mathf.Clamp(scaleMultiplier, minimumScaleMultiplier, maximumScaleMultiplier);
        indicator.transform.localScale = baseScale * scaleMultiplier;
    }

    Vector3 ResolveWorldPosition(Component focusedComponent)
    {
        if (anchorOverride != null && anchorOverride.Anchor != null)
            return anchorOverride.Anchor.position;

        Collider focusedCollider = interactor != null ? interactor.FocusedCollider : null;
        if (focusedCollider != null)
            return focusedCollider.bounds.center;

        Collider fallbackCollider = focusedComponent.GetComponentInChildren<Collider>();
        return fallbackCollider != null ? fallbackCollider.bounds.center : focusedComponent.transform.position;
    }

    InteractionIndicatorAnchor ResolveAnchor(IInteractable target)
    {
        Component targetComponent = target as Component;
        if (targetComponent == null)
            return null;

        Collider focusedCollider = interactor != null ? interactor.FocusedCollider : null;
        InteractionIndicatorAnchor anchor = focusedCollider != null
            ? focusedCollider.GetComponentInParent<InteractionIndicatorAnchor>()
            : null;
        if (anchor != null)
            return anchor;

        anchor = targetComponent.GetComponentInParent<InteractionIndicatorAnchor>();
        return anchor != null ? anchor : targetComponent.GetComponentInChildren<InteractionIndicatorAnchor>(true);
    }

    void ResolveCamera()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
    }

    void UpdateBindingLabel()
    {
        string currentLabel = ResolveBindingLabel();
        if (currentLabel == lastBindingLabel)
            return;

        lastBindingLabel = currentLabel;
        indicator.SetBindingLabel(lastBindingLabel);
    }

    string ResolveBindingLabel()
    {
        if (playerInput == null && player != null)
            playerInput = player.GetComponentInChildren<PlayerInput>(true);

        InputAction action = playerInput != null && playerInput.actions != null
            ? playerInput.actions.FindAction(interactActionName, false)
            : null;
        if (action == null)
            return fallbackBindingLabel;

        if (!string.IsNullOrWhiteSpace(playerInput.currentControlScheme))
        {
            string schemeLabel = action.GetBindingDisplayString(
                InputBinding.MaskByGroup(playerInput.currentControlScheme));
            if (!string.IsNullOrWhiteSpace(schemeLabel))
                return schemeLabel;
        }

        string label = action.GetBindingDisplayString();
        return string.IsNullOrWhiteSpace(label) ? fallbackBindingLabel : label;
    }
}
