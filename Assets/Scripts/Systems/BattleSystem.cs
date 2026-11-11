using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using CardCombat;

public enum CardType { Attack, Heal, Shield, Draw }
public enum EnemyIntentType { Attack, Defend, Buff }

[System.Serializable]
public class CardData
{
    // 卡牌在 CardLibrary 里的 id（basic/heavy/guard...）。存档只认这个，
    // 名字是给人看的，改文案不该让存档里的牌变成别的牌。
    public string id;
    public string cardName;
    public int damage;
    [TextArea] public string description;
    public CardType cardType;
    // 纯逻辑层的五类（攻击/格挡/卸力/回复/抽牌）。UI 靠它上色，
    // 所以商店、奖励、牌库这些"预制体卡"也能和手牌同一个色，不用各自判 cardType。
    public CardKind kind;
    public Color rareColor;

    [Header("消耗与回能")]
    public int cost;
    public int hpCost = 0;       // 卖血
    public int energyGain = 0;   // 爆费

    [Header("进阶机制 (打勾开启)")]
    public bool isRetain;        // 保留：回合结束不进弃牌堆
    public bool isDesperation;   // 绝境：玩家血量低于30%时，伤害翻3倍！
    public int healAmount = 0;   // 吸血：无论什么卡，只要填了数字打出就能回血

    public Sprite cardBackgroundSprite;
}

/// <summary>
/// 战斗宿主（薄适配器）。
/// 规则和数值全在 Assets\Scripts\Combat\ 里：纯 C#、零 UnityEngine 引用，
/// 可以在命令行里跑几十万局（D:\deepharnes\.tmp\combat\harness.exe）。
/// 这里只做四件事：把时间换成格子、把手牌画出来、把点击翻译成命令、把事件播成反馈。
/// </summary>
public partial class BattleSystem : MonoBehaviour
{
    public static BattleSystem Instance;

    [Header("基础UI引用")]
    [SerializeField] private TextMeshProUGUI stageInfoText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI energyText;
    private TextMeshProUGUI gaugeText;
    private RectTransform relicRow;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI cardDescriptionText;
    [SerializeField] private TextMeshProUGUI enemyIntentText;

    // 职业标签（"清道夫 · 格挡回血"）。运行时自己建，不依赖场景里有没有这个物体。
    private TextMeshProUGUI classLabel;

    [Header("战斗对象引用")]
    [SerializeField] private Image playerImage;
    [SerializeField] private Image enemyImage;

    [Header("UI预制体与节点")]
    [SerializeField] private CardView[] cardViews;
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Transform popupCanvasParent;

    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;

    [Header("战斗配置")]
    [SerializeField] private int drawCardPerTurn = 5;
    [SerializeField] private int maxEnergy = 3;

    [Header("节奏")]
    [Tooltip("一格等于多少秒。0.34 秒一格时，一场战斗大约 8~16 秒。")]
    [SerializeField] private float tickSeconds = 0.55f;

    private CombatSim sim;
    private CombatView view;
    private EnemyData currentEnemyData;
    private float accumulator;
    private bool finished;

