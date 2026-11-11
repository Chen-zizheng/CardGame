using System;
using System.Collections.Generic;

namespace CardCombat
{
    /// <summary>
    /// 实时时间轴战斗的纯逻辑核心。零 UnityEngine 依赖，可以在命令行里跑十万局。
    /// 时间单位是"格"（tick），谁在什么真实时间推进格子由外层决定，这里完全不知道。
    /// </summary>
    public sealed class CombatSim
    {
        private readonly CombatConfig _cfg;
        private readonly EnemyTemplate _enemy;
        private readonly IRandom _rng;
        private readonly List<CardDef> _hand = new List<CardDef>();
        private readonly List<CardDef> _drawPile = new List<CardDef>();
        private readonly List<CardDef> _discardPile = new List<CardDef>();
        private readonly List<Act> _acts = new List<Act>();
        private readonly List<CombatEvent> _events = new List<CombatEvent>();

        private int _seq;
        private int _moveIndex = -1;
        private int _lastEnemyEnd;
        // 首领换招时欠玩家的那几格空档（那一刻手上没排招时先记着，下一招补上）。
        private int _pendingBreathe;
        private bool _stopwatchUsed;
        private bool _readUsed;

        public CombatSim(CombatConfig cfg, IList<string> deckIds, EnemyTemplate enemy, IRandom rng)
        {
            _cfg = cfg != null ? cfg.Clone() : new CombatConfig();
            _enemy = enemy ?? EnemyLibrary.Get(EnemyLibrary.Slime);
            _rng = rng ?? new SeededRandom(1);

            PlayerMaxHp = _cfg.PlayerMaxHp;
            PlayerHp = _cfg.PlayerMaxHp;
            EnemyMaxHp = _enemy.MaxHp;
            EnemyHp = _enemy.MaxHp;
            Energy = _cfg.StartEnergy;
            Tick = 0;

            // 防毒面具：开场自带 5 点格挡，盖住前 3 格。它不花灵能、不占手牌，
            // 所以结算成一条"开局就摆好的格挡动作"，走的还是同一套覆盖判定。
            if (RelicLibrary.Has(_cfg.Relics, RelicLibrary.FlagMask))
            {
                _acts.Add(new Act
                {
                    Id = ++_seq,
                    Owner = ActOwner.Player,
                    CardId = null,
                    Name = "防毒面具",
                    Kind = CardKind.Guard,
                    Block = 5,
                    Start = 0,
                    End = 2
                });
            }

            if (deckIds != null)
            {
                for (int i = 0; i < deckIds.Count; i++)
                {
                    CardDef def = CardLibrary.Get(deckIds[i]);
                    if (def != null) _drawPile.Add(def);
                }
            }

            Shuffle(_drawPile);
            for (int i = 0; i < _cfg.StartHand; i++) DrawOne();
            PlanEnemy();
        }

        public CombatConfig Config { get { return _cfg; } }
        public EnemyTemplate Enemy { get { return _enemy; } }
        public int Tick { get; private set; }
        public int PlayerHp { get; private set; }
        public int PlayerMaxHp { get; private set; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; private set; }
        public int Energy { get; private set; }
        public int EnergyCap { get { return _cfg.EnergyCap; } }

        /// <summary>回收值：格挡真的吃下伤害才涨，是清道夫"以挡养打"的弹药。</summary>
        public int Gauge { get; private set; }
        public int GaugeMax
        {
            get
            {
                int max = _cfg.GaugeMax;
                if (TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagStock)) max += TalentLibrary.StockBonus;
                return max;
            }
        }

        public bool Phase2 { get; private set; }
        public CombatOutcome Outcome { get; private set; }
        public string LastBlockReason { get; private set; }

        public List<CardDef> Hand { get { return _hand; } }
        public List<Act> Acts { get { return _acts; } }
        public int DrawPileCount { get { return _drawPile.Count; } }
        public int DiscardPileCount { get { return _discardPile.Count; } }
        public int TotalCards { get { return _drawPile.Count + _hand.Count + _discardPile.Count; } }

