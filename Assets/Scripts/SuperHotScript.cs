using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SuperHotScript : MonoBehaviour
{
    public static SuperHotScript instance;

    public float charge;
    public bool canShoot = true;
    public bool action;
    public GameObject bullet;
    public Transform bulletSpawner;

    [Header("Weapon")]
    public WeaponScript weapon;
    public Transform weaponHolder;
    public LayerMask weaponLayer;
    [SerializeField] public float pickupDistance = 2.5f; // 新增：可调节的拾取距离（推荐 3 到 4 米）

    [Space]
    [Header("UI")]
    public Image indicator;

    [Space]
    [Header("Prefabs")]
    public GameObject hitParticlePrefab;
    public GameObject bulletPrefab;

    [Space]
    [Header("Player Health & Debug Backdoor")]
    [Tooltip("无敌模式开关：勾选后玩家免疫任何伤害")]
    public bool isGodMode = false;
    [Tooltip("演示用无敌模式切换键")]
    public KeyCode godModeToggleKey = KeyCode.G;
    [Tooltip("视野变暗所需的全局全屏黑布 UI")]
    public Image fadeImage;

    private bool isPlayerDead = false; // 内部死亡锁：防止一帧内多发子弹重复触发死亡协程

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // 在 Start 中安全地检查初始武器
        if (weaponHolder.GetComponentInChildren<WeaponScript>() != null)
            weapon = weaponHolder.GetComponentInChildren<WeaponScript>();
    }

    // Update is called once per frame
    void Update()
    {
        // ==================== 0. 无敌模式后门检测 ====================
        if (Input.GetKeyDown(godModeToggleKey))
        {
            isGodMode = !isGodMode;
            Debug.Log($"【后门日志】玩家无敌模式状态已切换为: {isGodMode}");
        }

        // ==================== 1. 玩家死亡拦截线 ====================
        if (isPlayerDead)
        {
            // 死亡演播期间，强行让时间流速平滑收敛至极慢速（3%），并切断所有玩家输入
            Time.timeScale = Mathf.Lerp(Time.timeScale, 0.03f, 0.1f);
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ==================== 拾取与换武器逻辑（最高优先级） ====================
        RaycastHit hit;
        //  修改拾取参数，便于随时调整
        bool aimingAtWeapon = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, pickupDistance, weaponLayer);

        if (aimingAtWeapon && Input.GetMouseButtonDown(0))
        {
            // 规范化获取组件：由于存在父子嵌套，使用 GetComponentInParent 确保能精准拿到 WeaponScript
            WeaponScript targetWeapon = hit.transform.GetComponentInParent<WeaponScript>();

            if (targetWeapon != null && targetWeapon != weapon)
            {
                // 如果手上有旧武器，先强行把它强制丢弃（释放），腾出手来
                if (weapon != null)
                {
                    weapon.Release();
                }

                // 拾取新武器
                targetWeapon.Pickup();
                return; // 换枪成功后，拦截本次点击，防止触发新武器的开火
            }
        }

        // ==================== 正常开火/挥砍逻辑 ====================
        if (canShoot && Input.GetMouseButtonDown(0))
        {
            if (weapon != null)
            {
                // 彻底解决捡到刀依然开枪的 Bug：在此处进行绝对严格的类型分流判定！
                if (weapon.weaponType == WeaponType.Katana)
                {
                    // 确保在时间静止时，挥刀能短暂激活时间流逝（带来肉感）
                    StopCoroutine(ActionE(.03f));
                    StartCoroutine(ActionE(.03f));
                    weapon.PerformMeleeAttack();
                }
                else if (weapon.weaponType == WeaponType.Pistol)
                {
                    StopCoroutine(ActionE(.03f));
                    StartCoroutine(ActionE(.03f));
                    weapon.Shoot(SpawnPos(), Camera.main.transform.rotation, false);
                }
            }
        }

        // ==================== 投掷武器逻辑 ====================
        if (Input.GetMouseButtonDown(1))
        {
            if (weapon != null)
            {
                StopCoroutine(ActionE(.4f));
                StartCoroutine(ActionE(.4f));

                // 先执行投掷物理冲力
                weapon.Throw();
                // 彻底解除引用
                weapon = null;
            }
        }

        // ==================== 本游戏核心时间缩放机制 ====================
        // 核心时间流速平滑拟合状态机算法片段
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        // 行为状态分流判定：若存在位移输入则目标为常速，否则收敛至3%的极慢速
        float time = (x != 0 || y != 0) ? 1f : .03f;
        float lerpTime = (x != 0 || y != 0) ? .05f : .5f;

        // 攻击短时序状态锁覆盖
        time = action ? 1 : time;
        lerpTime = action ? .1f : lerpTime;

        // 跨越时间轴的平滑插值计算
        Time.timeScale = Mathf.Lerp(Time.timeScale, time, lerpTime);
    }

    IEnumerator ActionE(float time)
    {
        action = true;
        yield return new WaitForSecondsRealtime(.06f);
        action = false;
    }

    public void ReloadUI(float time)
    {
        if (indicator == null) return;
        indicator.transform.DOComplete();
        indicator.transform.DORotate(new Vector3(0, 0, 90), time, RotateMode.LocalAxisAdd).SetEase(Ease.Linear).OnComplete(() => indicator.transform.DOPunchScale(new Vector3(.2f, .2f, .2f), .1f, 10, 1).SetUpdate(true));
    }

    public Vector3 SpawnPos()
    {
        return bulletSpawner != null ? bulletSpawner.position : transform.position;
    }

    // ==================== 玩家伤害与死亡状态控制 ====================
    /// <summary>
    /// 供外部子弹或近战射线调用的唯一受击入口
    /// </summary>
    public void TakeDamage()
    {
        // 如果开启了无敌后门，或者已经处于死亡转场中，直接安全拦截
        if (isGodMode || isPlayerDead) return;

        StartCoroutine(PlayerDeathRoutine());
    }

    private IEnumerator PlayerDeathRoutine()
    {
        isPlayerDead = true;
        canShoot = false; // 剥夺武器开火与控制权

        // 动态兜底：如果忘记在 Inspector 中挂载 fadeImage，则尝试在当前场景中通过名字寻找
        if (fadeImage == null)
        {
            GameObject go = GameObject.Find("FadeImage");
            if (go != null) fadeImage = go.GetComponent<Image>();
        }

        if (fadeImage != null)
        {
            fadeImage.DOComplete();
            // 核心细节：添加 .SetUpdate(true)，保证在游戏被强制减速时，黑屏依然按现实时间的 1.2 秒平滑完成
            fadeImage.DOFade(1f, 1.2f).SetUpdate(true);
        }

        // 使用不受 Time.timeScale 影响的真实物理时间等待变暗演播
        yield return new WaitForSecondsRealtime(1.5f);

        // 重新加载当前激活的关卡场景
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }
}