using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using CardCombat;

/// <summary>
/// 战斗画面。全部运行时用代码搭（编辑器在我这边起不来，Inspector 拖不了引用）。
/// 只画三样：时间轴双轨（传送带）、手牌条、飘字与闪光。
/// 角色立绘也是这里新建的，不复用场景里那两个 Image——
/// 它们的颜色 alpha 是 0、类型是 Sliced，改起来比新建更麻烦。
/// </summary>
public class CombatView : MonoBehaviour
{
    public Action<int> CardClicked;

    // ---- 传送带 ----
    private const int SlotCount = 10;
    private const float SlotW = 150f;
    private const float SlotH = 52f;
    private const float Track1Y = -150f;   // 我方轨道
    private const float Track2Y = -226f;   // 敌方轨道

    // ---- 手牌 ----
    private const float CardW = 150f;
    private const float CardH = 224f;
    private const float CardGap = 18f;
    private const float HandY = -402f;

    private const float ActorPlayerX = -500f;
    private const float ActorPlayerY = 110f;
    private const float ActorEnemyX = 470f;
    private const float ActorEnemyY = 140f;
    private const float ActorMaxHeight = 380f;

    private static readonly Color Ink = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color Dim = new Color(0.55f, 0.62f, 0.62f, 1f);
    private static readonly Color Mint = new Color(0.72f, 0.90f, 0.86f, 1f);
    private static readonly Color Red = new Color(0.80f, 0.30f, 0.38f, 1f);

    // ---- 质感层（2026-06 界面返工）----
    // 之前 HUD 是一堆裸字叠在背景上，时间轴是一块平涂矩形，卡牌是直接贴上去的图。
    // 这一组颜色只做"承托"：底板、格线、血条、压角。不加新美术，全靠运行时画。
    private static readonly Color PlateFill = new Color(0.040f, 0.045f, 0.068f, 0.66f);
    private static readonly Color PlateEdge = new Color(0.72f, 0.90f, 0.86f, 0.18f);
    private static readonly Color BarBack = new Color(0.11f, 0.10f, 0.15f, 0.92f);
    private static readonly Color BarGhost = new Color(0.80f, 0.34f, 0.38f, 0.50f);
    private static readonly Color BarPlayer = new Color(0.62f, 0.92f, 0.82f, 1f);
    private static readonly Color BarEnemy = new Color(0.88f, 0.42f, 0.48f, 1f);
    /// <summary>已占格方块上的字：方块现在是亮底，字必须反过来。</summary>
    private static readonly Color BlockInk = new Color(0.08f, 0.09f, 0.12f, 1f);
    // 命中格只做"投影"：比攻击方块淡一档，否则两者都是红块，分不出哪个是招、哪个是落点
    private static readonly Color CellHot = new Color(0.95f, 0.36f, 0.42f, 0.38f);

    private class CardSlot
    {
        public RectTransform rt;
        public Image bg;
        public TextMeshProUGUI name;
        public TextMeshProUGUI hint;
        public Button button;
        public CanvasGroup group;
        public TextMeshProUGUI cost;
        public Image kindBar;
        public Image costBg;
        public TextMeshProUGUI kindTag;
        public GameObject upgradeBadge;
        public Action<int> capture;
    }

    private class ActSlot
    {
        public RectTransform rt;
        public Image bg;
        public TextMeshProUGUI label;
    }

    private class HpBar
    {
        public RectTransform rt;
        public Image fill;
        public Image ghost;
        public float ghostRatio = 1f;
    }

    private RectTransform root;
    private RectTransform handRow;
    private RectTransform trackPlayer;
    private RectTransform trackEnemy;
    private TextMeshProUGUI textTemplate;
    private TextMeshProUGUI warn;
    private TextMeshProUGUI speedText;
    private Image playerActor;
    private Image enemyActor;
    private Canvas canvas;
    private Sprite playerSprite;
    private Sprite enemySprite;

    private readonly List<CardSlot> cards = new List<CardSlot>();
    private readonly List<ActSlot> playerActs = new List<ActSlot>();
    private readonly List<ActSlot> enemyActs = new List<ActSlot>();
    private readonly List<Image> playerCells = new List<Image>();
    private readonly List<Image> enemyCells = new List<Image>();
    private readonly List<Color> enemyCellBase = new List<Color>();
    private HpBar playerBar;
    private HpBar enemyBar;
    private float lastPlayerRatio = -1f;
    private float lastEnemyRatio = -1f;
    private string handSignature = "";

