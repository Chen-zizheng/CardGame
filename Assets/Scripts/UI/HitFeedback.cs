using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 受击 / 攻击 / 防御 反馈组件。
/// 用法：把本脚本挂到敌人或主角的那个 Image 上，什么都不用改，按 H/J/K 就能看到效果。
/// 只做表现，不碰任何玩法逻辑；删掉这个组件就完全恢复原样。
/// </summary>
public class HitFeedback : MonoBehaviour
{
    [Header("基础")]
    [Tooltip("要表现的图形，留空则自动取自己身上的 Graphic")]
    public Graphic target;

    [Tooltip("自动创建一个白色遮罩层用于闪白（不会改动物件的贴图）")]
    public bool autoCreateFlashOverlay = true;

    [Header("受击")]
    [Tooltip("横向挤压比例，0.19 = 变宽 19%，纵向同步变扁以保持体积")]
    public float hitSquash = 0.19f;
    [Tooltip("压下去用多久")]
    public float hitDownTime = 0.10f;
    [Tooltip("弹回来用多久（带过冲）")]
    public float hitRecoverTime = 0.32f;
    [Tooltip("闪白强度 0~1")]
    public float hitFlash = 0.62f;
    [Tooltip("闪白持续多久，0.13 秒约等于 4 帧")]
    public float hitFlashTime = 0.13f;
    [Tooltip("抖动幅度（像素），按 960 宽度设计，分辨率不同要等比换算")]
    public float hitShake = 8f;
    [Tooltip("抖动衰减时长")]
    public float hitShakeTime = 0.45f;
    [Tooltip("顿帧毫秒数，0 = 关闭。会让全局时间短暂停住")]
    public float hitStopMs = 50f;

    [Header("攻击")]
    [Tooltip("冲前的距离（像素）")]
    public float atkForward = 30f;
    [Tooltip("冲出去时的横向拉伸")]
    public float atkStretchX = 0.11f;
    [Tooltip("冲出去时的纵向压扁")]
    public float atkStretchY = 0.09f;
    [Tooltip("冲出去用时")]
    public float atkOutTime = 0.09f;
    [Tooltip("收回来用时")]
    public float atkBackTime = 0.16f;
    [Tooltip("1 = 往右冲，-1 = 往左冲")]
    public float atkDirection = -1f;

    [Header("防御")]
    [Tooltip("收缩比例")]
    public float blkShrink = 0.09f;
    [Tooltip("收缩用时")]
    public float blkTime = 0.12f;
    [Tooltip("蓝色闪光强度")]
    public float blkFlash = 0.24f;
    [Tooltip("防御闪光颜色")]
    public Color blkColor = new Color(0.47f, 0.71f, 1f, 1f);

    [Header("测试热键")]
    [Tooltip("勾上后按 H 受击 / J 攻击 / K 防御，方便直接在编辑器里看效果")]
    public bool testHotkeys = true;

