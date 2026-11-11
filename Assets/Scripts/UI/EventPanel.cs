using System.Collections.Generic;
using CardCombat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 事件格（地图上那个 ? ）的弹窗。
///
/// 跟 TalentChoice 一样是运行时建的：编辑器起不来（团结引擎 140063），
/// 场景不能改，所以整块面板由代码搭。
///
/// 流程刻意只有两步：读一段话 → 选一个 → 看一行结果 → 继续走。
/// 不做多段分支对话，因为时间是这款游戏的资源，事件也不该比一场战斗更长。
/// </summary>
public class EventPanel : MonoBehaviour
{
    private static EventPanel _instance;

    private RectTransform _panel;
    private RectTransform _choiceRoot;
    private TextMeshProUGUI _body;
    private System.Action _onClosed;

    // 面板高度按"量出来的正文高度 + 选项条数"算，不留猜出来的空白，
    // 也不会再出现正文压到按钮上（之前正文写死 130，长一点就溢出到第一项里）
    private const float ButtonH = 92f;
    private const float ButtonGap = 12f;
    private const float BodyTop = 140f;
    private const float BodyWidth = 980f;
    private const float BodyGap = 30f;
    private const float BodyMinH = 70f;
    private const float BodyMaxH = 260f;
    private const float BottomPad = 40f;

    private static int ChoiceCount(EventDef def) => def != null && def.Choices != null ? def.Choices.Length : 0;

    private static float ListHeight(EventDef def)
    {
        int n = ChoiceCount(def);
        return n <= 0 ? 0f : n * ButtonH + (n - 1) * ButtonGap;
    }

    /// <summary>正文本该多高：交给 TMP 按宽度量，而不是写死一个数字再指望文案不变长。</summary>
    private float BodyHeight()
    {
        if (_body == null) return BodyMinH;
        Vector2 pref = _body.GetPreferredValues(_body.text, BodyWidth, 0f);
        return Mathf.Clamp(pref.y + 8f, BodyMinH, BodyMaxH);
    }

    /// <summary>正文 → 选项 → 面板，三者一起重排。改文案不用再手调坐标。</summary>
    private void Relayout(EventDef def)
    {
        float bh = BodyHeight();
        _body.rectTransform.sizeDelta = new Vector2(BodyWidth, bh);

        float listTop = BodyTop + bh + BodyGap;
        _choiceRoot.anchoredPosition = new Vector2(0f, -listTop);
        _choiceRoot.sizeDelta = new Vector2(940f, ListHeight(def));

        _panel.sizeDelta = new Vector2(1180f, listTop + ListHeight(def) + BottomPad);
        _panel.anchoredPosition = Vector2.zero;
    }

