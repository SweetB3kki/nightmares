using Cinemachine;
using UnityEngine;
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance;

    public CinemachineVirtualCamera cam;
    private CinemachineBasicMultiChannelPerlin noise;

    private float shakeDuration = 0f;
    private float shakeAmplitude = 1.2f;
    private float shakeFrequency = 2f;

    private void Awake()
    {
        Instance = this;
        noise = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            shakeDuration -= Time.deltaTime;
            noise.m_AmplitudeGain = shakeAmplitude;
            noise.m_FrequencyGain = shakeFrequency;
        }
        else
        {
            noise.m_AmplitudeGain = 0f;
            noise.m_FrequencyGain = 0f;
        }
    }

    public void Shake(float duration = 0.5f)
    {
        shakeDuration = duration;
    }
}
