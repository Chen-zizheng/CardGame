using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(RawImage))]
public class BGScroller : MonoBehaviour
{
    [Tooltip("X轴滚动速度，正数向右，负数向左")]
    public float scrollSpeedX = 0.02f;
    [Tooltip("Y轴滚动速度，正数向上，负数向下")]
    public float scrollSpeedY = 0.01f;

    private RawImage bgImage;

    void Awake()
    {
        bgImage = GetComponent<RawImage>();
    }

    void Update()
    {
        // 无限滚动核心：偏移纹理的UV坐标
        Rect uvRect = bgImage.uvRect;
        uvRect.x += scrollSpeedX * Time.deltaTime;
        uvRect.y += scrollSpeedY * Time.deltaTime;
        bgImage.uvRect = uvRect;
    }

    // 编辑模式关闭时重置UV
    void OnDisable()
    {
        if (bgImage != null)
        {
            bgImage.uvRect = new Rect(0, 0, 1, 1);
        }
    }
}