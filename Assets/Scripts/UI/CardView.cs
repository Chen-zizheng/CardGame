using CardCombat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("卡牌视觉层(必填)")]
    public Transform cardVisual; // 动画只操作这个层！

    [Header("卡牌UI组件")]
    public UnityEngine.UI.Button cardButton;
    public UnityEngine.UI.Image cardBackground;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardCostText;
    public TextMeshProUGUI cardDescriptionText;

    [Header("悬停设置")]
    public float hoverScale = 1.1f;
    public float hoverYOffset = 30f;

    [Header("动画设置")]
    public float drawAnimationDuration = 0.3f;
    public float playAnimationDuration = 0.2f;

    // 卡面文字的三级层级：名称 30 / 描述 22 / 费用跟随圆框。
    // 全部是「上限」，TMP 开了 Auto Size 时不动它，避免撑破卡框。
    private const float NameSize = 30f;
    private const float DescSize = 22f;
    private static readonly Color NameColor = new Color(0.953f, 0.937f, 0.902f, 1f); // #F3EFE6
    private static readonly Color DescColor = new Color(0.64f, 0.72f, 0.72f, 1f);

    // 强化牌（id 带 +）在卡面右上角挂一枚角标：牌库里十几张牌扫一眼就能分出哪些是强化过的。
    // 只有名字末尾一个 "+" 太弱了，卡片版式完全一样，扫不出来。
    private GameObject upgradeBadge;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Vector3 restLocalPosition;
    private bool isHovering = false;

    public CardData currentCard { get; private set; }

    private void Awake()
    {
        // 没在 Inspector 里拖 CardVisual 时，自动在子物体里找一层（不再打印整棵子树）
        if (cardVisual == null)
        {
            RectTransform[] allChildren = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform child in allChildren)
            {
                if (child.name == "CardVisual")
                {
                    cardVisual = child;
                    break;
                }
            }

            if (cardVisual == null)
            {
                Debug.LogError("【CardView】找不到名为 CardVisual 的子物体：" + gameObject.name);
                return;
            }
        }

        originalScale = cardVisual.localScale;
        restLocalPosition = cardVisual.localPosition;
    }

    public void SetCardData(CardData data)
    {
        currentCard = data;
        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // 名字按类型提色：这是"一眼看出这是什么牌"的同一套语言，
        // 手牌、奖励、商店、牌库四处共用 CardPalette，避免同一个类型在不同界面换颜色。
        Color kindColor = CardPalette.Of(data.kind);
        if (cardNameText != null)
        {
            cardNameText.text = data.cardName;
            cardNameText.color = Color.Lerp(NameColor, kindColor, 0.55f);
            if (!cardNameText.enableAutoSizing) cardNameText.fontSize = Mathf.Min(cardNameText.fontSize, NameSize);
        }

        if (cardCostText != null)
        {
            cardCostText.text = data.cost.ToString();
            cardCostText.color = kindColor;
            if (!cardCostText.enableAutoSizing) cardCostText.fontSize = Mathf.Clamp(cardCostText.fontSize, 16f, 40f);
        }

        if (cardDescriptionText != null)
        {
            cardDescriptionText.text = data.description;
            cardDescriptionText.color = DescColor;
            if (!cardDescriptionText.enableAutoSizing) cardDescriptionText.fontSize = Mathf.Min(cardDescriptionText.fontSize, DescSize);
        }

        if (cardBackground != null && data.cardBackgroundSprite != null)
            cardBackground.sprite = data.cardBackgroundSprite;

        ShowUpgradeBadge(CardLibrary.IsUpgraded(data.id));
    }

    /// <summary>
    /// 角标做成运行时子物体而不是预制体：CardView 预制体在场景里，加节点要重开工程，
    /// 而编辑器目前起不来（Tuanjie 140063）。挂在 cardVisual 上，跟着悬停一起缩放。
    /// </summary>
    private void ShowUpgradeBadge(bool on)
    {
        if (upgradeBadge == null && on)
        {
            Transform parent = cardVisual != null ? cardVisual : transform;
            upgradeBadge = new GameObject("UpgradeBadge", typeof(RectTransform), typeof(Image));
            upgradeBadge.transform.SetParent(parent, false);

            RectTransform rt = upgradeBadge.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(54f, 22f);
            // 压在名字行下面一点：名字在 -12、高 34，从 -50 起就不打架了
            rt.anchoredPosition = new Vector2(-8f, -50f);

            Image bg = upgradeBadge.GetComponent<Image>();
            UiSkin.Frame(bg, new Color(0.42f, 0.86f, 0.76f, 0.96f),
                new Color(0.06f, 0.10f, 0.11f, 0.55f), 1, 8);
            bg.raycastTarget = false;

            TextMeshProUGUI label = UiSkin.CreateText(upgradeBadge.transform, "Text", "强化",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 22f), 12f,
                new Color(0.07f, 0.09f, 0.11f, 1f));
            if (label != null) label.raycastTarget = false;
        }

        if (upgradeBadge != null) upgradeBadge.SetActive(on);
    }

    // 牌库/奖励等"展示用"卡：不暗淡、不响应悬停与点击
    public void SetReadOnly()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;

        if (cardButton != null) cardButton.interactable = true;
        if (cardBackground != null) cardBackground.canvasRenderer.SetAlpha(1f);
        ResetVisual();
    }

    public void SetCardInteractable(bool canClick)
    {
        if (cardButton != null)
            cardButton.interactable = canClick;

        if (cardBackground != null)
            cardBackground.canvasRenderer.SetAlpha(canClick ? 1f : 0.5f);
    }

    // 费用不足时只把数字压暗发红，能打时必须回到"名字色→稀有色的混合"，不能刷成纯白
    public void SetEnergyInsufficient(bool isInsufficient)
    {
        if (cardCostText == null) return;

        Color baseColor = currentCard != null ? CardPalette.Of(currentCard.kind) : NameColor;

        cardCostText.color = isInsufficient
            ? new Color(0.82f, 0.36f, 0.36f, 1f)
            : baseColor;
    }

    // 牌库/奖励里的小卡片版式：同一个 prefab，只换尺寸与字号，避免 30 号字撑破 180 宽的卡框
    public void ApplyThumbnail(float width, float height)
    {
        RectTransform self = GetComponent<RectTransform>();
        if (self != null) self.sizeDelta = new Vector2(width, height);

        if (cardNameText != null)
        {
            RectTransform rt = cardNameText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta = new Vector2(width - 28f, 34f);

            cardNameText.fontSize = 22f;
            cardNameText.enableAutoSizing = true;
            cardNameText.fontSizeMin = 14f;
            cardNameText.fontSizeMax = 22f;
            cardNameText.enableWordWrapping = false;
            cardNameText.alignment = TextAlignmentOptions.Center;
        }

        if (cardDescriptionText != null)
        {
            RectTransform rt = cardDescriptionText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 12f);
            rt.sizeDelta = new Vector2(width - 26f, 70f);

            cardDescriptionText.fontSize = 15f;
            cardDescriptionText.enableAutoSizing = true;
            cardDescriptionText.fontSizeMin = 11f;
            cardDescriptionText.fontSizeMax = 15f;
            cardDescriptionText.enableWordWrapping = true;
            cardDescriptionText.alignment = TextAlignmentOptions.Center;
        }

        if (cardCostText != null)
        {
            RectTransform rt = cardCostText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -6f);
            rt.sizeDelta = new Vector2(34f, 34f);

            cardCostText.fontSize = 20f;
            cardCostText.enableAutoSizing = false;
            cardCostText.alignment = TextAlignmentOptions.Center;
        }

        if (cardButton != null) cardButton.interactable = true;
    }

    public void PrepareForDraw(Vector3 startLocalPos)
    {
        if (cardVisual == null) return;

        originalPosition = cardVisual.localPosition;
        cardVisual.localPosition = startLocalPos;
        cardVisual.localScale = Vector3.zero;
    }

    public void PlayDrawAnimation()
    {
        if (cardVisual == null) return;
        cardVisual.DOKill();
        cardVisual.DOLocalMove(originalPosition, drawAnimationDuration).SetEase(Ease.OutBack);
        cardVisual.DOScale(originalScale, drawAnimationDuration).SetEase(Ease.OutBack);
    }

    // 手牌重排后把视觉层拉回静止位：清掉上一张牌残留的位移/缩放/悬停放大
    public void ResetVisual()
    {
        if (cardVisual == null) return;
        cardVisual.DOKill();
        cardVisual.localScale = originalScale;
        cardVisual.localPosition = restLocalPosition;
        originalPosition = restLocalPosition;
        isHovering = false;
    }

    public void PlayPlayAnimation(Vector3 toPosition, TweenCallback onComplete)
    {
        if (cardVisual == null)
        {
            Debug.LogError($"【CardView.PlayPlayAnimation】cardVisual 为 NULL，无法执行动画！");
            return;
        }
        cardVisual.DOKill();
        cardVisual.DOMove(toPosition, playAnimationDuration).SetEase(Ease.InBack).OnComplete(onComplete);
        cardVisual.DOScale(originalScale * 0.5f, playAnimationDuration);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardVisual == null) return;
        if (cardButton == null || !cardButton.interactable) return;

        // 抽牌/打出动画进行中不响应悬停，否则和 Tween 抢 localPosition
        if (DOTween.IsTweening(cardVisual)) return;

        if (!isHovering)
        {
            originalPosition = cardVisual.localPosition;
            isHovering = true;
        }

        cardVisual.localScale = originalScale * hoverScale;
        cardVisual.localPosition = originalPosition + Vector3.up * hoverYOffset;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cardVisual == null) return;

        // 注意：这里不能因为 cardButton 不可点就 return，否则出牌时卡牌会卡在放大状态
        cardVisual.localScale = originalScale;
        cardVisual.localPosition = isHovering ? originalPosition : restLocalPosition;
        isHovering = false;
    }
}