using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Threading.Tasks;

public class MoonshotDirect : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_InputField inputField;
    public Button sendButton;
    public TMP_Text responseText;

    [Header("API Settings")]
    public string apiKey = "YOUR_MOONSHOT_API_KEY";
    private string apiUrl = "https://api.moonshot.cn/v1/chat/completions";

    [Header("Request Control")]
    private bool isRequesting = false;            // 当前是否有请求正在进行
    private float requestCooldown = 2.0f;         // 冷却时间（秒）
    private float lastRequestTime = -10f;         // 上一次请求时间

    void Start()
    {
        // 清理旧监听器，避免重复注册
        sendButton.onClick.RemoveAllListeners();
        sendButton.onClick.AddListener(() => { _ = OnSendButtonClickedAsync(); });
    }

    private async Task OnSendButtonClickedAsync()
    {
        // 冷却检查
        if (isRequesting || Time.time - lastRequestTime < requestCooldown) return;

        string userInput = inputField.text.Trim();
        if (string.IsNullOrEmpty(userInput))
        {
            Debug.Log("输入不能为空");
            return;
        }

        sendButton.interactable = false;
        isRequesting = true;
        lastRequestTime = Time.time;

        await SendMessageToLLMAsync(userInput);

        inputField.text = "";
        sendButton.interactable = true;
        inputField.Select();
        inputField.ActivateInputField();
        isRequesting = false;
    }

    private async Task SendMessageToLLMAsync(string userMessage)
    {
        string jsonBody = "{\"model\": \"kimi-k2-0711-preview\", \"messages\": [" +
            "{\"role\": \"system\", \"content\": \"You are Tongtong, a 6-year-old girl from BIGai. Always speak like a child in 3 sentences.\"}," +
            "{\"role\": \"user\", \"content\": \"" + userMessage + "\"}" +
        "]}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            var operation = request.SendWebRequest();

            while (!operation.isDone) await Task.Yield();

            // 检查返回状态
            if (request.result != UnityWebRequest.Result.Success)
            {
                if ((int)request.responseCode == 429)
                {
                    responseText.text = "请求过于频繁，请稍后再试。";
                    Debug.LogWarning("API 429 Too Many Requests");
                }
                else
                {
                    responseText.text = "Error: " + request.error;
                    Debug.LogError(request.error);
                }
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                try
                {
                    var parsedJson = JsonUtility.FromJson<LLMResponseWrapper>(jsonResponse);
                    if (parsedJson.choices != null && parsedJson.choices.Length > 0)
                        responseText.text = parsedJson.choices[0].message.content;
                    else
                        responseText.text = "No response from model.";
                }
                catch
                {
                    responseText.text = "Failed to parse model response.";
                }
            }
        }
    }
}

// 用于解析 API 返回
[System.Serializable]
public class LLMResponseWrapper
{
    public Choice[] choices;
}

[System.Serializable]
public class Choice
{
    public Message message;
}                    

[System.Serializable]
public class Message
{
    public string role;
    public string content;
}
