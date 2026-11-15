using System;
using System.Collections;
using UnityEngine;

public class FogTrigger : MonoBehaviour
{
    public GameObject fogObject;
    public float fadeDuration = 1.5f;

    private SpriteRenderer fogRenderer;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        if (fogObject == null)
        {
            Debug.LogError("FogTrigger: fogObject is not assigned.");
            enabled = false;
            return;
        }

        fogRenderer = fogObject.GetComponent<SpriteRenderer>();
        if (fogRenderer == null)
        {
            Debug.LogError("FogTrigger: fogObject has no SpriteRenderer.");
            enabled = false;
            return;
        }

        // Initial state: fog visible.
        fogObject.SetActive(true);
        Color color = fogRenderer.color;
        color.a = 1f;
        fogRenderer.color = color;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !enabled)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fogObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadeFog(1f));
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !enabled)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeFog(0f, () => fogObject.SetActive(false)));
    }

    private IEnumerator FadeFog(float targetAlpha, Action onComplete = null)
    {
        float startAlpha = fogRenderer.color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            fogRenderer.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        fogRenderer.color = new Color(1f, 1f, 1f, targetAlpha);
        onComplete?.Invoke();
    }
}
