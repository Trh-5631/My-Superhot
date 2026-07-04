using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
}