        // 本场累计（结算屏要的"这一局你干了什么"）。只加不减，随战斗结束被 GameManager 取走。
        public int StatCardsPlayed { get; private set; }

        /// <summary>被动回血累计（体检台用，不影响战斗）。</summary>
        public int StatPassiveHeal { get; private set; }
        public int StatDamageDealt { get; private set; }
        public int StatDamageBlocked { get; private set; }
        public int StatInterrupts { get; private set; }
        public bool IsOver { get { return Outcome != CombatOutcome.None; } }

        /// <summary>
        /// 把上一场剩下的伤带到这一场（宿主用它继承存档血量）。
        /// 传进来的是「已经掉了多少血」，不是当前血量。
        /// </summary>
        public void ApplyCarryOverDamage(int amount)
        {
            if (amount <= 0) return;
            PlayerHp = Math.Max(1, PlayerHp - amount);
            if (PlayerHp <= 0) Outcome = CombatOutcome.Lose;
        }

        /// <summary>下一张牌会落在哪一格。</summary>
        public int NextSlot()
        {
            return Math.Max(Tick + 1, PlayerEnd() + 1);
        }

        /// <summary>玩家已排动作里最晚的结束格。</summary>
        public int PlayerEnd()
        {
            int end = 0;
            for (int i = 0; i < _acts.Count; i++)
            {
                Act a = _acts[i];
                if (a.Owner == ActOwner.Player && a.End > end) end = a.End;
            }
            return end;
        }

