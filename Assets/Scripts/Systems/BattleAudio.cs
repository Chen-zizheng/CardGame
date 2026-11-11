using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// 战斗音频总控。
// 接入方式对场景零侵入：BattleSystem.Start() 里调用 BattleAudio.Ensure()，
// 音频对象在运行时自己建、自己 DontDestroyOnLoad，场景里不用摆任何东西。
// 音效文件放在 Assets/Resources/Audio/SFX/*.wav，BGM 在 Assets/Resources/Audio/BGM/*.wav。
public class BattleAudio : MonoBehaviour
{
    public static BattleAudio Instance { get; private set; }

    [Header("音量")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float bgmVolume = 0.32f;

    [Header("变化")]
    [Tooltip("同一个音效每次播放的随机音高偏移，避免连击时听起来像复读机")]
    public float pitchJitter = 0.06f;

    [Header("防叠")]
    [Tooltip("同一个音效在这个时间窗口内只播一次，防止一次结算触发多个伤害时音量爆掉")]
    public float sameClipCooldown = 0.045f;

    private const int SfxSourceCount = 6;

    private AudioSource[] sfxSources;
    private AudioSource bgmSource;
    private int nextSource;
    private string currentBgm = "";

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();

    // 画面里没有音频对象时，用它按需创建
    public static BattleAudio Ensure()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("BattleAudio");
        return go.AddComponent<BattleAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSources = new AudioSource[SfxSourceCount];
        for (int i = 0; i < SfxSourceCount; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;   // 2D
            src.volume = sfxVolume;
            sfxSources[i] = src;
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = bgmVolume;

        // 设置面板里改过的音量存在 PlayerPrefs 里，进战斗时要以那份为准
        PauseMenu.ApplySavedVolumes();
    }

    // ---------------- 播放 ----------------

    public void Play(string clipName, float volumeScale = 1f)
    {
        AudioClip clip = LoadClip("Audio/SFX/" + clipName);
        if (clip == null) return;

        if (lastPlayTime.TryGetValue(clipName, out float last) && Time.unscaledTime - last < sameClipCooldown) return;
        lastPlayTime[clipName] = Time.unscaledTime;

        AudioSource src = sfxSources[nextSource];
        nextSource = (nextSource + 1) % sfxSources.Length;

        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.volume = Mathf.Clamp01(sfxVolume * volumeScale);
        src.clip = clip;
        src.Play();
    }

    public void PlayBgm(string clipName, float fadeTime = 1.2f)
    {
        if (currentBgm == clipName && bgmSource != null && bgmSource.isPlaying) return;
        if (bgmSource == null) return;

        AudioClip clip = LoadClip("Audio/BGM/" + clipName);
        if (clip == null) return;

        currentBgm = clipName;
        bgmSource.DOKill();
        bgmSource.clip = clip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        bgmSource.DOFade(bgmVolume, fadeTime);
    }

    public void StopBgm(float fadeTime = 0.8f)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        currentBgm = "";
        bgmSource.DOKill();
        bgmSource.DOFade(0f, fadeTime).OnComplete(() => bgmSource.Stop());
    }

    // 结算时把 BGM 压下去，让胜负音效听得清
    public void DuckBgm(float toVolume, float time)
    {
        if (bgmSource == null) return;
        bgmSource.DOKill();
        bgmSource.DOFade(Mathf.Clamp01(toVolume), time);
    }

    public void SetVolumes(float sfx, float bgm)
    {
        sfxVolume = Mathf.Clamp01(sfx);
        bgmVolume = Mathf.Clamp01(bgm);

        for (int i = 0; i < sfxSources.Length; i++)
        {
            if (sfxSources[i] != null) sfxSources[i].volume = sfxVolume;
        }

        if (bgmSource != null && currentBgm != "") bgmSource.volume = bgmVolume;
    }

    private AudioClip LoadClip(string resourcePath)
    {
        if (clipCache.TryGetValue(resourcePath, out AudioClip cached)) return cached;

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        clipCache[resourcePath] = clip;

        if (clip == null) Debug.LogWarning("【BattleAudio】找不到音频资源：Resources/" + resourcePath);
        return clip;
    }

    // ---------------- 和战斗事件对接 ----------------

    // BattleSystem 每次进战斗调一次。事件随 BattleSystem 销毁一起回收，不需要手动解绑。
    public void BindBattle(BattleSystem battle)
    {
        if (battle == null) return;

        battle.CardPlayed += OnCardPlayed;
        battle.EnemyDamaged += OnEnemyDamaged;
        battle.PlayerDamaged += OnPlayerDamaged;
        battle.TurnStarted += OnTurnStarted;
        battle.BattleEnded += OnBattleEnded;
        battle.CardsDrawn += OnCardsDrawn;
    }

    private void OnCardPlayed(CardData card)
    {
        if (card == null) { Play("card_play"); return; }

        switch (card.cardType)
        {
            case CardType.Shield:
                Play("card_shield");
                break;
            case CardType.Heal:
                Play("card_heal");
                break;
            case CardType.Draw:
                Play("card_draw");
                break;
            default:
                Play("card_play");
                break;
        }

        if (card.energyGain > 0) Play("energy_gain", 0.7f);
    }

    private void OnEnemyDamaged(int damage, int remainingHp)
    {
        // 伤害越高，打击感越重
        Play(damage >= 10 ? "hit_heavy" : "hit");
    }

    private void OnPlayerDamaged(int damage, int remainingHp)
    {
        Play("hit_player");
    }

    private void OnTurnStarted(int turn)
    {
        if (turn > 1) Play("turn_start", 0.8f);
    }

    private void OnCardsDrawn(int count)
    {
        Play("card_draw", 0.8f);
    }

    private void OnBattleEnded(bool win)
    {
        Play(win ? "win" : "lose");
        DuckBgm(bgmVolume * 0.25f, 0.5f);
    }
}
