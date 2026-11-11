using System;
using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>敌方一个招式的模板：名字 / 占格 / 伤害 / 提前几格公布 / 是否霸体（硬直无效）。</summary>
    public sealed class EnemyMove
    {
        public string Name;
        public int Duration = 2;
        public int Damage = 8;
        public int Lead = 4;
        public bool Armor;
        /// <summary>这一击结束后停几格再出下一击。没有空档的战斗只是换血。</summary>
        public int Gap = 2;

        public EnemyMove(string name, int duration, int damage, int lead, bool armor, int gap)
        {
            Name = name;
            Duration = duration;
            Damage = damage;
            Lead = lead;
            Armor = armor;
            Gap = gap;
        }
    }

    public sealed class EnemyTemplate
    {
        public string Id;
        public string Name;
        public int MaxHp;
        public List<EnemyMove> Moves = new List<EnemyMove>();
        public int Phase2AtPercent = 50;
        public int Phase2BonusDamage = 2;
        public int Phase2LeadReduction = 1;

        /// <summary>
        /// 进二阶段时把下一招往后推这么多格（0 = 不推）。
        /// 只给首领用：它是整趟唯一的结局，输一次废掉十层，
        /// 而"换招"那一刻恰恰是最该让玩家看清楚的一次 —— 推两格，等于白送一次读招机会。
        /// </summary>
        public int Phase2Breathe;
    }

    /// <summary>
    /// 敌人亚种：同一个怪在 10 层里要出现两次，靠这层前缀和数值微调把两场分开。
    /// 名字是直接拼在怪名前面的（"腐蚀的" + "游丝" = "腐蚀的游丝"），所以不用加分隔符。
    /// </summary>
    public sealed class EnemyVariant
    {
        public string Id;
        public string Prefix;
        public float HpMul = 1f;
        public float DmgMul = 1f;
        /// <summary>正数 = 预警更短（更难），负数 = 更长的反应窗口。</summary>
        public int LeadTrim;

        public EnemyVariant(string id, string prefix, float hpMul, float dmgMul, int leadTrim)
        {
            Id = id;
            Prefix = prefix;
            HpMul = hpMul;
            DmgMul = dmgMul;
            LeadTrim = leadTrim;
        }
    }

    public static class EnemyVariants
    {
        /// <summary>四个亚种各改一样东西：血、伤、预警，别让它们只是换个名字。</summary>
        public static readonly EnemyVariant[] All =
        {
            new EnemyVariant("corroded", "腐蚀的", 0.92f, 1.14f, 0),
            new EnemyVariant("shelled",  "硬壳的", 1.24f, 0.96f, -1),
            new EnemyVariant("rabid",    "狂躁的", 0.90f, 1.08f, 1),
            new EnemyVariant("starved",  "饥饿的", 1.12f, 1.06f, 0)
        };

        /// <summary>
        /// 前四层保留原种（玩家得先认识它们长什么样、怎么出招），第 5 层起才出现亚种；
        /// 精英格和 Boss 格一律带亚种 —— 花一个节点换一场怪的难度，总得看得出区别。
        /// 同一个 stageIndex + 同一个 salt 永远给同一个亚种，所以重开一局地图还是那只。
        /// </summary>
        public static EnemyVariant Pick(int stageIndex, int tier, int salt)
        {
            if (stageIndex < 4 && tier < 2) return null;
            int h = stageIndex * 73856093 ^ salt * 19349663 ^ tier * 83492791;
            if (h == int.MinValue) h = 0;
            if (h < 0) h = -h;
            return All[(h >> 3) % All.Length];
        }
    }

    /// <summary>
    /// 游戏里所有牌的唯一定义处。数值改这里，逻辑层和视图层都跟着变。
    /// Duration 就是"出这张牌要占几格"，它是这套战斗的核心资源。
    ///
    /// 数值口径（2026-11 重整）：总成本 = 占格 Duration + 2 × 灵能 Cost
    /// （灵能每 2 格回 1 点，所以 2 格 ≈ 1 灵能）。伤害 ≈ 2.2 × 总成本。
    ///
    /// 数值第二版（2026-11）改了两处，都是体检台量出来的：
    /// ① 格挡 9→12、长盾 7→14。L3 起单次伤害 13~22，旧值只能减伤，完整解法只剩卸力（霸体招专打卸力）。
    /// ② 连环刺每格 9→7。旧值 3.9/成本，是全表最超模的一张（标准线 2.2）。
    /// 没改灵能回速：试过 2→3 格，全线崩（见 CombatTypes.EnergyEveryTicks 的注释）。
    /// 于是"短牌"并不更划算，它只是更容易塞进制胜窗口；"长牌"也不会更亏，它只是把赌注拉长。
    /// </summary>
    public static class CardLibrary
    {
        // 通用
        public const string Basic = "basic";
        public const string Quick = "quick";
        public const string Heavy = "heavy";
        public const string Guard = "guard";
        public const string Barrier = "barrier";
        public const string Stagger = "stagger";
        public const string Potion = "potion";
        // 战士：拿血换输出
        public const string Lunge = "lunge";
        public const string Bloodlust = "bloodlust";
        public const string Burning = "burning";
        // 游侠：拿血换节奏
        public const string Combo = "combo";
        public const string Haste = "haste";
        public const string Overdraw = "overdraw";
        // 清道夫·回收：把挡下来的伤害存成"回收值"，再一次性砸回去
        public const string Haul = "haul";
        public const string Compact = "compact";
        public const string Scrap = "scrap";
        // 清道夫·重装：更厚的盾、把小牌也变成回收来源
        public const string Bulwark = "bulwark";
        public const string Sweep = "sweep";
        public const string Brace = "brace";
        public const string Dredge = "dredge";
        // 解剖师·快刀：碎牌、多段、把节奏接起来
        public const string Needle = "needle";
        public const string Flay = "flay";
        public const string Siphon = "siphon";
        public const string Tempo = "tempo";

        private static readonly Dictionary<string, CardDef> Table = new Dictionary<string, CardDef>
        {
            // ---- 通用 ----
            { Basic, Make(Basic, "普通攻击", "占 2 格 · 伤害 9", CardKind.Attack, 1, 2, 9) },
            { Quick, Make(Quick, "快斩", "占 1 格 · 伤害 8", CardKind.Attack, 1, 1, 8) },
            { Heavy, Make(Heavy, "重击", "占 4 格 · 伤害 18", CardKind.Attack, 2, 4, 18) },
            { Guard, Make(Guard, "格挡", "占 3 格 · 覆盖命中格格挡 12", CardKind.Guard, 1, 3, 0, 12) },
            { Barrier, Make(Barrier, "长盾", "占 5 格 · 覆盖命中格格挡 14", CardKind.Guard, 2, 5, 0, 14) },
            { Stagger, Make(Stagger, "卸力", "占 2 格 · 覆盖命中格 = 取消该击", CardKind.Stagger, 3, 2) },
            { Potion, Make(Potion, "回复药水", "占 2 格 · 回复 9 生命", CardKind.Heal, 1, 2, 0, 0, 9) },

            // ---- 战士 ----
            { Lunge, Make(Lunge, "猛袭", "占 2 格 · 伤害 13", CardKind.Attack, 2, 2, 13) },
            { Bloodlust, Make(Bloodlust, "嗜血", "占 3 格 · 伤害 12，自伤 3", CardKind.Attack, 1, 3, 12, 0, 0, 0, 3) },
            { Burning, Make(Burning, "燃烧血液", "占 4 格 · 伤害 22，自伤 5", CardKind.Attack, 2, 4, 22, 0, 0, 0, 5) },

            // ---- 清道夫·回收（格挡吸收 → 回收值 → 一次性清算）----
            { Haul, Make(Haul, "清运", "占 2 格 · 伤害 4 + 回收值 ×1.4，清空回收值", CardKind.Attack, 2, 2, 4,
                0, 0, 0, 0, 0, 0, 140, true) },
            { Compact, Make(Compact, "压实", "占 3 格 · 格挡 10，吸收量 ×1.5 记入回收值", CardKind.Guard, 1, 3, 0,
                10, 0, 0, 0, 0, 0, 0, false, 1.5f) },
            { Scrap, Make(Scrap, "废料铲", "占 3 格 · 伤害 8 + 回收值 ×0.4，保留回收值", CardKind.Attack, 2, 3, 8,
                0, 0, 0, 0, 0, 0, 40, false) },

            // ---- 清道夫·重装（第二批）----
            // 铁墙：一张盖住 4 格，专治霸体招（霸体不能卸力，只能硬挡）。
            { Bulwark, Make(Bulwark, "铁墙", "占 4 格 · 覆盖命中格格挡 22", CardKind.Guard, 2, 4, 0, 22) },
            // 扫拖：介于废料铲（×0.4 保留）与清运（×1.4 清空）之间，保留弹药的中号清算。
            { Sweep, Make(Sweep, "扫拖", "占 3 格 · 伤害 6 + 回收值 ×0.8，保留回收值", CardKind.Attack, 2, 3, 6,
                0, 0, 0, 0, 0, 0, 80, false) },
            // 靠住：小牌，专门塞进一格缝隙，顺便把挡下来的量乘 1.2 存进回收。
            { Brace, Make(Brace, "靠住", "占 2 格 · 格挡 10，吸收量 ×1.2 记入回收值", CardKind.Guard, 1, 2, 0,
                10, 0, 0, 0, 0, 0, 0, false, 1.2f) },
            // 掏底：他的牌都大，卡手是主要死因，给一张一格的换手牌。
            { Dredge, Make(Dredge, "掏底", "占 1 格 · 抽 2 张，自伤 1", CardKind.Draw, 1, 1, 0, 0, 0, 2, 1) },

            // ---- 游侠 ----
            { Combo, Make(Combo, "连环刺", "占 3 格 · 每格伤害 7（共 21）", CardKind.Attack, 2, 3, 7, 0, 0, 0, 0, 1) },
            { Haste, Make(Haste, "加速", "占 1 格 · 抽 2 张 · 回 1 灵能", CardKind.Draw, 1, 1, 0, 0, 0, 2, 0, 0, 1) },
            { Overdraw, Make(Overdraw, "透支", "占 1 格 · 抽 2 张，自伤 4", CardKind.Draw, 0, 1, 0, 0, 0, 2, 4) },

            // ---- 解剖师·快刀（第二批）----
            // 细针：一格的小刀口，带回血，专门补最后一格。
            { Needle, Make(Needle, "细针", "占 1 格 · 伤害 7，回复 2 生命", CardKind.Attack, 1, 1, 7, 0, 2) },
            // 剥皮：两格两段，霓虹灯牌 / 本能·连击每段都吃加成。
            { Flay, Make(Flay, "剥皮", "占 2 格 · 每格伤害 5（共 10）", CardKind.Attack, 1, 2, 5, 0, 0, 0, 0, 1) },
            // 抽髓：慢一点的高伤 + 回血，把她的续航补上。
            { Siphon, Make(Siphon, "抽髓", "占 3 格 · 伤害 11，回复 4 生命", CardKind.Attack, 2, 3, 11, 0, 4) },
            // 换气：零灵能的一格循环，抽一张、回一点，让节奏不断。
            { Tempo, Make(Tempo, "换气", "占 1 格 · 抽 1 张 · 回 1 灵能", CardKind.Draw, 0, 1, 0, 0, 0, 1, 0, 0, 1) }
        };

        private static CardDef Make(string id, string name, string desc, CardKind kind,
            int cost, int duration, int damage = 0, int block = 0, int heal = 0, int draw = 0,
            int selfDamage = 0, int hitsPerTick = 0, int energyGain = 0,
            int gaugeDamagePercent = 0, bool consumeGauge = false, float gaugeGainMul = 1f)
        {
            return new CardDef
            {
                Id = id, Name = name, Desc = desc, Kind = kind,
                Cost = cost, Duration = duration, Damage = damage, Block = block,
                Heal = heal, Draw = draw, SelfDamage = selfDamage,
                HitsPerTick = hitsPerTick, EnergyGain = energyGain,
                GaugeDamagePercent = gaugeDamagePercent, ConsumeGauge = consumeGauge,
                GaugeGainMul = gaugeGainMul
            };
        }

        /// <summary>强化牌的后缀。存档里存 "haul+"，牌图书馆认得它。</summary>
        public const string Up = "+";

        public static CardDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsUpgraded(id)) return GetUpgraded(id);
            CardDef def;
            return Table.TryGetValue(id, out def) ? def : null;
        }

        public static IEnumerable<string> AllIds { get { return Table.Keys; } }

        public static bool IsUpgraded(string id)
        {
            return !string.IsNullOrEmpty(id) && id.EndsWith(Up, StringComparison.Ordinal);
        }

        /// <summary>去掉强化后缀，取底牌 id。取贴图、查图鉴、老存档兜底都要用这个。</summary>
        public static string BaseId(string id)
        {
            return IsUpgraded(id) ? id.Substring(0, id.Length - 1) : id;
        }

        public static string UpgradeOf(string id)
        {
            return IsUpgraded(id) ? id : id + Up;
        }

        private static readonly Dictionary<string, CardDef> UpTable = new Dictionary<string, CardDef>();

        /// <summary>
        /// 强化 = 同一张牌再长一档，不是另加一张新牌。
        /// 按「这张牌靠什么吃饭」分别加，而不是一刀切 +25%：
        /// 伤害牌补 25%（至少 +2），格挡牌补 25%（至少 +2），治疗 +3，抽牌多抽 1 张，
        /// 回收牌倍率 +20%，自伤减半；卸力本来就是一整击取消、加数值没意义，改成便宜 1 点灵能。
        /// 每张牌只能强化一次，所以数值可以直接写死在这一处。
        /// </summary>
        public static CardDef GetUpgraded(string id)
        {
            string baseId = BaseId(id);
            CardDef up;
            if (UpTable.TryGetValue(baseId, out up)) return up;

            CardDef src = Get(baseId);
            if (src == null) return null;

            up = src.Clone();
            up.Id = baseId + Up;
            up.Name = src.Name + Up;

            if (src.Damage > 0) up.Damage = src.Damage + Math.Max(2, (int)Math.Round(src.Damage * 0.25f));
            if (src.Block > 0) up.Block = src.Block + Math.Max(2, (int)Math.Round(src.Block * 0.25f));
            if (src.Heal > 0) up.Heal = src.Heal + 3;
            if (src.Draw > 0) up.Draw = src.Draw + 1;
            if (src.GaugeDamagePercent > 0) up.GaugeDamagePercent = src.GaugeDamagePercent + 20;
            if (src.SelfDamage > 0) up.SelfDamage = src.SelfDamage / 2;
            if (src.Kind == CardKind.Stagger) up.Cost = Math.Max(0, src.Cost - 1);

            up.Desc = Describe(up);
            UpTable[baseId] = up;
            return up;
        }

        /// <summary>用字段拼一句描述。强化牌的数值是现算的，文案也只好现拼。</summary>
        public static string Describe(CardDef d)
        {
            string s = "占 " + d.Duration + " 格";
            if (d.Kind == CardKind.Stagger)
            {
                s += " · 覆盖命中格 = 取消该击";
            }
            else
            {
                if (d.Damage > 0)
                {
                    s += d.HitsPerTick > 0
                        ? " · 每格伤害 " + d.Damage + "（共 " + (d.Damage * d.Duration) + "）"
                        : " · 伤害 " + d.Damage;
                }
                if (d.Block > 0) s += " · 格挡 " + d.Block;
                if (d.GaugeDamagePercent > 0) s += " + 回收值 ×" + (d.GaugeDamagePercent / 100f).ToString("0.##");
                if (d.Heal > 0) s += " · 回复 " + d.Heal + " 生命";
                if (d.Draw > 0) s += " · 抽 " + d.Draw + " 张";
                if (d.EnergyGain > 0) s += " · 回 " + d.EnergyGain + " 灵能";
            }
            if (d.SelfDamage > 0) s += "，自伤 " + d.SelfDamage;
            if (d.GaugeDamagePercent > 0) s += d.ConsumeGauge ? "，清空回收值" : "，保留回收值";
            if (d.GaugeGainMul > 1f) s += "，吸收量 ×" + d.GaugeGainMul.ToString("0.#") + " 记入回收值";
            return s;
        }

        /// <summary>
        /// 老存档里存的是中文卡名（CardData.cardName），这是唯一的稳定标识。
        /// 名字匹配从"最具体"往"最笼统"排，避免"重击"被"击"类规则抢走。
        /// </summary>
        public static string MapChinese(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return Basic;
            if (cardName.Contains("铁墙")) return Bulwark;
            if (cardName.Contains("扫拖")) return Sweep;
            if (cardName.Contains("靠住")) return Brace;
            if (cardName.Contains("掏底")) return Dredge;
            if (cardName.Contains("细针")) return Needle;
            if (cardName.Contains("剥皮")) return Flay;
            if (cardName.Contains("抽髓")) return Siphon;
            if (cardName.Contains("换气")) return Tempo;
            if (cardName.Contains("清运")) return Haul;
            if (cardName.Contains("压实")) return Compact;
            if (cardName.Contains("废料")) return Scrap;
            if (cardName.Contains("连环")) return Combo;
            if (cardName.Contains("快斩")) return Quick;
            if (cardName.Contains("重击")) return Heavy;
            if (cardName.Contains("猛袭")) return Lunge;
            if (cardName.Contains("燃烧")) return Burning;
            if (cardName.Contains("嗜血")) return Bloodlust;
            if (cardName.Contains("长盾")) return Barrier;
            if (cardName.Contains("格挡") || cardName.Contains("护盾")) return Guard;
            if (cardName.Contains("卸力") || cardName.Contains("打断")) return Stagger;
            if (cardName.Contains("药水") || cardName.Contains("回复")) return Potion;
            if (cardName.Contains("透支")) return Overdraw;
            if (cardName.Contains("加速")) return Haste;
            return Basic;
        }

        /// <summary>
        /// 两套起始牌组。清道夫堆大牌换伤害，解剖师堆节奏（多段 + 抽牌 + 回灵能）。
        /// 都保证 3 张卸力：打断是这套战斗唯一的防守型胜负手，缺了它就是纯换血。
        /// </summary>
        public static List<string> StarterDeck(bool dissector)
        {
            List<string> deck = new List<string>();
            if (dissector)
            {
                Add(deck, Basic, 3);
                Add(deck, Quick, 1);
                Add(deck, Combo, 3);
                Add(deck, Haste, 2);
                Add(deck, Guard, 2);
                Add(deck, Barrier, 1);
                Add(deck, Stagger, 3);
                Add(deck, Overdraw, 1);
                Add(deck, Potion, 2);
            }
            else
            {
                Add(deck, Basic, 4);
                Add(deck, Quick, 2);
                Add(deck, Heavy, 2);
                Add(deck, Lunge, 1);
                // 回收三件套先给一张清运、一张压实：不靠奖励牌也能在第一场摸到流派手感。
                Add(deck, Haul, 1);
                Add(deck, Compact, 1);
                Add(deck, Guard, 2);
                Add(deck, Stagger, 3);
                Add(deck, Bloodlust, 1);
                Add(deck, Potion, 1);
            }
            return deck;
        }

        private static void Add(List<string> deck, string id, int count)
        {
            for (int i = 0; i < count; i++) deck.Add(id);
        }
    }

    /// <summary>
    /// 六种敌人。每一层的招式组用不同方式逼玩家：快预警、连击不留缝、霸体不能卸力。
    /// 招式顺序固定，所以玩家可以背下节奏 —— 这是刻意的（时间轴战斗靠记谱）。
    /// </summary>
    public static class EnemyLibrary
    {
        public const string Slime = "slime";
        public const string Eel = "eel";
        public const string Giant = "giant";
        public const string Moth = "moth";
        public const string Tank = "tank";
        public const string Lord = "lord";

        private static readonly Dictionary<string, EnemyTemplate> Table = new Dictionary<string, EnemyTemplate>
        {
            {
                Slime, new EnemyTemplate
                {
                    Id = Slime, Name = "史莱姆", MaxHp = 52,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("撞击", 2, 10, 9, false, 2),
                        new EnemyMove("弹跳", 1, 7, 8, false, 1),
                        new EnemyMove("压扁", 3, 14, 10, true, 3)
                    }
                }
            },
            {
                Eel, new EnemyTemplate
                {
                    Id = Eel, Name = "游丝", MaxHp = 72,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("电击", 1, 11, 6, false, 0),
                        new EnemyMove("缠绕", 2, 14, 7, false, 1),
                        new EnemyMove("甩尾", 2, 17, 9, false, 1)
                    },
                    Phase2AtPercent = 45
                }
            },
            {
                Giant, new EnemyTemplate
                {
                    Id = Giant, Name = "羊巨人", MaxHp = 94,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("践踏", 3, 14, 10, false, 2),
                        new EnemyMove("冲撞", 2, 11, 9, false, 1),
                        new EnemyMove("怒吼", 1, 8, 8, false, 3),
                        new EnemyMove("锤击", 4, 19, 10, true, 3)
                    }
                }
            },
            {
                Moth, new EnemyTemplate
                {
                    Id = Moth, Name = "鳞蛾", MaxHp = 96,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("俯冲", 2, 15, 9, false, 0),
                        new EnemyMove("鳞粉", 1, 11, 8, false, 0),
                        new EnemyMove("振翅", 2, 18, 11, false, 0)
                    },
                    Phase2AtPercent = 50,
                    Phase2BonusDamage = 1
                }
            },
            {
                Tank, new EnemyTemplate
                {
                    Id = Tank, Name = "锈壳", MaxHp = 86,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("炮击", 2, 12, 12, false, 2),
                        new EnemyMove("碾压", 4, 15, 13, true, 4),
                        // 原来两招霸体，"卸力"对锈壳只有 23% 生效（其余敌人 30~40%），L5 因此成了整条曲线的坑。
                        // 只留碾压一个霸体，让锈壳教"看清哪一招不能卸"而不是"全都不能卸"。
                        new EnemyMove("装甲突进", 3, 13, 14, false, 3)
                    },
                    Phase2AtPercent = 55
                }
            },
            {
                Lord, new EnemyTemplate
                {
                    Id = Lord, Name = "领主", MaxHp = 104,
                    Moves = new List<EnemyMove>
                    {
                        new EnemyMove("斩击", 3, 13, 10, false, 2),
                        new EnemyMove("突刺", 1, 9, 8, false, 1),
                        new EnemyMove("处刑", 4, 17, 12, true, 4),
                        new EnemyMove("横扫", 2, 12, 9, false, 2)
                    },
                    Phase2AtPercent = 60,
                    // 首领换招时留两格空档：这一招本来就"预警 10~12 格 + 霸体"，
                    // 再多给两格不是削伤害，是把"看清它换成了什么"这件事还回来。
                    Phase2Breathe = 2
                }
            }
        };

        /// <summary>图鉴按这个顺序列（字典的 Key 顺序不保证，人工排一下：由弱到强）。</summary>
        public static readonly string[] Order = { Slime, Eel, Giant, Moth, Tank, Lord };

        public static EnemyTemplate Get(string id)
        {
            EnemyTemplate t;
            return Table.TryGetValue(id, out t) ? t : Table[Slime];
        }

        /// <summary>
        /// 十层各自的守关者。难度不是靠加血堆的，是靠招式组换手法：
        /// 只有 7 层真的会打架：L1 / L2 / L4 / L5 / L7 / L8 / L10。
        /// **L3 / L6 / L9 是篝火层（MapConfig.campfireLayerIndices = {2,5,8}），那一格上的敌人永远不会出手** ——
        /// 所以这三行只是占位，写什么都不影响战斗；图鉴的「出场」必须跳过它们（CodexPanel.LayersOf）。
        /// 真正会打的顺序：L1 史莱姆（教学）· L2 游丝（预警短）· L4 鳞蛾（不留缝）· L5 锈壳（长预警高伤）
        /// · L7 羊巨人（霸体，卸力失效）· L8 锈壳回访（带亚种）· L10 领主。
        /// 试过把鳞蛾挪到 L5 / L7（残血入场最狠的那只往后放），L4 的悬崖是消失了，
        /// 但 L7 立刻变 14.8%、L8 15.5%，总通关率从 42.8% 掉到 36.3%（那两轮量的基准是篝火 40% 的版本，
        /// 现在基准是 40.5%，但"换位置会更差"这个结论不变）——
        /// 结论：锈壳这种「长预警」的怪在残血时反而公平（能提前盖住），鳞蛾在哪儿都是一道墙，放 L4 最省。
        /// 血量和伤害仍按层数放大，第 5 层起套亚种前缀（腐蚀的伤害高、硬壳的血厚预警长、狂躁的预警短、饥饿的两样都涨）。
        /// </summary>
        public static string IdForStage(int stageIndex)
        {
            switch (stageIndex)
            {
                case 0: return Slime;      // L1 教学
                case 1: return Eel;        // L2 预警短
                case 2: return Eel;        // L3 是篝火，不打架（占位）
                case 3: return Moth;       // L4 不留缝
                case 4: return Tank;       // L5 长预警高伤
                case 5: return Tank;       // L6 是篝火，不打架（占位）
                case 6: return Giant;      // L7 霸体，卸力失效
                case 7: return Tank;       // L8 回访，带亚种
                case 8: return Moth;       // L9 是篝火，不打架（占位）
                default: return Lord;      // L10 首领
            }
        }

        /// <summary>地图节点的强度档：1=普通战斗格，2=精英，3=Boss。</summary>
        public static EnemyTemplate ForStage(int stageIndex, int tier)
        {
            return ForStage(stageIndex, tier, 0);
        }

        /// <summary>salt 决定这一场的亚种；传每局的种子就够，传 0 会给固定的一只。</summary>
        public static EnemyTemplate ForStage(int stageIndex, int tier, int salt)
        {
            return ForStageOf(IdForStage(stageIndex), stageIndex, tier, salt);
        }

        /// <summary>指定是哪只怪，按同样的层数/档位/亚种规则算数值。体检台排"同一层谁最难"要用它。</summary>
        public static EnemyTemplate ForStageOf(string id, int stageIndex, int tier, int salt)
        {
            EnemyTemplate baseT = Get(id);
            if (tier < 1) tier = 1;
            if (tier > 3) tier = 3;

            EnemyVariant v = EnemyVariants.Pick(stageIndex, tier, salt);

            int stage = stageIndex + 1;   // 入参是 0 基，这里换成"第几层"再算缩放
            // 层数负责"越往下越硬"，档位负责"同一个怪在精英格上不该一样弱"。
            // 十层的斜率比七层平：原来 0.06/层走到 L10 就是 1.54 倍血，最后两层会变成纯数值墙。
            // 精英格的层数缩放封顶在第 8 层：再往上叠，第 9 层的精英会难到 47%（比 Boss 的 52% 还低），
            // 玩家会在临门一脚之前一律绕开精英格。最后那一层的陡坡留给 Boss。
            int scaleStage = (tier == 2 && stage > 8) ? 8 : stage;
            // 第三版（牌池 16→24 之后重标定）：每个职业只能拿到自己那 6 张职业牌了，
            // 以前 bench 里两个职业共用同一份 8 张池子，等于双方都能抽到对方的王牌（猛袭/连环刺），
            // 曲线是被高估的。按真实牌池重跑后中段掉到 66~74%，所以把斜率压平。
            float hpMul = 1f + 0.038f * (scaleStage - 1) + (tier == 3 ? 0.15f : tier == 2 ? 0.06f : 0f);
            float dmgMul = 1f + 0.015f * (scaleStage - 1) + (tier == 3 ? 0.05f : tier == 2 ? 0.015f : 0f);
            if (v != null) { hpMul *= v.HpMul; dmgMul *= v.DmgMul; }
            // 每只怪的"耐力"不一样：游丝血薄、常在把高伤害打出来之前就被打死，所以同层里它最软。
            // 这张系数是把六只放到同一层实测后（balance.exe rank 7）拿来拉平的，
            // 目的是让「第几层」决定硬度、「哪只怪」只决定手法，否则曲线会一层上、一层下。
            switch (id)
            {
                // 游丝在精英格上不能再加：它的伤害本来就高，血一厚就变成"打不死 + 每下都痛"，
                // 实测 L6 精英从 69% 掉到 50%，比 Boss 还难。
                case Eel: hpMul *= tier >= 2 ? 1.02f : 1.12f; break;
                case Slime: hpMul *= 1.10f; break;
                case Moth: hpMul *= 1.08f; break;
                case Tank: hpMul *= 1.05f; break;
                default: break;
            }
            int leadRaw = (tier >= 2 ? 1 : 0) + (v != null ? v.LeadTrim : 0);   // 精英以上预警短一格，反应窗口更紧
            int leadTrim = leadRaw > 1 ? 1 : leadRaw;   // 封顶一格：再短就等于把"看预警"这个玩法拿掉了

            EnemyTemplate t = new EnemyTemplate
            {
                Id = baseT.Id,
                Name = (v != null ? v.Prefix : "") + baseT.Name + (tier == 3 ? " · 母体" : tier == 2 ? " · 精英" : ""),
                MaxHp = Math.Max(1, (int)Math.Round(baseT.MaxHp * hpMul)),
                Phase2AtPercent = baseT.Phase2AtPercent,
                Phase2BonusDamage = baseT.Phase2BonusDamage,
                Phase2LeadReduction = baseT.Phase2LeadReduction,
                Phase2Breathe = baseT.Phase2Breathe,
                Moves = new List<EnemyMove>()
            };
            for (int i = 0; i < baseT.Moves.Count; i++)
            {
                EnemyMove m = baseT.Moves[i];
                t.Moves.Add(new EnemyMove(
                    m.Name,
                    m.Duration,
                    Math.Max(1, (int)Math.Round(m.Damage * dmgMul)),
                    Math.Max(3, m.Lead - leadTrim),
                    m.Armor,
                    m.Gap));
            }
            return t;
        }
    }
}