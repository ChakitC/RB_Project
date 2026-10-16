using UnityEngine;

// Presentation only. UILoadLaval continues to own the selected slot and its stats.
public sealed class BasementCharacterMenuScreenController : MonoBehaviour, IBasementMenuView
{
    [SerializeField] private UILoadLaval owner;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private CharacterSelectManager characterSelection;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private RectTransform artboard;
    [SerializeField] private BasementMenuDefinitionSO menuDefinition;
    [SerializeField] private GameObject[] backgroundRoots;
    [SerializeField] private RectTransform[] letterboxBars;
    BasementMenuPresentationController presentation;
    int resumedFrame = -1;
    bool skillModal;

    public bool IsOpen { get; private set; }
    public bool CanInteract => IsOpen && !skillModal && presentation != null &&
        presentation.Owns(this) && presentation.CanInteract && Time.frameCount > resumedFrame;
    public CanvasGroup PresentationGroup => screenGroup;
    public CharacterSelectManager CharacterSelection => characterSelection;
    public GameObject[] BackgroundRoots => backgroundRoots;
    public RectTransform[] LetterboxBars => letterboxBars;

    void LateUpdate()
    {
        if (artboard == null || !(artboard.parent is RectTransform frame)) return;
        float scale = Mathf.Min(frame.rect.width / 1920f, frame.rect.height / 1080f);
        artboard.localScale = Vector3.one * Mathf.Max(0, scale);
    }

    void OnEnable()
    {
        if (screenGroup == null) return;
        screenGroup.alpha = 0;
        screenGroup.interactable = screenGroup.blocksRaycasts = false;
    }

    void Update()
    {
        if (!IsOpen)
        {
            if (owner == null || owner._slot == null || owner._slot.Selected == null || cameraManager == null) return;
            presentation = BasementMenuPresentationController.Resolve(cameraManager);
            IsOpen = presentation != null && presentation.TryOpen(menuDefinition, owner._slot, this);
            return;
        }
        if (!Input.GetKeyDown(KeyCode.Escape) || Time.frameCount <= resumedFrame) return;
        if (skillModal) owner.CloseActiveSkillTree();
        else if (CanInteract) Back();
    }

    public void Back()
    {
        if (CanInteract) presentation.Close();
    }

    public void SuspendForSkills()
    {
        skillModal = true;
        presentation?.SetModal(this, true);
    }

    public void Resume()
    {
        skillModal = false;
        resumedFrame = Time.frameCount;
        presentation?.SetModal(this, false);
        if (owner != null) owner.RefreshStats();
    }

    public void PresentationClosing()
    {
        if (skillModal && owner != null) owner.CloseActiveSkillTree();
    }

    public void PresentationClosed()
    {
        IsOpen = false;
        skillModal = false;
        if (owner != null) owner.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (presentation != null && presentation.Contains(this)) presentation.Close(true);
        IsOpen = false;
    }
}
