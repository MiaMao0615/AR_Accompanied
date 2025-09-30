// TongTongAnimationRig.cs
// Ŀ�꣺�� Start/OnTongTongSpawned ��֡��ͬ����������ǽڰ󶨣�����ʼ�� rig.weight=0��
// �ṩ SetActive()/StopRig() �����ӿڸ��ⲿ���á�
// �� �󶨳ɹ������ Console ��ӡ���Ѿ��󶨺��˹���

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TongTongAnimationRig : MonoBehaviour
{
    [Header("Ŀ��")]
    public Transform initialTarget;     // ������� q����ʼԼ��Ŀ�꣩
    public Transform arCameraTarget;    // AR ��������壨SetActive ʱ�е�����

    [Header("���ǽ�Լ��Ȩ�� (MultiAimConstraint.weight)")]
    [Range(0, 1)] public float headAimWeight = 0.4f;
    [Range(0, 1)] public float spine02AimWeight = 0.2f;
    [Range(0, 1)] public float spine03AimWeight = 0.35f;

    [Header("���� Rig �������")]
    public float activateTweenDuration = 1.0f; // SetActive ʱ rig.weight �ɵ�ǰ��1 ��ʱ��

    [Header("����")]
    public bool debugLogs = true;

    // ����ʱ
    private Transform _tongTongRoot;    // �� Manager ע��
    private Rig _rig;                   // ���� Rig��ֻ������ weight��
    private Transform _rigAimRoot;      // rig_aim
    private float _tweenTime = 0f;
    private bool _isTweening = false;
    private float _tweenFrom, _tweenTo, _tweenDur;

    // ���� watchingTarget��= initialTarget�������
    private Coroutine _followCo;
    private Vector3 _savedPos;
    private Quaternion _savedRot;
    private bool _hasSavedPose = false;


    // ======== �� Manager ���ã�ע�롰��ǰͨͨ��������ͬ����ɰ� ========
    public void OnTongTongSpawned(Transform tongTongRoot)
    {
        _tongTongRoot = tongTongRoot;
        if (!BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget))
        {
            Warn("��ʧ�ܣ����� initialTarget(q) ���ɫ�����Ƿ����");
            return;
        }
        // ��ʼΪ�ر�
        BindRigAndConstraintsImmediate(_tongTongRoot, initialTarget);
        print("�󶨳ɹ�");
        SetRigImmediate(0f);
        Debug.Log("�Ѿ��󶨺��˹���");           // �� ��Ҫ��ȷ����־
        Log("Rig ready. target=initialTarget, rig=0");
    }

    // ======== ����ӿ� ========
    public void SetActive()
    {
        if (!arCameraTarget) { Warn("arCameraTarget δ����"); return; }
        if (!initialTarget) { Warn("initialTarget(watchingTarget) δ����"); return; }

        // ���� watchingTarget ��ʼλ�ˣ�ֻ����һ�Σ�
        if (!_hasSavedPose)
        {
            _savedPos = initialTarget.position;
            _savedRot = initialTarget.rotation;
            _hasSavedPose = true;
        }

        // ��ʼ��֡���� watchingTarget �� arCameraTarget
        if (_followCo != null) { StopCoroutine(_followCo); }
        _followCo = StartCoroutine(FollowWatchingTargetToAR());

        // ����Ȩ�ػ����� 1
        StartTweenTo(1f, activateTweenDuration);
        Log("SetActive -> FOLLOW: watchingTarget follows AR, rig �� 1 (tween)");
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

        // ֹͣ���沢�� watchingTarget ��λ
        if (_followCo != null) { StopCoroutine(_followCo); _followCo = null; }
        if (_hasSavedPose && initialTarget)
        {
            initialTarget.position = _savedPos;
            initialTarget.rotation = _savedRot;
        }

        // �����ر� Rig
        StopTween();
        SetRigImmediate(0f);
        Log("StopRig -> restore watchingTarget, rig=0");
    }


    void Update()
    {
        // �򵥵ı��� tween��������Э�̣�������֡��̬��
        if (_isTweening && _rig)
        {
            _tweenTime += Time.deltaTime;
            float k = Mathf.Clamp01(_tweenTime / _tweenDur);
            _rig.weight = Mathf.Lerp(_tweenFrom, _tweenTo, k);
            if (k >= 1f) _isTweening = false;
        }
    }

    // ======== ���ģ�ͬ��������ǽڰ󶨣���Э�̰棩 ========
    private bool BindRigAndConstraintsImmediate(Transform characterRoot, Transform aimTarget)
    {
        if (characterRoot == null) { Debug.LogError("characterRoot Ϊ��"); return false; }
        if (aimTarget == null) { Debug.LogError("initialTarget δ����"); return false; }

        var animator = characterRoot.GetComponentInChildren<Animator>();
        Transform FindFirst(params Transform[] cands) { foreach (var c in cands) if (c) return c; return null; }
        Transform Fuzzy(string key) => FindChildByNameContains(characterRoot, key);

        // Head
        Transform head = FindFirst(
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03/neck_01/head"),
            Fuzzy("head")
        );
        if (!head) { Debug.LogError("δ�ҵ� head ����"); return false; }

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
        rig.weight = 1f; // ����ʱ�� 1����������� 0 ��Ϊ��ʼ�ر�

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
            src.Add(new WeightedTransform(aimTarget, 1f)); // ��ʼ source = initialTarget(q)
            d.sourceObjects = src;

            aim.data = d;
            aim.weight = Mathf.Clamp01(aimWeightEach);     // 0.4 / 0.2 / 0.35
            return aim;
        }

        // ���ǽ�
        EnsureAimNode("headaim", head, headAimWeight);

        Transform spine2 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02"),
            (animator && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Spine) : null,
            Fuzzy("spine_02")
        );
        if (spine2) EnsureAimNode("spine02aim", spine2, spine02AimWeight);
        else Debug.LogWarning("δ�ҵ� spine_02������ Spine_02 Լ��");

        Transform spine3 = FindFirst(
            characterRoot.Find("root/pelvis/spine_01/spine_02/spine_03"),
            (animator && animator.isHuman) ? (animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine)) : null,
            Fuzzy("spine_03")
        );
        if (spine3) EnsureAimNode("spineaim", spine3, spine03AimWeight);
        else Debug.LogWarning("δ�ҵ� spine_03������ Spine Լ��");

        // ע��㲢���� Build��ͬ����
        if (!rb.layers.Exists(l => l.rig == rig)) rb.layers.Add(new RigLayer(rig));
        rb.Build();

        // ����
        _rig = rig;
        _rigAimRoot = rigTf;
        return true;
    }

    // ======== Ŀ���л� & Ȩ�ؿ��� ========
    private void RebindAllAimTargets(Transform newTarget)
    {
        if (!_rigAimRoot || !newTarget) return;
        var aims = _rigAimRoot.GetComponentsInChildren<MultiAimConstraint>(true);

        var src = new WeightedTransformArray();
        src.Add(new WeightedTransform(newTarget, 1f));

        foreach (var aim in aims)
        {
            var d = aim.data;
            d.sourceObjects = src; // ֻ��Ŀ��
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

    // ======== ���� ========
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
