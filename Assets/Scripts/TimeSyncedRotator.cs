using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeSyncedRotator : MonoBehaviour
{
    [Header("旋转配置")]
    [Tooltip("风扇在正常时间流速下，每秒钟旋转的角度（度/秒）")]
    public float rotationSpeed = 360f;

    [Tooltip("让风扇绕着哪个轴旋转（通常天花板风扇是绕 Y 轴或者 Z 轴，根据模型朝向调整）")]
    public Vector3 rotationAxis = Vector3.up; // Vector3.up 代表沿 Y 轴自转

    void Update()
    {
        // 核心加分算法：
        // 旋转角度 = 基础速度 * 旋转方向轴 * Time.deltaTime
        // 工业级铁律：乘以 Time.deltaTime 后，由于 SUPERHOT 会把 Time.timeScale 压到 0.03f，
        // Time.deltaTime 也会按比例瞬间缩减，风扇的转速就会自动跟着慢下来！
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
    }
}