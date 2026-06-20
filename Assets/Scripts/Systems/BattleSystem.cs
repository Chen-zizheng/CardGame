using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

public enum CardType { Attack, Heal, Shield, Draw }
public enum EnemyIntentType { Attack, Defend, Buff }

[System.Serializable]
public class CardData
{
    public string cardName;
    public int damage;
    [TextArea] public string description;
    public CardType cardType;
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

public class BattleSystem : MonoBehaviour
{
    public static BattleSystem Instance;

    [Header("基础UI引用")]
    [SerializeField] private TextMeshProUGUI stageInfoText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI cardDescriptionText;
    [SerializeField] private TextMeshProUGUI enemyIntentText;

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

    private EnemyData currentEnemyData;
    private int currentEnemyHp;
    private int currentEnemyMaxHp;
    private int currentEnemyAttack;
    private int currentEnergy;
    private bool isPlayerTurn;

    private bool isCardPlaying = false;

    private int currentPlayerShield = 0;
    private int currentEnemyShield = 0;
    private EnemyIntentType currentEnemyIntent;
    private int currentEnemyIntentValue;

    private List<CardData> drawPile;
    private List<CardData> discardPile;
    private CardData[] hand;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (GameManager.Instance == null) return;

        // 【动态初始化】如果 cardViews 数组为空，自动从 HandContainer 查找
        if (cardViews == null || cardViews.Length == 0)
        {
            Transform handContainer = transform.Find("HandContainer");
            if (handContainer != null)
            {
                cardViews = handContainer.GetComponentsInChildren<CardView>();
                Debug.Log($"【BattleSystem】自动找到 {cardViews.Length} 个 CardView");
            }
            else
            {
                Debug.LogError("【BattleSystem】找不到 HandContainer！");
            }
        }

        // 【诊断】打印当前牌库
        Debug.Log($"【BattleSystem】当前角色: {GameManager.Instance.currentCharacter?.characterName}");
        Debug.Log($"【BattleSystem】playerDeck 中的卡牌数: {GameManager.Instance.playerDeck.Count}");
        foreach (var card in GameManager.Instance.playerDeck)
        {
            Debug.Log($"  - {card.cardName}");
        }
        if (GameManager.Instance.currentCharacter.battleSprite != null)
        {
            playerImage.sprite = GameManager.Instance.currentCharacter.battleSprite;
            playerImage.SetNativeSize();
            Debug.Log($"【立绘加载】sprite: {playerImage.sprite.name}, 尺寸: {playerImage.rectTransform.sizeDelta}");
        }
        if (GameManager.Instance == null) return;

        foreach (CardView view in cardViews)
        {
            if (view != null) view.gameObject.SetActive(false);
        }

        int stageIndex = GameManager.Instance.currentStageIndex;
        int enemyIndex = GameManager.Instance.currentEnemyLevel - 1;

        if (enemyIndex >= 0 && enemyIndex < GameManager.Instance.stageEnemies.Count)
        {
            currentEnemyData = GameManager.Instance.stageEnemies[enemyIndex];
        }
        else
        {
            currentEnemyData = GameManager.Instance.stageEnemies[0];
        }

        currentEnemyMaxHp = currentEnemyData.maxHp;
        currentEnemyHp = currentEnemyMaxHp;
        currentEnemyAttack = currentEnemyData.baseAttack;

        if (currentEnemyData.enemySprite != null && enemyImage != null)
            enemyImage.sprite = currentEnemyData.enemySprite;

        // ==========================================
        // 【立绘加载与兜底机制】
        if (GameManager.Instance.currentCharacter != null && playerImage != null)
        {
            // 优先使用战斗专属Q版立绘，如果没有，自动回退使用选人界面的立绘
            if (GameManager.Instance.currentCharacter.battleSprite != null)
            {
                playerImage.sprite = GameManager.Instance.currentCharacter.battleSprite;
                // 【Bug 2 根治】：强制刷新 Image 组件的布局尺寸
                // 因为初始状态 Source Image 是 None，Unity 会把尺寸设为 (0,0)
                // 即使后来赋了 sprite，尺寸还是 0，所以看不见
                playerImage.SetNativeSize();
            }
            else if (GameManager.Instance.currentCharacter.characterSprite != null)
            {
                playerImage.sprite = GameManager.Instance.currentCharacter.characterSprite;
                // 同样需要刷新尺寸
                playerImage.SetNativeSize();
            }
        }
        // ==========================================
        currentEnergy = maxEnergy;
        if (stageInfoText != null) stageInfoText.text = $"Stage {stageIndex} - {currentEnemyData.enemyName}";

