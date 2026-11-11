using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人立绘按 id 从 Resources 取，不再依赖场景里那三个 EnemyData 槽位。
/// 原方案要把新敌人塞进 GameManager.stageEnemies，可那是场景序列化字段，
/// 加一次就得手改 .scene 的 YAML —— 一改就会把 guid 引用改坏，所以绕开它。
///
/// 图在 Resources/Art/Enemy/{id}.png（1024² RGBA，方形：CombatView 按 sprite 宽高比定尺寸，
/// 非方形会被钳成更小）。找不到就返回 null，调用方退回 currentEnemyData.enemySprite。
/// </summary>
public static class EnemyArt
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Sprite cached;
        if (Cache.TryGetValue(id, out cached)) return cached;

        string path = "Art/Enemy/" + id;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                if (sprite != null) sprite.name = id;
            }
        }
        if (sprite != null) sprite.name = id;   // 动画表也是按 id 命名的

        Cache[id] = sprite;
        return sprite;
    }

    /// <summary>有没有这个敌人的烘焙待机帧表（Resources/Art/EnemyAnim/{id}_sheet.png）。</summary>
    public static bool HasSheet(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return Resources.Load<Texture2D>("Art/EnemyAnim/" + id + "_sheet") != null;
    }
}
