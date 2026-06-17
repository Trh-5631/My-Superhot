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