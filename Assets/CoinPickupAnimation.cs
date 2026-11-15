using UnityEngine;

public class CoinPickupAnimation : MonoBehaviour
{
    public float rotationSpeed = 720f;
    public float arcHeight = 1.5f;      // Насколько высоко "подпрыгнет"
    public float fallDistance = 3f;     // Насколько далеко вниз
    public float duration = 1f;

    private Vector3 startPos;
    private Vector3 endPos;
    private float timer = 0f;
    private bool animating = false;

    public void PlayPickupAnimation()
    {
        startPos = transform.position;
        endPos = startPos + Vector3.down * fallDistance;
        timer = 0f;
        animating = true;
    }

    void Update()
    {
        if (!animating) return;

        timer += Time.deltaTime;
        float t = timer / duration;

        // Ключевой момент: движение по дуге
        float heightOffset = arcHeight * Mathf.Sin(Mathf.PI * t); // дуга
        Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
        currentPos.y += heightOffset;
        transform.position = currentPos;

        // Вращаем спрайт
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // Удаляем по завершении
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
