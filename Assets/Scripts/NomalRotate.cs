using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlowRotate : MonoBehaviour
{
    [Header("旋转速度设置")]
    // 允许在 Inspector 里自由调节三个轴向的旋转速度
    // 因为要体现“Slow-Paced（慢节奏/时空减速）”，默认数值我们设得非常小
    public Vector3 rotateSpeed = new Vector3(5f, 10f, 3f);

    void Update()
    {
        // 使用 Time.deltaTime 确保在任何帧率下旋转都是平滑恒定的
        // 使用 transform.Rotate 在物体自身坐标系下进行旋转
        transform.Rotate(rotateSpeed * Time.deltaTime, Space.Self);
    }
}