using UnityEngine;

/// <summary>
/// 选人卡上的角色文案。
/// 数据（血量、被动数值）在 CharacterData 里，但"这个人是谁"这句话不该塞进 .asset ——
/// 那是设定文案，改起来要连 asset 一起动，而 .asset 里的枚举是整型存盘、动一次就可能串位。
/// 所以放在代码里，按被动枚举取。
///
/// 2026-11 玩家明确否掉了两种写法：
///   ① 说明书式定位（"大牌 · 硬吃 · 慢慢磨"）—— 让人自己玩出来，别在选人界面教学；
///   ② 玩法指导（"挡得住就换成血"）—— 不要教玩家做事。
/// 所以这里只写来历、脾气、她带着什么，一句能让人记住这个人的话就够。
/// </summary>
public static class CharacterCopy
{
    /// <summary>一段来历，一个人。不写玩法，不写数值。</summary>
    public static string Bio(CharacterData data)
    {
        if (data == null) return "";

        if (data.passiveSkill == PassiveSkillType.Dissector_Tempo)
        {
            return "培育所的解剖台上，她认得出每一根神经的走向。"
                 + "楼塌那天，她只带走了自己的刀和一只没缝完的标本。";
        }

        return "下层回收站里长大的，别人丢掉的东西她都会重新称一遍重量 —— 铁皮、断电线，还有命。"
             + "她不着急，她有的是时间。";
    }
}
