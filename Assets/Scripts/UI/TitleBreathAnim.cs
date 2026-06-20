using UnityEngine;
using UnityEngine.UI;

// 编辑模式就能预览效果，不用运行游戏
[ExecuteInEditMode]
[RequireComponent(typeof(RectTransform))]
public class TitleBreathAnim : MonoBehaviour
{
    [Header("===== 上下浮动效果 =====")]
    [Tooltip("上下浮动的最大距离（像素），推荐5-15")]
    public float floatRange = 10f;
    [Tooltip("浮动速度，数值越大越快，推荐0.8-1.5")]
    public float floatSpeed = 1f;

    [Header("===== 呼吸缩放效果 =====")]
    [Tooltip("缩放幅度，0.05=最大放大5%，推荐0.03-0.08")]
    public float scaleRange = 0.05f;
    [Tooltip("缩放速度，和浮动速度保持一致即可")]
    public float scaleSpeed = 1f;

    [Header("===== 轻微旋转效果（可选） =====")]
    [Tooltip("旋转最大角度，推荐0.5-2度，像素风不要太大")]
    public float rotateRange = 1f;
    [Tooltip("旋转速度，推荐0.8-1.2")]
    public float rotateSpeed = 1f;

    // 记录标题初始状态，避免动画跑飞
    private RectTransform rectTrans;
    private Vector2 startAnchoredPos;
    private Vector3 startScale;
    private Vector3 startRotation;

    void Awake()
    {
        rectTrans = GetComponent<RectTransform>();
        // 记录初始的位置、缩放、旋转
        startAnchoredPos = rectTrans.anchoredPosition;
        startScale = rectTrans.localScale;
        startRotation = rectTrans.localEulerAngles;
    }

    void Update()
    {
        // 用Sin函数做平滑循环动画，范围-1~1，永远循环
        float time = Time.time;

        // 1. 上下浮动动画
        float floatOffset = Mathf.Sin(time * floatSpeed) * floatRange;
        rectTrans.anchoredPosition = new Vector2(startAnchoredPos.x, startAnchoredPos.y + floatOffset);

        // 2. 呼吸缩放动画
        float scaleOffset = Mathf.Sin(time * scaleSpeed) * scaleRange;
        rectTrans.localScale = startScale * (1 + scaleOffset);

        // 3. 轻微旋转动画
        float rotateOffset = Mathf.Sin(time * rotateSpeed) * rotateRange;
        rectTrans.localEulerAngles = new Vector3(startRotation.x, startRotation.y, startRotation.z + rotateOffset);
    }

    // 编辑模式下重置回初始状态
    void OnDisable()
    {
        if (rectTrans != null)
        {
            rectTrans.anchoredPosition = startAnchoredPos;
            rectTrans.localScale = startScale;
            rectTrans.localEulerAngles = startRotation;
        }
    }
}