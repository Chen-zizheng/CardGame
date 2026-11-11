using CardCombat;
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

    [Header("【本局战绩（每开一局清零）】")]
    public int runWins;        // 这局打赢了几场
    public int runMaxStage;    // 走到的最深处（第几层）
    public bool runWon;        // 这局是通关结束还是倒下结束
    private float runStartSeconds;

    [Header("关卡基础配置")]
    public int totalStageCount = 10;   // 进地图时会被 MapManager 的 totalLayers 覆盖，这里只是兜底
    public int currentStageIndex;
    public int currentEnemyLevel;
    public bool[] stageCleared;

    [Header("【本局遗物】")]
    // 遗物跨战斗保留，是本局内"你这局捡到了什么"的记录。存档会把它一起带走。
    public List<string> relics = new List<string>();

    [Header("【本局天赋】")]
    // 天赋每 3 层二选一（3 / 6 / 9 层之后），本局内一直有效，存档一起带走。
    public List<string> talents = new List<string>();

    [Header("【本局统计（结算屏用，每开一局清零）】")]
    // 这些数只在战斗结束时累加，谁都不会因为它们改变数值，所以掉线/回滚都不影响平衡。
    public int statCardsPlayed;
    public int statDamageDealt;
    public int statDamageBlocked;
    public int statInterrupts;
    public int statKills;

    [Header("【本局已经遇过的事件】")]
    // 事件格从 EventLibrary 里抽，抽过的记在这里，避免同一局重复读到同一段话。
    public List<string> eventsSeen = new List<string>();

    [Header("【当前牌库与战利品池】")]
    public List<CardData> playerDeck = new List<CardData>();
    // 保持原来 allAvailableCards 这个名字，这样你的 RewardController 就不会报错
    public List<CardData> allAvailableCards = new List<CardData>();

    [Header("【各关卡的怪物配置】")]
    public List<EnemyData> stageEnemies = new List<EnemyData>();

    /// <summary>
    /// 当前角色是不是解剖师。
    /// 以前靠 characterName.Contains("游侠") 判断职业，改一次名就会把卡组判错，所以改成看被动枚举。
    /// </summary>
    public bool IsDissector
    {
        get
        {
            return currentCharacter != null && currentCharacter.passiveSkill == PassiveSkillType.Dissector_Tempo;
        }
    }

    /// <summary>当前角色的职业名，只用于日志和文案。</summary>
    public string ClassName
    {
        get { return IsDissector ? "解剖师" : "清道夫"; }
    }

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

    /// <summary>已经拿到这件遗物了吗（id 见 RelicLibrary）。</summary>
    public bool HasRelic(string id)
    {
        if (string.IsNullOrEmpty(id) || relics == null) return false;
        for (int i = 0; i < relics.Count; i++) if (relics[i] == id) return true;
        return false;
    }

    /// <summary>拿一件遗物。重复的不给，返回 false 让调用方换一件。</summary>
    public bool AddRelic(string id)
    {
        if (string.IsNullOrEmpty(id) || relics == null || HasRelic(id)) return false;
        relics.Add(id);
        return true;
    }

    /// <summary>已经拿到这条天赋了吗（id 见 TalentLibrary）。</summary>
    public bool HasTalent(string id)
    {
        if (string.IsNullOrEmpty(id) || talents == null) return false;
        for (int i = 0; i < talents.Count; i++) if (talents[i] == id) return true;
        return false;
    }

    /// <summary>拿一条天赋。重复的不给。</summary>
    public bool AddTalent(string id)
    {
        if (string.IsNullOrEmpty(id) || talents == null || HasTalent(id)) return false;
        talents.Add(id);
        return true;
    }

    /// <summary>还有几条本能没长出来（开局自带 1 条，之后 3/6/9 层各一条）。</summary>
    public int PendingTalents()
    {
        int earned = 1 + TalentLibrary.SlotsForStage(currentStageIndex);
        int taken = talents != null ? talents.Count : 0;
        return Mathf.Max(0, earned - taken);
    }

    /// <summary>这一局角色对应的本能池（0 清道夫 / 1 解剖师，与 PassiveSkillType 同值）。</summary>
    public int InstinctOwner()
    {
        if (currentCharacter == null) return TalentLibrary.Sweeper;
        return currentCharacter.passiveSkill == PassiveSkillType.Dissector_Tempo
            ? TalentLibrary.Dissector : TalentLibrary.Sweeper;
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

        if (relics == null) relics = new List<string>();
        relics.Clear();

        if (eventsSeen == null) eventsSeen = new List<string>();
        eventsSeen.Clear();

        if (talents == null) talents = new List<string>();
        talents.Clear();
        // 每个角色出生就带一条本能（清道夫铁壁 / 解剖师轻装），这是她的身份而不是随机奖励
        TalentDef starter = TalentLibrary.StarterFor(InstinctOwner());
        if (starter != null) talents.Add(starter.Id);

        // 战绩清零：结算屏（ResultScreen）读的就是这两个数
        runWins = 0;
        runMaxStage = 1;
        runWon = false;
        ResetRunStats();
        runStartSeconds = Time.realtimeSinceStartup;

        // 地图有几层就打几层。以前写死 3，铺好的 7 层地图只走三场就被送回主菜单。
        SyncStageCountFromMap();

        // 清空旧牌库
        Debug.Log($"【GameManager】清空前 playerDeck 数量: {playerDeck.Count}");
        playerDeck.Clear();
        Debug.Log($"【GameManager】清空后 playerDeck 数量: {playerDeck.Count}");

        // 添加初始牌库：以 CardLibrary.StarterDeck 为唯一真源，
        // 角色 asset 里的 initialDeck 已过时（还是老回合制的数值），不再使用。
        bool dissector = IsDissector;
        playerDeck.AddRange(DeckFactory.Starter(dissector));
        Debug.Log($"【GameManager】起始牌库 {playerDeck.Count} 张（{(dissector ? "解剖师" : "清道夫")}）");

        Debug.Log($"【GameManager】最终 playerDeck 数量: {playerDeck.Count}");

        // 3. 掉落/商店卡池：通用 7 张 + 本职业 3 张（同样以 CardLibrary 为真源）
        allAvailableCards.Clear();
        allAvailableCards.AddRange(DeckFactory.AllCards(dissector));

        // 4. 重置关卡进度
        for (int i = 0; i < stageCleared.Length; i++)
        {
            stageCleared[i] = false;
        }
        if (stageCleared.Length > 0) stageCleared[0] = true;
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

    /// <summary>
    /// 让"关卡总数"跟地图的实际层数对齐。
    /// 选人时 MapManager 可能还没被创建（它挂在主菜单的 DontDestroyOnLoad 上，
    /// 也可能只在地图场景里），所以地图场景一加载会再调一次。
    /// </summary>
    public void SyncStageCountFromMap()
    {
        if (MapManager.Instance == null || MapManager.Instance.mapConfig == null) return;

        int layers = MapManager.Instance.mapConfig.totalLayers;
        if (layers <= 0) return;

        totalStageCount = layers;
        if (stageCleared == null || stageCleared.Length != totalStageCount)
        {
            bool[] resized = new bool[totalStageCount];
            if (stageCleared != null)
            {
                int keep = Mathf.Min(stageCleared.Length, resized.Length);
                for (int i = 0; i < keep; i++) resized[i] = stageCleared[i];
            }
            stageCleared = resized;
        }
    }

    /// <summary>清零本局统计（开局与读档时调）。</summary>
    public void ResetRunStats()
    {
        statCardsPlayed = 0;
        statDamageDealt = 0;
        statDamageBlocked = 0;
        statInterrupts = 0;
        statKills = 0;
    }

    /// <summary>
    /// 一场战斗结束后把 sim 的计数并进本局统计。
    /// 输的那场也要算 —— 失败屏同样要显示"你打出了多少"。
    /// </summary>
    public void AddBattleStats(CombatSim s)
    {
        if (s == null) return;
        statCardsPlayed += s.StatCardsPlayed;
        statDamageDealt += s.StatDamageDealt;
        statDamageBlocked += s.StatDamageBlocked;
        statInterrupts += s.StatInterrupts;
        if (s.Outcome == CombatOutcome.Win) statKills++;
    }

    /// <summary>一场战斗打赢：记战绩，并更新走到的最深处。</summary>
    public void NoteBattleWon(int stageIndex)
    {
        runWins++;
        if (stageIndex > runMaxStage) runMaxStage = stageIndex;
    }

    /// <summary>本局从选完角色到现在用了多少秒（结算屏显示"用时"）。</summary>
    public int RunElapsedSeconds()
    {
        return Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - runStartSeconds));
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