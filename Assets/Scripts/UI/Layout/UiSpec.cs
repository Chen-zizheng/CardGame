using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// 版式规格：把"一个面板上有什么、在哪、多大"写成纯数据。
//
// 为什么要这么做：以前版式是命令式的 C# 代码（new GameObject + 设 anchorMin…），
// 只有 Unity 能执行它。我这边起不来编辑器，于是用 PIL 另写一份"预览图"——
// 两份实现，预览永远正确，代码永远可能有错，而且错了没人拦得住。
// 现在的链条是：UiSpec（唯一真源）→ 应用层建 GameObject / 校验器算几何 / 预览图渲染，
// 三方读的是同一份数字，预览再也不会"说谎"。
public enum UiKind { Box, Text, Button, Slider }

public struct V2
{
    public float X, Y;
    public V2(float x, float y) { X = x; Y = y; }
    public override string ToString() { return "(" + X.ToString("0.#") + ", " + Y.ToString("0.#") + ")"; }
}

public class UiEl
{
    public string Name = "";
    public string Parent = "";
    public UiKind Kind = UiKind.Box;
    public string Text = "";
    public string ColorKey = "ink";     // ink / dim / mint / paper / dark / slate / glass / clear
    public float Alpha = 1f;

    public float AnchorX = 0.5f, AnchorY = 0.5f;
    public float PivotX = 0.5f, PivotY = 0.5f;
    public float PosX, PosY;
    public float Width = 100f, Height = 100f;
    public bool Stretch;                // 铺满父级（锚点四边拉伸，Pos/Size 不再使用）

    public float FontSize = 20f;
    public float Spacing;               // 字距
    public bool LeftAligned;
    public float Radius = 12f;
    public float BorderPx = 2f;

    public float Value = -1f;           // Slider 当前值，<0 表示不适用
    public bool Ghost;                  // 幽灵按钮（透明底 + 细边）

    public UiEl Copy()
    {
        return (UiEl)MemberwiseClone();
    }
}

// 一个面板（父级尺寸由 Width/Height 给出，坐标系原点在父级中心）
public class UiSpec
{
    public string Name = "panel";
    public float Width = 760f, Height = 620f;
    public readonly List<UiEl> Elements = new List<UiEl>();

    public UiEl Add(UiEl el)
    {
        Elements.Add(el);
        return el;
    }

    public UiEl Find(string name)
    {
        for (int i = 0; i < Elements.Count; i++)
            if (Elements[i].Name == name) return Elements[i];
        return null;
    }

    // 父级在自身坐标系里的矩形（锚点按父级长宽换算，pivot 默认居中）
    public void ParentRect(out float left, out float right, out float bottom, out float top)
    {
        left = -Width * 0.5f;
        right = Width * 0.5f;
        bottom = -Height * 0.5f;
        top = Height * 0.5f;
    }

    // 元素在父级坐标系里的矩形
    public void RectOf(UiEl el, out float left, out float right, out float bottom, out float top)
    {
        if (el.Stretch)
        {
            ParentRect(out left, out right, out bottom, out top);
            return;
        }

        float w, h;
        ContainerSize(el.Parent, out w, out h);

        float anchorX = -w * 0.5f + el.AnchorX * w;
        float anchorY = -h * 0.5f + el.AnchorY * h;

        float pivotX = anchorX + el.PosX;
        float pivotY = anchorY + el.PosY;

        left = pivotX - el.PivotX * el.Width;
        right = left + el.Width;
        bottom = pivotY - el.PivotY * el.Height;
        top = bottom + el.Height;
    }

    // 子容器（命名元素自身当父级时）的尺寸
    public void ContainerSize(string parentName, out float w, out float h)
    {
        if (string.IsNullOrEmpty(parentName))
        {
            w = Width;
            h = Height;
            return;
        }

        UiEl p = Find(parentName);
        if (p == null)
        {
            w = Width;
            h = Height;
            return;
        }

        w = p.Width;
        h = p.Height;
    }

