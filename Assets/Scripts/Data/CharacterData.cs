using UnityEngine;
using System.Collections.Generic;

// 定义被动技能的类型。注意：.asset 里存的是 int（0/1），所以只能往后追加，不能改顺序。
public enum PassiveSkillType
{
    Sweeper_GuardHeal = 0, // 清道夫：格挡真的挡下伤害时回 2 血，战后按已损失血量回 30%
    Dissector_Tempo = 1    // 解剖师：打出占 1 格、且花灵能的牌，立刻回 1 点灵能
}

[CreateAssetMenu(fileName = "NewCharacter", menuName = "GameData/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("角色基础信息")]
    public string characterName;      // 角色名字
    public Sprite characterSprite;    // 角色立绘/头像（选人界面用）

    [Header("战斗专属")]
    public Sprite battleSprite;       // 【新增】战斗里用的Q版小人立绘！

    public int maxHp;                 // 最大血量

    [Header("专属被动技能")]
    public PassiveSkillType passiveSkill; // 下拉菜单选技能

    [Header("给玩家看的说明（选人界面直接读这三个字段）")]
    [Tooltip("被动名字，例如「格挡回血」")] public string passiveName;
    [TextArea(2, 4)]
    [Tooltip("一句话说清被动什么时候生效、生效多少")] public string passiveDesc;
    [Tooltip("这个角色怎么打，用来区分两个职业的手感")] public string tagline;

    [Header("卡组配置")]
    public List<CardData> initialDeck = new List<CardData>();       // 初始牌库（自带的牌）
    public List<CardData> exclusiveCardPool = new List<CardData>(); // 专属战利品卡池（打赢掉落的牌）
}