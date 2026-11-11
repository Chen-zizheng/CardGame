using System;

// BattleSystem 的事件出口（partial 拆分自 BattleSystem.cs）
// 战斗逻辑只负责 Invoke，不关心谁在听。表现层（音效/粒子/镜头）订阅这里，
// 这样后面加打击感不需要再改结算代码。
public partial class BattleSystem
{
    /// <summary>敌人受伤：damage = 本次伤害，remainingHp = 结算后血量</summary>
    public event Action<int, int> EnemyDamaged;

    /// <summary>玩家受伤：damage = 本次伤害，remainingHp = 结算后血量</summary>
    public event Action<int, int> PlayerDamaged;

    /// <summary>一张牌被打出（结算前触发，拿到的是原始卡数据）</summary>
    public event Action<CardData> CardPlayed;

    /// <summary>新回合开始：turn = 第几回合（从 1 开始）</summary>
    public event Action<int> TurnStarted;

    /// <summary>战斗结束：true = 胜利</summary>
    public event Action<bool> BattleEnded;

    /// <summary>抽牌：count = 这次抽了几张（一次抽牌只触发一次，不按张数刷音效）</summary>
    public event Action<int> CardsDrawn;

    private void RaiseEnemyDamaged(int damage, int remainingHp) { EnemyDamaged?.Invoke(damage, remainingHp); }
    private void RaisePlayerDamaged(int damage, int remainingHp) { PlayerDamaged?.Invoke(damage, remainingHp); }
    private void RaiseCardPlayed(CardData card) { CardPlayed?.Invoke(card); }
    private void RaiseTurnStarted(int turn) { TurnStarted?.Invoke(turn); }
    private void RaiseBattleEnded(bool win) { BattleEnded?.Invoke(win); }
    private void RaiseCardsDrawn(int count) { CardsDrawn?.Invoke(count); }
}
