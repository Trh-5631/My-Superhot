using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;
    [Header("✨ 关卡开局剧情定制")]
    [Tooltip("是否在关卡开局时触发看板娘对话")]
    public bool triggerDialogueOnStart = true;
    [Tooltip("开局说话人的名字")]
    public string openingSpeakerName = "引导员";
    [Tooltip("本关独有的开局台词内容")]
    [TextArea(2, 5)] // 让编辑器里的输入框变大，方便写小作文
    public string openingDialogueContent = "进入第二关战场。检测到前方出现高能近战反应，敌人已拔刀，请保持移动！";
    [Tooltip("开局台词对应的表情")]
    public DialogueManager.PortraitExpression openingExpression = DialogueManager.PortraitExpression.Default;
    
    // ==================== ✨ 新增：单个敌人的精准刷新指令 ====================
    [System.Serializable]
    public class SpawnCommand
    {
        [Tooltip("该名敌人专属的刷新位置（从层级面板把具体的 Point 拖进来）")]
        public Transform spawnPoint;
        [Tooltip("该位置要刷出的敌人专属预制件（远程拖远程Prefab，近战拖近战Prefab）")]
        public GameObject specificEnemyPrefab;
    }
    // ========================================================================

    [System.Serializable]
    public class Wave
    {
        // ✨ 已重构：波次里的敌人数量直接由配置的“精准指令列表”数量决定
        [Tooltip("配置该波次中每一个敌人的位置和兵种Prefab（点加号自由添加）")]
        public List<SpawnCommand> spawnCommands;
        [Tooltip("刷每名敌人之间的间隔（正常时间下）")]
        public float spawnInterval = 0.5f;
    }

    [Header("Wave Configuration")]
    [Tooltip("在这里手动配置每一波的敌人数量和参数")]
    public List<Wave> waves;
    [Tooltip("两波敌人之间的安全喘息/预警时间（单位：秒）")]
    public float waveDelay = 3.0f;

    [Header("References")]
    [Tooltip("刚刚制作的红色预警圆柱体预制件")]
    public GameObject indicatorPrefab;
    [Tooltip("把场景里的 SpawnPointsGroup 拖进来，脚本会自动提取所有子物体作为刷怪位")]
    public Transform spawnPointsGroup;

    // ====================  新增：多波次预警音效配置 ====================
    [Header("Audio Settings")]
    [Tooltip("新波次拉响警报时的本地音频剪辑")]
    public AudioClip waveWarningSound;
    private AudioSource audioSource;
    // ===================================================================

    private int currentWaveIndex = 0;
    private bool isWaveSpawning = false;
    private int aliveEnemiesInCurrentWave = 0;
    private bool allWavesCleared = false;
    private List<Transform> spawnPoints = new List<Transform>();

    void Awake()
    {
        instance = this;

        // 自动挂载或获取音频源组件，用来播放预警音效
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        // 工业标准：确保预警音效是全场通告的2D音效，且不随距离衰减
        audioSource.spatialBlend = 0f;
    }

    void Start()
    {
        // 自动提取所有配置的刷怪位
        if (spawnPointsGroup != null)
        {
            foreach (Transform child in spawnPointsGroup)
            {
                spawnPoints.Add(child);
            }
        }

        // 核心改动：自适应接管本地老兵，改用组件扫描
        int initialEnemyCount = 0;
        EnemyScript[] allEnemiesInScene = Object.FindObjectsByType<EnemyScript>(FindObjectsSortMode.None);

        foreach (EnemyScript eScript in allEnemiesInScene)
        {
            if (eScript != null && !eScript.dead && eScript.gameObject.scene.IsValid())
            {
                initialEnemyCount++;
            }
        }

        if (initialEnemyCount > 0)
        {
            // 如果玩家在地图上摆了初始敌人，将其纳为“第 1 波”，管理器在后台静默等待他们死亡
            aliveEnemiesInCurrentWave = initialEnemyCount;
            Debug.Log($"<color=yellow>【波次系统】</color> 成功接管场景初始敌人共 {initialEnemyCount} 名！它们将作为第 1 波。");
        }
        else if (waves.Count > 0)
        {
            // 如果地图上什么都没摆，则立刻启动配置的列表里的第一波
            StartCoroutine(StartNextWaveRoutine());
        }
        else
        {
            allWavesCleared = true;
        }

        if (triggerDialogueOnStart && DialogueManager.instance != null)
        {
            DialogueManager.instance.ShowDialogue(openingSpeakerName, openingDialogueContent, openingExpression);
        }
    }

    /// <summary>
    /// 核心协程：控制波次延迟、发射地面刷新预警，并克隆出实体红人
    /// </summary>
    IEnumerator StartNextWaveRoutine()
    {
        isWaveSpawning = true;

        // 确定当前要跑的是配置列表里的第几波
        int configIndex = (aliveEnemiesInCurrentWave == 0 && currentWaveIndex > 0) ? currentWaveIndex - 1 : currentWaveIndex;
        Wave wave = waves[configIndex];

        // ✨ 该波次要击杀的人数等于配置的指令总数
        aliveEnemiesInCurrentWave = wave.spawnCommands.Count;

        Debug.Log($"<color=cyan>【波次系统】</color> 第 {currentWaveIndex + 1} 波即将到来，正在分派不重复的点位...");

        // ====================  新增：高精度预警音效触发 ====================
        // 当红柱子亮起的绝对第一帧，跨越时间流速限制，完美播放提示音效
        if (audioSource != null && waveWarningSound != null)
        {
            // 在 SUPERHOT 开发中，由于时间缩放，必须通过调整 pitch（或者由全局管理器特殊控制）
            // 如果你发现声音跟着变慢，可以取消下面这行的注释强制原速播放：
            // audioSource.pitch = 1f / Time.timeScale; 
            audioSource.PlayOneShot(waveWarningSound);
        }
        // ===================================================================

        // 1. 依次在指令指定的专属点位上，先生成警告虚影
        foreach (SpawnCommand cmd in wave.spawnCommands)
        {
            if (cmd.spawnPoint != null && indicatorPrefab != null)
            {
                GameObject ind = Instantiate(indicatorPrefab, cmd.spawnPoint.position, Quaternion.identity);
                // 使用真实现实时间销毁预警物体，防止受到 SUPERHOT 慢动作影响导致警告框迟迟不走
                Destroy(ind, waveDelay);
            }
        }

        // 给玩家留出配置的 waveDelay（喘息/预警时间）
        yield return new WaitForSecondsRealtime(waveDelay);

        // 2. 正式下放实体敌人，一个坑点绝对只下一个兵
        foreach (SpawnCommand cmd in wave.spawnCommands)
        {
            if (cmd.spawnPoint != null && cmd.specificEnemyPrefab != null)
            {
                //  自动化防呆下压射线贴地系统
                Vector3 spawnPosition = cmd.spawnPoint.position;
                RaycastHit hit;

                if (Physics.Raycast(spawnPosition + Vector3.up * 1f, Vector3.down, out hit, 10f))
                {
                    spawnPosition.y = hit.point.y;
                }

                // ✨ 核心魔改：实例化对应的独立敌人预制件
                Instantiate(cmd.specificEnemyPrefab, spawnPosition, cmd.spawnPoint.rotation);
            }
            else if (cmd.spawnPoint != null)
            {
                Debug.LogError("⚠️ 发现未配置完全的刷怪指令！请检查特定的 Enemy Prefab 是否漏拖了。");
            }

            // 敌人相互之间带有轻微的间隔，防止由于同帧生成发生卡模型引发物理弹飞
            yield return new WaitForSeconds(wave.spawnInterval);
        }

        isWaveSpawning = false;
    }

    /// <summary>
    /// 公开接口：供场景里的敌人死亡时调用。当敌人彻底散架时，向本管理器报到
    /// </summary>
    public void OnEnemyKilled()
    {
        aliveEnemiesInCurrentWave--;

        // 判断当前波次是否死光，且当前没有在刷怪过程中
        if (aliveEnemiesInCurrentWave <= 0 && !isWaveSpawning)
        {
            currentWaveIndex++;
            int totalExpectedWaves = waves.Count + 1; // 1(初始老兵) + 配置动态波次

            // A. 如果还有下一波：在短暂清屏后，继续拉响警报
            if (currentWaveIndex < totalExpectedWaves)
            {
                StartCoroutine(StartNextWaveRoutine());

                if (DialogueManager.instance != null)
                {
                    DialogueManager.instance.ShowDialogue("奈珂", "警告：检测到敌方增援正在传送，注意隐蔽，消灭他们！", DialogueManager.PortraitExpression.Warning);
                }
            }
            // B. 如果这就是最后一波：彻底解放，关卡防火墙宣布正式解禁！
            else
            {
                allWavesCleared = true;
                Debug.Log("<color=green>【波次系统】</color> 最后一波敌人已被全歼！通关封锁解除！");      
                
                if (DialogueManager.instance != null)
                {
                DialogueManager.instance.ShowDialogue("奈珂", "威胁已安全清除，检测到大门已开启。干得好，我就知道我没看错你。", DialogueManager.PortraitExpression.Victory);
                }
            }

        }
    }

    /// <summary>
    /// 提供给大门脚本调用的安全防火墙权限验证
    /// </summary>
    public bool IsAllWavesCleared()
    {
        return allWavesCleared;
    }
}