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

        // 1. 核心连带致死：一旦这条肢体被打碎，立刻强制顶层红人执行 Ragdoll 散架和波次报到！
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
                        KillcamManager.instance.TriggerKillcam(this.transform);
                    }
                }
            }
        }

        if (bodyPartRenderer != null)
            bodyPartRenderer.enabled = false;

        GameObject part = new GameObject();
        if (bodyPartPrefab != null)
            part = Instantiate(bodyPartPrefab, transform.position, transform.rotation);

        Rigidbody[] rbs = part.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody r in rbs)
        {
            r.interpolation = RigidbodyInterpolation.Interpolate;
            r.AddExplosionForce(15, transform.position, 5);
        }

        rb.AddExplosionForce(15, transform.position, 5);

        this.enabled = false;
        replaced = true;
    }
}