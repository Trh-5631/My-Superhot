using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GlobalBGMManager : MonoBehaviour
{
    // 全局静态实例，方便后续可能需要的音量控制或静音操作
    private static GlobalBGMManager _instance;

    [Header("Audio Settings")]
    [Tooltip("拖入用于全局循环播放的战斗BGM")]
    public AudioSource musicSource;

    void Awake()
    {
        // 1. 单例机制检测：确保整个游戏生命周期中，有且仅有一个 BGM 控制器存在
        if (_instance != null && _instance != this)
        {
            // 如果此物体被重复创建（例如玩家从第2关死掉重新回到第1关，导致初始关卡被重新加载）
            // 则立刻销毁这个新诞生的复制品，防止多首音乐重叠播放
            Destroy(gameObject);
            return;
        }

        // 2. 确立唯一实例
        _instance = this;

        // 3. 核心机制：将此物体移入 DontDestroyOnLoad 隐藏保护区
        // 使其彻底脱离常规场景切换的销毁周期，实现连续不中断播放
        DontDestroyOnLoad(gameObject);

        // 容错处理：如果在 Inspector 面板忘记拖入，则自动获取本地组件
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();
    }
}