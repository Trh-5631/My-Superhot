using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("关卡配置")]
    public string firstLevelName = "Level1";

    [Header("✨ 新增：通关感谢弹窗 UI 配置")]
    [Tooltip("将场景 Canvas 下做好的感谢下游玩弹窗（Panel/GameObject）拖到这里")]
    public GameObject demoThanksPopup;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // 🚨【核心时序拦截】：检查是否是走完关卡返回的
        CheckAndHandleDemoCompletion();
    }

    /// <summary>
    /// 检测跨场景全局状态，判定是否需要弹出感谢面板
    /// </summary>
    private void CheckAndHandleDemoCompletion()
    {
        if (GlobalBGMManager.Instance != null && GlobalBGMManager.Instance.isDemoCompletedViaPortal)
        {
            // 1. 如果绑定了弹窗物体，则将其显示
            if (demoThanksPopup != null)
            {
                demoThanksPopup.SetActive(true);
            }
            else
            {
                Debug.LogWarning("⚠️ 发现了通关标记，但 MainMenuController 未绑定 demoThanksPopup 弹窗物体。");
            }

            // 2. 【核心安全锁】：阅后即焚。立刻重置为 false！
            // 确保玩家关闭弹窗后再次点击菜单或者下次重启游戏时，绝对不会因为脏数据再次弹出！
            GlobalBGMManager.Instance.isDemoCompletedViaPortal = false;
        }
        else
        {
            // 首次进入或未通关状态，确保弹窗默认是关闭的
            if (demoThanksPopup != null) demoThanksPopup.SetActive(false);
        }
    }

    /// <summary>
    /// ✨ 新增：给弹窗上的“确定/关闭”按钮绑定的公共点击事件
    /// </summary>
    public void CloseThanksPopup()
    {
        if (demoThanksPopup != null)
        {
            demoThanksPopup.SetActive(false);
        }
    }

    /// <summary>
    /// 开始游戏：触发音乐完美淡变，并加载第一关
    /// </summary>
    public void StartGame()
    {
        if (!string.IsNullOrEmpty(firstLevelName))
        {
            // 【核心调用】：通知全局音频管理器，开始执行主界面淡出、战斗BGM淡入
            if (GlobalBGMManager.Instance != null)
            {
                GlobalBGMManager.Instance.TriggerMenuToBattleFade();
            }

            SceneManager.LoadScene(firstLevelName);
        }
        else
        {
            Debug.LogError("🚨 请在 Inspector 中填写第一关的场景名称！");
        }
    }

    public void OpenSettings()
    {
        Debug.Log("⚙️ 点击了设置（演示版暂未开放功能）");
    }

    public void QuitGame()
    {
        Debug.Log("🚪 退出游戏");
        Application.Quit();
    }
}