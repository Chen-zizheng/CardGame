using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CardCombat;

/// <summary>
/// 存档里的一个地图节点。MapNode 直接挂在场景对象上、且带 Vector2，
/// 这里单独用一个扁平结构存，JSON 读出来才不会因为字段改名而炸。
/// </summary>
[Serializable]
public class SavedNode
{
    public int id;
    public int layer;
    public int type;      // MapNodeType 的 int 值
    public float x;
    public float y;
    public bool visited;
    public bool accessible;
    public int[] links;
}

/// <summary>
/// 一局游戏的完整快照。只存"能重建成同一个局面"的最小集合：
/// 角色四件套、血量/金币/层数、牌组的卡 id、整张地图的节点状态、地图种子。
/// 卡牌数据本身是代码里的 CardLibrary，不存数值，存 id 就行。
/// </summary>
[Serializable]
public class SaveFile
{
    // v2：地图从 7 层改成 10 层。旧档的节点图对不上新地图，直接作废。
    public int version = 2;
    public string savedAt;

    // 地图
    public int seed;
    public List<SavedNode> nodes = new List<SavedNode>();

    // 角色
    public string characterName;
    public int passive;
    public int maxHp;
    public string passiveName;
    public string passiveDesc;
    public string tagline;

    // 局内资源与进度
    public int hp;
    public int gold;
    public int stageIndex;
    public int enemyLevel;
    public int totalStage;
    public bool[] stageCleared;
    public int runWins;
    public int runMaxStage;
    public bool runWon;

    // 本局统计（结算屏）。老存档没有这些字段，JsonUtility 给 0，读出来就是"还没打过"。
    public int statCardsPlayed;
    public int statDamageDealt;
    public int statDamageBlocked;
    public int statInterrupts;
    public int statKills;

    // 牌组（卡 id 列表）
    public List<string> deck = new List<string>();

    // 遗物（RelicLibrary 的 id 列表）。老存档里没有这个字段，JsonUtility 会给 null，读的时候兜空。
    public List<string> relics = new List<string>();
    public List<string> talents = new List<string>();
    // 这一局已经读过的事件（EventLibrary 的 id）。不存的话同一局会重复撞见同一段话。
    public List<string> eventsSeen = new List<string>();
}

/// <summary>
/// 存档系统：一个 JSON 文件，放在 Application.persistentDataPath 下。
/// 存档时机全部在"战斗外"：进地图时、战斗结算完、拿完奖励回地图前，
/// 所以战斗中途关程序不会留下打到一半的局面，最坏是重打当前节点。
/// </summary>
public static class SaveSystem
{
    public const string FileName = "run_save.json";
    public const string BackupName = "run_save.bak.json";

    private static List<SavedNode> pendingNodes;
    private static int pendingSeed;

