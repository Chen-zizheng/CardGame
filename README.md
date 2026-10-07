
# CardGame

一个基于 Unity 和 C# 开发的 **2D 卡牌游戏**，重点探索了自定义着色器在 UI 动效与卡牌表现中的应用。

## 🎮 核心玩法
- **回合制对战**：玩家与 AI 进行策略博弈，通过手牌管理击败对手。
- **卡牌交互**：实现了卡牌拖拽、悬停放大、选中高亮等流畅的 UI 反馈。
- **战斗逻辑**：包含完整的抽牌、出牌、结算与胜负判定系统。

## ✨ 技术亮点
- **自定义 Shader 实现**：使用 ShaderLab / HLSL 编写了卡牌翻转、UI 流光及受击闪烁等特效，摆脱了对纯代码动画的依赖。
- **高性能 UI 渲染**：通过 Shader 处理视觉反馈，减少 Canvas 重建开销，提升移动端适配潜力。
- **模块化架构**：游戏逻辑（C#）与视觉表现（Shader）分离，便于后续扩展新卡牌与特效。

## 🛠️ 技术栈
- Unity (C#)
- ShaderLab / HLSL
- Unity UI (UGUI)

## 🚀 如何运行
1. 使用 Unity Hub 打开项目根目录。
2. 建议使用 **Unity 2021 LTS 或更高版本**。
3. 打开 `Assets/Scenes` 下的 `MainScene` 即可运行。

## 📂 项目结构
Assets/
├── Scripts/ # 游戏逻辑与状态管理
├── Shaders/ # 自定义着色器源码
├── Prefabs/ # 卡牌与UI预制体
└── Scenes/ # 场景文件
文本

编辑




## 📸 演示截图
<img width="559" height="361" alt="image" src="https://github.com/user-attachments/assets/7a95514b-ea77-420d-b3fd-55bd272aa389" />
<img width="549" height="353" alt="image" src="https://github.com/user-attachments/assets/8349e335-3a91-481a-bf63-d2e750cf8b2c" />
<img width="555" height="346" alt="image" src="https://github.com/user-attachments/assets/147b10db-bbf7-4044-bb10-8cb46b806cec" />
<img width="557" height="353" alt="image" src="https://github.com/user-attachments/assets/d0484d11-a894-4cf7-894a-5cb8ee4a0fc1" />
<img width="554" height="347" alt="image" src="https://github.com/user-attachments/assets/67144845-ee48-4380-bcdd-cb9cf0ca7735" />





*本项目为个人技术练习作品，旨在验证 Shader 在卡牌游戏 UI 中的表现力。*
