using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;





[RequireComponent(typeof(ObserverBehaviour))]
public class PictureManager : MonoBehaviour
{
    [Serializable]
    public class SpotPairEntry
    {
        public string spotId;
        [Header("Start")]
        public GameObject startPrefab;
        [HideInInspector] public Transform startInst;
        public bool startUseCapturedLocalTRS = false;
        public Vector3 startCapturedLocalPos;
        public Vector3 startCapturedLocalEuler;
        public Vector3 startCapturedLocalScale = Vector3.one;

        [Header("Target")]
        public GameObject targetPrefab;
        [HideInInspector] public Transform targetInst;
        public bool targetUseCapturedLocalTRS = false;
        public Vector3 targetCapturedLocalPos;
        public Vector3 targetCapturedLocalEuler;
        public Vector3 targetCapturedLocalScale = Vector3.one;
    }

    [Header("多位置包（每条=一对 Start+Target）")]
    public List<SpotPairEntry> pairs = new List<SpotPairEntry>();

    [Header("初始启用的 spotId（为空则用第一条）")]
    public string defaultSpotId;

    [Header("对接")]
    public TongTongManager tongTongManager;

    [Header("调试")]
    public bool debugLogs = true;




    // 追踪丢失多长时间后才判定“真正失活”
    [Header("丢失判定")]
    [SerializeField] private float lostClearDelaySeconds = 3f;
    // 是否把 LIMITED 看作“仍在追踪”
    [SerializeField] private bool treatLimitedAsTracked = false;
    // 是否把 EXTENDED_TRACKED 看作“仍在追踪”
    [SerializeField] private bool treatExtendedAsTracked = false;

    // 内部：丢失计时协程句柄
    private Coroutine _lossCo;


    // 把 Vuforia 的 TargetStatus 统一成“我们认为是否在追踪”
    private bool IsConsideredTracked(TargetStatus status)
    {
        switch (status.Status)
        {
            case Status.TRACKED:
                return true;
            case Status.EXTENDED_TRACKED:
                return treatExtendedAsTracked;   // 默认 false → 更严格
            case Status.LIMITED:
                return treatLimitedAsTracked;    // 默认 false → 更严格
            default:
                return false; // NO_POSE / DETECTED / UNKNOWN 等都按未追踪
        }
    }



    private IEnumerator LossTimeoutRoutine()
    {
        float t = lostClearDelaySeconds > 0f ? lostClearDelaySeconds : 0f;
        if (t > 0f) yield return new WaitForSeconds(t);

        // 计时完仍未恢复追踪 → 判定失活
        if (_isTracked) yield break; // 在计时期间已经恢复了，本协程就不用做事
        _currentSpotId = "none";
        Log($"Inactive (timeout {lostClearDelaySeconds:F1}s) -> id=none");

        ActiveManagers.Remove(this);
        FindObjectOfType<TongTongManager>()?.MatchId();  // 通知回 fallback
        DespawnAllPairs();

        _lossCo = null;
    }






    private void Log(string m) { if (debugLogs) Debug.Log($"[PictureManager:{name}] {m}"); }

    // —— 对 TongTongManager 暴露的“当前 pictureId” —— //
    private string _currentSpotId = "none";
    public string GetPictureId() => _currentSpotId;

