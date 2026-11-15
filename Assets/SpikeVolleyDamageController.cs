using UnityEngine;

[DisallowMultipleComponent]
public class SpikeVolleyDamageController : MonoBehaviour
{
    [Header("Projectile Names")]
    [SerializeField] private string projectile1Name = "снаряд1";
    [SerializeField] private string projectile2Name = "снаряд2";
    [SerializeField] private string projectile3Name = "снаряд3";

    private SpikeProjectileDamageState projectile1;
    private SpikeProjectileDamageState projectile2;
    private SpikeProjectileDamageState projectile3;

    private void Awake()
    {
        projectile1 = FindOrCreateProjectileState(projectile1Name);
        projectile2 = FindOrCreateProjectileState(projectile2Name);
        projectile3 = FindOrCreateProjectileState(projectile3Name);
    }

    public void ResetDamageStates()
    {
        EnableAllDamage();
    }

    public void EnableAllDamage()
    {
        projectile1?.EnableDamage();
        projectile2?.EnableDamage();
        projectile3?.EnableDamage();
    }

    public void DisableAllDamage()
    {
        projectile1?.DisableDamage();
        projectile2?.DisableDamage();
        projectile3?.DisableDamage();
    }

    public void EnableProjectile1Damage() => projectile1?.EnableDamage();
    public void DisableProjectile1Damage() => projectile1?.DisableDamage();

    public void EnableProjectile2Damage() => projectile2?.EnableDamage();
    public void DisableProjectile2Damage() => projectile2?.DisableDamage();

    public void EnableProjectile3Damage() => projectile3?.EnableDamage();
    public void DisableProjectile3Damage() => projectile3?.DisableDamage();

    private SpikeProjectileDamageState FindOrCreateProjectileState(string projectileName)
    {
        if (string.IsNullOrWhiteSpace(projectileName))
            return null;

        Transform projectileTransform = FindInChildren(transform, projectileName);
        if (projectileTransform == null)
            return null;

        SpikeProjectileDamageState state = projectileTransform.GetComponent<SpikeProjectileDamageState>();
        if (state == null)
            state = projectileTransform.gameObject.AddComponent<SpikeProjectileDamageState>();

        return state;
    }

    private static Transform FindInChildren(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindInChildren(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}

[DisallowMultipleComponent]
public class SpikeProjectileDamageState : MonoBehaviour
{
    [SerializeField] private bool disableDamageOnPlayerHit = true;
    [SerializeField] private bool damageEnabledOnStart = true;
    [SerializeField] private bool toggleCollidersWithDamage = true;

    private DamageSource[] damageSources;
    private Collider2D[] colliders;

    private void Awake()
    {
        CacheComponents();
        SetDamageEnabled(damageEnabledOnStart);
    }

    private void OnEnable()
    {
        if (damageSources == null || damageSources.Length == 0 || colliders == null || colliders.Length == 0)
            CacheComponents();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!disableDamageOnPlayerHit || other == null || !other.CompareTag("Player"))
            return;

        DisableDamage();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!disableDamageOnPlayerHit || collision == null || collision.collider == null || !collision.collider.CompareTag("Player"))
            return;

        DisableDamage();
    }

    public void EnableDamage()
    {
        SetDamageEnabled(true);
    }

    public void DisableDamage()
    {
        SetDamageEnabled(false);
    }

    public void SetDamageEnabled(bool enabled)
    {
        if (damageSources == null || damageSources.Length == 0 || colliders == null || colliders.Length == 0)
            CacheComponents();

        for (int i = 0; i < damageSources.Length; i++)
        {
            if (damageSources[i] != null)
                damageSources[i].enabled = enabled;
        }

        if (!toggleCollidersWithDamage)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private void CacheComponents()
    {
        damageSources = GetComponentsInChildren<DamageSource>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }
}
