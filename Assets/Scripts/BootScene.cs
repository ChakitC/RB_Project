using UnityEngine;
using UnityEngine.SceneManagement;

public class BootScene : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("Basement");
    }
}