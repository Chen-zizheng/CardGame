using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using CardCombat;

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

    [Header("遗物货架")]
    public int relicSlots = 2;               // 一次摆几件
    public int relicPriceMin = 130;
    public int relicPriceMax = 181;

    private List<GameObject> spawnedCards = new List<GameObject>();

    private void Start()
    {
        // 版式交给 ScreenLayout：标题 / 金币 / 货架 / 治疗 / 离开
        ScreenLayout.ApplyShop(Object.FindObjectOfType<Canvas>(), cardGridParent, leaveButton,
            healButton, healButtonText, goldText);

        UpdateGoldText();

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(LeaveShop);
        }

        if (healButton != null)
        {
            healButton.onClick.RemoveAllListeners();
            healButton.onClick.AddListener(BuyHeal);
        }

        GenerateShopCards();

        ScreenLayout.PlaceCards(cardGridParent, spawnedCards.Count, 0f);

        if (healButtonText != null)
        {
            healButtonText.text = "灌注修复 " + healAmount + " 生命\n" + healPrice + " 金币";
        }

        GenerateShopRelics(cardGridParent != null ? cardGridParent.parent : transform);
    }

    /// <summary>
    /// 遗物货架：一次摆两件，买走即从本局永久生效（会立刻写存档）。
    /// 价格故意压在 130~180 —— 一局打完 7 层大概能攒 250~300，够买一件、不够全买。
    /// </summary>
    private void GenerateShopRelics(Transform parent)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || parent == null) return;

        List<string> pool = new List<string>();
        for (int i = 0; i < RelicLibrary.All.Length; i++)
        {
            if (!gm.HasRelic(RelicLibrary.All[i].Id)) pool.Add(RelicLibrary.All[i].Id);
        }

        if (pool.Count == 0)
        {
            UiSkin.CreateText(parent, "RelicSoldOut", "遗物已经集齐了", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, ScreenLayout.RelicY), new Vector2(900f, 40f), 20f, UiSkin.Dim);
            return;
        }

        int slots = Mathf.Min(relicSlots, pool.Count);
        float step = 452f;
        float startX = -(slots - 1) * step * 0.5f;

        for (int i = 0; i < slots; i++)
        {
            int pick = Random.Range(0, pool.Count);
            string id = pool[pick];
            pool.RemoveAt(pick);

            RelicDef def = RelicLibrary.Get(id);
            if (def == null) continue;
            int price = Random.Range(relicPriceMin, relicPriceMax);

            Button btn = UiSkin.CreateButton(parent, "Relic_" + id, def.Name + "\n" + price + " 补给点",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(startX + i * step, ScreenLayout.RelicY), new Vector2(420f, 92f), 20f);

            // 图标靠左、文字右移，避免字压在图标上
            Transform label = btn.transform.Find("Label");
            if (label != null)
            {
                RectTransform lrt = label.GetComponent<RectTransform>();
                if (lrt != null)
                {
                    // UiSkin.CreateButton 给的 Label 是"拉满父级"的锚点，那种锚点下 sizeDelta 是
                    // 相对父级的增减量（420+300=720 宽），anchoredPosition 也只是偏移中心，
                    // 于是文字会整块甩到按钮左边外面去。这里改回居中式锚点再给绝对尺寸。
                    lrt.anchorMin = new Vector2(0.5f, 0.5f);
                    lrt.anchorMax = new Vector2(0.5f, 0.5f);
                    lrt.pivot = new Vector2(0.5f, 0.5f);
                    lrt.sizeDelta = new Vector2(300f, 78f);
                    lrt.anchoredPosition = new Vector2(46f, 0f);
                }
                TextMeshProUGUI lt = label.GetComponent<TextMeshProUGUI>();
                if (lt != null) lt.alignment = TextAlignmentOptions.Left;
            }

            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(btn.transform, false);
            RectTransform irt = icon.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(22f, 0f);
            irt.sizeDelta = new Vector2(56f, 56f);
            Image iimg = icon.GetComponent<Image>();
            iimg.raycastTarget = false;
            iimg.preserveAspect = true;
            Sprite s = Resources.Load<Sprite>("Art/Relics/" + id);
            if (s != null) iimg.sprite = s;
            else iimg.color = UiSkin.Mint;

            Button captured = btn;
            string capturedId = id;
            int capturedPrice = price;
            captured.onClick.RemoveAllListeners();
            captured.onClick.AddListener(() => BuyRelic(capturedId, capturedPrice, captured, def.Name));
        }
    }

    private void BuyRelic(string id, int price, Button btn, string name)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.currentGold < price)
        {
            if (goldText != null) goldText.text = "补给点数  " + gm.currentGold + "   (不够)";
            return;
        }

        if (!gm.AddRelic(id)) return;

        gm.currentGold -= price;
        UpdateGoldText();
        SaveSystem.Save();

        if (btn != null)
        {
            btn.interactable = false;
            Transform label = btn.transform.Find("Label");
            if (label != null)
            {
                TextMeshProUGUI lt = label.GetComponent<TextMeshProUGUI>();
                if (lt != null) lt.text = name + "\n已带走";
            }
        }

        Debug.Log("[商店] 花 " + price + " 补给点买了遗物 " + name);
    }

    private void UpdateGoldText()
    {
        if (GameManager.Instance != null && goldText != null)
        {
            goldText.text = "补给点数  " + GameManager.Instance.currentGold;
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

                // 绑卡前先把价格挂上去（卡片缩到 240 宽之后，描述里再加一行会挤）
                ScreenLayout.AddCaption(cardObj, cardPrice + " 金币", UiSkin.Mint);

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
            // 扣钱、牌入库
            GameManager.Instance.currentGold -= price;
            GameManager.Instance.AddCardToDeck(card);
            UpdateGoldText();

            Debug.Log($"花 {price} 金币购买了 {card.cardName}！");

            // 买完之后，这张卡从货架上消失
            cardUIObj.SetActive(false);
            RelayoutShelf();
        }
        else
        {
            Debug.LogWarning("金币不足，买不起这张牌！");
            if (goldText != null) goldText.text = "金币  " + GameManager.Instance.currentGold + "   (不够)";
        }
    }

    // 买走一张后剩下的牌重新居中，不然货架上会留一个洞
    private void RelayoutShelf()
    {
        int alive = 0;
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null && spawnedCards[i].activeSelf) alive++;
        }
        ScreenLayout.PlaceCards(cardGridParent, alive, 0f);
    }

    private void BuyHeal()
    {
        if (GameManager.Instance.currentGold >= healPrice)
        {
            if (GameManager.Instance.currentPlayerHp >= GameManager.Instance.playerMaxHp)
            {
                Debug.LogWarning("血量已满，不需要回血！");
                if (healButtonText != null) healButtonText.text = "你不需要治疗";
                return;
            }

            // 扣钱、回血
            GameManager.Instance.currentGold -= healPrice;
            GameManager.Instance.currentPlayerHp = Mathf.Min(GameManager.Instance.playerMaxHp, GameManager.Instance.currentPlayerHp + healAmount);

            UpdateGoldText();
            Debug.Log("回血成功！");

            // 回血服务只能买一次，买完禁用按钮
            healButton.interactable = false;
            if (healButtonText != null) healButtonText.text = "治疗用过了";
        }
        else
        {
            Debug.LogWarning("金币不足，无法回血！");
            if (healButtonText != null) healButtonText.text = "金币不足";
        }
    }

    private void LeaveShop()
    {
        SceneManager.LoadScene("MapScene");
    }
}
