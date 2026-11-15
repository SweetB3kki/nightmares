using UnityEngine;

public class BossVisionController : MonoBehaviour
{
    public static BossVisionController Instance;

    public static bool isLooking = false;

    [Header("Danger Timing")]
    public float growthDuration = 2f;
    public float recoveryDuration = 0.75f;
    public float maxDangerScale = 5f;

    [Header("Boss Vignette")]
    public Q_Vignette_Single bossVignette;
    public int bossCornerType = 1;
    public Color bossVignetteColor = Color.black;
    public bool bossStretchMainToScreen = false;

    [Header("Boss Pulse")]
    public bool enablePulse = true;
    public float pulseStartScale = 3.5f;
    public float pulseAmplitude = 0.06f;
    public float pulseSpeed = 1.5f;

    private float currentDangerScale = 0f;

    public float CurrentDangerScale => currentDangerScale;

    private void Awake()
    {
        Instance = this;

        if (bossVignette == null)
            bossVignette = FindBossVignette();

        ApplyBossVignetteAppearance();
        SetBossVignetteScaleImmediate(0f);
    }

    private void Update()
    {
        if (bossVignette == null)
            return;

        ApplyBossVignetteAppearance();

        bool fightActive = GameState.bossFightStarted && !GameState.bossCompleted;
        bool playerHiding = PlayerStealth.Instance != null && PlayerStealth.Instance.isHiding;
        bool shouldGrow = fightActive && isLooking && !playerHiding;

        float duration = shouldGrow ? growthDuration : recoveryDuration;
        float moveSpeed = maxDangerScale / Mathf.Max(0.01f, duration);
        float targetScale = shouldGrow ? maxDangerScale : 0f;
        currentDangerScale = Mathf.MoveTowards(currentDangerScale, targetScale, moveSpeed * Time.deltaTime);

        float pulse = 0f;
        if (enablePulse && currentDangerScale >= pulseStartScale && currentDangerScale > 0f)
            pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) * pulseAmplitude;

        bossVignette.mainScale = Mathf.Clamp(currentDangerScale + pulse, 0f, maxDangerScale);
    }

    private void OnDisable()
    {
        if (Instance == this)
            ForceResetVision();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            ForceResetVision();
    }

    public void BossEyeState(bool looking)
    {
        bool startedLookingNow = looking && !isLooking;
        isLooking = looking;

        if (startedLookingNow)
            CameraShaker.Instance?.Shake();
    }

    public void ResetDangerTimer()
    {
        currentDangerScale = 0f;
        SetBossVignetteScaleImmediate(0f);
    }

    public void SetVignetteVisible(bool visible)
    {
        if (!visible && !isLooking)
            SetBossVignetteScaleImmediate(0f);
    }

    public static bool IsLethalNow()
    {
        if (Instance == null)
            return false;

        if (!GameState.bossFightStarted || GameState.bossCompleted)
            return false;

        return Instance.currentDangerScale >= Instance.maxDangerScale - 0.001f;
    }

    public static void ForceResetVision()
    {
        isLooking = false;

        if (Instance == null)
            return;

        Instance.currentDangerScale = 0f;
        Instance.SetBossVignetteScaleImmediate(0f);
    }

    private void ApplyBossVignetteAppearance()
    {
        if (bossVignette == null)
            return;

        bossVignette.mainCornerType = bossCornerType;
        bossVignette.mainColor = bossVignetteColor;
        bossVignette.stretchMainToScreenRatio = bossStretchMainToScreen;
    }

    private void SetBossVignetteScaleImmediate(float scale)
    {
        currentDangerScale = Mathf.Clamp(scale, 0f, maxDangerScale);

        if (bossVignette != null)
            bossVignette.mainScale = currentDangerScale;
    }

    private Q_Vignette_Single FindBossVignette()
    {
        Q_Vignette_Single[] vignettes = FindObjectsOfType<Q_Vignette_Single>(true);
        foreach (Q_Vignette_Single vignette in vignettes)
        {
            if (vignette != null && vignette.gameObject.name.Contains("(boss)"))
                return vignette;
        }

        return null;
    }
}
