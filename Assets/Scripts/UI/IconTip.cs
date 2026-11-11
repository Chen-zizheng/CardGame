using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 42px 的图标只能说"你有这件东西"，说不出它是什么。
/// 悬停（或者点一下，兼容触屏）就在图标上方弹一条：名字 + 效果。
///
/// 之所以做成运行时弹窗而不是常驻文字：战斗 HUD 左上角那块板已经排满了，
/// 常驻说明会把血条/灵能挤掉；而"我捡到的这个到底是什么"是偶尔才需要查一次的信息。
/// </summary>
public class IconTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private static GameObject _panel;
    private static object _owner;

    public string Title;
    public string Body;

    public static void Attach(GameObject host, string title, string body)
    {
        if (host == null || string.IsNullOrEmpty(title)) return;

        // 不接射线就永远收不到悬停 —— HUD 里很多图默认 raycastTarget=false
        Image img = host.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        IconTip tip = host.GetComponent<IconTip>();
        if (tip == null) tip = host.AddComponent<IconTip>();
        tip.Title = title;
        tip.Body = body;
    }

    public void OnPointerEnter(PointerEventData eventData) { Show(this); }
    public void OnPointerExit(PointerEventData eventData) { if (ReferenceEquals(_owner, this)) Hide(); }

    // 点一下也能看：手柄/触屏没有悬停这件事
    public void OnPointerClick(PointerEventData eventData)
    {
        if (ReferenceEquals(_owner, this)) Hide();
        else Show(this);
    }

    private void OnDisable() { if (ReferenceEquals(_owner, this)) Hide(); }

    private static void Hide()
    {
        _owner = null;
        if (_panel != null) Destroy(_panel);
        _panel = null;
    }

    private static void Show(IconTip src)
    {
        Canvas canvas = src.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Hide();
        _owner = src;

        string body = src.Body != null ? src.Body : "";
        float width = 340f;
        int lines = Mathf.Max(1, Mathf.CeilToInt(body.Length / 22f));
        float height = 40f + lines * 21f;

        _panel = new GameObject("IconTip", typeof(RectTransform));
        _panel.transform.SetParent(canvas.transform, false);
        RectTransform rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);     // 以底边中点为锚：弹在图标正上方
        rt.sizeDelta = new Vector2(width, height);
        rt.localScale = Vector3.one;

        Image bg = _panel.AddComponent<Image>();
        UiSkin.Frame(bg, new Color(0.040f, 0.044f, 0.066f, 0.97f), new Color(0.72f, 0.90f, 0.86f, 0.42f), 1, 10);
        bg.raycastTarget = false;

        UiSkin.CreateText(_panel.transform, "Title", src.Title,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(width - 32f, 22f),
            16f, new Color(0.72f, 0.90f, 0.86f, 1f), TextAlignmentOptions.Left);

        TextMeshProUGUI desc = UiSkin.CreateText(_panel.transform, "Body", body,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -34f), new Vector2(width - 32f, height - 40f),
            13.5f, new Color(0.88f, 0.91f, 0.90f, 1f), TextAlignmentOptions.TopLeft);
        desc.lineSpacing = 6f;

        // 摆到图标正上方：Overlay 画布下相机传 null 即可换算
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, src.transform.position);
        Vector2 local;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
        {
            // 贴左边界时别把说明弹到屏幕外
            float half = width * 0.5f;
            float limit = canvasRect.rect.width * 0.5f - half - 12f;
            local.x = Mathf.Clamp(local.x, -limit, limit);
            rt.anchoredPosition = local + new Vector2(0f, 30f);
        }
    }
}
