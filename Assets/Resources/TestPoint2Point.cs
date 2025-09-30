// Assets/Scripts/TestPoint2Point.cs
using UnityEngine;
using System.Collections;

public class TestPoint2Point : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ① 引用与配置
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("引用")]
    public Animator animator;                          // 角色 Animator
    public RuntimeAnimatorController controller;       // 可选：默认 AnimatorController
    public Transform startPoint;                       // 起点
    public Transform endPoint;                         // 终点

    [Header("Animator 状态名（Layer 0）")]
    public string moveStateName = "drawfwalk";       // 行走/移动动画
    public string arriveStateName = "run";             // 到达/待机动画（你确认所有 controller 都有 run）
    public string idleStateName = "";                // 可留空；留空时会回退到 arriveStateName 或 moveStateName

    [Header("移动参数")]
    public float moveSpeed = 2.0f;
    public float arriveThreshold = 0.05f;
    public float rotateSpeed = 10f;
    public bool smoothStop = true;

    [Header("到达策略")]
    public bool arriveOnBackward = false;            // 返程到达时是否也播 arrive
    public bool parentToEndOnForward = false;          // 不建议改父子，通常保持 false

    [Header("外部触发（true: Start→End；false: End→Start）")]
    public bool trigger = true;

    [Header("到达事件/收尾策略")]
    public System.Action<bool> onArrived;              // 参数: isForward。true=正向到达 end，false=返程到达 start
    public bool hideOnBackwardArrive = false;         // 返程到达是否立刻隐藏
    public GameObject objectToHide;                    // 默认隐藏自己（若设置为 true）

    // ─────────────────────────────────────────────────────────────────────────────
    // ② 运行时状态
    // ─────────────────────────────────────────────────────────────────────────────
    private bool _lastTrigger;
    private Coroutine _moveCo;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("[TestPoint2Point] 未找到 Animator。");
            enabled = false;
            return;
        }
        if (controller) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        VerifyStates();

        if (objectToHide == null) objectToHide = gameObject;
    }

    void Start()
    {
        _lastTrigger = trigger;
        TryStartMoveByTrigger(force: true);
    }

    void Update()
    {
        if (trigger != _lastTrigger)
        {
            _lastTrigger = trigger;
            TryStartMoveByTrigger(force: false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ③ 触发与开跑入口
    // ─────────────────────────────────────────────────────────────────────────────
    private void TryStartMoveByTrigger(bool force)
    {
        EnsureAnimatorReady();

        if (!animator || !startPoint || !endPoint)
        {
            Debug.LogWarning("[TestPoint2Point] 缺少 animator/startPoint/endPoint");
            return;
        }

        if (_moveCo != null) { StopCoroutine(_moveCo); _moveCo = null; }

        if (trigger)
        {
            _moveCo = StartCoroutine(MoveRoutine(
                from: startPoint.position,
                to: endPoint.position,
                isForward: true,
                playArriveAnim: true
            ));
        }
        else
        {
            _moveCo = StartCoroutine(MoveRoutine(
                from: endPoint.position,
                to: startPoint.position,
                isForward: false,
                playArriveAnim: arriveOnBackward
            ));
        }
    }

    /// <summary>
    /// 外部统一入口（例如 MotionRunner 调用）：设置起止点并立即开跑。
    /// </summary>
    public void Kickoff(Transform start, Transform end, bool forward)
    {
        startPoint = start;
        endPoint = end;
        trigger = forward;

        Vector3 from = forward ? startPoint.position : endPoint.position;
        Vector3 to = forward ? endPoint.position : startPoint.position;

        EnsureAnimatorReady();

        // 先把角色放到 from，并朝向 to
        transform.position = from;
        Vector3 dir = (to - from);
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (_moveCo != null) { StopCoroutine(_moveCo); _moveCo = null; }
        bool playArriveAnim = forward || arriveOnBackward;
        _moveCo = StartCoroutine(MoveRoutine(from, to, forward, playArriveAnim));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ④ 动画工具
    // ─────────────────────────────────────────────────────────────────────────────
    private void EnsureAnimatorReady()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("[TestPoint2Point] 未找到 Animator。");
            enabled = false;
            return;
        }

        if (!animator.runtimeAnimatorController && controller)
            animator.runtimeAnimatorController = controller;

        animator.enabled = true;             // ★ 确保每次开跑前都启用
        animator.applyRootMotion = false;
        VerifyStates();
    }

    private void VerifyStates()
    {
        if (!animator || animator.runtimeAnimatorController == null) return;

        if (!string.IsNullOrEmpty(moveStateName) &&
            !animator.HasState(0, Animator.StringToHash(moveStateName)))
        {
            Debug.LogWarning($"[TestPoint2Point] Layer0 未找到 moveState：{moveStateName}");
        }

        if (!string.IsNullOrEmpty(arriveStateName) &&
            !animator.HasState(0, Animator.StringToHash(arriveStateName)))
        {
            Debug.LogWarning($"[TestPoint2Point] Layer0 未找到 arriveState：{arriveStateName}");
        }
    }

    private void PlayStateImmediately(string stateName)
    {
        if (!string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName)))
            animator.Play(stateName, 0, 0f);
    }

    private void CrossFadeState(string stateName, float fade = 0.1f)
    {
        if (!string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName)))
            animator.CrossFade(stateName, fade, 0, 0f);
    }

    private void PlayMoveAnimImmediately() => PlayStateImmediately(moveStateName);
    private void PlayArriveAnim() => CrossFadeState(arriveStateName, 0.1f);

    // ─────────────────────────────────────────────────────────────────────────────
    // ⑤ 核心移动协程
    // ─────────────────────────────────────────────────────────────────────────────
    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, bool isForward, bool playArriveAnim)
    {
        // 放置到 from，并给个合理朝向（看向 to）
        transform.position = from;
        Vector3 dir = (to - from);
        if (dir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // 确保 Animator 可用并立刻播放“移动”动画
        EnsureAnimatorReady();
        PlayMoveAnimImmediately();

        // 平滑移动
        while (true)
        {
            Vector3 pos = transform.position;
            Vector3 toDir = (to - pos);
            float dist = toDir.magnitude;

            if (dist <= arriveThreshold) break;

            if (toDir.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
            }

            float speed = moveSpeed;
            if (smoothStop)
            {
                float factor = Mathf.Clamp01(dist / 1.0f);
                speed *= Mathf.Lerp(0.5f, 1f, factor);
            }

            transform.position = Vector3.MoveTowards(pos, to, speed * Time.deltaTime);
            yield return null;
        }

        // —— 收尾 —— //
        if (isForward)
        {
            if (playArriveAnim) PlayArriveAnim();

            if (parentToEndOnForward && endPoint)
            {
                // 若你确实需要吸附到 end 的局部原点，可开启此选项
                transform.SetParent(endPoint, worldPositionStays: false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            onArrived?.Invoke(true);
        }
        else
        {
            // 返程到达：不禁用 Animator（否则下一段不会播），优先切 idle；没有 idle 就切到 arrive(run)；都没有再回退到 move 的第 0 帧
            animator.enabled = true;

            if (!string.IsNullOrEmpty(idleStateName) &&
                animator.HasState(0, Animator.StringToHash(idleStateName)))
            {
                PlayStateImmediately(idleStateName);
            }
            else if (!string.IsNullOrEmpty(arriveStateName) &&
                     animator.HasState(0, Animator.StringToHash(arriveStateName)))
            {
                PlayStateImmediately(arriveStateName); // 你确认所有 controller 都有 run
            }
            else
            {
                PlayStateImmediately(moveStateName);
            }

            if (hideOnBackwardArrive && objectToHide)
                objectToHide.SetActive(false);

            onArrived?.Invoke(false);
        }

        _moveCo = null;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ⑥ 其它外部接口
    // ─────────────────────────────────────────────────────────────────────────────
    public void SetTrigger(bool value)
    {
        trigger = value;
        if (trigger != _lastTrigger)
        {
            _lastTrigger = trigger;
            TryStartMoveByTrigger(force: false);
        }
    }
}
