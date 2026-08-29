using UnityEngine;
using UnityEngine.TextCore.Text;

public class UIMunuBar : MonoBehaviour
{
    public GameObject menuBar;
    public GameObject UiCharacterDead;
    public KeyCode key = KeyCode.Escape;
    public CharacteContext ctx;
    [Header("Audio")]
    [SerializeField] private AudioCue menuToggleCue;
    [SerializeField] private AudioCue backToBaseCue;
    [SerializeField] private AudioCue deathScreenCue;
    private bool _MenubarOpen = false;
    
    public void SetContext(CharacteContext newCtx)
    {
        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.ReturnbaseUI -= OnCharacterDead;

        ctx = newCtx;

        if (ctx != null && ctx.HealthSystem != null)
            ctx.HealthSystem.ReturnbaseUI += OnCharacterDead;
    }
    
    
    private void OnCharacterDead()
    {
        if (UiCharacterDead != null)
        {
            UiCharacterDead.SetActive(true);
        }

        if (deathScreenCue != null)
            AudioService.Instance.Play(deathScreenCue);
    }


    void Start()
    {
        if (menuBar != null)
        {
            menuBar.SetActive(_MenubarOpen);
            ThirdPersonCameraSettingsPanel.EnsureExists(menuBar);
        }
    }
    void Update()
    {
        if (NpcPresentationController.IsActive)
            return;

        if (Input.GetKeyDown(key))
        {
            Debug.Log("Open Menu bar");
            _MenubarOpen = !_MenubarOpen;          
            if (menuBar != null)
                menuBar.SetActive(_MenubarOpen);

            if (menuToggleCue != null)
                AudioService.Instance.Play(menuToggleCue);
        }
    }

    public void OnBacktoBase()
    {
        if (backToBaseCue != null)
            AudioService.Instance.Play(backToBaseCue);

        SceneLoaderSystem.Instance.LoadBasement();
        menuBar.SetActive(false);
    }
}
