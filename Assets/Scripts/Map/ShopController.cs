using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ShopController : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI goldText;         // 显示当前金币
    public Transform cardGridParent;         // 货架（挂载了LayoutGroup的容器）
    public GameObject cardViewPrefab;        // 卡牌预制体

    [Header("商店服务")]
    public Button healButton;                // 花钱回血的按钮
    public TextMeshProUGUI healButtonText;   // 回血按钮上的文字
    public int healPrice = 50;               // 回血价格
    public int healAmount = 20;              // 回血量

    public Button leaveButton;               // 离开商店按钮

    private List<GameObject> spawnedCards = new List<GameObject>();

    private void Start()
    {
        UpdateGoldText();

        if (leaveButton != null)
            leaveButton.onClick.AddListener(LeaveShop);

        if (healButton != null)
        {
            healButtonText.text = $"休息恢复 {healAmount}HP\n<color=#FFD700>{healPrice} 金币</color>";
            healButton.onClick.AddListener(BuyHeal);
        }

        GenerateShopCards();
    }

    private void UpdateGoldText()
    {
        if (GameManager.Instance != null)
        {
            goldText.text = $"金币: {GameManager.Instance.currentGold}";
        }
    }

    private void GenerateShopCards()
    {
        if (GameManager.Instance == null || GameManager.Instance.allAvailableCards.Count == 0) return;

        // 随机进货 3 张牌
        List<CardData> pool = new List<CardData>(GameManager.Instance.allAvailableCards);

        for (int i = 0; i < 3; i++)
        {
            if (pool.Count == 0) break;

            int randomIndex = Random.Range(0, pool.Count);
            CardData cardToSell = pool[randomIndex];
            pool.RemoveAt(randomIndex);

            // 每张卡随机一个价格 (50 ~ 80)
            int cardPrice = Random.Range(50, 81);

            GameObject cardObj = Instantiate(cardViewPrefab, cardGridParent);
            CardView cardView = cardObj.GetComponent<CardView>();

            if (cardView != null)
            {
                cardView.SetCardData(cardToSell);
                cardView.SetCardInteractable(true);

                // 在卡牌描述下面强制加上价格显示
                if (cardView.cardDescriptionText != null)
                {
                    cardView.cardDescriptionText.text += $"\n\n<color=#FFD700>售价: {cardPrice} G</color>";
                }

                // 绑定购买事件
                cardView.cardButton.onClick.RemoveAllListeners();
                cardView.cardButton.onClick.AddListener(() => BuyCard(cardToSell, cardPrice, cardObj));
            }
            spawnedCards.Add(cardObj);
        }
    }

    private void BuyCard(CardData card, int price, GameObject cardUIObj)
    {
        if (GameManager.Instance.currentGold >= price)
        {
            // 扣钱、���卡
            GameManager.Instance.currentGold -= price;
            GameManager.Instance.AddCardToDeck(card);
            UpdateGoldText();

            Debug.Log($"花 {price} 金币购买了 {card.cardName}！");

            // 买完之后，这张卡从货架上消失
            cardUIObj.SetActive(false);
        }
        else
        {
            Debug.LogWarning("金币不足，买不起这张卡！");
            // 这里以后可以加个屏幕飘字提示“金币不足”
        }
    }

    private void BuyHeal()
    {
        if (GameManager.Instance.currentGold >= healPrice)
        {
            if (GameManager.Instance.currentPlayerHp >= GameManager.Instance.playerMaxHp)
            {
                Debug.LogWarning("血量已满，不需要回血！");
                return;
            }

            // 扣钱、回血
            GameManager.Instance.currentGold -= healPrice;
            GameManager.Instance.currentPlayerHp = Mathf.Min(GameManager.Instance.playerMaxHp, GameManager.Instance.currentPlayerHp + healAmount);

            UpdateGoldText();
            Debug.Log("回血成功！");

            // 回血服务只能买一次，买完禁用按钮
            healButton.interactable = false;
        }
        else
        {
            Debug.LogWarning("金币不足，无法回血！");
        }
    }

    private void LeaveShop()
    {
        SceneManager.LoadScene("MapScene");
    }
}