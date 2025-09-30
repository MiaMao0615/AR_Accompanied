using UnityEngine;

public class MicrophoneCapture : MonoBehaviour
{
    [Header("Audio")]
    public int sampleRate = 16000;     // Whisper �Ƽ� 16kHz
    public int recordLengthSec = 10;   // ѭ�����������ȣ��룩

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
    /// ��ȡ���ϴε��������ġ�����������float32 PCM��
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
            // ����
            sampleCount = (clipSamples - lastReadPos) + currentPos;
        }

        float[] buffer = new float[sampleCount * channels];

        // �� lastReadPos ��ʼ������ȡ sampleCount*channels ��������
        // ע�⣺AudioClip.GetData ���ܿ绺��β��һ���Զ������Է����δ���
        int samplesToEnd = (clipSamples - lastReadPos);
        if (sampleCount <= samplesToEnd)
        {
            micClip.GetData(buffer, lastReadPos);
        }
        else
        {
            // ��һ�Σ�lastReadPos -> clip end
            float[] part1 = new float[samplesToEnd * channels];
            micClip.GetData(part1, lastReadPos);

            // �ڶ��Σ�clip start -> currentPos
            float[] part2 = new float[(sampleCount - samplesToEnd) * channels];
            micClip.GetData(part2, 0);

            part1.CopyTo(buffer, 0);
            part2.CopyTo(buffer, part1.Length);
        }

        lastReadPos = currentPos;
        return buffer;
    }

    /// <summary>
    /// �� float32 PCM��С�ˣ�תΪ byte[]
    /// </summary>
    public static byte[] FloatArrayToByteArray(float[] floatArray)
    {
        if (floatArray == null || floatArray.Length == 0) return null;
        byte[] byteArray = new byte[floatArray.Length * 4];
        System.Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }
}
