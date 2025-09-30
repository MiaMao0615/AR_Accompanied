using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TMP_Text transcriptText;        // 显示转录文本
    public TMP_InputField chatInputField;  // 新增：对应 Chatbot 的输入框

    public void SetText(string text)
    {
        if (transcriptText != null)
            transcriptText.text = text;
        else
            Debug.Log($"[UI] {text}");
    }

    public void AppendLine(string text)
    {
        if (transcriptText != null)
            transcriptText.text += "\n" + text;
        else
            Debug.Log($"[UI] {text}");
    }

    // 新增：实时更新输入框
    public void UpdateInputField(string text)
    {
        if (chatInputField != null)
            chatInputField.text = text;
        else
            Debug.Log($"[UI Input] {text}");
    }
}
