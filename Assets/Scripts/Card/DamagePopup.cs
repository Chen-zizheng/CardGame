using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamagePopup : MonoBehaviour
{
    private TextMeshProUGUI textMesh;

    [Header("动画配置")]
    [Tooltip("向上飘动的距离")]
    public float floatHeight = 100f;
    [Tooltip("动画持续时间")]
    public float duration = 0.8f;

    /// <summary>
    /// 初始化跳字的数据和动画
    /// </summary>
    /// <param name="amount">数值大小</param>
    /// <param name="color">文字颜色</param>
    /// <param name="isHeal">是否为治疗（治疗显示+号，伤害显示-号）</param>
    public void Setup(int amount, Color color, bool isHeal = false)
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh == null)
        {
            Debug.LogError("【DamagePopup】找不到 TextMeshProUGUI 组件！");
            return;
        }

        // 设置文本内容和颜色
        string prefix = isHeal ? "+" : "-";
        textMesh.text = $"{prefix}{amount}";
        textMesh.color = color;

        // 稍微加一点随机的初始位置偏移，避免多个数字挤在完全相同的位置
        float randomX = Random.Range(-30f, 30f);
        float randomY = Random.Range(-20f, 20f);
        transform.localPosition += new Vector3(randomX, randomY, 0);

        // --- DoTween 动画序列 ---
        // 1. 向上飘动 (LocalMoveY)
        transform.DOLocalMoveY(transform.localPosition.y + floatHeight, duration).SetEase(Ease.OutQuad);

        // 2. 透明度渐隐到 0 (Fade)
        textMesh.DOFade(0, duration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            // 动画结束后，自动销毁这个跳字对象，释放内存
            Destroy(gameObject);
        });
    }
}