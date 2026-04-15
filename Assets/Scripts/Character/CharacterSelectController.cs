using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectController : MonoBehaviour
{
    [Header("把刚才做好的角色数据拖到这里")]
    public CharacterData warriorData; // 战士数据
    public CharacterData rangerData;  // 游侠数据

    [Header("选完角色后去哪个场景？")]
    public string nextSceneName = "MapScene"; // 默认去大地图

    /// <summary>
    /// 给“选择战士”按钮绑定的方法
    /// </summary>
    public void OnSelectWarrior()
    {
        Debug.Log("选择了：战士");
        StartGameWithCharacter(warriorData);
    }

    /// <summary>
    /// 给“选择游侠”按钮绑定的方法
    /// </summary>
    public void OnSelectRanger()
    {
        Debug.Log("选择了：游侠");
        StartGameWithCharacter(rangerData);
    }

    /// <summary>
    /// 核心通用逻辑：把数据塞给全局管家，然后切场景
    /// </summary>
    private void StartGameWithCharacter(CharacterData selectedData)
    {
        if (GameManager.Instance != null)
        {
            // 1. 让 GameManager 吞下这个角色的所有专属数据！
            GameManager.Instance.StartNewRun(selectedData);

            // 2. 切场景！正式开始肉鸽之旅！
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("【致命错误】找不到 GameManager！请务必从 MainMenu 场景启动游戏！");
        }
    }
}