    /// <summary>点击事件格时调。onClosed 会在玩家读完结果、关掉面板之后执行。</summary>
    public static void Open(EventDef def, System.Action onClosed)
    {
        if (def == null) { if (onClosed != null) onClosed(); return; }
        if (_instance != null) return;

        Canvas canvas = FindCanvas();
        if (canvas == null) { if (onClosed != null) onClosed(); return; }

        GameObject go = new GameObject("EventPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        _instance = go.AddComponent<EventPanel>();
        _instance._onClosed = onClosed;
        _instance.Build(rt, def);
    }

    private static Canvas FindCanvas()
    {
        Canvas[] all = Object.FindObjectsOfType<Canvas>();
        Canvas fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == null) continue;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            if (fallback == null || c.sortingOrder > fallback.sortingOrder) fallback = c;
        }
        return fallback;
    }

    private void Build(RectTransform root, EventDef def)
    {
        // 压到几乎全黑：事件是"停一下"的时刻，底下的地图不该抢注意力
        Image scrim = root.gameObject.AddComponent<Image>();
        scrim.color = new Color(0.016f, 0.016f, 0.024f, 0.94f);
        scrim.raycastTarget = true;

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(root, false);
        _panel = panelObj.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(1180f, BodyTop + BodyMinH + BodyGap + ListHeight(def) + BottomPad);
        _panel.anchoredPosition = Vector2.zero;
        UiSkin.Frame(panelObj.GetComponent<Image>(),
            new Color(0.046f, 0.050f, 0.076f, 0.99f), new Color(0.72f, 0.90f, 0.86f, 0.30f), 2, 18);

        // 左上角那枚 ? 字形，和地图节点上的是同一张图
        GameObject iconObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(_panel, false);
        RectTransform ir = iconObj.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 1f);
        ir.anchorMax = new Vector2(0f, 1f);
        ir.pivot = new Vector2(0f, 1f);
        ir.sizeDelta = new Vector2(46f, 46f);
        ir.anchoredPosition = new Vector2(30f, -26f);
        Image icon = iconObj.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        Sprite glyphSprite = UiSkin.MapGlyph("m_event");
        if (glyphSprite != null) icon.sprite = glyphSprite;
        icon.color = new Color(0.72f, 0.90f, 0.86f, 0.85f);

        UiSkin.CreateText(_panel, "Kicker", def.Kicker, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(1000f, 24f), 15f, new Color(0.72f, 0.90f, 0.86f, 0.72f));

        UiSkin.CreateText(_panel, "Title", def.Title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -66f), new Vector2(1000f, 56f), 40f, new Color(0.953f, 0.937f, 0.902f, 1f));

        // 正文：左对齐、行距放宽，读起来像一段叙述而不是说明书
        _body = UiSkin.CreateText(_panel, "Body", def.Body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -BodyTop), new Vector2(BodyWidth, BodyMinH), 19f, new Color(0.80f, 0.86f, 0.85f, 1f),
            TextAlignmentOptions.TopLeft);
        _body.lineSpacing = 12f;
        _body.characterSpacing = 1.5f;

        GameObject choiceRoot = new GameObject("Choices", typeof(RectTransform));
        choiceRoot.transform.SetParent(_panel, false);
        _choiceRoot = choiceRoot.GetComponent<RectTransform>();
        // 选项从正文下面往下排（顶对齐），不是从面板中心往两边铺 ——
        // 三个选项时中心对齐会让最后一项离底边还剩一大块空，像没画完。
        _choiceRoot.anchorMin = new Vector2(0.5f, 1f);
        _choiceRoot.anchorMax = new Vector2(0.5f, 1f);
        _choiceRoot.pivot = new Vector2(0.5f, 1f);
        BuildChoices(def);
        Relayout(def);
    }

    private void BuildChoices(EventDef def)
    {
        for (int i = _choiceRoot.childCount - 1; i >= 0; i--)
            Destroy(_choiceRoot.GetChild(i).gameObject);

        int count = def.Choices != null ? def.Choices.Length : 0;
        if (count == 0) return;

        const float step = ButtonH + ButtonGap;
        for (int i = 0; i < count; i++)
        {
            EventChoice choice = def.Choices[i];
            float y = -i * step;

            Button btn = UiSkin.CreateButton(_choiceRoot, "Choice_" + i, "",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, ButtonH), 22f);
            UiSkin.Frame(btn.GetComponent<Image>(),
                new Color(0.066f, 0.071f, 0.100f, 0.98f), new Color(0.72f, 0.90f, 0.86f, 0.20f), 2, 12);

            Transform label = btn.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);

            // 主文在左，后果在它下面一行：一眼能算清代价，不用把鼠标悬停上去等提示
            TextMeshProUGUI main = UiSkin.CreateText(btn.transform, "Main", choice.Label,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 14f), new Vector2(820f, 34f),
                23f, new Color(0.953f, 0.937f, 0.902f, 1f), TextAlignmentOptions.Left);

            UiSkin.CreateText(btn.transform, "Hint", choice.Hint,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, -16f), new Vector2(820f, 26f),
                15f, new Color(0.64f, 0.72f, 0.72f, 0.95f), TextAlignmentOptions.Left);

            if (main != null) main.characterSpacing = 1f;

            int index = i;
            btn.onClick.AddListener(() => Pick(def, index));
        }
    }

    private void Pick(EventDef def, int index)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) { Close(); return; }
        if (def.Choices == null || index < 0 || index >= def.Choices.Length) { Close(); return; }

        EventChoice choice = def.Choices[index];
        Apply(gm, choice);

        // 事件一落地就写盘：地图节点状态、血量、金币都已经变了，
        // 这时候退出游戏不该把这次遭遇"退回"成没发生。
        SaveSystem.Save();

        ShowResult(def, choice);
    }

    private void Apply(GameManager gm, EventChoice c)
    {
        // 先算血：涨最大生命要连带涨当前血，否则"最大生命 +12"会读成掉血
        if (c.MaxHp != 0)
        {
            gm.playerMaxHp = Mathf.Max(10, gm.playerMaxHp + c.MaxHp);
            if (c.MaxHp > 0) gm.currentPlayerHp = Mathf.Min(gm.playerMaxHp, gm.currentPlayerHp + c.MaxHp);
        }

        if (c.Heal != 0) gm.currentPlayerHp = Mathf.Min(gm.playerMaxHp, gm.currentPlayerHp + c.Heal);
        // 扣血保底留 1 点：一个事件不该成为"处决"
        if (c.Hp != 0) gm.currentPlayerHp = Mathf.Max(1, gm.currentPlayerHp - c.Hp);

        if (c.Gold != 0) gm.currentGold = Mathf.Max(0, gm.currentGold + c.Gold);

        if (!string.IsNullOrEmpty(c.AddCard))
        {
            CardData card = DeckFactory.FromId(c.AddCard);
            if (card != null) gm.playerDeck.Add(card);
        }

        if (c.RemoveCount > 0) RemoveCards(gm, c.RemoveCount);

        // 事件版的「强化一张牌」：走篝火同一套规则（每张只能强化一次），
        // 所以这里只挑还没强化过的，强化满了就什么也不发生 —— 不额外造机制。
        if (c.UpgradeCount > 0) UpgradeCards(gm, c.UpgradeCount);

        if (c.GiveRelic)
        {
            string id = RelicLibrary.RollNew(gm.relics, new SeededRandom(System.Environment.TickCount));
            if (!string.IsNullOrEmpty(id) && gm.AddRelic(id)) { }
            else gm.currentGold += 80;   // 遗物集齐了就折算成钱，别给一个空选项
        }
    }

    /// <summary>随机强化 N 张还没强化过的牌。等价于在篝火上多按了一次「强化」。</summary>
    private static void UpgradeCards(GameManager gm, int count)
    {
        SeededRandom rng = new SeededRandom(System.Environment.TickCount ^ 0x2b3f);
        for (int n = 0; n < count; n++)
        {
            List<int> pool = new List<int>(gm.playerDeck.Count);
            for (int i = 0; i < gm.playerDeck.Count; i++)
            {
                CardData cd = gm.playerDeck[i];
                if (cd == null) continue;
                if (CardLibrary.IsUpgraded(cd.id)) continue;
                if (CardLibrary.Get(cd.id) == null) continue;
                pool.Add(i);
            }
            if (pool.Count == 0) return;

            int idx = pool[rng.Next(pool.Count)];
            gm.playerDeck[idx] = DeckFactory.FromId(CardLibrary.UpgradeOf(gm.playerDeck[idx].id));
        }
    }

    /// <summary>优先删「普通攻击」：那是每个人牌组里最多、也最没用的一张。</summary>
    private static void RemoveCards(GameManager gm, int count)
    {
        SeededRandom rng = new SeededRandom(System.Environment.TickCount ^ 0x51ed);
        for (int n = 0; n < count; n++)
        {
            if (gm.playerDeck.Count <= 1) return;

            int pick = -1;
            for (int i = 0; i < gm.playerDeck.Count; i++)
            {
                if (gm.playerDeck[i] != null && gm.playerDeck[i].id == CardLibrary.Basic) { pick = i; break; }
            }
            if (pick < 0) pick = rng.Next(gm.playerDeck.Count);

            gm.playerDeck.RemoveAt(pick);
        }
    }

    private void ShowResult(EventDef def, EventChoice choice)
    {
        for (int i = _choiceRoot.childCount - 1; i >= 0; i--)
            Destroy(_choiceRoot.GetChild(i).gameObject);

        if (_body != null)
        {
            _body.text = choice.Result;
            _body.color = new Color(0.86f, 0.94f, 0.90f, 1f);
        }

        // 结果态：正文换成那一行结果，下面接真实得失与继续键。
        // 【曾经的 bug】这两样原来挂在 _choiceRoot 上，而它固定悬在面板顶下方 294；
        // 面板一收矮，它们就落到面板外面 —— 继续键点不到，等于把玩家锁在事件里。
        float bh = BodyHeight();
        _body.rectTransform.sizeDelta = new Vector2(BodyWidth, bh);

        float summaryY = BodyTop + bh + 40f;
        float buttonTop = summaryY + 30f;

        // 面板是中心锚点：收矮时把中心抬等同的一半，顶边才不跳（标题不该在结果态上下移）
        float from = _panel.sizeDelta.y;
        float to = buttonTop + 72f + BottomPad;
        _panel.sizeDelta = new Vector2(1180f, to);
        _panel.anchoredPosition = new Vector2(0f, (from - to) * 0.5f);

        UiSkin.CreateText(_panel, "Summary", Summary(choice),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -summaryY), new Vector2(900f, 30f),
            17f, new Color(0.72f, 0.90f, 0.86f, 0.92f));

        Button go = UiSkin.CreateButton(_panel, "Continue", "继续往下走",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -buttonTop), new Vector2(360f, 72f), 24f);
        go.onClick.AddListener(Close);
    }

    /// <summary>把这次的实际得失拼成一行，写在结果下面（数字用真实值，不是文案里的承诺）。</summary>
    private static string Summary(EventChoice c)
    {
        List<string> parts = new List<string>(4);
        if (c.Hp != 0) parts.Add("-" + c.Hp + " 生命");
        if (c.Heal != 0) parts.Add("+" + c.Heal + " 生命");
        if (c.MaxHp > 0) parts.Add("+" + c.MaxHp + " 生命上限");
        if (c.MaxHp < 0) parts.Add(c.MaxHp + " 生命上限");
        if (c.Gold != 0) parts.Add((c.Gold > 0 ? "+" : "") + c.Gold + " 补给点");
        if (!string.IsNullOrEmpty(c.AddCard))
        {
            CardDef def = CardLibrary.Get(c.AddCard);
            parts.Add("牌组 +「" + (def != null ? def.Name : c.AddCard) + "」");
        }
        if (c.RemoveCount > 0) parts.Add("牌组 -" + c.RemoveCount + " 张");
        if (c.GiveRelic) parts.Add("一件遗物");
        if (c.UpgradeCount > 0) parts.Add("强化 " + c.UpgradeCount + " 张牌");
        if (parts.Count == 0) return "什么也没有改变。";
        return string.Join("　·　", parts.ToArray());
    }

    private void Close()
    {
        _instance = null;
        System.Action cb = _onClosed;
        _onClosed = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }
}
