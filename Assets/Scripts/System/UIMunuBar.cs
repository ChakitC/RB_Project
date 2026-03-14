using UnityEngine;
using UnityEngine.TextCore.Text;

public class UIMunuBar : MonoBehaviour
{
    public GameObject menuBar;
    public GameObject UiCharacterDead;
    public KeyCode key = KeyCode.Escape;
    public CharacteContext ctx;
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
    }


    void Start()
    {
        if (menuBar != null)
            menuBar.SetActive(_MenubarOpen);
    }
    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            Debug.Log("Open Menu bar");
            _MenubarOpen = !_MenubarOpen;          
            menuBar.SetActive(_MenubarOpen);        
        }
    }

    public void OnBacktoBase()
    {
        SceneLoaderSystem.Instance.LoadBasement();
        menuBar.SetActive(false);
    }
}
