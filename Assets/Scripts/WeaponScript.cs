using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro; //  引入 TextMeshPro 命名空间来控制斜杠 UI

// 武器类型枚举，用于区分当前武器是手枪还是武士刀
public enum WeaponType { Pistol, Katana }

[RequireComponent(typeof(Rigidbody))]
[SelectionBase]
public class WeaponScript : MonoBehaviour
{
    [Header("Bools")]
    public bool active = true;
    public bool reloading;

    private Rigidbody rb;
    private Collider collider;
    private new Renderer renderer;

    // ==================== 武士刀新增变量 ====================
    [Header("Weapon General Settings")]
    public WeaponType weaponType = WeaponType.Pistol; // 默认是手枪

    [Header("Melee Settings (Only for Katana)")]
    public float meleeRange = 3.0f;     // 近战挥砍的最大距离
    public float attackCooldown = 0.4f; // 挥砍后的冷却时间（等同于手枪 reloadTime）
    [HideInInspector] public bool isAttacking; // 标记当前是否正在播放挥砍动画
    // ============================================================

    [Space]
    [Header("Weapon Settings (Only for Pistol)")]
    public float reloadTime = .3f;
    public int bulletAmount = 4; //  按照你的设定：默认预设修改为 4 发子弹

    private static TextMeshProUGUI ammoHUDText; // 静态引用，避免每把枪频繁寻找 UI 组件

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        collider = GetComponent<Collider>();
        renderer = GetComponent<Renderer>();

        // 自动建立前端 HUD 文本的全局关联
        if (ammoHUDText == null)
        {
            GameObject hudObj = GameObject.Find("AmmoHUD");
            if (hudObj != null) ammoHUDText = hudObj.GetComponent<TextMeshProUGUI>();
        }

