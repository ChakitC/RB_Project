using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderSystem : MonoBehaviour
{
    [Serializable]
    private sealed class SceneAudioEntry
    {
        public string sceneName;
        public AudioCue musicCue;
        public AudioCue ambienceCue;
        public bool stopPreviousMusic = true;
        public bool stopPreviousAmbience = true;
    }

    public static SceneLoaderSystem Instance { get; private set; }
    public PlayerInventory playerInventory;
    public ItemDatabase itemDatabase;
    [SerializeField] private string _mapSelect;
    [SerializeField] private MapRunConfigSO selectedMapRunConfig;
    [SerializeField] private List<SceneAudioEntry> sceneAudio = new();
    
    
    
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

        ApplySceneAudio(scene.name);
    }

    public void LoadStage(MapRunConfigSO config)
    {
        if (config == null)
        {
            Debug.LogWarning("[SceneLoaderSystem] Cannot load a stage without a MapRunConfigSO.", this);
            return;
        }

        selectedMapRunConfig = config;
        _mapSelect = "MapRun";
        LoadGame();
    }

    public MapRunConfigSO ConsumeSelectedMapRunConfig()
    {
        MapRunConfigSO selected = selectedMapRunConfig;
        selectedMapRunConfig = null;
        return selected;
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
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        else
            Debug.LogWarning("[SceneLoaderSystem] SaveManager is missing; returning to Basement without saving.", this);

        SceneManager.LoadScene("Basement");
    }

    void ApplySceneAudio(string sceneName)
    {
        if (sceneAudio == null || sceneAudio.Count == 0)
            return;

        for (int i = 0; i < sceneAudio.Count; i++)
        {
            var entry = sceneAudio[i];
            if (entry == null || !string.Equals(entry.sceneName, sceneName, StringComparison.Ordinal))
                continue;

            if (entry.stopPreviousMusic)
                AudioService.Instance.StopCategory(AudioCategory.Music);

            if (entry.stopPreviousAmbience)
                AudioService.Instance.StopCategory(AudioCategory.Ambience);

            if (entry.musicCue != null)
                AudioService.Instance.Play(entry.musicCue);

            if (entry.ambienceCue != null)
                AudioService.Instance.Play(entry.ambienceCue);

            return;
        }
    }
}
