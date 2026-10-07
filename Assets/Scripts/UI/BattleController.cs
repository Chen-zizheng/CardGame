using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class BattleController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageInfoText;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            int stageIndex = GameManager.Instance.currentStageIndex;
            int enemyLevel = GameManager.Instance.currentEnemyLevel;

            if (stageInfoText != null)
            {
                stageInfoText.text = $"Stage {stageIndex}  -  Enemy Lv.{enemyLevel}";
            }
        }
        else
        {
            Debug.LogWarning("GameManager is missing in BattleScene. 如果你是直接单独运行 BattleScene，这条警告是正常的；正式游戏请从 MainMenu → MapScene 进入。");
        }
    }

    public void GoToEnding()
    {
        SceneManager.LoadScene("EndingScene");
    }
}