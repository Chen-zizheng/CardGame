using CardCombat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 主菜单与选人界面的版式（1920×1080 参考分辨率）。
// 坐标写在代码里，因为场景里的 MenuPanel 被存成了 scale 3.37 + 旋转 41°，
// 在编辑器里拖不回来（编辑器在我这边起不来），只能让代码在启动时把它掰正。
//
// 风格取向（这一轮改）：杀戮尖塔那种"暗色石板 + 细边 + 大字距"的界面，
// 而不是米白色块胶囊 —— 之前按钮既丑又缺角，是九宫格边框大于控件高度被压出来的。
public static class MainMenuLayout
{
    private static readonly Color Ink = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color Dim = new Color(0.64f, 0.72f, 0.72f, 1f);

    public static void Apply()
    {
        GameObject panel = GameObject.Find("MenuPanel");
        if (panel == null) return;

        RectTransform pr = panel.GetComponent<RectTransform>();
        if (pr != null)
        {
            pr.localScale = Vector3.one;
            pr.localRotation = Quaternion.identity;
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;
        }

        StyleTitle("TitleText");
        BuildTitleLockup(panel.transform);

        // 有存档就把"继续"顶在上面一格：继续在上、开始在下，是从上到下的阅读顺序，
        // 也符合"上一局还没结束"这件事的分量。
        bool hasSave = SaveSystem.Exists();
        StyleButton("StartButton", 52f, hasSave ? "开始新的一局" : "开始游戏");
        StyleButton("SettingsButton", -112f, "设置");
        StyleButton("QuitButton", -194f, "退出");

        BuildCodexButton(panel.transform);

        if (hasSave) BuildContinueButton(panel.transform);

        CreateCredit(panel.transform);
    }

    /// <summary>
    /// 标题不再是一个横着躺的大单词。
    /// 现在的结构是：一行小字距的英文标签 → 一条薄荷细线 → 中文大字标题 → 一句副标。
    /// 主标题两个字，靠字距撑开（characterSpacing 46），比拉大字号的英文耐看。
    /// </summary>
    private static void BuildTitleLockup(Transform parent)
    {
        if (parent.Find("TitleKicker") != null) return;

        UiSkin.CreateText(parent, "TitleKicker", "处置科 · 外勤记录 CT-09",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 344f), new Vector2(900f, 30f),
            15f, new Color(Dim.r, Dim.g, Dim.b, 0.8f));

