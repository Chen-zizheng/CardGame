using UnityEngine;

/// <summary>
/// 地图配置ScriptableObject，用于配置全局地图参数
/// </summary>
[CreateAssetMenu(fileName = "NewMapConfig", menuName = "Map/MapConfig")]
public class MapConfig : ScriptableObject
{
    [Header("地图全局配置")]
    public int totalLayers = 10;              // 总层数（包含Boss层）
    public int minNodesPerLayer = 2;          // 每层最少节点数
    public int maxNodesPerLayer = 4;          // 每层最多节点数
    public float layerVerticalSpacing = 150f; // 层与层之间的垂直间距
    public float nodeHorizontalSpacing = 200f;// 同层节点之间的水平间距

    [Header("节点类型概率")]
    [Range(0, 1)] public float normalNodeChance = 0.6f;
    [Range(0, 1)] public float eliteNodeChance = 0.2f;
    [Range(0, 1)] public float campfireNodeChance = 0.10f;
    // 事件格：一段话 + 一个选择，给"往下走"这件事留点不确定性。
    // 别调太高 —— 一局撞见 3~4 个刚好，再多就变成读小说。
    [Range(0, 1)] public float eventNodeChance = 0.10f;
    [Range(0, 1)] public float shopNodeChance = 0.06f;

    [Header("强制节点配置")]
    public int bossLayerIndex = 9;            // Boss所在层数（最后一层）
    // 篝火挂在 3 / 6 / 9 层：正好是本能（每 3 层一条）发完的那一层之后，
    // 「打完拿一条本能 → 在篝火旁回血删牌」变成同一段节奏。
    public int[] campfireLayerIndices = { 2, 5, 8 };// 强制出现篝火的层数
}