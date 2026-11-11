using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 把 UiSpec 变成真实的 GameObject。所有面板都应该走这里，
// 而不是各自 new GameObject 然后手算 anchor —— 那正是上一版错位的原因。
public static class UiSpecApply
{
    public static Dictionary<string, GameObject> Build(UiSpec spec, Transform parent, float scale = 1f)
    {
        Dictionary<string, GameObject> made = new Dictionary<string, GameObject>();

        // 容器（面板本体）已经存在，spec 只描述它里面的东西
        GameObject root = new GameObject(spec.Name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        UiSkin.SetRect(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(spec.Width * scale, spec.Height * scale));
        made["__root"] = root;

        // 先建没有父级的元素，再建它们的子级
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < spec.Elements.Count; i++)
            {
                UiEl e = spec.Elements[i];
                bool hasParent = !string.IsNullOrEmpty(e.Parent);
                if ((pass == 0) == hasParent) continue;

                Transform target = root.transform;
                if (hasParent)
                {
                    GameObject p;
                    if (made.TryGetValue(e.Parent, out p)) target = p.transform;
                }

                GameObject go = Create(e, target, scale);
                made[e.Name] = go;
            }
        }

        return made;
    }

    private static GameObject Create(UiEl e, Transform parent, float scale)
    {
        GameObject go;
        switch (e.Kind)
        {
            case UiKind.Text:
            {
                go = new GameObject(e.Name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                ApplyRect(go, e, scale);

                TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
                text.text = e.Text;
                text.fontSize = e.FontSize * scale;
                text.enableAutoSizing = false;
                text.alignment = e.LeftAligned ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                text.color = ColorOf(e);
                text.raycastTarget = false;
                if (e.Spacing > 0f) text.characterSpacing = e.Spacing;
                return go;
            }

            case UiKind.Button:
            {
                Button btn = UiSkin.CreateButton(parent, e.Name, e.Text,
                    new Vector2(e.AnchorX, e.AnchorY), new Vector2(e.PivotX, e.PivotY),
                    new Vector2(e.PosX * scale, e.PosY * scale),
                    new Vector2(e.Width * scale, e.Height * scale), e.FontSize * scale);

                if (e.Ghost)
                    UiSkin.StyleButton(btn, e.FontSize * scale, new Color(1f, 1f, 1f, 0.04f),
                        new Color(1f, 1f, 1f, 0.18f), UiSkin.Dim);
                else
                {
                    Color paper = new Color(0.93f, 0.92f, 0.88f, 0.95f);
                    UiSkin.StyleButton(btn, e.FontSize * scale, paper, paper, UiSkin.Dark);
                    TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.characterSpacing = 6f;
                }

                return btn.gameObject;
            }

            case UiKind.Slider:
                return BuildSlider(e, parent, scale);

            default:
            {
                go = new GameObject(e.Name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                ApplyRect(go, e, scale);

                Image img = go.GetComponent<Image>();
                img.color = ColorOf(e);
                img.raycastTarget = false;
                if (e.Radius > 0f)
                    UiSkin.Frame(img, ColorOf(e), new Color(0f, 0f, 0f, 0f), 0, Mathf.RoundToInt(e.Radius));
                return go;
            }
        }
    }

    private static GameObject BuildSlider(UiEl e, Transform parent, float scale)
    {
        GameObject track = new GameObject(e.Name, typeof(RectTransform), typeof(Image), typeof(Slider));
        track.transform.SetParent(parent, false);
        ApplyRect(track, e, scale);

        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.10f);
        trackImage.raycastTarget = true;

        GameObject fillArea = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillArea.transform.SetParent(track.transform, false);
        RectTransform frt = fillArea.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;
        Image fill = fillArea.GetComponent<Image>();
        fill.color = ColorOf(e);
        fill.raycastTarget = false;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(track.transform, false);
        UiSkin.SetRect(handle, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(26f * scale, 26f * scale));
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = UiSkin.Paper;
        handleImage.raycastTarget = true;

        Slider slider = track.GetComponent<Slider>();
        slider.fillRect = frt;
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = Mathf.Clamp01(e.Value < 0f ? 0.8f : e.Value);
        return track;
    }

    private static void ApplyRect(GameObject go, UiEl e, float scale)
    {
        RectTransform rt = go.GetComponent<RectTransform>();

        if (e.Stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return;
        }

        // 注意：UiSkin.SetRect 的第 2 个参数是 pivot，不是 anchorMax ——
        // 之前把它当成"四边拉伸"用，做出来的面板是 0×0 贴在父级左下角，
        // 里面所有元素都掉到面板外面。拉伸请用 Stretch 或直接设锚点。
        UiSkin.SetRect(go, new Vector2(e.AnchorX, e.AnchorY), new Vector2(e.PivotX, e.PivotY),
            new Vector2(e.PosX * scale, e.PosY * scale),
            new Vector2(e.Width * scale, e.Height * scale));
    }

    public static Color ColorOf(UiEl e)
    {
        Color c;
        switch (e.ColorKey)
        {
            case "dim": c = UiSkin.Dim; break;
            case "mint": c = UiSkin.Mint; break;
            case "paper": c = new Color(0.93f, 0.92f, 0.88f, 0.95f); break;
            case "dark": c = UiSkin.Dark; break;
            case "slate": c = UiSkin.Slate; break;
            case "glass": c = UiSkin.Glass; break;
            case "clear": c = new Color(0f, 0f, 0f, 0f); break;
            default: c = UiSkin.Ink; break;
        }

        c.a *= e.Alpha;
        return c;
    }

    public static void Stretch(GameObject go)
    {
        if (go == null) return;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
