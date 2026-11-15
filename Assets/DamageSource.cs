using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageSource : MonoBehaviour
{
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private bool instantKill = false;
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private Vector2 knockback = new Vector2(-12f, 0f);

    public void Configure(int configuredDamageAmount, bool configuredInstantKill, Vector2 configuredKnockback, bool configuredApplyKnockback = true)
    {
        damageAmount = Mathf.Max(1, configuredDamageAmount);
        instantKill = configuredInstantKill;
        applyKnockback = configuredApplyKnockback;
        knockback = configuredKnockback;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        playerHealth.TryTakeDamage(damageAmount, applyKnockback ? knockback : Vector2.zero, instantKill);
    }
}
