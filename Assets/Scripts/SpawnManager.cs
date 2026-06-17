using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    [System.Serializable]
    public class Wave
    {
        [Tooltip("该波次要刷出的敌人总数")]
        public int enemyCount;
        [Tooltip("刷每名敌人之间的间隔（正常时间下）")]
        public float spawnInterval = 0.5f;
    }

    [Header("Wave Configuration")]
    [Tooltip("在这里手动配置每一波的敌人数量和参数")]
    public List<Wave> waves;
    [Tooltip("两波敌人之间的安全喘息/预警时间（单位：秒）")]
    public float waveDelay = 3.0f;

    [Header("References")]
    [Tooltip("敌人的预制件（Prefab）")]
    public GameObject enemyPrefab;
    [Tooltip("刚刚制作的红色预警圆柱体预制件")]
    public GameObject indicatorPrefab;
    [Tooltip("把场景里的 SpawnPointsGroup 拖进来，脚本会自动提取所有子物体作为刷怪位")]
    public Transform spawnPointsGroup;

    private int currentWaveIndex = 0;
    private bool isWaveSpawning = false;
    private int aliveEnemiesInCurrentWave = 0;
    private bool allWavesCleared = false;
    private List<Transform> spawnPoints = new List<Transform>();

    void Awake()
    {
        instance = this;
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
    }

    /// <summary>
    /// 核心协程：控制波次延迟、发射地面刷新预警，并克隆出实体红人
    /// ✨ 已重构：引入 Fisher-Yates 洗牌算法，确保一个点位绝对只刷一个敌人，不产生位置堆叠
    /// </summary>
    IEnumerator StartNextWaveRoutine()
    {
        isWaveSpawning = true;

        // 确定当前要跑的是配置列表里的第几波
        int configIndex = (aliveEnemiesInCurrentWave == 0 && currentWaveIndex > 0) ? currentWaveIndex - 1 : currentWaveIndex;
        Wave wave = waves[configIndex];
        aliveEnemiesInCurrentWave = wave.enemyCount;

        Debug.Log($"<color=cyan>【波次系统】</color> 第 {currentWaveIndex + 1} 波即将到来，正在分派不重复的点位...");

        // 1. ✨【高水准洗牌算法】：复制一份点位列表，将其顺序彻底打乱
        List<Transform> shuffledPoints = new List<Transform>(spawnPoints);
        for (int i = 0; i < shuffledPoints.Count; i++)
        {
            Transform temp = shuffledPoints[i];
            int randomIndex = Random.Range(i, shuffledPoints.Count);
            shuffledPoints[i] = shuffledPoints[randomIndex];
            shuffledPoints[randomIndex] = temp;
        }

        // 2. ✨【安全边界拦截】：如果配置的刷怪数大于你的点位总数，防止数组越界
        int actualSpawnCount = Mathf.Min(wave.enemyCount, shuffledPoints.Count);
        if (wave.enemyCount > shuffledPoints.Count)
        {
            Debug.LogError($"⚠️ 警告：当前波次配置的敌人数量({wave.enemyCount})超过了场景里的刷怪点总数({shuffledPoints.Count})！已自动拦截堆叠。");
        }

        // 3. 依次在各不相同的专属点位上，先生成警告虚影
        for (int i = 0; i < actualSpawnCount; i++)
        {
            Transform p = shuffledPoints[i];
            if (indicatorPrefab != null)
            {
                GameObject ind = Instantiate(indicatorPrefab, p.position, Quaternion.identity);
                // 使用真实现实时间销毁预警物体，防止受到 SUPERHOT 慢动作影响导致警告框迟迟不走
                Destroy(ind, waveDelay);
            }
        }

        // 给玩家留出配置的 waveDelay（喘息/预警时间）
        yield return new WaitForSecondsRealtime(waveDelay);

        // 4. 正式下放实体敌人，一个坑点绝对只下一个兵
        for (int i = 0; i < actualSpawnCount; i++)
        {
            Transform p = shuffledPoints[i];
            if (enemyPrefab != null)
            {
                // ==================== ✨ 新增：自动化防呆下压射线贴地系统 ====================
                // 抓取当前刷怪点的基础坐标
                Vector3 spawnPosition = p.position;
                RaycastHit hit;

                // 从点位上方 1 米处向下发射一条 10 米长的射线，探测真正的地板物理碰撞表面
                if (Physics.Raycast(spawnPosition + Vector3.up * 1f, Vector3.down, out hit, 10f))
                {
                    // 如果射中地板，强制将刷怪高度重置为地板的真实 Y 轴高度，完美解决悬空问题！
                    spawnPosition.y = hit.point.y;
                }
                // ============================================================================

                // 实例化敌人时，使用修正后的贴地坐标 spawnPosition 替代原先可能悬空的 p.position
                Instantiate(enemyPrefab, spawnPosition, p.rotation);
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
            }
            // B. 如果这就是最后一波：彻底解放，关卡防火墙宣布正式解禁！
            else
            {
                allWavesCleared = true;
                Debug.Log("<color=green>【波次系统】</color> 最后一波敌人已被全歼！通关封锁解除！");
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