    // 粗略字宽：中日韩按 1em，ASCII 按 .55em
    public static float EstimateTextWidth(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float sum = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n' || c == '\r') { continue; }
            if (c >= 0x2E80) sum += 1f;
            else if (c == ' ') sum += 0.32f;
            else if (c >= '0' && c <= '9') sum += 0.55f;
            else sum += 0.56f;
        }
        return sum * fontSize;
    }

    // ---------------- 校验 ----------------
    // 只查四件我这边确实会错、而且靠眼睛在 PNG 里容易漏掉的事：
    // 出界 / 互相压住 / 点击区太小 / 文字比框宽。
    public List<string> Verify()
    {
        List<string> problems = new List<string>();

        ParentRect(out float pl, out float pr, out float pb, out float pt);

        for (int i = 0; i < Elements.Count; i++)
        {
            UiEl e = Elements[i];
            RectOf(e, out float l, out float r, out float b, out float t);

            // 只跟"同一个父级"的比较，跨容器不算
            if (string.IsNullOrEmpty(e.Parent))
            {
                const float tol = 1f;
                if (l < pl - tol || r > pr + tol || b < pb - tol || t > pt + tol)
                {
                    problems.Add(string.Format(
                        "[出界] {0} 超出父级：x {1:0.#}~{2:0.#}（父级 {3:0.#}~{4:0.#}），y {5:0.#}~{6:0.#}（父级 {7:0.#}~{8:0.#}）",
                        e.Name, l, r, pl, pr, b, t, pb, pt));
                }
            }

            if (e.Kind == UiKind.Button && (e.Width < 44f || e.Height < 44f))
                problems.Add(string.Format("[点击区] {0} 只有 {1:0}×{2:0}，手指点得到，鼠标也难", e.Name, e.Width, e.Height));

            if (e.Kind == UiKind.Slider && e.Width < 120f)
                problems.Add(string.Format("[点击区] 滑块 {0} 宽 {1:0}，太短了拖不准", e.Name, e.Width));

            if (e.Kind == UiKind.Text)
            {
                float need = EstimateTextWidth(e.Text, e.FontSize);
                float room = e.Width - 6f;
                if (need > room)
                    problems.Add(string.Format("[文字] {0} 估计需要 {1:0}px，框只有 {2:0}px：[{3}]", e.Name, need, room, e.Text));
                if (e.Height < e.FontSize * 1.15f)
                    problems.Add(string.Format("[文字] {0} 框高 {1:0} < 字号 {2:0}×1.15，会裁字", e.Name, e.Height, e.FontSize));
            }
        }

        // 同父级两两重叠（允许相切 1px）
        for (int i = 0; i < Elements.Count; i++)
        {
            for (int j = i + 1; j < Elements.Count; j++)
            {
                UiEl a = Elements[i], b2 = Elements[j];
                if (a.Parent != b2.Parent) continue;

                RectOf(a, out float al, out float ar, out float ab, out float at);
                RectOf(b2, out float bl, out float br, out float bb, out float bt);

                float ox = Math.Min(ar, br) - Math.Max(al, bl);
                float oy = Math.Min(at, bt) - Math.Max(ab, bb);
                if (ox > 1f && oy > 1f)
                {
                    problems.Add(string.Format(
                        "[重叠] {0} 与 {1} 压住 {2:0.#}×{3:0.#}px（同父级 {4}）",
                        a.Name, b2.Name, ox, oy, string.IsNullOrEmpty(a.Parent) ? "panel" : a.Parent));
                }
            }
        }

        return problems;
    }

    // ---------------- 导出（给预览图用） ----------------
    public string ToJson()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"name\":\"").Append(Esc(Name)).Append("\",");
        sb.Append("\"width\":").Append(F(Width)).Append(",\"height\":").Append(F(Height)).Append(",");
        sb.Append("\"elements\":[");
        for (int i = 0; i < Elements.Count; i++)
        {
            UiEl e = Elements[i];
            RectOf(e, out float l, out float r, out float b, out float t);
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append("\"name\":\"").Append(Esc(e.Name)).Append("\",");
            sb.Append("\"parent\":\"").Append(Esc(e.Parent)).Append("\",");
            sb.Append("\"kind\":\"").Append(e.Kind.ToString().ToLowerInvariant()).Append("\",");
            sb.Append("\"text\":\"").Append(Esc(e.Text)).Append("\",");
            sb.Append("\"color\":\"").Append(Esc(e.ColorKey)).Append("\",");
            sb.Append("\"alpha\":").Append(F(e.Alpha)).Append(",");
            sb.Append("\"rect\":[").Append(F(l)).Append(",").Append(F(b)).Append(",").Append(F(r)).Append(",").Append(F(t)).Append("],");
            sb.Append("\"size\":[").Append(F(e.Width)).Append(",").Append(F(e.Height)).Append("],");
            sb.Append("\"font\":").Append(F(e.FontSize)).Append(",");
            sb.Append("\"spacing\":").Append(F(e.Spacing)).Append(",");
            sb.Append("\"left\":").Append(e.LeftAligned ? "true" : "false").Append(",");
            sb.Append("\"radius\":").Append(F(e.Radius)).Append(",");
            sb.Append("\"border\":").Append(F(e.BorderPx)).Append(",");
            sb.Append("\"ghost\":").Append(e.Ghost ? "true" : "false").Append(",");
            sb.Append("\"value\":").Append(F(e.Value));
            sb.Append("}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
