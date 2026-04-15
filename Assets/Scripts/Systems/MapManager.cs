using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 大地图系统核心管理器
/// 负责生成、存储和管理整个地图的节点数据
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [Header("地图配置")]
    public MapConfig mapConfig; // 拖拽你刚才创建的DefaultMapConfig到这里

    [Header("调试选项")]
    public bool generateOnStart = true; // 启动时自动生成地图
    public bool printDebugLog = true; // 打印生成日志

    // 存储所有生成的节点
    private List<MapNode> allNodes = new List<MapNode>();
    // 按层存储节点，方便快速访问
    private Dictionary<int, List<MapNode>> nodesByLayer = new Dictionary<int, List<MapNode>>();

    private int nextNodeId = 0;

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateNewMap();
        }
    }

    /// <summary>
    /// 生成一张全新的随机地图
    /// </summary>
    public void GenerateNewMap()
    {
        // 清空旧数据
        allNodes.Clear();
        nodesByLayer.Clear();
        nextNodeId = 0;

        if (printDebugLog)
        {
            Debug.Log("【MapManager】开始生成新地图...");
        }

        // 1. 从底层到顶层逐层生成节点
        for (int layer = 0; layer < mapConfig.totalLayers; layer++)
        {
            GenerateLayer(layer);
        }

        // 2. 建立节点之间的连接关系
        ConnectAllLayers();
        // 3. 设置初始可到达节点（第0层所有节点）
        foreach (MapNode node in nodesByLayer[0])
        {
            node.isAccessible = true;
        }

        // ==========================================
        // 【新增：商店强制保底机制】
        // 保证在地图的第 2 层或第 3 层（中段），必定有一个商店！
        // ==========================================
        bool hasShop = false;
        foreach (var node in allNodes)
        {
            if (node.nodeType == MapNodeType.Shop) hasShop = true;
        }

        if (!hasShop && mapConfig.totalLayers > 3)
        {
            // 随机找第 3 层的某一个节点，强制变成商店！
            List<MapNode> middleLayerNodes = nodesByLayer[mapConfig.totalLayers / 2];
            int randomNodeIndex = Random.Range(0, middleLayerNodes.Count);
            middleLayerNodes[randomNodeIndex].nodeType = MapNodeType.Shop;
            Debug.Log("【保底机制触发】强制生成了一个商店！");
        }
        // ==========================================

        if (printDebugLog)
        {
            Debug.Log($"【MapManager】地图生成完成！总节点数：{allNodes.Count}");
            PrintMapDebugInfo();
        }
    }

    /// <summary>
    /// 生成指定层数的节点
    /// </summary>
    private void GenerateLayer(int layerIndex)
    {
        List<MapNode> layerNodes = new List<MapNode>();
        int nodeCount;

        // 特殊处理：Boss层只有1个节点
        if (layerIndex == mapConfig.bossLayerIndex)
        {
            nodeCount = 1;
        }
        else
        {
            // 随机生成该层节点数
            nodeCount = Random.Range(mapConfig.minNodesPerLayer, mapConfig.maxNodesPerLayer + 1);
        }

        // 计算节点在屏幕上的水平位置
        float totalWidth = (nodeCount - 1) * mapConfig.nodeHorizontalSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < nodeCount; i++)
        {
            MapNode node = new MapNode();
            node.nodeId = nextNodeId++;
            node.layerIndex = layerIndex;
            node.isVisited = false;
            node.isAccessible = false;
            node.connectedNodeIds = new int[0];

            // 设置节点位置（居中对齐）
            float x = startX + i * mapConfig.nodeHorizontalSpacing;
            float y = layerIndex * mapConfig.layerVerticalSpacing;
            node.position = new Vector2(x, y);

            // 分配节点类型
            node.nodeType = GetRandomNodeType(layerIndex);

            layerNodes.Add(node);
            allNodes.Add(node);
        }

        nodesByLayer.Add(layerIndex, layerNodes);
    }

    /// <summary>
    /// 根据层数和概率获取随机节点类型
    /// </summary>
    private MapNodeType GetRandomNodeType(int layerIndex)
    {
        // Boss层固定为Boss
        if (layerIndex == mapConfig.bossLayerIndex)
        {
            return MapNodeType.Boss;
        }

        // 强制篝火层固定为篝火
        foreach (int campfireLayer in mapConfig.campfireLayerIndices)
        {
            if (layerIndex == campfireLayer)
            {
                return MapNodeType.Campfire;
            }
        }

        // 随机生成其他类型
        float roll = Random.value;
        if (roll < mapConfig.normalNodeChance)
        {
            return MapNodeType.Normal;
        }
        else if (roll < mapConfig.normalNodeChance + mapConfig.eliteNodeChance)
        {
            return MapNodeType.Elite;
        }
        else if (roll < mapConfig.normalNodeChance + mapConfig.eliteNodeChance + mapConfig.campfireNodeChance)
        {
            return MapNodeType.Campfire;
        }
        else
        {
            return MapNodeType.Shop;
        }
    }

    /// <summary>
    /// 连接所有相邻层的节点
    /// </summary>
    private void ConnectAllLayers()
    {
        for (int layer = 0; layer < mapConfig.totalLayers - 1; layer++)
        {
            ConnectTwoLayers(layer, layer + 1);
        }
    }

    /// <summary>
    /// 连接两层节点，保证100%连通性
    /// </summary>
    private void ConnectTwoLayers(int lowerLayer, int upperLayer)
    {
        List<MapNode> lowerNodes = nodesByLayer[lowerLayer];
        List<MapNode> upperNodes = nodesByLayer[upperLayer];

        // 为每个下层节点随机连接1-2个上层节点
        foreach (MapNode lowerNode in lowerNodes)
        {
            int connectionCount = Random.Range(1, Mathf.Min(2, upperNodes.Count) + 1);
            List<int> connectedIds = new List<int>();

            // 随机选择不重复的上层节点
            while (connectedIds.Count < connectionCount)
            {
                int randomIndex = Random.Range(0, upperNodes.Count);
                int upperNodeId = upperNodes[randomIndex].nodeId;

                if (!connectedIds.Contains(upperNodeId))
                {
                    connectedIds.Add(upperNodeId);
                }
            }

            lowerNode.connectedNodeIds = connectedIds.ToArray();
        }

        // 确保所有上层节点至少有一个连接（防止出现孤立节点）
        foreach (MapNode upperNode in upperNodes)
        {
            bool hasConnection = false;
            foreach (MapNode lowerNode in lowerNodes)
            {
                if (lowerNode.connectedNodeIds.Contains(upperNode.nodeId))
                {
                    hasConnection = true;
                    break;
                }
            }

            // 如果没有连接，随机找一个下层节点连接它
            if (!hasConnection)
            {
                MapNode randomLowerNode = lowerNodes[Random.Range(0, lowerNodes.Count)];
                List<int> newConnections = new List<int>(randomLowerNode.connectedNodeIds);
                newConnections.Add(upperNode.nodeId);
                randomLowerNode.connectedNodeIds = newConnections.ToArray();
            }
        }
    }

    /// <summary>
    /// 根据节点ID获取节点数据
    /// </summary>
    public MapNode GetNodeById(int nodeId)
    {
        return allNodes.Find(n => n.nodeId == nodeId);
    }

    /// <summary>
    /// 获取指定层数的所有节点
    /// </summary>
    public List<MapNode> GetNodesByLayer(int layerIndex)
    {
        if (nodesByLayer.TryGetValue(layerIndex, out List<MapNode> nodes))
        {
            return nodes;
        }
        return new List<MapNode>();
    }

    /// <summary>
    /// 打印地图调试信息
    /// </summary>
    private void PrintMapDebugInfo()
    {
        for (int layer = 0; layer < mapConfig.totalLayers; layer++)
        {
            List<MapNode> nodes = GetNodesByLayer(layer);
            string layerInfo = $"第{layer}层: ";

            foreach (MapNode node in nodes)
            {
                layerInfo += $"[{node.nodeId}:{node.nodeType}] ";
            }

            Debug.Log(layerInfo);
        }
    }
}