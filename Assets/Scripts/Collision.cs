using System.Collections;
using TMPro;
using UnityEngine;

public class CollisionMessage : MonoBehaviour
{
    [Header("UI & �İ�")]
    public TMP_Text messageText;
    [TextArea] public string messageOnHit = "ͨͨ���������ˣ�Ҫ����һ��";
    public float displayTime = 5f;

    [Header("�������ˣ���ײ���� Tag��")]
    public string triggerTag = "Collision";  // �����ӵ���������Ϊ��� Tag

    [Header("��ѡ��Ч")]
    public AudioClip collisionClip;

    [Header("��ѡ�ص�")]
    public TongTongManager tongTongManager;

    private AudioSource _audio;
    private Coroutine _co;

    void Awake()
    {
        _audio = GetComponent<AudioSource>(); // ����װ�߼��������Ȼ�� AudioSource
        if (_audio) _audio.playOnAwake = false;
    }

    // ���������룺�Լ��� isTrigger=true�����ӵ������Ƿ� Trigger �� Collider
    void OnTriggerEnter(Collider other)
    {
        if (!other) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

        // 1) �İ���ʾ
        if (messageText)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShowMessageRoutine(messageOnHit, displayTime));
        }

        // 2) ��Ч
        if (_audio && collisionClip) _audio.PlayOneShot(collisionClip);
        
    }

    private IEnumerator ShowMessageRoutine(string text, float seconds)
    {
        messageText.text = text;
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
            messageText.text = "";
        }
        _co = null;
    }
}
