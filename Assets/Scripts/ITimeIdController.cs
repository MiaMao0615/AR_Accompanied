// Assets/Scripts/ITimeIdController.cs
using UnityEngine;

public interface ITimeIdController
{
    /// 由宿主注入 Animator / TwoPointMover（或 MotionRunner）
    void Bind(Animator animator, TwoPointMover mover);

    /// 一次 RunMotion 开始前（from→to，forward=true 表示 Start→Target）
    void BeforeRun(Transform from, Transform to, bool forward);

    /// 到达后回调（forward 同上）
    void AfterArrive(bool forward);
}
