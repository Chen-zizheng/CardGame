using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("点击开始游戏后跳转的场景名")]
    // 【关键修改】强制跳转到选人场景！
    public string gameSceneName = "CharacterSelectScene";

    // 开始游戏按钮的功能
    public void StartGame()
    {
        Debug.Log("开始游戏！即将进入场景：" + gameSceneName);
        SceneManager.LoadScene(gameSceneName); // 切换到选人场景
    }

    // 设置按钮的功能
    public void OpenSettings()
    {
        Debug.Log("打开设置面板");
    }

    // ��出游戏按钮的功能
    public void QuitGame()
    {
        Debug.Log("退出游戏！");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}