    // 只在“当前 spot 且已跟踪”时返回挂点
    public bool TryGetPairById(string id, out Transform start, out Transform target)
    {
        start = null; target = null;
        if (!_isTracked) return false;
        if (string.IsNullOrEmpty(id) || !string.Equals(id, _currentSpotId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_runtimePairs.TryGetValue(_currentSpotId, out var rt))
        {
            start = rt.start;
            target = rt.target;
            return (start && target);
        }
        return false;
    }

    // 运行时索引：spotId -> (start,target)
    private readonly Dictionary<string, (Transform start, Transform target)> _runtimePairs =
        new Dictionary<string, (Transform start, Transform target)>(StringComparer.OrdinalIgnoreCase);

    private ObserverBehaviour _observer;
    private bool _isTracked;
    private readonly List<string> _orderedSpotIds = new List<string>();


    // PictureManager.cs 顶部类内
    public static readonly HashSet<PictureManager> ActiveManagers = new HashSet<PictureManager>();

    public bool IsTracked => _isTracked;
    public string CurrentSpotId => _currentSpotId; // 当前激活的 spotId
    public bool TryGetLivePair(out Transform start, out Transform target)
    {
        // 只要当前这个 PM 正在跟踪且有有效节点，就返回
        return TryGetPairById(_currentSpotId, out start, out target);
    }




    private void Awake()
    {
        _observer = GetComponent<ObserverBehaviour>();
        _observer.OnTargetStatusChanged += OnTargetStatusChanged;

        _orderedSpotIds.Clear();
        foreach (var e in pairs)
            if (e != null && !string.IsNullOrEmpty(e.spotId) && !_orderedSpotIds.Contains(e.spotId))
                _orderedSpotIds.Add(e.spotId);
    }

    private void OnDestroy()
    {
        if (_observer != null) _observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void Start()
    {
        // 若启动即已被追踪
        OnTargetStatusChanged(_observer, _observer.TargetStatus);
    }

    private void OnTargetStatusChanged(ObserverBehaviour ob, TargetStatus status)
    {
        bool nowTracked = IsConsideredTracked(status);
        // 状态没变化就不处理（避免重复）
        if (nowTracked == _isTracked) return;

        _isTracked = nowTracked;

        if (_isTracked)
        {
            // 恢复追踪：终止“丢失计时”
            if (_lossCo != null) { StopCoroutine(_lossCo); _lossCo = null; }

            // 正常激活流程
            SpawnAllPairs();
            ReapplyAllLocalTRS();
            ActivateDefaultSpotId();

            ActiveManagers.Add(this);
            Log($"Active -> id={_currentSpotId}");

            BindThisToManager();                      // 绑定给 Manager（如果还没绑定）
            FindObjectOfType<TongTongManager>()?.MatchId();
        }
        else
        {
            // 刚变成“未追踪”：不要立刻清理，启动 3 秒计时
            Log($"Lost tracking -> wait {lostClearDelaySeconds:F1}s before clear");
            if (_lossCo != null) StopCoroutine(_lossCo);
            _lossCo = StartCoroutine(LossTimeoutRoutine());
        }
    }


    // —— 切换当前 spot（可由 UI 或逻辑调用）—— //
    public void SetActiveSpotId(string spotId)
    {
        if (!_isTracked) return;
        if (string.IsNullOrEmpty(spotId)) return;
        if (!_runtimePairs.ContainsKey(spotId)) return;

        _currentSpotId = spotId;
        Log($"SetActiveSpotId -> {_currentSpotId}");
        BindThisToManager();
        ReapplyAllLocalTRS();
        // ★ 切换时也绑定一次，确保 Manager 用当前这一个 PictureManager
        TriggerMatch();
    }

    public void CycleNextSpot()
    {
        if (!_isTracked || _orderedSpotIds.Count == 0) return;
        int idx = Mathf.Max(0, _orderedSpotIds.IndexOf(_currentSpotId));
        idx = (idx + 1) % _orderedSpotIds.Count;
        _currentSpotId = _orderedSpotIds[idx];
        Log($"CycleNextSpot -> {_currentSpotId}");
        BindThisToManager();         // ★ 同上
        TriggerMatch();
    }

    // ================= 生成 / 销毁 =================

    private void SpawnAllPairs()
    {
        _runtimePairs.Clear();
        foreach (var e in pairs)
        {
            if (e == null || string.IsNullOrEmpty(e.spotId)) continue;

            e.startInst = SpawnChild(e.startPrefab, transform, GetNodeName(e.spotId, "start"));
            e.targetInst = SpawnChild(e.targetPrefab, transform, GetNodeName(e.spotId, "target"));

            if (e.startUseCapturedLocalTRS)
                ApplyLocalTRS(e.startInst, e.startCapturedLocalPos, e.startCapturedLocalEuler, e.startCapturedLocalScale);
            if (e.targetUseCapturedLocalTRS)
                ApplyLocalTRS(e.targetInst, e.targetCapturedLocalPos, e.targetCapturedLocalEuler, e.targetCapturedLocalScale);

            _runtimePairs[e.spotId] = (e.startInst, e.targetInst);
        }
        Log($"SpawnAllPairs: {_runtimePairs.Count} pair(s)");
    }

    private void DespawnAllPairs()
    {
        foreach (var e in pairs)
        {
            if (e == null) continue;
            if (e.startInst) Destroy(e.startInst.gameObject);
            if (e.targetInst) Destroy(e.targetInst.gameObject);
            e.startInst = e.targetInst = null;
        }
        _runtimePairs.Clear();
        Log("DespawnAllPairs");
    }

    private Transform SpawnChild(GameObject prefab, Transform parent, string nameWhenEmpty)
    {
        if (prefab == null)
        {
            var go = new GameObject(nameWhenEmpty);
            return go.transform;
        }
        return Instantiate(prefab, parent, false).transform;
    }

    private static void ApplyLocalTRS(Transform t, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
    {
        if (!t) return;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(localEuler);
        t.localScale = localScale;
    }


    [ContextMenu("Reapply Local TRS For All Pairs")]
    public void ReapplyAllLocalTRS()
    {
        foreach (var e in pairs)
        {
            if (e == null) continue;

            if (e.startUseCapturedLocalTRS && e.startInst)
                ApplyLocalTRS(e.startInst.transform, e.startCapturedLocalPos, e.startCapturedLocalEuler, e.startCapturedLocalScale);

            if (e.targetUseCapturedLocalTRS && e.targetInst)
                ApplyLocalTRS(e.targetInst.transform, e.targetCapturedLocalPos, e.targetCapturedLocalEuler, e.targetCapturedLocalScale);
        }
        Debug.Log("[PictureManager] Reapplied local TRS for all pairs.");
    }

    private string GetNodeName(string spotId, string kind)
    {
        string imageName = !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : "Image";
        return $"PM_{imageName}_{spotId}/{kind}";
    }

    private void ActivateDefaultSpotId()
    {
        if (!string.IsNullOrEmpty(defaultSpotId) && _runtimePairs.ContainsKey(defaultSpotId))
            _currentSpotId = defaultSpotId;
        else
            _currentSpotId = _orderedSpotIds.Count > 0 ? _orderedSpotIds[0] : "none";
    }

    // ====== 新增的小方法：把“当前这一个 PictureManager”绑定给 Manager ======
    private void BindThisToManager()
    {
        if (!tongTongManager) tongTongManager = FindObjectOfType<TongTongManager>();
        if (tongTongManager)
        {
            tongTongManager.SetPictureManager(this);
            Log($"Bind to manager: {tongTongManager.name}, currentId={_currentSpotId}");
        }
        else
        {
            Log("Bind failed: no TongTongManager in scene");
        }
    }

    private void TriggerMatch()
    {
        if (tongTongManager != null)
            tongTongManager.MatchId();
        else
            Log("TriggerMatch skipped: tongTongManager is null");
    }
}
