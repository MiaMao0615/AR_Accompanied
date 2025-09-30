using UnityEngine;
using TMPro;
using UnityEngine.UI;
using NativeWebSocket;
using System.Collections;
using System.Text;

public class MoonshotPCClient : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_InputField inputField;   // 用户输入
    public TMP_Text answerText;         // 模型回答显示
    public Button sendButton;           // 发送按钮

    [Header("Time & State Provider")]
    public TimeStateController timeController; // 获取当前时间
    public TongTongManager tongTongManager;    // 获取当前状态信息

    [Header("WebSocket Settings")]
    public string wsUrl = "ws://127.0.0.1:4000"; // PC 端 LLM 服务地址
    private WebSocket ws;

    // ================== 状态字段 ==================
    private string hiddenMessage = "";       // TimeController 设置的隐藏消息
    private string lastAnswer = "";          // 保存最后一次回答
    private string pendingAnswer = null;     // 待显示消息，主线程更新

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
        StartCoroutine(ConnectWebSocketRoutine());
    }

    private IEnumerator ConnectWebSocketRoutine()
    {
        ws = new WebSocket(wsUrl);

        ws.OnOpen += () =>
        {
            Debug.Log("Connected to LLM WebSocket");
        };

        ws.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            Debug.Log("Received from LLM: " + msg);
            pendingAnswer = msg; // ⭐ 后台线程接收，主线程显示
        };

        ws.OnError += (err) =>
        {
            Debug.LogError("WebSocket Error: " + err);
        };

        ws.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed with code: " + code);
        };

        yield return ws.Connect();
    }

    private void OnSendClicked()
    {
        if (ws == null || ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not connected!");
            return;
        }

        // 生成时间前缀
        string timePrefix = GetCurrentTimeString();

        // 获取当前状态名
        string stateName = tongTongManager != null ? tongTongManager.GetCurrentStateName() : "";

        // 拼接消息：时间 + 状态 + 用户输入
        string userInput = inputField.text.Trim();
        string fullMsg = string.IsNullOrEmpty(userInput) ? $"{timePrefix} {stateName}"
                                                         : $"{timePrefix} {stateName} {userInput}";

        if (!string.IsNullOrEmpty(fullMsg))
        {
            SendMessageToServer(fullMsg);
            inputField.text = "";
        }

        // 发送隐藏消息（TimeController 状态）
        if (!string.IsNullOrEmpty(hiddenMessage))
        {
            SendMessageToServer(hiddenMessage);
            hiddenMessage = "";
        }

        // 保持上一次的回答显示在 answerText
        if (!string.IsNullOrEmpty(lastAnswer) && answerText != null)
        {
            answerText.text = lastAnswer;
        }
    }

    private string GetCurrentTimeString()
    {
        if (timeController == null) return "00:00";

        float t = timeController.debugTime;
        int hour = Mathf.FloorToInt(t);
        int minute = Mathf.FloorToInt((t - hour) * 60f);

        return $"{hour:D2}:{minute:D2}";
    }

    private void SendMessageToServer(string message)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            ws.Send(bytes);
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null)
            ws.DispatchMessageQueue();
#endif

        // ⭐ 主线程更新 answerText，保证 UI 显示
        if (!string.IsNullOrEmpty(pendingAnswer))
        {
            lastAnswer = pendingAnswer;
            if (answerText != null)
                answerText.text = pendingAnswer;

            pendingAnswer = null;
        }
    }

    private void OnApplicationQuit()
    {
        if (ws != null)
            StartCoroutine(CloseWebSocketRoutine());
    }

    private IEnumerator CloseWebSocketRoutine()
    {
        yield return ws.Close();
    }

    // ================== 新增接口 ==================
    /// <summary>
    /// 设置隐藏消息，不显示在 InputField，上一次点击 Send 时发送
    /// </summary>
    public void SetHiddenInputAndSend(string message)
    {
        hiddenMessage = message;
    }
}
