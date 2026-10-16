using UnityEngine;

public sealed class BasementGunMenuScreenController : MonoBehaviour, IBasementMenuView
{
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private BasementGunMenuWeapons weapons;
    [SerializeField] private CharacterSelectManager characterSelection;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private GameObject[] backgroundRoots;
    [SerializeField] private RectTransform[] letterboxBars;
    // Retained for old prefab serialization. Timing now belongs to menuDefinition.
    [SerializeField, HideInInspector] private float fadeInDuration = 0.5f;
    [SerializeField, HideInInspector] private float otherCharactersFadeDuration = 0.45f;
    [SerializeField] private BasementMenuDefinitionSO menuDefinition;
    BasementMenuPresentationController presentation;
    bool closing;

    public bool IsOpen { get; private set; }
    public bool CanInteract => IsOpen && !closing && presentation != null && presentation.Owns(this) && presentation.CanInteract;
    public event System.Action EquipmentChanged;
    public CanvasGroup PresentationGroup => screenGroup;
    public CharacterSelectManager CharacterSelection => characterSelection;
    public GameObject[] BackgroundRoots => backgroundRoots;
    public RectTransform[] LetterboxBars => letterboxBars;

    void Awake()
    {
        screenGroup.alpha = 0;
        screenGroup.interactable = screenGroup.blocksRaycasts = false;
        if (weapons != null) weapons.EquipmentChanged += OnEquipmentChanged;
    }
    void OnEquipmentChanged() => EquipmentChanged?.Invoke();
    void OnDestroy()
    {
        if (weapons != null) weapons.EquipmentChanged -= OnEquipmentChanged;
    }

    public void Open(PartySlot slot, PlayerInventory inventory = null)
    {
        if (!isActiveAndEnabled || IsOpen || screenGroup == null || cameraManager == null) return;
        presentation = BasementMenuPresentationController.Resolve(cameraManager);
        if (presentation == null || !presentation.TryOpen(menuDefinition, slot, this)) return;
        IsOpen = true;
        closing = false;
        if (weapons != null) weapons.Bind(slot, inventory);
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    public void Back()
    {
        if (!CanInteract) return;
        if (weapons != null && weapons.HideDetails()) return;
        Close();
    }

    public void Close()
    {
        if (IsOpen && presentation != null && presentation.Owns(this)) presentation.Close();
    }

    public void PresentationClosing()
    {
        closing = true;
        if (weapons != null) weapons.Unbind();
    }

    public void PresentationClosed() => IsOpen = closing = false;

    void OnDisable()
    {
        if (presentation != null && presentation.Owns(this)) presentation.Close(true);
    }
}