    // ---- 速度档位 ----
    // 一格 1 秒是"看得清"的节奏，但熟了以后推一格一格地走会烦。
    // [ 和 ] 调速，Tab 在 1× 和 2× 之间来回。
    private static readonly float[] SpeedSteps = { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
    private int speedIndex = 2;                       // 默认 1×

    private float TickStep()
    {
        float mult = SpeedSteps[Mathf.Clamp(speedIndex, 0, SpeedSteps.Length - 1)];
        return Mathf.Max(0.08f, tickSeconds / mult);
    }

    private void ChangeSpeed(int delta)
    {
        speedIndex = Mathf.Clamp(speedIndex + delta, 0, SpeedSteps.Length - 1);
        PushSpeedLabel();
    }

    private void PushSpeedLabel()
    {
        if (view == null) return;
        float mult = SpeedSteps[Mathf.Clamp(speedIndex, 0, SpeedSteps.Length - 1)];
        view.SetSpeedLabel("速度 " + mult.ToString("0.##") + "×　[ 慢　] 快");
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("【战斗】找不到 GameManager（直接运行 BattleScene 时属正常）");
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            int mine = WiredRefCount();
            int theirs = Instance.WiredRefCount();
            if (theirs >= mine)
            {
                Debug.LogWarning("【战斗】发现重复组件，已关闭 " + name);
                enabled = false;
                return;
            }
        }
        Instance = this;

        if (tickSeconds < 0.02f) tickSeconds = 0.55f;   // 老场景里没有这个字段时是 0，防死循环
        // 一格给 1 秒。实测：反应 4 格（≈2.2 秒一次决策）在 0.55 秒/格下胜率 63%/0%/0%，
        // 人读五张牌 + 数轨道格数 + 点下去本来就要 2~4 秒，慢一点才打得动。
        if (tickSeconds < 1.0f) tickSeconds = 1.0f;

        ApplyHudLayout();
        BattleBackdrop.Ensure(transform);

        BuildSim();
        LoadPortraits();
        BuildView();
        UpdateHud();

        BattleAudio audio = BattleAudio.Ensure();
        audio.BindBattle(this);
        audio.PlayBgm("bgm_battle");

        Debug.Log("【开战】" + sim.Enemy.Name + " " + sim.EnemyMaxHp + "HP · 牌库 " + sim.TotalCards
            + " 张 · 一格 " + tickSeconds + " 秒 · 节奏：" + sim.Config.EnergyEveryTicks + " 格回 1 灵能");
    }

    private void Update()
    {
        if (sim == null || finished) return;

        HandleKeyboard();

        float step = TickStep();
        accumulator += Time.deltaTime;
        int guard = 0;
        while (accumulator >= step && !sim.IsOver && guard < 8)
        {
            accumulator -= step;
            sim.Step();
            DrainEvents();
            guard++;
        }

        view.SyncTimeline(sim);
        view.SetHand(sim, false);
        UpdateHud();

        if (sim.IsOver && !finished)
        {
            finished = true;
            StartCoroutine(FinishRoutine());
        }
    }

    // ---------------- 初始化 ----------------

    private int WiredRefCount()
    {
        int n = 0;
        if (stageInfoText != null) n++;
        if (enemyHpText != null) n++;
        if (playerHpText != null) n++;
        if (energyText != null) n++;
        if (endTurnButton != null) n++;
        if (cardDescriptionText != null) n++;
        if (enemyIntentText != null) n++;
        if (playerImage != null) n++;
        if (enemyImage != null) n++;
        if (damagePopupPrefab != null) n++;
        if (popupCanvasParent != null) n++;
        if (deckTransform != null) n++;
        if (discardTransform != null) n++;
        return n;
    }

    /// <summary>
    /// 找出这一场敌人的贴图。
    /// 不能按 currentEnemyLevel 取：地图上随便点哪个战斗格都会把它设成 1，
    /// 于是第五关的领主会顶着史莱姆的立绘上场（上一版就是这么错的）。
    /// 现在按战斗核心给出的敌人名字匹配，匹配不到才退回关卡序号。
    /// </summary>
    private void LoadPortraits()
    {
        GameManager gm = GameManager.Instance;
        List<EnemyData> enemies = gm.stageEnemies;
        currentEnemyData = null;
        if (enemies == null || enemies.Count == 0) return;

        string wanted = sim != null ? sim.Enemy.Name : "";
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData data = enemies[i];
            if (data == null || data.enemySprite == null) continue;
            if (!string.IsNullOrEmpty(wanted) && data.enemyName != null &&
                (wanted.Contains(data.enemyName) || data.enemyName.Contains(wanted)))
            {
                currentEnemyData = data;
                return;
            }
        }

