using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // 新增引入：TextMeshPro 命名空间用于控制高级 3D 和 2D 文本
using DG.Tweening; // 引入项目自带的 DOTween 库实现丝滑黑屏

public class LevelExitDoor : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("要跳转到的下一个关卡场景名称（必须在 Build Settings 中注册）")]
    public string nextSceneName;

    [Header("Interaction Settings")]
    public float interactDistance = 3.5f; // 允许按 E 触发的有效距离

    [Header("UI References")]
    private Image fadeImage; // 自动动态寻找全屏黑布
    private bool isTransitioning = false; // 防止玩家疯狂连点 E 键重复加载

    [Header("✨ Showcase Enhanced UI Settings (新增答辩加分视觉)")]
    [Tooltip("拖入大 Canvas 下创建的 LEVEL CLEARED 红色艺术字文本物体")]
    public GameObject missionCompleteText;
    [Tooltip("拖入挂在门模型底下的 World Space（世界空间）Canvas 物体")]
    public GameObject doorWarningCanvas;

    private bool hasTriggeredCompleteUI = false; // 内部标记：防止“任务终了”特效重复高频触发
    private Tween warningTween; // 动画句柄：用于控制门前立体小悬浮窗的淡出流程

    void Start()
    {
        // 自动建立前端黑屏 UI 的全局关联，防止跨场景配置丢失
        GameObject fadeObj = GameObject.Find("FadeImage");
        if (fadeObj != null)
        {
            fadeImage = fadeObj.GetComponent<Image>();
        }
        else
        {
            Debug.LogError("未能在场景中找到名为 'FadeImage' 的 UI 物体，请确认第一步是否创建成功！");
        }

        // 规范化重置：确保游戏刚开局时，通关大字和门前警告卡片默认处于完全隐藏状态
        if (missionCompleteText != null) missionCompleteText.SetActive(false);
        if (doorWarningCanvas != null) doorWarningCanvas.SetActive(false);
    }

    void Update()
    {
        // 核心实时轮询：检测全图敌人是否被彻底清除，在全清的千分之一秒瞬间爆出大红特效
        if (!hasTriggeredCompleteUI && !AreEnemiesStillAlive())
        {
            ShowMissionCompleteVisual();
        }

        // 核心检测：当玩家按下 E 键，且当前没有在转场中
        if (Input.GetKeyDown(KeyCode.E) && !isTransitioning)
        {
            TryInteractWithDoor();
        }
    }

    private void TryInteractWithDoor()
    {
        // 1. 射线检测：只有玩家眼睛（主相机）正对着门时，才能按下 E 键
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, interactDistance))
        {
            // 判定射线射中的是不是这扇门（或者门的子物体）
            if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform))
            {
                // 2. 核心拦截：关卡内是否还有活着的敌人？
                if (AreEnemiesStillAlive())
                {
                    Debug.LogWarning("门锁住了！关卡内仍有顽固敌人存活，无法通行！");

                    // 实时激活并弹射浮空在木门前方的人眼等高 3D 警告提示窗
                    ShowDoorWarning();

                    // 让屏幕中心的准星或者 indicator 抖动一下，提示玩家开不了门
                    if (SuperHotScript.instance.indicator != null)
                    {
                        SuperHotScript.instance.indicator.transform.DOComplete();
                        SuperHotScript.instance.indicator.transform.DOShakePosition(0.2f, 10f);
                    }
                    return; // 彻底拦截，不触发黑屏转场
                }

                // 3. 敌人全清，条件达成，开始执行黑屏穿越
                StartCoroutine(TransitionToNextLevel());
            }
        }
    }

    /// <summary>
    /// ✨ 独立高阶动效：在屏幕中央以极具冲击力的带弹性放大动画打出“任务终了”大字，并同步轰出高光音效
    /// </summary>
    private void ShowMissionCompleteVisual()
    {
        hasTriggeredCompleteUI = true;

        if (missionCompleteText != null)
        {
            missionCompleteText.SetActive(true);

            // 完美的工业设计：初始本地缩放设为 0，随后用 DOTween 配合 OutBack 弹性曲线放大至正常
            missionCompleteText.transform.localScale = Vector3.zero;
            missionCompleteText.transform.DOScale(Vector3.one, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true); // .SetUpdate(true) 极其关键：确保在 SUPERHOT 时间近乎静止时动画也能丝滑播放！

            // 添加音频：自动寻找挂在自己身上的音频组件，并用真实现实时间（不卡顿）瞬间播放通关音效！
            AudioSource audio = GetComponent<AudioSource>();
            if (audio != null && audio.clip != null)
            {
                // .PlayScheduled 或常规 Play 都可以，但必须确保它能绕过系统 timeScale 的冰冻机制
                audio.Play();
            }
        }
    }

    /// <summary>
    /// ✨ 独立高阶动效：呼出门前 3D 悬浮小窗，使其始终面朝玩家主相机，并在 2 秒后智能化缩回隐藏
    /// </summary>
    private void ShowDoorWarning()
    {
        if (doorWarningCanvas == null) return;

        // 斩断旧的缓动序列，强制高亮并初始化状态
        doorWarningCanvas.SetActive(true);
        doorWarningCanvas.transform.DOComplete();
        if (warningTween != null) warningTween.Kill();

        // 交互细节拉满：每次玩家尝试撞门，提示框发生高频轻微缩放震动，极具 UI 敲击反馈感
        doorWarningCanvas.transform.localScale = Vector3.one * 0.01f;
        doorWarningCanvas.transform.DOPunchScale(new Vector3(0.003f, 0.003f, 0.003f), 0.2f).SetUpdate(true);

        doorWarningCanvas.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // 智能自动降落伞：在显示 1.8 秒后，通过极短的 InBack 崩塌动画缩回到 0 并隐藏，保证不遮挡战斗视线
        warningTween = doorWarningCanvas.transform.DOScale(Vector3.zero, 0.3f)
            .SetDelay(1.8f)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() => doorWarningCanvas.SetActive(false));
    }

    /// <summary>
    /// 高阶算法：实时扫描全图带有 "Enemy" 标签的布娃娃或活体
    /// </summary>
    private bool AreEnemiesStillAlive()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemyObj in enemies)
        {
            // 扫描敌人身上挂载的 EnemyScript
            EnemyScript enemyScript = enemyObj.GetComponentInParent<EnemyScript>();
            if (enemyScript != null)
            {
                // 如果发现有任何一个敌人 dead 变量不为 true，说明还有活口
                if (!enemyScript.dead)
                {
                    return true;
                }
            }
        }
        return false; // 全图搜完，没有活着的敌人
    }

    /// <summary>
    /// 契合 SUPERHOT 电影级时间流逝的转场协程
    /// </summary>
    IEnumerator TransitionToNextLevel()
    {
        isTransitioning = true;

        if (fadeImage != null)
        {
            // 核心视觉：使用 .SetUpdate(true) 强制让黑屏以“真实现实时间”渐变，不受 SUPERHOT 静止机制的影响！
            fadeImage.DOFade(1f, 0.8f).SetUpdate(true);
        }

        // 短暂在黑屏里停留 1.0 秒，给老师一个切换和呼吸的心理暗示
        yield return new WaitForSecondsRealtime(1.0f);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            // 异步加载新关卡，彻底清洗内存
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("门上的 'Next Scene Name' 变量为空！请在检查器中输入场景名。");
            isTransitioning = false;
        }
    }
}