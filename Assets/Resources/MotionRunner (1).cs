using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 统一承接通通的位移动画/路径移动/跟随/返程与回fallback。
/// 说明：时机判断仍由外部 TongTongManager 决定；本类只负责“怎么移动/跟随/复位”。
/// </summary>
[RequireComponent(typeof(Animator))]
public class MotionRunner : MonoBehaviour
{
    // ====== 可调参数 ======
    [Header("到达判定")]
    [Tooltip("到达阈值（米）。若 TestPoint2Point 上有 arriveThreshold，则优先用它")]
    public float arriveThresholdFallback = 0.06f;

    [Header("跟随选项")]
    public bool followCopyRotation = true;
    public bool followCopyScale = true; // 严格保持缩放一致

    [Header("调试日志")]
    public bool debugLogs = false;

    // ====== 运行时组件 ======
    private TestPoint2Point _pointMover;   // Start<->Target 点到点移动
    private FollowTarget _follower;        // 到达后持续对齐
    private Transform _fallback;           // 回收点
    private Coroutine _pendingCo;          // 内部协程
    private Animator _anim;

    // 操作 token：新任务会使旧任务失效
    private int _opToken = 0;
    private int BumpToken() { _opToken++; return _opToken; }

    // —— 世界缩放工具 —— //
    static Vector3 GetWorldScale(Transform t) => t ? t.lossyScale : Vector3.one;
    static void SetWorldScale(Transform t, Vector3 world)
    {
        if (!t) return;
        var p = t.parent;
        if (!p) { t.localScale = world; return; }
        var ps = p.lossyScale;
        t.localScale = new Vector3(
            ps.x != 0 ? world.x / ps.x : 0f,
            ps.y != 0 ? world.y / ps.y : 0f,
            ps.z != 0 ? world.z / ps.z : 0f
        );
    }

    public void SetFollowOptions(bool copyRot, bool copyScale)
    {
        followCopyRotation = copyRot;
        followCopyScale = copyScale;
        if (_follower)
        {
            _follower.copyRotation = copyRot;
            _follower.copyScale = copyScale;
        }
    }

    void Awake()
    {
        _anim = GetComponent<Animator>();

        // 没有就加；先禁用，避免它的 Start() 触发
        _pointMover = GetComponent<TestPoint2Point>() ?? gameObject.AddComponent<TestPoint2Point>();
        _pointMover.enabled = false;

        _follower = GetComponent<FollowTarget>() ?? gameObject.AddComponent<FollowTarget>();
        _follower.copyRotation = followCopyRotation;
        _follower.copyScale = followCopyScale;
        _follower.EnableFollow(false);
    }


    // ============ 对外接口（Manager 直接调用） ============

    /// <summary>设置（或更新）fallback 位置引用（供回收用）。</summary>
    public void SetFallback(Transform fallback) => _fallback = fallback;

    /// <summary>
    /// 步骤1：把通通从 Fallback 对齐到 Start（严格复制世界位姿：位置+角度+缩放）
    /// </summary>
    public void AlignFromFallbackToStart(Transform start)
    {
        if (!start) return;
        StopAllInternal();
        transform.position = start.position;
        transform.rotation = start.rotation;
        SetWorldScale(transform, GetWorldScale(start));
        if (debugLogs) Debug.Log("[MotionRunner] AlignFromFallbackToStart");
    }

    /// <summary>
    /// 步骤2：从 Start 到 Target 的正向移动；到达后可开启实时跟随
    /// </summary>
    public void MoveStartToTarget(Transform start, Transform target, bool targetFollow = true)
    {
        if (!start || !target) return;
        StopAllInternal();
        int token = BumpToken();
        _pendingCo = StartCoroutine(MoveRoutine(start, target, forward: true, afterArrive: () =>
        {
            if (token != _opToken) return;                // 被新任务打断
            if (targetFollow) StartFollowing(target);     // 到达后开始跟随
        }, token));
    }

    /// <summary>开始实时跟随 target（位置+角度+可选缩放）。</summary>
    public void StartFollowing(Transform target)
    {
        if (!_follower) _follower = GetComponent<FollowTarget>() ?? gameObject.AddComponent<FollowTarget>();
        _follower.copyRotation = followCopyRotation;
        _follower.copyScale = followCopyScale;
        _follower.SetTarget(target, true);
        _follower.EnableFollow(true);
        if (debugLogs) Debug.Log("[MotionRunner] StartFollowing -> " + (target ? target.name : "null"));
    }

    /// <summary>停止跟随。</summary>
    public void StopFollowing()
    {
        if (_follower) _follower.EnableFollow(false);
    }

    /// <summary>反向：Target->Start 到达后回 Fallback。</summary>
    public void ReverseToStartThenFallback(Transform start, Transform target)
    {
        if (!start) return;
        StopAllInternal();
        int token = BumpToken();
        _pendingCo = StartCoroutine(ReverseThenFallbackRoutine(start, target, token));
    }

