using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectController : MonoBehaviour
{
    [Header("把刚才做好的角色数据拖到这里")]
    // 注意：这两个字段名被场景引用着，改名会断连线，所以即使叫 warrior/ranger 也不要动。
    public CharacterData warriorData; // 清道夫数据
    public CharacterData rangerData;  // 解剖师数据

    [Header("选完角色后去哪个场景？")]
    public string nextSceneName = "MapScene"; // 默认去大地图

    private void Start()
    {
        // 版式写在 UiLayout.cs 里（坐标从代码来，场景里那块灰板只当按钮用）
        CharacterSelectLayout.Apply(warriorData, rangerData);
    }

    /// <summary>
    /// 给“清道夫”按钮绑定的方法（方法名被场景的 onClick 引用，不要改）
    /// </summary>
    public void OnSelectWarrior()
    {
        Debug.Log("选择了：" + (warriorData != null ? warriorData.characterName : "清道夫"));
        StartGameWithCharacter(warriorData);
    }

    /// <summary>
    /// 给“解剖师”按钮绑定的方法（方法名被场景的 onClick 引用，不要改）
    /// </summary>
    public void OnSelectRanger()
    {
        Debug.Log("选择了：" + (rangerData != null ? rangerData.characterName : "解剖师"));
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