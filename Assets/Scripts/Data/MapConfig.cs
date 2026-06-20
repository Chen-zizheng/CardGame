using UnityEngine;

/// <summary>
/// 地图配置ScriptableObject，用于配置全局地图参数
/// </summary>
[CreateAssetMenu(fileName = "NewMapConfig", menuName = "Map/MapConfig")]
public class MapConfig : ScriptableObject
{
    [Header("地图全局配置")]
    public int totalLayers = 7;               // 总层数（包含Boss层）
    public int minNodesPerLayer = 2;          // 每层最少节点数
    public int maxNodesPerLayer = 4;          // 每层最多节点数
    public float layerVerticalSpacing = 150f; // 层与层之间的垂直间距
    public float nodeHorizontalSpacing = 200f;// 同层节点之间的水平间距

    [Header("节点类型概率")]
    [Range(0, 1)] public float normalNodeChance = 0.6f;
    [Range(0, 1)] public float eliteNodeChance = 0.2f;
    [Range(0, 1)] public float campfireNodeChance = 0.15f;
    [Range(0, 1)] public float shopNodeChance = 0.05f;

    [Header("强制节点配置")]
    public int bossLayerIndex = 6;            // Boss所在层数（最后一层）
    public int[] campfireLayerIndices = { 2, 5 };// 强制出现篝火的层数
}