        isPlayerTurn = true;
        isCardPlaying = false;
        endTurnButton.interactable = true;
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(EndPlayerTurn);

        for (int i = 0; i < cardViews.Length; i++)
        {
            int index = i;
            if (cardViews[index] != null && cardViews[index].cardButton != null)
            {
                cardViews[index].cardButton.onClick.RemoveAllListeners();
                cardViews[index].cardButton.onClick.AddListener(() => OnCardClick(index));
            }
        }

        InitDeck();
        GenerateEnemyIntent();
        UpdateAllUI();

        DrawCards(drawCardPerTurn);
    }

    private void UpdateAllUI()
    {
        UpdateEnemyHpText();
        UpdatePlayerHpText();
        UpdateEnergyText();
    }

    private int TakeDamage(ref int hp, ref int shield, int damage)
    {
        int damageToShield = Mathf.Min(shield, damage);
        shield -= damageToShield;
        int damageToHp = damage - damageToShield;
        hp -= damageToHp;
        hp = Mathf.Max(hp, 0);
        return damageToHp;
    }

    private void PlayerTakeDamage(int damage)
    {
        int damageToShield = Mathf.Min(currentPlayerShield, damage);
        currentPlayerShield -= damageToShield;
        int damageToHp = damage - damageToShield;

        GameManager.Instance.currentPlayerHp -= damageToHp;
        GameManager.Instance.currentPlayerHp = Mathf.Max(GameManager.Instance.currentPlayerHp, 0);
    }

    private void GenerateEnemyIntent()
    {
        int rand = Random.Range(0, 100);
        if (currentEnemyData.aiType == EnemyAIType.Basic)
        {
            if (rand < 80) SetIntent(EnemyIntentType.Attack, currentEnemyAttack);
            else SetIntent(EnemyIntentType.Defend, 5);
        }
        else if (currentEnemyData.aiType == EnemyAIType.Defensive)
        {
            if (rand < 40) SetIntent(EnemyIntentType.Attack, currentEnemyAttack);
            else if (rand < 90) SetIntent(EnemyIntentType.Defend, 12);
            else SetIntent(EnemyIntentType.Buff, 2);
        }
        else if (currentEnemyData.aiType == EnemyAIType.Boss)
        {
            if (rand < 50) SetIntent(EnemyIntentType.Attack, Mathf.RoundToInt(currentEnemyAttack * 1.5f));
            else if (rand < 75) SetIntent(EnemyIntentType.Defend, 15);
            else SetIntent(EnemyIntentType.Buff, 3);
        }
    }

    private void SetIntent(EnemyIntentType type, int value)
    {
        currentEnemyIntent = type;
        currentEnemyIntentValue = value;

        if (enemyIntentText != null)
        {
            if (type == EnemyIntentType.Attack) enemyIntentText.text = $"意图: 攻击 ({value})";
            else if (type == EnemyIntentType.Defend) enemyIntentText.text = $"意图: 防御 ({value})";
            else enemyIntentText.text = $"意图: 强化 (攻击+{value})";
        }
    }

    private void InitDeck()
    {
        if (GameManager.Instance != null && GameManager.Instance.playerDeck.Count > 0)
            drawPile = new List<CardData>(GameManager.Instance.playerDeck);
        else return;

        discardPile = new List<CardData>();
        hand = new CardData[cardViews.Length];
        ShuffleDeck();
    }

