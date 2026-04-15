using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class CampfireController : MonoBehaviour
{
    [Header("基础 UI 引用")]
    public TextMeshProUGUI statusText;
    public Button restButton;
    public Button removeCardButton;
    public Button leaveButton;

    [Header("删牌面板 UI 引用")]
    public GameObject removeCardPanel;      // 整个删牌黑底面板
    public Transform cardGridParent;        // 挂载了Grid Layout的节点
    public GameObject cardViewPrefab;       // 卡牌预制体
    public Button cancelRemoveButton;       // 取消删牌的按钮

    private List<GameObject> spawnedCards = new List<GameObject>();

    private void Start()
    {
        UpdateStatusText();

        restButton.onClick.AddListener(OnRest);
        removeCardButton.onClick.AddListener(OpenRemoveCardPanel);
        leaveButton.onClick.AddListener(OnLeave);

        if (cancelRemoveButton != null)
            cancelRemoveButton.onClick.AddListener(CloseRemoveCardPanel);

        // 初始隐藏删牌面板
        if (removeCardPanel != null)
            removeCardPanel.SetActive(false);
    }

    private void UpdateStatusText()
    {
        if (GameManager.Instance != null)
        {
            statusText.text = $"温暖的篝火\n当前生命: {GameManager.Instance.currentPlayerHp} / {GameManager.Instance.playerMaxHp}";
        }
    }

    private void OnRest()
    {
        if (GameManager.Instance != null)
        {
            int healAmount = Mathf.RoundToInt(GameManager.Instance.playerMaxHp * 0.3f);
            GameManager.Instance.currentPlayerHp = Mathf.Min(GameManager.Instance.playerMaxHp, GameManager.Instance.currentPlayerHp + healAmount);

            Debug.Log($"休息恢复了 {healAmount} 点生命。");
            OnLeave(); // 休息完直接离开
        }
    }

    private void OpenRemoveCardPanel()
    {
        if (removeCardPanel != null) removeCardPanel.SetActive(true);

        // 1. 先清理旧的卡牌UI
        foreach (var card in spawnedCards) Destroy(card);
        spawnedCards.Clear();

        if (GameManager.Instance == null) return;

        // 2. 遍历生成玩家当前拥有的所有卡牌
        foreach (CardData cardData in GameManager.Instance.playerDeck)
        {
            GameObject cardObj = Instantiate(cardViewPrefab, cardGridParent);
            CardView cardView = cardObj.GetComponent<CardView>();

            if (cardView != null)
            {
                cardView.SetCardData(cardData);
                cardView.SetCardInteractable(true); // 允许点击

                // 【核心逻辑】绑定点击事件：点击即删除这张卡
                cardView.cardButton.onClick.RemoveAllListeners();
                cardView.cardButton.onClick.AddListener(() => RemoveCardConfirm(cardData));
            }
            spawnedCards.Add(cardObj);
        }
    }

    private void CloseRemoveCardPanel()
    {
        if (removeCardPanel != null) removeCardPanel.SetActive(false);
    }

    private void RemoveCardConfirm(CardData cardToRemove)
    {
        if (GameManager.Instance != null)
        {
            // 从全局牌库中永久移除这张卡牌！
            GameManager.Instance.playerDeck.Remove(cardToRemove);
            Debug.Log($"【精简牌库】成功删除了卡牌: {cardToRemove.cardName}");
        }

        // 删牌��于消耗篝火的操作，删完直接离开篝火
        OnLeave();
    }

    private void OnLeave()
    {
        SceneManager.LoadScene("MapScene");
    }
}