using System.Collections;
using UnityEngine;

public class DamageVignetteController : MonoBehaviour
{
    [Header("Primary")]
    [SerializeField] private bool overrideVignetteAppearance = false;
    [SerializeField] private int cornerType = 1;
    [SerializeField] private Color mainColor = Color.black;
    [SerializeField] private bool stretchMainToScreen = false;
    [SerializeField] private float transitionDuration = 1.15f;
    [SerializeField] private float secondHealthScale = 3f;
    [SerializeField] private float firstHealthScale = 4f;
    [SerializeField] private float deathScale = 5f;
    [SerializeField] private float secondHealthPulseAmplitude = 0.03f;
    [SerializeField] private float firstHealthPulseAmplitude = 0.08f;
    [SerializeField] private float pulseSpeed = 1.3f;

    [Header("Secondary Overlay")]
    [SerializeField] private bool useSecondaryVignette = true;
    [SerializeField] private string secondaryVignetteName = "Q_Vignette_Single2";
    [SerializeField] private float secondarySecondHealthScale = 2f;
    [SerializeField] private float secondaryFirstHealthScale = 3f;
    [SerializeField] private float secondaryDeathScale = 3f;
    [SerializeField] private float secondarySecondHealthPulseAmplitude = 0.02f;
    [SerializeField] private float secondaryFirstHealthPulseAmplitude = 0.05f;

    private Q_Vignette_Single vignette;
    private Q_Vignette_Single secondaryVignette;
    private float currentScale = 0f;
    private float targetScale = 0f;
    private float scaleVelocity = 0f;
    private float secondaryCurrentScale = 0f;
    private float secondaryTargetScale = 0f;
    private float secondaryScaleVelocity = 0f;
    private bool deathBurstActive = false;

    public bool IsVisuallyCleared =>
        !deathBurstActive &&
        targetScale <= 0.01f &&
        secondaryTargetScale <= 0.01f &&
        currentScale <= 0.01f &&
        secondaryCurrentScale <= 0.01f;

    private void Awake()
    {
        vignette = GetComponent<Q_Vignette_Single>();
        secondaryVignette = FindSecondaryVignette();
        ApplyStaticSettings();
        ResetState(true);
    }

    private void Update()
    {
        if (vignette == null)
            return;

        if (useSecondaryVignette && secondaryVignette == null)
            secondaryVignette = FindSecondaryVignette();

        ApplyStaticSettings();

        if (deathBurstActive)
        {
            vignette.mainScale = deathScale;
            if (secondaryVignette != null)
                secondaryVignette.mainScale = secondaryDeathScale;
            return;
        }

        float smoothTime = Mathf.Max(0.01f, transitionDuration);
        currentScale = Mathf.SmoothDamp(currentScale, targetScale, ref scaleVelocity, smoothTime);
        secondaryCurrentScale = Mathf.SmoothDamp(
            secondaryCurrentScale,
            secondaryTargetScale,
            ref secondaryScaleVelocity,
            smoothTime);

        float pulse = 0f;
        if (targetScale > 0f)
        {
            float amplitude = targetScale >= firstHealthScale - 0.01f
                ? firstHealthPulseAmplitude
                : secondHealthPulseAmplitude;

            pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) * amplitude;
        }

        vignette.mainScale = Mathf.Max(0f, currentScale + pulse);

        if (secondaryVignette == null)
            return;

