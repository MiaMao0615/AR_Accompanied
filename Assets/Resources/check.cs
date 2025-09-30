using UnityEngine;

public class ParentGuard : MonoBehaviour
{
    Transform last;
    void LateUpdate()
    {
        if (transform.parent != last)
        {
            Debug.LogError($"[ParentGuard] Parent changed -> {transform.parent?.name ?? "null"}\n{System.Environment.StackTrace}");
            last = transform.parent;
        }
    }
}
