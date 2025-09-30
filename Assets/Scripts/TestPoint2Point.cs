// Assets/Scripts/TwoPointMover.cs
using UnityEngine;
using System.Collections;

public class TwoPointMover : MonoBehaviour
{
    [Header("引用（可留空，运行时注入）")]
    public Animator animator;                           // 若留空，将在 Kickoff 时自动从 moveTarget 或其子物体上获取
    public Transform startPoint;                        // 起点（运行时传入）
    public Transform endPoint;                          // 终点（运行时传入）

    [Header("要移动的对象（必填：通通的 Transform）")]
    public Transform moveObject;                        // 由 Manager 传入（currentTongTong.transform）

    [Header("Animator 状态名（与 Controller 的 State 名一致）")]
    public string moveStateName = "drawfwalk";          // 移动时播放
    public string arriveStateName = "run";              // 到达时播放

    [Header("到达策略")]
    public bool playArriveOnForward = true;             // 正向到达是否切动画2
    public bool playArriveOnBackward = false;           // 反向到达是否切动画2（默认不切）

    [Header("移动参数")]
    public float moveSpeed = 2.0f;                      // m/s
    public float arriveThreshold = 0.05f;               // 到达判定
    public float rotateSpeed = 10f;                     // 朝向插值速度
    public bool smoothStop = true;                      // 近端减速

    [Header("调试")]
    public bool logVerbose = true;

    // 运行态
    private bool _forward = true;
    private Coroutine _moveCo;

    void Awake()
    {
        // 不在这里找 animator/moveObject；统一在 Kickoff 时完成，避免场景里无效引用
    }

    // —— 对外入口：一把梭 —— //
    /// <summary>
    /// 运行一次移动：
    /// forward=true  : Start→End（到达默认切动画2）
    /// forward=false : End→Start（到达默认不切动画2，除非 playArriveOnBackward=true）
    /// </summary>
    public void Kickoff(Transform start, Transform end, bool forward, Transform moveTarget, Animator animOverride = null)
    {
        // 注入
        startPoint = start;
        endPoint = end;
        moveObject = moveTarget;
        animator = animOverride ? animOverride : (animator ? animator : FindAnimator(moveTarget));

        if (!SanityCheck()) return;

        // 强制关闭 RootMotion（位移由脚本控制）
        animator.applyRootMotion = false;

        // 停掉旧协程
        if (_moveCo != null) { StopCoroutine(_moveCo); _moveCo = null; }

        _forward = forward;

        // 选方向
        Vector3 from = forward ? startPoint.position : endPoint.position;
        Vector3 to = forward ? endPoint.position : startPoint.position;

        // 立刻切到“移动动画”
        PlayMoveAnim();

        // 跑起来
        bool playArrive = forward ? playArriveOnForward : playArriveOnBackward;
        _moveCo = StartCoroutine(MoveRoutine(from, to, playArrive));
    }

    private Animator FindAnimator(Transform t)
    {
        if (!t) return null;
        var a = t.GetComponentInChildren<Animator>();
        return a;
    }

    private bool SanityCheck()
    {
        if (!moveObject)
        {
            Debug.LogWarning("[TwoPointMover] moveObject 为空（需要传通通的 Transform）。");
            return false;
        }
        if (!startPoint || !endPoint)
        {
            Debug.LogWarning("[TwoPointMover] startPoint / endPoint 为空。");
            return false;
        }
        if (!animator || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[TwoPointMover] Animator 或其 Controller 为空。");
            return false;
        }

        // 校验状态是否存在（短名）
        bool okMove = animator.HasState(0, Animator.StringToHash(moveStateName));
        bool okArr = animator.HasState(0, Animator.StringToHash(arriveStateName));
        if (logVerbose)
        {
            var ctrl = animator.runtimeAnimatorController;
            string ctrlName = ctrl ? ctrl.name : "null";
            string clips = (ctrl && ctrl.animationClips != null && ctrl.animationClips.Length > 0)
                ? string.Join(", ", System.Array.ConvertAll(ctrl.animationClips, c => c.name))
                : "(no clips)";
            Debug.Log($"[TwoPointMover] Ctrl={ctrlName}, Move='{moveStateName}'({okMove}), Arrive='{arriveStateName}'({okArr}), Clips=[{clips}]");
        }
        if (!okMove) Debug.LogWarning($"[TwoPointMover] 未找到移动状态：{moveStateName}");
        if (!okArr) Debug.LogWarning($"[TwoPointMover] 未找到到达状态：{arriveStateName}");

        return okMove; // 移动状态必须存在；到达状态缺失则仅不切
    }

    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, bool playArriveAnim)
    {
        // 如需每次都从 from 开始位置起步，解开下一行
        // moveObject.position = from;

        while (true)
        {
            Vector3 pos = moveObject.position;
            Vector3 dir = to - pos;
            float dist = dir.magnitude;

            if (dist <= arriveThreshold) break;

            // 朝向移动方向
            if (dir.sqrMagnitude > 1e-6f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                moveObject.rotation = Quaternion.Slerp(moveObject.rotation, targetRot, Time.deltaTime * rotateSpeed);
            }

            // 匀速 + 可选近端减速
            float v = moveSpeed;
            if (smoothStop)
            {
                float k = Mathf.Clamp01(dist / 1.0f);
                v *= Mathf.Lerp(0.5f, 1f, k);
            }

            moveObject.position = Vector3.MoveTowards(pos, to, v * Time.deltaTime);
            yield return null;
        }

        if (playArriveAnim) PlayArriveAnim();
        _moveCo = null;
    }

    // —— 动画切换 —— //
    private void PlayMoveAnim()
    {
        if (animator && !string.IsNullOrEmpty(moveStateName))
        {
            if (logVerbose) Debug.Log($"[TwoPointMover] Play Move: {moveStateName}");
            animator.Play(moveStateName, 0, 0f);
        }
    }

    private void PlayArriveAnim()
    {
        if (animator && !string.IsNullOrEmpty(arriveStateName))
        {
            if (logVerbose) Debug.Log($"[TwoPointMover] CrossFade Arrive: {arriveStateName}");
            animator.CrossFade(arriveStateName, 0.15f, 0, 0f);
        }
    }
}