        GameObject rule = new GameObject("TitleRule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(parent, false);
        RectTransform rr = rule.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(96f, 2f);
        rr.anchoredPosition = new Vector2(0f, 312f);
        Image ri = rule.GetComponent<Image>();
        ri.color = new Color(0.72f, 0.90f, 0.86f, 0.85f);
        ri.raycastTarget = false;

        UiSkin.CreateText(parent, "TitleSub", "深渊培育所 · 一层一层往下清场",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 206f), new Vector2(900f, 34f),
            19f, new Color(Dim.r, Dim.g, Dim.b, 0.9f));
    }

    private static void StyleTitle(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = "牌局";
            text.fontSize = 96f;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Ink;
            text.characterSpacing = 46f;
            text.raycastTarget = false;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, 180f);
            rt.anchoredPosition = new Vector2(0f, 262f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }

    private static void StyleButton(string name, float y, string label)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 68f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        Button btn = go.GetComponent<Button>();
        if (btn != null) UiSkin.StyleButton(btn, 24f);

        TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = label;
            text.characterSpacing = 4f;
        }
    }

    /// <summary>
    /// 图鉴入口。跟"继续游戏"一样是运行时建的——场景里没有这个物体。
    /// 摆在开始与设置之间：它只是"读"，既不推进本局也不改设置。
    /// </summary>
    private static void BuildCodexButton(Transform panel)
    {
        Transform old = panel.Find("CodexButton");
        if (old != null)
        {
            old.gameObject.SetActive(true);
            return;
        }

        Button btn = UiSkin.CreateButton(panel, "CodexButton", "图鉴",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(320f, 68f), 24f);
        btn.onClick.AddListener(() => CodexPanel.Open());
    }

    /// <summary>
    /// "继续游戏"按钮 + 一行进度摘要。
    /// 按钮是运行时建的，因为场景里没有这个物体，而场景文件我不能改（团结引擎的 .meta guid 是加密的）。
    /// </summary>
    private static void BuildContinueButton(Transform panel)
    {
        Transform old = panel.Find("ContinueButton");
        if (old != null) old.gameObject.SetActive(true);
        else
        {
            Button btn = UiSkin.CreateButton(panel, "ContinueButton", "继续游戏",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 134f), new Vector2(320f, 68f), 24f);
            btn.onClick.AddListener(() =>
            {
                MainMenuController c = Object.FindObjectOfType<MainMenuController>();
                if (c != null) c.ContinueGame();
            });
        }

        SaveFile f = SaveSystem.Peek();
        string cap = f == null ? "" : string.Format("上次　{0}　第 {1} 层　HP {2}/{3}　{4} 张牌",
            string.IsNullOrEmpty(f.characterName) ? "处置员" : f.characterName,
            Mathf.Max(1, f.stageIndex), f.hp, Mathf.Max(1, f.maxHp),
            f.deck != null ? f.deck.Count : 0);

        Transform oldCap = panel.Find("SaveCaption");
        if (oldCap != null)
        {
            oldCap.gameObject.SetActive(true);
            TextMeshProUGUI t = oldCap.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = cap;
            return;
        }

        UiSkin.CreateText(panel, "SaveCaption", cap,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -258f), new Vector2(1000f, 30f),
            16f, new Color(Dim.r, Dim.g, Dim.b, 0.72f));
    }

    // 主菜单底部那行署名（game-icons.net 是 CC BY 3.0，必须写出来）
    private static void CreateCredit(Transform parent)
    {
        if (parent.Find("CreditLine") != null) return;

        UiSkin.CreateText(parent, "CreditLine", "v0.1  ·  美术致谢 game-icons.net (CC BY 3.0)",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 30f),
            16f, new Color(Dim.r, Dim.g, Dim.b, 0.55f));
    }
}

public static class CharacterSelectLayout
{
    private static readonly Color Ink = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color Dim = new Color(0.64f, 0.72f, 0.72f, 1f);
    private static readonly Color Mint = new Color(0.72f, 0.90f, 0.86f, 1f);

    private const float CardW = 500f;
    private const float CardH = 820f;
    private const float ArtH = 470f;

    // 两张卡不居中，靠左排：玩家要的是"人在左边、信息在卡上"，不是两块板正对屏幕中线。
    private const float LeftMargin = 70f;
    private const float CardGap = 40f;

    public static void Apply(CharacterData warrior, CharacterData ranger)
    {
        GameObject title = GameObject.Find("TitleText");
        if (title != null)
        {
            TextMeshProUGUI t = title.GetComponent<TextMeshProUGUI>();
            if (t != null)
            {
                t.text = "选择处置员";
                t.fontSize = 42f;
                t.enableAutoSizing = false;
                t.alignment = TextAlignmentOptions.Center;
                t.color = Ink;
                t.characterSpacing = 16f;
                t.raycastTarget = false;
            }
            RectTransform rt = title.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(1000f, 66f);
                rt.anchoredPosition = new Vector2(0f, -44f);
            }
        }