    /// <summary>硬中断一切并回到 fallback（位置/旋转/缩放全部恢复）。</summary>
    public void HardCancelAndReturnToFallback()
    {
        StopAllInternal();     // 停协程、停跟随、停 point-move
        BumpToken();           // 使正在跑的协程失效
        if (_fallback)
        {
            if (transform.parent == _fallback)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.position = _fallback.position;
                transform.rotation = _fallback.rotation;
            }
            SetWorldScale(transform, GetWorldScale(_fallback));
        }

        if (_pointMover) _pointMover.enabled = false;     // ★ 回到fallback时关闭它


        if (debugLogs) Debug.Log("[MotionRunner] HardCancelAndReturnToFallback");
    }

    /// <summary>
    /// （封装）TimeIdTable：应用 timeId 动画配置并执行正向/反向移动
    /// </summary>
    public void ApplyAndRunForId(string timeId, Transform start, Transform end, bool forward, bool debugLogsFromManager)
    {
        var table = TimeIdTable.Instance;
        if (table) table.ApplyAndRunForId(timeId, gameObject, start, end, forward, debugLogsFromManager);

        // ★ 刚被 TimeIdTable 添加/配置完，重新抓一次
        _pointMover = GetComponent<TestPoint2Point>();

        if (forward) MoveStartToTarget(start, end, true);
        else ReverseToStartThenFallback(start, end);
    }

    // ============ 内部实现 ============

    private void StopAllInternal()
    {
        StopFollowing();
        if (_pointMover) _pointMover.StopAllCoroutines(); // 若有专用 Stop 方法可替换
        if (_pendingCo != null) { StopCoroutine(_pendingCo); _pendingCo = null; }

        // 停 mover 自己的协程 + 清空回调，暂时禁用，避免 Start() 自动跑
        if (_pointMover)
        {
            _pointMover.StopAllCoroutines();
            _pointMover.onArrived = null;
            _pointMover.enabled = false;
        }
    }

    private float GetArriveThreshold()
    {
        if (_pointMover && _pointMover.arriveThreshold > 0f) return _pointMover.arriveThreshold;
        return Mathf.Max(0.0001f, arriveThresholdFallback);
    }

    // ★ 增加 token 参数，允许随时中断
    private IEnumerator MoveRoutine(Transform start, Transform end, bool forward, Action afterArrive, int token)
    {
        StopFollowing();

        if (!_pointMover) _pointMover = GetComponent<TestPoint2Point>() ?? gameObject.AddComponent<TestPoint2Point>();

        // 起点严格对齐（位/角/缩放）
        var begin = forward ? start : end;
        if (begin)
        {
            transform.position = begin.position;
            transform.rotation = begin.rotation;
            SetWorldScale(transform, GetWorldScale(begin));
        }

        if (_pointMover)
        {
            _pointMover.enabled = true;
            _pointMover.parentToEndOnForward = false;   // 绝不改父子
            _pointMover.hideOnBackwardArrive = false;
            bool arrived = false;

            // 事件驱动：到达时置位；反向时顺带回 fallback
            _pointMover.onArrived = (isForward) =>
            {
                if (token != _opToken) return;          // 已被新任务作废
                arrived = true;
                if (!isForward)                          // 反向到达 -> 立即回 fallback（不等外层）
                {
                    // 先关 mover，避免它的 Start()/Update 再起协程
                    _pointMover.enabled = false;
                    HardCancelAndReturnToFallback();
                }
            };

            // 写入起止点并启动
            _pointMover.startPoint = start;
            _pointMover.endPoint = end;
            _pointMover.Kickoff(start, end, forward);

            // 等待“到达”或超时/被打断
            float t0 = Time.time, timeout = 30f;
            while (!arrived && token == _opToken && (Time.time - t0) < timeout)
                yield return null;

            // 清掉回调，防止残留
            _pointMover.onArrived = null;

            // 正向到达：在同一帧再做 afterArrive（通常是开始跟随）
            if (arrived && token == _opToken && forward)
                afterArrive?.Invoke();
        }
        else
        {
            Debug.LogWarning("[MotionRunner] 未找到 TestPoint2Point，使用兜底瞬移");
            var dest = forward ? end : start;
            if (dest)
            {
                transform.position = dest.position;
                transform.rotation = dest.rotation;
                SetWorldScale(transform, GetWorldScale(dest));
            }
            yield return null;

            if (forward) afterArrive?.Invoke(); else HardCancelAndReturnToFallback();
        }

        _pendingCo = null;
    }

    void OnDestroy()
    {
        if (debugLogs)
            Debug.Log("[MotionRunner] destroyed on " + name + "\n" + Environment.StackTrace);
    }

    // 只保留“带 token”的版本
    private IEnumerator ReverseThenFallbackRoutine(Transform start, Transform target, int token)
    {
        yield return MoveRoutine(start, target, forward: false, afterArrive: null, token: token);
    }
}
