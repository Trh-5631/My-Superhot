using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletMovement : MonoBehaviour
{
    public float speed;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 判定撞击到了敌人总体或其任何骨骼层级
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponentInParent<EnemyScript>() != null)
        {
            // 保存子弹撞击瞬间的飞行方向，用于计算冲击力推向哪里
            Vector3 shotDirection = transform.forward;

            // 优先尝试获取直接撞击部位的断肢组件
            BodyPartScript bp = collision.gameObject.GetComponent<BodyPartScript>();

            // 🌟【防空引用报错防火墙】：
            // 如果子弹由于速度太快，直接一头撞在了怪物的总体大胶囊碰撞体上（导致 bp 为 null）
            if (bp == null)
            {
                // 自动向里层骨骼搜寻所有的肢体块
                BodyPartScript[] allParts = collision.gameObject.GetComponentsInChildren<BodyPartScript>();

                if (allParts != null && allParts.Length > 0)
                {
                    // 工业级智能补偿：从怪物身上完好的肢体中，随机抽选一个倒霉的部位直接切断！
                    List<BodyPartScript> availableParts = new List<BodyPartScript>();
                    foreach (var p in allParts)
                    {
                        if (!p.replaced) availableParts.Add(p);
                    }

                    if (availableParts.Count > 0)
                    {
                        bp = availableParts[Random.Range(0, availableParts.Count)];
                    }
                }
            }

            // 成功捕获或补偿到了断肢目标
            if (bp != null)
            {
                // 生成受击火花粒子
                if (SuperHotScript.instance != null && SuperHotScript.instance.hitParticlePrefab != null)
                {
                    Instantiate(SuperHotScript.instance.hitParticlePrefab, transform.position, transform.rotation);
                }

                //  严格执行时序：先斩肢、后死人
                bp.HidePartAndReplace();
                if (bp.enemy != null) bp.enemy.Ragdoll();

                // 【核心修复：给被击中部位注入子弹冲击力】
                // 此时布娃娃已经苏醒（isKinematic已为false），立刻顺着子弹方向加一个爆发力（Impulse）
                // 乘数 15f 是基础冲击权重，如果觉得怪飞得不够远，可以调大这个数值
                if (bp.rb != null)
                {
                    bp.rb.AddForce(shotDirection * speed * 1.3f, ForceMode.Impulse);
                }
            }
            else
            {
                // 终极防掉线：如果实在没挂肢体组件，直接让怪执行布娃娃死亡
                EnemyScript esc = collision.gameObject.GetComponent<EnemyScript>();
                if (esc == null) esc = collision.gameObject.GetComponentInParent<EnemyScript>();
                if (esc != null)
                {
                    esc.Ragdoll();

                    // 🚀 兜底冲击力：如果是大胶囊体中弹，直接把力推给红人盆骨根节点
                    Rigidbody rootRb = esc.GetComponentInChildren<Rigidbody>();
                    if (rootRb != null)
                    {
                        rootRb.AddForce(shotDirection * speed * 1.3f, ForceMode.Impulse);
                    }
                }
            }
        }

        // 销毁子弹本身
        Destroy(gameObject);
    }
}