    private RectTransform rt;
    private Graphic g;
    private Image flashImg;
    private Vector3 baseScale;
    private Vector2 basePos;
    private Tween scaleTween, posTween, flashTween;
    private int stopLock = 0;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        g = target != null ? target : GetComponent<Graphic>();
        if (rt == null || g == null)
        {
            Debug.LogError("【HitFeedback】需要挂在带 RectTransform + Graphic(Image) 的物体上", this);
            enabled = false;
            return;
        }
        baseScale = rt.localScale;
        basePos = rt.anchoredPosition;
        if (autoCreateFlashOverlay) CreateFlashOverlay();
    }

    // 复制一个同贴图的子物体当闪白层，原贴图一个像素都不动
    void CreateFlashOverlay()
    {
        var go = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var crt = go.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        flashImg = go.GetComponent<Image>();
        var src = g as Image;
        if (src != null)
        {
            flashImg.sprite = src.sprite;
            flashImg.type = src.type;
            flashImg.preserveAspect = src.preserveAspect;
        }
        flashImg.raycastTarget = false;
        flashImg.color = new Color(1f, 1f, 1f, 0f);
    }

    // ---------------- 对外接口 ----------------
    /// <summary>受击：挤压 + 闪白 + 抖动 + 顿帧</summary>
    public void PlayHit() { PlayHit(hitSquash, hitFlash, Color.white); }

    /// <summary>攻击：冲前 + 拉伸，然后收回</summary>
    public void PlayAttack()
    {
        KillTweens();
        Vector2 from = basePos;
        Vector2 to = basePos + new Vector2(atkForward * atkDirection, 0f);
        posTween = rt.DOAnchorPos(to, atkOutTime).SetEase(Ease.OutQuad)
            .OnComplete(() => posTween = rt.DOAnchorPos(from, atkBackTime).SetEase(Ease.OutCubic));
        scaleTween = rt.DOScale(new Vector3(baseScale.x * (1f + atkStretchX), baseScale.y * (1f - atkStretchY), baseScale.z), atkOutTime)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => scaleTween = rt.DOScale(baseScale, atkBackTime).SetEase(Ease.OutCubic));
    }

    /// <summary>防御：收缩 + 蓝色闪光</summary>
    public void PlayBlock()
    {
        KillTweens();
        scaleTween = rt.DOScale(baseScale * (1f - blkShrink), blkTime).SetEase(Ease.OutQuad)
            .OnComplete(() => scaleTween = rt.DOScale(baseScale, blkTime * 1.6f).SetEase(Ease.OutCubic));
        Flash(blkColor, blkFlash, hitFlashTime);
    }

    /// <summary>通用受击，可自定义强度和颜色</summary>
    public void PlayHit(float squash, float flash, Color flashColor)
    {
        KillTweens();
        float s = squash;
        var squashScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z);
        scaleTween = rt.DOScale(squashScale, hitDownTime).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                scaleTween = rt.DOScale(baseScale, hitRecoverTime)
                    .SetEase(Ease.OutBack, 1.70158f);
            });
        Flash(flashColor, flash, hitFlashTime);
        if (hitShake > 0f)
        {
            posTween = rt.DOShakeAnchorPos(hitShakeTime, new Vector2(hitShake, hitShake * 0.5f), 22, 90f, false, true);
            DOVirtual.DelayedCall(hitShakeTime, () => { if (rt != null) rt.anchoredPosition = basePos; });
        }
        if (hitStopMs > 0f) HitStop(hitStopMs / 1000f);
    }

    // ---------------- 内部 ----------------
    void Flash(Color c, float strength, float dur)
    {
        if (flashImg == null) return;
        if (flashTween != null) flashTween.Kill();
        var col = new Color(c.r, c.g, c.b, strength);
        flashImg.color = col;
        flashTween = flashImg.DOFade(0f, dur).SetEase(Ease.OutQuad);
    }

    // 顿帧：短暂把时间停住，用不受时间缩放影响的方式恢复
    void HitStop(float seconds)
    {
        if (seconds <= 0f) return;
        if (stopLock == 0) Time.timeScale = 0f;
        stopLock++;
        DOVirtual.DelayedCall(seconds, () =>
        {
            stopLock--;
            if (stopLock <= 0) { stopLock = 0; Time.timeScale = 1f; }
        }).SetUpdate(true);
    }

    void KillTweens()
    {
        if (scaleTween != null) scaleTween.Kill();
        if (posTween != null) posTween.Kill();
        if (flashTween != null) flashTween.Kill();
    }

    void OnDisable()
    {
        KillTweens();
        if (stopLock > 0) { stopLock = 0; Time.timeScale = 1f; }
        if (rt != null) { rt.localScale = baseScale; rt.anchoredPosition = basePos; }
        if (flashImg != null) { var c = flashImg.color; c.a = 0f; flashImg.color = c; }
    }

    void Update()
    {
        if (!testHotkeys) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        // 只在启用旧版 Input Manager 时才读键，避免新输入系统下抛异常
        if (Input.GetKeyDown(KeyCode.H)) PlayHit();
        if (Input.GetKeyDown(KeyCode.J)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.K)) PlayBlock();
#endif
    }
}