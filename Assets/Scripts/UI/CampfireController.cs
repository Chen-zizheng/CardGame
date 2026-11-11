using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class CampfireController : MonoBehaviour
{
    // 休息回复的最大生命比例。
    // 曾经提到 0.4 想把通关率抬上去，量下来才发现它偏心：清道夫 48.5%→54.5%，解剖师 40.0%→41.0%，
    // 两个职业的差距反而从 8.5 个点拉大到 13.5 个点（能扛住的人最吃治疗）。
    // 所以退回 0.3，把差距交给解剖师自己的被动去补（见 BattleSystem.DissectorHealPerCard）。
    private const float RestHealPercent = 0.3f;

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
    private Button upgradeButton;

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

        ApplyCampfireLayout();
        EnsureUpgradeButton();
    }

    /// <summary>
    /// 第三个选项"强化一张牌"是运行时加出来的：场景里只有三个按钮，编辑器又起不来，
    /// 所以复用 UiSkin 的按钮工厂，把第四个按钮挂在同一张画布上，位置交给 StyleCampfireButton。
    /// </summary>
    private void EnsureUpgradeButton()
    {
        if (upgradeButton != null) return;

        Transform parent = statusText != null ? statusText.transform.parent : transform;
        if (parent == null) return;

        upgradeButton = UiSkin.CreateButton(parent, "UpgradeButton", "强化一张牌",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 252f), new Vector2(340f, 72f), 26f);
        upgradeButton.onClick.AddListener(() => UpgradePanel.Open());
        StyleCampfireButton(upgradeButton, 252f);
    }

    // 营火界面本来只有一行橘色文字和三个默认色按钮，黑底上飘着，很荒芜。
    // 这里给它一套固定版式：居中标题 + 三个米白按钮竖排 + 左上血量。
    private void ApplyCampfireLayout()
    {
        if (statusText != null)
        {
            statusText.fontSize = 40f;
            statusText.enableAutoSizing = false;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = UiSkin.Ink;
            RectTransform rt = statusText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1100f, 130f);
            rt.anchoredPosition = new Vector2(0f, -150f);
        }

        // 四个按钮竖排：回血 / 强化 / 精简 / 离开。间距 84，最下面那个离底边 84。
        StyleCampfireButton(restButton, 336f);
        StyleCampfireButton(upgradeButton, 252f);
        StyleCampfireButton(removeCardButton, 168f);
        StyleCampfireButton(leaveButton, 84f);

        UpdateRestLabel();
    }

    /// <summary>「休息」的按钮上直接写清楚能回多少 —— 这个数字以前只写在 Debug.Log 里。</summary>
    private void UpdateRestLabel()
    {
        if (restButton == null || GameManager.Instance == null) return;
        int heal = Mathf.RoundToInt(GameManager.Instance.playerMaxHp * RestHealPercent);
        TextMeshProUGUI label = restButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "休息 · 回复 " + heal + " 生命";
    }

    private void StyleCampfireButton(Button button, float y)
    {
        if (button == null) return;

        RectTransform rt = button.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(340f, 72f);
        rt.anchoredPosition = new Vector2(0f, y);

        UiSkin.StyleButton(button, 26f);
    }

    private void UpdateStatusText()
    {
        if (GameManager.Instance != null)
        {
            // 营火 = 设施里的「应急充能站」。世界观名词换掉，机制一个字没改。
            statusText.text = $"应急充能站\n处置员生命 {GameManager.Instance.currentPlayerHp} / {GameManager.Instance.playerMaxHp}";
        }
    }

    private void OnRest()
    {
        if (GameManager.Instance != null)
        {
            int healAmount = Mathf.RoundToInt(GameManager.Instance.playerMaxHp * RestHealPercent);
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