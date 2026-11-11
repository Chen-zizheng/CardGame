using System.Collections.Generic;
using CardCombat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴：卡牌 / 敌人 / 遗物 / 本能 / 事件 五页，全部运行时建。
///
/// 为什么做这个：卡牌效果只写在卡面小字里，敌人在不同层会换成不同招式组，
/// 遗物和天赋又都是"拿到才知道"——没有一个地方能把这套东西一次看全。
/// 图鉴是本作唯一不靠记忆的说明入口。
/// </summary>
public class CodexPanel : MonoBehaviour
{
    private static CodexPanel _instance;

    private const float PanelW = 1560f;
    private const float PanelH = 862f;

    private RectTransform _content;
    private readonly List<Button> _tabs = new List<Button>();
    private string _tab = "cards";

    private static readonly Color Ink = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color Dim = new Color(0.64f, 0.72f, 0.72f, 1f);
    private static readonly Color Mint = new Color(0.72f, 0.90f, 0.86f, 1f);

    public static void Open()
    {
        if (_instance != null) return;
        Canvas canvas = FindCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("CodexPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        _instance = go.AddComponent<CodexPanel>();
        _instance.Build(rt);
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

    private void Build(RectTransform root)
    {
        Image scrim = root.gameObject.AddComponent<Image>();
        scrim.color = new Color(0.015f, 0.015f, 0.022f, 0.94f);
        scrim.raycastTarget = true;

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(root, false);
        RectTransform panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panel.anchoredPosition = Vector2.zero;
        UiSkin.Frame(panelObj.GetComponent<Image>(),
            new Color(0.040f, 0.045f, 0.068f, 0.99f), new Color(0.72f, 0.90f, 0.86f, 0.28f), 2, 18);

        UiSkin.CreateText(panel, "Kicker", "CODEX · 深渊培育所记录", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(52f, -40f), new Vector2(700f, 24f), 15f, new Color(Mint.r, Mint.g, Mint.b, 0.75f),
            TextAlignmentOptions.MidlineLeft);

        UiSkin.CreateText(panel, "Title", "图鉴", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(50f, -64f), new Vector2(600f, 56f), 40f, Ink, TextAlignmentOptions.MidlineLeft);

        UiSkin.CreateText(panel, "Sub", "卡牌 / 敌人 / 遗物 / 本能 / 事件",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -122f), new Vector2(1100f, 26f), 16f,
            new Color(Dim.r, Dim.g, Dim.b, 0.85f), TextAlignmentOptions.MidlineLeft);

        Button close = UiSkin.CreateButton(panel, "CloseButton", "×",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(58f, 58f), 30f);
        close.onClick.AddListener(Close);

        // 五个页签
        string[] ids = { "cards", "enemies", "relics", "talents", "events" };
        int cardCount = 0;
        foreach (string cid in CardLibrary.AllIds) cardCount++;
        string[] names = { "卡牌 " + cardCount, "敌人 " + EnemyLibrary.Order.Length,
            "遗物 " + RelicLibrary.All.Length, "本能 " + TalentLibrary.All.Length,
            "事件 " + EventLibrary.All.Length };
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            Button tab = UiSkin.CreateButton(panel, "Tab_" + id, names[i],
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(52f + i * 196f, -168f), new Vector2(184f, 50f), 20f);
            tab.onClick.AddListener(() => Select(id));
            _tabs.Add(tab);
        }

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(panel, false);
        _content = contentObj.GetComponent<RectTransform>();
        _content.anchorMin = new Vector2(0.5f, 0.5f);
        _content.anchorMax = new Vector2(0.5f, 0.5f);
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.sizeDelta = new Vector2(PanelW - 96f, 560f);
        _content.anchoredPosition = new Vector2(0f, -104f);

        UiSkin.CreateText(panel, "Foot", "每场战斗结束回复已损失生命的 30% · 两职业相同",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1200f, 24f), 14f,
            new Color(Dim.r, Dim.g, Dim.b, 0.55f));

