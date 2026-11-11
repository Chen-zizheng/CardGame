using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("点击开始游戏后跳转的场景名")]
    // 【关键修改】强制跳转到选人场景！
    public string gameSceneName = "CharacterSelectScene";

    // 版本水印：只用来确认"新代码有没有真的跑起来"（从 Editor.log 里搜 BuildTag 就能判断）
    public const string BuildTag = "UI-2026-11-01-b";

    private void Start()
    {
        Debug.Log("[BuildTag] " + BuildTag + " 主菜单版式已接管（若界面没变，说明跑的是旧程序集）");

        // 场景里 MenuPanel 被存成了 3.37 倍 + 41°，这里掰回正并重排
        MainMenuLayout.Apply();
    }

    // 开始游戏按钮的功能
    public void StartGame()
    {
        Debug.Log("开始游戏！即将进入场景：" + gameSceneName);
        // 开新局就等于放弃上一局：留着旧存档会让主菜单的"继续"指向一个已经不存在的角色
        SaveSystem.Delete();
        SceneManager.LoadScene(gameSceneName); // 切换到选人场景
    }

    /// <summary>
    /// 继续上一局。存档只在战斗外写，所以读进来的一定是"某层地图上、还没进这一格"的状态。
    /// </summary>
    public void ContinueGame()
    {
        if (!SaveSystem.Exists())
        {
            Debug.Log("[存档] 没有可继续的存档，回到开始游戏");
            StartGame();
            return;
        }

        if (!SaveSystem.Restore())
        {
            Debug.LogError("[存档] 读档失败，回到开始游戏");
            StartGame();
            return;
        }

        SceneManager.LoadScene("MapScene");
    }

    // 设置按钮的功能：复用暂停菜单里的那一块（音量存在 PlayerPrefs，全局生效）
    public void OpenSettings()
    {
        PauseMenu.OpenSettings();
    }

    // 退出游戏按钮的功能
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