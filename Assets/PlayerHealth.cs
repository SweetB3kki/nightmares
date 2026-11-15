using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invulnerabilityDuration = 0.8f;
    [SerializeField] private float deathBurstDuration = 0.45f;
    [SerializeField] private float deathBurstScale = 5f;
    [SerializeField] private float knockbackControlBlockDuration = 0.22f;

    private int currentHealth;
    private float invulnerableUntilTime = -1f;
    private bool deathSequenceActive = false;

    private Rigidbody2D rb;
    private FailManager failManager;
    private DamageVignetteController damageVignette;
    private PlayerHitFlash hitFlash;
    private PlayerController playerController;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvulnerable => Time.time < invulnerableUntilTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
        currentHealth = maxHealth;
    }

    public void Configure(
        FailManager manager,
        DamageVignetteController vignetteController,
        PlayerHitFlash flashController,
        int configuredMaxHealth,
        float configuredInvulnerabilityDuration,
        float configuredDeathBurstDuration,
        float configuredDeathBurstScale)
    {
        failManager = manager;
        damageVignette = vignetteController;
        hitFlash = flashController;

        maxHealth = Mathf.Max(1, configuredMaxHealth);
        invulnerabilityDuration = Mathf.Max(0f, configuredInvulnerabilityDuration);
        deathBurstDuration = Mathf.Max(0f, configuredDeathBurstDuration);
        deathBurstScale = Mathf.Max(0f, configuredDeathBurstScale);

        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        damageVignette?.SetStageForHealth(currentHealth, true);
    }

    public void BindDependencies(
        FailManager manager,
        DamageVignetteController vignetteController,
        PlayerHitFlash flashController)
    {
        failManager = manager;
        damageVignette = vignetteController;
        hitFlash = flashController;
    }

    public bool TryTakeDamage(int amount, Vector2 knockback, bool instantKill = false)
    {
        if (deathSequenceActive)
            return false;

        if (failManager != null && failManager.IsFailInProgress)
            return false;

        if (!instantKill && IsInvulnerable)
            return false;

        if (instantKill)
        {
            currentHealth = 0;
            StartDeathSequence(knockback);
            return true;
        }

        amount = Mathf.Max(1, amount);
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

        ApplyKnockback(knockback);
        hitFlash?.PlayHitFlash();

        if (currentHealth > 0)
        {
            invulnerableUntilTime = Time.time + invulnerabilityDuration;
            damageVignette?.SetStageForHealth(currentHealth);
            return true;
        }

        StartDeathSequence(knockback);
        return true;
    }

    public void ResetAfterRespawn()
    {
        StopAllCoroutines();

        deathSequenceActive = false;
        currentHealth = maxHealth;
        invulnerableUntilTime = Time.time + 0.1f;

        damageVignette?.ResetState(true);
        damageVignette?.SetStageForHealth(currentHealth, true);
        hitFlash?.ResetVisuals();
    }

    public void RestoreFullHealth(bool smoothVignetteReset = true)
    {
        if (deathSequenceActive)
            return;

        currentHealth = maxHealth;
        invulnerableUntilTime = Time.time + 0.1f;

        if (smoothVignetteReset)
            damageVignette?.SetStageForHealth(currentHealth);
        else
            damageVignette?.SetStageForHealth(currentHealth, true);

        hitFlash?.ResetVisuals();
    }

    public void FadeOutDamageVignette()
    {
        if (deathSequenceActive)
            return;

        damageVignette?.SetStageForHealth(maxHealth);
        hitFlash?.ResetVisuals();
    }

    private void StartDeathSequence(Vector2 knockback)
    {
        if (deathSequenceActive)
            return;

        deathSequenceActive = true;
        invulnerableUntilTime = Time.time + deathBurstDuration;

        ApplyKnockback(knockback);
        playerController?.LockAllControl(deathBurstDuration + 0.2f);
        damageVignette?.SetDeathScale(deathBurstScale);
        StartCoroutine(HandleDeathSequence());
    }

    private IEnumerator HandleDeathSequence()
    {
        if (damageVignette != null)
            yield return damageVignette.PlayDeathBurst(deathBurstDuration);
        else
            yield return new WaitForSeconds(deathBurstDuration);

        FailManager manager = failManager;
        if (manager == null)
            manager = FailManager.Instance;

        if (manager != null)
        {
            failManager = manager;
            manager.TryTriggerFail();
        }
        else
        {
            Debug.LogError("PlayerHealth could not resolve FailManager during death sequence.");
        }
    }

    private void ApplyKnockback(Vector2 knockback)
    {
        if (rb == null || knockback == Vector2.zero)
            return;

        playerController?.ApplyExternalKnockback(knockback.x, knockbackControlBlockDuration, true);
        if (Mathf.Abs(knockback.x) > 0.01f)
        {
            float positionalNudge = Mathf.Clamp(Mathf.Abs(knockback.x) * 0.02f, 0.08f, 0.3f);
            rb.position += new Vector2(Mathf.Sign(knockback.x) * positionalNudge, 0f);
        }

        rb.velocity = new Vector2(knockback.x, rb.velocity.y);
    }
}
