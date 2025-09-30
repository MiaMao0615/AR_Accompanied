using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ConnectionPCTest : MonoBehaviour
{
    [Header("UI Components")]
    public UIManager uiManager;           // 用于显示识别结果
    public Button voiceButton;            // 按住说话按钮
    public MicrophoneCapture micCapture;  // 麦克风采集
    public string wsUrl = "ws://127.0.0.1:3000";

    private WebSocket websocket;
    private bool isRecording = false;

    // 消息队列，用于跨线程更新UI
    private Queue<string> messageQueue = new Queue<string>();

    private void Awake()
    {
        if (voiceButton == null)
        {
            Debug.LogError("voiceButton not assigned in Inspector!");
        }
        else
        {
            SetupVoiceButton();
        }
    }

    async void Start()
    {
        websocket = new WebSocket(wsUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket connected");
        };

        websocket.OnMessage += (bytes) =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            lock (messageQueue)
            {
                messageQueue.Enqueue(msg);
            }
        };

        websocket.OnError += (err) =>
        {
            Debug.LogError("WebSocket Error: " + err);
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed with code: " + code);
        };

        await websocket.Connect();
    }

    private void SetupVoiceButton()
    {
        EventTrigger trigger = voiceButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = voiceButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        // 按下开始录音
        EventTrigger.Entry entryDown = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        entryDown.callback.AddListener((data) => StartRecording());
        trigger.triggers.Add(entryDown);

        // 松开停止录音
        EventTrigger.Entry entryUp = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        entryUp.callback.AddListener((data) => StopRecording());
        trigger.triggers.Add(entryUp);
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
            websocket.DispatchMessageQueue();
#endif

        // 主线程安全更新 UI
        lock (messageQueue)
        {
            while (messageQueue.Count > 0)
            {
                string msg = messageQueue.Dequeue();
                if (uiManager != null)
                    uiManager.UpdateInputField(msg);
            }
        }
    }

    private void StartRecording()
    {
        if (isRecording) return;

        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not connected!");
            return;
        }

        Debug.Log("Start recording...");
        isRecording = true;
        StartCoroutine(SendMicAudio());
    }

    private void StopRecording()
    {
        if (!isRecording) return;

        Debug.Log("Stop recording.");
        isRecording = false;
    }

    private IEnumerator SendMicAudio()
    {
        while (isRecording)
        {
            float[] samples = micCapture.GetNewSamples();
            if (samples != null && samples.Length > 0 && websocket.State == WebSocketState.Open)
            {
                byte[] bytes = MicrophoneCapture.FloatArrayToByteArray(samples);
                websocket.Send(bytes);
                Debug.Log($"Sent {bytes.Length} bytes to WebSocket");
            }
            yield return new WaitForSeconds(0.05f); // 每50ms发送一次
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }

    // 公开获取 WebSocket
    public WebSocket GetWebSocket()
    {
        return websocket;
    }
}
