# CardGame

一个基于 **团结引擎（Tuanjie 1.8.5 / 内核 Unity 2022.3）** 与 C# 开发的 **2D 卡牌 Roguelike**，
重点探索了自定义着色器在 UI 动效与卡牌表现中的应用。

## ▶ 下载试玩（Windows x64 · 免安装）

**[⬇ 下载 CardGame-Windows-x64.zip（约 90 MB）](https://github.com/Chen-zizheng/CardGame/releases/download/v1.0.0/CardGame-Windows-x64.zip)**

解压后双击 `CardGame.exe` 即可运行，无需安装 Unity 或团结引擎。
若想直接看构建记录，见 [Releases](https://github.com/Chen-zizheng/CardGame/releases/tag/v1.0.0)。

## 📸 演示截图

<img width="559" height="361" alt="主菜单" src="https://github.com/user-attachments/assets/7a95514b-ea77-420d-b3fd-55bd272aa389" />
<img width="549" height="353" alt="角色选择" src="https://github.com/user-attachments/assets/8349e335-3a91-481a-bf63-d2e750cf8b2c" />
<img width="555" height="346" alt="战斗" src="https://github.com/user-attachments/assets/147b10db-bbf7-4044-bb10-8cb46b806cec" />
<img width="557" height="353" alt="奖励结算" src="https://github.com/user-attachments/assets/d0484d11-a894-4cf7-894a-5cb8ee4a0fc1" />
<img width="554" height="347" alt="地图" src="https://github.com/user-attachments/assets/67144845-ee48-4380-bcdd-cb9cf0ca7735" />

## 🎮 核心玩法

- **回合制对战**：玩家进行策略博弈，通过手牌管理击败对手。
- **卡牌交互**：实现了卡牌拖拽、悬停放大、选中高亮等流畅的 UI 反馈。
- **战斗逻辑**：包含完整的抽牌、出牌、结算与胜负判定系统。
- **关卡循环**：主菜单 → 选角色 → 地图 → 战斗 → 奖励 → 篝火/商店 → 失败结算。

## ✨ 技术亮点

- **自定义 Shader 实现**：使用 ShaderLab / HLSL 编写了卡牌翻转、UI 流光及受击闪烁等特效，摆脱了对纯代码动画的依赖。
- **高性能 UI 渲染**：通过 Shader 处理视觉反馈，减少 Canvas 重建开销。
- **模块化架构**：游戏逻辑（C#）与视觉表现（Shader）分离，便于扩展新卡牌与特效。

## 🗺️ 场景一览

`Assets/Scenes/` 下共 **8 个场景**：

| 场景文件 | 作用 |
|---|---|
| `MainMenu.scene` | **游戏入口**——主菜单 |
| `CharacterSelectScene.scene` | 角色选择 |
| `MapScene.scene` | 关卡地图与节点推进 |
| `BattleScene.scene` | 战斗主场景（抽牌 / 出牌 / 结算） |
| `RewardScene.scene` | 战斗胜利后的奖励三选一 |
| `CampfireScene.scene` | 篝火休整（回血 / 移除卡牌） |
| `ShopScene.scene` | 商店购买 |
| `FailScene.scene` | 失败结算 |

> 注意：文件名是 `.scene` 而不是 `.unity`，这是团结引擎的序列化扩展名，**原版 Unity 无法识别**（详见下方「运行环境」）。

## ⚠️ 运行环境（重要）

| 项 | 值 |
|---|---|
| 引擎 | **团结引擎（Tuanjie）1.8.5** |
| 内核版本 | `2022.3.62t7` |
| 渲染管线 | 内置管线（Built-in） |
| 必需模块 | Windows Standalone |

```text
ProjectSettings/ProjectVersion.txt
  m_EditorVersion: 2022.3.62t7
  m_TuanjieEditorVersion: 1.8.5
```

**为什么必须用团结引擎：** 场景文件的 YAML 标签是 `!u! tag=yousandi.cn,2023:`，
这是团结引擎的私有标记。**原版 Unity 打开会报场景损坏 / 全部引用丢失。**
团结引擎可从 [unity.cn](https://unity.cn/tuanjie) 免费下载，选 `2022.3.62t7` 版本安装即可。

## 🚀 如何运行

1. 安装 **团结引擎 2022.3.62t7**（含 Windows Standalone 模块）。
2. 用团结引擎 Hub「添加项目」指向本仓库根目录（含 `Assets/`、`Packages/`、`ProjectSettings/` 的那一层）。
3. 首次导入需要等待贴图与字体资源处理完成。
4. 打开 `Assets/Scenes/MainMenu.scene`。
5. 点击 Play。

## 📦 如何构建

`File → Build Settings → Windows, Mac, Linux → Windows`，Target 选 `Windows x86_64`，点 Build。

> 本仓库**未内置 WebGL 模块**。若想导出浏览器可玩版本，需先在团结引擎 Hub 里补装
> `WebGL Build Support`，再把 Build Target 切到 WebGL。

## 📂 项目结构

```text
Assets/
├── Scenes/          # 8 个场景（入口：MainMenu.scene）
├── Scripts/         # 游戏逻辑与状态管理（C#）
├── Shaders/         # 自定义 ShaderLab / HLSL 源码
├── Prefabs/         # 卡牌、UI、角色预制体
├── Sprites/         # 卡牌 / 角色 / 敌人美术资源
├── Audio/           # 音效与 BGM
├── CharacterDatas/  # 角色数值配置
├── MapData/         # 地图节点配置
├── Resources/       # 运行时加载资源
├── Plugins/         # 第三方插件（DOTween）
└── TextMesh Pro/    # TMP 字体与材质
```

代码规模：C# 约 203 KB、ShaderLab 约 80 KB、HLSL 约 14 KB（GitHub Languages 统计）。

## 🧭 已知局限

- **仓库体积偏大**：卡牌贴图为未压缩 PNG（单张 3–6 MB），`TextMesh Pro/Fonts/SIMHEI.TTF` 占 9.7 MB。
  后续应改为图集压缩 + TMP SDF 子集化。
- **DOTween 以 DLL 形式入库**（`Assets/Plugins/`），而非 UPM 依赖；若从零配置，需自行补齐该插件。
- **BGM 为第三方版权曲目**，仅作本地学习与演示使用，**不随任何构建分发出售**。
- **尚无自动化测试**：`Packages/manifest.json` 已含 `com.unity.test-framework 1.1.33`，
  但 EditMode / PlayMode 用例还没写——这是下一步计划。

---

*本项目为个人技术练习作品，旨在验证 Shader 在卡牌游戏 UI 中的表现力。*
