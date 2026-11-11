using System;
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
    /// 初始化跳字的数据和动画。onFinished 不为空时把对象交还给调用方（对象池），否则直接销毁。
    /// </summary>
    /// <param name="amount">数值大小</param>
    /// <param name="color">文字颜色</param>
    /// <param name="isHeal">是否为治疗（治疗显示+号，伤害显示-号）</param>
    /// <param name="onFinished">动画播完的回调，用于回池</param>
    public void Setup(int amount, Color color, bool isHeal = false, Action<DamagePopup> onFinished = null)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh == null)
        {
            Debug.LogError("【DamagePopup】找不到 TextMeshProUGUI 组件！");
            return;
        }

        gameObject.SetActive(true);

        // 池化复用：先杀掉上一轮的补间，否则旧 Tween 会和新的抢位置/透明度
        transform.DOKill();
        textMesh.DOKill();

        string prefix = isHeal ? "+" : "-";
        textMesh.text = prefix + amount;

        color.a = 1f;
        textMesh.color = color;

        // 稍微加一点随机的初始偏移，避免多个数字挤在完全相同的位置
        float randomX = UnityEngine.Random.Range(-30f, 30f);
        float randomY = UnityEngine.Random.Range(-20f, 20f);
        transform.localPosition += new Vector3(randomX, randomY, 0);

        // 1. 向上飘动
        transform.DOLocalMoveY(transform.localPosition.y + floatHeight, duration).SetEase(Ease.OutQuad);

        // 2. 透明度渐隐到 0，播完回池（没有回调才销毁）
        textMesh.DOFade(0f, duration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            if (onFinished != null)
            {
                onFinished(this);
            }
            else
            {
                Destroy(gameObject);
            }
        });
    }
}
