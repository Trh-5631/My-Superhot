using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // 引入 DOTween 用于震动准星反馈

public class CatEasterEgg : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("允许玩家按 E 摸猫的有效距离")]
    public float interactDistance = 3.0f;

    [Header("✨ 彩蛋对话定制")]
    [Tooltip("看板娘说话人的名字")]
    public string speakerName = "奈珂";
    [Tooltip("按 E 互动时看板娘弹出的吐槽台词")]
    [TextArea(2, 5)]
    public string catDialogueContent = "（信号杂音）...等等，你居然找到了一只猫？这不符合安全条例...好吧，顺便替我摸摸它。";
    [Tooltip("触发该对话时看板娘的表情（建议选择严肃或警告表情）")]
    public DialogueManager.PortraitExpression dialogueExpression = DialogueManager.PortraitExpression.Warning;

    void Update()
    {
        // 实时轮询：当玩家按下 E 键，且当前看板娘没有在播放别的关键剧情
        if (Input.GetKeyDown(KeyCode.E))
        {
            // 防呆拦截：如果此时看板娘正在说重要的主线台词，先不让摸猫触发打断
            if (DialogueManager.instance != null && DialogueManager.instance.IsDialogueActive())
            {
                return;
            }

            TryInteractWithCat();
        }
    }

    private void TryInteractWithCat()
    {
        // 证明按键响应正常
        Debug.Log("<color=orange>【彩蛋测试】</color> 玩家按下了 E 键，开始发射射线...");
        // 1. 射线检测：完美的工业标准，只有玩家眼睛（主相机）正对着猫时才能触发
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, interactDistance))
        {
            Debug.Log($"<color=orange>【彩蛋测试】</color> 射线射中了物体: {hit.transform.name}");
            // 判定射线射中的是不是这只小猫（或者小猫模型的子层级物体）
            if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform))
            {
                // 2. 核心联动：直接把定制好的台词和表情打包塞给看板娘管理器
                if (DialogueManager.instance != null)
                {
                    DialogueManager.instance.ShowDialogue(speakerName, catDialogueContent, dialogueExpression);
                }

                // 3. 细节拉满：让屏幕中心的准星轻微抖动一下，给玩家极强的“点击交互成功”的手感反馈
                if (SuperHotScript.instance != null && SuperHotScript.instance.indicator != null)
                {
                    SuperHotScript.instance.indicator.transform.DOComplete();
                    // 轻微抖动，不晃动镜头，只作为 UI 确认
                    SuperHotScript.instance.indicator.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.15f).SetUpdate(true);
                }

                Debug.Log("<color=green>【彩蛋系统】</color> 玩家成功触发了摸猫彩蛋！");
            }
        }
    }
}