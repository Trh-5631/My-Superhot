using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // 1. 新增：引入 Unity 原生导航命名空间
// 新增：枚举类型，用于在 Inspector 面板中一键切换敌人的兵种
public enum EnemyType { Ranged, Melee }

[SelectionBase]
public class EnemyScript : MonoBehaviour
{
    Animator anim;
    public bool dead;
    public Transform weaponHolder;
    // ==================== 魔改近战敌人 AI 新增变量 ====================
    [Header("AI Settings")]
    public EnemyType enemyType = EnemyType.Ranged; // 默认是远程射击兵
    public float moveSpeed = 3.5f; // 敌人的移动速度
    public float attackRange = 2.0f; // 近战敌人的攻击距离限制
    public float attackRate = 1.5f; // 近战挥砍的频率（秒/次）
    private NavMeshAgent agent; // 导航组件引用
    private float nextAttackTime; // 下一次可以攻击的时间戳
    // ==================================================================
    void Start()
    {
        anim = GetComponent<Animator>();
        StartCoroutine(RandomAnimation());
        // 获取或自动添加导航组件（仅在未死亡且是近战时工作）
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            // 如果是远程兵，初始不需要导航移动
            if (enemyType == EnemyType.Ranged)
            {
                agent.enabled = false;
            }
        }
        if (weaponHolder.GetComponentInChildren<WeaponScript>() != null)
            weaponHolder.GetComponentInChildren<WeaponScript>().active = false; //
    }
    void Update()
    {
        if (dead)
            return;

        // 🚨【安全锁拦截】：获取当前主相机并进行安全检查
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            // 如果触发了死亡回放（Killcam）导致主相机被隐藏/销毁，立刻安全返回，防止闪退报错
            if (agent != null && agent.enabled) agent.isStopped = true;
            return;
        }

        // 无论哪种敌人，活着时都要在水平方向死死盯住玩家
        transform.LookAt(new Vector3(mainCam.transform.position.x, transform.position.y, mainCam.transform.position.z));
        // ==================== AI 行为决策机 ====================
        if (enemyType == EnemyType.Melee && agent != null && agent.enabled)
        {
            // 实时将玩家的当前坐标设为导航终点
            agent.SetDestination(mainCam.transform.position);
            // 计算敌人与玩家之间的水平距离
            float distanceToPlayer = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(mainCam.transform.position.x, 0, mainCam.transform.position.z));
            // 判断是否进入了攻击范围
            if (distanceToPlayer <= attackRange)
            {
                // 停下脚步，准备攻击
                agent.isStopped = true;
                if (anim != null)
                {
                    anim.SetBool("IsRunning", false);
                }
                // 频率控制器：使用真实时间戳判定（防止 SUPERHOT 时间减速导致计时器抽风）
                if (Time.time >= nextAttackTime)
                {
                    PerformMeleeAttack(mainCam); // 传递健康的相机引用
                    nextAttackTime = Time.time + attackRate;
                }
            }
            else
            {
                // 距离还不够，继续迈开腿追击
                agent.isStopped = false;
                // 如果你有跑步/走路动画，可以在这里设置动画参数
                if (anim != null) anim.SetBool("IsRunning", true);
            }
        }
    }

    // ==================== 新增：近战敌人挥砍逻辑 ====================
    // ==================== 修改：近战敌人挥砍时序逻辑 ====================
    // ==================== 彻底修复：完美契合 SUPERHOT 变速机制的近战动作时序 ====================
    void PerformMeleeAttack(Camera targetCam)
    {
        if (dead || targetCam == null) return;

        // 1. 触发敌人的挥砍动画
        if (anim != null)
        {
            anim.SetTrigger("shoot");
        }

        // 2. 开启时序协程：使用游戏内的动态流速时间进行延迟
        StartCoroutine(MeleeDamageTimingRoutine());
    }

    private IEnumerator MeleeDamageTimingRoutine()
    {
        // 🚨【终极修复点】：必须使用 WaitForSeconds，严禁使用 Realtime！
        // 这里的 0.3f 代表挥砍动画在【标准100%流速下】砍下来的时间。
        // 当时间变为 3% 的极慢速时，这个等待时间会自动拉长 33 倍，确保伤害判定与由于时停变慢的动画完全同步！
        yield return new WaitForSeconds(0.8f);

        // 3. 安全检查：如果在等待期间敌人已经中弹变成布娃娃（dead），或者玩家已经触发死亡，则直接拦截判定
        if (dead || SuperHotScript.instance == null) yield break;

        // 4. 动画真正砍下来这一帧，执行球体空间范围检测
        Vector3 attackCenter = transform.position + Vector3.up * 1.2f;
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRange);

        bool playerHit = false;

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") ||
                hitCollider.CompareTag("MainCamera") ||
                hitCollider.GetComponentInParent<SuperHotScript>() != null)
            {
                playerHit = true;
                break;
            }
        }

        // 5. 判定最终挥砍物理结果
        if (playerHit)
        {
            Debug.Log("💥 [近战判定成功] 敌人的动作与流速完美匹配，实实在在地砍中了范围内的玩家！");
            SuperHotScript.instance.TakeDamage();
        }
        else
        {
            Debug.Log("💨 [近战未命中] 敌人刀砍下了，但玩家通过动态走位成功闪开了这记挥砍。");
        }
    }

    public void Ragdoll()
    {
        // 【工业级金闸门】：核心拦截防火墙！
        // 如果在这一帧之前，我已经处于 dead 状态，或者已经被别的子肢体抢先“上报”过死亡了，
        // 立刻执行 return，绝对拒绝向下执行！防止多重碰撞导致波次计数器雪崩！
        if (dead) return;

        // 抢先占位，上锁！
        dead = true;

        // 死亡时立刻关闭导航网格，防止尸体死了还会诡异地滑向玩家
        if (agent != null) agent.enabled = false;
        if (anim != null) anim.enabled = false;

        BodyPartScript[] parts = GetComponentsInChildren<BodyPartScript>();
        foreach (BodyPartScript bp in parts)
        {
            if (bp.rb != null)
            {
                bp.rb.isKinematic = false;
                bp.rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        if (weaponHolder != null && weaponHolder.GetComponentInChildren<WeaponScript>() != null)
        {
            WeaponScript w = weaponHolder.GetComponentInChildren<WeaponScript>();
            w.Release();
        }

        // 只有成功突破上方 `if (dead)` 防火墙的第一帧，才有资格向管理器发送【唯一一次】死讯传真
        if (SpawnManager.instance != null)
        {
            SpawnManager.instance.OnEnemyKilled();
        }
    }

    public void Shoot()
    {
        if (dead || enemyType == EnemyType.Melee) // 如果是近战兵，拦截并拒绝执行射击子弹事件
            return;
        if (weaponHolder.GetComponentInChildren<WeaponScript>() != null) //
            weaponHolder.GetComponentInChildren<WeaponScript>().Shoot(GetComponentInChildren<ParticleSystem>().transform.position, transform.rotation, true); //
    }

    IEnumerator RandomAnimation()
    {
        anim.enabled = false; //
        yield return new WaitForSecondsRealtime(Random.Range(.1f, .5f)); //
        anim.enabled = true; //
    }
}