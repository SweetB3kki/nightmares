using UnityEngine;

public class BossBreathing : MonoBehaviour
{
    public float breathScale = 0.02f;     // Насколько тянется вверх
    public float breathSpeed = 1.5f;      // Скорость дыхания

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private SpriteRenderer sr;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.position;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float offset = (Mathf.Sin(Time.time * breathSpeed) + 1f) / 2f; // [0..1]
        float scaleY = 1f + offset * breathScale;
        transform.localScale = new Vector3(originalScale.x, scaleY, originalScale.z);

        // Компенсация смещения вниз
        float spriteHeight = sr.bounds.size.y / originalScale.y;
        float deltaY = (scaleY - 1f) * spriteHeight / 2f;
        transform.position = originalPosition + new Vector3(0f, deltaY, 0f);
    }
}
