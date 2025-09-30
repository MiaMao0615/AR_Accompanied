// Assets/Scripts/ITimeIdController.cs
using UnityEngine;

public interface ITimeIdController
{
    /// ������ע�� Animator / TwoPointMover���� MotionRunner��
    void Bind(Animator animator, TwoPointMover mover);

    /// һ�� RunMotion ��ʼǰ��from��to��forward=true ��ʾ Start��Target��
    void BeforeRun(Transform from, Transform to, bool forward);

    /// �����ص���forward ͬ�ϣ�
    void AfterArrive(bool forward);
}
