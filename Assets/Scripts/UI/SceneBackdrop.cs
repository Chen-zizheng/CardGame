using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 每个场景一层背景。之前只有战斗界面有背景，其余场景是纯黑（营地）或灰板（地图/选人），
// 所以整款游戏看起来"荒芜"。这里按场景名挂不同的一张星云，连换场都不用手动改场景文件。
public static class SceneBackdrop
{
    private static readonly Dictionary<string, string> Backgrounds = new Dictionary<string, string>
    {
        { "MainMenu",              "Art/menu_bg" },
        { "CharacterSelectScene",  "Art/select_bg" },
        { "MapScene",              "Art/map_bg" },
        { "CampfireScene",         "Art/campfire_bg" },
        { "ShopScene",             "Art/shop_bg" },
        { "RewardScene",           "Art/map_bg" },
        { "EndingScene",           "Art/end_bg" },
        { "FailScene",             "Art/end_bg" },
        { "BattleScene",           "Art/battle_bg" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene.name);
    }

    public static void Apply(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (canvas.transform.Find("SceneBackdrop") != null) return;
        if (canvas.transform.Find("FutaBackdrop") != null) return; // 战斗界面自己挂过了

        string path;
        if (!Backgrounds.TryGetValue(sceneName, out path)) path = "Art/menu_bg";

        Sprite sprite = LoadSprite(path);
        if (sprite == null)
        {
            Debug.LogWarning("【背景】找不到 " + path + "，检查 Assets/Resources/Art 下的贴图");
            return;
        }

        GameObject go = new GameObject("SceneBackdrop", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = false;

        HidePlaceholderBlocks(canvas.transform);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        // 新加的 PNG 若被导入成 Default 而不是 Sprite，运行时自己包一层
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // 老场景里那些"没有贴图的纯色块"（叫 background / bg / panel 的）会把新背景盖住
    private static void HidePlaceholderBlocks(Transform canvas)
    {
        RectTransform canvasRect = canvas as RectTransform;
        float canvasArea = 1f;
        if (canvasRect != null)
        {
            Vector2 size = canvasRect.rect.size;
            if (size.x > 1f && size.y > 1f) canvasArea = size.x * size.y;
        }

        // 主菜单有一层 768×512 的旧 RawImage 背景，被拉伸到 1920×1080 后很糊，
        // 而且它排在 SceneBackdrop 之后会把新背景完全盖住 —— 直接关掉
        foreach (RawImage raw in canvas.GetComponentsInChildren<RawImage>(true))
        {
            RectTransform rt = raw.rectTransform;
            if (rt == null) continue;
            Vector2 size = rt.rect.size;
            if (size.x * size.y < canvasArea * 0.45f) continue;
            if (raw.color.a < 0.25f) continue;
            raw.color = new Color(raw.color.r, raw.color.g, raw.color.b, 0f);
            raw.raycastTarget = false;
        }

        foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
        {
            Color c = image.color;

            if (image.sprite == null)
            {
                string n = image.gameObject.name.ToLowerInvariant();
                if (n == "background" || n == "bg" || n == "backdrop" || n == "panel" || n == "menupanel")
                {
                    if (c.a > 0.02f && c.a < 0.35f) continue;   // 已经是淡淡的底，保留
                    image.color = new Color(c.r, c.g, c.b, 0f);
                }
                continue;
            }

            // 半透明的白色大板（地图的 Scroll View、牌库面板之类）在深色星云上会变成一块灰，
            // 按"覆盖半屏以上 + 接近纯白"判定，压到几乎透明，只留一点分层。
            RectTransform rt = image.rectTransform;
            if (rt == null) continue;
            Vector2 s = rt.rect.size;
            if (s.x * s.y < canvasArea * 0.45f) continue;
            bool nearWhite = Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 0.6f;
            if (!nearWhite || c.a < 0.25f) continue;
            image.color = new Color(c.r, c.g, c.b, 0.10f);
        }
    }
}
