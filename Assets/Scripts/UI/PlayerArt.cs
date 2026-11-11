using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家立绘的两套图。
/// 选择界面用「上身特写」，战斗界面用「全身站位」，两张是同一张手绘原图裁出来的，
/// 所以同一个角色在两个界面是同一套配色、同一个人，只是取景不同。
/// 图在 Resources/Art/Player/{key}_select.png 与 {key}_battle.png（1024² RGBA）。
///
/// 键名只跟被动技能绑定，不看名字：以后改角色叫什么都还能对上贴图，
/// 也不会因为 order 变化把两个角色的图互换（.asset 里存的是 int）。
/// </summary>
public static class PlayerArt
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static string KeyOf(CharacterData data)
    {
        if (data == null) return null;
        switch (data.passiveSkill)
        {
            case PassiveSkillType.Sweeper_GuardHeal: return "qingdao";   // 清道夫 · 红黑机械
            case PassiveSkillType.Dissector_Tempo: return "jiepou";      // 解剖师 · 绿夹克
            default: return null;
        }
    }

    /// <summary>选择界面：上身特写。</summary>
    public static Sprite Select(CharacterData data)
    {
        string key = KeyOf(data);
        return key == null ? null : Load("Art/Player/" + key + "_select");
    }

    /// <summary>战斗界面：全身站位（也有对应的蠕动帧表）。</summary>
    public static Sprite Battle(CharacterData data)
    {
        string key = KeyOf(data);
        return key == null ? null : Load("Art/Player/" + key + "_battle");
    }

    // Resources.Load<Sprite> 要看导入设置（Texture Type 得是 Sprite）；
    // 拿不到就退回 Texture2D 自己建一个 —— 这样不管怎么导入都不会空手而归。
    private static Sprite Load(string path)
    {
        Sprite cached;
        if (Cache.TryGetValue(path, out cached) && cached != null) return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            if (sprite == null) return null;
            sprite.name = System.IO.Path.GetFileName(path);
        }

        Cache[path] = sprite;
        return sprite;
    }
}
