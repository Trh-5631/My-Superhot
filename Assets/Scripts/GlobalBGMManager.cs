using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GlobalBGMManager : MonoBehaviour
{
    private static GlobalBGMManager _instance;
    public static GlobalBGMManager Instance => _instance; // 暴露单例接口，方便主界面调用

    [Header("音频源配置")]
    [Tooltip("拖入用于全局循环播放的战斗BGM (AudioSource)")]
    public AudioSource musicSource;
    [Tooltip("拖入专门用于主界面的BGM (AudioSource)")]
    public AudioSource mainMenuSource;
    [Header("淡入淡出设置")]
    [Tooltip("音乐淡变的时间（秒），0.8 - 1.2秒最舒适")]
    public float fadeDuration = 1.0f;

    // ==================== ✨ 新增：Demo通关状态防火墙 ====================
    [System.NonSerialized]
    public bool isDemoCompletedViaPortal = false; // 内部标记：仅在玩家从最终关传送门离开时置为 true
    // ====================================================================

    private float targetBattleVolume;

    void Awake()
    {
        // 1. 单例机制检测：确保整个游戏生命周期中，有且仅有一个 BGM 控制器存在
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 2. 确立唯一实例并移入保护区
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 容错处理
        if (musicSource == null) musicSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        // 记录你在 Inspector 里给战斗 BGM 设定的初始音量上限
        if (musicSource != null) targetBattleVolume = musicSource.volume;

        // 初始化：如果我们在主界面场景启动
        if (mainMenuSource != null)
        {
            mainMenuSource.loop = true;
            mainMenuSource.Play(); // 主界面音乐直接拉满播放
        }

        // 战斗 BGM 先闭嘴，等待点击开始游戏
        if (musicSource != null)
        {
            musicSource.volume = 0f;
            musicSource.Stop();
        }
    }

    /// <summary>
    /// ✨ 核心触发器：当玩家点击主菜单的“开始游戏”时，主菜单脚本调用此函数
    /// </summary>
    public void TriggerMenuToBattleFade()
    {
        StartCoroutine(FadeMenuToBattleRoutine());
    }

    /// <summary>
    /// ⏳ 协程：优雅的主界面音乐淡出 ──> 战斗音乐淡入
    /// </summary>
    private IEnumerator FadeMenuToBattleRoutine()
    {
        float timer = 0f;

        // 确保战斗BGM在静音状态下先跑起来，准备接棒
        if (musicSource != null)
        {
            musicSource.volume = 0f;
            musicSource.loop = true;
            musicSource.Play();
        }

        // 【第一阶段：双向交织淡变】在 fadeDuration 时间内，一个变小，一个变大
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // 使用无视时间减速的真实时间，防止SUPERHOT机制干扰
            float progress = timer / fadeDuration;

            // 主界面 BGM 线性淡出
            if (mainMenuSource != null)
            {
                mainMenuSource.volume = Mathf.Lerp(1f, 0f, progress);
            }

            // 战斗 BGM 线性淡入
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Lerp(0f, targetBattleVolume, progress);
            }

            yield return null;
        }

        // 【第二阶段：斩草除根】彻底关闭主界面音频源，确保音量严格校准
        if (mainMenuSource != null)
        {
            mainMenuSource.Stop();
            mainMenuSource.volume = 0f;
        }

        if (musicSource != null)
        {
            musicSource.volume = targetBattleVolume;
        }
    }
}