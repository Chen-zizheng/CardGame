using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 立绘的「活着」动画。三件事叠加：
/// 1) 贴图翻页——Resources/Art/EnemyAnim/{key}_sheet 是烘焙好的循环帧表。
///    敌人：生根的弯 + 沿身体向上传播的行波 + 局部呼吸（软体），12 帧 / 7fps。
///    玩家：绕脚下的呼吸 + 胸廓上提 + 头部滞后（刚体，不弯），24 帧 / 12fps。
///    慢速正弦在 6fps 下会被采样成一格一格的跳，所以玩家用了 24 帧 / 12fps。
/// 2) 果冻——很小幅度的等比呼吸 + 摆头，只补一点重量感，振幅大了会盖过翻页。
/// 3) 叠加层——受击挤压 / 抖动 / 出牌前倾，都在这里按帧叠加，不夺 transform 的所有权。
/// 贴图缺失时只跑第 2、3 项，绝不把已经显示好的立绘弄没。
/// </summary>
public class ActorAnimator : MonoBehaviour
{
    private Image image;
    private RectTransform rt;
    private Sprite[] frames;
    private int index = -1;
    private float clock;
    private float fps = 7f;
    private float phase;
    private Vector2 basePos;
    private float amp;
    private float sway;
    private bool groundAnchor;

    private float punchAmp, punchDur, punchT = 99f;
    private float shakeAmp, shakeDur, shakeT = 99f;
    private float leanAmp, leanDur, leanT = 99f;   // 出牌前倾（角度，度）

    public static void Attach(Image img, string key, bool enemy, int cell, int cols, int rows, float fps)
    {
        if (img == null) return;
        ActorAnimator a = img.gameObject.AddComponent<ActorAnimator>();
        a.Init(img, key, enemy, cell, cols, rows, fps);
    }

    private void Init(Image img, string key, bool enemy, int cell, int cols, int rows, float fpsIn)
    {
        fps = fpsIn > 0f ? fpsIn : 7f;
        image = img;
        rt = img.rectTransform;
        basePos = rt.anchoredPosition;
        // 主形变已经在烘焙帧里，这里只补很小的重量感。
        amp = enemy ? 0.008f : 0.004f;
        sway = enemy ? 0.9f : 0.25f;
        groundAnchor = true;
        phase = Random.Range(0f, 12f);
        frames = LoadFrames(key, cell, cols, rows);
        if (frames != null) { index = 0; img.sprite = frames[0]; }
    }

    /// <summary>受击挤压。</summary>
    public void Punch(float amount, float seconds) { punchAmp = amount; punchDur = Mathf.Max(0.01f, seconds); punchT = 0f; }

    /// <summary>受击抖动（像素）。</summary>
    public void Shake(float pixels, float seconds) { shakeAmp = pixels; shakeDur = Mathf.Max(0.01f, seconds); shakeT = 0f; }

    /// <summary>出牌时的一次前倾/后仰（角度，正数往前压）。</summary>
    public void Lean(float degrees, float seconds) { leanAmp = degrees; leanDur = Mathf.Max(0.01f, seconds); leanT = 0f; }

    private static Sprite[] LoadFrames(string key, int cell, int cols, int rows)
    {
        if (string.IsNullOrEmpty(key) || cell <= 0 || cols <= 0 || rows <= 0) return null;
        Texture2D sheet = Resources.Load<Texture2D>("Art/EnemyAnim/" + key + "_sheet");
        if (sheet == null) return null;
        if (sheet.width < cell * cols || sheet.height < cell * rows) return null;

        List<Sprite> list = new List<Sprite>(cols * rows);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // Sprite 的 rect 以左下角为原点，而 sheet 是从上往下排的
                Rect rect = new Rect(c * cell, sheet.height - (r + 1) * cell, cell, cell);
                Sprite s = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                if (s != null) list.Add(s);
            }
        }
        return list.Count > 1 ? list.ToArray() : null;
    }

    private void Update()
    {
        if (image == null || rt == null) return;
        float dt = Time.deltaTime;

        // ---- 1. 翻页 ----
        if (frames != null)
        {
            clock += dt * fps;
            int next = (int)clock % frames.Length;
            if (next != index)
            {
                index = next;
                image.sprite = frames[index];
            }
        }

        // ---- 2. 果冻 ----
        float t = Time.time + phase;
        float w = Mathf.PI * 2f / 1.55f;
        float s = Mathf.Sin(w * t) + 0.45f * Mathf.Sin(w * 0.47f * t + 1.1f);
        float sx = 1f + amp * s;
        float sy = 1f - amp * 0.85f * s;

        // ---- 3. 叠加层 ----
        if (punchT < punchDur)
        {
            punchT += dt;
            float k = Mathf.Clamp01(1f - punchT / punchDur);
            float e = Mathf.Sin(punchT * Mathf.PI * 7f) * k * k;
            sx *= 1f + punchAmp * e;
            sy *= 1f + punchAmp * e;
        }
        float extraRot = 0f;
        if (leanT < leanDur)
        {
            leanT += dt;
            float u = Mathf.Clamp01(leanT / leanDur);
            float k = 1f - u;
            extraRot += leanAmp * Mathf.Sin(u * Mathf.PI) * k;
        }
        rt.localScale = new Vector3(sx, sy, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, sway * Mathf.Sin(w * 0.71f * t + 0.7f) + extraRot);

        // 缩放是绕中心做的，所以竖着压扁时下沿会往下跑、拉高时下沿会往上跑。
        // 站在地上的角色要补回这一半位移，脚才踩得住地面（不然像在原地飘）。
        float h = rt.rect.height;
        float keepFeet = groundAnchor ? h * (sy - 1f) * 0.5f : 0f;
        float shx = 0f, shy = 0f;
        if (shakeT < shakeDur)
        {
            shakeT += dt;
            float k = Mathf.Clamp01(1f - shakeT / shakeDur);
            shx = shakeAmp * k * k * Mathf.Sin(shakeT * 92f);
            shy = shakeAmp * 0.45f * k * k * Mathf.Sin(shakeT * 71f + 1.2f);
        }
        rt.anchoredPosition = basePos + new Vector2(shx, keepFeet + shy);
    }
}
