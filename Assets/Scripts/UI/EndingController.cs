using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingController : MonoBehaviour
{
    public void BackToMainMenu()
    {
        Debug.Log("从 EndingScene 返回 MainMenu");
        SceneManager.LoadScene("MainMenu");
    }
}