using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 一套共用的"UI 皮"。
// 上一版的按钮用的是素材库里那张 512×256 的白色胶囊做九宫格，边框 48px：
// 按钮只有 84 高，上下两条 48 的切片加起来 96 > 84，Unity 只能压缩重叠 ——
// 这就是你看到的"按钮缺了一块 / 形状歪掉"的真正原因。
// 现在改成运行时用代码画一张 64×64 的圆角空心框（线宽 2px、圆角 12px），
// 九宫格边框只有 15px，缩放再小也不会变形，也顺手做出了"细边石板"的质感。
public static class UiSkin
{
    public static readonly Color Paper = new Color(0.949f, 0.937f, 0.902f, 0.92f);
    public static readonly Color Ink   = new Color(0.953f, 0.937f, 0.902f, 1f);
    public static readonly Color Dim   = new Color(0.64f, 0.72f, 0.72f, 1f);
    public static readonly Color Mint  = new Color(0.72f, 0.90f, 0.86f, 1f);
    public static readonly Color Dark  = new Color(0.10f, 0.08f, 0.14f, 1f);
    public static readonly Color Glass = new Color(1f, 1f, 1f, 0.06f);

    // 石板按钮：极暗的填充 + 薄荷细边，文字米白
    public static readonly Color Slate      = new Color(0.055f, 0.052f, 0.085f, 0.78f);
    public static readonly Color SlateHover = new Color(0.10f, 0.10f, 0.15f, 0.86f);

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    // ---------- 程序化生成的九宫格圆角框 ----------
    public static Sprite FrameSprite(Color fill, Color border, int borderPx, int radius)
    {
        string key = "frame:" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(border)
                     + borderPx + ":" + radius;
        Sprite cached;
        if (SpriteCache.TryGetValue(key, out cached) && cached != null) return cached;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        float box = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                // 圆角矩形的有符号距离：d<0 在内部
                float qx = Mathf.Abs(px) - box;
                float qy = Mathf.Abs(py) - box;
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                Color c;
                if (d >= 0.5f) c = new Color(0f, 0f, 0f, 0f);
                else
                {
                    c = d > -borderPx ? border : fill;
                    c.a *= Mathf.Clamp01(0.5f - d);   // 1px 抗锯齿
                }
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        float b = radius + borderPx + 2f;
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));

        SpriteCache[key] = sprite;
        return sprite;
    }

    public static void Frame(Image img, Color fill, Color border, int borderPx = 2, int radius = 12)
    {
        if (img == null) return;
        Sprite sprite = FrameSprite(fill, border, borderPx, radius);
        if (sprite == null) { img.color = fill; return; }
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.pixelsPerUnitMultiplier = 1f;
    }

    // ---------- 素材库里的白色 UI 形状（圆片 / 药丸）----------
    public static Sprite LoadSliced(string path, Vector4 border)
    {
        Sprite cached;
        if (SpriteCache.TryGetValue(path, out cached) && cached != null) return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        SpriteCache[path] = sprite;
        return sprite;
    }

    // 圆角胶囊（Button.png，512×256）。边框必须远小于控件高度，否则会被压出缺口，
    // 所以这里默认给 24px 而不是 48。
    public static void Pill(Image img, Color tint, float border = 24f)
    {
        if (img == null) return;
        Sprite sprite = LoadSliced("Art/ui_pill", new Vector4(border, border, border, border));
        if (sprite == null) { img.color = tint; return; }
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = tint;
    }

    // 圆按钮（button_circle_1.png，256×256）
    public static void Circle(Image img, Color tint)
    {
        if (img == null) return;
        Sprite sprite = LoadSliced("Art/ui_circle", new Vector4(72f, 72f, 72f, 72f));
        if (sprite == null) { img.color = tint; return; }
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = tint;
    }

    public static void SolidButton(GameObject go)
    {
        if (go == null) return;
        Image bg = go.GetComponent<Image>();
        if (bg != null) bg.color = Paper;

        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.color = Dark;
            label.enableAutoSizing = false;
        }
    }

    // ---------- 按钮统一样式 ----------
    // Button 的 ColorBlock 会在运行时覆盖 Image.color（每次 OnEnable 都会 CrossFade 回去），
    // 这也是之前"颜色改不动、永远是青/灰/红"的原因 —— 必须同时把 ColorBlock 也改掉。
    public static void StyleButton(Button btn, float fontSize = 26f, Color? fill = null, Color? border = null,
        Color? labelColor = null)
    {
        if (btn == null) return;

        Color f = fill.HasValue ? fill.Value : Slate;
        Color b = border.HasValue ? border.Value : new Color(Mint.r, Mint.g, Mint.b, 0.55f);
        Color l = labelColor.HasValue ? labelColor.Value : Ink;

        Frame(btn.GetComponent<Image>(), f, b, 2, 12);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.75f, 0.80f, 0.80f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) return;

        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.color = l;
        label.characterSpacing = 2f;
        label.raycastTarget = false;

        RectTransform lt = label.rectTransform;
        lt.anchorMin = Vector2.zero;
        lt.anchorMax = Vector2.one;
        lt.pivot = new Vector2(0.5f, 0.5f);
        lt.offsetMin = Vector2.zero;
        lt.offsetMax = Vector2.zero;
        lt.localScale = Vector3.one;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size, float fontSize = 24f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = true;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.raycastTarget = false;

        StyleButton(btn, fontSize);
        return btn;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, string content, Vector2 anchor,
        Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    public static Image CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static void SetRect(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (go == null) return;
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Outline(Graphic graphic, Color color, float width)
    {
        if (graphic == null) return;
        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
        outline.useGraphicAlpha = false;
    }

    // ---------- 地图 ----------
    // 以前的节点是「彩色实心圆 + 三个汉字标签」，十六个节点一起看就是一堆色块，
    // 所以丑。现在：所有节点共用一个深色圆盘 + 细描边，类型只由里面的白色字形区分
    // （剑=战斗 / 骷髅=精英 / 火=篝火 / 店=商店 / 冠=首领），不再有文字。
    public static Sprite LoadSprite(string path)
    {
        Sprite cached;
        if (SpriteCache.TryGetValue(path, out cached) && cached != null) return cached;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) SpriteCache[path] = sprite;
        return sprite;
    }

    public static Sprite MapGlyph(string id)
    {
        string path = "Art/Map/" + id;
        Sprite cached;
        if (SpriteCache.TryGetValue(path, out cached) && cached != null) return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) SpriteCache[path] = sprite;
        return sprite;
    }

    public static void MapNode(GameObject nodeObj, bool accessible, bool visited, Color accent, Sprite glyph)
    {
        if (nodeObj == null) return;

        Image bg = nodeObj.GetComponent<Image>();
        if (bg != null)
        {
            float discAlpha = visited ? 0.42f : (accessible ? 0.97f : 0.80f);
            Circle(bg, new Color(0.048f, 0.052f, 0.078f, discAlpha));
            Outline(bg, new Color(accent.r, accent.g, accent.b,
                visited ? 0.30f : (accessible ? 0.95f : 0.26f)), accessible ? 3f : 1.6f);
        }

        // 老的三个汉字标签直接关掉，地图上不再出现文字
        TextMeshProUGUI text = nodeObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.gameObject.SetActive(false);

        Transform existing = nodeObj.transform.Find("Glyph");
        Image gi;
        if (existing == null)
        {
            GameObject go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(nodeObj.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(46f, 46f);
            rt.localScale = Vector3.one;
            gi = go.GetComponent<Image>();
            gi.raycastTarget = false;
        }
        else gi = existing.GetComponent<Image>();

        if (gi != null && glyph != null)
        {
            gi.sprite = glyph;
            gi.type = Image.Type.Simple;
            gi.preserveAspect = true;
            gi.color = visited
                ? new Color(Dim.r, Dim.g, Dim.b, 0.38f)
                : (accessible ? Ink : new Color(0.78f, 0.84f, 0.84f, 0.55f));
        }
    }

    public static void MapLine(Image line, float alpha = 0.16f, float width = 2f)
    {
        if (line == null) return;
        line.color = new Color(Mint.r, Mint.g, Mint.b, alpha);
        RectTransform rt = line.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, width);
    }
}
