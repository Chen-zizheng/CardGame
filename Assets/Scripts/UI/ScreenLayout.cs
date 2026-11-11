using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 奖励屏 / 商店屏的版式。
// 和 UiLayout 同样的理由：我这边起不来编辑器，改不了 Inspector，所以版式写在代码里，
// 场景里只留"谁的引用"（cardGridParent / skipButton / leaveButton / healButton / goldText）。
//
// 两屏共用一套骨架：
//   标题(58) + 副标题(20) + 一道薄荷细线 + 一排卡 + 底部按钮
// 卡片用 CardView.ApplyThumbnail 的规格（240×334），这个尺寸下卡名 22、描述 15 都不会溢出。
public static class ScreenLayout
{
    public const float CardW = 240f;
    public const float CardH = 334f;
    public const float CardGap = 108f;

    public const float CardY = 6f;
    public const float RelicY = -294f;        // 遗物货架（ShopController 里同步用这个值）
    public const float BottomY = -410f;       // 回血 / 离开 这一排

    private static readonly Color ButtonFill = new Color(0.075f, 0.078f, 0.105f, 0.94f);
    private static readonly Color ButtonEdge = new Color(0.72f, 0.90f, 0.86f, 0.42f);
    private const float KickerY = 368f;   // 小字英文，压在标题上面
    private const float TitleY = 298f;
    private const float SubY = 234f;
    private const float RuleY = 204f;

