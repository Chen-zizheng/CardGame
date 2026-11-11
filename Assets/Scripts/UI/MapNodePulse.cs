using DG.Tweening;
using UnityEngine;

// 地图上「下一步能走」的节点，做一次很轻的缩放呼吸。
// 只放大 6%，0.9 秒一个来回 —— 再大就会像在抽搐。
public class MapNodePulse : MonoBehaviour
{
    private void OnEnable()
    {
        transform.localScale = Vector3.one;
        transform.DOScale(1.06f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
    }

    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
    }
}
