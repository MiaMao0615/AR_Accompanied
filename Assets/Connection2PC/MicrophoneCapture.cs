using UnityEngine;

public class MicrophoneCapture : MonoBehaviour
{
    [Header("Audio")]
    public int sampleRate = 16000;     // Whisper 推荐 16kHz
    public int recordLengthSec = 10;   // 循环缓冲区长度（秒）

    private AudioClip micClip;
    private string micDevice;
    private int lastReadPos = 0;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone found.");
            return;
        }

        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, recordLengthSec, sampleRate);
        Debug.Log($"Mic started: {micDevice} @ {sampleRate} Hz");
    }

    /// <summary>
    /// 获取从上次调用以来的“新样本”（float32 PCM）
    /// </summary>
    public float[] GetNewSamples()
    {
        if (micClip == null) return null;

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos == lastReadPos) return null;

        int channels = micClip.channels;
        int clipSamples = micClip.samples;

        int sampleCount;
        if (currentPos > lastReadPos)
        {
            sampleCount = currentPos - lastReadPos;
        }
        else
        {
            // 环回
            sampleCount = (clipSamples - lastReadPos) + currentPos;
        }

        float[] buffer = new float[sampleCount * channels];

        // 从 lastReadPos 开始连续读取 sampleCount*channels 个样本：
        // 注意：AudioClip.GetData 不能跨缓冲尾部一次性读，所以分两段处理
        int samplesToEnd = (clipSamples - lastReadPos);
        if (sampleCount <= samplesToEnd)
        {
            micClip.GetData(buffer, lastReadPos);
        }
        else
        {
            // 第一段：lastReadPos -> clip end
            float[] part1 = new float[samplesToEnd * channels];
            micClip.GetData(part1, lastReadPos);

            // 第二段：clip start -> currentPos
            float[] part2 = new float[(sampleCount - samplesToEnd) * channels];
            micClip.GetData(part2, 0);

            part1.CopyTo(buffer, 0);
            part2.CopyTo(buffer, part1.Length);
        }

        lastReadPos = currentPos;
        return buffer;
    }

    /// <summary>
    /// 把 float32 PCM（小端）转为 byte[]
    /// </summary>
    public static byte[] FloatArrayToByteArray(float[] floatArray)
    {
        if (floatArray == null || floatArray.Length == 0) return null;
        byte[] byteArray = new byte[floatArray.Length * 4];
        System.Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }
}
