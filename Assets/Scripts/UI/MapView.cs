using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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

    private void Start()
    {
        // 延迟0.1秒执行，确保 MapManager 先把地图数据生成完毕
        Invoke("GenerateUI", 0.1f);
    }

    private void GenerateUI()
    {
        if (MapManager.Instance == null || mapNodePrefab == null || mapLinePrefab == null) return;

        // 1. 纯粹地只计算和设置画布的“总高度”
        int totalLayers = MapManager.Instance.mapConfig.totalLayers;
        float layerSpacing = MapManager.Instance.mapConfig.layerVerticalSpacing;
        contentParent.sizeDelta = new Vector2(contentParent.sizeDelta.x, totalLayers * layerSpacing + 400f);

        // 2. 画线
        DrawAllLines(totalLayers);

        // 3. 画节点
        DrawAllNodes(totalLayers);

        // 4. 滚到底部（代码只负责交互，不负责排版）
        Invoke("ScrollToBottom", 0.05f);

        Debug.Log("【MapView】可视化地图生成完毕！");
    }

    // 新增一个专门用来滚到底部的方法
    private void ScrollToBottom()
    {
        ScrollRect scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f; // 0 代表最底部
        }
    }

    private void DrawAllLines(int totalLayers)
    {
        for (int layer = 0; layer < totalLayers - 1; layer++)
        {
            foreach (MapNode node in MapManager.Instance.GetNodesByLayer(layer))
            {
                foreach (int targetId in node.connectedNodeIds)
                {
                    MapNode targetNode = MapManager.Instance.GetNodeById(targetId);
                    // 【新增】：给连线的两端都加上 150 的高度偏移
                    if (targetNode != null)
                    {
                        CreateLineBetween(node.position + new Vector2(0, 150f), targetNode.position + new Vector2(0, 150f));
                    }
                }
            }
        }
    }

    private void CreateLineBetween(Vector2 startPos, Vector2 endPos)
    {
        GameObject lineObj = Instantiate(mapLinePrefab, contentParent);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        // ==========================================
        // 【核心修复2】：强制把每条线的锚点也改为“底部居中”！
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        // ==========================================

        lineRect.pivot = new Vector2(0, 0.5f);
        lineRect.anchoredPosition = startPos;

        float distance = Vector2.Distance(startPos, endPos);
        lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);

        float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;
        lineRect.localEulerAngles = new Vector3(0, 0, angle);
    }

    private void DrawAllNodes(int totalLayers)
    {
        for (int layer = 0; layer < totalLayers; layer++)
        {
            foreach (MapNode node in MapManager.Instance.GetNodesByLayer(layer))
            {
                GameObject nodeObj = Instantiate(mapNodePrefab, contentParent);
                RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();

                // ==========================================
                // 【核心修复1】：强制把每个节点的锚点改为“底部居中”！
                nodeRect.anchorMin = new Vector2(0.5f, 0f);
                nodeRect.anchorMax = new Vector2(0.5f, 0f);
                // ==========================================

                nodeRect.anchoredPosition = node.position + new Vector2(0, 150f);

                SetupNodeVisuals(nodeObj, node);

                Button btn = nodeObj.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => OnNodeClicked(node));
            }
        }
    }

    private void SetupNodeVisuals(GameObject nodeObj, MapNode nodeData)
    {
        Image bgImage = nodeObj.GetComponent<Image>();
        TextMeshProUGUI text = nodeObj.GetComponentInChildren<TextMeshProUGUI>();

        if (bgImage != null)
        {
            // 1. 根据类型分配基础颜色
            Color baseColor = normalColor;
            string typeName = "怪";
            switch (nodeData.nodeType)
            {
                case MapNodeType.Elite: baseColor = eliteColor; typeName = "精英"; break;
                case MapNodeType.Campfire: baseColor = campfireColor; typeName = "火"; break;
                case MapNodeType.Shop: baseColor = shopColor; typeName = "商店"; break;
                case MapNodeType.Boss: baseColor = bossColor; typeName = "Boss"; break;
            }

            // 2. 根据状态调整透明度（有没有被访问、能不能点击）
            float alpha = lockedAlpha;
            if (nodeData.isVisited) alpha = visitedAlpha;
            else if (nodeData.isAccessible) alpha = accessibleAlpha;

            baseColor.a = alpha;
            bgImage.color = baseColor;

            // 如果不能访问，直接关掉按钮交互
            Button btn = nodeObj.GetComponent<Button>();
            if (btn != null) btn.interactable = nodeData.isAccessible && !nodeData.isVisited;

            // 显示文字缩写
            if (text != null) text.text = typeName;
        }
    }

    /// <summary>
    /// 玩家点击某个节点的逻辑
    /// </summary>
    private void OnNodeClicked(MapNode nodeData)
    {
        Debug.Log($"点击了节点 ID: {nodeData.nodeId}, 类型: {nodeData.nodeType}");

        // 1. 标记当前节点已访问
        nodeData.isVisited = true;
        nodeData.isAccessible = false;

        // 2. 将全图所有其他节点设为不可访问，防止作弊
        for (int i = 0; i < MapManager.Instance.mapConfig.totalLayers; i++)
        {
            foreach (var n in MapManager.Instance.GetNodesByLayer(i))
            {
                if (!n.isVisited) n.isAccessible = false;
            }
        }

        // 3. 激活下一层连线的节点！
        foreach (int nextId in nodeData.connectedNodeIds)
        {
            MapNode nextNode = MapManager.Instance.GetNodeById(nextId);
            if (nextNode != null) nextNode.isAccessible = true;
        }

        // 4. 根据节点类型跳转场景！
        switch (nodeData.nodeType)
        {
            case MapNodeType.Normal:
                GameManager.Instance.currentEnemyLevel = 1; // 对应第1个怪（史莱姆）
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Elite:
                GameManager.Instance.currentEnemyLevel = 2; // 对应第2个怪（巨人）
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Boss:
                GameManager.Instance.currentEnemyLevel = 3; // 对应第3个怪（领主）
                GameManager.Instance.currentStageIndex = nodeData.layerIndex + 1;
                SceneManager.LoadScene("BattleScene");
                break;

            case MapNodeType.Campfire:
                SceneManager.LoadScene("CampfireScene");
                break;

            case MapNodeType.Shop:
                SceneManager.LoadScene("ShopScene"); // 去商店
                break;
        }
    }
}