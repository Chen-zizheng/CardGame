using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>
    /// 事件里的一个选项。
    ///
    /// 全部是"数值字段"而不是行为：事件是手写的数据表，后果由 UI 层
    /// （EventPanel）逐项套到 GameManager 上。这样这套表既能在游戏里跑，
    /// 也能被 headless 台子直接读出来算期望收益，不用起 Unity。
    /// </summary>
    public sealed class EventChoice
    {
        public string Label;      // 按钮主文：一行，动词开头
        public string Hint;       // 按钮下面那行，把后果直接写清楚（不给惊喜）
        public int Heal;          // 回血
        public int Hp;            // 直接扣血（保底留 1 点，不会因为一个事件直接死）
        public int Gold;          // 补给点增减
        public int MaxHp;         // 最大生命增减（涨的时候当前血跟着涨）
        public string AddCard;    // 往牌组里塞一张牌（CardLibrary 的 id）
        public int RemoveCount;   // 随机删 N 张基础牌（优先「普通攻击」，删不动就删任意一张）
        public bool GiveRelic;    // 随机拿一件还没有的遗物；拿满了折算成补给点
        public int UpgradeCount;  // 随机强化 N 张还没强化过的牌（和篝火第三选同一套规则）
        public string Result;     // 选完之后显示的那一行

        public EventChoice(string label, string hint, string result)
        {
            Label = label; Hint = hint; Result = result;
        }
    }

    /// <summary>一个遭遇。身体文案是 2~4 短行，读起来像一句叙述而不是设定集。</summary>
    public sealed class EventDef
    {
        public string Id;
        public string Kicker;   // 「事件 · 培育槽」这种小标
        public string Title;
        public string Body;
        public EventChoice[] Choices;

        public EventDef(string id, string kicker, string title, string body, params EventChoice[] choices)
        {
            Id = id; Kicker = kicker; Title = title; Body = body; Choices = choices;
        }
    }

    /// <summary>
    /// 手写的十四个事件（一局能碰到 2~3 个，同一局不重复，读完十四个才开始重复）。
    ///
    /// 规矩：
    ///   1. 每个选项的代价和收益都写在 Hint 里，不玩"看起来很好其实很坑"。
    ///   2. 至少留一个"什么都不做"的出口 —— 玩家手里血少的时候要有路可走。
    ///   3. 后果只用已有的系统（血、钱、牌组、遗物），不新增资源。
    /// </summary>
    public static class EventLibrary
    {
        public const string Specimen = "specimen";
        public const string Scale = "scale";
        public const string Stash = "stash";
        public const string Molt = "molt";
        public const string Voice = "voice";
        public const string Queue = "queue";
        public const string Ledger = "ledger";
        public const string Shelf = "shelf";
        public const string Drain = "drain";
        public const string Lamp = "lamp";
        public const string Hatch = "hatch";
        public const string Table = "table";
        public const string Roster = "roster";
        public const string Echo = "echo";

        public static readonly EventDef[] All =
        {
            new EventDef(Specimen, "事件 · 培育槽", "槽里的东西还活着",
                "玻璃上贴着一只手掌，比你的小一圈，指节的位置对不上。\n" +
                "管子每抽回去一次，它就安静三秒，然后再把掌心按上来。",
                new EventChoice("抽一管血", "-8 生命 · +60 补给点", "血是热的。它没有挣，只是看着你把管子拧紧。")
                { Hp = 8, Gold = 60 },
                new EventChoice("把管子接回自己身上", "+22 生命 · 牌组多一张「普通攻击」", "液体进去那一下很轻，像有人在你胸口按了按。之后你听见自己多了半拍心跳。")
                { Heal = 22, AddCard = CardLibrary.Basic },
                new EventChoice("不管它，继续往下", "什么都不会发生", "走出十几步，抽气声还在。你决定当那是风机。")
            ),

            new EventDef(Scale, "事件 · 称重站", "一台还在工作的旧秤",
                "托盘上留着一层干掉的东西。屏幕没有归零，它在等下一个站上去的人。\n" +
                "秤旁边贴着一张手写的纸条：轻的往下走，重的留下来。",
                new EventChoice("站上去", "最大生命 +8 · 现在 -14 生命", "数字停在你的体重上，停了两秒，然后往下掉了三格。它替你减掉了一些东西。")
                { MaxHp = 8, Hp = 14 },
                new EventChoice("把一张牌压上去", "随机删掉一张基础牌 · +40 补给点", "牌在托盘上变成一张薄片。数字回零了，你的手感轻了一点。")
                { RemoveCount = 1, Gold = 40 },
                new EventChoice("记下那个数字就走", "+25 补给点", "你抄下那串数字。它和你现在的体重差了两个整数。")
                { Gold = 25 }
            ),

            new EventDef(Stash, "事件 · 通风口", "有人在这里存过东西",
                "通风口后面塞着一个包。包带上有编号，编号被磨掉了一半。\n" +
                "它属于上一个走到这里、然后没有继续往下的人。",
                new EventChoice("全部倒出来", "+75 补给点 · 牌组多一张「普通攻击」", "包里最值钱的是一只没拆封的止血钳。剩下的都是别人替他收拾好的、他再也用不上的东西。")
                { Gold = 75, AddCard = CardLibrary.Basic },
                new EventChoice("只拿能带走的", "+48 补给点", "你把包重新塞回去，压得比原来深一点。")
                { Gold = 48 },
                new EventChoice("留一张自己的牌给他", "删掉一张基础牌 · +12 生命", "你把牌立在包上，像一个路标。走出去的时候，你觉得有人在背后松了一下肩。")
                { RemoveCount = 1, Heal = 12 }
            ),

            new EventDef(Molt, "事件 · 蜕壳", "它还立在那里",
                "半透明的壳立在通道中间，形状像人，但关节比人多。\n" +
                "内壁是干净的 —— 它是自己走出来的，而且走得挺稳。",
                new EventChoice("钻进去试试", "最大生命 +10 · 牌组多一张「普通攻击」", "壳比想象的窄。出来的时候你的肩宽了一点点，动作也多了一点不属于自己的迟疑。")
                { MaxHp = 10, AddCard = CardLibrary.Basic },
                new EventChoice("烧掉它", "+18 生命", "火不大，烧得很久。你借着那点火烤干了外套，也烤暖了手指。")
                { Heal = 18 },
                new EventChoice("敲一块下来带走", "-6 生命 · +55 补给点", "碎片在罐子里一直轻轻发响，像有人在里面换了个姿势。")
                { Hp = 6, Gold = 55 }
            ),

            new EventDef(Voice, "事件 · 广播", "有人在念你的编号",
                "声音从头顶的格栅里下来，念的是你的编号，念得不算标准。\n" +
                "它停了停，像是在等一个回答。",
                new EventChoice("回答它", "随机拿到一件遗物（已集齐则 +80 补给点）", "格栅后面有什么东西放下了。你没有看清，只听见金属落地的声音。")
                { GiveRelic = true },
                new EventChoice("捂住耳朵往下走", "+14 生命 · +18 补给点", "走出去很远，它还在念。你开始怀疑它念的其实不是编号，是一个名字。")
                { Heal = 14, Gold = 18 },
                new EventChoice("用编号反问它是谁", "-5 生命 · 最大生命 +6", "它安静了。然后你的耳朵里响了一声，像有人在你身体里把什么东西拧正。")
                { Hp = 5, MaxHp = 6 }
            ),

            new EventDef(Queue, "事件 · 排队", "前面还有三个人",
                "走廊里排着队。你看不清他们的脸，只看得出每个人手里都捏着一张牌。\n" +
                "队伍往前挪得很慢，但没有一个人回头。",
                new EventChoice("插到最前面", "-10 生命 · 随机拿到一件遗物（已集齐则 +80 补给点）", "你把最前面那个人的东西拿走了。队伍没有乱，只是少了一个人的重量。")
                { Hp = 10, GiveRelic = true },
                new EventChoice("跟着排下去", "+16 生命 · +20 补给点", "轮到你的时候窗口已经关了。不过你在长椅上睡了一会儿，睡得很沉。")
                { Heal = 16, Gold = 20 },
                new EventChoice("数了数人数就走", "什么都不会发生", "三个人。你把这个数记住，然后忘了自己为什么要数。")
            ),

            new EventDef(Ledger, "事件 · 账本", "这一页写着你还没付的东西",
                "桌上摊着账本，钢笔还是湿的。你的编号在最后一行，后面那一栏空着。\n" +
                "再往下翻，是别人替你付掉的记录，一笔一笔，全打了勾。",
                new EventChoice("把自己的名字划掉", "-12 生命 · 最大生命 +12", "划得很用力，纸破了。你低头看了看手背，那里多了一道同方向的旧疤。")
                { Hp = 12, MaxHp = 12 },
                new EventChoice("替下一个人付掉", "删掉一张基础牌 · +26 生命", "你把自己的牌押上去，钢笔放回原处。走出去的时候，账本自己翻了一页。")
                { RemoveCount = 1, Heal = 26 },
                new EventChoice("合上它", "什么都不会发生", "合上的时候你听见纸页粘了一下，像有人在里面咽了口唾沫。")
            ),

            new EventDef(Shelf, "事件 · 标本架", "第七个罐子是空的",
                "架子上排着六个罐子，标签都朝外。第七个位置是空的，落灰的形状很整齐。\n" +
                "罐子里的东西都面朝同一个方向 —— 朝走廊的深处。",
                new EventChoice("拿走第六个罐子", "-7 生命 · +70 补给点", "罐子在包里很轻。走到下一段时你发现它在渗，渗出来的东西是温的。")
                { Hp = 7, Gold = 70 },
                new EventChoice("把第七个位置填上自己的东西", "删掉一张基础牌 · 最大生命 +8", "你放进去的是一张牌。标签你自己写的，字很丑。")
                { RemoveCount = 1, MaxHp = 8 },
                new EventChoice("按原样摆好就走", "+10 生命", "你把落灰的形状用手指抹平了。手指上留了一点白。")
                { Heal = 10 }
            ),

            new EventDef(Drain, "事件 · 排水口", "水是往回流的方向走的",
                "排水口没有盖子，水在里面往回流 —— 往上一层流。\n" +
                "上一层的东西泡在水里，转得很慢，像还没决定要不要沉下去。",
                new EventChoice("伸手进去捞", "-11 生命 · +85 补给点", "捞上来的东西是湿的、热的、还在动。你把它按进袋子里，它就不再动了。")
                { Hp = 11, Gold = 85 },
                new EventChoice("把一张牌浸一下", "删掉一张基础牌 · 最大生命 +9", "牌沉下去又浮上来，字被泡开了一半。你把它捞起来丢掉的时候，手指凉了一下。")
                { RemoveCount = 1, MaxHp = 9 },
                new EventChoice("盖上井盖走", "什么都不会发生", "井盖压上去的时候，下面的转动声停了一拍，又接上了。")
            ),

            new EventDef(Lamp, "事件 · 应急灯", "它还亮着",
                "应急灯罩着一小圈地面，光圈外面黑得很干净，像被剪出来的。\n" +
                "灯座侧面有一道凹槽，尺寸和你手里的牌一样。",
                new EventChoice("掰下灯管带走", "+5 生命 · +50 补给点", "灯管在包里一直温着。你走远之后，身后那圈光才暗下去。")
                { Heal = 5, Gold = 50 },
                new EventChoice("关掉它再走", "最大生命 +7 · 现在 -9 生命", "黑下来的那一秒，你听见自己的呼吸在墙上来回撞了三次。你记住了这件事。")
                { MaxHp = 7, Hp = 9 },
                new EventChoice("绕开它", "什么都不会发生", "你贴着墙走，没有进那圈光。走出去很远，眼睛才重新习惯。")
            ),

            new EventDef(Hatch, "事件 · 孵化池", "池子里还没有睁眼的东西",
                "池子里的东西都还是软的，靠在一起，像一锅没煮开的粥。\n" +
                "你把手伸进去，它们会让开一条缝，然后再合上。",
                new EventChoice("捞一只上来", "最大生命 +10 · 现在 -12 生命", "它在你的手臂里找到位置就不动了。之后你偶尔会觉得，有人在里面轻轻翻身。")
                { MaxHp = 10, Hp = 12 },
                new EventChoice("把整池倒掉", "+26 生命 · 删掉一张基础牌", "水下去之后池底是干净的。你把手在裤子上擦了两遍，还是觉得黏。")
                { Heal = 26, RemoveCount = 1 },
                new EventChoice("盖上盖子走", "+40 补给点", "盖子是沉的。你压上去的时候，下面有一阵很轻的顶动，像谁在里面点头。")
                { Gold = 40 }
            ),

            new EventDef(Table, "事件 · 手术台", "台子上还留着体温",
                "台面比你手心的温度高一点，中间凹下去一块，形状是一个人侧躺过的样子。\n" +
                "器械摊在旁边，摆得整整齐齐，少了一把刀。",
                new EventChoice("躺上去，躺一会儿", "最大生命 +14 · 现在 -18 生命", "你躺下去，凹处刚好合上。起身的时候你在台面上留了一个自己的形状。")
                { MaxHp = 14, Hp = 18 },
                new EventChoice("拿走台上剩下的工具", "随机一件遗物 · 现在 -6 生命", "工具套进手里尺寸正好。你走出两步，才想起来回头看那把少掉的刀。")
                { GiveRelic = true, Hp = 6 },
                new EventChoice("关灯走", "什么都不会发生", "灯灭之后台面还是温的。你没有再摸第二次。")
            ),

            new EventDef(Roster, "事件 · 值班表", "还有三个名字没划掉",
                "墙上的表已经翻到最后一页，三个名字留在那里，笔迹一个比一个淡。\n" +
                "表下面挂着一支笔，笔尖朝下，没人扶它也不掉。",
                new EventChoice("划掉自己的名字", "删掉一张基础牌 · +22 生命", "笔划过纸的时候很顺。划完你确实轻了一点，像少背了一件东西。")
                { RemoveCount = 1, Heal = 22 },
                new EventChoice("替它值一班", "-9 生命 · +70 补给点", "那一班没有钟。你数着自己的呼吸，数到七百多的时候，天亮了。")
                { Hp = 9, Gold = 70 },
                new EventChoice("不动它，走", "什么都不会发生", "走出去很远，你还在想那支笔为什么没掉下来。")
            ),

            new EventDef(Echo, "事件 · 回声井", "回音比你先到",
                "井口很窄，你朝下面喊了一个字，回音在半路就把这个字还给了你 ——\n" +
                "比你说得更清楚，用的是它自己的嗓子。",
                new EventChoice("再朝下面喊一声", "+18 生命", "这一声它没有还。你听见它往下沉，一路沉到听不见为止，胸口那口气才松开。")
                { Heal = 18 },
                new EventChoice("把一张牌丢下去", "随机强化一张牌", "牌落下去的时间比井深长。停在半空的时候，纸面上多了一道刻痕。")
                { UpgradeCount = 1 },
                new EventChoice("捂住耳朵走", "什么都不会发生", "走出去很远，那句回音还在等你。你没有回头。")
            ),
        };

        public static EventDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        /// <summary>
        /// 抽一个这一局还没出现过的事件。全出现过了就允许重复
        /// （10 层最多撞见 3~4 个事件格，5 个事件够用，但别在最后两层全是空白）。
        /// </summary>
        public static EventDef Roll(IRandom rng, IList<string> happened)
        {
            if (rng == null) rng = new SeededRandom(1);
            List<EventDef> pool = new List<EventDef>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                if (happened != null && happened.Contains(All[i].Id)) continue;
                pool.Add(All[i]);
            }
            if (pool.Count == 0)
            {
                for (int i = 0; i < All.Length; i++) pool.Add(All[i]);
            }
            return pool[rng.Next(pool.Count)];
        }
    }
}
