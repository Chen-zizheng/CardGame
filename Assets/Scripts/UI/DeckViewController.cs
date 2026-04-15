using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DeckViewController : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("整个查看牌库的UI面板(包含半透明背景)")]
    public GameObject deckPanel;
    [Tooltip("卡牌生成的父节点 (通常是一个挂载了 GridLayoutGroup 的 Content 节点)")]
    public Transform cardGridParent;
    [Tooltip("你在战斗里用的那个 Card 预制体")]
    public GameObject cardViewPrefab;

    [Header("按钮引用")]
    public Button viewDeckButton;  // 地图上的“查看牌库”按钮
    public Button closeDeckButton; // 面板上的“关闭”按钮

    private List<GameObject> spawnedCards = new List<GameObject>();

    private void Start()
    {
        // 初始隐藏面板
        if (deckPanel != null) deckPanel.SetActive(false);

        if (viewDeckButton != null) viewDeckButton.onClick.AddListener(OpenDeckPanel);
        if (closeDeckButton != null) closeDeckButton.onClick.AddListener(CloseDeckPanel);
    }

    private void OpenDeckPanel()
    {
        deckPanel.SetActive(true);
        RefreshDeckView();
    }

    private void CloseDeckPanel()
    {
        deckPanel.SetActive(false);
    }

    private void RefreshDeckView()
    {
        // 1. 清理旧的UI展示
        foreach (var cardObj in spawnedCards)
        {
            Destroy(cardObj);
        }
        spawnedCards.Clear();

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("找不到GameManager，无法显示牌库！");
            return;
        }

        // 2. 遍历玩家真正的牌库，生成预制体
        foreach (CardData cardData in GameManager.Instance.playerDeck)
        {
            GameObject newCardObj = Instantiate(cardViewPrefab, cardGridParent);
            CardView cardView = newCardObj.GetComponent<CardView>();

            if (cardView != null)
            {
                cardView.SetCardData(cardData);
                // 【关键】在展示界面，卡牌必须是只读的，不能点击出牌！
                cardView.SetCardInteractable(false);
            }

            spawnedCards.Add(newCardObj);
        }
    }
}