        Select("cards");
    }

    private void Select(string id)
    {
        _tab = id;
        for (int i = 0; i < _tabs.Count; i++)
        {
            Button b = _tabs[i];
            bool on = b.name == "Tab_" + id;
            UiSkin.Frame(b.GetComponent<Image>(),
                on ? new Color(0.16f, 0.30f, 0.29f, 0.95f) : new Color(0.055f, 0.052f, 0.085f, 0.85f),
                on ? new Color(Mint.r, Mint.g, Mint.b, 0.85f) : new Color(Mint.r, Mint.g, Mint.b, 0.22f), 2, 12);
        }

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        if (id == "cards") BuildCards();
        else if (id == "enemies") BuildEnemies();
        else if (id == "relics") BuildRelics();
        else if (id == "events") BuildEvents();
        else BuildTalents();
    }

    private GameObject Cell(string name, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_content, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        UiSkin.Frame(go.GetComponent<Image>(),
            new Color(0.062f, 0.066f, 0.092f, 0.85f), new Color(0.72f, 0.90f, 0.86f, 0.12f), 1, 12);
        return go;
    }

    /// <summary>
    /// 网格排布：固定列数，每行居中。
    /// 之前遗物/本能是"一行全铺开"写的 ((i - 2.5f) * 252f)，件数一超过 6 就往右挤出面板 —— 本能 11 条正好踩中。
    /// </summary>
    private static Vector2 TilePos(int index, int count, int cols, float stepX, float cellH,
        float rowGap, float centerY)
    {
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        int row = index / cols;
        int col = index % cols;
        int inRow = Mathf.Min(cols, count - row * cols);

        float x = (col - (inRow - 1) * 0.5f) * stepX;
        float rowStep = cellH + rowGap;
        float y = centerY + (rows - 1) * 0.5f * rowStep - row * rowStep;
        return new Vector2(x, y);
    }

    private static Image Art(GameObject cell, Sprite sprite, float w, float h, float topOffset)
    {
        GameObject go = new GameObject("Art", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(cell.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, -topOffset);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    // ---------------- 卡牌 ----------------

    private void BuildCards()
    {
        List<string> ids = new List<string>(CardLibrary.AllIds);
        ids.Sort();
        // 排序按牌面强度不直观，按"名字"排反而找得快
        ids.Sort(delegate (string a, string b)
        {
            CardDef da = CardLibrary.Get(a);
            CardDef db = CardLibrary.Get(b);
            int ka = da != null ? (int)da.Kind : 9;
            int kb = db != null ? (int)db.Kind : 9;
            if (ka != kb) return ka.CompareTo(kb);
            return string.Compare(a, b, System.StringComparison.Ordinal);
        });

        for (int i = 0; i < ids.Count; i++)
        {
            CardDef def = CardLibrary.Get(ids[i]);
            if (def == null) continue;

            // 一格 3 行装 24 张：8 列 × 3 行，整块在内容区（1480×560）里居中。
            int col = i % 8;
            int row = i / 8;
            GameObject cell = Cell("Card_" + def.Id, (col - 3.5f) * 178f, 186f - row * 186f, 168f, 176f);
            Color kind = CardPalette.Of(def.Kind);
            KindBar(cell, kind);
            Art(cell, UiSkin.LoadSprite("Art/Cards/" + def.Id), 84f, 126f, 12f);

            UiSkin.CreateText(cell.transform, "Name", def.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -138f), new Vector2(160f, 20f), 14f, Ink);
            UiSkin.CreateText(cell.transform, "Meta", MetaOf(def), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -154f), new Vector2(160f, 18f), 10.5f, new Color(Dim.r, Dim.g, Dim.b, 0.9f));
            UiSkin.CreateText(cell.transform, "Kind", CardPalette.Label(def.Kind),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -168f), new Vector2(160f, 16f), 9.5f,
                new Color(kind.r, kind.g, kind.b, 0.95f));

            // 格子里塞不下第二套数值，改成悬停看：强化后这张牌具体变成什么。
            IconTip.Attach(cell, def.Name, "强化后：\n" + UpgradeDelta(def));
        }
    }

    /// <summary>卡牌格顶部的类型色条，和手牌是同一个 CardPalette。</summary>
    private static void KindBar(GameObject cell, Color color)
    {
        GameObject go = new GameObject("KindBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(cell.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(148f, 4f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = color;
    }

    /// <summary>
    /// 强化只改六个数：伤害 / 格挡 / 回复 / 抽牌 / 灵能 / 自伤 / 回收倍率。
    /// 逐项对比现算的强化版，只报真的变了的项 —— 不然每张牌都写「伤害 0 → 0」。
    /// </summary>
    private static string UpgradeDelta(CardDef def)
    {
        CardDef up = CardLibrary.GetUpgraded(def.Id);
        if (up == null || up == def) return "这张牌没有强化版";

        string s = "";
        if (up.Damage != def.Damage) s += "伤害 " + def.Damage + " → " + up.Damage + "\n";
        if (up.Block != def.Block) s += "格挡 " + def.Block + " → " + up.Block + "\n";
        if (up.Heal != def.Heal) s += "回复 " + def.Heal + " → " + up.Heal + "\n";
        if (up.Draw != def.Draw) s += "抽牌 " + def.Draw + " → " + up.Draw + "\n";
        if (up.Cost != def.Cost) s += "灵能 " + def.Cost + " → " + up.Cost + "\n";
        if (up.SelfDamage != def.SelfDamage) s += "自伤 " + def.SelfDamage + " → " + up.SelfDamage + "\n";
        if (up.GaugeDamagePercent != def.GaugeDamagePercent)
            s += "回收 ×" + (def.GaugeDamagePercent / 100f).ToString("0.##") + " → ×"
                 + (up.GaugeDamagePercent / 100f).ToString("0.##") + "\n";

        return s.Length == 0 ? "这张牌没有可强化的数值" : s.TrimEnd('\n');
    }

    private static string MetaOf(CardDef def)
    {
        string s = def.Cost + " 灵能 · " + Mathf.Max(1, def.Duration) + " 格";
        if (def.HitsPerTick > 0) s += " · " + def.Damage + "×" + def.HitsPerTick;
        else if (def.Damage > 0) s += " · " + def.Damage + " 伤";
        if (def.Block > 0) s += " · " + def.Block + " 挡";
        if (def.Heal > 0) s += " · 回 " + def.Heal;
        if (def.Draw > 0) s += " · 抽 " + def.Draw;
        return s;
    }

    // ---------------- 敌人 ----------------

    private void BuildEnemies()
    {
        string[] order = EnemyLibrary.Order;
        for (int i = 0; i < order.Length; i++)
        {
            EnemyTemplate t = EnemyLibrary.Get(order[i]);
            if (t == null) continue;

            GameObject cell = Cell("Enemy_" + t.Id, (i - 2.5f) * 252f, 60f, 236f, 460f);
            Art(cell, EnemyArt.Load(t.Id), 176f, 176f, 16f);

            UiSkin.CreateText(cell.transform, "Name", t.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -200f), new Vector2(220f, 28f), 22f, Ink);
            UiSkin.CreateText(cell.transform, "Hp", "HP " + t.MaxHp + " · 二阶段 " + t.Phase2AtPercent + "%",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -230f), new Vector2(220f, 22f), 13f, new Color(Dim.r, Dim.g, Dim.b, 0.9f));
            bool variants = HasVariants(t.Id);
            UiSkin.CreateText(cell.transform, "Layers", LayersOf(t.Id) + (variants ? " · 带亚种" : ""),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -248f), new Vector2(220f, 22f), 12f, new Color(0.62f, 0.86f, 0.80f, 0.85f));

            string moves = "";
            for (int m = 0; m < t.Moves.Count; m++)
            {
                EnemyMove mv = t.Moves[m];
                if (m > 0) moves += "\n";
                moves += mv.Name + "　" + mv.Duration + " 格 · " + mv.Damage + " 伤 · 预警 " + mv.Lead;
                if (mv.Armor) moves += " · 霸体";
                if (mv.Gap > 0) moves += " · 隔 " + mv.Gap;
            }
            UiSkin.CreateText(cell.transform, "Moves", moves, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -272f), new Vector2(214f, 180f), 12.5f, new Color(Dim.r, Dim.g, Dim.b, 0.85f),
                TextAlignmentOptions.Top);

            // 亚种是第 5 层才出现的机制，格子里写不下四项倍率 —— 悬停看这只怪具体会怎么变。
            IconTip.Attach(cell, t.Name,
                "出场：" + LayersOf(t.Id) + "\n" +
                (variants ? "亚种（第 5 层起 · 普通格）：\n" + VariantLines()
                          : (t.Id == EnemyLibrary.Lord ? "首领怪不带亚种前缀" : "只在前四层出场，不带亚种前缀")));
        }

        // 两行说明：第一行是层数缩放（所有怪），第二行是亚种倍率（四只回访怪）。倍率直接从 EnemyVariants 读，不手抄。
        UiSkin.CreateText(_content, "NoteScale",
            "十层：血量 +5%/层 · 伤害 +3.5%/层；精英格再 +8% / +3%，Boss 格 +15% / +7%",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -206f), new Vector2(1400f, 22f),
            12.5f, new Color(Dim.r, Dim.g, Dim.b, 0.65f));
        UiSkin.CreateText(_content, "NoteVariant",
            "亚种前缀 = 同一只怪的变体，第 5 层起普通格才有：" + VariantLines().Replace("\n", " ／ "),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -234f), new Vector2(1400f, 22f),
            12.5f, new Color(Dim.r, Dim.g, Dim.b, 0.65f));
    }

    /// <summary>这只怪会不会被抽中亚种（第 5 层起的普通格才带前缀，首领格不带）。</summary>
    private static bool HasVariants(string id)
    {
        for (int stage = 4; stage < 10; stage++)
        {
            if (IsCampfireLayer(stage + 1)) continue;
            if (EnemyLibrary.IdForStage(stage) == id) return true;
        }
        return false;
    }

    /// <summary>四行亚种倍率，直接读 EnemyVariants，改数值时图鉴跟着变。</summary>
    private static string VariantLines()
    {
        EnemyVariant[] all = EnemyVariants.All;
        string s = "";
        for (int i = 0; i < all.Length; i++)
        {
            EnemyVariant v = all[i];
            if (i > 0) s += "\n";
            s += v.Prefix + " 血 ×" + v.HpMul.ToString("0.00") + " 伤 ×" + v.DmgMul.ToString("0.00");
            if (v.LeadTrim < 0) s += " 预警 −1 格";
            else if (v.LeadTrim > 0) s += " 预警 +1 格";
        }
        return s;
    }

    /// <summary>这一层是不是篝火（篝火层上的敌人不出手，不能算「出场」）。</summary>
    private static bool IsCampfireLayer(int layer)
    {
        // 和 MapConfig.campfireLayerIndices = { 2, 5, 8 }（0 基）对齐：第 3 / 6 / 9 层。
        return layer == 3 || layer == 6 || layer == 9;
    }

    /// <summary>这只怪在第几层真的会打（跳过篝火层，否则图鉴会写出一场永远不会发生的遭遇）。</summary>
    private static string LayersOf(string id)
    {
        string s = "";
        for (int stage = 0; stage < 10; stage++)
        {
            if (IsCampfireLayer(stage + 1)) continue;
            if (EnemyLibrary.IdForStage(stage) != id) continue;
            s += s.Length == 0 ? "L" : " / L";
            s += (stage + 1);
        }
        return s;
    }

    // ---------------- 遗物 ----------------

    private void BuildRelics()
    {
        RelicDef[] all = RelicLibrary.All;
        for (int i = 0; i < all.Length; i++)
        {
            RelicDef def = all[i];
            Vector2 pos = TilePos(i, all.Length, 6, 252f, 300f, 30f, 86f);
            GameObject cell = Cell("Relic_" + def.Id, pos.x, pos.y, 236f, 300f);
            Art(cell, UiSkin.LoadSprite("Art/Relics/" + def.Id), 104f, 104f, 18f);

            UiSkin.CreateText(cell.transform, "Name", def.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -134f), new Vector2(220f, 26f), 21f, Ink);
            UiSkin.CreateText(cell.transform, "Desc", def.Desc, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -166f), new Vector2(200f, 120f), 13.5f, new Color(Dim.r, Dim.g, Dim.b, 0.9f),
                TextAlignmentOptions.Top);
        }

        UiSkin.CreateText(_content, "Note",
            "精英与首领必掉 · 普通格 20%",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -252f), new Vector2(1300f, 24f),
            14f, new Color(Dim.r, Dim.g, Dim.b, 0.65f));
    }

    // ---------------- 天赋 ----------------

    private void BuildTalents()
    {
        for (int i = 0; i < TalentLibrary.All.Length; i++)
        {
            TalentDef def = TalentLibrary.All[i];
            Vector2 pos = TilePos(i, TalentLibrary.All.Length, 6, 248f, 244f, 26f, 40f);
            GameObject cell = Cell("Talent_" + def.Id, pos.x, pos.y, 232f, 244f);
            Art(cell, UiSkin.LoadSprite("Art/Talents/" + def.Icon), 88f, 88f, 12f);

            UiSkin.CreateText(cell.transform, "Name", def.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(212f, 26f), 21f, Ink);
            UiSkin.CreateText(cell.transform, "Desc", def.Desc, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -148f), new Vector2(200f, 92f), 13f, new Color(Dim.r, Dim.g, Dim.b, 0.9f),
                TextAlignmentOptions.Top);
        }

        UiSkin.CreateText(_content, "Note",
            "出生自带一条 · 第 3 / 6 / 9 层各长一条",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -252f), new Vector2(1300f, 24f),
            14f, new Color(Dim.r, Dim.g, Dim.b, 0.65f));
    }

    // ---------------- 事件 ----------------

    /// <summary>
    /// 事件是"一段话 + 三个带代价的选项"，所以不用格子而是整行：
    /// 左边是谁、中间发生了什么事、右边三个选项各自要付什么。
    /// 玩家在事件里是当场做决定的，事先能查清"这个选项会不会拿走我的最大生命"才叫公平。
    /// </summary>
    private void BuildEvents()
    {
        // 事件从 10 条长到 14 条之后，两列 × 七行会掉出内容区（这个坑踩过两次），
        // 所以列数跟着条数走：≤10 条两列，超过就三列。格子高度也跟着压缩。
        EventDef[] all = EventLibrary.All;
        bool compact = all.Length > 10;
        int cols = compact ? 3 : 2;
        int rows = (all.Length + cols - 1) / cols;
        float cellW = compact ? 452f : 700f;
        float cellH = compact ? 100f : 96f;
        float stepX = compact ? 468f : 704f;
        float pitch = compact ? 110f : 98f;
        float centerY = compact ? 8f : 0f;

        for (int i = 0; i < all.Length; i++)
        {
            EventDef ev = all[i];
            int col = i / rows;          // 先填满一列再填下一列，读起来是"从上往下"
            int row = i % rows;
            float x = (col - (cols - 1) * 0.5f) * stepX;
            float y = centerY + (rows - 1) * 0.5f * pitch - row * pitch;
            GameObject cell = Cell("Event_" + ev.Id, x, y, cellW, cellH);

            UiSkin.CreateText(cell.transform, "Kicker", ev.Kicker, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, compact ? -6f : -8f), new Vector2(cellW - 28f, 14f), compact ? 9f : 9.5f,
                new Color(Mint.r, Mint.g, Mint.b, 0.65f), TextAlignmentOptions.MidlineLeft);
            UiSkin.CreateText(cell.transform, "Title", ev.Title, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, compact ? -20f : -26f), new Vector2(cellW - 28f, compact ? 22f : 44f),
                compact ? 13f : 14.5f, Ink, TextAlignmentOptions.TopLeft);

            for (int k = 0; k < ev.Choices.Length; k++)
            {
                EventChoice ch = ev.Choices[k];
                // 主文 + 代价挤在一行（代价用 TMP 富文本压小压灰）。
                // 早先拆成两行时，主文和它下面的代价在这么矮的行里必然糊在一起。
                string line = ch.Label + "  <color=#B4C6C6><size=" + (compact ? "8.5" : "9.5") + ">"
                    + ch.Hint + "</size></color>";
                UiSkin.CreateText(cell.transform, "Choice" + k, line,
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(14f, -(compact ? 44f + k * 19f : 6f + k * 28f)),
                    new Vector2(cellW - 28f, compact ? 18f : 26f), compact ? 9.5f : 11f,
                    Ink, TextAlignmentOptions.TopLeft);
            }

            // 整行悬停看气氛文字：事件的价值一半在"那里发生了什么"，但那一半不该占掉选项的位置。
            IconTip.Attach(cell, ev.Title, ev.Body.Replace("\n", " "));
        }

        UiSkin.CreateText(_content, "Note",
            "问号格才会读到 · 同一局不重复 · " + EventLibrary.All.Length + " 个都读过后才会再抽到看过的",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -272f), new Vector2(1400f, 22f),
            13.5f, new Color(Dim.r, Dim.g, Dim.b, 0.65f));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Close()
    {
        _instance = null;
        Destroy(gameObject);
    }
}
