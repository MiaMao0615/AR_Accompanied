// Assets/Scripts/FollowTarget.cs
using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    public Transform target;
    public bool copyRotation = true;
    public bool copyScale = false;
    public bool enabledFollow = false;

    public void SetTarget(Transform t, bool follow = true)
    {
        target = t;
        enabledFollow = follow && t != null && t.gameObject.activeInHierarchy;
        if (enabledFollow)
            SnapToTarget(); // 先对齐一次
    }

    public void EnableFollow(bool on)
    {
        enabledFollow = on && target != null && target.gameObject.activeInHierarchy;
    }

    public void SnapToTarget()
    {
        if (!target) return;
        transform.position = target.position;
        if (copyRotation) transform.rotation = target.rotation;
        if (copyScale) transform.localScale = target.lossyScale; // 一般不用
    }

    void LateUpdate()
    {
        if (!enabledFollow || !target) return;
        if (!target.gameObject.activeInHierarchy)
        {
            // 目标被禁用（丢跟踪），暂停跟随
            enabledFollow = false;
            return;
        }
        transform.position = target.position;
        if (copyRotation) transform.rotation = target.rotation;
        if (copyScale) transform.localScale = target.lossyScale;
    }
}