        GameObject background = GameObject.Find("background");
        if (background != null)
        {
            RectTransform rt = background.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            Image img = background.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0f);
        }

        // 卡片不能留在场景原来的容器里：CharacterContainer 不是满屏的（它在一个小面板里、带缩放，
        // 而且上面挂着 HorizontalLayoutGroup，每次 Layout 都会把卡的位置和尺寸改回去），
        // 结果就是两张卡整体左移、左边那张被切出屏幕、卡还被放大 —— 2026-11 玩家截图里就是这个。
        // 所以这里另建一个满屏、无 LayoutGroup 的根节点，把两张卡搬进来，坐标才说了算。
        RectTransform root = EnsureSelectRoot();
        Transform cardParent = root != null ? (Transform)root : null;
        if (root == null) Debug.LogWarning("[选人] 没找到 Canvas，选人卡会留在场景原来的容器里，位置可能不对");

        if (cardParent != null)
        {
            Reparent(cardParent, "WarriorButton");
            Reparent(cardParent, "RangerButton");

            // 标题下面那行"两张卡选一个 · …"删掉了：它压在立绘上沿，而且玩家已经说了不需要。
            Transform staleHint = cardParent.Find("SelectHint");
            if (staleHint != null) Object.Destroy(staleHint.gameObject);
            foreach (string cardName in new[] { "WarriorButton", "RangerButton" })
            {
                Transform c = cardParent.Find(cardName);
                Transform staleTag = c != null ? c.Find("Tagline") : null;
                if (staleTag != null) Object.Destroy(staleTag.gameObject);
            }
        }

        // 靠左排：第一张卡的左边缘离屏幕左沿 LeftMargin，两张卡间距 CardGap。
        float firstCenter = LeftMargin + CardW * 0.5f - 960f;
        StyleCard("WarriorButton", warrior, firstCenter);
        StyleCard("RangerButton", ranger, firstCenter + CardW + CardGap);
    }

    /// <summary>
    /// 满屏、不带 LayoutGroup 的选人根节点。挂在最上层的 ScreenSpaceOverlay Canvas 上，
    /// 这样两张卡的 anchoredPosition 就是相对屏幕中心的，不会被场景容器改写。
    /// </summary>
    private static RectTransform EnsureSelectRoot()
    {
        Canvas target = null;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { target = c; break; }
        }
        if (target == null && canvases.Length > 0) target = canvases[0];
        if (target == null) return null;

        Transform existing = target.transform.Find("SelectRoot");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("SelectRoot", typeof(RectTransform));
            go.transform.SetParent(target.transform, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        go.transform.SetAsLastSibling();
        return rt;
    }

    private static void Reparent(Transform parent, string childName)
    {
        GameObject child = GameObject.Find(childName);
        if (child == null || child.transform.parent == parent) return;
        // worldPositionStays=false：不要用世界坐标换算，直接把 localPosition 交给后面的 StyleCard 设。
        child.transform.SetParent(parent, false);
    }

    private static void StyleCard(string name, CharacterData data, float x)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardW, CardH);
            rt.anchoredPosition = new Vector2(x, -20f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        // 整张卡的底板：暗石板 + 薄荷细边
        Image slab = go.GetComponent<Image>();
        if (slab != null)
        {
            UiSkin.Frame(slab, new Color(0.048f, 0.046f, 0.072f, 0.90f),
                new Color(Mint.r, Mint.g, Mint.b, 0.36f), 2, 8);
        }

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.82f, 0.86f, 0.86f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.transition = Selectable.Transition.ColorTint;
        }

        // 卡片里原来那几块没贴图的灰底 Image 全部压透明
        foreach (Image child in go.GetComponentsInChildren<Image>(true))
        {
            if (child == slab) continue;
            if (child.gameObject.name == "ArtFrame" || child.gameObject.name == "Portrait") continue;
            if (child.gameObject.name == "Plate" || child.gameObject.name == "Scrim") continue;
            if (child.gameObject.name == "Divider" || child.gameObject.name == "TextPlate") continue;
            if (child.gameObject.name == "InstinctPlate" || child.gameObject.name == "KindEdge") continue;
            if (child.sprite == null) child.color = new Color(1f, 1f, 1f, 0f);
        }

        BuildArtFrame(go.transform, data);
        BuildTextPlate(go.transform, "TextPlate");
        BuildDivider(go.transform, "Divider");

        // 名字与被动说明
        foreach (TextMeshProUGUI text in go.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.gameObject.name == "DescText")
            {
                // 文案不再写死在场景里：角色 asset 里叫什么，这里就显示什么。
                if (data != null)
                {
                    string body = "<b>被动 · " + (string.IsNullOrEmpty(data.passiveName) ? "无" : data.passiveName) + "</b>";
                    if (!string.IsNullOrEmpty(data.passiveDesc)) body += "\n" + data.passiveDesc;
                    text.text = body;
                }

                text.fontSize = 13f;
                text.enableAutoSizing = false;
                text.enableWordWrapping = true;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.color = new Color(0.80f, 0.86f, 0.85f, 1f);
                text.lineSpacing = 3f;
                SetRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(28f, 92f), new Vector2(CardW - 56f, 64f));
            }
            else
            {
                // 名字压到立绘下沿上（左侧对齐），这是卡牌游戏里"人物名"该在的位置。
                // 玩家说"名字太暗了，套个颜色亮一些"——现在是薄荷偏白的亮色，加一点外发光，
                // 底下再垫一圈柔光和浮尘，那块地方就有生气了。
                text.fontSize = 42f;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.BottomLeft;
                text.color = new Color(0.82f, 1f, 0.95f, 1f);
                text.characterSpacing = 5f;
                text.fontStyle = FontStyles.Bold;
                text.outlineWidth = 0.14f;
                text.outlineColor = new Color32(64, 190, 172, 255);
                if (data != null && !string.IsNullOrEmpty(data.characterName)) text.text = data.characterName;
                SetRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(28f, 294f), new Vector2(CardW - 56f, 56f));
            }
        }

        BuildNameGlow(go.transform);
        BuildBio(go.transform, data);
        BuildInstinct(go.transform, data);
        BuildHover(go, btn, data);
        CardMotes.Attach(go.GetComponent<RectTransform>(), CardW, CardH);
    }

    // 立绘区：固定 480×500，立绘按等比铺满后由 RectMask2D 裁掉多余部分（杀戮尖塔的做法）
    private static void BuildArtFrame(Transform card, CharacterData data)
    {
        Transform frame = card.Find("ArtFrame");
        if (frame == null)
        {
            GameObject obj = new GameObject("ArtFrame", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            obj.transform.SetParent(card, false);
            obj.transform.SetAsFirstSibling();
            frame = obj.transform;
        }

        // 老场景里的 ArtFrame 可能没有 RectMask2D（那时立绘是"装进框"的，不需要裁），
        // 现在改成"铺满再裁"，没这个组件立绘就会压到卡片边框外面去。
        if (frame.GetComponent<RectMask2D>() == null) frame.gameObject.AddComponent<RectMask2D>();

        RectTransform frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 1f);
        frt.anchorMax = new Vector2(1f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        frt.offsetMin = new Vector2(0f, -ArtH);
        frt.offsetMax = Vector2.zero;

        Image frameImg = frame.GetComponent<Image>();
        if (frameImg != null)
        {
            frameImg.color = new Color(0f, 0f, 0f, 0f);
            frameImg.raycastTarget = false;
        }

        Transform portrait = frame.Find("Portrait");
        if (portrait == null)
        {
            GameObject obj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(frame, false);
            portrait = obj.transform;
        }

        // 立绘区底层：素材库里的 portrait_underlay（深蓝→青的竖直渐变板）。
        // 这张图本来就是原作给角色立绘垫底的，垫在我们自己的剪影下面刚好接得上。
        Transform plate = frame.Find("Plate");
        if (plate == null)
        {
            GameObject obj = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(frame, false);
            plate = obj.transform;
        }
        RectTransform plt = plate.GetComponent<RectTransform>();
        plt.anchorMin = Vector2.zero;
        plt.anchorMax = Vector2.one;
        plt.pivot = new Vector2(0.5f, 0.5f);
        plt.offsetMin = Vector2.zero;
        plt.offsetMax = Vector2.zero;
        Image plateImg = plate.GetComponent<Image>();
        if (plateImg != null)
        {
            Sprite plateSprite = UiSkin.LoadSprite("Art/portrait_plate");
            plateImg.sprite = plateSprite;
            plateImg.type = Image.Type.Simple;
            // 加载失败时不能留一块白 —— 之前整屏发白就是这么来的
            plateImg.color = plateSprite != null ? new Color(1f, 1f, 1f, 0.95f) : new Color(0f, 0f, 0f, 0f);
            plateImg.raycastTarget = false;
        }
        plate.SetSiblingIndex(0);

        // 立绘本身用战斗里那个剪影（battleSprite），而不是选人用的脸图 ——
        // 这样"选人界面看到的人"和"战斗里站着的人"是同一个形状。
        // 选择界面用「上身特写」那一套，和战斗里的「全身站位」是同一张原图裁的两个人视图，
        // 所以选人时看到的脸和进战斗后站着的人是同一个人（不是两张不同的画）。
        Sprite sprite = PlayerArt.Select(data);
        if (sprite == null && data != null)
        {
            sprite = data.battleSprite != null ? data.battleSprite : data.characterSprite;
        }

        Image img = portrait.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            img.color = Color.white;
        }

        // 现在是「等比铺满」而不是「装进」：四周多出来的由 ArtFrame 的 RectMask2D 裁掉。
        // 特写立绘要贴到卡边才像立绘；装进框里会在两侧留出 30 多像素的空气，
        // 看起来像贴上去的一张小编图，反而不像卡面。
        float artW = CardW * 0.98f;
        float artH = ArtH * 0.98f;
        Vector2 size = new Vector2(artW, artH);
        if (sprite != null && sprite.rect.height > 1f)
        {
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            if (spriteAspect > artW / artH) size = new Vector2(artH * spriteAspect, artH);
            else size = new Vector2(artW, artW / spriteAspect);
        }

        RectTransform prt = portrait.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = size;
        prt.anchoredPosition = Vector2.zero;
        prt.localScale = Vector3.one;
        portrait.SetAsLastSibling();
    }

    // 立绘下面压一块暗板，保证白字压在任何画面上都读得清
    private static void BuildTextPlate(Transform card, string name)
    {
        Transform plate = card.Find(name);
        if (plate == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(card, false);
            plate = obj.transform;
        }

        RectTransform rt = plate.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, CardH - ArtH);

        Image img = plate.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.035f, 0.033f, 0.052f, 0.96f);
            img.raycastTarget = false;
        }

        // 这块暗板必须压在文字下面。它是运行时 new 出来的，默认排在最后一个兄弟位，
        // 于是把场景里那两个文字对象（名字 / 被动）整个盖住 —— 玩家看到的"名字太暗、
        // 被动看不见"就是这个：字没变色，是上面盖了一层 96% 的暗板。
        // 索引 0 是 ArtFrame，所以放到 1。
        plate.SetSiblingIndex(1);
    }

    private static void BuildDivider(Transform card, string name)
    {
        Transform divider = card.Find(name);
        if (divider == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(card, false);
            divider = obj.transform;
        }

        RectTransform rt = divider.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW - 56f, 1f);
        rt.anchoredPosition = new Vector2(0f, 252f);

        Image img = divider.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(Mint.r, Mint.g, Mint.b, 0.22f);
            img.raycastTarget = false;
        }
    }

    /// <summary>
    /// 名字背后的一圈柔光。原来名字是纯纸白色压在深色板上，玩家反馈"太暗、没生气"。
    /// 这里用一张运行时生成的径向衰减贴图拉成横向椭圆垫在字后面，字本身也换成了发光字体色。
    /// 插在 TextPlate 之后，所以会盖住底板、但不盖住名字和立绘。
    /// </summary>
    private static void BuildNameGlow(Transform card)
    {
        Transform glow = card.Find("NameGlow");
        if (glow == null)
        {
            GameObject obj = new GameObject("NameGlow", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(card, false);
            glow = obj.transform;
        }

        RectTransform rt = glow.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(470f, 150f);
        rt.anchoredPosition = new Vector2(-34f, 318f);

        Image img = glow.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = CardMotes.SoftSprite();
            img.type = Image.Type.Simple;
            img.color = new Color(0.42f, 0.86f, 0.78f, 0.22f);
            img.raycastTarget = false;
        }

        // 底板上、文字下：TextPlate 是第 1 个兄弟，插到它后面就行
        Transform plate = card.Find("TextPlate");
        int index = plate != null ? plate.GetSiblingIndex() + 1 : 0;
        glow.SetSiblingIndex(Mathf.Clamp(index, 0, card.childCount - 1));
    }

    /// <summary>
    /// 「初始本能」：这一局的角色从出生就带的一条本能。
    /// 放在卡上是因为它是角色身份的一部分（清道夫铁壁 / 解剖师轻装），
    /// 玩家选人时就该知道"我拿这个人起手是什么手感"，而不是进战斗才发现多了一条图标。
    /// </summary>
    private static void BuildInstinct(Transform card, CharacterData data)
    {
        if (data == null) return;

        int owner = data.passiveSkill == PassiveSkillType.Dissector_Tempo
            ? TalentLibrary.Dissector : TalentLibrary.Sweeper;
        TalentDef starter = TalentLibrary.StarterFor(owner);

        Transform plate = card.Find("InstinctPlate");
        if (plate == null)
        {
            GameObject obj = new GameObject("InstinctPlate", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(card, false);
            plate = obj.transform;
        }

        RectTransform rt = plate.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(CardW - 40f, 54f);
        rt.anchoredPosition = new Vector2(0f, 14f);

        Image img = plate.GetComponent<Image>();
        if (img != null)
        {
            UiSkin.Frame(img, new Color(0.062f, 0.062f, 0.092f, 0.92f),
                new Color(Mint.r, Mint.g, Mint.b, 0.16f), 1, 12);
            img.raycastTarget = false;
        }

        // 图标
        Transform iconT = plate.Find("Icon");
        if (iconT == null)
        {
            GameObject obj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(plate, false);
            iconT = obj.transform;
        }
        RectTransform irt = iconT.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(34f, 34f);
        irt.anchoredPosition = new Vector2(12f, 0f);
        Image icon = iconT.GetComponent<Image>();
        if (icon != null)
        {
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.sprite = starter != null ? UiSkin.LoadSprite("Art/Talents/" + starter.Icon) : null;
            icon.color = icon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        // 标签
        Transform kick = plate.Find("Kicker");
        if (kick == null)
        {
            TextMeshProUGUI created = UiSkin.CreateText(plate, "Kicker", "初始本能",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -7f),
                new Vector2(140f, 14f), 10.5f, new Color(Mint.r, Mint.g, Mint.b, 0.62f),
                TextAlignmentOptions.TopLeft);
            created.characterSpacing = 5f;
            kick = created.transform;
        }

        Transform iname = plate.Find("InstinctName");
        if (iname == null)
        {
            iname = UiSkin.CreateText(plate, "InstinctName", "", new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(56f, -21f), new Vector2(360f, 20f), 15f, Ink,
                TextAlignmentOptions.TopLeft).transform;
        }
        TextMeshProUGUI nameText = iname.GetComponent<TextMeshProUGUI>();
        if (nameText != null) nameText.text = starter != null ? starter.Name : "无";

        Transform idesc = plate.Find("InstinctDesc");
        if (idesc == null)
        {
            idesc = UiSkin.CreateText(plate, "InstinctDesc", "", new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(56f, -41f), new Vector2(388f, 14f), 11f,
                new Color(Dim.r, Dim.g, Dim.b, 0.9f), TextAlignmentOptions.TopLeft).transform;
        }
        TextMeshProUGUI descText = idesc.GetComponent<TextMeshProUGUI>();
        if (descText != null)
        {
            descText.text = starter != null ? starter.Desc.Replace("，", " · ") : "";
            descText.enableWordWrapping = false;
            descText.overflowMode = TextOverflowModes.Ellipsis;
        }

        plate.SetAsLastSibling();
    }

    /// <summary>
    /// 角色描述：这张卡上最该被读的一段字。
    /// 玩家说过"介绍有点蠢"——原来这里写的是被动数值（"回 2 点生命"），
    /// 那是在讲规则，不是在讲这个人。所以这一段讲打法气质，被动条放到下面小一号的位置。
    /// </summary>
    private static void BuildBio(Transform card, CharacterData data)
    {
        Transform bio = card.Find("BioText");
        if (bio == null)
        {
            TextMeshProUGUI created = UiSkin.CreateText(card, "BioText", "", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(28f, 166f), new Vector2(CardW - 56f, 74f), 15.5f,
                new Color(0.90f, 0.91f, 0.88f, 1f), TextAlignmentOptions.TopLeft);
            created.enableWordWrapping = true;
            created.lineSpacing = 6f;
            bio = created.transform;
        }
        TextMeshProUGUI text = bio.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = CharacterCopy.Bio(data);
            text.fontSize = 15.5f;
            text.lineSpacing = 8f;
            text.color = new Color(0.90f, 0.91f, 0.88f, 1f);
            SetRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 166f), new Vector2(CardW - 56f, 74f));
        }
        bio.SetAsLastSibling();
    }

    /// <summary>
    /// 悬停反馈：卡放大 1.02、外圈亮一道薄荷边、底部提示变亮。
    /// 原来整张卡就是一张静态图，鼠标移上去没任何反应 —— 这是"反馈太少"的主因。
    /// 不用 DOTween，直接在 Update 里做指数逼近：场景切走时不会留下没 Kill 的补间。
    /// </summary>
    private static void BuildHover(GameObject card, Button btn, CharacterData data)
    {
        CharacterCardHover hover = card.GetComponent<CharacterCardHover>();
        if (hover == null) hover = card.AddComponent<CharacterCardHover>();
        hover.card = card.GetComponent<RectTransform>();
        Transform hintT = card.transform.Find("PickHint");
        hover.hint = hintT != null ? hintT.GetComponent<TextMeshProUGUI>() : null;
        if (hover.edge == null)
        {
            GameObject edgeObj = new GameObject("KindEdge", typeof(RectTransform), typeof(Image));
            edgeObj.transform.SetParent(card.transform, false);
            RectTransform ert = edgeObj.GetComponent<RectTransform>();
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = Vector2.one;
            ert.pivot = new Vector2(0.5f, 0.5f);
            ert.offsetMin = new Vector2(-4f, -4f);
            ert.offsetMax = new Vector2(4f, 4f);
            Image edge = edgeObj.GetComponent<Image>();
            UiSkin.Frame(edge, new Color(0f, 0f, 0f, 0f), new Color(Mint.r, Mint.g, Mint.b, 0.75f), 2, 10);
            edge.raycastTarget = false;
            edgeObj.transform.SetAsFirstSibling();
            hover.edge = edge;
            Color c = edge.color;
            edge.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    private static void SetRect(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
    }
}

/// <summary>选人卡的悬停反馈（放大 + 薄荷外圈 + 提示变亮），指数逼近，不用补间。</summary>
public class CharacterCardHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    public RectTransform card;
    public Image edge;
    public TextMeshProUGUI hint;

    private float target;
    private float current;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        target = 1f;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
    {
        target = 0f;
    }

    private void Update()
    {
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

        if (card != null) card.localScale = Vector3.one * (1f + 0.02f * current);
        if (edge != null)
        {
            Color c = edge.color;
            edge.color = new Color(c.r, c.g, c.b, 0.85f * current);
        }
        if (hint != null) hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, 0.5f + 0.45f * current);
    }
}