        int index = Mathf.Clamp(gm.currentEnemyLevel - 1, 0, enemies.Count - 1);
        currentEnemyData = enemies[index];
    }

    private void BuildSim()
    {
        GameManager gm = GameManager.Instance;

        List<string> deck = new List<string>();
        if (gm.playerDeck != null)
        {
            for (int i = 0; i < gm.playerDeck.Count; i++)
            {
                CardData data = gm.playerDeck[i];
                if (data == null) continue;
                // 有 id 就信 id（强化牌是 "haul+"，中文名映射会把它退回基础牌、强化就丢了）。
                string id = string.IsNullOrEmpty(data.id) ? CardLibrary.MapChinese(data.cardName) : data.id;
                if (CardLibrary.Get(id) == null) id = CardLibrary.MapChinese(data.cardName);
                deck.Add(id);
            }
        }

        bool dissector = gm.IsDissector;

        if (deck.Count < 8)
        {
            Debug.LogWarning("【战斗】playerDeck 只有 " + deck.Count + " 张，改用角色起始牌组");
            deck.Clear();
            deck.AddRange(CardLibrary.StarterDeck(dissector));
        }

        // 打断牌是这套战斗唯一的防守型胜负手，老存档的牌组里没有它，至少补到 3 张。
        int stagger = 0;
        for (int i = 0; i < deck.Count; i++) if (CardLibrary.BaseId(deck[i]) == CardLibrary.Stagger) stagger++;
        for (int i = stagger; i < 3; i++) deck.Add(CardLibrary.Stagger);

        // 敌人按「层数 + 节点档位」生成：普通格 / 精英格 / Boss 格的血量与伤害不同。
        // 以前 currentEnemyLevel 只拿去搅种子，于是精英格和普通格打起来一模一样。
        int stageForEnemy = Mathf.Max(0, gm.currentStageIndex - 1);
        int enemyTier = Mathf.Clamp(gm.currentEnemyLevel <= 0 ? 1 : gm.currentEnemyLevel, 1, 3);
        // 亚种用「地图种子 + 层号」定：同一张图同一个节点永远是同一只，
        // 重开一局地图换了种子，同一个怪就换成另一种亚种。
        int variantSalt = gm.currentStageIndex * 31;
        if (MapManager.Instance != null) variantSalt ^= MapManager.Instance.mapSeed;
        EnemyTemplate template = EnemyLibrary.ForStage(stageForEnemy, enemyTier, variantSalt);
        int seed = System.Environment.TickCount ^ (gm.currentStageIndex * 7919) ^ (enemyTier * 104729);

        CombatConfig cfg = new CombatConfig();
        cfg.PlayerMaxHp = Mathf.Max(1, gm.playerMaxHp);
        cfg.HandCap = 5;

        // 职业差异就落在两条数上：灵能上限 + 被动。
        // 清道夫只能攒 3 点，所以它的牌必须大；解剖师能攒 4 点，靠碎牌续节奏。
        // 场景里序列化的 maxEnergy 是老回合制留下的 3，不再参与计算。
        if (dissector)
        {
            cfg.EnergyCap = 4;
            cfg.Passive = PassiveId.DissectorTempo;
        }
        else
        {
            cfg.EnergyCap = 3;
            cfg.Passive = PassiveId.SweeperGuardHeal;
        }
        cfg.PassiveHealAmount = 2;
        // 解剖师的续航：每打出 2 张牌回 3 点生命（清道夫靠"挡得住"回，她靠"动得多"回）。
        // 每场平均出牌 12.7 张 → 约 19 点，抵掉一场掉血的一大半。
        // 数字是整局台（600 局 · 300/职业 · 4 格反应 · 30% 手滑 · 战后整备 30% · 篝火 30%）量出来的：
        //   每张回 1  → 解剖师 36.3%，清道夫 41.0%（差 −4.7）
        //   每张回 2  → 解剖师 46.0%，清道夫 41.0%（差 +5.0，反超过头）
        //   每 2 张回 3 → 取中间那档，目标是和清道夫打平。
        cfg.DissectorHealPerCard = 3;
        cfg.DissectorHealEveryN = 2;
        cfg.Relics = RelicLibrary.MaskOf(gm.relics);
        cfg.Talents = TalentLibrary.MaskOf(gm.talents);

        sim = new CombatSim(cfg, deck, template, new SeededRandom(seed));
        if (gm.currentPlayerHp > 0 && gm.currentPlayerHp < cfg.PlayerMaxHp)
        {
            // 继承上一场的血量（CombatSim 内部只暴露只读血量，这里用受伤补齐）
            int missing = cfg.PlayerMaxHp - gm.currentPlayerHp;
            sim.ApplyCarryOverDamage(missing);
        }
        gm.currentPlayerHp = sim.PlayerHp;
    }

    private void BuildView()
    {
        Transform canvasRoot = playerImage != null && playerImage.canvas != null
            ? playerImage.canvas.transform
            : (enemyImage != null && enemyImage.canvas != null ? enemyImage.canvas.transform : transform);

        // 战斗立绘优先取 Resources 里那一套「全身站位」，取不到才退回角色 asset 里的旧字段
        CharacterData pc = GameManager.Instance.currentCharacter;
        Sprite playerSprite = PlayerArt.Battle(pc);
        if (playerSprite == null) playerSprite = pc != null ? pc.battleSprite : null;
        // 敌人立绘优先按敌人 id 从 Resources 取（新增的 6 种都走这里），
        // 取不到才退回场景里那三个 EnemyData 槽位（老图兜底）。
        Sprite enemySprite = sim != null ? EnemyArt.Load(sim.Enemy.Id) : null;
        if (enemySprite == null && currentEnemyData != null) enemySprite = currentEnemyData.enemySprite;

        // 最后这个参数是 HUD 文字的父节点：CombatView 要在它们下面垫两块玻璃底板
        Transform hudParent = playerHpText != null ? playerHpText.transform.parent : null;
        view = CombatView.Create(canvasRoot, playerSprite, enemySprite, playerImage, enemyImage, stageInfoText, hudParent);
        view.CardClicked = TryPlayIndex;
        HideLegacyNodes();

        // CombatView 是运行时最后挂上去的，它会盖住场景里的 HUD 文字。
        // 手牌上方那行说明正好压在敌方轨道下沿，必须提到最上层才看得见。
        if (cardDescriptionText != null) cardDescriptionText.transform.SetAsLastSibling();
        view.SetHand(sim, true);
        view.SyncTimeline(sim);
        PushSpeedLabel();
    }

    /// <summary>
    /// 老战斗场景里还摆着一排卡牌模板和一堆占位节点。
    /// 新画面全部由 CombatView 现搭，这些旧东西要么挡住手牌、要么抢走点击，统一关掉。
    /// 只关不删：场景文件不改，回滚时重开就好。
    /// </summary>
    private void HideLegacyNodes()
    {
        if (cardViews != null)
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i] == null) continue;
                cardViews[i].gameObject.SetActive(false);
            }
        }

        Transform hand = transform.Find("HandContainer");
        if (hand == null) hand = GameObject.Find("HandContainer")?.transform;
        if (hand != null) hand.gameObject.SetActive(false);

        if (deckTransform != null) deckTransform.gameObject.SetActive(false);
        if (discardTransform != null) discardTransform.gameObject.SetActive(false);
    }

    // ---------------- 布局（代码搭，不依赖 Inspector） ----------------

    private void ApplyHudLayout()
    {
        Place(stageInfoText, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(900f, 40f), TextAlignmentOptions.Center, 22, new Color(0.86f, 0.88f, 0.86f, 1f));
        Place(playerHpText, new Vector2(0f, 1f), new Vector2(72f, -96f), new Vector2(420f, 44f), TextAlignmentOptions.Left, 30, InkCol);
        Place(energyText, new Vector2(0f, 1f), new Vector2(72f, -158f), new Vector2(420f, 36f), TextAlignmentOptions.Left, 22, MintCol);

        EnsureGaugeText();
        Place(gaugeText, new Vector2(0f, 1f), new Vector2(72f, -190f), new Vector2(420f, 32f),
            TextAlignmentOptions.Left, 20, new Color(0.80f, 0.95f, 0.90f, 0.92f));

        EnsureRelicRow();
        RefreshRelicRow();

        EnsureClassLabel();
        Place(classLabel, new Vector2(0f, 1f), new Vector2(72f, -58f), new Vector2(560f, 32f),
            TextAlignmentOptions.Left, 20, new Color(MintCol.r, MintCol.g, MintCol.b, 0.82f));
        Place(enemyHpText, new Vector2(1f, 1f), new Vector2(-72f, -96f), new Vector2(520f, 40f), TextAlignmentOptions.Right, 24, InkCol);
        Place(enemyIntentText, new Vector2(1f, 1f), new Vector2(-72f, -158f), new Vector2(520f, 36f), TextAlignmentOptions.Right, 20, new Color(0.86f, 0.62f, 0.66f, 1f));
        // 这张说明夹在敌方轨道和手牌之间，只有 30px。盒子做薄、字降到 18，
        // 才能既不被轨道压住、也不压到手牌。
        Place(cardDescriptionText, new Vector2(0.5f, 0f), new Vector2(0f, 252f), new Vector2(1200f, 26f), TextAlignmentOptions.Center, 18, new Color(0.62f, 0.68f, 0.68f, 1f));

        if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 左上角那行职业标签。玩家要知道"我这一局是谁、被动是什么"，
    /// 否则两个职业只差一个灵能上限，看不出区别。
    /// </summary>
    private void EnsureClassLabel()
    {
        if (classLabel != null) return;
        Transform parent = playerHpText != null ? playerHpText.transform.parent : transform;
        GameObject go = new GameObject("ClassLabel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        classLabel = go.AddComponent<TextMeshProUGUI>();
        if (playerHpText != null) classLabel.font = playerHpText.font;
        classLabel.raycastTarget = false;
        classLabel.enableAutoSizing = false;
        classLabel.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// 回收值那一行。清道夫的流派资源没有它，玩家看不见自己攒了多少弹药，
    /// "清运"就变成一张莫名其妙的大数字牌。
    /// </summary>
    private void EnsureGaugeText()
    {
        if (gaugeText != null) return;
        Transform parent = energyText != null ? energyText.transform.parent : transform;
        GameObject go = new GameObject("GaugeLabel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        gaugeText = go.AddComponent<TextMeshProUGUI>();
        if (energyText != null) gaugeText.font = energyText.font;
        gaugeText.raycastTarget = false;
        gaugeText.enableAutoSizing = false;
        gaugeText.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// 遗物图标行（左上角，回收值下面）。遗物是这一局最有存在感的成长，
    /// 只写在弹窗里没人看得到，所以常驻一排 40px 的小图标。
    /// </summary>
    private void EnsureRelicRow()
    {
        if (relicRow != null) return;
        Transform parent = gaugeText != null ? gaugeText.transform.parent : transform;
        GameObject go = new GameObject("RelicRow", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        relicRow = go.GetComponent<RectTransform>();
        relicRow.anchorMin = new Vector2(0f, 1f);
        relicRow.anchorMax = new Vector2(0f, 1f);
        relicRow.pivot = new Vector2(0f, 1f);
        relicRow.anchoredPosition = new Vector2(72f, -226f);
        relicRow.sizeDelta = new Vector2(560f, 46f);

        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
    }

    private void RefreshRelicRow()
    {
        if (relicRow == null) return;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.relics == null) return;

        // 每次都重搭：遗物最多十几件，重建比增量同步省心，也不会漏掉读档那一条路径。
        for (int i = relicRow.childCount - 1; i >= 0; i--)
        {
            Destroy(relicRow.GetChild(i).gameObject);
        }

        for (int i = 0; i < gm.relics.Count; i++)
        {
            GameObject cell = new GameObject("Relic_" + gm.relics[i], typeof(RectTransform));
            cell.transform.SetParent(relicRow, false);
            RectTransform rt = cell.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(42f, 42f);

            Image img = cell.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = true;
            Sprite s = Resources.Load<Sprite>("Art/Relics/" + gm.relics[i]);
            if (s != null) img.sprite = s;
            else img.color = new Color(0.72f, 0.90f, 0.86f, 0.55f);

            // 光有个图标，玩家捡完并不知道它干什么；悬停/点一下出名字和效果
            RelicDef rdef = RelicLibrary.Get(gm.relics[i]);
            if (rdef != null) IconTip.Attach(cell, rdef.Name, rdef.Desc);
        }

        // 本能紧跟在遗物后面，用同一排。它是本局最"重"的一次选择，
        // 不该只活在选完那一下的弹窗里。
        if (gm.talents == null) return;
        for (int i = 0; i < gm.talents.Count; i++)
        {
            GameObject cell = new GameObject("Talent_" + gm.talents[i], typeof(RectTransform));
            cell.transform.SetParent(relicRow, false);
            RectTransform rt = cell.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(42f, 42f);

            Image img = cell.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = true;
            TalentDef tdef = TalentLibrary.Get(gm.talents[i]);
            Sprite s = Resources.Load<Sprite>("Art/Talents/" + (tdef != null ? tdef.Icon : gm.talents[i]));
            if (s != null) img.sprite = s;
            else img.color = new Color(0.72f, 0.90f, 0.86f, 0.55f);

            if (tdef != null) IconTip.Attach(cell, "本能 · " + tdef.Name, tdef.Desc);
        }
    }

    private static readonly Color InkCol = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color MintCol = new Color(0.72f, 0.90f, 0.86f, 1f);

    private static void Place(TextMeshProUGUI text, Vector2 anchor, Vector2 pos, Vector2 size,
        TextAlignmentOptions align, int fontSize, Color color)
    {
        if (text == null) return;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, anchor.y);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        text.alignment = align;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.color = color;
    }

    // ---------------- 输入 ----------------

    private void HandleKeyboard()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftBracket)) ChangeSpeed(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket)) ChangeSpeed(1);
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            speedIndex = speedIndex == 2 ? 4 : 2;   // 1× <-> 2×
            PushSpeedLabel();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) TryPlayIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryPlayIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryPlayIndex(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TryPlayIndex(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) TryPlayIndex(4);
#endif
    }

    private void TryPlayIndex(int index)
    {
        if (sim == null || sim.IsOver) return;

        string reason;
        if (!sim.CanPlay(index, out reason)) { Reject(reason); return; }

        CardDef def = sim.Hand[index];
        if (!sim.TryPlay(index, out reason)) { Reject(reason); return; }

        view.SetHand(sim, false);
        view.SyncTimeline(sim);
        UpdateHud();

        if (BattleAudio.Instance != null) BattleAudio.Instance.Play("card_play");

        CardData data = new CardData
        {
            cardName = def.Name,
            damage = def.Damage,
            cost = def.Cost,
            cardType = (CardType)(int)def.Kind
        };
        RaiseCardPlayed(data);

        // 打击感放在构图层（整张图小幅转 + 挤压），不折身体：盘腿坐姿一折就露馅。
        switch (data.cardType)
        {
            case CardType.Attack: view.LeanPlayer(-1.2f, 0.26f); break;
            case CardType.Shield: view.LeanPlayer(0.8f, 0.30f); break;
            default: view.LeanPlayer(0.5f, 0.26f); break;
        }
        if (cardDescriptionText != null) cardDescriptionText.text = "打出 " + def.Name + "（占 " + def.Duration + " 格）";
    }

    private void Reject(string reason)
    {
        Debug.LogWarning("[出牌被拒] " + reason);
        if (cardDescriptionText != null) cardDescriptionText.text = "打不出：" + reason;
    }

    // ---------------- 事件 -> 反馈 ----------------

    private void DrainEvents()
    {
        List<CombatEvent> events = sim.DrainEvents();
        for (int i = 0; i < events.Count; i++)
        {
            CombatEvent e = events[i];
            switch (e.Kind)
            {
                case CombatEventKind.EnemyHurt:
                    view.FlashEnemy(new Color(1f, 0.72f, 0.72f), true);
                    view.ShowFloat(view.EnemyWorld, "-" + e.Amount, new Color(1f, 0.62f, 0.60f), 40);
                    if (BattleAudio.Instance != null) BattleAudio.Instance.Play("hit");
                    RaiseEnemyDamaged(e.Amount, sim.EnemyHp);
                    break;

                case CombatEventKind.PlayerHurt:
                    view.FlashPlayer(new Color(1f, 0.55f, 0.55f), true);
                    view.ShowFloat(view.PlayerWorld, "-" + e.Amount, new Color(1f, 0.45f, 0.45f), 36);
                    if (BattleAudio.Instance != null) BattleAudio.Instance.Play("hit_player");
                    RaisePlayerDamaged(e.Amount, sim.PlayerHp);
                    break;

                case CombatEventKind.Interrupted:
                    view.ShowFloat(view.EnemyWorld, "打断！", MintCol, 44);
                    if (BattleAudio.Instance != null) BattleAudio.Instance.Play("card_shield");
                    break;

                case CombatEventKind.Armored:
                    view.ShowFloat(view.EnemyWorld, "霸体 · 挡不住", new Color(0.86f, 0.74f, 0.45f), 30);
                    break;

                case CombatEventKind.Blocked:
                    view.ShowFloat(view.PlayerWorld, "格挡 " + e.Amount, new Color(0.55f, 0.78f, 1f), 28);
                    break;

                case CombatEventKind.Reclaimed:
                    view.ShowFloat(view.PlayerWorld, "回收 +" + e.Amount, new Color(0.62f, 0.94f, 0.82f), 26);
                    break;

                case CombatEventKind.Healed:
                {
                    // 被动回血要写清楚是"格挡换来的"，不然玩家会以为药水多回了一次。
                    bool passive = !string.IsNullOrEmpty(e.Label) && e.Label.Contains("被动");
                    view.FlashPlayer(new Color(0.7f, 1f, 0.8f));
                    view.ShowFloat(view.PlayerWorld,
                        passive ? "格挡回血 +" + e.Amount : "+" + e.Amount,
                        new Color(0.62f, 0.94f, 0.72f), 32);
                    if (BattleAudio.Instance != null) BattleAudio.Instance.Play("card_heal");
                    break;
                }

                case CombatEventKind.Enraged:
                    view.ShowFloat(view.EnemyWorld, "激怒", new Color(1f, 0.78f, 0.4f), 32);
                    break;

                case CombatEventKind.Breathe:
                    // 首领换招：时间轴上的下一招往后挪了 Amount 格，这里明说一句，
                    // 免得玩家以为是自己看错了格子。
                    view.ShowFloat(view.EnemyWorld, "换招 · 空档 " + e.Amount + " 格",
                        new Color(0.62f, 0.94f, 0.90f), 30);
                    break;

                case CombatEventKind.Drawn:
                    if (!string.IsNullOrEmpty(e.Label) && e.Label.Contains("被动"))
                        view.ShowFloat(view.PlayerWorld, "回灵能", MintCol, 28);
                    if (BattleAudio.Instance != null) BattleAudio.Instance.Play("card_draw");
                    break;
            }
        }
    }

    private void UpdateHud()
    {
        if (sim == null) return;
        if (classLabel != null)
        {
            GameManager gm = GameManager.Instance;
            string who = gm != null && !string.IsNullOrEmpty(gm.ClassName) ? gm.ClassName : "处置员";
            string passive = gm != null && gm.currentCharacter != null ? gm.currentCharacter.passiveName : "";
            classLabel.text = string.IsNullOrEmpty(passive) ? who : who + " · " + passive;
        }

        if (playerHpText != null) playerHpText.text = "HP " + sim.PlayerHp + " / " + sim.PlayerMaxHp;
        if (energyText != null) energyText.text = "灵能 " + sim.Energy + " / " + sim.EnergyCap;
        if (gaugeText != null)
        {
            bool show = sim.GaugeMax > 0 && sim.Gauge > 0;
            gaugeText.gameObject.SetActive(show);
            if (show) gaugeText.text = "回收 " + sim.Gauge + " / " + sim.GaugeMax;
        }
        if (enemyHpText != null) enemyHpText.text = sim.Enemy.Name + " " + sim.EnemyHp + " / " + sim.EnemyMaxHp;

        if (view != null) view.SetHpBars(sim.PlayerHp, sim.PlayerMaxHp, sim.EnemyHp, sim.EnemyMaxHp);
        if (stageInfoText != null)
            stageInfoText.text = "Stage " + GameManager.Instance.currentStageIndex + " · " + sim.Enemy.Name;

        if (enemyIntentText != null)
        {
            Act next = null;
            for (int i = 0; i < sim.Acts.Count; i++)
            {
                Act a = sim.Acts[i];
                if (a.Owner != ActOwner.Enemy || a.Resolved || a.Cancelled) continue;
                if (next == null || a.End < next.End) next = a;
            }
            enemyIntentText.text = next == null
                ? "意图：—"
                : "意图：" + next.Name + " " + next.Damage + (next.Armor ? "（霸体）" : "") + " · " + Mathf.Max(0, next.End - sim.Tick) + " 格后";
        }
    }

    // ---------------- 收尾 ----------------

    private IEnumerator FinishRoutine()
    {
        bool win = sim.Outcome == CombatOutcome.Win;
        RaiseBattleEnded(win);

        yield return new WaitForSecondsRealtime(0.9f);
        if (this == null) yield break;

        GameManager gm = GameManager.Instance;
        if (gm == null) yield break;

        // 统计先行：输的那场也要记（失败屏同样显示"你打出了多少"）
        gm.AddBattleStats(sim);

        if (!win)
        {
            gm.runWon = false;
            gm.runMaxStage = Mathf.Max(gm.runMaxStage, gm.currentStageIndex);
            if (cardDescriptionText != null) cardDescriptionText.text = "你被同化了 · 处置记录终止";
            yield return new WaitForSecondsRealtime(0.5f);
            SceneManager.LoadScene("FailScene");
            yield break;
        }

        // 打赢了：先把战绩记上，结局屏/奖励屏都从这里取数
        gm.NoteBattleWon(gm.currentStageIndex);

        if (cardDescriptionText != null) cardDescriptionText.text = "清场完成 · 数据已回收";

        // 战后整备：两职业一样，按已损失生命的 30% 回血。
        // 以前这条只挂在清道夫身上，整局模拟跑出来 37% / 6% —— 差的不是战斗里的手法，
        // 是"能不能回血"这件事本身（单场台子每场都满血起手，所以看不见）。
        int missingHp = sim.PlayerMaxHp - sim.PlayerHp;
        gm.currentPlayerHp = missingHp > 0
            ? Mathf.Min(sim.PlayerMaxHp, sim.PlayerHp + Mathf.RoundToInt(missingHp * 0.3f))
            : sim.PlayerHp;

        int stage = gm.currentStageIndex;
        if (gm.stageCleared != null)
        {
            if (stage - 1 >= 0 && stage - 1 < gm.stageCleared.Length) gm.stageCleared[stage - 1] = true;
            if (stage < gm.stageCleared.Length) gm.stageCleared[stage] = true;
        }
        // 结算单：没花掉的灵能换成补给点。它把"留着灵能保命"从浪费变成收益。
        if (gm.HasRelic(RelicLibrary.Invoice) && sim.Energy > 0)
        {
            int bonus = sim.Energy * RelicLibrary.InvoiceGoldPerEnergy;
            gm.currentGold += bonus;
            if (cardDescriptionText != null) cardDescriptionText.text = "清场完成 · 结算单 +" + bonus + " 补给点";
        }

        TryDropRelic(gm);

        gm.SaveStageData();

        // 什么时候算"打通"：优先看地图的首领层（地图 7 层 → 第 7 场就是首领），
        // 地图配置拿不到时退回 totalStageCount。
        bool lastStage = stage >= gm.totalStageCount;
        if (MapManager.Instance != null && MapManager.Instance.mapConfig != null)
        {
            lastStage = stage - 1 >= MapManager.Instance.mapConfig.bossLayerIndex;
        }

        if (lastStage)
        {
            // 工程里没有独立的结局场景，结局屏与失败屏共用 FailScene，
            // 由 ResultScreen 读 gm.runWon 决定显示"通关"还是"败退"。
            gm.runWon = true;
            SceneManager.LoadScene("FailScene");
        }
        else
        {
            gm.currentGold += Random.Range(15, 31);
            SaveSystem.Save();
            SceneManager.LoadScene("RewardScene");
        }
    }

    /// <summary>
    /// 遗物掉落：精英格与首领格必掉，普通格 20%。
    /// 六件全拿齐之后就什么都不掉——这局到此为止，多给重复的没有意义。
    /// </summary>
    private void TryDropRelic(GameManager gm)
    {
        int tier = Mathf.Clamp(gm.currentEnemyLevel, 1, 3);
        bool drop = tier >= 2 || Random.value < 0.20f;
        if (!drop) return;

        string id = RelicLibrary.RollNew(gm.relics, new SeededRandom(System.Environment.TickCount));
        if (id == null) return;
        if (!gm.AddRelic(id)) return;

        RelicDef def = RelicLibrary.Get(id);
        if (def != null && cardDescriptionText != null)
            cardDescriptionText.text = "清场完成 · 拿到遗物「" + def.Name + "」";
        RefreshRelicRow();
    }
}
