using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingController : MonoBehaviour
{
    public void BackToMainMenu()
    {
        Debug.Log("´Ó EndingScene ·µ»Ø MainMenu");
        SceneManager.LoadScene("MainMenu");
    }
}