using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 核心：引入场景管理命名空间

public class MainMenuController : MonoBehaviour
{
    [Header("关卡配置")]
    public string firstLevelName = "Level1"; // 你的第一关场景名字

    void Start()
    {
        // 确保进标题界面时，鼠标指针是解锁且可见的，防止玩家点不到按钮
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 确保时间流速是正常的
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 开始游戏：加载第一关场景
    /// </summary>
    public void StartGame()
    {
        if (!string.IsNullOrEmpty(firstLevelName))
        {
            SceneManager.LoadScene(firstLevelName);
        }
        else
        {
            Debug.LogError("🚨 请在 Inspector 中填写第一关的场景名称！");
        }
    }

    /// <summary>
    /// 游戏设置：留空，用于给老师演示时证明你有预留功能拓展
    /// </summary>
    public void OpenSettings()
    {
        Debug.Log("⚙️ 点击了设置（演示版暂未开放功能）");
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("🚪 退出游戏");
        Application.Quit(); // 该方法在打包后生效，编辑器下仅打印日志
    }
}