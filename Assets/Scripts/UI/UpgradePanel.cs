using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using CardCombat;

/// <summary>
/// 应急充能站的第三个选项：把牌组里的一张牌强化一档。
/// 编辑器在这台机器上起不来（Tuxedo 加密狗），所以整个面板是运行时用代码搭的，
/// 不依赖场景里任何预制体 —— 和天赋弹窗、事件弹窗同一套做法。
/// </summary>
public class UpgradePanel : MonoBehaviour
{
    private const float PanelW = 1180f;
    private const float HeaderH = 210f;
    private const float BottomPad = 40f;
    private const int Cols = 4;
    private const float CellW = 264f;
    private const float CellH = 76f;
    private const float StepX = 276f;
    private const float StepY = 90f;

    private RectTransform _panel;
    private Transform _grid;

    public static void Open()
    {
        if (GameManager.Instance == null) return;
        if (FindObjectOfType<UpgradePanel>() != null) return;

        Transform canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[强化] 没找到 Canvas");
            return;
        }

        GameObject root = new GameObject("UpgradePanel", typeof(RectTransform), typeof(Image), typeof(UpgradePanel));
        root.transform.SetParent(canvas, false);
        root.transform.SetAsLastSibling();
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image scrim = root.GetComponent<Image>();
        scrim.color = new Color(0.012f, 0.012f, 0.018f, 0.88f);
        scrim.raycastTarget = true;

