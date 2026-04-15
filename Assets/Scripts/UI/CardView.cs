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

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isHovering = false;

    public CardData currentCard { get; private set; }

    private void Awake()
    {
        Debug.Log($"【CardView.Awake】开始执行，cardVisual 当前值: {(cardVisual == null ? "NULL" : cardVisual.name)}");

        if (cardVisual == null)
        {
            Debug.Log($"【CardView.Awake】cardVisual 为 NULL，开始查找...");

            // 第1步：打印整个 GameObject 的子物体树
            Debug.Log($"【CardView】当前 GameObject: {gameObject.name}，所有子物体：");
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                Debug.Log($"  └─ {child.name} (深度: {GetHierarchyDepth(child, transform)})");
            }

            // 第2步：查找 CardVisual
            RectTransform[] allChildren = GetComponentsInChildren<RectTransform>();
            Debug.Log($"【CardView】找到 {allChildren.Length} 个 RectTransform");

            foreach (RectTransform child in allChildren)
            {
                Debug.Log($"  检查: {child.name}");
                if (child.name == "CardVisual")
                {
                    Debug.Log($"  ✅ 找到 CardVisual！");
                    cardVisual = child;
                    break;
                }
            }

            if (cardVisual == null)
            {
                Debug.LogError($"【CardView】最终还是没找到 CardVisual！GameObject: {gameObject.name}");
                return;
            }
            else
            {
                Debug.Log($"【CardView】✅ 成功找到 CardVisual: {cardVisual.name}");
            }
        }

        if (cardVisual != null)
        {
            originalScale = cardVisual.localScale;
        }
    }

    // 辅助函数：计算层级深度
    private int GetHierarchyDepth(Transform target, Transform root)
    {
        int depth = 0;
        Transform current = target;
        while (current != root && current != null)
        {
            depth++;
            current = current.parent;
        }
        return depth;
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

        if (cardNameText != null)
            cardNameText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(data.rareColor)}>{data.cardName}</color>\n<size=24>效果: {data.damage}</size>";

        if (cardCostText != null)
            cardCostText.text = data.cost.ToString();

        if (cardDescriptionText != null)
            cardDescriptionText.text = data.description;

        if (cardBackground != null && data.cardBackgroundSprite != null)
            cardBackground.sprite = data.cardBackgroundSprite;
    }

    public void SetCardInteractable(bool canClick)
    {
        if (cardButton != null)
            cardButton.interactable = canClick;

        if (cardBackground != null)
            cardBackground.canvasRenderer.SetAlpha(canClick ? 1f : 0.5f);
    }

    public void SetEnergyInsufficient(bool isInsufficient)
    {
        if (cardCostText != null)
        {
            cardCostText.color = isInsufficient ? Color.red : Color.white;
        }
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
        if (cardButton != null && cardButton.interactable && cardVisual != null)
        {
            if (!isHovering)
            {
                originalPosition = cardVisual.localPosition;
                isHovering = true;
            }

            cardVisual.localScale = originalScale * hoverScale;
            cardVisual.localPosition = originalPosition + Vector3.up * hoverYOffset;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cardButton != null && !cardButton.interactable) return;
        if (cardVisual == null) return;

        cardVisual.localScale = originalScale;
        cardVisual.localPosition = originalPosition;
        isHovering = false;
    }
}