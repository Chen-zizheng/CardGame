// 暂停面板的版式：纯数据，不引用 UnityEngine。
//
// 这份文件被三方共用：
//   1) PauseMenu.cs         —— 照着它建 GameObject
//   2) .tmp/layout 下的校验器 —— 照它算几何，查出界/重叠/点击区/文字溢出
//   3) .tmp/layout/render.py —— 照它画预览图
// 所以预览图里的坐标就是游戏里的坐标，不会再出现"预览好看、实机错位"。
public static class PauseMenuLayout
{
    public const float CardW = 760f;
    public const float CardH = 620f;

    // 标题随场景变：战斗里按 ESC 是"暂停"（时间轴真的停了），
    // 地图/商店/选人里按 ESC 只是打开菜单，写"暂停"会让玩家以为画面卡住了。
    public static UiSpec MainPanel(string title = "暂停", string kicker = "PAUSED")
    {
        UiSpec s = new UiSpec { Name = "pause-main", Width = CardW, Height = CardH };

        s.Add(new UiEl
        {
            Name = "Kicker", Kind = UiKind.Text, Text = kicker, ColorKey = "dim", Alpha = 0.7f,
            AnchorX = 0.5f, AnchorY = 1f, PivotX = 0.5f, PivotY = 0.5f,
            PosX = 0f, PosY = -76f, Width = 600f, Height = 26f,
            FontSize = 14f, Spacing = 14f
        });

        s.Add(new UiEl
        {
            Name = "Title", Kind = UiKind.Text, Text = title, ColorKey = "ink",
            AnchorX = 0.5f, AnchorY = 1f,
            PosX = 0f, PosY = -140f, Width = 600f, Height = 80f, FontSize = 48f
        });

        s.Add(new UiEl
        {
            Name = "Resume", Kind = UiKind.Button, Text = "继续游戏", ColorKey = "paper",
            PosX = 0f, PosY = 40f, Width = 340f, Height = 68f, FontSize = 24f
        });

        s.Add(new UiEl
        {
            Name = "Settings", Kind = UiKind.Button, Text = "设置", ColorKey = "paper",
            PosX = 0f, PosY = -44f, Width = 340f, Height = 68f, FontSize = 24f
        });

        // 这颗按钮以前叫"放弃本局"，但它并不删档——回到主菜单以后「继续游戏」还能接上。
        // 名字和行为对不上，所以改成"返回主菜单"，真正放弃本局在主菜单点"开始新的一局"（那里会删档）。
        s.Add(new UiEl
        {
            Name = "Return", Kind = UiKind.Button, Text = "返回主菜单", ColorKey = "ghost", Ghost = true,
            PosX = 0f, PosY = -128f, Width = 340f, Height = 68f, FontSize = 24f
        });

        s.Add(new UiEl
        {
            Name = "SaveNote", Kind = UiKind.Text, Text = "已存档 · 可从「继续游戏」接上",
            ColorKey = "dim", Alpha = 0.55f,
            AnchorX = 0.5f, AnchorY = 0.5f,
            PosX = 0f, PosY = -186f, Width = 660f, Height = 26f, FontSize = 15f
        });

        s.Add(new UiEl
        {
            Name = "Hint", Kind = UiKind.Text, Text = "ESC 继续", ColorKey = "dim", Alpha = 0.6f,
            AnchorX = 0.5f, AnchorY = 0f,
            PosX = 0f, PosY = 46f, Width = 600f, Height = 26f, FontSize = 16f
        });

        return s;
    }

    public static UiSpec SettingsPanel(float sfx, float bgm)
    {
        UiSpec s = new UiSpec { Name = "pause-settings", Width = CardW, Height = CardH };

        s.Add(new UiEl
        {
            Name = "Title", Kind = UiKind.Text, Text = "设置", ColorKey = "ink",
            AnchorX = 0.5f, AnchorY = 1f,
            PosX = 0f, PosY = -140f, Width = 600f, Height = 80f, FontSize = 44f
        });

        AddSlider(s, "Sfx", "音效", 110f, 68f, sfx);
        AddSlider(s, "Bgm", "音乐", 10f, -32f, bgm);

        s.Add(new UiEl
        {
            Name = "Back", Kind = UiKind.Button, Text = "返回", ColorKey = "paper",
            PosX = 0f, PosY = -150f, Width = 340f, Height = 68f, FontSize = 24f
        });

        return s;
    }

    private static void AddSlider(UiSpec s, string key, string label, float labelY, float sliderY, float value)
    {
        s.Add(new UiEl
        {
            Name = key + "Label", Kind = UiKind.Text,
            Text = label + "   " + (int)System.Math.Round(value * 100f) + "%",
            ColorKey = "ink", LeftAligned = true,
            AnchorX = 0.5f, AnchorY = 0.5f, PivotX = 0f, PivotY = 0.5f,
            PosX = -280f, PosY = labelY, Width = 560f, Height = 30f, FontSize = 20f
        });

        s.Add(new UiEl
        {
            Name = key + "Slider", Kind = UiKind.Slider, ColorKey = "mint",
            PosX = 0f, PosY = sliderY, Width = 560f, Height = 14f, Value = value
        });
    }
}
