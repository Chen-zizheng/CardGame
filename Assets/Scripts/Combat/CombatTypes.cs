using System;
using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>牌的效果类别。Stagger 是打断，Guard 是格挡，其余是伤害/治疗/抽牌。</summary>
    public enum CardKind { Attack, Guard, Stagger, Heal, Draw }

    public enum ActOwner { Player, Enemy }

    public enum CombatOutcome { None, Win, Lose }

    /// <summary>
    /// 职业被动。逻辑层只管规则，不管这被动叫什么名字——名字由 CharacterData 提供。
    /// 放在这里是因为被动会改变胜率，必须能被命令行模拟器算出来。
    /// </summary>
    public enum PassiveId
    {
        None,
        /// <summary>清道夫：格挡真的挡下伤害时，回 2 点血。</summary>
        SweeperGuardHeal,
        /// <summary>解剖师：打出只占 1 格、且要花灵能的牌，立刻回 1 点灵能。</summary>
        DissectorTempo
    }

    /// <summary>结算事件：视图层每帧取走并播放，逻辑层不关心怎么播。</summary>
    public enum CombatEventKind
    {
        Played, Resolved, Interrupted, Armored, Blocked,
        PlayerHurt, EnemyHurt, Healed, Drawn, Announced, Enraged,
        /// <summary>格挡真的吸收了伤害，回收值涨了 Amount 点（清道夫的流派资源）。</summary>
        Reclaimed,
        /// <summary>首领换招时留下的空档，Amount = 空了几格（视图层拿它报一句）。</summary>
        Breathe
    }

    public struct CombatEvent
    {
        public CombatEventKind Kind;
        public ActOwner Owner;
        public int Tick;
        public int Amount;
        public string Label;

        public override string ToString()
        {
            return Kind + "(" + Label + ", t=" + Tick + ", v=" + Amount + ")";
        }
    }

    /// <summary>随机源。逻辑层只依赖这个接口，测试时换成固定序列即可复现。</summary>
    public interface IRandom
    {
        int Next(int maxExclusive);
    }

    public sealed class SeededRandom : IRandom
    {
        private uint _state;

        public SeededRandom(int seed)
        {
            _state = seed == 0 ? 2463534242u : (uint)seed;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)maxExclusive);
        }
    }

    /// <summary>一张牌的定义。占格数 Duration 是它最重要的属性：越慢的牌锁住你越久。</summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public CardKind Kind;
        public int Cost;
        public int Duration = 2;
        public int Damage;
        public int Block;
        public int Heal;
        public int Draw;
        public int SelfDamage;

        /// <summary>大于 0 时，伤害在它覆盖的每一格都结算一次（多段攻击），而不是只在最后一格。总伤害 = Damage × 段数。</summary>
        public int HitsPerTick;

        /// <summary>最后一格结算时回多少灵能。用来做"节奏牌"（花一格换回一点的牌）。</summary>
        public int EnergyGain;

        /// <summary>结算时每 1 点回收值折算多少伤害（百分比）。清运 = 150，废料铲 = 60。</summary>
        public int GaugeDamagePercent;

        /// <summary>结算后是否把回收值清零。清运是消耗型，废料铲是保留型。</summary>
        public bool ConsumeGauge;

        /// <summary>用这张牌挡住伤害时，吸收量按这个倍率记入回收值（1 = 100%，1.5 = 150%）。</summary>
        public float GaugeGainMul = 1f;

        public bool IsStagger { get { return Kind == CardKind.Stagger; } }

        public CardDef Clone()
        {
            return (CardDef)MemberwiseClone();
        }
    }

    /// <summary>时间轴上的一段动作。玩家和敌人共用这个结构。</summary>
    public sealed class Act
    {
        public int Id;
        public ActOwner Owner;
        public string CardId;
        public string Name;
        public int Start;
        public int End;
        public int Damage;
        public int Block;
        public int Heal;
        public int Draw;
        public int SelfDamage;
        public CardKind Kind;
        public int HitsPerTick;
        public int EnergyGain;

        /// <summary>打出这张牌花了多少灵能（被动要按它判断，结算伤害时不使用）。</summary>
        public int Cost;

        public int GaugeDamagePercent;
        public bool ConsumeGauge;
        public float GaugeGainMul = 1f;

        public bool Armor;
        public bool Resolved;
        public bool Cancelled;

        public bool Covers(int tick)
        {
            return !Cancelled && Start <= tick && tick <= End;
        }

        public override string ToString()
        {
            return Name + "[" + Start + "-" + End + "]";
        }
    }

    public sealed class CombatConfig
    {
        public int PlayerMaxHp = 65;
        public int EnemyMaxHp = 110;
        public int EnergyCap = 4;
        public int StartEnergy = 2;
        // 每 2 格回 1 点。试过改成 3（想治"灵能满溢 27~51%"），台子上全线崩塌：
        // L4 普通格 86%→74%、L7 精英 63%→25%、L7 Boss 60%→20%。满溢只发生在"手上没牌"的空转格，
        // 真正要连挡两格 + 卸力的时候并不够 —— 所以灵能不是富余，只是用不出去。已改回 2。
        public int EnergyEveryTicks = 2;
        // 手牌上限 5 → 6，同时把抽牌从每 2 格放慢到每 4 格（两个一起改才是等价交换）。
        // 只放开上限不改抽速：整条曲线抬 8~10 点、Boss 62.7%→72.7%，直接崩。
        // 只放慢抽速不放上限：手里更空，"满手抽不进来"的浪费照旧。
        // 这一组台子上是中性（Boss 61.2%→60.4%，中段 -2 点），换来的是每一格能看 6 张牌。
        public int HandCap = 6;
        public int StartHand = 4;
        // 每 4 格抽 1 张。抽牌间隔直接影响"手里有没有能塞进这一格的牌"：
        // 2 格抽 1 张时手牌长期顶满（体检台 64~74% 的格子是满手），抽到就浪费；
        // 4 格抽 1 张 + 上限 6，牌来得慢但每次都存得下。
        public int DrawEveryTicks = 4;
        public int AnnounceLead = 4;
        public int MaxTicks = 400;

        /// <summary>这一局给玩家挂哪个职业被动。默认没有，旧调用方不受影响。</summary>
        public PassiveId Passive = PassiveId.None;

        /// <summary>被动回血的量（清道夫用）。</summary>
        public int PassiveHealAmount = 2;

        /// <summary>
        /// 解剖师被动额外回血：每打出一张"只占 1 格且消耗灵能"的牌回这么多生命。
        /// 0 = 只回灵能（旧行为）。灵能长期溢出的实测结果说明光回灵能没价值，
        /// 所以把同一个触发点接到生命上 —— 触发条件不变，收益换成真正稀缺的东西。
        /// </summary>
        public int DissectorHealPerCard;

        /// <summary>
        /// 上面那笔回血每 N 张牌触发一次（1 = 每张牌都回）。
        /// 用来做 1.5 点/张这种档位：每 2 张回 3 点。整数字段拼不出小数，
        /// 而"每两张牌"在文案里也读得通，比强行取整诚实。
        /// </summary>
        public int DissectorHealEveryN = 1;

        /// <summary>回收值上限。格挡吸收伤害才会涨，花在"清运 / 废料铲"这类牌上。</summary>
        public int GaugeMax = 30;

        /// <summary>吸收量折成回收值的比例。100 = 挡下多少存多少（那样太肥，实测 boss 胜率直接从 50% 冲到 78%）。</summary>
        public int GaugeAbsorbPercent = 50;

        /// <summary>这一局带进来的遗物（RelicLibrary 的 Flag 位）。默认空，旧调用方不受影响。</summary>
        public int Relics;

        /// <summary>这一局选到的天赋（TalentLibrary 的 Flag 位）。默认空，旧调用方不受影响。</summary>
        public int Talents;

        public CombatConfig Clone()
        {
            return (CombatConfig)MemberwiseClone();
        }
    }
}
