using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FailController : MonoBehaviour
{
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    void Start()
    {
        // 增加严格的空值保护
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryCurrentStage);
        }
        else
        {
            Debug.LogError("【FailController】retryButton 未在 Inspector 中赋值！");
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(BackToMainMenu);
        }
        else
        {
            Debug.LogError("【FailController】mainMenuButton 未在 Inspector 中赋值！");
        }
    }

    void RetryCurrentStage()
    {
        if (GameManager.Instance != null)
        {
            SceneManager.LoadScene("BattleScene");
        }
        else
        {
            Debug.LogWarning("【FailController】GameManager丢失，返回主菜单");
            SceneManager.LoadScene("MainMenu");
        }
    }

    void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}