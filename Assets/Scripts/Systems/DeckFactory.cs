using System.Collections.Generic;
using UnityEngine;
using CardCombat;

/// <summary>
/// 把纯逻辑的 CardLibrary 翻译成 UI 层认识的 CardData。
/// 这样"卡牌是什么"只有一处定义（CardLibrary），存档视图、奖励池、起始牌组都从这里取。
/// </summary>
public static class DeckFactory
{
    public static CardData FromId(string id)
    {
        CardDef def = CardLibrary.Get(id);
        if (def == null) return null;

        CardData data = new CardData();
        data.id = def.Id;
        data.cardName = def.Name;
        data.description = def.Desc;
        data.cost = def.Cost;
        data.damage = def.Damage;
        data.healAmount = def.Heal;
        data.energyGain = def.EnergyGain;
        data.hpCost = def.SelfDamage;
        data.cardType = ToType(def.Kind);
        data.kind = def.Kind;
        data.rareColor = RarityOf(def);
        // 强化牌的 id 带后缀（haul+），贴图还是原来那张 —— 别去 Resources 里找 "haul+.png"。
        data.cardBackgroundSprite = Resources.Load<Sprite>("Art/Cards/" + CardLibrary.BaseId(def.Id));
        return data;
    }

    public static List<string> StarterIds(bool dissector)
    {
        return CardLibrary.StarterDeck(dissector);
    }

    public static List<CardData> Starter(bool dissector)
    {
        List<string> ids = CardLibrary.StarterDeck(dissector);
        List<CardData> list = new List<CardData>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            CardData d = FromId(ids[i]);
            if (d != null) list.Add(d);
        }
        return list;
    }

    /// <summary>
    /// 奖励/商店池：7 张通用 + 本职业牌（清道夫 6 / 解剖师 6）。
    /// 以前是全部 13 张都给，结果清道夫能在奖励里抓到"加速"，看起来像是他的卡组里本来就有它。
    /// 职业专属卡只有一个职业能拿到，牌组才有辨识度。
    /// </summary>
    public static List<CardData> AllCards(bool dissector)
    {
        List<CardData> list = new List<CardData>();
        foreach (string id in CardLibrary.AllIds)
        {
            if (!Universal(id) && !ClassOnly(id, dissector)) continue;
            CardData d = FromId(id);
            if (d != null) list.Add(d);
        }
        return list;
    }

    /// <summary>两个职业都能拿到的通用牌。</summary>
    private static bool Universal(string id)
    {
        return id != CardLibrary.Lunge
            && id != CardLibrary.Bloodlust
            && id != CardLibrary.Burning
            && id != CardLibrary.Combo
            && id != CardLibrary.Haste
            && id != CardLibrary.Overdraw
            && id != CardLibrary.Haul
            && id != CardLibrary.Compact
            && id != CardLibrary.Scrap
            && id != CardLibrary.Bulwark
            && id != CardLibrary.Sweep
            && id != CardLibrary.Brace
            && id != CardLibrary.Dredge
            && id != CardLibrary.Needle
            && id != CardLibrary.Flay
            && id != CardLibrary.Siphon
            && id != CardLibrary.Tempo;
    }

    /// <summary>
    /// 职业专属牌：清道夫 猛袭/嗜血/燃烧血液 + 回收三件套（清运/压实/废料铲），
    /// 解剖师 连环刺/加速/透支。回收是"格挡吸收攒弹药"的流派，只有清道夫有被动支撑。
    /// </summary>
    private static bool ClassOnly(string id, bool dissector)
    {
        if (dissector)
            return id == CardLibrary.Combo || id == CardLibrary.Haste || id == CardLibrary.Overdraw
                || id == CardLibrary.Needle || id == CardLibrary.Flay || id == CardLibrary.Siphon
                || id == CardLibrary.Tempo;
        return id == CardLibrary.Lunge || id == CardLibrary.Bloodlust || id == CardLibrary.Burning
            || id == CardLibrary.Haul || id == CardLibrary.Compact || id == CardLibrary.Scrap
            || id == CardLibrary.Bulwark || id == CardLibrary.Sweep || id == CardLibrary.Brace
            || id == CardLibrary.Dredge;
    }

    private static CardType ToType(CardKind kind)
    {
        switch (kind)
        {
            case CardKind.Guard: return CardType.Shield;
            case CardKind.Stagger: return CardType.Shield;
            case CardKind.Heal: return CardType.Heal;
            case CardKind.Draw: return CardType.Draw;
            default: return CardType.Attack;
        }
    }

    private static Color RarityOf(CardDef def)
    {
        if (def.Cost >= 3) return new Color(1f, 0.85f, 0.42f);
        if (def.Cost >= 2) return new Color(0.74f, 0.58f, 1f);
        return new Color(0.62f, 0.78f, 0.78f);
    }
}
