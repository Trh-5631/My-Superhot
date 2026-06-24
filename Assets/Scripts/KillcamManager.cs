using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillcamManager : MonoBehaviour
{
    public static KillcamManager instance;

    [Header("时间系统配置")]
    [Tooltip("回放特写持续的真实现实时间（秒）")]
    public float playbackDuration = 3.0f;
    [Tooltip("回放期间强制的固定时间缩放值（极慢动作，推荐 0.02 - 0.05）")]
    public float killcamTimeScale = 0.05f;

    [Header("🎬 电影级运镜参数可调面板")]
    [Tooltip("回放相机距离敌人身体中心的水平距离（观察半径）")]
    public float cameraDistance = 2.5f;
    [Tooltip("回放相机高出敌人头顶/身体的高度偏移")]
    public float cameraHeightOffset = 1.5f;
    [Tooltip("回放相机围绕敌人旋转的速度（度/秒）")]
    public float rotationSpeed = 25.0f;
    [Tooltip("镜头看向敌人身体的垂直偏移（例如看向胸口还是脚底，1.0代表看向胸口左右）")]
    public float lookAtHeightOffset = 1.0f;

    // ==================== ✨【电影级低通滤波器参数】====================
    [Header("🎵 黑客帝国式闷音特效配置")]
    [Tooltip("回放期间的低通截止频率。数值越低听起来越闷（推荐 700 - 1200）")]
    public float killcamAudioCutoff = 900f;
    // ===================================================================

    private Camera mainCamera;
    private Camera clonedKillcamCamera; // 动态克隆出的回放相机
    private bool isPlayingKillcam = false;

    void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 核心入口：触发倒数第二个敌人的击杀慢动作特写
    /// </summary>
    public void TriggerKillcam(Transform targetEnemy)
    {
        if (isPlayingKillcam || targetEnemy == null) return;

        mainCamera = Camera.main;
        if (mainCamera == null) return;

        StartCoroutine(KillcamRoutine(targetEnemy));
    }

    private IEnumerator KillcamRoutine(Transform enemyTransform)
    {
        isPlayingKillcam = true;

        // 1. 完美兼容 SUPERHOT：彻底剥夺时间控制权，冻结玩家输入
        if (SuperHotScript.instance != null)
        {
            SuperHotScript.instance.enabled = false;
        }

        // 强行锁死电影级极慢速
        Time.timeScale = killcamTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 2. 完美动态克隆主相机
        GameObject clonedObj = Instantiate(mainCamera.gameObject, mainCamera.transform.position, mainCamera.transform.rotation);
        clonedObj.name = "Dynamic_KillcamCamera";
        clonedKillcamCamera = clonedObj.GetComponent<Camera>();

        // ==================== 【绝杀 AI 目标劫持防御线】====================
        // 强行把克隆出来的相机 Tag 改为 Untagged（未标记）！
        clonedObj.tag = "Untagged";
        // ===================================================================

        // 移除克隆体身上残留的玩家控制脚本等（保留音频监听器）
        Component[] allComponents = clonedObj.GetComponents<Component>();
        foreach (var comp in allComponents)
        {
            if (comp is Transform || comp is Camera || comp.GetType().Name.Contains("Camera"))
                continue;

            // 剔除 AudioListener，允许克隆相机保留听觉
            if (comp.GetType().Name.Contains("Look") || comp.GetType().Name.Contains("Controller"))
            {
                Destroy(comp);
            }
        }

        // 3. 配置相机初始状态
        clonedKillcamCamera.gameObject.SetActive(true);

        // 如果主相机的克隆体由于某些特殊原因没有携带 AudioListener，在此处兜底补上
        if (clonedObj.GetComponent<AudioListener>() == null)
        {
            clonedObj.AddComponent<AudioListener>();
        }

        // ==================== ✨【新增：黑客帝国闷音特效线】====================
        // 动态给回放相机挂载低通滤波器组件
        AudioLowPassFilter lowPass = clonedObj.GetComponent<AudioLowPassFilter>();
        if (lowPass == null)
        {
            lowPass = clonedObj.AddComponent<AudioLowPassFilter>();
        }
        // 将截止频率设定为低频，高频的清脆声被砍掉，瞬间产生极其压抑、厚重的潜水/时空凝固闷音感
        lowPass.cutoffFrequency = killcamAudioCutoff;
        // =======================================================================

        // 【自适应动态对齐】：利用面板配置的 cameraDistance 和 cameraHeightOffset 计算初始坐标
        Vector3 initialDirection = -enemyTransform.forward;
        if (initialDirection == Vector3.zero) initialDirection = Vector3.back; // 容错处理

        clonedKillcamCamera.transform.position = enemyTransform.position + (initialDirection * cameraDistance) + (Vector3.up * cameraHeightOffset);
        clonedKillcamCamera.transform.LookAt(enemyTransform.position + Vector3.up * lookAtHeightOffset);

        // 关闭主相机渲染（主相机的正常耳朵关闭，克隆相机带滤波器的耳朵接管）
        mainCamera.gameObject.SetActive(false);

        // 4. 电影级运镜：围绕碎裂的肢体作精准轨道旋转
        float elapsed = 0f;
        while (elapsed < playbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (clonedKillcamCamera != null && enemyTransform != null)
            {
                // 【参数映射】：通过面板上的 rotationSpeed 来控制相机的环绕速度
                clonedKillcamCamera.transform.RotateAround(enemyTransform.position, Vector3.up, rotationSpeed * Time.unscaledDeltaTime);

                // 确保相机始终死死盯着敌人指定的身体高度
                clonedKillcamCamera.transform.LookAt(enemyTransform.position + Vector3.up * lookAtHeightOffset);
            }
            yield return null;
        }

        // 5. 回放结束，归还主相机与系统流速控制权
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);

        // 彻底销毁克隆相机（随之销毁其身上的低通滤波器和临时耳朵，音效瞬间恢复清脆高昂）
        if (clonedObj != null) Destroy(clonedObj);

        // 重新恢复 SuperHot 的机制
        if (SuperHotScript.instance != null)
        {
            SuperHotScript.instance.enabled = true;
        }

        // 恢复默认的 fixedDeltaTime 标准值
        Time.timeScale = 0.03f;
        Time.fixedDeltaTime = 0.02f;

        isPlayingKillcam = false;
    }
}