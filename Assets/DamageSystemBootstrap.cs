using UnityEngine;
using UnityEngine.SceneManagement;

public class DamageSystemBootstrap : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invulnerabilityDuration = 0.8f;
    [SerializeField] private float deathBurstDuration = 0.45f;
    [SerializeField] private float deathBurstScale = 5f;

    [Header("Damage Vignette")]
    [SerializeField] private int vignetteCornerType = 1;
    [SerializeField] private Color vignetteColor = Color.black;
    [SerializeField] private bool stretchMainToScreen = false;
    [SerializeField] private float vignetteTransitionDuration = 1.15f;
    [SerializeField] private float secondHealthScale = 3f;
    [SerializeField] private float firstHealthScale = 4f;
    [SerializeField] private float secondHealthPulseAmplitude = 0.03f;
    [SerializeField] private float firstHealthPulseAmplitude = 0.08f;
    [SerializeField] private float vignettePulseSpeed = 1.3f;

    [Header("Player Flash")]
    [SerializeField] private Color flashColor = new Color(0.56f, 0.58f, 0.78f, 1f);
    [SerializeField] private float flashAlphaMultiplier = 0.65f;
    [SerializeField] private int flashCount = 3;
    [SerializeField] private float flashOnDuration = 0.06f;
    [SerializeField] private float flashOffDuration = 0.05f;

    [Header("Underground Roots")]
    [SerializeField] private int undergroundRootDamage = 2;
    [SerializeField] private Vector2 undergroundRootKnockback = new Vector2(-12f, 0f);
    [SerializeField] private string[] undergroundHitboxNames =
    {
        "1_корень",
        "2_корень",
        "3_корень",
        "4_корень"
    };

    private FailManager failManager;
    private bool setupComplete = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToAllFailManagers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToAllFailManagers();
    }

    private static void AttachToAllFailManagers()
    {
        FailManager[] failManagers = FindObjectsOfType<FailManager>(true);
        foreach (FailManager manager in failManagers)
        {
            if (manager == null)
                continue;

            DamageSystemBootstrap bootstrap = manager.GetComponent<DamageSystemBootstrap>();
            if (bootstrap == null)
                bootstrap = manager.gameObject.AddComponent<DamageSystemBootstrap>();

            bootstrap.TrySetup();
        }
    }

    private void Awake()
    {
        TrySetup();
    }

    public void TrySetup()
    {
        failManager = GetComponent<FailManager>();
        if (failManager == null || failManager.player == null)
            return;

        setupComplete = true;

        PlayerHitFlash hitFlash = failManager.player.GetComponent<PlayerHitFlash>();
        if (hitFlash == null)
        {
            hitFlash = failManager.player.AddComponent<PlayerHitFlash>();
            hitFlash.Configure(
                flashColor,
                flashAlphaMultiplier,
                flashCount,
                flashOnDuration,
                flashOffDuration);
        }

        DamageVignetteController vignetteController = SetupVignette();

        PlayerHealth playerHealth = failManager.player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = failManager.player.AddComponent<PlayerHealth>();
            playerHealth.Configure(
                failManager,
                vignetteController,
                hitFlash,
                maxHealth,
                invulnerabilityDuration,
                deathBurstDuration,
                deathBurstScale);
        }
        else
        {
            playerHealth.BindDependencies(
                failManager,
                vignetteController,
                hitFlash);
        }

        SetupUndergroundDamageSources();
    }

    private DamageVignetteController SetupVignette()
    {
        Q_Vignette_Single[] vignettes = FindObjectsOfType<Q_Vignette_Single>(true);
        Q_Vignette_Single vignette = null;

        for (int i = 0; i < vignettes.Length; i++)
        {
            if (vignettes[i] == null)
                continue;

            if (vignettes[i].gameObject.name == "Q_Vignette_Single")
            {
                vignette = vignettes[i];
                break;
            }
        }

        if (vignette == null)
        {
            for (int i = 0; i < vignettes.Length; i++)
            {
                if (vignettes[i] != null && !vignettes[i].gameObject.name.Contains("(boss)"))
                {
                    vignette = vignettes[i];
                    break;
                }
            }
        }

        if (vignette == null)
            return null;

        DamageVignetteController controller = vignette.GetComponent<DamageVignetteController>();
        if (controller == null)
        {
            controller = vignette.gameObject.AddComponent<DamageVignetteController>();
            controller.ConfigureDynamic(
                vignetteTransitionDuration,
                secondHealthScale,
                firstHealthScale,
                deathBurstScale,
                secondHealthPulseAmplitude,
                firstHealthPulseAmplitude,
                vignettePulseSpeed);

            controller.ResetState(true);
        }

        return controller;
    }

    private void SetupUndergroundDamageSources()
    {
        for (int i = 0; i < undergroundHitboxNames.Length; i++)
        {
            GameObject hitbox = GameObject.Find(undergroundHitboxNames[i]);
            if (hitbox == null)
                continue;

            DamageSource damageSource = hitbox.GetComponent<DamageSource>();
            if (damageSource == null)
            {
                damageSource = hitbox.AddComponent<DamageSource>();
                damageSource.Configure(undergroundRootDamage, false, undergroundRootKnockback);
            }
        }
    }

    private void OnEnable()
    {
        if (!setupComplete)
            TrySetup();
    }
}
