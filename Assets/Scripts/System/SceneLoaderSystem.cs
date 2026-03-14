using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderSystem : MonoBehaviour
{
    public static SceneLoaderSystem Instance { get; private set; }
    public PlayerInventory playerInventory;
    public ItemDatabase itemDatabase;
    [SerializeField] private string _mapSelect;
    
    
    
    private void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        // กัน error ตอนปิดเกม
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SetMapToLoad(string sceneName)
    {
        _mapSelect = sceneName;
        Debug.Log($"เลือกด่าน: {_mapSelect}");
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Loaded scene: " + scene.name);
        playerInventory = GameObject.FindObjectOfType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.Log("No player inventory found");
        }        
    }

    public void LoadGame()
    {
        
        if (string.IsNullOrEmpty(_mapSelect))
        {
            Debug.LogWarning("_mapSelect="+_mapSelect);
            Debug.LogWarning("เลือกด่านก่อน");
            return;
        }
        SceneManager.LoadScene(_mapSelect);
    }

    public void LoadBasement()
    {
        SaveManager.Instance.Save();
        SceneManager.LoadScene("Basement");
        
    }
}
