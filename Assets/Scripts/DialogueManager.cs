using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    // 定义表情枚举，对应你本地下载的不同图片
    public enum PortraitExpression
    {
        Default,
        Warning,
        Victory,
        Cat
    }

    [Header("UI References")]
    [Tooltip("整个对话系统的总根节点（DialogueSystem）")]
    public GameObject dialogueGroup;
    [Tooltip("右上角看板娘的 Image 组件")]
    public Image portraitImage;
    [Tooltip("显示对话文本的 TextMeshPro 或者是 Text")]
    public TextMeshProUGUI dialogueText;
    [Tooltip("显示说话人名字的 TextMeshPro 或者是 Text")]
    public TextMeshProUGUI nameText;

    [Header("Expression Sprites")]
    [Tooltip("默认/平静表情的图片")]
    public Sprite spriteDefault;
    [Tooltip("预警/严肃表情的图片")]
    public Sprite spriteWarning;
    [Tooltip("胜利/放松表情的图片")]
    public Sprite spriteVictory;
    [Tooltip("🐱 触发猫咪彩蛋时的专属表情图片")]
    public Sprite spriteCat; // ✨ 新增：猫咪表情贴图槽位

    [Header("Animation Settings")]
    [Tooltip("看板娘滑行入场/出场需要的时间（秒）")]
    public float animationDuration = 0.3f;
    [Tooltip("立绘在屏幕内正常显示时的 Anchored Position X")]
    public float inScreenX = -150f;
    [Tooltip("立绘隐藏在屏幕右侧外部时的 Anchored Position X (根据图片宽度微调)")]
    public float outScreenX = 300f;

    [Header("Typing Settings")]
    public float typeSpeed = 0.05f;

    private RectTransform portraitRect;
    private Coroutine currentDialogueCoroutine;
    private bool isDialogueActive = false;

    void Awake()
    {
        instance = this;
        if (dialogueGroup != null) dialogueGroup.SetActive(false);

        if (portraitImage != null)
        {
            portraitRect = portraitImage.GetComponent<RectTransform>();
            // 初始状态让立绘归位到屏幕右侧外
            Vector3 pos = portraitRect.anchoredPosition;
            pos.x = outScreenX;
            portraitRect.anchoredPosition = pos;
        }
    }

    void Start()
    {

    }

    /// <summary>
    /// 支持表情切换的公开对话调用接口
    /// </summary>
    public void ShowDialogue(string speakerName, string content, PortraitExpression expression)
    {
        if (dialogueGroup == null) return;

        dialogueGroup.SetActive(true);
        isDialogueActive = true;

        if (nameText != null) nameText.text = speakerName;

        // 根据传入的枚举值切换对应的立绘图片
        SetPortraitExpression(expression);

        if (currentDialogueCoroutine != null)
        {
            StopCoroutine(currentDialogueCoroutine);
        }

        currentDialogueCoroutine = StartCoroutine(DialogueSequenceRoutine(content));
    }

    /// <summary>
    /// 内部方法：切换图片组件的 Sprite
    /// </summary>
    private void SetPortraitExpression(PortraitExpression expression)
    {
        if (portraitImage == null) return;

        switch (expression)
        {
            case PortraitExpression.Default:
                if (spriteDefault != null) portraitImage.sprite = spriteDefault;
                break;
            case PortraitExpression.Warning:
                if (spriteWarning != null) portraitImage.sprite = spriteWarning;
                break;
            case PortraitExpression.Victory:
                if (spriteVictory != null) portraitImage.sprite = spriteVictory;
                break;
            case PortraitExpression.Cat: // ✨ 新增：当传入 Cat 枚举时，切换为猫咪专用贴图
                if (spriteCat != null) portraitImage.sprite = spriteCat;
                break;
        }
    }

    /// <summary>
    /// 核心序列协程：控制滑入 -> 打字 -> 等待 -> 滑出
    /// </summary>
    IEnumerator DialogueSequenceRoutine(string line)
    {
        // 1. 看板娘滑入场 (使用无视时间缩放的算法)
        yield return StartCoroutine(MovePortraitRoutine(inScreenX));

        // 2. 打字机文字生成
        dialogueText.text = "";
        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        // 3. 停留阅读时间（4秒）
        yield return new WaitForSecondsRealtime(4.0f);

        // 4. 看板娘滑出场
        yield return StartCoroutine(MovePortraitRoutine(outScreenX));

        // 5. 隐藏整个 UI
        EndDialogue();
    }

    /// <summary>
    /// 线性/平滑插值移动立绘的独立协程
    /// </summary>
    IEnumerator MovePortraitRoutine(float targetX)
    {
        if (portraitRect == null) yield break;

        float elapsed = 0f;
        float startX = portraitRect.anchoredPosition.x;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime; // 必须使用 unscaled，确保不受慢动作影响
            float t = Mathf.Clamp01(elapsed / animationDuration);

            // 使用平滑插值(SmoothStep)让滑动有减速缓冲效果，显得更像自然弹出
            t = t * t * (3f - 2f * t);

            Vector2 pos = portraitRect.anchoredPosition;
            pos.x = Mathf.Lerp(startX, targetX, t);
            portraitRect.anchoredPosition = pos;

            yield return null;
        }

        // 最终精准对齐目标点
        Vector2 finalPos = portraitRect.anchoredPosition;
        finalPos.x = targetX;
        portraitRect.anchoredPosition = finalPos;
    }

    public void EndDialogue()
    {
        isDialogueActive = false;
        if (dialogueGroup != null)
        {
            dialogueGroup.SetActive(false);
        }
    }

    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }
}