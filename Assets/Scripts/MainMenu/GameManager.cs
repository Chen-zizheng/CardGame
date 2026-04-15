using UnityEngine;
using System.Collections.Generic;

public enum EnemyAIType { Basic, Defensive, Boss }

[System.Serializable]
public class EnemyData
{
    public string enemyName;
    public Sprite enemySprite;
    public int maxHp;
    public int baseAttack;
    public EnemyAIType aiType;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("【当前选择的角色数据】")]
    public CharacterData currentCharacter; // 玩家选好的角色数据插槽

    [Header("【玩家状态（跨局继承）】")]
    public int playerMaxHp;
    public int currentPlayerHp;
    [Header("【玩家资源】")]
    public int currentGold = 100; // 初始给100块钱启动资金

    [Header("关卡基础配置")]
    public int totalStageCount = 3;
    public int currentStageIndex;
    public int currentEnemyLevel;
    public bool[] stageCleared;

    [Header("【当前牌库与战利品池】")]
    public List<CardData> playerDeck = new List<CardData>();
    // 保持原来 allAvailableCards 这个名字，这样你的 RewardController 就不会报错
    public List<CardData> allAvailableCards = new List<CardData>();

    [Header("【各关���怪物配置】")]
    public List<EnemyData> stageEnemies = new List<EnemyData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        stageCleared = new bool[totalStageCount];
    }

    /// <summary>
    /// 【核心新增】在选人界面点击角色后调用此方法，初始化这局游戏的数据！
    /// </summary>
    public void StartNewRun(CharacterData selectedCharacter)
    {
        Debug.Log($"【GameManager.StartNewRun】开始初始化新游戏，选择角色: {selectedCharacter.characterName}");

        currentCharacter = selectedCharacter;
        playerMaxHp = currentCharacter.maxHp;
        currentPlayerHp = playerMaxHp;
        currentGold = 100;

        // 清空旧牌库
        Debug.Log($"【GameManager】清空前 playerDeck 数量: {playerDeck.Count}");
        playerDeck.Clear();
        Debug.Log($"【GameManager】清空后 playerDeck 数量: {playerDeck.Count}");

        // 添加初始牌库
        Debug.Log($"【GameManager】角色初始牌库数量: {currentCharacter.initialDeck.Count}");
        foreach (var card in currentCharacter.initialDeck)
        {
            playerDeck.Add(card);
            Debug.Log($"  + 添加卡牌: {card.cardName}");
        }

        Debug.Log($"【GameManager】最终 playerDeck 数量: {playerDeck.Count}");

        // 3. 读取角色专属掉落卡池
        allAvailableCards.Clear();
        foreach (var card in currentCharacter.exclusiveCardPool)
        {
            allAvailableCards.Add(card);
        }

        // 4. 重置关卡进度
        for (int i = 0; i < stageCleared.Length; i++)
        {
            stageCleared[i] = false;
        }
        stageCleared[0] = true;
        currentStageIndex = 1;
        currentEnemyLevel = 1;
        SaveStageData();

        Debug.Log($"已选择角色：{currentCharacter.characterName}，游戏初始化完毕！");

        // 5. 重置并重新生成大地图（解决换角色后地图全灰、无法点击的问题）
        if (MapManager.Instance != null)
        {
            MapManager.Instance.GenerateNewMap();
        }
    }

    public void AddCardToDeck(CardData newCard)
    {
        playerDeck.Add(newCard);
    }

    public void SaveStageData()
    {
        for (int i = 0; i < stageCleared.Length; i++)
        {
            PlayerPrefs.SetInt($"Stage_{i + 1}_Cleared", stageCleared[i] ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private void Start()
    {
        // 这是一个“兜底测试逻辑”：如果你没有做选人界面直接运行游戏，并且挂载了测试角色，就自动初始化
        if (currentCharacter != null && playerDeck.Count == 0)
        {
            StartNewRun(currentCharacter);
        }
    }
}