    private void ShuffleDeck()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            int randomIndex = Random.Range(i, drawPile.Count);
            CardData temp = drawPile[i];
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
    }

    // 【新增工具】：获取当前手牌数量
    private int GetHandCount()
    {
        int count = 0;
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null) count++;
        }
        return count;
    }

    // 【新增工具】：判断当前是不是整个牌库只剩���了1张牌，防止无限抽牌
    private bool IsSingleCardLoop()
    {
        // 因为这张牌马上要被打出去进入弃牌堆，所以把手牌也算进去
        int totalCards = drawPile.Count + discardPile.Count + GetHandCount();
        return totalCards <= 1; // 如果只有1张牌，就说明陷入了死循环
    }

    // 【修复】：引入 immediate 参数，确保抽牌能立刻填入 hand 数组
    private void DrawCards(int amount, bool immediate = false)
    {
        for (int i = 0; i < amount; i++)
        {
            if (immediate)
            {
                DrawOneCardWithAnimation(0f, true);
            }
            else
            {
                DrawOneCardWithAnimation(i * 0.15f, false);
            }
        }

        if (!immediate)
        {
            DOVirtual.DelayedCall(amount * 0.15f + 0.2f, RefreshAllCardViews);
        }
        else
        {
            RefreshAllCardViews();
        }
    }

    private void DrawOneCardWithAnimation(float delay = 0f, bool immediate = false)
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return;
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }

        if (drawPile.Count > 0)
        {
            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] == null)
                {
                    // 【防守第1步】检查 cardViews[i] 是否有效
                    if (i >= cardViews.Length || cardViews[i] == null)
                    {
                        Debug.LogError($"【DrawOneCard】cardViews[{i}] 为 NULL 或索引越界！cardViews.Length={cardViews.Length}");
                        return;
                    }

                    CardData drawnCard = drawPile[0];
                    drawPile.RemoveAt(0);

                    hand[i] = drawnCard;
                    cardViews[i].SetCardData(drawnCard);
                    cardViews[i].SetCardInteractable(false);

                    // 【防守第2步】检查 parent 是否存在
                    if (cardViews[i].transform.parent == null)
                    {
                        Debug.LogError($"【DrawOneCard】cardViews[{i}].transform.parent 为 NULL！");
                        return;
                    }

                    RectTransform parentRect = cardViews[i].transform.parent.GetComponent<RectTransform>();
                    if (parentRect != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                    }

                    Vector3 startPos = deckTransform != null ?
                        cardViews[i].transform.parent.InverseTransformPoint(deckTransform.position) :
                        new Vector3(-800, -500, 0);

                    cardViews[i].PrepareForDraw(startPos);

                    if (immediate)
                    {
                        cardViews[i].PlayDrawAnimation();
                    }
                    else
                    {
                        int slotIndex = i;
                        DOVirtual.DelayedCall(delay, () => {
                            if (cardViews[slotIndex] != null && cardViews[slotIndex].gameObject.activeInHierarchy)
                                cardViews[slotIndex].PlayDrawAnimation();
                        });
                    }
                    break;
                }
            }
        }
    }

    private void RefreshAllCardViews()
    {
        for (int i = 0; i < cardViews.Length; i++)
        {
            if (hand[i] != null && cardViews[i].gameObject.activeSelf)
            {
                cardViews[i].SetCardInteractable(isPlayerTurn);
                cardViews[i].SetEnergyInsufficient(currentEnergy < hand[i].cost);
            }
        }
    }

    private void OnCardClick(int cardIndex)
    {
        Debug.Log($"【OnCardClick】敌人位置: {enemyImage.transform.position}, 卡牌索引: {cardIndex}");
        if (!isPlayerTurn || hand[cardIndex] == null || isCardPlaying) return;
        CardData useCard = hand[cardIndex];
        CardView usedView = cardViews[cardIndex];

        if (currentEnergy < useCard.cost) return;
        if (useCard.hpCost > 0 && GameManager.Instance.currentPlayerHp <= useCard.hpCost) return;

        isCardPlaying = true;
        currentEnergy -= useCard.cost;

        // --- 1. 立即计算当前手牌数，用于游侠被动判定 ---
        int cardsLeftInHand = 0;
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null && i != cardIndex) cardsLeftInHand++;
        }

        // --- 2. 立即将卡牌移出物理手牌数组，并加入弃牌堆 ---
        hand[cardIndex] = null;
        usedView.SetCardInteractable(false);
        discardPile.Add(useCard);

        // --- 3. 立即结算所有卡牌数值效果 ---
        // 卖血
        if (useCard.hpCost > 0)
        {
            GameManager.Instance.currentPlayerHp -= useCard.hpCost;
            FlashOnHit(playerImage, Color.red);
            SpawnDamagePopup(playerImage.transform.position, useCard.hpCost, Color.red, false);
        }
        // 爆费
        if (useCard.energyGain > 0) currentEnergy += useCard.energyGain;
        // 吸血
        if (useCard.healAmount > 0)
        {
            int actualHeal = Mathf.Min(useCard.healAmount, GameManager.Instance.playerMaxHp - GameManager.Instance.currentPlayerHp);
            GameManager.Instance.currentPlayerHp += actualHeal;
            FlashOnHit(playerImage, Color.green);
            SpawnDamagePopup(playerImage.transform.position, actualHeal, Color.green, true);
        }

        int finalDamage = useCard.damage;
        if (useCard.isDesperation && GameManager.Instance.currentPlayerHp <= GameManager.Instance.playerMaxHp * 0.3f) finalDamage *= 3;

        // 卡牌主效果
        switch (useCard.cardType)
        {
            case CardType.Attack:
                TakeDamage(ref currentEnemyHp, ref currentEnemyShield, finalDamage);
                FlashOnHit(enemyImage, Color.red);
                SpawnDamagePopup(enemyImage.transform.position, finalDamage, Color.red, false);
                break;
            case CardType.Heal:
                int healAmt = Mathf.Min(useCard.damage, GameManager.Instance.playerMaxHp - GameManager.Instance.currentPlayerHp);
                GameManager.Instance.currentPlayerHp += healAmt;
                FlashOnHit(playerImage, Color.green);
                SpawnDamagePopup(playerImage.transform.position, healAmt, Color.green, true);
                break;
            case CardType.Shield:
                currentPlayerShield += finalDamage;
                FlashOnHit(playerImage, Color.blue);
                SpawnDamagePopup(playerImage.transform.position, finalDamage, new Color(0.2f, 0.6f, 1f), true);
                break;
            case CardType.Draw:
                DrawCards(finalDamage);
                break;
        }

        // --- 4. 立即结算游侠被动 ---
        if (GameManager.Instance.currentCharacter != null &&
            GameManager.Instance.currentCharacter.passiveSkill == PassiveSkillType.Ranger_LastCardDraw)
        {
            if (useCard.cardType != CardType.Draw && cardsLeftInHand == 0)
            {
                Debug.Log("【游侠被动触发】打出了最后一张牌，额外抽1张！");
                DrawCards(1);
            }
        }

        UpdateAllUI();
        RefreshAllCardViews();

        // --- 5. 纯视觉表现：卡牌飞出与弃牌动画 ---
        usedView.PlayPlayAnimation(enemyImage.transform.position, () => {
            Vector3 discardPos = discardTransform != null ? discardTransform.position : new Vector3(Screen.width + 200, -200, 0);

            // 【精确修复】：弃牌动画也必须且只能移动 cardVisual，绝不能去动受 LayoutGroup 控制的根节点!
            usedView.PlayPlayAnimation(enemyImage.transform.position, () => {
                Vector3 discardPos = discardTransform != null ? discardTransform.position : new Vector3(Screen.width + 200, -200, 0);

                // 【Bug 1 根治】：确保 OnComplete 回调在 SetActive(false) 之前执行
                // 原始错误：SetActive(false) 会禁用 GameObject，导致 DoTween 回调永远无法触发
                usedView.cardVisual.DOMove(discardPos, 0.2f).OnComplete(() => {
                    // 第1步：立刻解开出牌锁，允许下一次出牌
                    isCardPlaying = false;

                    // 第2步：检查游戏状态
                    if (currentEnemyHp <= 0) { OnWin(); return; }
                    if (GameManager.Instance.currentPlayerHp <= 0) { OnLose(); return; }

                    // 第3步：最后才隐藏卡牌，这样 OnComplete 已经安全执行了
                    usedView.gameObject.SetActive(false);
                });
            });
        });
    }

    private void DiscardHand()
    {
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null)
            {
                CardData leftoverCard = hand[i];

                if (leftoverCard.isRetain)
                {
                    cardViews[i].SetCardInteractable(false);
                    continue;
                }

                CardView leftoverView = cardViews[i];
                discardPile.Add(leftoverCard);
                hand[i] = null;

                leftoverView.SetCardInteractable(false);
                Vector3 discardPos = discardTransform != null ? discardTransform.position : new Vector3(Screen.width + 200, -200, 0);
                leftoverView.transform.DOMove(discardPos, 0.4f).OnComplete(() => leftoverView.gameObject.SetActive(false));
            }
        }
    }

    private void EndPlayerTurn()
    {
        CancelInvoke();
        if (!isPlayerTurn) return;
        isPlayerTurn = false;
        endTurnButton.interactable = false;

        DiscardHand();

        currentEnemyShield = 0;
        UpdateAllUI();
        cardDescriptionText.text = "回合结束，敌人行动中...";

        Invoke(nameof(EnemyAction), 1.5f);
    }

    private void EnemyAction()
    {
        switch (currentEnemyIntent)
        {
            case EnemyIntentType.Attack:
                PlayerTakeDamage(currentEnemyIntentValue);
                FlashOnHit(playerImage, Color.red);
                SpawnDamagePopup(playerImage.transform.position, currentEnemyIntentValue, Color.red, false);
                break;
            case EnemyIntentType.Defend:
                currentEnemyShield += currentEnemyIntentValue;
                FlashOnHit(enemyImage, Color.blue);
                SpawnDamagePopup(enemyImage.transform.position, currentEnemyIntentValue, new Color(0.2f, 0.6f, 1f), true);
                break;
            case EnemyIntentType.Buff:
                currentEnemyAttack += currentEnemyIntentValue;
                FlashOnHit(enemyImage, Color.yellow);
                SpawnDamagePopup(enemyImage.transform.position, currentEnemyIntentValue, Color.yellow, true);
                break;
        }
        UpdateAllUI();
        if (GameManager.Instance.currentPlayerHp <= 0) { OnLose(); return; }
        Invoke(nameof(StartNewTurn), 1f);
    }

    private void StartNewTurn()
    {
        currentPlayerShield = 0;
        GenerateEnemyIntent();
        isPlayerTurn = true;
        isCardPlaying = false; // 新回合强制解锁
        endTurnButton.interactable = true;
        currentEnergy = maxEnergy;
        UpdateAllUI();

        DrawCards(drawCardPerTurn);

        cardDescriptionText.text = "你的回合，请选择一张牌";
    }

    private void SpawnDamagePopup(Vector3 spawnPosition, int amount, Color color, bool isHeal)
    {
        if (damagePopupPrefab == null || popupCanvasParent == null || amount <= 0) return;
        GameObject popupObj = Instantiate(damagePopupPrefab, popupCanvasParent);
        popupObj.transform.position = spawnPosition;
        DamagePopup popup = popupObj.GetComponent<DamagePopup>();
        if (popup != null) popup.Setup(amount, color, isHeal);
    }

    private void FlashOnHit(Image targetImage, Color flashColor)
    {
        Color originalColor = targetImage.color;
        targetImage.DOColor(flashColor, 0.15f).OnComplete(() => { targetImage.DOColor(originalColor, 0.15f); });
    }

    private void OnWin()
    {
        CancelInvoke();
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentCharacter != null &&
            GameManager.Instance.currentCharacter.passiveSkill == PassiveSkillType.Warrior_BloodHeal)
        {
            int missingHp = GameManager.Instance.playerMaxHp - GameManager.Instance.currentPlayerHp;
            if (missingHp > 0)
            {
                int healAmount = Mathf.RoundToInt(missingHp * 0.3f);
                GameManager.Instance.currentPlayerHp += healAmount;
                Debug.Log($"【战士被动触发】战斗结束，回复了 {healAmount} 点生命值！");
            }
        }

        int currentStage = GameManager.Instance.currentStageIndex;
        GameManager.Instance.stageCleared[currentStage - 1] = true;
        if (currentStage < GameManager.Instance.totalStageCount) GameManager.Instance.stageCleared[currentStage] = true;
        GameManager.Instance.SaveStageData();

        if (currentStage >= GameManager.Instance.totalStageCount)
        {
            Debug.Log("击败最终Boss！游戏通关！");
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            Debug.Log("战斗胜利，进入战利品结算...");
            int goldReward = UnityEngine.Random.Range(15, 31);
            GameManager.Instance.currentGold += goldReward;
            Debug.Log($"战斗胜利！获得金币: {goldReward}，当前总金币: {GameManager.Instance.currentGold}");
            SceneManager.LoadScene("RewardScene");
        }
    }

    private void OnLose()
    {
        CancelInvoke();
        SceneManager.LoadScene("FailScene");
    }

    private void UpdateEnemyHpText()
    {
        if (enemyHpText != null && currentEnemyData != null)
        {
            string shieldText = currentEnemyShield > 0 ? $" <color=#3399FF>[护盾:{currentEnemyShield}]</color>" : "";
            enemyHpText.text = $"{currentEnemyData.enemyName} HP: {currentEnemyHp} / {currentEnemyMaxHp}{shieldText}";
        }
    }

    private void UpdatePlayerHpText()
    {
        if (playerHpText != null && GameManager.Instance != null)
        {
            string shieldText = currentPlayerShield > 0 ? $" <color=#3399FF>[护盾:{currentPlayerShield}]</color>" : "";
            playerHpText.text = $"Player HP: {GameManager.Instance.currentPlayerHp} / {GameManager.Instance.playerMaxHp}{shieldText}";
        }
    }

    private void UpdateEnergyText()
    {
        if (energyText != null) energyText.text = $"能量: {currentEnergy} / {maxEnergy}";
    }

    private void OnDestroy()
    {
        DOTween.KillAll();
    }
}