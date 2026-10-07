using UnityEngine;
using System.Collections.Generic;

// 定义被动技能的类型
public enum PassiveSkillType
{
    Warrior_BloodHeal,   // 战士：战后按已损失比例回血
    Ranger_LastCardDraw  // 游侠：最后一张牌抽牌
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

    [Header("卡组配置")]
    public List<CardData> initialDeck = new List<CardData>();       // 初始牌库（自带的牌）
    public List<CardData> exclusiveCardPool = new List<CardData>(); // 专属战利品卡池（打赢掉落的牌）
}