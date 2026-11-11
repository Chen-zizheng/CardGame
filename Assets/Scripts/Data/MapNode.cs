using UnityEngine;

/// <summary>
/// 地图节点类型枚举
/// </summary>
public enum MapNodeType
{
    Normal,     // 普通怪
    Elite,      // 精英怪
    Campfire,   // 篝火
    Shop,       // 商店
    Boss,       // Boss
    Event       // 事件（? 格）：一段话 + 一个选择，不打架
                // 注意：新类型只能往后面加。存档里 nodeType 存的是 int，
                // 插在中间会让老档里的 Boss 变回商店。
}

/// <summary>
/// 单个地图节点的数据结构
/// </summary>
[System.Serializable]
public class MapNode
{
    public int nodeId;               // 节点唯一ID
    public int layerIndex;           // 所在层数（从0开始，最底层是第0层）
    public MapNodeType nodeType;     // 节点类型
    public Vector2 position;         // UI坐标
    public bool isVisited;           // 是否已访问过
    public bool isAccessible;        // 当前是否可到达
    public int[] connectedNodeIds;   // 连接的上层节点ID数组
}