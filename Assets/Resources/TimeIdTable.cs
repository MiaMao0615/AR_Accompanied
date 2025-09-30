// Assets/Scripts/TimeIdTable.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimeIdRecord
{
    public string id;
    public string startTime;   // "HH:mm" 或浮点小时
    public string endTime;

    [Header("本 timeId 的动画/移动配置（不再用 Prefab）")]
    public RuntimeAnimatorController controller;  // 必填：该 id 下要用的 AnimatorController

    [Header("State 名（Layer0）")]
    public string moveStateName = "drawfwalk";
    public string arriveStateName = "run";

    [Header("移动参数")]
    public float moveSpeed = 2f;
    public float arriveThreshold = 0.05f;
    public float rotateSpeed = 10f;
    public bool smoothStop = true;

    [Header("到达策略")]
    public bool arriveOnBackward = false; // 反向到达是否也切 arrive

    public void ApplyOn(GameObject tongObj) // 只套配置，不启动
    {
        var animator = tongObj.GetComponentInChildren<Animator>() ?? tongObj.AddComponent<Animator>();
        if (!controller) { Debug.LogWarning($"[TimeIdRecord] id={id} 未配置 controller"); return; }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var mover = tongObj.GetComponent<TestPoint2Point>() ?? tongObj.AddComponent<TestPoint2Point>();
        mover.animator = animator;
        mover.controller = controller;
        mover.moveStateName = moveStateName;
        mover.arriveStateName = arriveStateName;
        mover.moveSpeed = moveSpeed;
        mover.arriveThreshold = arriveThreshold;
        mover.rotateSpeed = rotateSpeed;
        mover.smoothStop = smoothStop;
        mover.arriveOnBackward = arriveOnBackward;
    }

    /// <summary>
    /// 将本记录套用到一个“通通对象”上，并按 forward 方向启动。
    /// 要求：tongObj 上有（或可添加）Animator 和 TestPoint2Point；Animator 必须有 Avatar（Humanoid/Generic）。
    /// </summary>
    public void ApplyAndRunOn(GameObject tongObj, Transform start, Transform end, bool forward, bool log = true)
    {
        ApplyOn(tongObj);  // 只把 Animator/TestPoint2Point 的参数配置好
        if (log) Debug.Log($"[TimeIdRecord] Applied config for id={id}");
    }
}

[DefaultExecutionOrder(-200)]
public class TimeIdTable : MonoBehaviour
{
    public static TimeIdTable Instance { get; private set; }

    [Header("时间表记录（id → 控制器与参数）")]
    public List<TimeIdRecord> records = new List<TimeIdRecord>();

    private struct Slice { public string id; public float start; public float end; }
    private readonly List<Slice> _slices = new List<Slice>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TimeIdTable] 场景中存在多个 TimeIdTable，仅保留第一个实例。");
            return;
        }
        Instance = this;
        RebuildSlices();
    }

    private void OnValidate() => RebuildSlices();

    private void RebuildSlices()
    {
        _slices.Clear();
        if (records == null) return;

        foreach (var r in records)
        {
            if (r == null || string.IsNullOrEmpty(r.id)) continue;
            if (!TryParseHHMM(r.startTime, out float sh)) continue;
            if (!TryParseHHMM(r.endTime, out float eh)) continue;
            sh = Mathf.Repeat(sh, 24f);
            eh = Mathf.Repeat(eh, 24f);
            _slices.Add(new Slice { id = r.id, start = sh, end = eh });
        }
    }

    public TimeIdRecord GetRecordById(string id)
    {
        if (string.IsNullOrEmpty(id) || records == null) return null;
        return records.Find(r => r != null && r.id == id);
    }

    public bool TryMapTimeToId(float hour, out string id)
    {
        id = string.Empty;
        if (_slices.Count == 0) return false;

        float h = Mathf.Repeat(hour, 24f);
        foreach (var s in _slices)
        {
            bool hit =
                (s.start < s.end && h >= s.start && h < s.end) ||
                (s.start > s.end && (h >= s.start || h < s.end)) ||
                Mathf.Approximately(s.start, s.end); // 整日
            if (hit) { id = s.id; return true; }
        }
        return false;
    }

    public static bool TryParseHHMM(string s, out float hour)
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

    //对外接口
    public void ApplyAndRunForId(string id, GameObject tongObj, Transform start, Transform end, bool forward, bool log = true)
    {
        var rec = GetRecordById(id);
        if (rec == null)
        {
            Debug.LogWarning($"[TimeIdTable] 未找到记录：{id}");
            return;
        }
        rec.ApplyAndRunOn(tongObj, start, end, forward, log);
    }
}
