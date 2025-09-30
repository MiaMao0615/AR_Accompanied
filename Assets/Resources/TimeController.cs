using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeStateController : MonoBehaviour
{
    [Header("引用")]
    public TongTongManager tongTongManager;
    public TimeUIManager timeUIManager;
    public TimeIdTable timeIdTable;
    public MoonshotPCClient moonClient;

    [Header("时间（0~24 小时）")]
    [Range(0f, 24f)] public float debugTime = 9f;

    [Header("UI 控制")]
    public Slider timeSlider;
    public TMP_Text timeLabel;
    public TMP_Text statusLabel;

    private string currentTimeId = string.Empty;
    public string GetTimeId() => currentTimeId;

    private void Start()
    {
        if (timeSlider)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 24f;
            timeSlider.value = debugTime;
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        currentTimeId = MapTimeToId(debugTime);
        CheckAndUpdateStudyMessage();
        SendStateToMoon();
        TriggerMatch();
        UpdateStatusLabel();
    }

    private void Update()
    {
        CheckAndUpdateStudyMessage();
    }

    private void OnTimeSliderChanged(float value)
    {
        debugTime = value;
        string newId = MapTimeToId(debugTime);
        bool idChanged = (newId != currentTimeId);
        currentTimeId = newId;

        UpdateStatusLabel();
        CheckAndUpdateStudyMessage();
        SendStateToMoon();

        TriggerMatch();
        
    }

    private void UpdateStatusLabel()
    {
        if (statusLabel == null) return;

        string timeStr = FormatTime(debugTime);
        string idStr = string.IsNullOrEmpty(currentTimeId) ? "-" : currentTimeId;

        statusLabel.text = $"{timeStr} - {idStr}";
    }
    private void TriggerMatch()
    {
        if (tongTongManager != null)
            tongTongManager.MatchId();
        print("triggermatch");
        print(currentTimeId);
    }

    private string MapTimeToId(float hour)
    {
        if (timeIdTable == null || timeIdTable.records == null || timeIdTable.records.Count == 0)
            return string.Empty;

        float h = Mathf.Repeat(hour, 24f);

        foreach (var r in timeIdTable.records)
        {
            if (r == null || string.IsNullOrEmpty(r.id)) continue;
            if (!TryParseHHMM(r.startTime, out float sh)) continue;
            if (!TryParseHHMM(r.endTime, out float eh)) continue;

            sh = Mathf.Repeat(sh, 24f);
            eh = Mathf.Repeat(eh, 24f);

            bool hit =
                (sh < eh && h >= sh && h < eh) ||
                (sh > eh && (h >= sh || h < eh)) ||
                Mathf.Approximately(sh, eh);
            if (hit) return r.id;
        }
        return string.Empty;
    }

    private bool TryParseHHMM(string s, out float hour)
    {
        hour = 0f;
        if (string.IsNullOrEmpty(s)) return false;

        if (s.Contains(":"))
        {
            var parts = s.Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out int h)) return false;
            if (!int.TryParse(parts[1], out int m)) return false;
            hour = h + (m / 60f);
            return true;
        }
        return float.TryParse(s, out hour);
    }

    private string FormatTime(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{h:D2}:{m:D2}";
    }

    private void SendStateToMoon()
    {
        if (moonClient == null) return;

        string msg = $"{{\"timeId\":\"{currentTimeId}\",\"time\":{debugTime:F2}}}";
        moonClient.SetHiddenInputAndSend(msg);
    }

    private void CheckAndUpdateStudyMessage()
    {
        if (timeLabel == null) return;

        int hour = Mathf.FloorToInt(debugTime);
        int minute = Mathf.FloorToInt((debugTime - hour) * 60f);

        string displayText = $"{hour:D2}:{minute:D2} Welcome to the world of Tongtong, Tongtong will grow with you";

        if ((hour >= 8 && hour < 10) || (hour >= 12 && hour < 18))
            displayText = "Tongtong is studying next door, come join her to study!";

        timeLabel.text = displayText;
    }
}