    public static CombatView Create(Transform parent, Sprite playerSprite, Sprite enemySprite,
        Image scenePlayerImage, Image sceneEnemyImage, TextMeshProUGUI textTemplate, Transform hudParent)
    {
        GameObject go = new GameObject("CombatView", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        CombatView view = go.AddComponent<CombatView>();
        view.root = rt;
        view.textTemplate = textTemplate;
        view.playerSprite = playerSprite;
        view.enemySprite = enemySprite;
        view.canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
        if (view.canvas == null && scenePlayerImage != null) view.canvas = scenePlayerImage.canvas;

        // 压角 + 两块玻璃底板最先建：它们要垫在所有东西下面（底板的 sibling 顺序在 Plate 里单独处理）
        view.BuildVignette();
        view.BuildHudPlates(hudParent);

        view.warn = view.MakeText("Warn", view.root, "", 30, Mint, TextAlignmentOptions.Center,
            new Vector2(0f, 372f), new Vector2(1100f, 52f));
        view.warn.raycastTarget = false;

        // 一行常驻规则。不打字说明这套时间轴怎么算账，玩家会以为"格挡=加个盾值"而不会把它摆到命中格上。
        TextMeshProUGUI rule = view.MakeText("Rule", view.root, "", 16, Dim, TextAlignmentOptions.Center,
            new Vector2(0f, 340f), new Vector2(1400f, 30f));
        rule.text = "挡住「命中」那格才算数";
        rule.raycastTarget = false;

        view.speedText = view.MakeText("Speed", view.root, "", 18, Dim, TextAlignmentOptions.Right,
            new Vector2(760f, 468f), new Vector2(380f, 30f));
        view.speedText.raycastTarget = false;

        view.trackPlayer = view.BuildTrack("TrackPlayer", Track1Y, true);
        view.trackEnemy = view.BuildTrack("TrackEnemy", Track2Y, false);
        view.handRow = view.BuildHandRow();

        view.playerActor = view.BuildActor("PlayerActor", playerSprite, ActorPlayerX, ActorPlayerY);
        view.enemyActor = view.BuildActor("EnemyActor", enemySprite, ActorEnemyX, ActorEnemyY);

        view.BuildTrackLabels();
        view.BuildHpBars(hudParent);

        // 场景里那两个旧 Image 只关不删，避免它们以旧贴图/旧 alpha 叠在新立绘上
        if (scenePlayerImage != null) scenePlayerImage.gameObject.SetActive(false);
        if (sceneEnemyImage != null) sceneEnemyImage.gameObject.SetActive(false);
        return view;
    }

    // ---------------- 搭建 ----------------

    private RectTransform BuildTrack(string name, float y, bool playerTrack)
    {
        float width = SlotCount * SlotW;
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, SlotH);
        rt.anchoredPosition = new Vector2(0f, y);

        // 裁掉超出传送带的东西：牌再长也不会飞出带子
        RectMask2D mask = go.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        // 托底：一条窄的深色圆角带，只比格子高 8px。
        // 试过完全不铺（格子散在花背景上，数不清第几格）和铺满整块（读成黑玻璃板），
        // 这一版是中间值：带子自己给一道 1px 边，格子再往里收 5px。
        GameObject bed = new GameObject("Bed", typeof(RectTransform), typeof(Image));
        RectTransform brt = bed.GetComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(0f, -4f);
        brt.offsetMax = new Vector2(0f, 4f);
        Image bedImg = bed.GetComponent<Image>();
        UiSkin.Frame(bedImg, new Color(0.055f, 0.060f, 0.082f, 0.55f),
            new Color(0.72f, 0.90f, 0.86f, playerTrack ? 0.24f : 0.17f), 1, 12);
        bedImg.type = Image.Type.Sliced;
        bedImg.raycastTarget = false;

        Color cellColor = playerTrack ? new Color(0.32f, 0.58f, 0.55f, 0.20f)
            : new Color(0.58f, 0.27f, 0.33f, 0.22f);
        for (int i = 0; i < SlotCount; i++)
        {
            GameObject cell = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image));
            RectTransform crt = cell.GetComponent<RectTransform>();
            crt.SetParent(rt, false);
            crt.anchorMin = new Vector2(0f, 0.5f);
            crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(SlotW - 10f, SlotH - 10f);
            crt.anchoredPosition = new Vector2(i * SlotW + 5f, 0f);
            Image img = cell.GetComponent<Image>();
            img.sprite = CellSprite();
            img.type = Image.Type.Sliced;
            Color c = i % 2 == 0 ? cellColor : new Color(cellColor.r, cellColor.g, cellColor.b, cellColor.a * 0.50f);
            img.color = c;
            img.raycastTarget = false;
            if (playerTrack) playerCells.Add(img);
            else { enemyCells.Add(img); enemyCellBase.Add(c); }

            // 格与格之间一道 1px 分隔线：十格连成一片时，玩家没法把"第几格"数准
            GameObject div = new GameObject("Div", typeof(RectTransform), typeof(Image));
            RectTransform drt = div.GetComponent<RectTransform>();
            drt.SetParent(crt, false);
            drt.anchorMin = new Vector2(1f, 0f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(1f, 0.5f);
            drt.sizeDelta = new Vector2(1f, 0f);
            drt.anchoredPosition = new Vector2(2f, 0f);
            Image dimg = div.GetComponent<Image>();
            dimg.color = new Color(1f, 1f, 1f, 0.05f);
            dimg.raycastTarget = false;
        }

