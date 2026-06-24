using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyPartScript : MonoBehaviour
{
    public Rigidbody rb;
    public EnemyScript enemy;
    public Renderer bodyPartRenderer;
    public GameObject bodyPartPrefab;
    public bool replaced;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // ✨【工业级防呆设计】：如果在 Inspector 里忘了拖拽关联父体 Enemy，代码自动往上查找，确保绝对能抓到爸爸
        if (enemy == null)
        {
            enemy = GetComponentInParent<EnemyScript>();
        }
    }

    /// <summary>
    /// ✨ 核心拦截与传导：当原项目的子弹脚本通过消息、碰撞或直接调用这个函数时，
    /// 或者哪怕子弹只触发了隐藏替换，它都能百分之百强行把顶层的敌人本体干掉！
    /// </summary>
    public void HidePartAndReplace()
    {
        if (replaced)
            return;

        // ==================== ✨【时序修正：先断肢，后死人】====================
        // 1. 视觉层彻底断裂：关闭原本挂在红人身上的这个完好肢体渲染
        if (bodyPartRenderer != null)
            bodyPartRenderer.enabled = false;

        // 2. 物理层碎片喷射：生成断肢残片预制体
        GameObject part = new GameObject();
        if (bodyPartPrefab != null)
            part = Instantiate(bodyPartPrefab, transform.position, transform.rotation);

        // 给新生成的断肢碎片骨骼施加爆炸力，让它飞得更解压
        Rigidbody[] rbs = part.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody r in rbs)
        {
            r.interpolation = RigidbodyInterpolation.Interpolate;
            r.AddExplosionForce(15, transform.position, 5);
        }

        // 针对自身物理块施加冲力
        if (rb != null)
        {
            rb.AddExplosionForce(15, transform.position, 5);
        }

        // 锁定当前部位的替换锁，防止多发子弹重复断同一根手脚
        replaced = true;
        this.enabled = false;
        // =======================================================================


        // ==================== ✨【核心连带致死与死亡回放线】====================
        // 3. 肢体断裂完毕、碎片已飞出后，再通知顶层红人执行散架、死亡计数与镜头特写！
        if (enemy != null && !enemy.dead)
        {
            enemy.Ragdoll();

            // ✨【新增核心拦截】：在敌人散架触发 OnEnemyKilled 之前，去验证这是否是最后一个人！
            // 工业级判定：如果场景里有刷怪器，并且当前活着的人数刚好等于 1（就是眼前这个正在被打碎的倒霉蛋）
            if (SpawnManager.instance != null && SpawnManager.instance.IsAllWavesCleared() == false)
            {
                // ⚠️ 重点推敲：需要配合你在 SpawnManager 里的存活总人数变量
                // 假设你的 SpawnManager 里记录存活人数的变量是 aliveEnemiesInCurrentWave
                // 如果目前只剩这最后 1 个怪了，且目前是最后一波
                if (SpawnManager.instance.waves.Count > 0 && SpawnManager.instance.IsLastEnemyLeft())
                {
                    if (KillcamManager.instance != null)
                    {
                        // 传入当前的肢体部位 transform，回放飞出去的碎尸残片
                        // 此时因为上面的碎片在一瞬间已经生成完毕，回放镜头拍到的第一帧，刚好是手脚带着低通黑客帝国音效断开飞出的史诗画幅！
                        KillcamManager.instance.TriggerKillcam(this.transform);
                    }
                }
            }
        }
        // =======================================================================
    }
}