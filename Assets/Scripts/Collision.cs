using System.Collections;
using TMPro;
using UnityEngine;

public class CollisionMessage : MonoBehaviour
{
    [Header("UI & 文案")]
    public TMP_Text messageText;
    [TextArea] public string messageOnHit = "通通碰到桌子了，要往后一点";
    public float displayTime = 5f;

    [Header("触发过滤（被撞物体 Tag）")]
    public string triggerTag = "Collision";  // ★桌子等物体请设为这个 Tag

    [Header("可选音效")]
    public AudioClip collisionClip;

    [Header("可选回调")]
    public TongTongManager tongTongManager;

    private AudioSource _audio;
    private Coroutine _co;

    void Awake()
    {
        _audio = GetComponent<AudioSource>(); // 按安装逻辑，这里必然有 AudioSource
        if (_audio) _audio.playOnAwake = false;
    }

    // 触发器进入：自己是 isTrigger=true，桌子等物体是非 Trigger 的 Collider
    void OnTriggerEnter(Collider other)
    {
        if (!other) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

        // 1) 文案显示
        if (messageText)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShowMessageRoutine(messageOnHit, displayTime));
        }

        // 2) 音效
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
