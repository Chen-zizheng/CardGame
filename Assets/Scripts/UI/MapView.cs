using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using CardCombat;

public class MapView : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("拖入 Scroll View 里面的 Content 物体")]
    public RectTransform contentParent;
    public GameObject mapNodePrefab;
    public GameObject mapLinePrefab;

    [Header("节点类型颜色")]
    public Color normalColor = Color.white;
    public Color eliteColor = new Color(1f, 0.4f, 0.4f); // 偏红
    public Color campfireColor = new Color(1f, 0.6f, 0.2f); // 橘色
    public Color shopColor = Color.yellow;
    public Color bossColor = Color.magenta;

    [Header("节点状态颜色（透明度）")]
    public float lockedAlpha = 0.4f;     // 没解锁的灰暗
    public float accessibleAlpha = 1.0f; // 可以点击的高亮
    public float visitedAlpha = 0.2f;    // 走过的变半透明

    private const float NodeSize = 84f;
    private const float ContentWidth = 1240f;
    private const float EdgePadding = 70f;

    // 地图节点在 Content 里的坐标原点（生成时整叠居中用）
    private Vector2 layoutOrigin;

    // 默认缩放：1.6 倍于"刚好入屏"，即一屏看 5 层上下（地图比屏幕长）
    private const float DefaultZoom = 1.6f;

    private MapDrag drag;
    private RectTransform viewport;
    private Canvas canvas;

    // 拆掉 ScrollRect / Mask：Content 直接铺在 Canvas 中央，节点不再被视口裁掉
    private void PrepareFlatCanvas()
    {
        if (contentParent == null) return;

        viewport = contentParent.parent as RectTransform;
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();

        if (viewport != null)
        {
            Mask mask = viewport.GetComponent<Mask>();
            if (mask != null) Destroy(mask);

            RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
            if (rectMask != null) Destroy(rectMask);

            Image vpImage = viewport.GetComponent<Image>();
            if (vpImage != null)
            {
                vpImage.color = new Color(1f, 1f, 1f, 0f);
                vpImage.raycastTarget = false;
            }

            ScrollRect scroll = viewport.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.enabled = false;
                Image bg = scroll.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = new Color(1f, 1f, 1f, 0f);
                    bg.raycastTarget = false;
                }
            }

            EnsureDragSurface();
        }

        contentParent.anchorMin = new Vector2(0.5f, 0.5f);
        contentParent.anchorMax = new Vector2(0.5f, 0.5f);
        contentParent.pivot = new Vector2(0.5f, 0.5f);
        contentParent.anchoredPosition = Vector2.zero;
        contentParent.localScale = Vector3.one;
    }

    // 拖动用的透明板：排在 Content 前面，所以不会挡住节点的点击
    private void EnsureDragSurface()
    {
        Transform existing = viewport.Find("MapDragSurface");
        if (existing != null)
        {
            drag = existing.GetComponent<MapDrag>();
            if (drag == null) drag = existing.gameObject.AddComponent<MapDrag>();
            return;
        }

        GameObject go = new GameObject("MapDragSurface", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(viewport, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        drag = go.AddComponent<MapDrag>();
    }

    private void Start()
    {
        // 进地图就把"这一局要打几层"对齐到地图的实际层数。
        // 选人时 MapManager 可能还不存在，那时候拿到的是场景里的旧值（3）。
        if (GameManager.Instance != null) GameManager.Instance.SyncStageCountFromMap();

        // 延迟0.1秒执行，确保 MapManager 先把地图数据生成完毕
        Invoke("GenerateUI", 0.1f);
    }

    private void GenerateUI()
    {
        if (MapManager.Instance == null || mapNodePrefab == null || mapLinePrefab == null) return;

        // 回到地图 = 上一场已经结清（血量、金币、牌组都结算过了），这里是最稳的存盘点。
        // 必须放在这里而不是 Start：Start 有可能比 MapManager 生成地图更早跑，
        // 那时节点表还是空的，存下去的地图会是一张空图。
        SaveSystem.Save();

        BuildMapVisuals(false);
    }

    /// <summary>
    /// 事件结算完之后原地重画（节点状态变了：走过的、能走的都换了）。
    ///
    /// 不重载场景：MapManager 是 DontDestroyOnLoad，重载只会把同一张图再摆一遍，
    /// 而玩家应该还站在刚才那个位置。所以这里清掉 contentParent 的子物体再画一次，
    /// 用 preserveView 把缩放和纵向位置接回去。
    /// </summary>
    public void RebuildMap()
    {
        if (MapManager.Instance == null || mapNodePrefab == null || mapLinePrefab == null) return;
        SaveSystem.Save();
        BuildMapVisuals(true);
    }

    private void BuildMapVisuals(bool preserveView)
    {
        int totalLayers = MapManager.Instance.mapConfig.totalLayers;
        float layerSpacing = MapManager.Instance.mapConfig.layerVerticalSpacing;

        PrepareFlatCanvas();

        // 10 层 × 150 = 1350，加上节点和上下留白约 1570（旧的 7 层是 1120）。
        // fitScale = 整张图刚好装进一屏的比例；实际显示 = fitScale × zoom，
        // 默认 zoom 1.6 → 一屏只能看到 5 层左右，剩下的靠拖动，像杀戮尖塔那样"图比屏幕长"。
        float contentHeight = (totalLayers - 1) * layerSpacing + NodeSize + EdgePadding * 2f;
        float viewportHeight = viewport != null && viewport.rect.height > 100f ? viewport.rect.height : 1080f;
        float fitScale = Mathf.Clamp((viewportHeight - 24f) / contentHeight, 0.45f, 1f);

        contentParent.sizeDelta = new Vector2(ContentWidth, contentHeight);
        contentParent.localScale = Vector3.one;

        // 重画之前先把上一轮的节点和线收掉。直接 Destroy 的话它们会活到帧末，
        // 新节点压上旧节点，一帧里会出现两层圆盘；所以先脱父再销毁。
        float keepY = contentParent.anchoredPosition.y;
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        // 整叠节点在竖直方向居中：层 0（起点）在下，Boss 在上
        layoutOrigin = new Vector2(0f, -(totalLayers - 1) * layerSpacing * 0.5f);

        CreateScrim();
        DrawAllLines(totalLayers);
        DrawAllNodes(totalLayers);

        if (drag != null && viewport != null)
        {
            drag.minZoom = 0.72f;   // 缩到底能勉强看到全图，用来"远眺"
            drag.maxZoom = 2.8f;
            float startZoom = preserveView && drag.Zoom > 0.01f ? drag.Zoom : DefaultZoom;
            drag.Configure(contentParent, viewport, canvas, fitScale, startZoom);
            if (preserveView)
            {
                // Configure 会把视角打回最底层，这里把玩家刚才看到的位置接回去
                Vector2 p = contentParent.anchoredPosition;
                p.y = keepY;
                contentParent.anchoredPosition = p;
                drag.RecalculateBounds();
            }
            else
            {
                drag.JumpToBottom();
            }
        }

        CreateZoomControls();
        CreateZoomHint();

        // 打完第 3/6/9 层会攒下一次天赋选择。回到地图时补弹窗——
        // 放在地图建好之后，这样弹窗关掉时底下已经是一张能点的图。
        TalentChoice.TryShow();

        Debug.Log("【MapView】可视化地图生成完毕！层数=" + totalLayers
                  + " 适配缩放=" + fitScale.ToString("0.00")
                  + " 默认缩放=" + DefaultZoom.ToString("0.00"));
    }

    // 背景是一张暖橙色的战场图，节点直接压上去会糊成一团。
    // 盖一层 0.34 的黑，只为了让深色圆盘和细描边"跳"出来。
    private void CreateScrim()
    {
        if (canvas == null) return;
        if (canvas.transform.Find("MapScrim") != null) return;

        GameObject go = new GameObject("MapScrim", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.04f, 0.035f, 0.06f, 0.34f);
        img.raycastTarget = false;

        go.transform.SetSiblingIndex(1);
    }

    // 右侧的 ＋/－ 缩放按钮（没有滚轮也能用）
    private void CreateZoomControls()
    {
        if (canvas == null) return;
        if (canvas.transform.Find("MapZoomIn") != null) return;

        Button zoomIn = UiSkin.CreateButton(canvas.transform, "MapZoomIn", "＋",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 70f), new Vector2(56f, 56f), 28f);
        zoomIn.transform.SetAsLastSibling();
        zoomIn.onClick.AddListener(() => { if (drag != null) drag.ZoomBy(0.25f); });

        Button zoomOut = UiSkin.CreateButton(canvas.transform, "MapZoomOut", "－",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 6f), new Vector2(56f, 56f), 28f);
        zoomOut.transform.SetAsLastSibling();
        zoomOut.onClick.AddListener(() => { if (drag != null) drag.ZoomBy(-0.25f); });
    }

    private void CreateZoomHint()
    {
        if (canvas == null) return;
        if (canvas.transform.Find("MapZoomHint") != null) return;

        TextMeshProUGUI hint = UiSkin.CreateText(canvas.transform, "MapZoomHint", "点击发亮的节点前进 · ESC 菜单",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(860f, 30f),
            17f, new Color(UiSkin.Dim.r, UiSkin.Dim.g, UiSkin.Dim.b, 0.72f));
        hint.transform.SetAsLastSibling();
    }

    // 两遍画线：
    //   第一遍所有路都很淡（alpha 0.10）——只是"这张图有分叉"的信息，不该抢眼；
    //   第二遍把「当前能走的那几条」提亮（alpha 0.55、宽 3），视线自然被引到下一步。
    // 以前全部同亮度，十六个节点交叉出来的就是一团网。
    private void DrawAllLines(int totalLayers)
    {
        for (int layer = 0; layer < totalLayers - 1; layer++)
        {
            foreach (MapNode node in MapManager.Instance.GetNodesByLayer(layer))
            {
                foreach (int targetId in node.connectedNodeIds)
                {
                    MapNode targetNode = MapManager.Instance.GetNodeById(targetId);
                    if (targetNode != null)
                    {
                        Vector2 a = node.position + layoutOrigin;
                        Vector2 b = targetNode.position + layoutOrigin;
                        CreateLineBetween(a, b, 0.10f, 2f);
                        if (node.isAccessible) CreateLineBetween(a, b, 0.55f, 3f);
                    }
                }
            }
        }
    }

    private void CreateLineBetween(Vector2 startPos, Vector2 endPos, float alpha, float width)
    {
        GameObject lineObj = Instantiate(mapLinePrefab, contentParent);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        // 线的原点放在起点，锚点用 Content 的中心（和节点保持同一套坐标系）
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.anchoredPosition = startPos;
        lineRect.localScale = Vector3.one;

        float distance = Vector2.Distance(startPos, endPos);
        lineRect.sizeDelta = new Vector2(distance, width);

        float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;
        lineRect.localEulerAngles = new Vector3(0, 0, angle);

        Image lineImage = lineObj.GetComponent<Image>();
        if (lineImage != null) UiSkin.MapLine(lineImage, alpha, width);
    }

    private void DrawAllNodes(int totalLayers)
    {
        for (int layer = 0; layer < totalLayers; layer++)
        {
            foreach (MapNode node in MapManager.Instance.GetNodesByLayer(layer))
            {
                GameObject nodeObj = Instantiate(mapNodePrefab, contentParent);
                RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();

                nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.sizeDelta = new Vector2(NodeSize, NodeSize);
                nodeRect.anchoredPosition = node.position + layoutOrigin;
                nodeRect.localScale = Vector3.one;

                SetupNodeVisuals(nodeObj, node);

                Button btn = nodeObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.transition = Selectable.Transition.ColorTint;
                    btn.onClick.AddListener(() => OnNodeClicked(node));
                }
            }
        }
    }

    private void SetupNodeVisuals(GameObject nodeObj, MapNode nodeData)
    {
        Image bgImage = nodeObj.GetComponent<Image>();

        if (bgImage != null)
        {
            // 类型决定强调色，状态只影响描边亮度（UiSkin 里统一处理）
            // 类型由字形表达，描边颜色只在暖/冷两根轴上做轻微区分，
            // 不再有"红黄青洋红"四色齐飞。
            Color accent = new Color(0.72f, 0.90f, 0.86f);   // 薄荷：普通战斗
            string glyph = "m_combat";
            switch (nodeData.nodeType)
            {
                case MapNodeType.Elite:
                    accent = new Color(0.90f, 0.56f, 0.58f); glyph = "m_elite"; break;
                case MapNodeType.Campfire:
                    accent = new Color(0.96f, 0.78f, 0.56f); glyph = "m_fire"; break;
                case MapNodeType.Shop:
                    accent = new Color(0.90f, 0.86f, 0.64f); glyph = "m_shop"; break;
                case MapNodeType.Boss:
                    accent = new Color(0.86f, 0.58f, 0.86f); glyph = "m_boss"; break;
                case MapNodeType.Event:
                    // 冷青：和战斗的薄荷、精英的砖红都拉开，一眼是"这里不打架"
                    accent = new Color(0.62f, 0.86f, 0.96f); glyph = "m_event"; break;
            }

            UiSkin.MapNode(nodeObj, nodeData.isAccessible, nodeData.isVisited, accent, UiSkin.MapGlyph(glyph));

            Button btn = nodeObj.GetComponent<Button>();
            if (btn != null) btn.interactable = nodeData.isAccessible && !nodeData.isVisited;

            if (nodeData.isAccessible) PulseNode(nodeObj);
        }
    }

    // 可走的节点轻微呼吸：0.06 的振幅、0.9 秒一个来回，像在"闪一下"而不是抖
    private void PulseNode(GameObject nodeObj)
    {
        if (nodeObj == null) return;
        if (nodeObj.GetComponent<MapNodePulse>() != null) return;
        nodeObj.AddComponent<MapNodePulse>();
    }

    /// <summary>
    /// 玩家点击某个节点的逻辑
    /// </summary>
    private void OnNodeClicked(MapNode nodeData)
    {
        Debug.Log($"点击了节点 ID: {nodeData.nodeId}, 类型: {nodeData.nodeType}");

        nodeData.isVisited = true;
        nodeData.isAccessible = false;

        for (int i = 0; i < MapManager.Instance.mapConfig.totalLayers; i++)
        {
            foreach (var n in MapManager.Instance.GetNodesByLayer(i))
            {
                if (!n.isVisited) n.isAccessible = false;
            }
        }

        foreach (int nextId in nodeData.connectedNodeIds)
        {
            MapNode nextNode = MapManager.Instance.GetNodeById(nextId);
            if (nextNode != null) nextNode.isAccessible = true;
        }

        switch (nodeData.nodeType)
        {
            case MapNodeType.Normal:
                GameManager.Instance.currentEnemyLevel = 1;
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Elite:
                GameManager.Instance.currentEnemyLevel = 2;
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Boss:
                GameManager.Instance.currentEnemyLevel = 3;
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Campfire:
                SceneManager.LoadScene("CampfireScene");
                break;

            case MapNodeType.Shop:
                SceneManager.LoadScene("ShopScene");
                break;

            case MapNodeType.Event:
                OpenEvent();
                break;
        }
    }

    /// <summary>
    /// 事件格：抽一个这一局还没读过的事件，弹窗，读完关掉之后原地重画地图。
    /// 种子由「层数 + 地图种子」决定，所以同一张图上的同一个格子，
    /// 重开也在同一个位置（存读档不会变成另一个事件）。
    /// </summary>
    private void OpenEvent()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) { RebuildMap(); return; }

        int seed = gm.currentStageIndex * 7919;
        if (MapManager.Instance != null) seed ^= MapManager.Instance.mapSeed;

        EventDef def = EventLibrary.Roll(new SeededRandom(seed), gm.eventsSeen);
        if (def == null) { RebuildMap(); return; }

        if (gm.eventsSeen == null) gm.eventsSeen = new List<string>();
        if (!gm.eventsSeen.Contains(def.Id)) gm.eventsSeen.Add(def.Id);

        EventPanel.Open(def, RebuildMap);
    }
}
