using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景的背景层。
/// BattleScene 本身没有背景图层，用户看到的纯色就是相机的 solid color，
/// 所以这里在 Canvas 最底层塞一张全屏 Image（SetAsFirstSibling），
/// 不碰场景文件，删掉这个脚本就回到纯色底。
/// </summary>
public static class BattleBackdrop
{
    public const string SpritePath = "Art/battle_bg";
    public const string ObjectName = "FutaBackdrop";

    public static Image Ensure(Transform anyInCanvas)
    {
        Canvas canvas = anyInCanvas != null ? anyInCanvas.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("【BattleBackdrop】场景里找不到 Canvas，背景层跳过");
            return null;
        }

        Transform existing = canvas.transform.Find(ObjectName);
        if (existing != null) return existing.GetComponent<Image>();

        Sprite sprite = Resources.Load<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogWarning("【BattleBackdrop】缺少 Resources/" + SpritePath + ".png，背景层跳过");
            return null;
        }

        GameObject go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = false;   // 绝不能吃掉手牌和按钮的点击
        img.color = new Color(0.92f, 0.90f, 0.96f, 1f);

        go.transform.SetAsFirstSibling();
        return img;
    }
}