        ChangeSettings();
    }

    void ChangeSettings()
    {
        if (transform.parent != null)
            return;

        rb.isKinematic = (SuperHotScript.instance.weapon == this) ? true : false;
        rb.interpolation = (SuperHotScript.instance.weapon == this) ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        collider.isTrigger = (SuperHotScript.instance.weapon == this);
    }

    // 射击逻辑：符合 SUPERHOT 机制的硬核子弹计数器
    public void Shoot(Vector3 pos, Quaternion rot, bool isEnemy)
    {
        if (reloading)
            return;

        bool isPlayerWeapon = (SuperHotScript.instance.weapon == this);

        // ====================  子弹限制核心拦截线  ====================
        if (isPlayerWeapon)
        {
            // 如果玩家当前枪械的子弹已经全部打光
            if (bulletAmount <= 0)
            {
                // 核心要求：枪体依然播放后坐力后移效果和屏幕微震，但绝对不发射子弹
                Camera.main.transform.DOComplete();
                Camera.main.transform.DOShakePosition(.1f, .005f, 10, 90, false, true).SetUpdate(true);
                transform.DOLocalMoveZ(-.06f, .05f).OnComplete(() => transform.DOLocalMoveZ(0, .2f));

                Debug.Log("咔哒！玩家子弹耗尽，武器仅发生空响后坐力！");
                return; //  彻底拦截！不再生产子弹、不再播放开火粒子、不触发重新装填！
            }

            // 还有子弹，则正常扣除玩家这一发
            bulletAmount--;
            UpdateAmmoHUD(bulletAmount); // 同步刷新准星下方的斜杠状态
        }
        else
        {
            // 如果是敌人开枪：按照你的要求，敌人不会消耗该手枪预制件的真实子弹（保持数据满额）
            // 故此处不执行子弹递减，确保枪掉落并飞向玩家时依然是满的
        }
        // ===================================================================

        // 产生子弹实体
        GameObject bullet = Instantiate(SuperHotScript.instance.bulletPrefab, pos, rot);

        // 播放火光粒子特效
        if (GetComponentInChildren<ParticleSystem>() != null)
            GetComponentInChildren<ParticleSystem>().Play();

        // 仅玩家武器需要进入重新装填冷却机制
        if (isPlayerWeapon)
            StartCoroutine(Reload());

        // 屏幕震动视觉锤炼
        Camera.main.transform.DOComplete();
        Camera.main.transform.DOShakePosition(.2f, .01f, 10, 90, false, true).SetUpdate(true);

        // 正常的枪身后移回弹动画
        if (isPlayerWeapon)
            transform.DOLocalMoveZ(-.1f, .05f).OnComplete(() => transform.DOLocalMoveZ(0, .2f));
    }

    // ==================== 核心算法：TextMeshPro 富文本斜杠动态渲染  ====================
    public static void UpdateAmmoHUD(int remainBullet)
    {
        if (ammoHUDText == null) return;

        // 如果连手枪都没拿（比如手里是空的或者拿着刀），不显示斜杠
        if (SuperHotScript.instance.weapon == null || SuperHotScript.instance.weapon.weaponType == WeaponType.Katana)
        {
            ammoHUDText.text = "";
            return;
        }

        // 高级富文本标签替换逻辑：将耗尽的子弹包裹在透明度为 55 的淡灰色渲染标签内
        if (remainBullet >= 4) ammoHUDText.text = "/ / / /";
        else if (remainBullet == 3) ammoHUDText.text = "/ / / <color=#FFFFFF55>/</color>";
        else if (remainBullet == 2) ammoHUDText.text = "/ / <color=#FFFFFF55>/ /</color>";
        else if (remainBullet == 1) ammoHUDText.text = "/<color=#FFFFFF55> / / /</color>";
        else ammoHUDText.text = "<color=#FFFFFF55>/ / / /</color>";
    }

    // ==================== 武士刀近战挥砍逻辑 ====================
    public void PerformMeleeAttack()
    {
        // 如果正在冷却中，或者上一刀的动画还没播完，则拒绝执行攻击
        if (reloading || isAttacking)
            return;

        StartCoroutine(MeleeAttackRoutine());
    }

    IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;

        // 1. 播放近战挥砍的视觉动画效果（使用 DOTween 控制本地坐标和旋转）
        // .SetUpdate(true) 确保在 SUPERHOT 时间极慢或静止时，刀的挥砍速度依旧流畅
        Sequence swingSequence = DOTween.Sequence();
        swingSequence.Append(transform.DOLocalMove(new Vector3(0.3f, -0.2f, 0.4f), 0.04f).SetEase(Ease.OutQuad))
                     .Join(transform.DOLocalRotate(new Vector3(15f, -60f, 30f), 0.04f).SetEase(Ease.OutQuad))
                     .Append(transform.DOLocalMove(Vector3.zero, 0.15f).SetEase(Ease.InOutQuad))
                     .Join(transform.DOLocalRotate(Vector3.zero, 0.15f).SetEase(Ease.InOutQuad))
                     .SetUpdate(true);

        // 2. 射线检测：从屏幕中心向前方发射一条长度为 meleeRange 的射线
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, meleeRange))
        {
            // 修复：使用 hit.transform.gameObject 代替 hit.gameObject
            if (hit.transform.gameObject.CompareTag("Enemy"))
            {
                BodyPartScript bp = hit.transform.gameObject.GetComponent<BodyPartScript>();
                if (bp != null)
                {
                    // 在击中点生成火花粒子特效
                    if (SuperHotScript.instance.hitParticlePrefab != null)
                    {
                        Instantiate(SuperHotScript.instance.hitParticlePrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    }

                    // 调用原版肢体脚本：隐藏当前身体部位并生成碎裂物体
                    bp.HidePartAndReplace();
                    // 触发布娃娃系统，让敌人彻底死亡崩溃并扔掉枪
                    bp.enemy.Ragdoll();
                }
            }
        }
        // 3. 冷却处理：复用原版的 UI 转圈提示和 reloading 锁
        if (SuperHotScript.instance.weapon != null && SuperHotScript.instance.weapon == this)
        {
            SuperHotScript.instance.ReloadUI(attackCooldown);
            reloading = true;
            yield return new WaitForSeconds(attackCooldown);
            reloading = false;
        }

        isAttacking = false;
    }

    public void Throw()
    {
        Sequence s = DOTween.Sequence();
        s.Append(transform.DOMove(transform.position - transform.forward, .01f)).SetUpdate(true);
        s.AppendCallback(() => transform.parent = null);
        s.AppendCallback(() => transform.position = Camera.main.transform.position + (Camera.main.transform.right * .1f));
        s.AppendCallback(() => ChangeSettings());
        s.AppendCallback(() => rb.AddForce(Camera.main.transform.forward * 10, ForceMode.Impulse));
        s.AppendCallback(() => rb.AddTorque(transform.transform.right + transform.transform.up * 20, ForceMode.Impulse));
        //  投掷出枪械后，立刻把屏幕正中心的斜杠 HUD 清空关闭
        s.AppendCallback(() => { if (ammoHUDText != null) ammoHUDText.text = ""; });
    }

    public void Pickup()
    {
        if (!active)
            return;

        SuperHotScript.instance.weapon = this;
        ChangeSettings();

        transform.parent = SuperHotScript.instance.weaponHolder;

        transform.DOLocalMove(Vector3.zero, .25f).SetEase(Ease.OutBack).SetUpdate(true);
        transform.DOLocalRotate(Vector3.zero, .25f).SetUpdate(true);
        // 核心实装：当掉落的枪反向飞向玩家并被吸入手心（Pickup）的瞬间，立刻刷新 HUD 为该枪当前的真实子弹数！
        UpdateAmmoHUD(bulletAmount);
    }

    public void Release()
    {
        active = true;
        transform.parent = null;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        collider.isTrigger = false;

        rb.AddForce((Camera.main.transform.position - transform.position) * 2, ForceMode.Impulse);
        rb.AddForce(Vector3.up * 2, ForceMode.Impulse);
    }

    IEnumerator Reload()
    {
        if (SuperHotScript.instance.weapon != this)
            yield break;
        SuperHotScript.instance.ReloadUI(reloadTime);
        reloading = true;
        yield return new WaitForSeconds(reloadTime);
        reloading = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 飞刀/飞枪砸人逻辑：武士刀掷出砸到敌人时，完全复用此处的碰撞致死逻辑
        if (collision.gameObject.CompareTag("Enemy") && collision.relativeVelocity.magnitude < 15)
        {
            BodyPartScript bp = collision.gameObject.GetComponent<BodyPartScript>();

            if (bp != null && bp.enemy != null && !bp.enemy.dead)
                Instantiate(SuperHotScript.instance.hitParticlePrefab, transform.position, transform.rotation);

            if (bp != null)
            {
                bp.HidePartAndReplace();
                if (bp.enemy != null) bp.enemy.Ragdoll();
            }
        }
    }
}