    public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }
    public static string BackupPath { get { return Path.Combine(Application.persistentDataPath, BackupName); } }

    public static bool Exists()
    {
        return File.Exists(FilePath) || File.Exists(BackupPath);
    }

    // ---------------- 写 ----------------

    public static bool Save()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentCharacter == null) return false;

        SaveFile f = new SaveFile();
        f.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        f.characterName = gm.currentCharacter.characterName;
        f.passive = (int)gm.currentCharacter.passiveSkill;
        f.maxHp = gm.playerMaxHp;
        f.passiveName = gm.currentCharacter.passiveName;
        f.passiveDesc = gm.currentCharacter.passiveDesc;
        f.tagline = gm.currentCharacter.tagline;

        f.hp = gm.currentPlayerHp;
        f.gold = gm.currentGold;
        f.stageIndex = gm.currentStageIndex;
        f.enemyLevel = gm.currentEnemyLevel;
        f.totalStage = gm.totalStageCount;
        f.stageCleared = gm.stageCleared != null ? (bool[])gm.stageCleared.Clone() : new bool[0];
        f.runWins = gm.runWins;
        f.runMaxStage = gm.runMaxStage;
        f.runWon = gm.runWon;

        f.statCardsPlayed = gm.statCardsPlayed;
        f.statDamageDealt = gm.statDamageDealt;
        f.statDamageBlocked = gm.statDamageBlocked;
        f.statInterrupts = gm.statInterrupts;
        f.statKills = gm.statKills;

        f.relics = gm.relics != null ? new List<string>(gm.relics) : new List<string>();
        f.talents = gm.talents != null ? new List<string>(gm.talents) : new List<string>();
        f.eventsSeen = gm.eventsSeen != null ? new List<string>(gm.eventsSeen) : new List<string>();

        for (int i = 0; i < gm.playerDeck.Count; i++)
        {
            CardData c = gm.playerDeck[i];
            if (c == null) continue;
            string id = string.IsNullOrEmpty(c.id) ? CardLibrary.MapChinese(c.cardName) : c.id;
            if (!string.IsNullOrEmpty(id)) f.deck.Add(id);
        }

        // 地图还没生成好时不写盘，否则一进地图场景就可能用空地图覆盖掉刚读进来的存档
        if (MapManager.Instance == null) return false;
        f.seed = MapManager.Instance.mapSeed;
        f.nodes = MapManager.Instance.ExportNodes();
        if (f.nodes == null || f.nodes.Count == 0)
        {
            Debug.LogWarning("[存档] 地图还没有节点，跳过这次存盘");
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(f, true);
            if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
            File.WriteAllText(FilePath, json);
            Debug.Log("[存档] 已写入 " + FilePath + "（" + f.savedAt + "，牌组 " + f.deck.Count + " 张，节点 " + (f.nodes != null ? f.nodes.Count : 0) + " 个）");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[存档] 写入失败：" + e.Message);
            return false;
        }
    }

    // ---------------- 读 ----------------

    public static SaveFile Read()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                SaveFile f = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
                if (f != null) return f;
            }
            if (File.Exists(BackupPath))
            {
                SaveFile f = JsonUtility.FromJson<SaveFile>(File.ReadAllText(BackupPath));
                if (f != null) return f;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[存档] 读取失败：" + e.Message);
        }
        return null;
    }

    /// <summary>主菜单用：只读出来给"继续"按钮写一行进度，不改任何运行时状态。</summary>
    public static SaveFile Peek()
    {
        SaveFile f = Exists() ? Read() : null;
        // 旧版本的档（7 层时代）不算数，否则主菜单会挂着一个点进去就作废的"继续"。
        return f != null && f.version >= 2 ? f : null;
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            pendingNodes = null;
            Debug.Log("[存档] 已删除");
        }
        catch (Exception e)
        {
            Debug.LogError("[存档] 删除失败：" + e.Message);
        }
    }

    // ---------------- 还原 ----------------

    /// <summary>
    /// 把存档灌回 GameManager。地图节点先放在 pendingNodes 里：
    /// 从主菜单读档时 MapManager 往往还没被创建，等它 Start 时再取。
    /// </summary>
    public static bool Restore()
    {
        SaveFile f = Read();
        if (f == null) return false;

        if (f.version < 2)
        {
            Debug.LogWarning("[存档] 这是 7 层时代的旧档（v" + f.version + "），层数结构已经变了，作废重开");
            Delete();
            return false;
        }

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[存档] 找不到 GameManager，无法读档");
            return false;
        }

        // 角色用运行时实例重建：CharacterData 在 Resources 之外，路径加载拿不到。
        // 战斗里用到的字段就这几个（名字/血量/被动/文案），贴图由 PlayerArt 按被动枚举取。
        CharacterData cd = ScriptableObject.CreateInstance<CharacterData>();
        cd.characterName = f.characterName;
        cd.maxHp = Mathf.Max(1, f.maxHp);
        cd.passiveSkill = (PassiveSkillType)f.passive;
        cd.passiveName = f.passiveName;
        cd.passiveDesc = f.passiveDesc;
        cd.tagline = f.tagline;

        gm.currentCharacter = cd;
        gm.playerMaxHp = Mathf.Max(1, f.maxHp);
        gm.currentPlayerHp = Mathf.Clamp(f.hp, 1, gm.playerMaxHp);
        gm.currentGold = f.gold;
        gm.currentStageIndex = Mathf.Max(1, f.stageIndex);
        gm.currentEnemyLevel = Mathf.Clamp(f.enemyLevel, 1, 3);
        gm.totalStageCount = Mathf.Max(1, f.totalStage);
        gm.runWins = f.runWins;
        gm.runMaxStage = f.runMaxStage;
        gm.runWon = f.runWon;

        gm.statCardsPlayed = f.statCardsPlayed;
        gm.statDamageDealt = f.statDamageDealt;
        gm.statDamageBlocked = f.statDamageBlocked;
        gm.statInterrupts = f.statInterrupts;
        gm.statKills = f.statKills;

        if (f.stageCleared != null && f.stageCleared.Length > 0)
        {
            gm.stageCleared = (bool[])f.stageCleared.Clone();
            gm.totalStageCount = Mathf.Max(gm.totalStageCount, gm.stageCleared.Length);
        }
        else
        {
            gm.stageCleared = new bool[gm.totalStageCount];
        }

        gm.playerDeck.Clear();
        if (f.deck != null)
        {
            for (int i = 0; i < f.deck.Count; i++)
            {
                CardData c = DeckFactory.FromId(f.deck[i]);
                if (c != null) gm.playerDeck.Add(c);
            }
        }

        gm.relics.Clear();
        if (f.relics != null)
        {
            for (int i = 0; i < f.relics.Count; i++)
            {
                if (RelicLibrary.Get(f.relics[i]) != null) gm.relics.Add(f.relics[i]);
            }
        }

        gm.talents.Clear();
        if (f.talents != null)
        {
            for (int i = 0; i < f.talents.Count; i++)
            {
                if (TalentLibrary.Get(f.talents[i]) != null) gm.talents.Add(f.talents[i]);
            }
        }

        // 老存档（本能系统之前）没有这条字段：按角色补回出生自带的那一条
        if (gm.talents.Count == 0)
        {
            TalentDef starter = TalentLibrary.StarterFor(gm.InstinctOwner());
            if (starter != null) gm.talents.Add(starter.Id);
        }

        // 事件记录：只认 EventLibrary 里还存在的 id（表改过也不至于把老档读炸）
        if (gm.eventsSeen == null) gm.eventsSeen = new List<string>();
        gm.eventsSeen.Clear();
        if (f.eventsSeen != null)
        {
            for (int i = 0; i < f.eventsSeen.Count; i++)
            {
                if (EventLibrary.Get(f.eventsSeen[i]) != null) gm.eventsSeen.Add(f.eventsSeen[i]);
            }
        }

        bool dissector = gm.IsDissector;
        gm.allAvailableCards.Clear();
        gm.allAvailableCards.AddRange(DeckFactory.AllCards(dissector));

        pendingNodes = f.nodes;
        pendingSeed = f.seed;

        if (MapManager.Instance != null)
        {
            MapManager.Instance.mapSeed = f.seed;
            MapManager.Instance.RestoreNodes(pendingNodes);
            pendingNodes = null;
        }

        Debug.Log("[存档] 读档完成：" + f.characterName + " HP " + gm.currentPlayerHp + "/" + gm.playerMaxHp
                  + "，第 " + gm.currentStageIndex + " 层，牌组 " + gm.playerDeck.Count + " 张，遗物 " + gm.relics.Count + " 件");
        return true;
    }

    /// <summary>MapManager.Start 里调用：有挂着的存档地图就吃进去，返回 true 表示不要重新生成。</summary>
    public static bool TryApplyPendingMap(MapManager map)
    {
        if (map == null || pendingNodes == null) return false;

        map.mapSeed = pendingSeed;
        map.RestoreNodes(pendingNodes);
        pendingNodes = null;
        Debug.Log("[存档] 地图已从存档还原");
        return true;
    }
}
