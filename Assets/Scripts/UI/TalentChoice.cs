using System.Collections.Generic;
using CardCombat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天赋二选一的弹窗。
///
/// 挂在地图场景上：每打完第 3 / 6 / 9 层回到地图，如果还有天赋位没选，
/// 就先把这张盖上来（Modal，必须选一张才能继续走）。
///
/// 全部运行时建：编辑器起不来（团结引擎 140063），场景文件不能改，
/// 所以跟 PauseMenu、主菜单的"继续游戏"一样，用代码搭。
/// </summary>
public class TalentChoice : MonoBehaviour
{
    private static TalentChoice _instance;

    private RectTransform _panel;
    private RectTransform _optionRoot;
    private TextMeshProUGUI _sub;

    /// <summary>地图生成完之后调一次。没有待选天赋就什么都不做。</summary>
    public static void TryShow()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || _instance != null) return;
        if (gm.PendingTalents() <= 0) return;

        Canvas canvas = FindCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("TalentChoice", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        _instance = go.AddComponent<TalentChoice>();
        _instance.Build(rt);
    }

    private static Canvas FindCanvas()
    {
        Canvas[] all = Object.FindObjectsOfType<Canvas>();
        Canvas fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == null) continue;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            if (fallback == null || c.sortingOrder > fallback.sortingOrder) fallback = c;
        }
        return fallback;
    }

    private void Build(RectTransform root)
    {
        // 半透明压暗层：把地图压下去，视线只剩这两张牌
        Image scrim = root.gameObject.AddComponent<Image>();
        scrim.color = new Color(0.02f, 0.02f, 0.03f, 0.92f);
        scrim.raycastTarget = true;

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(root, false);
        _panel = panelObj.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(1160f, 620f);
        _panel.anchoredPosition = Vector2.zero;
        UiSkin.Frame(panelObj.GetComponent<Image>(),
            new Color(0.048f, 0.052f, 0.078f, 0.98f), new Color(0.72f, 0.90f, 0.86f, 0.32f), 2, 18);

        UiSkin.CreateText(_panel, "Kicker", "INSTINCT · 本能", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 248f), new Vector2(800f, 24f), 15f, new Color(0.72f, 0.90f, 0.86f, 0.75f));

        UiSkin.CreateText(_panel, "Title", "这一层又长出一条", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 200f), new Vector2(900f, 52f), 38f, new Color(0.953f, 0.937f, 0.902f, 1f));

        _sub = UiSkin.CreateText(_panel, "Sub", "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 170f), new Vector2(1000f, 30f), 17f, new Color(0.64f, 0.72f, 0.72f, 0.9f));

        GameObject optionRoot = new GameObject("Options", typeof(RectTransform));
        optionRoot.transform.SetParent(_panel, false);
        _optionRoot = optionRoot.GetComponent<RectTransform>();
        _optionRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _optionRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _optionRoot.pivot = new Vector2(0.5f, 0.5f);
        _optionRoot.sizeDelta = new Vector2(1040f, 420f);
        _optionRoot.anchoredPosition = new Vector2(0f, -66f);

        Rebuild();
    }

    private void Rebuild()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) { Close(); return; }
        if (gm.PendingTalents() <= 0) { Close(); return; }

        for (int i = _optionRoot.childCount - 1; i >= 0; i--)
            Destroy(_optionRoot.GetChild(i).gameObject);

        int stage = Mathf.Max(1, gm.currentStageIndex);
        int owner = OwnerOf(gm);
        if (_sub != null)
        {
            _sub.text = "还会长 " + gm.PendingTalents() + " 次";
        }

        List<string> offers = TalentLibrary.RollChoices(owner, gm.talents,
            new SeededRandom(System.Environment.TickCount), 2);
        if (offers.Count == 0) { Close(); return; }

        float step = 500f;
        float startX = -(offers.Count - 1) * step * 0.5f;
        for (int i = 0; i < offers.Count; i++)
        {
            string id = offers[i];
            TalentDef def = TalentLibrary.Get(id);
            if (def == null) continue;
            BuildOption(def, startX + i * step);
        }
    }

    private void BuildOption(TalentDef def, float x)
    {
        Button btn = UiSkin.CreateButton(_optionRoot, "Talent_" + def.Id, "",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(460f, 380f), 24f);
        UiSkin.Frame(btn.GetComponent<Image>(),
            new Color(0.070f, 0.075f, 0.105f, 0.98f), new Color(0.72f, 0.90f, 0.86f, 0.22f), 2, 16);

        // 按钮里那张空 Label 会占位置，直接关掉，改成自己排的三段文字
        Transform label = btn.transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(btn.transform, false);
        RectTransform ir = iconObj.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.5f, 1f);
        ir.anchorMax = new Vector2(0.5f, 1f);
        ir.pivot = new Vector2(0.5f, 1f);
        ir.sizeDelta = new Vector2(132f, 132f);
        ir.anchoredPosition = new Vector2(0f, -34f);
        Image icon = iconObj.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        Sprite sprite = UiSkin.LoadSprite("Art/Talents/" + def.Icon);
        if (sprite != null) icon.sprite = sprite;

        UiSkin.CreateText(btn.transform, "Name", def.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -180f), new Vector2(420f, 44f), 30f, new Color(0.953f, 0.937f, 0.902f, 1f));

        UiSkin.CreateText(btn.transform, "Desc", def.Desc, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(380f, 110f), 18f, new Color(0.64f, 0.72f, 0.72f, 1f),
            TextAlignmentOptions.Top);

        string id = def.Id;
        btn.onClick.AddListener(() => Pick(id));
    }

    /// <summary>这一局是谁（0 清道夫 / 1 解剖师），本能按角色分池。</summary>
    private static int OwnerOf(GameManager gm)
    {
        if (gm == null || gm.currentCharacter == null) return TalentLibrary.Sweeper;
        return gm.currentCharacter.passiveSkill == PassiveSkillType.Dissector_Tempo
            ? TalentLibrary.Dissector : TalentLibrary.Sweeper;
    }

    private void Pick(string id)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) { Close(); return; }

        if (!gm.AddTalent(id)) { Close(); return; }

        SaveSystem.Save();
        Debug.Log("[天赋] 拿到「" + id + "」，本局共 " + gm.talents.Count + " 条");

        if (gm.PendingTalents() > 0) Rebuild();
        else Close();
    }

    private void Close()
    {
        _instance = null;
        Destroy(gameObject);
    }

    private void Update()
    {
        // 弹窗是强制的：不给 Esc 逃课，避免"少选一个天赋就继续打"这种状态。
        // 但允许 F1 之类的调试键？不做，保持简单。
    }
}
