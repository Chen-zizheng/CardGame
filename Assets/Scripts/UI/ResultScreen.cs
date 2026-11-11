using CardCombat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 一局"打完了"之后的那一屏。
//
// 之前的问题：胜利分支走到 `stage >= totalStageCount` 就直接回主菜单，
// 而工程里根本没有 EndingScene.scene（只有一段引用它的死代码 EndingController），
// 所以这游戏没有结局，打完三场被弹回菜单 = 玩家觉得"没做完"。
//
// 现在结局屏和失败屏共用同一个场景（FailScene），内容按 GameManager.runWon 分支。
// 有意不为它新建 .scene：这份工程里的 guid 是加密 base64，手工给新场景编 guid 风险太大。
// 想拆成两个场景时，只要把 SceneName 的判断改成场景名即可。
public static class ResultScreen
{
    private const string ResultScene = "FailScene";

    private static readonly Color Ink = new Color(0.953f, 0.937f, 0.902f, 1f);
    private static readonly Color Dim = new Color(0.64f, 0.72f, 0.72f, 1f);

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
        if (sceneName != ResultScene && sceneName != "EndingScene") return;

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;
        if (canvas.transform.Find("ResultRoot") != null) return;

        Build(canvas.transform);
    }

    private static void Build(Transform canvas)
    {
        GameManager gm = GameManager.Instance;
        bool won = gm != null && gm.runWon;

        // 老场景里那几行固定文字和按钮全部关掉，只留背景层
        for (int i = canvas.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.GetChild(i);
            string n = child.name;
            if (n == "SceneBackdrop" || n == "FutaBackdrop") continue;
            child.gameObject.SetActive(false);
        }

        GameObject root = new GameObject("ResultRoot", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        RectTransform rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.DOFade(1f, 0.45f).SetUpdate(true);

        // 压一层黑，让星云退到后面去
        Image scrim = UiSkin.CreatePanel(root.transform, "Scrim", new Color(0.035f, 0.032f, 0.05f, 0.62f));
        scrim.raycastTarget = true;

        int total = gm != null ? Mathf.Max(1, gm.totalStageCount) : 10;
        int depth = gm != null ? Mathf.Clamp(gm.runMaxStage, 1, total) : 1;
        int wins = gm != null ? Mathf.Max(0, gm.runWins) : 0;
        int deck = gm != null && gm.playerDeck != null ? gm.playerDeck.Count : 0;
        int gold = gm != null ? gm.currentGold : 0;
        int seconds = gm != null ? gm.RunElapsedSeconds() : 0;

        UiSkin.CreateText(root.transform, "Kicker", won ? "FIELD CLEARED" : "SIGNAL LOST",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 286f), new Vector2(900f, 28f),
            15f, new Color(Dim.r, Dim.g, Dim.b, 0.70f)).characterSpacing = 12f;

        TextMeshProUGUI title = UiSkin.CreateText(root.transform, "Title", won ? "清场完成" : "被同化",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 192f), new Vector2(1000f, 120f),
            82f, Ink);
        title.characterSpacing = 22f;

        UiSkin.CreateText(root.transform, "Subtitle",
            won ? (total + " 层走到底，处置记录已封存") : ("你在第 " + depth + " 层失去了信号"),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 128f), new Vector2(1000f, 34f),
            21f, new Color(Dim.r, Dim.g, Dim.b, 0.92f));

        GameObject rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(root.transform, false);
        UiSkin.SetRect(rule, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 84f), new Vector2(120f, 2f));
        Image ruleImg = rule.GetComponent<Image>();
        ruleImg.color = new Color(UiSkin.Mint.r, UiSkin.Mint.g, UiSkin.Mint.b, 0.45f);
        ruleImg.raycastTarget = false;

        // 两列：左边是"走到哪、剩下什么"，右边是"这一局你干了什么"。
        // 单列五行的时候，出牌数/伤害这些最该被看见的数根本没地方放。
        int relics = gm != null && gm.relics != null ? gm.relics.Count : 0;
        int talents = gm != null && gm.talents != null ? gm.talents.Count : 0;
        int events = gm != null && gm.eventsSeen != null ? gm.eventsSeen.Count : 0;
        int played = gm != null ? gm.statCardsPlayed : 0;
        int dealt = gm != null ? gm.statDamageDealt : 0;
        int blocked = gm != null ? gm.statDamageBlocked : 0;
        int stops = gm != null ? gm.statInterrupts : 0;
        int kills = gm != null ? gm.statKills : 0;

        // 强化牌张数：牌组里能看，结算时也该有个交代 —— 篝火选几次强化是这一局最重的几个决定之一。
        int upgraded = 0;
        if (gm != null && gm.playerDeck != null)
        {
            foreach (CardData c in gm.playerDeck)
                if (c != null && CardLibrary.IsUpgraded(c.id)) upgraded++;
        }

        float y = 60f;
        AddRow(root.transform, "深度", depth + " / " + total, -330f, y);
        AddRow(root.transform, "清场", wins + " 次", 330f, y); y -= 46f;
        AddRow(root.transform, "牌组", deck + " 张" + (upgraded > 0 ? " · 强 " + upgraded : ""), -330f, y);
        AddRow(root.transform, "击杀", kills + " 只", 330f, y); y -= 46f;
        AddRow(root.transform, "用时", FormatTime(seconds), -330f, y);
        AddRow(root.transform, "出牌", played + " 张", 330f, y); y -= 46f;
        AddRow(root.transform, "补给点数", gold.ToString(), -330f, y);
        AddRow(root.transform, "造成伤害", dealt.ToString(), 330f, y); y -= 46f;
        AddRow(root.transform, "挡下伤害", blocked.ToString(), -330f, y);
        AddRow(root.transform, "卸力", stops + " 次", 330f, y);

        UiSkin.CreateText(root.transform, "Loadout",
            "遗物 " + relics + " 件 · 本能 " + talents + " 条 · 事件 " + events + " 个",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -142f), new Vector2(1000f, 26f),
            17f, new Color(Dim.r, Dim.g, Dim.b, 0.80f));

        if (won)
        {
            Button menu = UiSkin.CreateButton(root.transform, "MenuButton", "回主菜单",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(320f, 68f), 24f);
            StylePrimary(menu);
            menu.onClick.AddListener(LoadMainMenu);
        }
        else
        {
            Button retry = UiSkin.CreateButton(root.transform, "RetryButton", "再次派遣",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-172f, -262f), new Vector2(320f, 68f), 24f);
            StylePrimary(retry);
            retry.onClick.AddListener(RetryStage);

            Button menu = UiSkin.CreateButton(root.transform, "MenuButton", "回主菜单",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(172f, -262f), new Vector2(320f, 68f), 24f);
            menu.onClick.AddListener(LoadMainMenu);
        }
    }

    /// <summary>一行"标签 值"，cx 是这一列的中缝（左列 -330 / 右列 +330）。</summary>
    private static void AddRow(Transform parent, string label, string value, float cx, float y)
    {
        UiSkin.CreateText(parent, "Row_" + label, label,
            new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(cx - 14f, y), new Vector2(240f, 32f),
            19f, new Color(Dim.r, Dim.g, Dim.b, 0.85f), TextAlignmentOptions.Right);

        UiSkin.CreateText(parent, "RowVal_" + label, value,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(cx + 14f, y), new Vector2(240f, 32f),
            21f, Ink, TextAlignmentOptions.Left);
    }

    private static void StylePrimary(Button btn)
    {
        if (btn == null) return;
        Color paper = new Color(0.93f, 0.92f, 0.88f, 0.95f);
        UiSkin.StyleButton(btn, 24f, paper, paper, UiSkin.Dark);
        TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.characterSpacing = 6f;
    }

    private static string FormatTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }

    private static void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private static void RetryStage()
    {
        if (GameManager.Instance != null)
        {
            SceneManager.LoadScene("BattleScene");
            return;
        }
        SceneManager.LoadScene("MainMenu");
    }
}
