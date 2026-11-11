using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>一条本能：角色自带或本局长出来的被动，用 Flag 位合并进 CombatConfig.Talents。</summary>
    public sealed class TalentDef
    {
        public string Id;
        public string Name;
        public string Desc;
        /// <summary>图标名（Resources/Art/Talents/&lt;Icon&gt;）。之前用单字，太小的时候像标记符不像图标。</summary>
        public string Icon;
        public int Flag;
        /// <summary>属于哪个角色（0 清道夫 / 1 解剖师，与 PassiveSkillType 的数值一致）。</summary>
        public int Owner;
        /// <summary>角色初始携带的那一条：选人时就写进本局，不算"选过一次"。</summary>
        public bool Starter;
    }

    /// <summary>
    /// 本能系统（纯逻辑）。
    ///
    /// 定位：不挂在遗物体系里。遗物是"这局捡到了什么"，本能是"这个人本来就会什么"——
    /// 所以每个角色有自己的一池，开局就带第一条（角色辨识），之后每 3 层从本角色的池里长一条。
    ///
    /// 为什么一条都不碰灵能：体检台实测灵能满溢 27~51%、时间轴空转 56~64%，
    /// 灵能本来就用不完，再加"回灵能"等于加空气。真正的短板是
    /// ①格挡盖不住长预警 ②卸力是唯一的完整解法 ③回收流只有三张牌在喂。
    /// 清道夫补这三处；解剖师补"碎牌多打"这条线（1 格牌、多段、抽牌）。
    /// </summary>
    public static class TalentLibrary
    {
        // 清道夫
        public const string Brace = "brace";
        public const string Counter = "counter";
        public const string Scavenge = "scavenge";
        public const string Stock = "stock";
        public const string Interrupt = "interrupt";
        public const string Tempo = "tempo";

        // 解剖师
        public const string Light = "light";
        public const string Multi = "multi";
        public const string Echo = "echo";
        public const string Read = "read";
        public const string Sprint = "sprint";

        public const int FlagBrace = 1 << 0;
        public const int FlagCounter = 1 << 1;
        public const int FlagScavenge = 1 << 2;
        public const int FlagStock = 1 << 3;
        public const int FlagInterrupt = 1 << 4;
        public const int FlagTempo = 1 << 5;
        public const int FlagLight = 1 << 6;
        public const int FlagMulti = 1 << 7;
        public const int FlagEcho = 1 << 8;
        public const int FlagRead = 1 << 9;
        public const int FlagSprint = 1 << 10;

        public const int Sweeper = 0;
        public const int Dissector = 1;

        /// <summary>存货：回收值上限加多少。</summary>
        public const int StockBonus = 15;
        /// <summary>回击：完整挡住一次攻击后反多少伤。</summary>
        public const int CounterDamage = 5;
        /// <summary>精准卸力：打断一招后反多少伤。</summary>
        public const int InterruptDamage = 7;
        /// <summary>拾荒：每次吸收伤害，回收值额外加多少。</summary>
        public const int ScavengeBonus = 2;
        /// <summary>轻装：只占 1 格的攻击牌加多少伤害。</summary>
        public const int LightBonus = 2;
        /// <summary>连击：多段牌每一段加多少伤害。</summary>
        public const int MultiBonus = 1;
        /// <summary>余韵：一次抽牌换多少回收值。</summary>
        public const int EchoGain = 2;
        /// <summary>疾走：打出 1 格的牌攒多少回收值。</summary>
        public const int SprintGain = 1;

        /// <summary>第几层打完给一个本能位（第一条是开局自带的，不算在里）。</summary>
        private static readonly int[] SlotLayers = { 3, 6, 9 };

        public static readonly TalentDef[] All =
        {
            // ---------------- 清道夫：格挡 / 卸力 / 回收 ----------------
            new TalentDef
            {
                Id = Brace, Name = "铁壁", Icon = "brace", Flag = FlagBrace, Owner = Sweeper, Starter = true,
                Desc = "所有格挡牌多盖 1 格"
            },
            new TalentDef
            {
                Id = Counter, Name = "回击", Icon = "counter", Flag = FlagCounter, Owner = Sweeper,
                Desc = "把一次攻击完整挡住（盾比它厚），反伤 " + CounterDamage + " 点"
            },
            new TalentDef
            {
                Id = Scavenge, Name = "拾荒", Icon = "scavenge", Flag = FlagScavenge, Owner = Sweeper,
                Desc = "每次挡下伤害，回收值额外 +" + ScavengeBonus
            },
            new TalentDef
            {
                Id = Stock, Name = "存货", Icon = "stock", Flag = FlagStock, Owner = Sweeper,
                Desc = "回收值上限 +" + StockBonus
            },
            new TalentDef
            {
                Id = Interrupt, Name = "精准卸力", Icon = "interrupt", Flag = FlagInterrupt, Owner = Sweeper,
                Desc = "卸力打断的招式，反伤 " + InterruptDamage + " 点"
            },
            new TalentDef
            {
                Id = Tempo, Name = "节拍", Icon = "tempo", Flag = FlagTempo, Owner = Sweeper,
                Desc = "每打出一张牌，回收值 +1"
            },

            // ---------------- 解剖师：碎牌 / 多段 / 抽牌 ----------------
            new TalentDef
            {
                Id = Light, Name = "轻装", Icon = "light", Flag = FlagLight, Owner = Dissector, Starter = true,
                Desc = "只占 1 格的攻击牌 +" + LightBonus + " 伤害"
            },
            new TalentDef
            {
                Id = Multi, Name = "连击", Icon = "multi", Flag = FlagMulti, Owner = Dissector,
                Desc = "多段攻击每一段 +" + MultiBonus + " 伤害"
            },
            new TalentDef
            {
                Id = Echo, Name = "余韵", Icon = "echo", Flag = FlagEcho, Owner = Dissector,
                Desc = "一次抽牌，回收值 +" + EchoGain
            },
            new TalentDef
            {
                Id = Read, Name = "预判", Icon = "read", Flag = FlagRead, Owner = Dissector,
                Desc = "每场战斗的第一张牌不花灵能"
            },
            new TalentDef
            {
                Id = Sprint, Name = "疾走", Icon = "sprint", Flag = FlagSprint, Owner = Dissector,
                Desc = "打出只占 1 格的牌，回收值 +" + SprintGain
            }
        };

        public static TalentDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static bool Has(int mask, int flag)
        {
            return (mask & flag) != 0;
        }

        public static int MaskOf(IList<string> ids)
        {
            int mask = 0;
            if (ids == null) return 0;
            for (int i = 0; i < ids.Count; i++)
            {
                TalentDef def = Get(ids[i]);
                if (def != null) mask |= def.Flag;
            }
            return mask;
        }

        /// <summary>这个角色开局自带的那一条（找不到返回 null）。</summary>
        public static TalentDef StarterFor(int owner)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Owner == owner && All[i].Starter) return All[i];
            return null;
        }

        /// <summary>这个角色之后可以长的那些（不含初始那条）。</summary>
        public static List<TalentDef> ForOwner(int owner)
        {
            List<TalentDef> list = new List<TalentDef>();
            for (int i = 0; i < All.Length; i++)
                if (All[i].Owner == owner && !All[i].Starter) list.Add(All[i]);
            return list;
        }

        /// <summary>从本角色还没长出来的里抽 count 个（顺序由 rng 决定，同一局可复现）。</summary>
        public static List<string> RollChoices(int owner, IList<string> owned, IRandom rng, int count)
        {
            List<TalentDef> pool = ForOwner(owner);
            List<string> ids = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                bool has = false;
                if (owned != null) for (int j = 0; j < owned.Count; j++) if (owned[j] == pool[i].Id) has = true;
                if (!has) ids.Add(pool[i].Id);
            }

            if (rng != null)
            {
                for (int i = ids.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    string tmp = ids[i];
                    ids[i] = ids[j];
                    ids[j] = tmp;
                }
            }

            if (ids.Count > count) ids.RemoveRange(count, ids.Count - count);
            return ids;
        }

        /// <summary>打到第 stageIndex 层（并打赢它）为止，一共该有几个"长出来的"本能。</summary>
        public static int SlotsForStage(int stageIndex)
        {
            int slots = 0;
            for (int i = 0; i < SlotLayers.Length; i++) if (stageIndex >= SlotLayers[i]) slots++;
            return slots;
        }

        /// <summary>下一个本能位在第几层（拿满了返回 0）。</summary>
        public static int NextSlotLayer(int taken)
        {
            if (taken < 0) taken = 0;
            if (taken >= SlotLayers.Length) return 0;
            return SlotLayers[taken];
        }
    }
}
