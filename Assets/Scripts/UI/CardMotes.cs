using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选人卡上的薄荷浮尘：几个小亮点在卡面上慢慢往上飘、明暗呼吸。
/// 名字"太暗、没生气"的解法不是把字调白就完事 —— 得让那块地方有点动静。
/// 全部运行时生成（编辑器起不来，不能改场景），数量少、开销可忽略。
/// </summary>
public class CardMotes : MonoBehaviour
{
    private const int Count = 12;

    private RectTransform _container;
    private RectTransform[] _motes;
    private Image[] _imgs;
    private float[] _speed;
    private float[] _phase;
    private float[] _size;
    private float[] _baseAlpha;
    private float _w, _h;
    private bool _built;

    /// <summary>柔光圆点：整个卡面只生成一次的 64×64 径向衰减贴图，浮尘和名字辉光共用。</summary>
    private static Sprite _soft;

    public static Sprite SoftSprite()
    {
        if (_soft != null) return _soft;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        _soft = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _soft;
    }

    public static void Attach(RectTransform card, float width, float height)
    {
        if (card == null) return;
        CardMotes m = card.GetComponent<CardMotes>();
        if (m == null) m = card.gameObject.AddComponent<CardMotes>();
        m.Build(width, height);
    }

    private void Build(float width, float height)
    {
        _w = width;
        _h = height;

        if (_container == null)
        {
            GameObject go = new GameObject("Motes", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _container = go.GetComponent<RectTransform>();
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;
            _container.pivot = new Vector2(0.5f, 0.5f);
            // 不挡点击
            RectTransform self = transform as RectTransform;
            if (self != null) { }
        }
        _container.SetAsLastSibling();

        _motes = new RectTransform[Count];
        _imgs = new Image[Count];
        _speed = new float[Count];
        _phase = new float[Count];
        _size = new float[Count];
        _baseAlpha = new float[Count];

        Sprite dot = SoftSprite();
        for (int i = 0; i < Count; i++)
        {
            GameObject go = new GameObject("Mote" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_container, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float s = Random.Range(5f, 14f);
            rt.sizeDelta = new Vector2(s, s);
            _motes[i] = rt;
            _size[i] = s;

            Image img = go.GetComponent<Image>();
            img.sprite = dot;
            img.raycastTarget = false;
            img.color = new Color(0.66f, 0.94f, 0.88f, 0f);
            _imgs[i] = img;

            _speed[i] = Random.Range(7f, 20f);
            _phase[i] = Random.Range(0f, 1f);
            _baseAlpha[i] = Random.Range(0.18f, 0.55f);

            rt.anchoredPosition = StartPos(i);
        }
        _built = true;
    }

    private Vector2 StartPos(int i)
    {
        float x = Random.Range(-_w * 0.46f, _w * 0.46f);
        float y = Random.Range(-_h * 0.5f, -_h * 0.30f);
        return new Vector2(x, y);
    }

    private void Update()
    {
        if (!_built) return;
        // 世界静止时也要动起来：暂停（Time.timeScale=0）时用 unscaled，选人界面不受影响。
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _motes.Length; i++)
        {
            RectTransform rt = _motes[i];
            if (rt == null) continue;

            Vector2 p = rt.anchoredPosition;
            p.y += _speed[i] * dt;
            p.x += Mathf.Sin((Time.unscaledTime + _phase[i] * 6f) * 1.4f) * 6f * dt;

            // 出顶就回到卡底，重新开始一段
            if (p.y > _h * 0.5f)
            {
                p = StartPos(i);
                _speed[i] = Random.Range(7f, 20f);
                _baseAlpha[i] = Random.Range(0.18f, 0.55f);
            }
            rt.anchoredPosition = p;

            // 生活周期的明暗：底部升起时淡入，靠近中部最亮，出顶前淡出
            float t = Mathf.InverseLerp(-_h * 0.5f, _h * 0.5f, p.y);
            float a = _baseAlpha[i] * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            Image img = _imgs[i];
            if (img != null) img.color = new Color(0.66f, 0.94f, 0.88f, a);
        }
    }
}
