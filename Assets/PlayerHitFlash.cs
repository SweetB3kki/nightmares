using System.Collections;
using UnityEngine;

public class PlayerHitFlash : MonoBehaviour
{
    [SerializeField] private Color flashColor = new Color(0.56f, 0.58f, 0.78f, 1f);
    [SerializeField] private float flashedAlphaMultiplier = 0.65f;
    [SerializeField] private int flashCount = 3;
    [SerializeField] private float flashOnDuration = 0.06f;
    [SerializeField] private float flashOffDuration = 0.05f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    private void Awake()
    {
        CacheRenderers();
    }

    public void Configure(
        Color configuredFlashColor,
        float configuredAlphaMultiplier,
        int configuredFlashCount,
        float configuredFlashOnDuration,
        float configuredFlashOffDuration)
    {
        flashColor = configuredFlashColor;
        flashedAlphaMultiplier = Mathf.Clamp01(configuredAlphaMultiplier);
        flashCount = Mathf.Max(1, configuredFlashCount);
        flashOnDuration = Mathf.Max(0.01f, configuredFlashOnDuration);
        flashOffDuration = Mathf.Max(0.01f, configuredFlashOffDuration);
        CacheRenderers();
    }

    public void PlayHitFlash()
    {
        CacheRenderers();
        RememberCurrentColors();

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void ResetVisuals()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = null;

        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            ApplyFlash(true);
            yield return new WaitForSeconds(flashOnDuration);

            ApplyFlash(false);
            if (i < flashCount - 1)
                yield return new WaitForSeconds(flashOffDuration);
        }

        flashRoutine = null;
    }

    private void ApplyFlash(bool active)
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            if (!active)
            {
                spriteRenderer.color = originalColors[i];
                continue;
            }

            Color original = originalColors[i];
            Color flashed = Color.Lerp(original, flashColor, 0.85f);
            flashed.a = original.a * flashedAlphaMultiplier;
            spriteRenderer.color = flashed;
        }
    }

    private void CacheRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];
        RememberCurrentColors();
    }

    private void RememberCurrentColors()
    {
        if (spriteRenderers == null)
            return;

        if (originalColors == null || originalColors.Length != spriteRenderers.Length)
            originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void OnDisable()
    {
        ResetVisuals();
    }
}