        // 「现在」这条线：2px 亮线 + 一层 16px 柔光，播放头才看得出在往前走
        GameObject glow = new GameObject("NowGlow", typeof(RectTransform), typeof(Image));
        RectTransform grt = glow.GetComponent<RectTransform>();
        grt.SetParent(rt, false);
        grt.anchorMin = new Vector2(0f, 0.5f);
        grt.anchorMax = new Vector2(0f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(16f, SlotH + 26f);
        grt.anchoredPosition = new Vector2(1f, 0f);
        Image gimg = glow.GetComponent<Image>();
        gimg.color = new Color(0.72f, 0.90f, 0.86f, 0.14f);
        gimg.raycastTarget = false;

        GameObject now = new GameObject("NowLine", typeof(RectTransform), typeof(Image));
        RectTransform nrt = now.GetComponent<RectTransform>();
        nrt.SetParent(rt, false);
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(0f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.sizeDelta = new Vector2(2f, SlotH + 22f);
        nrt.anchoredPosition = new Vector2(1f, 0f);
        Image nimg = now.GetComponent<Image>();
        nimg.color = new Color(0.86f, 1f, 0.97f, 0.92f);
        nimg.raycastTarget = false;

        return rt;
    }

    /// <summary>
    /// 全屏压角。背景是一张画得很满的机房图，四角不收一下，UI 和场景抢注意力。
    /// 贴图运行时用代码画（256×256 径向 alpha），不进 Resources、不占包体。
    /// </summary>
    private void BuildVignette()
    {
        GameObject go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = VignetteSprite();
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.85f);
    }

    private static Sprite vignetteSprite;

    private static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 0.7071f;   // 0 中心 → 1 角
                float t = Mathf.Clamp01((d - 0.45f) / 0.55f);
                byte a = (byte)(t * t * 255f);
                px[y * size + x] = new Color32(8, 7, 12, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        vignetteSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        vignetteSprite.name = "Vignette";
        return vignetteSprite;
    }

    /// <summary>
    /// HUD 的两块玻璃底板。文字在场景里（BattleSystem 摆的），这里只补"它们坐在什么上面"。
    /// 底板挂在 HUD 文字同一个父节点下、插到背景之上，所以文字仍然压在最上面。
    /// </summary>
    private void BuildHudPlates(Transform hudParent)
    {
        if (hudParent == null) return;
        Plate(hudParent, "HudPlateLeft", new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(410f, 302f), true);
        Plate(hudParent, "HudPlateRight", new Vector2(1f, 1f), new Vector2(-40f, -26f), new Vector2(400f, 196f), false);
    }

    private static void Plate(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, bool accentLeft)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        UiSkin.Frame(img, PlateFill, PlateEdge, 1, 18);
        img.raycastTarget = false;

        // 一道薄荷竖条：把"这是玩家状态"和背景分开，成本几乎为零，但立刻不像裸字
        GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        RectTransform brt = bar.GetComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = new Vector2(accentLeft ? 0f : 1f, 0.5f);
        brt.anchorMax = new Vector2(accentLeft ? 0f : 1f, 0.5f);
        brt.pivot = new Vector2(accentLeft ? 0f : 1f, 0.5f);
        brt.sizeDelta = new Vector2(3f, size.y - 48f);
        brt.anchoredPosition = new Vector2(accentLeft ? 9f : -9f, 0f);
        Image bimg = bar.GetComponent<Image>();
        bimg.color = new Color(0.72f, 0.90f, 0.86f, 0.40f);
        bimg.raycastTarget = false;

