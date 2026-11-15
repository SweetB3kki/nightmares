using UnityEngine;

public class LoadingSplash : MonoBehaviour
{
    public float duration = 3f;

    void Start()
    {
        Invoke(nameof(HideSplash), duration);
    }

    void HideSplash()
    {
        gameObject.SetActive(false);
    }
}
