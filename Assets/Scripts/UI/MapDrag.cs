using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 地图的拖动 + 缩放（仿杀戮尖塔：地图比屏幕长，一次只看一段，可拖着走）。
// ScrollRect 那条路已经废了（它的 Mask 会把节点整体裁掉），所以这里全部自己算：
//   - 拖动：按住空白处平移，按内容/视口高度差夹住，不越界
//   - 缩放：滚轮（或界面上的 ＋/－），范围 minZoom~maxZoom，以视口中心为锚点
public class MapDrag : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("缩放范围（相对 整张图刚好装进屏幕 的倍率）")]
    public float minZoom = 0.85f;
    public float maxZoom = 2.8f;

    private RectTransform content;
    private RectTransform viewport;
    private Canvas canvas;

    private float baseScale = 1f;   // 整张图刚好入屏的比例
    private float zoom = 1f;        // 相对 baseScale 的倍率
    private float minY;
    private float maxY;

    public float Zoom { get { return zoom; } }
    public bool CanPan { get { return maxY - minY > 1f; } }

    public void Configure(RectTransform contentRect, RectTransform viewportRect, Canvas ownerCanvas,
        float fitScale, float startZoom)
    {
        content = contentRect;
        viewport = viewportRect;
        canvas = ownerCanvas;
        baseScale = Mathf.Max(0.05f, fitScale);
        zoom = Mathf.Clamp(startZoom, minZoom, maxZoom);

        ApplyScale();
        JumpToBottom();
    }

    public void ApplyScale()
    {
        if (content == null) return;
        content.localScale = Vector3.one * (baseScale * zoom);
        RecalculateBounds();
    }

    public void RecalculateBounds()
    {
        if (content == null || viewport == null) return;

        float contentHeight = content.sizeDelta.y * Mathf.Abs(content.localScale.y);
        float viewportHeight = viewport.rect.height;
        float overflow = (contentHeight - viewportHeight) * 0.5f;

        if (overflow <= 1f)
        {
            minY = 0f;
            maxY = 0f;
            Vector2 centered = content.anchoredPosition;
            centered.y = 0f;
            content.anchoredPosition = centered;
            return;
        }

        maxY = overflow;
        minY = -overflow;

        Vector2 p = content.anchoredPosition;
        p.y = Mathf.Clamp(p.y, minY, maxY);
        content.anchoredPosition = p;
    }

    // 初始视角：从最底层（起点）开始看
    public void JumpToBottom()
    {
        if (content == null) return;
        Vector2 p = content.anchoredPosition;
        p.y = minY;
        content.anchoredPosition = p;
    }

    public void ZoomBy(float delta)
    {
        if (content == null) return;

        float target = Mathf.Clamp(zoom + delta, minZoom, maxZoom);
        if (Mathf.Abs(target - zoom) < 0.0001f) return;

        // 以视口中心为锚点：缩放前后保持"视野中心看到的那一层"不动
        float before = content.anchoredPosition.y;
        float ratio = target / zoom;

        zoom = target;
        ApplyScale();

        Vector2 p = content.anchoredPosition;
        p.y = Mathf.Clamp(before * ratio, minY, maxY);
        content.anchoredPosition = p;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null) return;
        float scale = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.0001f) : 1f;
        Move(eventData.delta.y / scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    // 滚轮 = 缩放（StS 的滚轮是平移，但这里地图本来就短，缩放更有用）
    public void OnScroll(PointerEventData eventData)
    {
        ZoomBy(eventData.scrollDelta.y * 0.12f);
    }

    private void Move(float dy)
    {
        if (content == null) return;
        Vector2 p = content.anchoredPosition;
        p.y = Mathf.Clamp(p.y + dy, minY, maxY);
        content.anchoredPosition = p;
    }
}
