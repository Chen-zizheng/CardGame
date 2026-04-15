using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RewardController : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("卡牌生成的父节点 (挂载了 GridLayoutGroup 或 HorizontalLayoutGroup)")]
    public Transform cardGridParent;
    [Tooltip("你的 Card 预制体")]
    public GameObject cardViewPrefab;
    [Tooltip("跳过按钮 (不选牌直接走)")]
    public Button skipButton;

    private List<GameObject> spawnedCards = new List<GameObject>();

    private void Start()
    {
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(ReturnToMap);
        }

        GenerateRewards();
    }

    private void GenerateRewards()
    {
        if (GameManager.Instance == null || GameManager.Instance.allAvailableCards.Count == 0)
        {
            Debug.LogError("【RewardController】GameManager为空，或者总卡池(allAvailableCards)里没有配置任何卡牌！");
            return;
        }

        // 1. 从总卡池中拷贝一份临时列表，用于随机抽取
        List<CardData> pool = new List<CardData>(GameManager.Instance.allAvailableCards);
        List<CardData> selectedCards = new List<CardData>();

        // 2. 随机抽取3张（如果不满3张就有多少抽多少）
        int drawCount = Mathf.Min(3, pool.Count);
        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            selectedCards.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex); // 抽走后剔除，保证出来的3张牌不重复
        }

        // 3. 生成UI实体
        foreach (CardData cardData in selectedCards)
        {
            GameObject cardObj = Instantiate(cardViewPrefab, cardGridParent);
            CardView cardView = cardObj.GetComponent<CardView>();

            if (cardView != null)
            {
                cardView.SetCardData(cardData);
                cardView.SetCardInteractable(true); // 保证卡牌可以被点击

                // 绑定点击事件：点击这张卡，就把它加入牌库，并返回地图
                cardView.cardButton.onClick.RemoveAllListeners();
                cardView.cardButton.onClick.AddListener(() => OnCardSelected(cardData));
            }
            spawnedCards.Add(cardObj);
        }
    }

    private void OnCardSelected(CardData selectedCard)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCardToDeck(selectedCard);
        }
        ReturnToMap();
    }

    private void ReturnToMap()
    {
        // 拿完奖励，直接返回大地图场景继续选关
        Debug.Log("奖励结算完毕，返回地图...");
        SceneManager.LoadScene("MapScene"); // 填你实际的地图场景名字
    }
}