    // ── 奖励屏 ─────────────────────────────────────────────
    public static void ApplyReward(Canvas canvas, Transform cardGridParent, Button skipButton)
    {
        if (canvas == null) return;

        HideLegacyTexts(canvas.transform, "请选择一张卡牌");
        AddScrim(canvas.transform, 0.34f);
        AddHeader(canvas.transform, "清场完成", "带走一件", "SALVAGE");
        PrepareCardRow(cardGridParent);

        if (skipButton != null)
        {
            EnsureLabel(skipButton, "跳过");
            UiSkin.SetRect(skipButton.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -352f), new Vector2(320f, 68f));
            UiSkin.StyleButton(skipButton, 21f, ButtonFill, ButtonEdge, UiSkin.Ink);
        }
    }

    // ── 商店屏 ─────────────────────────────────────────────
    public static void ApplyShop(Canvas canvas, Transform cardGridParent, Button leaveButton,
        Button healButton, TextMeshProUGUI healButtonText, TextMeshProUGUI goldText)
    {
        if (canvas == null) return;

        HideLegacyTexts(canvas.transform, "神秘商店");
        AddScrim(canvas.transform, 0.30f);
        AddHeader(canvas.transform, "自动补给机", "", "SUPPLY");
        PrepareCardRow(cardGridParent);

        if (goldText != null)
        {
            UiSkin.SetRect(goldText.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-96f, -72f), new Vector2(400f, 56f));
            goldText.fontSize = 28f;
            goldText.alignment = TextAlignmentOptions.Right;
            goldText.color = UiSkin.Mint;
        }

        // 回血按钮：场景里那颗按钮本身没有文字，文字挂在 healButtonText 上。
        // 之前两边各管各的 —— 按钮在 (-330,-352) 画个框，文字留在 Inspector 原来的位置，
        // 结果就是"两个空白的框"。这里把文字收编成按钮的 Label，框和字永远在一起。
        if (healButton != null)
        {
            if (healButtonText != null) AdoptAsLabel(healButton, healButtonText);
            EnsureLabel(healButton, "灌注修复");
            UiSkin.SetRect(healButton.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-196f, BottomY), new Vector2(360f, 84f));
            UiSkin.StyleButton(healButton, 21f, ButtonFill, ButtonEdge, UiSkin.Ink);
        }

        if (leaveButton != null)
        {
            EnsureLabel(leaveButton, "离开补给机");
            UiSkin.SetRect(leaveButton.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(196f, BottomY), new Vector2(360f, 84f));
            UiSkin.StyleButton(leaveButton, 21f, ButtonFill, ButtonEdge, UiSkin.Ink);
        }
    }

    // ── 共用的零件 ─────────────────────────────────────────
    private static void AddHeader(Transform parent, string title, string subtitle, string kicker)
    {
        AddHeaderFade(parent);

        TextMeshProUGUI t = UiSkin.CreateText(parent, "ScreenTitle", title,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, TitleY), new Vector2(1200f, 90f),
            58f, UiSkin.Ink);
        t.characterSpacing = 12f;
        // 背景正中常有很亮的机器/罐体，纯白字压上去会糊 —— 用 TMP 自带的描边把字从背景里抠出来
        Outline(t, 0.20f);

        TextMeshProUGUI kick = UiSkin.CreateText(parent, "ScreenKicker", kicker,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, KickerY), new Vector2(900f, 26f),
            14f, new Color(UiSkin.Dim.r, UiSkin.Dim.g, UiSkin.Dim.b, 0.75f));
        kick.characterSpacing = 14f;
        Outline(kick, 0.16f);

        TextMeshProUGUI sub = UiSkin.CreateText(parent, "ScreenSub", subtitle,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, SubY), new Vector2(900f, 34f),
            19f, new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, 0.86f));
        Outline(sub, 0.18f);

        GameObject rule = new GameObject("ScreenRule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(parent, false);
        UiSkin.SetRect(rule, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, RuleY), new Vector2(108f, 2f));
        Image img = rule.GetComponent<Image>();
        img.color = new Color(UiSkin.Mint.r, UiSkin.Mint.g, UiSkin.Mint.b, 0.45f);
        img.raycastTarget = false;
    }

    /// <summary>
    /// 顶部一道从上往下的黑渐变。
    /// 背景正中往往是画面最亮的地方（商店那台机器的绿罐子），纯白标题压上去就糊了；
    /// 加这一层只压住上半屏，卡片区不受影响。
    /// </summary>
    /// <summary>TMP 描边：深色一圈，压在亮背景上也读得清（不依赖额外遮罩）。</summary>
    private static void Outline(TextMeshProUGUI text, float width)
    {
        if (text == null) return;
        text.outlineWidth = width;
        text.outlineColor = new Color32(8, 8, 12, 255);
    }

    private static void AddHeaderFade(Transform parent)
    {
        if (parent.Find("HeaderFade") != null) return;

        GameObject go = new GameObject("HeaderFade", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -380f);
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = FadeSprite();
        img.type = Image.Type.Simple;
        img.raycastTarget = false;

        go.transform.SetAsFirstSibling();
    }

    private static Sprite _fade;

    private static Sprite FadeSprite()
    {
        if (_fade != null) return _fade;

        const int h = 128;
        Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);        // 0 底部（透明）→ 1 顶部（最黑）
            // 指数 0.5：暗得快、到卡片上沿（约 380）基本收干净，不会把商品也压黑
            float k = Mathf.Clamp01(Mathf.Pow(t, 0.5f));
            px[y] = new Color(0.028f, 0.026f, 0.040f, k * 0.95f);
        }
        tex.SetPixels32(px);
        tex.Apply();

        _fade = Sprite.Create(tex, new Rect(0f, 0f, 1f, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
        return _fade;
    }

    private static void AddScrim(Transform parent, float alpha)
    {
        if (parent.Find("ScreenScrim") != null) return;
        Image scrim = UiSkin.CreatePanel(parent, "ScreenScrim", new Color(0.035f, 0.032f, 0.05f, alpha));
        scrim.raycastTarget = false;
    }

    // 卡牌容器：砍掉场景里原有的 LayoutGroup（它会和手动摆位打架），并居中
    private static void PrepareCardRow(Transform cardGridParent)
    {
        if (cardGridParent == null) return;

        LayoutGroup group = cardGridParent.GetComponent<LayoutGroup>();
        if (group != null) group.enabled = false;

        RectTransform rt = cardGridParent as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, CardY);
            rt.sizeDelta = new Vector2(1100f, CardH + 20f);
        }
    }

    /// <summary>把容器里的卡按 index 摆成一排；卡本身缩到 240×334 的可读规格。</summary>
    public static void PlaceCards(Transform cardGridParent, int count, float y)
    {
        if (cardGridParent == null || count <= 0) return;

        int slot = 0;
        for (int i = 0; i < cardGridParent.childCount; i++)
        {
            Transform child = cardGridParent.GetChild(i);
            if (!child.gameObject.activeSelf) continue;

            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(CardW, CardH);
            rt.anchoredPosition = new Vector2((slot - (count - 1) * 0.5f) * (CardW + CardGap), y);

            CardView view = child.GetComponent<CardView>();
            if (view != null) view.ApplyThumbnail(CardW, CardH);

            slot++;
        }
    }

    /// <summary>卡片下面那行小字（"点击加入牌库" / 价格）。</summary>
    public static void AddCaption(GameObject cardRoot, string text, Color color)
    {
        if (cardRoot == null) return;

        Transform existing = cardRoot.transform.Find("Caption");
        if (existing != null) Object.Destroy(existing.gameObject);

        UiSkin.CreateText(cardRoot.transform, "Caption", text,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -34f), new Vector2(CardW, 30f),
            19f, color);
    }

    private static void StyleLabel(Button btn, float size, Color color)
    {
        if (btn == null) return;
        TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) return;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>把外面那颗文字（比如 healButtonText）收编成按钮自己的 Label，位置铺满按钮。</summary>
    private static void AdoptAsLabel(Button btn, TextMeshProUGUI text)
    {
        if (btn == null || text == null) return;

        text.transform.SetParent(btn.transform, false);
        text.gameObject.name = "Label";
        text.raycastTarget = false;
        text.gameObject.SetActive(true);

        RectTransform rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 保证按钮上恰好有一个可见 Label（没有就建一个，多了就关掉）。
    /// 场景里那些按钮的标签有的挂在外部变量上、有的干脆没做 —— 不统一收编就会出现"空白的框"。
    /// </summary>
    private static TextMeshProUGUI EnsureLabel(Button btn, string fallback)
    {
        if (btn == null) return null;

        TextMeshProUGUI first = null;
        TextMeshProUGUI[] all = btn.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (first == null)
            {
                first = all[i];
                continue;
            }
            all[i].gameObject.SetActive(false);
        }

        if (first == null)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(btn.transform, false);
            first = go.GetComponent<TextMeshProUGUI>();
            first.text = fallback;
            first.raycastTarget = false;
        }

        RectTransform rt = first.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        first.gameObject.SetActive(true);
        return first;
    }

    // 旧场景里那些"战斗胜利！请选择一张卡牌加入牌库"之类的句子，留着会和新标题打架
    private static void HideLegacyTexts(Transform root, string keyword)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || string.IsNullOrEmpty(texts[i].text)) continue;
            if (texts[i].text.Contains(keyword)) texts[i].gameObject.SetActive(false);
        }
    }
}
