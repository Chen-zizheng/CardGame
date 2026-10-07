using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 按钮悬停缩放脚本，拖到按钮上就能直接用，无需额外设置
[RequireComponent(typeof(Button))]
public class ButtonScaleHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("缩放设置")]
    [Tooltip("悬停时的放大倍数，1.1=放大10%")] public float hoverScale = 1.1f;
    [Tooltip("按下时的缩小倍数")] public float pressedScale = 0.95f;
    [Tooltip("动画速度，数值越大动画越快")] public float animSpeed = 10f;

    private RectTransform rectTrans;
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Awake()
    {
        rectTrans = GetComponent<RectTransform>();
        originalScale = rectTrans.localScale; // 记录按钮原本的大小
        targetScale = originalScale;
    }

    void Update()
    {
        // 平滑缩放过渡，不会生硬卡顿
        rectTrans.localScale = Vector3.Lerp(rectTrans.localScale, targetScale, Time.deltaTime * animSpeed);
    }

    // 鼠标进入按钮（悬停）
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
    }

    // 鼠标离开按钮
    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    // 鼠标按下按钮
    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * pressedScale;
    }

    // 鼠标松开按钮
    public void OnPointerUp(PointerEventData eventData)
    {
        // 松开后，鼠标还在按钮上就回到悬停大小，否则恢复原大小
        targetScale = eventData.hovered.Contains(gameObject) ? originalScale * hoverScale : originalScale;
    }
}