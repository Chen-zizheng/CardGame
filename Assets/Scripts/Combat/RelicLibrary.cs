using System;
using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>
    /// 遗物：跨战斗保留的被动。和职业被动不一样，职业被动是"你是谁"，
    /// 遗物是"这一局你捡到了什么"，所以它必须能被存档带走、也能被命令行模拟器算胜率。
    /// </summary>
    public sealed class RelicDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public int Flag;
    }

    public static class RelicLibrary
    {
        public const string Stopwatch = "stopwatch";
        public const string Gloves = "gloves";
        public const string Bucket = "bucket";
        public const string Mask = "mask";
        public const string Neon = "neon";
        public const string Invoice = "invoice";

        public const int FlagStopwatch = 1 << 0;
        public const int FlagGloves = 1 << 1;
        public const int FlagBucket = 1 << 2;
        public const int FlagMask = 1 << 3;
        public const int FlagNeon = 1 << 4;
        public const int FlagInvoice = 1 << 5;

        /// <summary>结算单每点剩余灵能换多少补给点。</summary>
        public const int InvoiceGoldPerEnergy = 3;

        public static readonly RelicDef[] All =
        {
            new RelicDef { Id = Stopwatch, Name = "秒表", Flag = FlagStopwatch,
                Desc = "每场战斗的第一张牌少占 1 格" },
            new RelicDef { Id = Gloves, Name = "橡胶手套", Flag = FlagGloves,
                Desc = "所有格挡牌 +2 格挡" },
            new RelicDef { Id = Bucket, Name = "拖把桶", Flag = FlagBucket,
                Desc = "格挡比伤害多出来的那一格，转 1 点灵能" },
            new RelicDef { Id = Mask, Name = "防毒面具", Flag = FlagMask,
                Desc = "开场自带 5 点格挡，持续 3 格" },
            new RelicDef { Id = Neon, Name = "霓虹灯牌", Flag = FlagNeon,
                Desc = "多段攻击每一段 +1 伤害" },
            new RelicDef { Id = Invoice, Name = "结算单", Flag = FlagInvoice,
                Desc = "战斗结束时，每点剩余灵能换 " + InvoiceGoldPerEnergy + " 补给点" },
        };

        public static RelicDef Get(string id)
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
                RelicDef d = Get(ids[i]);
                if (d != null) mask |= d.Flag;
            }
            return mask;
        }

        /// <summary>还没拿到的遗物里随机挑一件（给定随机源，保证可复现）。</summary>
        public static string RollNew(IList<string> owned, IRandom rng)
        {
            List<RelicDef> pool = new List<RelicDef>();
            for (int i = 0; i < All.Length; i++)
            {
                bool has = false;
                if (owned != null)
                    for (int k = 0; k < owned.Count; k++) if (owned[k] == All[i].Id) { has = true; break; }
                if (!has) pool.Add(All[i]);
            }
            if (pool.Count == 0) return null;
            return pool[rng.Next(pool.Count)].Id;
        }
    }
}
