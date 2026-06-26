using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("关卡配置")]
    public string firstLevelName = "Level1";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
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