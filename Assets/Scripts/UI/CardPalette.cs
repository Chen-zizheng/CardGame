using CardCombat;
using UnityEngine;

/// <summary>
/// 卡牌按"这张牌要干什么"上色：攻击 / 格挡 / 卸力 / 回复 / 抽牌。
///
/// 为什么要单独一个文件：卡面（CombatView 手牌）、图鉴（CodexPanel）、
/// 以后的商店与奖励屏都要用同一套颜色，否则"一眼看出类型"这件事只在战斗里有，
/// 换个界面又得重新认一遍。
///
/// 取色跟着整套美术走（冷紫底 + 薄荷强调），五种类型在暗底上互相分得开：
/// 攻击砖红、格挡薄荷、卸力琥珀、回复粉、抽牌青。
/// </summary>
public static class CardPalette
{
    private static readonly Color AttackCol = new Color(0.90f, 0.40f, 0.34f, 1f);
    private static readonly Color GuardCol = new Color(0.42f, 0.86f, 0.76f, 1f);
    private static readonly Color StaggerCol = new Color(0.94f, 0.74f, 0.36f, 1f);
    private static readonly Color HealCol = new Color(0.92f, 0.56f, 0.70f, 1f);
    private static readonly Color DrawCol = new Color(0.44f, 0.70f, 0.94f, 1f);
    private static readonly Color InkCol = new Color(0.08f, 0.09f, 0.12f, 1f);

    /// <summary>类型主色：色条、费用圆、名字都用它。</summary>
    public static Color Of(CardKind kind)
    {
        switch (kind)
        {
            case CardKind.Guard: return GuardCol;
            case CardKind.Stagger: return StaggerCol;
            case CardKind.Heal: return HealCol;
            case CardKind.Draw: return DrawCol;
            default: return AttackCol;
        }
    }

    /// <summary>压在卡图上的深色版：给色条打底，避免色条和卡面花在一起。</summary>
    public static Color Deep(CardKind kind)
    {
        Color c = Of(kind);
        return new Color(c.r * 0.42f, c.g * 0.42f, c.b * 0.42f, 0.92f);
    }

    /// <summary>费用圆里的数字色。</summary>
    public static Color OnKind(CardKind kind) { return InkCol; }

    public static string Label(CardKind kind)
    {
        switch (kind)
        {
            case CardKind.Guard: return "格挡";
            case CardKind.Stagger: return "卸力";
            case CardKind.Heal: return "回复";
            case CardKind.Draw: return "抽牌";
            default: return "攻击";
        }
    }

    /// <summary>一行六字以内的短标签，给图鉴用（图鉴要写全一点）。</summary>
    public static string LongLabel(CardKind kind)
    {
        switch (kind)
        {
            case CardKind.Guard: return "格挡 · 盖住命中格";
            case CardKind.Stagger: return "卸力 · 取消这一击";
            case CardKind.Heal: return "回复 · 加血";
            case CardKind.Draw: return "抽牌 · 换节奏";
            default: return "攻击 · 打伤害";
        }
    }
}
