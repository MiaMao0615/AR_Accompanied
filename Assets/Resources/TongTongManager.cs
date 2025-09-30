using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class TongTongManager : MonoBehaviour
{
    [Header("基础引用")]
    public GameObject tongTongPrefab;
    public Transform fallbackPosition;
    public Vector3 tongTongScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("外部管理器")]
    public PictureManager pictureManager;       // 提供 GetPictureId()、TryGetPairById(id,out start,out target)
    public TimeStateController timeController;  // 提供 GetTimeId()

    [Header("可选提示/音效")]
    public TMP_Text messageText;
    public AudioClip collisionClip;

    [Header("可选：AnimationRig 控制器（剥离到独立脚本）")]
    public TongTongAnimationRig rigController;

    [Header("Debug")]
    public bool debugLogs = true;

    // 运行时
    private GameObject currentTongTong;
    private Animator currentAnimator;
    private MotionRunner motion;

    // 上一次“匹配”快照（仅用于判断图片是否变化 & 返程启动）
    private bool   lastWasMatch        = false;
    private string lastMatch_PictureId = null;
    private string lastMatch_TimeId    = null;
    private Transform lastMatch_Start  = null;
    private Transform lastMatch_Target = null;

    // 仅用于 Moon/调试
    private string lastPlayedStateName = "";

    void Start()
    {
        SpawnAtFallback();
        SetupCollisionForTong(currentTongTong);

        // 新场景/新实例出现时，统一关闭外部 Rig
        if (rigController != null && currentTongTong != null)
        {
            rigController.OnTongTongSpawned(currentTongTong.transform);
            rigController.StopRig();
        }
    }

    // ====================== 日志/工具 ======================

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[TongTongManager] {msg}");
    }

    private void DumpIds(string pictureId, string timeId)
    {
        if (!debugLogs) return;
        Debug.Log($"[TongTongManager] pictureId={(string.IsNullOrEmpty(pictureId) ? "null/empty" : pictureId)}, timeId={(string.IsNullOrEmpty(timeId) ? "null/empty" : timeId)}");
    }

    public void SetPictureManager(PictureManager pm)
    {
        pictureManager = pm;
        Log($"Bound pictureManager = {(pm ? pm.name : "null")}");
    }

    // 选择一个活跃的 PictureManager，并解析 start/target
    private bool PickActivePictureForTimeId(string timeId, out PictureManager pickedPM, out Transform start, out Transform target)
    {
        pickedPM = null; start = target = null;

        // 1) 优先：找到活跃并且 spotId == timeId 的 PM
        foreach (var pm in PictureManager.ActiveManagers)
        {
            if (pm && pm.IsTracked && string.Equals(pm.CurrentSpotId, timeId, StringComparison.OrdinalIgnoreCase))
            {
                if (pm.TryGetLivePair(out start, out target))
                {
                    pickedPM = pm;
                    return true;
                }
            }
        }

        // 2) 其次：任选一个正在跟踪的 PM（timeId 未命中时）
        foreach (var pm in PictureManager.ActiveManagers)
        {
            if (pm && pm.IsTracked && pm.TryGetLivePair(out start, out target))
            {
                pickedPM = pm;
                return true;
            }
        }

        // 3) 都没有
        return false;
    }

    private void ClearLastMatchSnapshot()
    {
        lastMatch_PictureId = null;
        lastMatch_TimeId    = null;
        lastMatch_Start     = null;
        lastMatch_Target    = null;
    }

    private void EnsureTongSpawned()
    {
        if (currentTongTong == null) SpawnAtFallback();
    }

    // ====================== 核心：只判定，位姿交给 MotionRunner ======================

    public void MatchId()
    {
        if (timeController == null) { Log("timeController == null → 退出"); return; }

        string timeId = timeController.GetTimeId();

        PictureManager chosenPM;
        Transform liveStart, liveTarget;
        bool havePair = PickActivePictureForTimeId(timeId, out chosenPM, out liveStart, out liveTarget);

        bool nowMatch = havePair && chosenPM &&
                        string.Equals(chosenPM.CurrentSpotId, timeId, StringComparison.OrdinalIgnoreCase);

        string pictureId = (chosenPM ? chosenPM.CurrentSpotId : null);
        DumpIds(pictureId ?? "none", timeId);
        LogPair($"Resolved pair (nowMatch={nowMatch}, lastWasMatch={lastWasMatch})", liveStart, liveTarget);

        // 1) 匹配 → 匹配：不做任何操作（保持现状）
        if (lastWasMatch && nowMatch)
        {
            lastMatch_PictureId = pictureId;
            lastMatch_TimeId    = timeId;
            lastMatch_Start     = liveStart;
            lastMatch_Target    = liveTarget;
            lastWasMatch        = true;
            return;
        }

        // 2) 匹配 → 不匹配
        if (lastWasMatch && !nowMatch)
        {
            EnsureTongSpawned();

            bool pictureChanged = (lastMatch_PictureId != null) &&
                                  !string.Equals(lastMatch_PictureId, pictureId, StringComparison.OrdinalIgnoreCase);

            // 图片变了 或 快照无效 → 立即回 fallback（不用依赖 last*）
            if (pictureChanged || lastMatch_Start == null || lastMatch_Target == null)
            {
                Log("匹配→不匹配：图片改变 或 点位缺失 → 立即回 fallback");
                motion?.HardCancelAndReturnToFallback();
                StopRigIfAny("picture changed OR invalid pair");
                ClearLastMatchSnapshot();
                lastWasMatch = false;
                return;
            }

            // 仅时间改变（图片未变） → Target→Start 返程；到达后由 MotionRunner 自动回 fallback
            Log("匹配→不匹配：时间改变（图片未变）→ Target→Start，再回 fallback");

            StopRigIfAny("start reverse");

            motion?.ApplyAndRunForId(lastMatch_TimeId, lastMatch_Start, lastMatch_Target, /*forward=*/false, debugLogs);

            // 清空快照，后续若仍不匹配，下次会直接 HardCancel
            ClearLastMatchSnapshot();
            lastWasMatch = false;
            return;
        }

        // 3) 不匹配 → 匹配：对齐 Start 并播放 Start→Target（到达后跟随由 MotionRunner 负责）
        if (!lastWasMatch && nowMatch)
        {
            EnsureTongSpawned();

            if (liveStart == null || liveTarget == null)
            {
                Log("不匹配→匹配：点位缺失 → 立即回 fallback");
                motion?.HardCancelAndReturnToFallback();
                ClearLastMatchSnapshot();
                lastWasMatch = false;
                return;
            }

            Log("不匹配→匹配：对齐 Start 并播放 Start→Target");
            motion?.AlignFromFallbackToStart(liveStart);
            motion?.ApplyAndRunForId(timeId, liveStart, liveTarget, /*forward=*/true, debugLogs);

            lastMatch_PictureId = pictureId;
            lastMatch_TimeId    = timeId;
            lastMatch_Start     = liveStart;
            lastMatch_Target    = liveTarget;
            lastWasMatch        = true;
            return;
        }

        // 4) 不匹配 → 不匹配：确保通通在 fallback（幂等）
        if (!lastWasMatch && !nowMatch)
        {
            EnsureTongSpawned();
            Log("不匹配→不匹配：确保在 fallback（幂等复位）");
            StopRigIfAny("not matched -> not matched");
            GetMotion()?.HardCancelAndReturnToFallback();
            ClearLastMatchSnapshot();
            lastWasMatch = false;
            return;
        }
    }

    // ====================== 生成/组件 ======================
    private MotionRunner GetMotion()
    {
        if (currentTongTong == null) return null;
        if (!motion) motion = currentTongTong.GetComponent<MotionRunner>()
                          ?? currentTongTong.AddComponent<MotionRunner>();
        return motion;
    }
    private void SpawnAtFallback()
    {
        if (currentTongTong != null) return;

        if (!tongTongPrefab || !fallbackPosition)
        {
            Debug.LogWarning("[TongTongManager] 缺少 tongTongPrefab 或 fallbackPosition");
            return;
        }

        // 1) fallback 下实例化
        currentTongTong = Instantiate(tongTongPrefab, fallbackPosition);
        if (!currentTongTong.activeSelf) currentTongTong.SetActive(true);

        // 2) 对齐到 fallback（local 归零），并设置缩放
        currentTongTong.transform.localPosition = Vector3.zero;
        currentTongTong.transform.localRotation = Quaternion.identity;
        currentTongTong.transform.localScale    = tongTongScale;

        // 3) 组件获取/添加
        currentAnimator = currentTongTong.GetComponent<Animator>() ?? currentTongTong.AddComponent<Animator>();

        motion = currentTongTong.GetComponent<MotionRunner>() ?? currentTongTong.AddComponent<MotionRunner>();
        motion.debugLogs          = debugLogs;
        motion.followCopyRotation = true;
        motion.followCopyScale    = true;  // 跟随时同步缩放
        motion.SetFallback(fallbackPosition);

        if (rigController != null)
        {
            rigController.OnTongTongSpawned(currentTongTong.transform);
            rigController.StopRig(); // 初始权重为 0，不干扰出场姿态
        }

        // 4) 可选：碰撞消息
        if (messageText || collisionClip)
        {
            var cm = currentTongTong.GetComponent<CollisionMessage>() ?? currentTongTong.AddComponent<CollisionMessage>();
            cm.messageText  = messageText;
            cm.collisionClip = collisionClip;
        }
    }


    private void StopRigIfAny(string reason = "")
    {
        if (rigController != null)
        {
            rigController.StopRig();   // rig.weight = 0，复位 watchingTarget
            if (debugLogs) Debug.Log($"[TongTongManager] StopRigIfAny({reason})");
        }
    }

    [Header("Collision 提示")]
    public TMP_Text CollisionText;      // 可选：碰撞时显示的 UI 文本
    public AudioClip studyClip;         // 可选：另一处音效（若不用可在 Inspector 置空）
    public string collisionTag = "Collision";
    public float collisionDisplayTime = 5f;

    private void SetupCollisionForTong(GameObject tong)
    {
        if (!tong) return;

        // 1) Collider（触发器）
        var col = tong.GetComponent<Collider>();
        if (!col) col = tong.AddComponent<CapsuleCollider>();
        col.isTrigger = true;

        // 2) Rigidbody（配合触发器）
        var rb = tong.GetComponent<Rigidbody>();
        if (!rb) rb = tong.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
        rb.drag        = 100f;

        // 3) AudioSource（可选）
        var audio = tong.GetComponent<AudioSource>() ?? tong.AddComponent<AudioSource>();
        audio.playOnAwake  = false;
        audio.spatialBlend = 0f;

        // 4) CollisionMessage（核心）
        var cm = tong.GetComponent<CollisionMessage>() ?? tong.AddComponent<CollisionMessage>();
        cm.messageText   = messageText;
        cm.messageOnHit  = "通通碰到桌子了，要往后一点";
        cm.displayTime   = (collisionDisplayTime > 0f) ? collisionDisplayTime : 5f;
        cm.triggerTag    = string.IsNullOrEmpty(collisionTag) ? "Collision" : collisionTag;
        cm.collisionClip = studyClip;
        cm.tongTongManager = this;
    }

    // ====================== 其它（兼容/调试） ======================

    // 仅做转调，保持向后兼容（可删）
    private void PlayAndRunForTimeId(string timeId, Transform start, Transform end, bool forward)
    {
        if (!currentTongTong || motion == null) return;
        motion.ApplyAndRunForId(timeId, start, end, forward, debugLogs);
    }

    // 兼容 Moon 上报：可被 TimeStateController 调用
    public string GetCurrentStateName()
    {
        if (!string.IsNullOrEmpty(lastPlayedStateName)) return lastPlayedStateName;
        if (currentAnimator == null) return "";
        var info = currentAnimator.GetCurrentAnimatorStateInfo(0);
        return info.IsName("") ? "" : "State";
    }

    private static string GetPath(Transform t)
    {
        if (!t) return "null";
        string path = t.name;
        Transform p = t.parent;
        int guard = 0;
        while (p && guard++ < 32) { path = p.name + "/" + path; p = p.parent; }
        return path;
    }

    private void LogPair(string tag, Transform s, Transform e)
    {
        if (!debugLogs) return;
        Debug.Log($"[TongTongManager] {tag}: start=({s?.name}) {s?.position} [{GetPath(s)}]  |  target=({e?.name}) {e?.position} [{GetPath(e)}]");
    }
}
