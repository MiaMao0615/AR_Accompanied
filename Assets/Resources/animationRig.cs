// TongTongAnimationRig.cs
// 目标：在 Start/OnTongTongSpawned 当帧“同步”完成三骨节绑定，并初始化 rig.weight=0。
// 提供 SetActive()/StopRig() 两个接口给外部调用。
// ★ 绑定成功后会在 Console 打印：已经绑定好了骨骼

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TongTongAnimationRig : MonoBehaviour
{
    [Header("目标")]
    public Transform initialTarget;     // 场景里的 q（初始约束目标）
    public Transform arCameraTarget;    // AR 相机子物体（SetActive 时切到它）

    [Header("各骨节约束权重 (MultiAimConstraint.weight)")]
    [Range(0, 1)] public float headAimWeight = 0.4f;
    [Range(0, 1)] public float spine02AimWeight = 0.2f;
    [Range(0, 1)] public float spine03AimWeight = 0.35f;

    [Header("整体 Rig 渐变参数")]
    public float activateTweenDuration = 1.0f; // SetActive 时 rig.weight 由当前→1 的时长

    [Header("调试")]
    public bool debugLogs = true;

    // 运行时
    private Transform _tongTongRoot;    // 由 Manager 注入
    private Rig _rig;                   // 整体 Rig（只改它的 weight）
    private Transform _rigAimRoot;      // rig_aim
    private float _tweenTime = 0f;
    private bool _isTweening = false;
    private float _tweenFrom, _tweenTo, _tweenDur;

    // 跟随 watchingTarget（= initialTarget）到相机
    private Coroutine _followCo;
    private Vector3 _savedPos;
    private Quaternion _savedRot;
    private bool _hasSavedPose = false;


    // ======== 由 Manager 调用：注入“当前通通根”，并同步完成绑定 ========
    public void OnTongTongSpawned(Transform tongTongRoot)
    {
        _tongTongRoot = tongTongRoot;
        if (!BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget))
        {
            Warn("绑定失败：请检查 initialTarget(q) 与角色骨骼是否存在");
            return;
        }
        // 初始为关闭
        BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget);
        print("绑定成功");
        SetRigImmediate(0f);
        Debug.Log("已经绑定好了骨骼");           // ★ 你要的确认日志
        Log("Rig ready. target=initialTarget, rig=0");
    }

    // ======== 对外接口 ========
    public void SetActive()
    {
        if (!arCameraTarget) { Warn("arCameraTarget 未设置"); return; }
        if (!initialTarget) { Warn("initialTarget(watchingTarget) 未设置"); return; }

        // 保存 watchingTarget 初始位姿（只保存一次）
        if (!_hasSavedPose)
        {
            _savedPos = initialTarget.position;
            _savedRot = initialTarget.rotation;
            _hasSavedPose = true;
        }

        // 开始逐帧对齐 watchingTarget → arCameraTarget
        if (_followCo != null) { StopCoroutine(_followCo); }
        _followCo = StartCoroutine(FollowWatchingTargetToAR());

        // 整体权重缓慢到 1
        StartTweenTo(1f, activateTweenDuration);
        Log("SetActive -> FOLLOW: watchingTarget follows AR, rig → 1 (tween)");
    }


    private System.Collections.IEnumerator FollowWatchingTargetToAR()
    {
        while (initialTarget && arCameraTarget)
        {
            initialTarget.position = arCameraTarget.position;
            initialTarget.rotation = arCameraTarget.rotation;
            yield return null;
        }
    }


    public void StopRig()
    {

        // 停止跟随并把 watchingTarget 复位
        if (_followCo != null) { StopCoroutine(_followCo); _followCo = null; }
        if (_hasSavedPose && initialTarget)
        {
            initialTarget.position = _savedPos;
            initialTarget.rotation = _savedRot;
        }

        // 立即关闭 Rig
        StopTween();
        SetRigImmediate(0f);
        Log("StopRig -> restore watchingTarget, rig=0");
    }


    void Update()
    {
        // 简单的本地 tween（不依赖协程，避免首帧竞态）
        if (_isTweening && _rig)
        {
            _tweenTime += Time.deltaTime;
            float k = Mathf.Clamp01(_tweenTime / _tweenDur);
            _rig.weight = Mathf.Lerp(_tweenFrom, _tweenTo, k);
            if (k >= 1f) _isTweening = false;
        }
    }

    // ======== 核心：同步完成三骨节绑定（无协程版） ========
    private bool BindRigAndConstraintsImmediate(Transform characterRoot, Transform aimTarget)
    {
        if (characterRoot == null) { Debug.LogError("characterRoot 为空"); return false; }
        if (aimTarget == null) { Debug.LogError("initialTarget 未设置"); return false; }

        var animator = characterRoot.GetComponentInChildren<Animator>();
        Transform FindFirst(params Transform[] cands) { foreach (var c in cands) if (c) return c; return null; }
        Transform Fuzzy(string key) => FindChildByNameContains(characterRoot, key);

        // Head
        Transform head = FindFirst(
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03/neck_01/head"),
            Fuzzy("head")
        );
        if (!head) { Debug.LogError("未找到 head 骨骼"); return false; }

        var host = animator ? animator.transform : characterRoot;
        var rb = host.GetComponent<RigBuilder>() ?? host.gameObject.AddComponent<RigBuilder>();
        if (rb.layers == null) rb.layers = new List<RigLayer>();

        // rig_aim
        var rigTf = host.Find("rig_aim");
        if (!rigTf)
        {
            var rigGO = new GameObject("rig_aim");
            rigGO.transform.SetParent(host, false);
            rigTf = rigGO.transform;
        }
        var rig = rigTf.GetComponent<Rig>() ?? rigTf.gameObject.AddComponent<Rig>();
        rig.weight = 1f; // 构建时设 1，随后立刻置 0 作为初始关闭

        MultiAimConstraint EnsureAimNode(string nodeName, Transform constrainedObj, float aimWeightEach)
        {
            var node = rigTf.Find(nodeName);
            if (!node)
            {
                var go = new GameObject(nodeName);
                node = go.transform;
                node.SetParent(rigTf, false);
            }
            var aim = node.GetComponent<MultiAimConstraint>() ?? node.gameObject.AddComponent<MultiAimConstraint>();

            var d = aim.data;
            d.constrainedObject = constrainedObj;
            d.worldUpType = MultiAimConstraintData.WorldUpType.None;
            d.aimAxis = MultiAimConstraintData.Axis.Y_NEG;
            d.upAxis = MultiAimConstraintData.Axis.X_NEG;
            d.constrainedXAxis = d.constrainedYAxis = d.constrainedZAxis = true;
            d.maintainOffset = true;
            d.limits = new Vector2(-100f, 100f);

            var src = new WeightedTransformArray();
            src.Add(new WeightedTransform(aimTarget, 1f)); // 初始 source = initialTarget(q)
            d.sourceObjects = src;

            aim.data = d;
            aim.weight = Mathf.Clamp01(aimWeightEach);     // 0.4 / 0.2 / 0.35
            return aim;
        }

        // 三骨节
        EnsureAimNode("headaim", head, headAimWeight);

        Transform spine2 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02"),
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Spine) : null,
            Fuzzy("spine_02")
        );
        if (spine2) EnsureAimNode("spine02aim", spine2, spine02AimWeight);
        else Debug.LogWarning("未找到 spine_02，跳过 Spine_02 约束");

        Transform spine3 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03"),
            (animator && animator.isHuman) ? (animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine)) : null,
            Fuzzy("spine_03")
        );
        if (spine3) EnsureAimNode("spineaim", spine3, spine03AimWeight);
        else Debug.LogWarning("未找到 spine_03，跳过 Spine 约束");

        // 注册层并立刻 Build（同步）
        if (!rb.layers.Exists(l => l.rig == rig)) rb.layers.Add(new RigLayer(rig));
        rb.Build();

        // 缓存
        _rig = rig;
        _rigAimRoot = rigTf;
        return true;
    }

    // ======== 目标切换 & 权重控制 ========
    private void RebindAllAimTargets(Transform newTarget)
    {
        if (!_rigAimRoot || !newTarget) return;
        var aims = _rigAimRoot.GetComponentsInChildren<MultiAimConstraint>(true);

        var src = new WeightedTransformArray();
        src.Add(new WeightedTransform(newTarget, 1f));

        foreach (var aim in aims)
        {
            var d = aim.data;
            d.sourceObjects = src; // 只改目标
            aim.data = d;
        }
    }

    private void StartTweenTo(float to, float duration)
    {
        if (_rig == null) return;
        _isTweening = true;
        _tweenFrom = Mathf.Clamp01(_rig.weight);
        _tweenTo = Mathf.Clamp01(to);
        _tweenDur = Mathf.Max(0.0001f, duration);
        _tweenTime = 0f;
    }

    private void StopTween() => _isTweening = false;

    private void SetRigImmediate(float w)
    {
        if (_rig) _rig.weight = Mathf.Clamp01(w);
    }

    // ======== 工具 ========
    private Transform FindChildByNameContains(Transform root, string key)
    {
        string lowerKey = key.ToLower();
        return FindChildRecursive(root, lowerKey);
    }
    private Transform FindChildRecursive(Transform current, string lowerKey)
    {
        if (current.name.ToLower().Contains(lowerKey)) return current;
        foreach (Transform child in current)
        {
            var r = FindChildRecursive(child, lowerKey);
            if (r != null) return r;
        }
        return null;
    }

    private void Log(string m) { if (debugLogs) Debug.Log($"[TongTongAnimationRig] {m}"); }
    private void Warn(string m) { Debug.LogWarning($"[TongTongAnimationRig] {m}"); }
}