        float secondaryPulse = 0f;
        if (secondaryTargetScale > 0f)
        {
            float amplitude = secondaryTargetScale >= secondaryFirstHealthScale - 0.01f
                ? secondaryFirstHealthPulseAmplitude
                : secondarySecondHealthPulseAmplitude;

            secondaryPulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) * amplitude;
        }

        secondaryVignette.mainScale = Mathf.Max(0f, secondaryCurrentScale + secondaryPulse);
    }

    public void Configure(
        int configuredCornerType,
        Color configuredColor,
        bool configuredStretchMainToScreen,
        float configuredTransitionDuration,
        float configuredSecondHealthScale,
        float configuredFirstHealthScale,
        float configuredDeathScale,
        float configuredSecondHealthPulseAmplitude,
        float configuredFirstHealthPulseAmplitude,
        float configuredPulseSpeed)
    {
        cornerType = Mathf.Max(0, configuredCornerType);
        mainColor = configuredColor;
        stretchMainToScreen = configuredStretchMainToScreen;
        transitionDuration = Mathf.Max(0.01f, configuredTransitionDuration);
        secondHealthScale = Mathf.Max(0f, configuredSecondHealthScale);
        firstHealthScale = Mathf.Max(0f, configuredFirstHealthScale);
        deathScale = Mathf.Max(0f, configuredDeathScale);
        secondHealthPulseAmplitude = Mathf.Max(0f, configuredSecondHealthPulseAmplitude);
        firstHealthPulseAmplitude = Mathf.Max(0f, configuredFirstHealthPulseAmplitude);
        pulseSpeed = Mathf.Max(0f, configuredPulseSpeed);

        ApplyStaticSettings();
    }

    public void ConfigureDynamic(
        float configuredTransitionDuration,
        float configuredSecondHealthScale,
        float configuredFirstHealthScale,
        float configuredDeathScale,
        float configuredSecondHealthPulseAmplitude,
        float configuredFirstHealthPulseAmplitude,
        float configuredPulseSpeed)
    {
        transitionDuration = Mathf.Max(0.01f, configuredTransitionDuration);
        secondHealthScale = Mathf.Max(0f, configuredSecondHealthScale);
        firstHealthScale = Mathf.Max(0f, configuredFirstHealthScale);
        deathScale = Mathf.Max(0f, configuredDeathScale);
        secondHealthPulseAmplitude = Mathf.Max(0f, configuredSecondHealthPulseAmplitude);
        firstHealthPulseAmplitude = Mathf.Max(0f, configuredFirstHealthPulseAmplitude);
        pulseSpeed = Mathf.Max(0f, configuredPulseSpeed);
    }

    public void SetStageForHealth(int currentHealth, bool immediate = false)
    {
        deathBurstActive = false;
        targetScale = GetScaleForHealth(currentHealth);
        secondaryTargetScale = GetSecondaryScaleForHealth(currentHealth);

        if (!immediate)
            return;

        currentScale = targetScale;
        secondaryCurrentScale = secondaryTargetScale;
        scaleVelocity = 0f;
        secondaryScaleVelocity = 0f;

        if (vignette != null)
            vignette.mainScale = targetScale;

        if (secondaryVignette != null)
            secondaryVignette.mainScale = secondaryTargetScale;
    }

    public void SetDeathScale(float configuredDeathScale)
    {
        deathScale = Mathf.Max(0f, configuredDeathScale);
    }

    public IEnumerator PlayDeathBurst(float duration)
    {
        deathBurstActive = true;
        currentScale = deathScale;
        targetScale = deathScale;
        scaleVelocity = 0f;
        secondaryCurrentScale = secondaryDeathScale;
        secondaryTargetScale = secondaryDeathScale;
        secondaryScaleVelocity = 0f;

        if (vignette != null)
            vignette.mainScale = deathScale;

        if (secondaryVignette != null)
            secondaryVignette.mainScale = secondaryDeathScale;

        yield return new WaitForSeconds(duration);
    }

    public void ResetState(bool immediate = false)
    {
        deathBurstActive = false;
        targetScale = 0f;
        secondaryTargetScale = 0f;

        if (!immediate)
            return;

        currentScale = 0f;
        secondaryCurrentScale = 0f;
        scaleVelocity = 0f;
        secondaryScaleVelocity = 0f;

        if (vignette != null)
            vignette.mainScale = 0f;

        if (secondaryVignette != null)
            secondaryVignette.mainScale = 0f;
    }

    private void ApplyStaticSettings()
    {
        if (vignette == null || !overrideVignetteAppearance)
            return;

        vignette.mainCornerType = cornerType;
        vignette.mainColor = mainColor;
        vignette.stretchMainToScreenRatio = stretchMainToScreen;
    }

    private float GetScaleForHealth(int currentHealth)
    {
        if (currentHealth >= 3)
            return 0f;

        if (currentHealth == 2)
            return secondHealthScale;

        if (currentHealth == 1)
            return firstHealthScale;

        return deathScale;
    }

    private float GetSecondaryScaleForHealth(int currentHealth)
    {
        if (!useSecondaryVignette)
            return 0f;

        if (currentHealth >= 3)
            return 0f;

        if (currentHealth == 2)
            return secondarySecondHealthScale;

        return secondaryFirstHealthScale;
    }

    private Q_Vignette_Single FindSecondaryVignette()
    {
        if (!useSecondaryVignette)
            return null;

        Q_Vignette_Single[] vignettes = FindObjectsOfType<Q_Vignette_Single>(true);
        for (int i = 0; i < vignettes.Length; i++)
        {
            Q_Vignette_Single candidate = vignettes[i];
            if (candidate == null || candidate == vignette)
                continue;

            if (candidate.gameObject.name == secondaryVignetteName)
                return candidate;
        }

        return null;
    }
}