        /// <summary>这一张牌实际要花多少灵能（本场第一张 + 预判 = 0）。</summary>
        private int EffectiveCost(CardDef def)
        {
            if (!_readUsed && def.Cost > 0 && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagRead)) return 0;
            return def.Cost;
        }
        public bool CanPlay(int handIndex, out string reason)
        {
            reason = null;
            if (IsOver) { reason = "战斗已结束"; return false; }
            if (handIndex < 0 || handIndex >= _hand.Count) { reason = "没有这张牌"; return false; }
            CardDef def = _hand[handIndex];
            int need = EffectiveCost(def);
            if (need > Energy) { reason = "灵能不足（需要 " + need + "，现有 " + Energy + "）"; return false; }
            if (NextSlot() > Tick + 7) { reason = "时间轴排满了"; return false; }
            return true;
        }

        public bool TryPlay(int handIndex, out string reason)
        {
            if (!CanPlay(handIndex, out reason)) return false;
            CardDef def = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            int cost = EffectiveCost(def);
            Energy -= cost;
            if (cost == 0 && def.Cost > 0) _readUsed = true;

            int start = NextSlot();
            Act act = new Act
            {
                Id = ++_seq,
                Owner = ActOwner.Player,
                CardId = def.Id,
                Name = def.Name,
                Start = start,
                End = start + Math.Max(1, def.Duration) - 1,
                Damage = def.Damage,
                Block = def.Block,
                Heal = def.Heal,
                Draw = def.Draw,
                SelfDamage = def.SelfDamage,
                Kind = def.Kind,
                HitsPerTick = def.HitsPerTick,
                EnergyGain = def.EnergyGain,
                Cost = def.Cost,
                GaugeDamagePercent = def.GaugeDamagePercent,
                ConsumeGauge = def.ConsumeGauge,
                GaugeGainMul = def.GaugeGainMul
            };
            // 橡胶手套：格挡牌整体加厚。加在 act 上而不是牌面上，
            // 这样卡面文案不用分"有遗物/没遗物"两套。
            if (act.Kind == CardKind.Guard && RelicLibrary.Has(_cfg.Relics, RelicLibrary.FlagGloves))
                act.Block += 2;

            // 秒表：每场只有第一张牌能省这一格，用完就没了。
            if (!_stopwatchUsed && act.End > act.Start && RelicLibrary.Has(_cfg.Relics, RelicLibrary.FlagStopwatch))
            {
                act.End -= 1;
                _stopwatchUsed = true;
            }

            // 本能·铁壁：格挡牌朝后多盖一格。它不加厚度只加长度——
            // 体检台证明格挡的短板是"盖不住长预警"，不是数值不够。
            if (act.Kind == CardKind.Guard && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagBrace))
                act.End += 1;

            // 本能·轻装（解剖师）：只占 1 格的攻击牌贴身更快，伤害 +2
            if (act.Kind == CardKind.Attack && def.Duration <= 1
                && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagLight))
                act.Damage += TalentLibrary.LightBonus;

            _acts.Add(act);
            StatCardsPlayed++;

            // 本能·节拍：出牌就攒一点回收值，清道夫的弹药从"挡得住"变成"动得多"。
            if (TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagTempo) && GaugeMax > 0 && Gauge < GaugeMax)
            {
                Gauge++;
                Push(CombatEventKind.Reclaimed, ActOwner.Player, start, 1, "本能·节拍");
            }

            // 本能·疾走（解剖师）：1 格的牌不占地方，落地就攒一点回收
            if (def.Duration <= 1 && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagSprint)
                && GaugeMax > 0 && Gauge < GaugeMax)
            {
                Gauge++;
                Push(CombatEventKind.Reclaimed, ActOwner.Player, start, 1, "本能·疾走");
            }

            // 解剖师被动：靠"小动作"换节奏。只占 1 格、且真的花了灵能的牌，落地当下就回 1 点。
            if (_cfg.Passive == PassiveId.DissectorTempo && def.Duration <= 1 && def.Cost >= 1 && Energy < EnergyCap)
            {
                Energy++;
                Push(CombatEventKind.Drawn, ActOwner.Player, start, Energy, "被动·回灵能");
            }

            // 解剖师的续航：打出任何一张牌都回一点生命。触发条件故意比回灵能松 ——
            // 回灵能那句是"碎牌换节奏"，回血这句是"她靠不停地动活着"，
            // 不是格挡换血（那是清道夫的），所以挂在出牌上而不是挡伤上。
            if (_cfg.Passive == PassiveId.DissectorTempo && _cfg.DissectorHealPerCard > 0
                && PlayerHp > 0 && PlayerHp < PlayerMaxHp
                && (_cfg.DissectorHealEveryN <= 1 || StatCardsPlayed % _cfg.DissectorHealEveryN == 0))
            {
                int heal = Math.Min(_cfg.DissectorHealPerCard, PlayerMaxHp - PlayerHp);
                PlayerHp += heal;
                StatPassiveHeal += heal;
                Push(CombatEventKind.Healed, ActOwner.Player, start, heal, "被动·回收");
            }
            _discardPile.Add(def);
            Push(CombatEventKind.Played, ActOwner.Player, act.End, def.Cost, def.Name);
            return true;
        }

        /// <summary>推进一格。这是整个游戏唯一的时间入口。</summary>
        public void Step()
        {
            if (IsOver) return;
            Tick++;

            if (_cfg.EnergyEveryTicks > 0 && Tick % _cfg.EnergyEveryTicks == 0 && Energy < EnergyCap)
                Energy++;

            if (_cfg.DrawEveryTicks > 0 && Tick % _cfg.DrawEveryTicks == 0 && _hand.Count < _cfg.HandCap)
                DrawOne();

            ResolveAt(Tick);

            if (!IsOver) PlanEnemy();

            if (!IsOver && Tick >= _cfg.MaxTicks)
            {
                Outcome = CombatOutcome.Lose;
                Push(CombatEventKind.PlayerHurt, ActOwner.Enemy, Tick, 0, "超时");
            }
        }

        public List<CombatEvent> DrainEvents()
        {
            if (_events.Count == 0) return _events;
            List<CombatEvent> copy = new List<CombatEvent>(_events);
            _events.Clear();
            return copy;
        }

        public void ClearEvents()
        {
            _events.Clear();
        }

        // ---------------- 内部 ----------------

        private void ResolveAt(int tick)
        {
            // 敌人先结算：慢牌有风险
            for (int i = 0; i < _acts.Count; i++)
            {
                Act a = _acts[i];
                if (a.Owner != ActOwner.Enemy || a.Resolved || a.Cancelled || a.End != tick) continue;

                bool staggered = false;
                for (int j = 0; j < _acts.Count; j++)
                {
                    Act s = _acts[j];
                    if (s.Owner == ActOwner.Player && s.Kind == CardKind.Stagger && s.Covers(tick)) staggered = true;
                }

                a.Resolved = true;

                if (staggered)
                {
                    if (a.Armor)
                    {
                        Push(CombatEventKind.Armored, ActOwner.Enemy, tick, 0, a.Name);
                    }
                    else
                    {
                        a.Cancelled = true;
                        StatInterrupts++;
                        Push(CombatEventKind.Interrupted, ActOwner.Enemy, tick, 0, a.Name);

                        // 本能·精准卸力：打断本身就是伤害来源，把"纯防守"变成换血手段。
                        if (TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagInterrupt))
                        {
                            EnemyHp = Math.Max(0, EnemyHp - TalentLibrary.InterruptDamage);
                            Push(CombatEventKind.EnemyHurt, ActOwner.Player, tick,
                                TalentLibrary.InterruptDamage, "本能·精准卸力");
                            if (EnemyHp <= 0)
                            {
                                Outcome = CombatOutcome.Win;
                                return;
                            }
                        }
                        continue;
                    }
                }

                int block = 0;
                for (int j = 0; j < _acts.Count; j++)
                {
                    Act g = _acts[j];
                    if (g.Owner == ActOwner.Player && g.Kind == CardKind.Guard && g.Covers(tick)) block += g.Block;
                }

                int damage = Math.Max(0, a.Damage - block);
                if (block > 0) Push(CombatEventKind.Blocked, ActOwner.Player, tick, block, a.Name);

                // 拖把桶：格挡有富余（盾比这一招还厚），富余那一格换 1 点灵能。
                // 和回收值互斥——攒弹药是"吃下伤害"，拖把桶是"根本不用吃"。
                if (RelicLibrary.Has(_cfg.Relics, RelicLibrary.FlagBucket) && block > a.Damage && Energy < EnergyCap)
                {
                    Energy++;
                    Push(CombatEventKind.Drawn, ActOwner.Player, tick, Energy, "拖把桶·余量转灵能");
                }

                // 回收：挡下来的那一部分（不是举起来的盾）才存得进弹药库。
                int absorbed = Math.Min(block, a.Damage);
                StatDamageBlocked += absorbed;
                if (absorbed > 0 && GaugeMax > 0)
                {
                    float mul = 1f;
                    for (int j = 0; j < _acts.Count; j++)
                    {
                        Act g = _acts[j];
                        if (g.Owner == ActOwner.Player && g.Kind == CardKind.Guard && g.Covers(tick) && g.GaugeGainMul > mul)
                            mul = g.GaugeGainMul;
                    }
                    int gain = (int)Math.Round(absorbed * mul * _cfg.GaugeAbsorbPercent / 100.0);
                    if (gain > 0 && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagScavenge))
                        gain += TalentLibrary.ScavengeBonus;
                    if (gain > 0)
                    {
                        int before = Gauge;
                        Gauge = Math.Min(GaugeMax, Gauge + gain);
                        if (Gauge > before) Push(CombatEventKind.Reclaimed, ActOwner.Player, tick, Gauge - before, "回收");
                    }
                }

                // 清道夫被动：格挡真的挡下了东西才回血，空举盾不算。
                if (_cfg.Passive == PassiveId.SweeperGuardHeal && block > 0 && damage < a.Damage
                    && PlayerHp > 0 && PlayerHp < PlayerMaxHp && _cfg.PassiveHealAmount > 0)
                {
                    int before = PlayerHp;
                    PlayerHp = Math.Min(PlayerMaxHp, PlayerHp + _cfg.PassiveHealAmount);
                    if (PlayerHp > before) Push(CombatEventKind.Healed, ActOwner.Player, tick, PlayerHp - before, "被动·格挡回血");
                }
                if (damage > 0)
                {
                    PlayerHp = Math.Max(0, PlayerHp - damage);
                    Push(CombatEventKind.PlayerHurt, ActOwner.Enemy, tick, damage, a.Name);
                }

                // 本能·回击：这一招被完整挡住（盾比它厚），就反手打一下。
                if (TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagCounter) && PlayerHp > 0
                    && a.Damage > 0 && block >= a.Damage)
                {
                    EnemyHp = Math.Max(0, EnemyHp - TalentLibrary.CounterDamage);
                    Push(CombatEventKind.EnemyHurt, ActOwner.Player, tick,
                        TalentLibrary.CounterDamage, "本能·回击");
                    if (EnemyHp <= 0)
                    {
                        Outcome = CombatOutcome.Win;
                        return;
                    }
                }
                if (PlayerHp <= 0)
                {
                    Outcome = CombatOutcome.Lose;
                    return;
                }
            }

            for (int i = 0; i < _acts.Count; i++)
            {
                Act a = _acts[i];
                if (a.Owner != ActOwner.Player || a.Resolved || a.Cancelled) continue;

                bool multi = a.HitsPerTick > 0;
                bool hitNow = multi && a.Covers(tick);
                bool atEnd = a.End == tick;
                if (!hitNow && !atEnd) continue;

                // 多段牌：每覆盖一格就打一次，最后一格再补治疗/抽牌/自伤/回灵能。
                int hitDamage = a.Damage;
                if (multi && RelicLibrary.Has(_cfg.Relics, RelicLibrary.FlagNeon)) hitDamage += 1;
                if (multi && TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagMulti)) hitDamage += TalentLibrary.MultiBonus;

                if (multi && hitNow && a.Damage > 0)
                {
                    EnemyHp = Math.Max(0, EnemyHp - hitDamage);
                    Push(CombatEventKind.EnemyHurt, ActOwner.Player, tick, hitDamage, a.Name);
                    if (EnemyHp <= 0)
                    {
                        a.Resolved = true;
                        Outcome = CombatOutcome.Win;
                        return;
                    }
                }

                if (!atEnd) continue;
                a.Resolved = true;

                if (!multi)
                {
                    // 回收流派：伤害 = 牌面 + 回收值换算，清运清空弹药、废料铲保留。
                    int dmg = a.Damage;
                    if (a.GaugeDamagePercent > 0 && Gauge > 0)
                    {
                        dmg += Gauge * a.GaugeDamagePercent / 100;
                        if (a.ConsumeGauge) Gauge = 0;
                    }
                    if (dmg > 0)
                    {
                        EnemyHp = Math.Max(0, EnemyHp - dmg);
                        Push(CombatEventKind.EnemyHurt, ActOwner.Player, tick, dmg, a.Name);
                        if (EnemyHp <= 0)
                        {
                            Outcome = CombatOutcome.Win;
                            return;
                        }
                    }
                }

                if (a.EnergyGain > 0)
                {
                    int before = Energy;
                    Energy = Math.Min(EnergyCap, Energy + a.EnergyGain);
                    if (Energy > before) Push(CombatEventKind.Drawn, ActOwner.Player, tick, Energy, a.Name + "·回灵能");
                }

                if (a.Heal > 0)
                {
                    int before = PlayerHp;
                    PlayerHp = Math.Min(PlayerMaxHp, PlayerHp + a.Heal);
                    Push(CombatEventKind.Healed, ActOwner.Player, tick, PlayerHp - before, a.Name);
                }

                if (a.Draw > 0)
                {
                    for (int k = 0; k < a.Draw && _hand.Count < _cfg.HandCap; k++) DrawOne();

                    // 本能·余韵（解剖师）：抽牌本身就是她的资源，抽一次攒一次
                    if (TalentLibrary.Has(_cfg.Talents, TalentLibrary.FlagEcho) && GaugeMax > 0 && Gauge < GaugeMax)
                    {
                        int before = Gauge;
                        Gauge = System.Math.Min(GaugeMax, Gauge + TalentLibrary.EchoGain);
                        if (Gauge > before)
                            Push(CombatEventKind.Reclaimed, ActOwner.Player, tick, Gauge - before, "本能·余韵");
                    }
                }

                if (a.SelfDamage > 0)
                {
                    PlayerHp = Math.Max(0, PlayerHp - a.SelfDamage);
                    Push(CombatEventKind.PlayerHurt, ActOwner.Player, tick, a.SelfDamage, a.Name + "·自伤");
                    if (PlayerHp <= 0)
                    {
                        Outcome = CombatOutcome.Lose;
                        return;
                    }
                }
            }

            if (!Phase2 && EnemyHp <= EnemyMaxHp * _enemy.Phase2AtPercent / 100)
            {
                Phase2 = true;
                int breathe = _enemy.Phase2Breathe;
                bool pushed = false;
                if (breathe > 0)
                {
                    // 手上已经排好的那一招往后推，等于当场多出两格空档；
                    // 如果那一刻它刚好没排招，就把这两格记下来，留给下一招。
                    for (int i = 0; i < _acts.Count; i++)
                    {
                        Act ea = _acts[i];
                        if (ea.Owner != ActOwner.Enemy || ea.Resolved || ea.Cancelled) continue;
                        ea.Start += breathe;
                        ea.End += breathe;
                        pushed = true;
                    }
                    if (pushed) _lastEnemyEnd += breathe;
                    else _pendingBreathe = breathe;
                }
                Push(CombatEventKind.Enraged, ActOwner.Enemy, tick, _enemy.Phase2BonusDamage, _enemy.Name);
                if (breathe > 0) Push(CombatEventKind.Breathe, ActOwner.Enemy, tick + breathe, breathe, _enemy.Name);
            }
        }

        private void PlanEnemy()
        {
            for (int i = 0; i < _acts.Count; i++)
            {
                Act a = _acts[i];
                if (a.Owner == ActOwner.Enemy && !a.Resolved && !a.Cancelled) return;
            }

            if (_enemy.Moves.Count == 0) return;

            _moveIndex = (_moveIndex + 1) % _enemy.Moves.Count;
            EnemyMove move = _enemy.Moves[_moveIndex];

            int lead = move.Lead - (Phase2 ? _enemy.Phase2LeadReduction : 0);
            int start = Tick + Math.Max(2, lead) + _pendingBreathe;
            _pendingBreathe = 0;
            int earliest = _lastEnemyEnd + move.Gap + 1;
            if (start < earliest) start = earliest;
            _acts.Add(new Act
            {
                Id = ++_seq,
                Owner = ActOwner.Enemy,
                CardId = _enemy.Id + "_" + move.Name,
                Name = move.Name,
                Start = start,
                End = start + Math.Max(1, move.Duration) - 1,
                Damage = move.Damage + (Phase2 ? _enemy.Phase2BonusDamage : 0),
                Kind = CardKind.Attack,
                Armor = move.Armor
            });
            _lastEnemyEnd = start + Math.Max(1, move.Duration) - 1;
            Push(CombatEventKind.Announced, ActOwner.Enemy, start, move.Damage, move.Name);
        }

        private void DrawOne()
        {
            if (_hand.Count >= _cfg.HandCap) return;
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) return;
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
            }
            if (_drawPile.Count == 0) return;
            CardDef def = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(def);
            Push(CombatEventKind.Drawn, ActOwner.Player, Tick, _hand.Count, def.Name);
        }

        private void Shuffle(List<CardDef> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                CardDef tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void Push(CombatEventKind kind, ActOwner owner, int tick, int amount, string label)
        {
            // 伤害统计只从这一处累加：所有来源（牌、回收换算、精准卸力、回击）都走同一个出口，
            // 散在各处加会漏掉后来新加的效果。
            if (kind == CombatEventKind.EnemyHurt && owner == ActOwner.Player && amount > 0)
                StatDamageDealt += amount;

            _events.Add(new CombatEvent
            {
                Kind = kind, Owner = owner, Tick = tick, Amount = amount, Label = label
            });
        }
    }
}
