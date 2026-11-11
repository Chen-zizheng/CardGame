using CardCombat;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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

    // 牌库版式：6 列 × 3 行的缩略卡，卡面 200×280（战斗里是 196×280，视觉上是同一张）
    private const float ThumbW = 200f;
    private const float ThumbH = 280f;
    private const float ThumbGap = 20f;
    private const int   ThumbColumns = 6;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private TextMeshProUGUI countText;
    private ScrollRect deckScroll;
    private RectTransform loadoutStrip;

    private void Start()
    {
        // 面板在场景里排在 Scroll View 前面，会被地图盖住，这里强制提到最上层
        if (deckPanel != null)
        {
            deckPanel.transform.SetAsLastSibling();
            deckPanel.SetActive(false);
        }

        ApplyPanelStyle();
        ApplyButtons();

        // 地图那边刚把 Scroll View 铺成整屏，它会盖住右上角的"牌库"按钮：
        // 把按钮直接挂到 Canvas 根上（父级换成整屏的 Canvas，锚点才算得准），再提到最上层
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        if (viewDeckButton != null && canvas != null && viewDeckButton.transform.parent != canvas.transform)
        {
            viewDeckButton.transform.SetParent(canvas.transform, false);
        }
        if (viewDeckButton != null) viewDeckButton.transform.SetAsLastSibling();

        if (viewDeckButton != null) viewDeckButton.onClick.AddListener(OpenDeckPanel);
        if (closeDeckButton != null) closeDeckButton.onClick.AddListener(CloseDeckPanel);
    }

    // 全屏深色遮罩 + 顶部标题 + 缩略卡网格
    private void ApplyPanelStyle()
    {
        if (deckPanel != null)
        {
            RectTransform panelRect = deckPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = Vector2.zero;
            }

            Image bg = deckPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.058f, 0.05f, 0.09f, 0.94f);

            CreateTitle();
        }

        if (cardGridParent != null)
        {
            GridLayoutGroup grid = cardGridParent.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = cardGridParent.gameObject.AddComponent<GridLayoutGroup>();

            grid.cellSize = new Vector2(ThumbW, ThumbH);
            grid.spacing = new Vector2(ThumbGap, ThumbGap);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = ThumbColumns;

            BuildScrollArea();
        }

        BuildLoadoutStrip();
    }

    /// <summary>
    /// 牌库滚动区。原来卡片是直接铺在面板上的，牌一多（18 张就要 3 行 × 300 = 900px）
    /// 就顶出屏幕外，玩家反馈"牌库无法上下滚动"。
    /// 场景改不了（编辑器起不来），所以运行时搭：面板 → DeckScroll(ScrollRect) → Viewport(RectMask2D) → 原来的网格。
    /// 网格改成顶对齐 + ContentSizeFitter，高度由行数撑开，ScrollRect 就能滚。
    /// </summary>
    private void BuildScrollArea()
    {
        if (deckPanel == null || cardGridParent == null) return;

        Transform existing = deckPanel.transform.Find("DeckScroll");
        GameObject scrollGo;
        if (existing != null)
        {
            scrollGo = existing.gameObject;
        }
        else
        {
            scrollGo = new GameObject("DeckScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(deckPanel.transform, false);
        }

        // ScrollRect 自己得有一块能被射线打中的东西（滚轮 / 拖拽都靠它接收），
        // 全透明但 raycastTarget=true —— 没有这块图形，鼠标在牌库上滚是没反应的。
        Image hit = scrollGo.GetComponent<Image>();
        if (hit == null) hit = scrollGo.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        // 可视区：标题（-150）和下方"遗物 / 本能"带（底部 222）之间，左右各留 120
        RectTransform srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(120f, 222f);
        srt.offsetMax = new Vector2(-120f, -150f);

        Transform viewT = scrollGo.transform.Find("Viewport");
        if (viewT == null)
        {
            GameObject v = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            v.transform.SetParent(scrollGo.transform, false);
            viewT = v.transform;
        }
        RectTransform vrt = viewT.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.pivot = new Vector2(0f, 1f);
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        if (viewT.GetComponent<RectMask2D>() == null) viewT.gameObject.AddComponent<RectMask2D>();

        // 网格搬进 Viewport，改成"贴顶、宽度跟随、高度由内容决定"
        if (cardGridParent.parent != viewT) cardGridParent.SetParent(viewT, false);
        RectTransform gridRect = cardGridParent as RectTransform;
        if (gridRect != null)
        {
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = Vector2.zero;
            gridRect.localScale = Vector3.one;
        }

        ContentSizeFitter fitter = cardGridParent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = cardGridParent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
        if (sr == null) sr = scrollGo.AddComponent<ScrollRect>();
        sr.viewport = vrt;
        sr.content = gridRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.elasticity = 0.12f;
        sr.inertia = true;
        sr.decelerationRate = 0.12f;
        sr.scrollSensitivity = 45f;
        deckScroll = sr;
    }

    private void CreateTitle()
    {
        if (countText != null) return;

        GameObject titleObj = new GameObject("DeckTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(deckPanel.transform, false);

        RectTransform rt = titleObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -48f);
        rt.sizeDelta = new Vector2(900f, 56f);

        TextMeshProUGUI title = titleObj.GetComponent<TextMeshProUGUI>();
        title.text = "牌库";
        title.fontSize = 40f;
        title.enableAutoSizing = false;
        title.alignment = TextAlignmentOptions.Center;
        title.color = UiSkin.Ink;
        title.raycastTarget = false;

        GameObject countObj = new GameObject("DeckCount", typeof(RectTransform), typeof(TextMeshProUGUI));
        countObj.transform.SetParent(deckPanel.transform, false);

        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.5f, 1f);
        countRect.anchorMax = new Vector2(0.5f, 1f);
        countRect.pivot = new Vector2(0.5f, 1f);
        countRect.anchoredPosition = new Vector2(0f, -96f);
        countRect.sizeDelta = new Vector2(900f, 32f);

        countText = countObj.GetComponent<TextMeshProUGUI>();
        countText.fontSize = 20f;
        countText.enableAutoSizing = false;
        countText.alignment = TextAlignmentOptions.Center;
        countText.color = UiSkin.Dim;
        countText.raycastTarget = false;
    }

    private void ApplyButtons()
    {
        if (viewDeckButton != null)
        {
            UiSkin.SetRect(viewDeckButton.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-56f, -44f), new Vector2(148f, 52f));
            UiSkin.StyleButton(viewDeckButton, 22f);
        }

        if (closeDeckButton != null)
        {
            // 关闭挪到右下，避免和右上角那个"牌库"按钮叠在一起
            UiSkin.SetRect(closeDeckButton.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-56f, 44f), new Vector2(148f, 52f));
            UiSkin.StyleButton(closeDeckButton, 22f);
        }

        // 这里原来还有一颗"主菜单"。它和 ESC 弹窗里的"返回主菜单"是同一件事，
        // 所以撤掉了：面板只管开关（关闭在右下），回主菜单统一按 ESC。
    }

    private void OpenDeckPanel()
    {
        if (deckPanel == null) return;
        deckPanel.SetActive(true);
        deckPanel.transform.SetAsLastSibling();
        if (viewDeckButton != null) viewDeckButton.gameObject.SetActive(false);
        RefreshDeckView();
    }

    private void CloseDeckPanel()
    {
        if (deckPanel != null) deckPanel.SetActive(false);
        if (viewDeckButton != null) viewDeckButton.gameObject.SetActive(true);
    }


    // ---------------- 遗物 / 本能带 ----------------
    // 牌库这一屏是玩家做规划的地方，但之前只有牌：遗物和本能是本局最强的两个变量，
    // 却只活在战斗 HUD 那排 42px 图标里（离开战斗就查不到）。所以挪到底部一条。

    private void BuildLoadoutStrip()
    {
        if (deckPanel == null) return;

        Transform existing = deckPanel.transform.Find("LoadoutStrip");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("LoadoutStrip", typeof(RectTransform));
            go.transform.SetParent(deckPanel.transform, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-240f, 96f);
        rt.anchoredPosition = new Vector2(0f, 118f);
        rt.localScale = Vector3.one;

        loadoutStrip = rt;
        RefreshLoadout();
    }

    private void RefreshLoadout()
    {
        if (loadoutStrip == null) return;

        for (int i = loadoutStrip.childCount - 1; i >= 0; i--)
            Destroy(loadoutStrip.GetChild(i).gameObject);

        GameManager gm = GameManager.Instance;
        BuildLoadoutRow(1, "遗物", gm != null ? gm.relics : null, true);
        BuildLoadoutRow(-1, "本能", gm != null ? gm.talents : null, false);
    }

    private void BuildLoadoutRow(int row, string label, System.Collections.Generic.IList<string> ids, bool relic)
    {
        float cy = row * 26f;
        UiSkin.CreateText(loadoutStrip, "Label_" + label, label,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, cy), new Vector2(46f, 22f),
            13f, new Color(0.62f, 0.70f, 0.70f, 0.95f), TextAlignmentOptions.Left);

        if (ids == null || ids.Count == 0)
        {
            string empty = relic ? "还没有" : "出生自带一条，每打完 3 / 6 / 9 层再长一条";
            UiSkin.CreateText(loadoutStrip, "Empty_" + label, empty,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, cy), new Vector2(760f, 22f),
                13f, new Color(0.52f, 0.59f, 0.60f, 0.9f), TextAlignmentOptions.Left);
            return;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            float x = 52f + i * 172f;

            GameObject cell = new GameObject("Cell_" + id, typeof(RectTransform));
            cell.transform.SetParent(loadoutStrip, false);
            RectTransform crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0.5f);
            crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(40f, 40f);
            crt.anchoredPosition = new Vector2(x, cy);

            Image img = cell.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = true;

            string title, body, iconPath;
            if (relic)
            {
                RelicDef d = RelicLibrary.Get(id);
                title = d != null ? d.Name : id;
                body = d != null ? d.Desc : "";
                iconPath = "Art/Relics/" + id;
            }
            else
            {
                TalentDef d = TalentLibrary.Get(id);
                title = d != null ? d.Name : id;
                body = d != null ? d.Desc : "";
                iconPath = "Art/Talents/" + (d != null ? d.Icon : id);
            }

            Sprite s = Resources.Load<Sprite>(iconPath);
            if (s != null) img.sprite = s;
            else img.color = new Color(0.72f, 0.90f, 0.86f, 0.55f);

            // 名字写在图标右边就够了，具体效果交给悬停（和战斗 HUD 用同一套 IconTip）
            UiSkin.CreateText(loadoutStrip, "Name_" + id, title,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 48f, cy), new Vector2(116f, 22f),
                14f, UiSkin.Ink, TextAlignmentOptions.Left);

            IconTip.Attach(cell, (relic ? "" : "本能 · ") + title, body);
        }
    }

    private void RefreshDeckView()
    {
        // 1. 清理旧的UI展示
        foreach (var cardObj in spawnedCards)
        {
            if (cardObj != null) Destroy(cardObj);
        }
        spawnedCards.Clear();

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("找不到GameManager，无法显示牌库！");
            return;
        }

        // 数一下有几张强化牌：牌库里 18 张混着 3 张「+」，只报总数等于没说
        int upgraded = 0;
        foreach (CardData d in GameManager.Instance.playerDeck)
            if (d != null && CardLibrary.IsUpgraded(d.id)) upgraded++;

        if (countText != null)
        {
            countText.text = GameManager.Instance.playerDeck.Count + " 张"
                + (upgraded > 0 ? " · " + upgraded + " 张已强化" : "");
        }

        // 2. 遍历玩家真正的牌库，生成预制体
        foreach (CardData cardData in GameManager.Instance.playerDeck)
        {
            GameObject newCardObj = Instantiate(cardViewPrefab, cardGridParent);
            CardView cardView = newCardObj.GetComponent<CardView>();

            if (cardView != null)
            {
                cardView.SetCardData(cardData);
                cardView.ApplyThumbnail(ThumbW, ThumbH);
                // 【关键】展示界面只读：不暗淡、不响应点击（不能真的出牌）
                cardView.SetReadOnly();
            }

            spawnedCards.Add(newCardObj);
        }

        // 打开时回到最上面。ForceUpdateCanvases 是为了让 GridLayoutGroup / ContentSizeFitter
        // 先把内容高度算出来，否则第一帧 content 还是旧尺寸，滚动条位置会被钳在最底。
        if (deckScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            deckScroll.verticalNormalizedPosition = 1f;
        }

        RefreshLoadout();
    }
}
