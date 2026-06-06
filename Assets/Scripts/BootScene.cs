using UnityEngine;
using UnityEngine.SceneManagement;

public class BootScene : MonoBehaviour
{
    [SerializeField] bool RunGame = false;
        
    private void Start()
    {
        if (RunGame)
        {
          SceneManager.LoadScene("Basement");
        }
    }
    
}