        UpgradePanel self = root.GetComponent<UpgradePanel>();
        self.Build(root.transform);
    }

    private static Transform FindCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        Transform best = null;
        int bestOrder = int.MinValue;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.isRootCanvas) return c.transform;
            if (c.sortingOrder > bestOrder)
            {
                bestOrder = c.sortingOrder;
                best = c.transform;
            }
        }
        return best;
    }

    private void Build(Transform root)
    {
        List<CardData> deck = GameManager.Instance.playerDeck;
        if (deck == null) deck = new List<CardData>();
        int rows = Mathf.Max(1, Mathf.CeilToInt(deck.Count / (float)Cols));
        float gridH = rows * StepY;
        float panelH = HeaderH + gridH + BottomPad;

        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(root, false);
        RectTransform prt = panelObj.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(PanelW, panelH);
        prt.anchoredPosition = Vector2.zero;
        Image pimg = panelObj.GetComponent<Image>();
        UiSkin.Frame(pimg, new Color(0.046f, 0.050f, 0.076f, 0.99f), new Color(0.72f, 0.90f, 0.86f, 0.32f), 2, 18);
        pimg.type = Image.Type.Sliced;
        _panel = prt;

        UiSkin.CreateText(prt, "Kicker", "SANDBOX · 强化台",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(600f, 24f),
            14f, new Color(0.62f, 0.86f, 0.80f), TextAlignmentOptions.Center);

        UiSkin.CreateText(prt, "Title", "强化一张牌",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(900f, 52f),
            38f, UiSkin.Ink, TextAlignmentOptions.Center);

        UiSkin.CreateText(prt, "Sub", "这一局永久生效 · 每张牌只能强化一次",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(900f, 26f),
            16f, new Color(0.64f, 0.72f, 0.72f), TextAlignmentOptions.Center);

        Button close = UiSkin.CreateButton(prt, "Close", "×",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(52f, 52f), 26f);
        close.onClick.AddListener(Close);

        GameObject gridObj = new GameObject("Grid", typeof(RectTransform));
        gridObj.transform.SetParent(prt, false);
        RectTransform grt = gridObj.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 1f);
        grt.anchorMax = new Vector2(0.5f, 1f);
        grt.pivot = new Vector2(0.5f, 1f);
        grt.sizeDelta = new Vector2(StepX * Cols, gridH);
        grt.anchoredPosition = new Vector2(0f, -HeaderH);
        _grid = gridObj.transform;

        BuildCards();
    }

    private void BuildCards()
    {
        for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);

        List<CardData> deck = GameManager.Instance.playerDeck;
        for (int i = 0; i < deck.Count; i++)
        {
            CardData data = deck[i];
            if (data == null) continue;

            string baseId = string.IsNullOrEmpty(data.id) ? CardLibrary.MapChinese(data.cardName) : data.id;
            bool already = CardLibrary.IsUpgraded(baseId);
            string label = already ? (data.cardName + "（已强化）") : data.cardName;
            string preview = already ? "—" : PreviewText(baseId);

            int col = i % Cols;
            int row = i / Cols;
            float x = (col - (Cols - 1) * 0.5f) * StepX;
            float y = -row * StepY;

            Button btn = UiSkin.CreateButton(_grid, "Card_" + i, "", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(CellW, CellH), 18f);
            btn.GetComponent<Image>().raycastTarget = true;

            // CreateButton 自带一个居中的 Label，这里关掉，自己排两行（名字 + 变化量）。
            Transform label0 = btn.transform.Find("Label");
            if (label0 != null) label0.gameObject.SetActive(false);

            TextMeshProUGUI name = UiSkin.CreateText(btn.transform, "Name", label,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 12f), new Vector2(CellW - 28f, 26f),
                18f, UiSkin.Ink, TextAlignmentOptions.Left);

            TextMeshProUGUI info = UiSkin.CreateText(btn.transform, "Info", preview,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -13f), new Vector2(CellW - 28f, 22f),
                13.5f, new Color(0.62f, 0.86f, 0.80f), TextAlignmentOptions.Left);

            if (already)
            {
                btn.interactable = false;
                name.color = new Color(0.52f, 0.58f, 0.58f);
                info.text = "已经强化过了";
                info.color = new Color(0.46f, 0.52f, 0.52f);
                continue;
            }

            int captured = i;
            btn.onClick.AddListener(() => Pick(captured));
        }
    }

    /// <summary>把"这张牌强化后会变多少"写成一行，别让玩家去猜。</summary>
    private static string PreviewText(string id)
    {
        CardDef a = CardLibrary.Get(id);
        CardDef b = CardLibrary.GetUpgraded(id);
        if (a == null || b == null) return "";

        List<string> parts = new List<string>();
        if (b.Damage != a.Damage)
        {
            parts.Add(a.HitsPerTick > 0
                ? "每格 " + a.Damage + " → " + b.Damage
                : "伤害 " + a.Damage + " → " + b.Damage);
        }
        if (b.Block != a.Block) parts.Add("格挡 " + a.Block + " → " + b.Block);
        if (b.Heal != a.Heal) parts.Add("回复 " + a.Heal + " → " + b.Heal);
        if (b.Draw != a.Draw) parts.Add("抽 " + a.Draw + " → " + b.Draw);
        if (b.Cost != a.Cost) parts.Add("灵能 " + a.Cost + " → " + b.Cost);
        if (b.SelfDamage != a.SelfDamage) parts.Add("自伤 " + a.SelfDamage + " → " + b.SelfDamage);
        if (b.GaugeDamagePercent != a.GaugeDamagePercent)
            parts.Add("回收 ×" + (a.GaugeDamagePercent / 100f).ToString("0.##") + " → ×" + (b.GaugeDamagePercent / 100f).ToString("0.##"));

        return parts.Count == 0 ? "稍微变强一点" : string.Join(" · ", parts.ToArray());
    }

    private void Pick(int index)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.playerDeck == null || index < 0 || index >= gm.playerDeck.Count) return;

        CardData data = gm.playerDeck[index];
        string baseId = string.IsNullOrEmpty(data.id) ? CardLibrary.MapChinese(data.cardName) : data.id;
        if (CardLibrary.IsUpgraded(baseId)) return;

        CardData upgraded = DeckFactory.FromId(CardLibrary.UpgradeOf(baseId));
        if (upgraded == null) return;

        gm.playerDeck[index] = upgraded;
        Debug.Log("[强化] " + data.cardName + " → " + upgraded.cardName);
        SaveSystem.Save();
        Close();
        SceneManager.LoadScene("MapScene");
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}
