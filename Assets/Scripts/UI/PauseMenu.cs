using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 全局暂停 / 设置。
//
// 为什么现在必须有它：战斗已经改成"实时时间轴"，格子自己往前走。没有暂停意味着
// 玩家接个电话、倒杯水，回来就已经被打死了——这不是难度，这是不讲理。
//
// 版式不在这个文件里手写坐标：坐标在 PauseMenuLayout（纯数据）里，
// 这样同一份数字能同时被"建面板 / 几何校验 / 预览图渲染"三处使用。
// 上一版就是手写坐标 + 另一份 PIL 预览，预览好看、实机全错位。
public class PauseMenu : MonoBehaviour
{
    private const string SfxKey = "opt_sfx";
    private const string BgmKey = "opt_bgm";

    public static PauseMenu Instance { get; private set; }

    private GameObject overlay;
    private GameObject mainView;
    private GameObject settingsView;
    private bool isOpen;
    private float savedTimeScale = 1f;
    private string panelTitle = "暂停";
    private string panelKicker = "PAUSED";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenu>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ApplySavedVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 换场景时把暂停收掉，否则新场景一进来就是冻结的
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 必须还原 timeScale：以前这里是 Close(false)，万一有别的路径在暂停状态下换了场景，
        // 新场景就会带着 timeScale=0 进来，画面完完整整但什么都不动。
        if (isOpen) Close(true);

        // 面板是挂在旧的 Canvas 下面的，场景一换就跟着销毁了，必须忘掉它重新建
        overlay = null;
        mainView = null;
        settingsView = null;
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        // ESC 是全局唯一的"回到菜单"入口：地图角落里那颗"主菜单"按钮已经删掉了，
        // 两条路都能回主菜单只会让人不知道该按哪个。
        // 主菜单和失败屏自己就有设置/退出，再叠一层暂停面板只是把同一件事做第二遍。
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (isOpen) { Close(true); return; }
        if (!EscMakesSenseHere(SceneManager.GetActiveScene().name)) return;
        Open();
#endif
    }

    private static bool EscMakesSenseHere(string sceneName)
    {
        return sceneName != "MainMenu" && sceneName != "FailScene";
    }

    // ---------------- 对外 ----------------

    public static void Open()
    {
        if (Instance == null) Boot();
        if (Instance != null) Instance.DoOpen(false);
    }

    public static void OpenSettings()
    {
        if (Instance == null) Boot();
        if (Instance != null) Instance.DoOpen(true);
    }

    // ---------------- 内部 ----------------

    private void DoOpen(bool straightToSettings)
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 只有战斗里时间轴是真的在走，那才叫"暂停"；其余场景写"菜单"
        bool inBattle = SceneManager.GetActiveScene().name == "BattleScene";
        panelTitle = inBattle ? "暂停" : "菜单";
        panelKicker = inBattle ? "PAUSED" : "MENU";

        if (overlay == null) Build(canvas);

        // 顿帧（HitFeedback）也会把 timeScale 压到 0，那时候不能把 0 记成"原本的速度"
        savedTimeScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
        isOpen = true;

        ShowMain(!straightToSettings);
    }

    private void Close(bool restoreTime)
    {
        if (overlay != null) overlay.SetActive(false);
        isOpen = false;
        if (restoreTime) Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
    }

    private void ShowMain(bool main)
    {
        if (mainView != null) mainView.SetActive(main);
        if (settingsView != null) settingsView.SetActive(!main);
    }

    private void Build(Canvas canvas)
    {
        overlay = new GameObject("PauseOverlay", typeof(RectTransform));
        overlay.transform.SetParent(canvas.transform, false);
        UiSpecApply.Stretch(overlay);

        // 压暗：暂停时把整个画面往后推一层
        Image dim = UiSkin.CreatePanel(overlay.transform, "Dim", new Color(0.03f, 0.026f, 0.045f, 0.72f));
        dim.raycastTarget = true;

        GameObject card = new GameObject("PauseCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        UiSkin.SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(PauseMenuLayout.CardW, PauseMenuLayout.CardH));
        UiSkin.Frame(card.GetComponent<Image>(), new Color(0.07f, 0.062f, 0.095f, 0.96f),
            new Color(0.72f, 0.90f, 0.86f, 0.35f), 2, 26);

        mainView = new GameObject("MainView", typeof(RectTransform));
        mainView.transform.SetParent(card.transform, false);
        UiSpecApply.Stretch(mainView);

        settingsView = new GameObject("SettingsView", typeof(RectTransform));
        settingsView.transform.SetParent(card.transform, false);
        UiSpecApply.Stretch(settingsView);

        BuildMain();
        BuildSettings();
    }

    private void BuildMain()
    {
        Dictionary<string, GameObject> made = UiSpecApply.Build(
            PauseMenuLayout.MainPanel(panelTitle, panelKicker), mainView.transform);

        Wire(made, "Resume", () => Close(true));
        Wire(made, "Settings", () => ShowMain(false));
        Wire(made, "Return", ReturnToMainMenu);
    }

    private void BuildSettings()
    {
        float sfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        float bgm = PlayerPrefs.GetFloat(BgmKey, 0.32f);
        Dictionary<string, GameObject> made = UiSpecApply.Build(PauseMenuLayout.SettingsPanel(sfx, bgm), settingsView.transform);

        Wire(made, "Back", () => ShowMain(true));

        HookSlider(made, "Sfx", SfxKey, sfx);
        HookSlider(made, "Bgm", BgmKey, bgm);
    }

    private void HookSlider(Dictionary<string, GameObject> made, string key, string prefKey, float value)
    {
        GameObject sliderObj, labelObj;
        if (!made.TryGetValue(key + "Slider", out sliderObj)) return;
        made.TryGetValue(key + "Label", out labelObj);

        Slider slider = sliderObj.GetComponent<Slider>();
        TextMeshProUGUI label = labelObj != null ? labelObj.GetComponent<TextMeshProUGUI>() : null;
        string caption = key == "Sfx" ? "音效" : "音乐";

        System.Action<float> refresh = v =>
        {
            if (label != null) label.text = caption + "   " + Mathf.RoundToInt(v * 100f) + "%";
        };

        slider.value = value;
        refresh(value);

        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(prefKey, v);
            PlayerPrefs.Save();
            refresh(v);
            ApplySavedVolumes();
        });
    }

    private static void Wire(Dictionary<string, GameObject> made, string name, UnityEngine.Events.UnityAction action)
    {
        GameObject go;
        if (!made.TryGetValue(name, out go) || go == null) return;
        Button btn = go.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    // 战斗开始时 BattleAudio 会调一次，把上次存的音量套上；平时改滑块也会调
    public static void ApplySavedVolumes()
    {
        float sfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        float bgm = PlayerPrefs.GetFloat(BgmKey, 0.32f);

        // 还没进过战斗时不要为了设音量凭空造一个音频对象
        if (BattleAudio.Instance == null) return;
        BattleAudio.Instance.SetVolumes(sfx, bgm);
    }

    // 回主菜单但不动存档：主菜单会出现「继续游戏」，能原样接回这一格。
    // 想真的重开一局，去主菜单点「开始新的一局」——那里才删档。
    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        isOpen = false;
        if (overlay != null) overlay.SetActive(false);

        SceneManager.LoadScene("MainMenu");
    }
}