        // 背景是 0 号子物体；插到 1 号，就在背景之上、所有 HUD 文字之下
        if (parent.childCount > 1) go.transform.SetSiblingIndex(1);
    }

    private void BuildTrackLabels()
    {
        // 贴着轨道左端 14px，行标和行本身要能一眼连起来
        float x = -SlotCount * SlotW * 0.5f - 14f;
        MakeText("LabelPlayer", root, "我方", 15, new Color(0.72f, 0.90f, 0.86f, 0.65f),
            TextAlignmentOptions.Right, new Vector2(x, Track1Y), new Vector2(96f, 24f));
        MakeText("LabelEnemy", root, "敌方", 15, new Color(0.86f, 0.62f, 0.66f, 0.65f),
            TextAlignmentOptions.Right, new Vector2(x, Track2Y), new Vector2(96f, 24f));
    }

    // ---------------- 血条 ----------------

    private static Sprite cellSprite;

    /// <summary>格子用的圆角块（九宫格）。32×32 就够，切边 6px。</summary>
    private static Sprite CellSprite()
    {
        if (cellSprite != null) return cellSprite;
        cellSprite = UiSkin.FrameSprite(Color.white, new Color(1f, 1f, 1f, 0f), 0, 6);
        return cellSprite;
    }

    /// <summary>
    /// 血条放进 HUD 底板里，不挂在立绘脚下。
    /// 立绘是透明 PNG，脚底位置随贴图留白变化（游丝那只几乎没有"脚"），
    /// 挂上去就会浮在半空；放进 HUD 里则是永远贴着数字。
    /// </summary>
    private void BuildHpBars(Transform hudParent)
    {
        if (hudParent == null) return;
        playerBar = MakeHpBar("PlayerHpBar", hudParent, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(72f, -124f), new Vector2(330f, 8f), BarPlayer);
        enemyBar = MakeHpBar("EnemyHpBar", hudParent, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-72f, -124f), new Vector2(330f, 8f), BarEnemy);
    }

    private HpBar MakeHpBar(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size, Color fillColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        GameObject backGo = new GameObject("Back", typeof(RectTransform), typeof(Image));
        RectTransform brt = backGo.GetComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        Image back = backGo.GetComponent<Image>();
        UiSkin.Frame(back, BarBack, new Color(0.72f, 0.90f, 0.86f, 0.14f), 1, 5);
        back.raycastTarget = false;

        HpBar bar = new HpBar();
        bar.rt = rt;
        bar.ghost = NewBarImage(rt, "Ghost", BarGhost);
        bar.fill = NewBarImage(rt, "Fill", fillColor);
        return bar;
    }

    private static Image NewBarImage(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(2f, 0f);
        rt.sizeDelta = new Vector2(parent.sizeDelta.x - 4f, -4f);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void ApplyFill(Image img, RectTransform parentRt, float ratio)
    {
        if (img == null || parentRt == null) return;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(2f, 0f);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, (parentRt.sizeDelta.x - 4f) * Mathf.Clamp01(ratio)), -4f);
    }

    private static void TweenGhost(HpBar bar, float ratio)
    {
        DOTween.To(() => bar.ghostRatio, v =>
        {
            bar.ghostRatio = v;
            ApplyFill(bar.ghost, bar.rt, v);
        }, ratio, 0.55f).SetDelay(0.12f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    /// <summary>
    /// 立绘底下的细血条。HUD 里已经有数字，但数字要读；一条 300px 的条能一眼看出"还早"还是"要死"。
    /// 掉血时先留一截红影再收（ghost），玩家才看得见这一下掉了多少。
    /// </summary>
    public void SetHpBars(int playerHp, int playerMax, int enemyHp, int enemyMax)
    {
        if (playerBar != null)
        {
            float r = playerMax > 0 ? Mathf.Clamp01((float)playerHp / playerMax) : 1f;
            if (lastPlayerRatio < 0f || Mathf.Abs(r - lastPlayerRatio) > 0.0005f)
            {
                bool first = lastPlayerRatio < 0f;
                lastPlayerRatio = r;
                ApplyFill(playerBar.fill, playerBar.rt, r);
                if (first)
                {
                    playerBar.ghostRatio = r;
                    ApplyFill(playerBar.ghost, playerBar.rt, r);
                }
                else TweenGhost(playerBar, r);
            }
        }

        if (enemyBar != null)
        {
            float r = enemyMax > 0 ? Mathf.Clamp01((float)enemyHp / enemyMax) : 1f;
            if (lastEnemyRatio < 0f || Mathf.Abs(r - lastEnemyRatio) > 0.0005f)
            {
                bool first = lastEnemyRatio < 0f;
                lastEnemyRatio = r;
                ApplyFill(enemyBar.fill, enemyBar.rt, r);
                if (first)
                {
                    enemyBar.ghostRatio = r;
                    ApplyFill(enemyBar.ghost, enemyBar.rt, r);
                }
                else TweenGhost(enemyBar, r);
            }
        }
    }

    private RectTransform BuildHandRow()
    {
        GameObject go = new GameObject("HandRow", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotCount * CardW, CardH);
        rt.anchoredPosition = new Vector2(0f, HandY);
        return rt;
    }

    private Image BuildActor(string name, Sprite sprite, float x, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;          // 场景里那个 alpha 是 0，新建的这个必须显式给白
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;

        // 按贴图宽高比算尺寸：绝不拉伸
        float aspect = 1f;
        if (sprite != null && sprite.rect.height > 0f) aspect = sprite.rect.width / sprite.rect.height;
        float h = ActorMaxHeight;
        float w = h * aspect;
        if (w > ActorMaxHeight * 1.15f) { w = ActorMaxHeight * 1.15f; h = w / aspect; }
        rt.sizeDelta = new Vector2(w, h);

        // 呆立动画：有烘焙帧就翻页。敌人是生根的弯+行波（软体），
        // 玩家是绕脚下的呼吸+重心摆+头部滞后（刚体变换，不弯，所以不会读成"掉水里"）。
        // 敌人 512 格 / 4x3 / 7fps；玩家 384 格 / 6x4 / 12fps（24 帧，否则慢速正弦会抖成台阶）。
        bool isEnemy = name == "EnemyActor";
        string animKey = ActorAnimKey(sprite);
        if (animKey != null)
        {
            if (isEnemy) ActorAnimator.Attach(img, animKey, true, 512, 4, 3, 7f);
            else ActorAnimator.Attach(img, animKey, false, 384, 6, 4, 12f);
        }
        return img;
    }

    /// <summary>贴图名 → Resources/Art/EnemyAnim 下的动画表名。没表就返回 null（只做果冻）。</summary>
    private static string ActorAnimKey(Sprite sprite)
    {
        if (sprite == null) return null;
        switch (sprite.name)
        {
            case "史莱姆": return "slime";
            case "羊巨人": return "giant";
            case "图片替换 (14)": return "lord";
            // 玩家：Resources 里的图名就是 {key}_battle
            case "qingdao_battle": return "qingdao";
            case "jiepou_battle": return "jiepou";
        }
        // Resources 里的敌人图直接用敌人 id 命名（slime/eel/giant/moth/tank/lord），
        // 有对应的帧表就直接当动画表名用。
        if (EnemyArt.HasSheet(sprite.name)) return sprite.name;
        return null;
    }

    private static void Breathe(RectTransform rt, float amount, float seconds)
    {
        rt.localScale = Vector3.one;
        rt.DOScale(amount, seconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, string content, int size, Color color,
        TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        TextMeshProUGUI text;
        if (textTemplate != null)
        {
            GameObject clone = Instantiate(textTemplate.gameObject, parent, false);
            clone.name = name;
            text = clone.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;

        text.text = content;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // ---------------- 手牌 ----------------

    public void SetHand(CombatSim sim, bool instant)
    {
        string sig = "";
        for (int i = 0; i < sim.Hand.Count; i++) sig += sim.Hand[i].Id + ",";
        if (sig == handSignature && cards.Count == sim.Hand.Count)
        {
            RefreshAffordable(sim);
            return;
        }
        handSignature = sig;

        while (cards.Count < sim.Hand.Count) cards.Add(CreateCardSlot());
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < sim.Hand.Count;
            cards[i].rt.gameObject.SetActive(used);
            if (!used) continue;
            ApplyCard(cards[i], sim.Hand[i]);
        }

        LayoutHand(sim.Hand.Count, instant);
        RefreshAffordable(sim);
    }

    private CardSlot CreateCardSlot()
    {
        GameObject go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(CanvasGroup));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(handRow, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);

        Image bg = go.GetComponent<Image>();
        bg.raycastTarget = true;
        bg.preserveAspect = false;

        CardSlot slot = new CardSlot();
        slot.rt = rt;
        slot.bg = bg;
        slot.button = go.GetComponent<Button>();
        slot.group = go.GetComponent<CanvasGroup>();

        GameObject costGo = new GameObject("Cost", typeof(RectTransform), typeof(Image));
        RectTransform crt = costGo.GetComponent<RectTransform>();
        crt.SetParent(rt, false);
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(38f, 38f);
        crt.anchoredPosition = new Vector2(26f, -26f);
        Image costBg = costGo.GetComponent<Image>();
        UiSkin.Circle(costBg, Ink);
        costBg.raycastTarget = false;
        slot.costBg = costBg;
        slot.cost = MakeText("CostText", costGo.transform, "1", 22, new Color(0.10f, 0.08f, 0.14f),
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(38f, 38f));

        // 顶部一条类型色：不看字也能分出"这五张里哪几张是防守"。
        GameObject barGo = new GameObject("KindBar", typeof(RectTransform), typeof(Image));
        RectTransform brt = barGo.GetComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = new Vector2(0.5f, 1f);
        brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(CardW - 16f, 5f);
        brt.anchoredPosition = new Vector2(0f, -7f);
        slot.kindBar = barGo.GetComponent<Image>();
        slot.kindBar.raycastTarget = false;

        // 强化牌的小角标：手牌里 5 张挤在一起，名字末尾那个 "+" 很容易被忽略。
        // 常驻建出来，没强化就 SetActive(false) —— 手牌每帧不重建，开关比反复建节点便宜。
        GameObject badgeGo = new GameObject("UpgradeBadge", typeof(RectTransform), typeof(Image));
        RectTransform ubrt = badgeGo.GetComponent<RectTransform>();
        ubrt.SetParent(rt, false);
        ubrt.anchorMin = new Vector2(0.5f, 1f);
        ubrt.anchorMax = new Vector2(0.5f, 1f);
        ubrt.pivot = new Vector2(1f, 0.5f);
        ubrt.sizeDelta = new Vector2(44f, 15f);
        // -44：类型标签占 17~35、名字从 -76 起、卡面图标顶边约 -57，这条 36~51 的空带正好塞得下
        ubrt.anchoredPosition = new Vector2(CardW * 0.5f - 9f, -44f);
        Image badgeBg = badgeGo.GetComponent<Image>();
        UiSkin.Frame(badgeBg, new Color(0.42f, 0.86f, 0.76f, 0.96f), new Color(0.06f, 0.10f, 0.11f, 0.5f), 1, 7);
        badgeBg.raycastTarget = false;
        MakeText("Text", badgeGo.transform, "强化", 10, new Color(0.07f, 0.09f, 0.11f),
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(44f, 15f));
        slot.upgradeBadge = badgeGo;

        slot.kindTag = MakeText("KindTag", rt, "", 12, Mint, TextAlignmentOptions.Right,
            new Vector2(0f, -26f), new Vector2(CardW - 56f, 18f));
        slot.kindTag.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        slot.kindTag.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        slot.kindTag.rectTransform.pivot = new Vector2(1f, 0.5f);
        slot.kindTag.rectTransform.anchoredPosition = new Vector2(CardW * 0.5f - 9f, -26f);

        slot.name = MakeText("Name", rt, "", 21, Ink, TextAlignmentOptions.Center,
            new Vector2(0f, -76f), new Vector2(CardW - 10f, 28f));
        slot.hint = MakeText("Hint", rt, "", 13, Dim, TextAlignmentOptions.Center,
            new Vector2(0f, -100f), new Vector2(CardW - 8f, 40f));

        CombatCardHover hover = go.AddComponent<CombatCardHover>();
        hover.rt = rt;

        int captured = -1;
        slot.button.onClick.AddListener(() => { if (captured >= 0 && CardClicked != null) CardClicked(captured); });
        slot.capture = c => captured = c;
        return slot;
    }

    private void ApplyCard(CardSlot slot, CardDef def)
    {
        Sprite face = Resources.Load<Sprite>("Art/Cards/" + CardLibrary.BaseId(def.Id));
        if (face != null) slot.bg.sprite = face;
        slot.bg.color = Color.white;
        slot.name.text = def.Name;
        slot.hint.text = HintFor(def);
        slot.cost.text = def.Cost.ToString();

        // 类型配色：色条 + 右上角标签 + 名字 + 费用圆，四处同一个色调，扫一眼就知道该不该留。
        Color kind = CardPalette.Of(def.Kind);
        if (slot.kindBar != null) slot.kindBar.color = kind;
        if (slot.kindTag != null)
        {
            slot.kindTag.text = CardPalette.Label(def.Kind);
            slot.kindTag.color = new Color(kind.r, kind.g, kind.b, 0.92f);
        }
        slot.name.color = new Color(
            Mathf.Lerp(kind.r, 0.953f, 0.55f),
            Mathf.Lerp(kind.g, 0.937f, 0.55f),
            Mathf.Lerp(kind.b, 0.902f, 0.55f), 1f);
        if (slot.cost != null)
        {
            slot.cost.text = def.Cost.ToString();
            slot.cost.color = CardPalette.OnKind(def.Kind);
        }
        if (slot.costBg != null) slot.costBg.color = kind;

        if (slot.upgradeBadge != null) slot.upgradeBadge.SetActive(CardLibrary.IsUpgraded(def.Id));
    }

    private static string HintFor(CardDef def)
    {
        string head = def.Duration + " 格 · ";
        string text;
        switch (def.Kind)
        {
            case CardKind.Guard: text = head + "格挡 " + def.Block; break;
            case CardKind.Stagger: text = head + "打断"; break;
            case CardKind.Heal: text = head + "回复 " + def.Heal; break;
            case CardKind.Draw:
                text = head + "抽 " + def.Draw;
                if (def.EnergyGain > 0) text += " · 回 " + def.EnergyGain + " 灵能";
                if (def.SelfDamage > 0) text += "\n自伤 " + def.SelfDamage;
                return text;
            default:
                text = def.HitsPerTick > 0
                    ? head + "每格伤害 " + def.Damage
                    : head + "伤害 " + def.Damage;
                if (def.SelfDamage > 0) text += "\n自伤 " + def.SelfDamage;
                break;
        }

        // 回收牌要把"这张牌跟你攒的弹药是什么关系"写在卡面上，否则玩家不知道它是清算牌。
        if (def.GaugeDamagePercent > 0)
        {
            text += "\n回收值 ×" + (def.GaugeDamagePercent / 100f).ToString("0.0")
                + (def.ConsumeGauge ? " · 清空" : " · 保留");
        }
        return text;
    }

    private void LayoutHand(int count, bool instant)
    {
        float total = count * CardW + Mathf.Max(0, count - 1) * CardGap;
        for (int i = 0; i < count; i++)
        {
            CardSlot slot = cards[i];
            float x = -total * 0.5f + CardW * 0.5f + i * (CardW + CardGap);
            Vector2 target = new Vector2(x, 0f);
            slot.capture(i);
            if (instant)
            {
                slot.rt.anchoredPosition = target;
                slot.rt.localScale = Vector3.one;
            }
            else
            {
                slot.rt.DOKill();
                slot.rt.anchoredPosition = target;
                slot.rt.localScale = new Vector3(0.9f, 0.9f, 1f);
                slot.rt.DOScale(1f, 0.16f).SetEase(Ease.OutBack);
            }
        }
    }

    public void SetSpeedLabel(string label)
    {
        if (speedText != null) speedText.text = label;
    }

    private void RefreshAffordable(CombatSim sim)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (!cards[i].rt.gameObject.activeSelf) continue;
            bool ok = i < sim.Hand.Count && sim.Hand[i].Cost <= sim.Energy && !sim.IsOver;
            cards[i].group.alpha = ok ? 1f : 0.42f;
            cards[i].button.interactable = ok;
        }
    }

    // ---------------- 时间轴 ----------------

    public void SyncTimeline(CombatSim sim)
    {
        FillTrack(playerActs, trackPlayer, sim, ActOwner.Player);
        FillTrack(enemyActs, trackEnemy, sim, ActOwner.Enemy);
        SyncWarn(sim);
        HighlightHitCells(sim);
    }

    /// <summary>
    /// 敌人下一击落在哪几格，就在敌方轨道的格子上点亮。
    /// 头顶那行「N 格后」是给"还要多久"，格子高亮是给"落在哪"——两件事，缺一件都得自己数。
    /// </summary>
    private void HighlightHitCells(CombatSim sim)
    {
        int start = -1;
        int len = 0;
        if (sim != null && !sim.IsOver)
        {
            int best = int.MaxValue;
            for (int i = 0; i < sim.Acts.Count; i++)
            {
                Act a = sim.Acts[i];
                if (a.Owner != ActOwner.Enemy || a.Resolved || a.Cancelled) continue;
                int offset = a.Start - sim.Tick;
                if (offset < 0) offset = 0;
                if (offset < best)
                {
                    best = offset;
                    start = offset;
                    len = a.End - a.Start + 1;
                }
            }
        }

        if (len < 1) len = 1;
        for (int i = 0; i < enemyCells.Count; i++)
        {
            bool hot = start >= 0 && start < SlotCount && i >= start && i < start + len;
            enemyCells[i].color = hot ? CellHot : enemyCellBase[i];
        }
    }

    /// <summary>
    /// 敌人下一击还有几格、多少伤害。时间轴上也能看出距离，但数格子是负担，
    /// 这行字是把"数格子"这一步替玩家做了。
    /// </summary>
    private void SyncWarn(CombatSim sim)
    {
        if (warn == null) return;
        if (sim == null || sim.IsOver) { warn.text = ""; return; }

        int best = int.MaxValue;
        int damage = 0;
        string name = null;
        for (int i = 0; i < sim.Acts.Count; i++)
        {
            Act a = sim.Acts[i];
            if (a.Owner != ActOwner.Enemy || a.Resolved || a.Cancelled) continue;
            int offset = a.Start - sim.Tick;
            if (offset < 0) offset = 0;
            if (offset < best) { best = offset; damage = a.Damage; name = a.Name; }
        }

        if (name == null) { warn.text = ""; return; }

        if (best == 0)
        {
            warn.color = Red;
            warn.text = name + " 命中 · " + damage + " 伤害";
        }
        else
        {
            warn.color = best <= 2 ? Red : Mint;
            warn.text = best + " 格后 · " + name + " " + damage + " 伤害";
        }
    }

    private void FillTrack(List<ActSlot> pool, RectTransform track, CombatSim sim, ActOwner owner)
    {
        float width = SlotCount * SlotW;
        int used = 0;
        for (int i = 0; i < sim.Acts.Count; i++)
        {
            Act a = sim.Acts[i];
            if (a.Owner != owner) continue;
            if (a.Resolved || a.Cancelled) continue;

            int offset = a.Start - sim.Tick;
            if (offset + (a.End - a.Start + 1) <= 0) continue;   // 整块已经流过去了

            if (used >= pool.Count) pool.Add(CreateActSlot(track));
            ActSlot slot = pool[used++];
            slot.rt.gameObject.SetActive(true);

            float bw = (a.End - a.Start + 1) * SlotW - 4f;
            slot.rt.sizeDelta = new Vector2(bw, SlotH - 6f);
            slot.rt.anchoredPosition = new Vector2(-width * 0.5f + offset * SlotW + bw * 0.5f + 2f, 0f);

            // 已占格的方块要是"亮的"，空格才是暗的 —— 明暗一眼就能分出"这格有没有牌"。
            // 之前方块也是深色，和空槽一个明度，玩家得读字才知道那格被占了。
            if (owner == ActOwner.Player)
            {
                slot.bg.color = a.Kind == CardKind.Stagger ? new Color(0.88f, 0.76f, 0.42f, 0.98f)
                    : a.Kind == CardKind.Guard ? new Color(0.36f, 0.72f, 0.70f, 0.98f)
                    : new Color(0.50f, 0.88f, 0.80f, 0.98f);
                slot.label.text = a.Name;
            }
            else
            {
                slot.bg.color = a.Armor ? new Color(0.94f, 0.76f, 0.38f, 0.98f)
                    : new Color(0.88f, 0.36f, 0.44f, 0.98f);
                slot.label.text = a.Name + " " + a.Damage;
            }
            slot.label.color = BlockInk;
        }

        for (int i = used; i < pool.Count; i++) pool[i].rt.gameObject.SetActive(false);
    }

    private ActSlot CreateActSlot(RectTransform track)
    {
        GameObject go = new GameObject("Act", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(track, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotW - 4f, SlotH - 6f);

        Image bg = go.GetComponent<Image>();
        bg.raycastTarget = false;

        ActSlot slot = new ActSlot();
        slot.rt = rt;
        slot.bg = bg;
        slot.label = MakeText("Label", rt, "", 19, Ink, TextAlignmentOptions.Center, Vector2.zero,
            new Vector2(SlotW - 4f, SlotH - 6f));
        return slot;
    }

    // ---------------- 反馈 ----------------

    /// <summary>出牌时的整体小角度摆动。盘腿坐姿不能折身体（一折就露馅），
    /// 所以打击感放在构图层：整张图 ±1° 的转、一次挤压、边光闪一下。</summary>
    public void LeanPlayer(float degrees, float seconds)
    {
        if (playerActor == null) return;
        ActorAnimator a = playerActor.GetComponent<ActorAnimator>();
        if (a == null) return;
        a.Lean(degrees, seconds);
        a.Punch(0.035f, 0.20f);
    }

    public void FlashPlayer(Color color, bool hurt = false) { Flash(playerActor, color, hurt); }
    public void FlashEnemy(Color color, bool hurt = false) { Flash(enemyActor, color, hurt); }

    public void Flash(Image image, Color color, bool hurt = false)
    {
        if (image == null) return;
        // 有 ActorAnimator 的立绘，Update 每帧都在写 transform，外面的 tween 会被覆盖，
        // 所以挤压/抖动必须交给它叠加。
        ActorAnimator anim = image.GetComponent<ActorAnimator>();
        if (anim != null)
        {
            anim.Punch(hurt ? 0.10f : 0.055f, hurt ? 0.26f : 0.18f);
            if (hurt) anim.Shake(7f, 0.28f);
        }
        else
        {
            Transform t = image.transform;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOPunchScale(new Vector3(hurt ? 0.10f : 0.06f, hurt ? 0.10f : 0.06f, 0f), hurt ? 0.26f : 0.22f, 8, 0.6f).SetUpdate(true);
        }

        Image img = image;
        img.color = color;
        DOTween.To(() => img.color, c => img.color = c, Color.white, 0.20f).SetUpdate(true);
    }

    public void ShowFloat(Vector3 world, string content, Color color, int size = 34)
    {
        if (root == null) return;
        Vector2 local;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, world, cam, out local);
        TextMeshProUGUI text = MakeText("Float", root, content, size, color, TextAlignmentOptions.Center,
            local, new Vector2(360f, 64f));
        RectTransform rt = text.rectTransform;
        rt.SetAsLastSibling();
        rt.DOScale(1.06f, 0.1f).SetEase(Ease.OutQuad);
        text.DOFade(0f, 0.55f).SetDelay(0.45f).OnComplete(() => { if (text != null) Destroy(text.gameObject); });
        rt.DOAnchorPos(local + new Vector2(0f, 60f), 0.95f).SetEase(Ease.OutCubic);
    }

    public Vector3 PlayerWorld { get { return playerActor != null ? playerActor.transform.position : new Vector3(Screen.width * 0.3f, Screen.height * 0.55f, 0f); } }
    public Vector3 EnemyWorld { get { return enemyActor != null ? enemyActor.transform.position : new Vector3(Screen.width * 0.7f, Screen.height * 0.55f, 0f); } }

    public Image PlayerActor { get { return playerActor; } }
    public Image EnemyActor { get { return enemyActor; } }
}

/// <summary>
/// 手牌悬停。卡面是 150×224 的整张图，鼠标经过时只放大 7%——
/// 放大再多就会盖住邻牌，而这是"这张我点得动"的提示，不是展示。
/// 只 kill 自己那条 tween，免得把 LayoutHand 的入场动画一起停掉。
/// </summary>
public class CombatCardHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    public RectTransform rt;
    private Button button;
    private Tween tween;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (button != null && !button.interactable) return;
        if (rt == null) rt = transform as RectTransform;
        if (rt == null) return;
        if (tween != null) tween.Kill();
        tween = rt.DOScale(1.07f, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
    {
        if (rt == null) rt = transform as RectTransform;
        if (rt == null) return;
        if (tween != null) tween.Kill();
        tween = rt.DOScale(1f, 0.14f).SetEase(Ease.OutQuad).SetUpdate(true);
    }
}
