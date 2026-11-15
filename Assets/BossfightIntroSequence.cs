using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BossfightIntroSequence : MonoBehaviour
{
    public static BossfightIntroSequence Instance { get; private set; }

    [Header("Scene Object Names")]
    [SerializeField] private string followCameraName = "VCam_Follow";
    [SerializeField] private string bossRevealCameraName = "VCam_BossReveal";
    [SerializeField] private string arenaCameraName = "VCam_Arena";
    [SerializeField] private string frameObjectName = "FRAME_BOSSFIGHT";
    [SerializeField] private string introWalkTargetName = "IntroWalkTarget";
    [SerializeField] private string blockObjectName = "block";
    [SerializeField] private string bossObjectName = "БОСС";
    [SerializeField] private string projectileAnimatorObjectName = "снаряды";
    [SerializeField] private string bossDissolveObjectName = "BossDissolve";
    [SerializeField] private string worldDissolveObjectName = "WorldDissolve";
    [SerializeField] private string mainSparkObjectName = "mainspark";
    [SerializeField] private string bonusSparkObjectName = "bonusspark";

    [Header("Sequence Timings")]
    [SerializeField] private float autoWalkSpeed = 8f;
    [SerializeField] private float cameraBlendDuration = 0.65f;
    [SerializeField] private float revealMoveDuration = 0.65f;
    [SerializeField] private float revealHoldDuration = 1.2f;
    [SerializeField] private float arenaMoveDuration = 0.75f;
    [SerializeField] private float bossStartDelay = 0.15f;
    [SerializeField] private float projectileVolleyDurationOverride = -1f;
    [SerializeField] private float postVolleySettleDelay = 0.05f;
    [SerializeField] private float outroCameraToBossDuration = 0.8f;
    [SerializeField] private float bossDissolveDuration = 2.7f;
    [SerializeField] private float postBossDissolvePanDuration = 0.55f;
    [SerializeField] private float postBossDissolveCameraY = -7f;
    [SerializeField] private float mainSparkFadeDuration = 0.75f;
    [SerializeField] private float outroCameraReturnDuration = 0.8f;
    [SerializeField] private float vignetteOutroWaitTimeout = 2.5f;
    [SerializeField] private float worldReturnDissolveDuration = 5f;
    [SerializeField] private float worldReturnZoomedOrthographicSize = 13.5f;
    [SerializeField] private float worldReturnFlashStartProgress = 0.82f;
    [SerializeField] private float worldReturnFlashDuration = 0.2f;
    [SerializeField] private float worldReturnFlashHoldDuration = 0.08f;
    [SerializeField] private float labyrinthReturnFlashDuration = 0.9f;
    [SerializeField] private float labyrinthReturnDissolveDuration = 5f;
    [SerializeField] private float labyrinthReturnDissolveStartProgress = 0.9f;

    [Header("Return To Labyrinth")]
    [SerializeField] private string labyrinthSceneName = "labirint";
    [SerializeField] private string labyrinthPlayerObjectName = "PLAYERL";
    [SerializeField] private string labyrinthEncounterObjectName = "BossEncounterTrigger";
    [SerializeField] private float worldReturnShakeAmplitude = 0.18f;
    [SerializeField] private float worldReturnShakeFrequency = 16f;

    [Header("Bossfight Player Stats")]
    [SerializeField] private float fallbackBossfightSpeed = 12f;
    [SerializeField] private float fallbackBossfightJumpForce = 27f;

    [Header("Camera Priorities")]
    [SerializeField] private int followPriority = 20;
    [SerializeField] private int revealPriority = 30;
    [SerializeField] private int arenaPriority = 40;

    private CinemachineVirtualCamera followCamera;
    private CinemachineVirtualCamera bossRevealCamera;
    private CinemachineVirtualCamera arenaCamera;
    private Collider2D frameBounds;
    private Transform introWalkTarget;
    private GameObject blockObject;
    private SpriteDissolveController bossDissolveController;
    private BossStagedDestructionController bossStagedDestructionController;
    private SpriteDissolveController worldDissolveController;
    private GameObject mainSparkObject;
    private GameObject bonusSparkObject;
    private GameObject bossObject;
    private Animator bossAnimator;
    private Animator projectileAnimator;
    private SpikeVolleyDamageController spikeDamageController;
    private CinemachineBrain cinemachineBrain;
    private CameraLockY followCameraLockY;
    private PlayerStatModifierZone statModifierZone;
    private FailManager failManager;
    private DamageSystemBootstrap damageSystemBootstrap;
    private PlayerController playerController;
    private Rigidbody2D playerRb;
    private Collider2D playerCollider;
    private Transform playerTransform;
    private SparkCollector mainSparkCollector;
    private Collider2D introTriggerCollider;
    private bool sequenceStarted;
    private bool playerReady;
    private bool returnSequenceStarted;
    private Coroutine damageBootstrapRoutine;
    private Coroutine projectileVolleyRoutine;
    private Coroutine outroRoutine;
    private Vector3 bossRevealCameraStartPosition;
    private float followCameraStartOrthographicSize;

    private void Awake()
    {
        Instance = this;
        ResolveSceneReferences();
        ApplyInitialState();
    }

    private void OnEnable()
    {
        SparkCollector.SparkCollected += HandleSparkCollected;
    }

    private void OnDestroy()
    {
        SparkCollector.SparkCollected -= HandleSparkCollected;

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        StartCoroutine(BindPlayerRoutine());
    }

    private void Update()
    {
        if (sequenceStarted || !playerReady || introTriggerCollider == null || playerCollider == null)
            return;

        if (!introTriggerCollider.IsTouching(playerCollider))
            return;

        BeginSequence();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (sequenceStarted || !playerReady || !other.CompareTag("Player"))
            return;

        BeginSequence();
    }

    private void ResolveSceneReferences()
    {
        followCamera = FindVirtualCamera(followCameraName);
        bossRevealCamera = FindVirtualCamera(bossRevealCameraName);
        arenaCamera = FindVirtualCamera(arenaCameraName);

        GameObject frameObject = FindSceneObjectIncludingInactive(frameObjectName);
        frameBounds = frameObject != null ? frameObject.GetComponent<Collider2D>() : null;

        GameObject walkTargetObject = FindSceneObjectIncludingInactive(introWalkTargetName);
        introWalkTarget = walkTargetObject != null ? walkTargetObject.transform : null;

        blockObject = FindSceneObjectIncludingInactive(blockObjectName);
        GameObject bossDissolveObject = FindSceneObjectIncludingInactive(bossDissolveObjectName);
        bossDissolveController = bossDissolveObject != null ? bossDissolveObject.GetComponent<SpriteDissolveController>() : null;
        if (bossDissolveObject != null)
        {
            bossStagedDestructionController = bossDissolveObject.GetComponent<BossStagedDestructionController>();
            if (bossStagedDestructionController == null)
                bossStagedDestructionController = bossDissolveObject.AddComponent<BossStagedDestructionController>();

            bossStagedDestructionController.BindSource(bossDissolveController);
            bossStagedDestructionController.ForceRebuild();
        }
        else
        {
            bossStagedDestructionController = null;
        }
        GameObject worldDissolveObject = FindSceneObjectIncludingInactive(worldDissolveObjectName);
        worldDissolveController = worldDissolveObject != null ? worldDissolveObject.GetComponent<SpriteDissolveController>() : null;
        mainSparkObject = FindSceneObjectIncludingInactive(mainSparkObjectName);
        mainSparkCollector = mainSparkObject != null ? mainSparkObject.GetComponent<SparkCollector>() : null;
        bonusSparkObject = FindSceneObjectIncludingInactive(bonusSparkObjectName);

        bossObject = FindSceneObjectIncludingInactive(bossObjectName);
        bossAnimator = bossObject != null ? bossObject.GetComponent<Animator>() : null;

        GameObject projectileAnimatorObject = FindSceneObjectIncludingInactive(projectileAnimatorObjectName);
        projectileAnimator = projectileAnimatorObject != null ? projectileAnimatorObject.GetComponent<Animator>() : null;
        spikeDamageController = projectileAnimatorObject != null ? projectileAnimatorObject.GetComponent<SpikeVolleyDamageController>() : null;
        introTriggerCollider = GetComponent<Collider2D>();

        statModifierZone = GetComponent<PlayerStatModifierZone>();
        failManager = FindObjectOfType<FailManager>(true);
        damageSystemBootstrap = failManager != null ? failManager.GetComponent<DamageSystemBootstrap>() : null;
        cinemachineBrain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;

        if (bossRevealCamera != null)
            bossRevealCameraStartPosition = bossRevealCamera.transform.position;

        if (followCamera != null)
            followCameraStartOrthographicSize = followCamera.m_Lens.OrthographicSize;

        EnsureConfiner(followCamera);
        EnsureConfiner(bossRevealCamera);
        RefreshConfiner(followCamera);
        RefreshConfiner(bossRevealCamera);
        EnsureFollowCameraLockY();
    }

    private void ApplyBossfightStats()
    {
        if (playerController == null)
            return;

        if (statModifierZone != null)
        {
            playerController.speed = statModifierZone.newSpeed;
            playerController.jumpForce = statModifierZone.newJumpForce;
            return;
        }

        playerController.speed = fallbackBossfightSpeed;
        playerController.jumpForce = fallbackBossfightJumpForce;
    }

    private void ApplyInitialState()
    {
        if (blockObject != null)
            blockObject.SetActive(false);

        if (bossAnimator != null)
            bossAnimator.enabled = false;

        if (projectileAnimator != null)
            projectileAnimator.enabled = false;

        spikeDamageController?.ResetDamageStates();
        ResetOutroObjects();

        GameState.bossFightStarted = false;
        GameState.isSceneTransitioning = false;

        SetInitialCameraPriorities();
        SetBrainBlendTime(0f);
        RestoreBossfightSpriteVisibility();
    }

    private IEnumerator BindPlayerRoutine()
    {
        while (playerTransform == null)
        {
            GameObject playerObject = FindActivePlayerRoot();
            if (playerObject != null)
            {
                playerObject = ResolvePlayerRoot(playerObject);
                playerTransform = playerObject.transform;
                playerController = playerObject.GetComponent<PlayerController>();
                playerRb = playerObject.GetComponent<Rigidbody2D>();
                playerCollider = playerObject.GetComponent<Collider2D>();
                if (playerCollider == null)
                    playerCollider = playerObject.GetComponentInChildren<Collider2D>(true);
                playerReady = true;
                ApplyBossfightStats();
                ForceVisibleSpriteRenderers(playerObject);
                if (projectileAnimator != null)
                    ForceVisibleSpriteRenderers(projectileAnimator.gameObject);

        if (followCamera != null)
        {
            followCamera.Follow = playerTransform;
            followCamera.LookAt = playerTransform;
        }

                RefreshConfiner(followCamera);
                RefreshConfiner(bossRevealCamera);
                BindBossfightDamageFlow(playerObject);
            }

            yield return null;
        }
    }

    private void BeginSequence()
    {
        if (sequenceStarted)
            return;

        ApplyBossfightStats();
        sequenceStarted = true;
        StartCoroutine(PlaySequenceRoutine());
    }

    private void HandleSparkCollected(SparkCollector spark)
    {
        if (spark == null || returnSequenceStarted || !GameState.bossCompleted)
            return;

        if (mainSparkCollector != null)
        {
            if (spark != mainSparkCollector)
                return;
        }
        else if (mainSparkObject != null && spark.gameObject != mainSparkObject)
        {
            return;
        }

        returnSequenceStarted = true;
        StartCoroutine(PlayBossReturnRoutine());
    }

    private void BindBossfightDamageFlow(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        if (failManager == null)
            failManager = FindObjectOfType<FailManager>(true);

        if (failManager == null)
            return;

        if (damageBootstrapRoutine != null)
            StopCoroutine(damageBootstrapRoutine);

        damageBootstrapRoutine = StartCoroutine(EnsureDamageBootstrapRoutine(playerObject));
    }

    private IEnumerator EnsureDamageBootstrapRoutine(GameObject playerObject)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (failManager == null)
                failManager = FindObjectOfType<FailManager>(true);

            if (failManager != null && playerObject != null)
            {
                failManager.player = playerObject;
                failManager.UpdateCheckpoint(playerObject.transform.position);

                if (damageSystemBootstrap == null)
                    damageSystemBootstrap = failManager.GetComponent<DamageSystemBootstrap>();

                if (damageSystemBootstrap == null)
                    damageSystemBootstrap = failManager.gameObject.AddComponent<DamageSystemBootstrap>();

                damageSystemBootstrap.TrySetup();

                PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
                PlayerHitFlash hitFlash = playerObject.GetComponent<PlayerHitFlash>();
                DamageVignetteController damageVignette = FindObjectOfType<DamageVignetteController>(true);
                if (playerHealth != null)
                    playerHealth.BindDependencies(failManager, damageVignette, hitFlash);

                if (playerObject.GetComponent<PlayerHealth>() != null &&
                    FindObjectOfType<DamageVignetteController>(true) != null)
                {
                    damageBootstrapRoutine = null;
                    yield break;
                }
            }

            yield return null;
        }

        damageBootstrapRoutine = null;
    }

    private IEnumerator PlaySequenceRoutine()
    {
        float totalLockDuration = revealMoveDuration + revealHoldDuration + arenaMoveDuration + bossStartDelay + 0.6f;
        GameState.isSceneTransitioning = true;

        if (playerController != null)
            playerController.LockAllControl(totalLockDuration);

        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.simulated = false;
        }

        yield return MovePlayerToIntroTarget();

        if (blockObject != null)
            blockObject.SetActive(true);

        SetBrainBlendTime(cameraBlendDuration);

        if (bossRevealCamera != null)
        {
            bossRevealCamera.Priority = revealPriority;
            if (followCamera != null)
                followCamera.Priority = 5;
            if (arenaCamera != null)
                arenaCamera.Priority = 10;
        }

        yield return new WaitForSeconds(revealMoveDuration);
        yield return new WaitForSeconds(revealHoldDuration);

        if (arenaCamera != null)
        {
            arenaCamera.Priority = arenaPriority;
            if (bossRevealCamera != null)
                bossRevealCamera.Priority = 10;
            if (followCamera != null)
                followCamera.Priority = 5;
        }

        yield return new WaitForSeconds(arenaMoveDuration);
        yield return new WaitForSeconds(bossStartDelay);

        if (bossAnimator != null)
            bossAnimator.enabled = true;

        if (projectileAnimator != null)
            projectileAnimator.enabled = true;

        spikeDamageController?.ResetDamageStates();

        if (playerRb != null)
        {
            playerRb.simulated = true;
            playerRb.velocity = Vector2.zero;
        }

        playerController?.RefreshPresentation(true);

        GameState.bossFightStarted = true;
        GameState.isSceneTransitioning = false;

        if (projectileVolleyRoutine != null)
            StopCoroutine(projectileVolleyRoutine);

        projectileVolleyRoutine = StartCoroutine(HandleProjectileVolleyRoutine());
    }

    private IEnumerator MovePlayerToIntroTarget()
    {
        if (playerTransform == null || introWalkTarget == null)
            yield break;

        Vector3 targetPosition = new Vector3(introWalkTarget.position.x, playerTransform.position.y, playerTransform.position.z);
        Animator playerAnimator = playerController != null ? playerController.anim : null;

        playerController?.SetPresentationRunningOverride(true);

        while (Mathf.Abs(playerTransform.position.x - targetPosition.x) > 0.05f)
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                playerTransform.position,
                targetPosition,
                autoWalkSpeed * Time.deltaTime);

            playerTransform.position = nextPosition;
            yield return null;
        }

        playerTransform.position = targetPosition;

        playerController?.SetPresentationRunningOverride(false);

        if (playerRb != null)
            playerRb.position = targetPosition;

        playerController?.RefreshPresentation(true);
    }

    private void SetInitialCameraPriorities()
    {
        if (followCamera != null)
            followCamera.Priority = followPriority;

        if (bossRevealCamera != null)
            bossRevealCamera.Priority = 10;

        if (arenaCamera != null)
            arenaCamera.Priority = 9;
    }

    public void ResetEncounterState()
    {
        StopAllCoroutines();

        if (damageBootstrapRoutine != null)
        {
            StopCoroutine(damageBootstrapRoutine);
            damageBootstrapRoutine = null;
        }

        if (outroRoutine != null)
        {
            StopCoroutine(outroRoutine);
            outroRoutine = null;
        }

        projectileVolleyRoutine = null;
        returnSequenceStarted = false;

        sequenceStarted = false;
        GameState.bossFightStarted = false;
        GameState.bossCompleted = false;
        GameState.isSceneTransitioning = false;

        if (blockObject != null)
            blockObject.SetActive(false);

        ResetAnimatorState(bossAnimator);
        ResetAnimatorState(projectileAnimator);
        spikeDamageController?.ResetDamageStates();
        ResetOutroObjects();

        SetInitialCameraPriorities();
        SetBrainBlendTime(0f);

        if (followCamera != null && playerTransform != null)
        {
            followCamera.Follow = playerTransform;
            followCamera.m_Lens.OrthographicSize = followCameraStartOrthographicSize;
        }

        if (bossRevealCamera != null)
            bossRevealCamera.transform.position = bossRevealCameraStartPosition;

        RefreshConfiner(followCamera);
        RefreshConfiner(bossRevealCamera);
        EnsureFollowCameraLockY();

        if (playerRb != null)
        {
            playerRb.simulated = true;
            playerRb.velocity = Vector2.zero;
        }

        playerController?.RefreshPresentation(true);

        if (playerTransform != null)
            BindBossfightDamageFlow(playerTransform.gameObject);
    }

    private IEnumerator HandleProjectileVolleyRoutine()
    {
        float volleyDuration = GetProjectileVolleyDuration();
        if (volleyDuration <= 0f)
        {
            projectileVolleyRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(volleyDuration + Mathf.Max(0f, postVolleySettleDelay));

        PlayerHealth playerHealth = playerTransform != null ? playerTransform.GetComponent<PlayerHealth>() : null;
        bool playerSurvived = playerHealth != null &&
            playerHealth.CurrentHealth > 0 &&
            failManager != null &&
            !failManager.IsFailInProgress;

        if (!playerSurvived)
        {
            projectileVolleyRoutine = null;
            yield break;
        }

        if (projectileAnimator != null)
            projectileAnimator.enabled = false;

        spikeDamageController?.DisableAllDamage();
        playerHealth.FadeOutDamageVignette();
        GameState.bossFightStarted = false;
        BossVisionController.ForceResetVision();

        if (outroRoutine != null)
            StopCoroutine(outroRoutine);

        outroRoutine = StartCoroutine(PlayBossOutroRoutine(playerHealth));
        projectileVolleyRoutine = null;
    }

    private float GetProjectileVolleyDuration()
    {
        if (projectileVolleyDurationOverride > 0f)
            return projectileVolleyDurationOverride;

        if (projectileAnimator == null || projectileAnimator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] clips = projectileAnimator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return 0f;

        float maxClipLength = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.length > maxClipLength)
                maxClipLength = clip.length;
        }

        float animatorSpeed = projectileAnimator != null ? Mathf.Max(0.01f, projectileAnimator.speed) : 1f;
        return maxClipLength / animatorSpeed;
    }

    private IEnumerator PlayBossOutroRoutine(PlayerHealth playerHealth)
    {
        float forcedLockDuration = Mathf.Max(0f, outroCameraToBossDuration) + Mathf.Max(0f, bossDissolveDuration) + 0.1f;
        GameState.isSceneTransitioning = true;

        if (playerController != null)
            playerController.LockAllControl(forcedLockDuration);

        if (playerRb != null)
            playerRb.velocity = Vector2.zero;

        yield return WaitForDamageVignetteClear();

        SetBrainBlendTime(outroCameraToBossDuration);
        if (bossRevealCamera != null)
        {
            bossRevealCamera.Priority = arenaPriority + 10;
            if (arenaCamera != null)
                arenaCamera.Priority = 10;
            if (followCamera != null)
                followCamera.Priority = 5;
        }

        yield return new WaitForSeconds(outroCameraToBossDuration);

        if (bossAnimator != null)
            bossAnimator.enabled = false;

        if (bossStagedDestructionController != null)
        {
            if (bossDissolveController != null)
                bossStagedDestructionController.BindSource(bossDissolveController);

            bossStagedDestructionController.ForceRebuild();
        }

        if (bossStagedDestructionController != null && bossStagedDestructionController.IsPrepared)
        {
            yield return bossStagedDestructionController.PlayStagedDestruction(bossDissolveDuration);
        }
        else if (bossDissolveController != null)
        {
            bossDissolveController.RadialProgress = 0f;
            bossDissolveController.DissolveAmount = 0f;

            float elapsed = 0f;
            while (elapsed < bossDissolveDuration)
            {
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossDissolveDuration));
                if (bossDissolveController.UseRadialMask)
                    bossDissolveController.RadialProgress = progress;
                else
                    bossDissolveController.DissolveAmount = progress;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (bossDissolveController.UseRadialMask)
                bossDissolveController.RadialProgress = 1f;
            else
                bossDissolveController.DissolveAmount = 1f;
        }
        else
        {
            yield return new WaitForSeconds(bossDissolveDuration);
        }

        yield return PanBossRevealCameraAfterDissolve();

        GameState.bossCompleted = true;
        SetBossCollidersEnabled(false);

        if (mainSparkObject != null)
            PrepareMainSparkForReveal();

        if (bonusSparkObject != null)
        {
            bonusSparkObject.SetActive(true);
            DisableSparkAnimators(bonusSparkObject);
        }

        if (blockObject != null)
            blockObject.SetActive(false);

        if (mainSparkObject != null)
            yield return FadeMainSparkAlpha(0f, 1f, mainSparkFadeDuration);

        SetBrainBlendTime(outroCameraReturnDuration);
        if (followCamera != null)
        {
            followCamera.Priority = followPriority;
            if (arenaCamera != null)
                arenaCamera.Priority = 9;
            if (bossRevealCamera != null)
                bossRevealCamera.Priority = 10;
        }

        GameState.isSceneTransitioning = false;
        yield return new WaitForSeconds(outroCameraReturnDuration);
        outroRoutine = null;
    }

    private IEnumerator PlayBossReturnRoutine()
    {
        GameState.isSceneTransitioning = true;
        GameState.bossFightStarted = false;

        float controlLockDuration = worldReturnDissolveDuration + worldReturnFlashDuration + worldReturnFlashHoldDuration + labyrinthReturnFlashDuration + labyrinthReturnDissolveDuration + 0.5f;
        if (playerController != null)
            playerController.LockAllControl(controlLockDuration);

        if (playerRb != null)
            playerRb.velocity = Vector2.zero;

        float cameraApproachDuration = Mathf.Max(0.01f, worldReturnDissolveDuration * Mathf.Clamp01(worldReturnFlashStartProgress));
        SetBrainBlendTime(cameraApproachDuration);
        if (bossRevealCamera != null)
        {
            bossRevealCamera.Priority = arenaPriority + 20;
            if (arenaCamera != null)
                arenaCamera.Priority = 9;
            if (followCamera != null)
                followCamera.Priority = 8;
        }

        float startOrtho = bossRevealCamera != null ? bossRevealCamera.m_Lens.OrthographicSize : followCameraStartOrthographicSize;
        Vector3 bossRevealStartPosition = bossRevealCamera != null ? bossRevealCamera.transform.position : Vector3.zero;
        float currentDissolve = 0f;
        if (worldDissolveController != null)
        {
            currentDissolve = worldDissolveController.UseRadialMask
                ? worldDissolveController.RadialProgress
                : worldDissolveController.DissolveAmount;
        }

        float flashStart = Mathf.Clamp01(worldReturnFlashStartProgress);
        float elapsed = 0f;
        while (elapsed < worldReturnDissolveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, worldReturnDissolveDuration));
            float shapedProgress = Mathf.SmoothStep(0f, 1f, progress);

            if (worldDissolveController != null)
            {
                float dissolveValue = Mathf.Lerp(currentDissolve, 1f, shapedProgress);
                if (worldDissolveController.UseRadialMask)
                    worldDissolveController.RadialProgress = dissolveValue;
                else
                    worldDissolveController.DissolveAmount = dissolveValue;
            }

            if (bossRevealCamera != null)
            {
                bossRevealCamera.m_Lens.OrthographicSize = Mathf.Lerp(startOrtho, worldReturnZoomedOrthographicSize, shapedProgress);

                float shakeStrength = Mathf.SmoothStep(0f, 1f, shapedProgress) * worldReturnShakeAmplitude;
                float shakeTime = Time.unscaledTime * worldReturnShakeFrequency;
                Vector3 shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(shakeTime, 0.37f) - 0.5f) * 2f * shakeStrength,
                    (Mathf.PerlinNoise(0.83f, shakeTime) - 0.5f) * 2f * shakeStrength,
                    0f);
                bossRevealCamera.transform.position = bossRevealStartPosition + shakeOffset;
            }

            if (progress >= flashStart)
                break;

            yield return null;
        }

        BossEncounterOverlayUtility.FlashHandle flashOverlay = BossEncounterOverlayUtility.CreateWhiteFlashOverlay();
        yield return BossEncounterOverlayUtility.PlayCenterFlashToSolid(flashOverlay, worldReturnFlashDuration);

        if (worldReturnFlashHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(worldReturnFlashHoldDuration);

        Vector3 returnPosition = GameState.hasLabyrinthReturnPosition
            ? GameState.labyrinthReturnPosition
            : (playerTransform != null ? playerTransform.position : Vector3.zero);

        BossEncounterOverlayUtility.PersistBossfightReturnToLabyrinth(
            flashOverlay,
            labyrinthSceneName,
            returnPosition,
            labyrinthReturnFlashDuration,
            labyrinthReturnDissolveDuration,
            labyrinthReturnDissolveStartProgress,
            labyrinthPlayerObjectName,
            labyrinthEncounterObjectName);

        GameState.isInLabirint = true;
        SceneManager.LoadScene(labyrinthSceneName, LoadSceneMode.Single);
    }

    private IEnumerator WaitForDamageVignetteClear()
    {
        DamageVignetteController damageVignette = FindObjectOfType<DamageVignetteController>(true);
        if (damageVignette == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < vignetteOutroWaitTimeout)
        {
            if (damageVignette.IsVisuallyCleared)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PanBossRevealCameraAfterDissolve()
    {
        if (bossRevealCamera == null || postBossDissolvePanDuration <= 0f)
            yield break;

        Vector3 start = bossRevealCamera.transform.position;
        Vector3 target = new Vector3(start.x, postBossDissolveCameraY, start.z);
        float elapsed = 0f;

        while (elapsed < postBossDissolvePanDuration)
        {
            float t = elapsed / Mathf.Max(0.01f, postBossDissolvePanDuration);
            bossRevealCamera.transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        bossRevealCamera.transform.position = target;
    }

    private void ResetOutroObjects()
    {
        if (bossStagedDestructionController != null)
            bossStagedDestructionController.ResetState();

        if (bossDissolveController != null)
        {
            bossDissolveController.RadialProgress = 0f;
            bossDissolveController.DissolveAmount = 0f;
        }

        if (worldDissolveController != null)
        {
            worldDissolveController.RadialProgress = 0f;
            worldDissolveController.DissolveAmount = 0f;
        }

        if (mainSparkObject != null)
        {
            mainSparkObject.SetActive(false);
            SetMainSparkAlpha(0f);
            SetSparkCollidersEnabled(mainSparkObject, false);
        }

        if (bonusSparkObject != null)
        {
            bonusSparkObject.SetActive(false);
            DisableSparkAnimators(bonusSparkObject);
        }

        SetBossCollidersEnabled(true);

        if (bossRevealCamera != null)
            bossRevealCamera.transform.position = bossRevealCameraStartPosition;
    }

    private static void SetSparkAlpha(GameObject sparkObject, float alpha)
    {
        if (sparkObject == null)
            return;

        SpriteRenderer[] renderers = sparkObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = true;
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private static void SetSparkCollidersEnabled(GameObject sparkObject, bool enabled)
    {
        if (sparkObject == null)
            return;

        Collider2D[] colliders = sparkObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private static void DisableSparkAnimators(GameObject sparkObject)
    {
        if (sparkObject == null)
            return;

        Animator[] animators = sparkObject.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].enabled = false;
        }
    }

    private static void EnableSparkAnimators(GameObject sparkObject)
    {
        if (sparkObject == null)
            return;

        Animator[] animators = sparkObject.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private static void PrepareSparkForReveal(GameObject sparkObject)
    {
        if (sparkObject == null)
            return;

        sparkObject.SetActive(true);
        DisableSparkAnimators(sparkObject);
        SetSparkAlpha(sparkObject, 0f);
        SetSparkCollidersEnabled(sparkObject, false);
    }

    private void PrepareMainSparkForReveal()
    {
        if (mainSparkObject == null)
            return;

        mainSparkObject.SetActive(true);
        SetMainSparkAlpha(0f);
        SetSparkCollidersEnabled(mainSparkObject, false);
    }

    private IEnumerator FadeSparkAlpha(GameObject sparkObject, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(from, to, elapsed / Mathf.Max(0.01f, duration));
            SetSparkAlpha(sparkObject, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetSparkAlpha(sparkObject, to);
        SetSparkCollidersEnabled(sparkObject, true);
    }

    private IEnumerator FadeMainSparkAlpha(float from, float to, float duration)
    {
        if (mainSparkObject == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(from, to, elapsed / Mathf.Max(0.01f, duration));
            SetMainSparkAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetMainSparkAlpha(to);
        SetSparkCollidersEnabled(mainSparkObject, true);
    }

    private void SetMainSparkAlpha(float alpha)
    {
        if (mainSparkObject == null)
            return;

        SpriteRenderer renderer = mainSparkObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.enabled = true;
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private void RestoreBossfightSpriteVisibility()
    {
        if (projectileAnimator != null)
            ForceVisibleSpriteRenderers(projectileAnimator.gameObject);

        if (playerTransform != null)
            ForceVisibleSpriteRenderers(playerTransform.gameObject);
    }

    private static void ForceVisibleSpriteRenderers(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        SpriteRenderer[] renderers = rootObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = true;
            Color color = renderer.color;
            if (color.a < 1f)
            {
                color.a = 1f;
                renderer.color = color;
            }
        }
    }

    private static void TriggerSparkFallAnimation(GameObject sparkObject)
    {
        if (sparkObject == null)
            return;

        Animator[] animators = sparkObject.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || !animator.enabled)
                continue;

            animator.ResetTrigger("PlayFall");
            animator.SetTrigger("PlayFall");
        }
    }

    private void SetBossCollidersEnabled(bool enabled)
    {
        if (bossObject == null)
            return;

        Collider2D[] colliders = bossObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private static void ResetAnimatorState(Animator animator)
    {
        if (animator == null)
            return;

        bool wasEnabled = animator.enabled;
        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        animator.enabled = false;

        if (!wasEnabled)
            return;
    }

    private void EnsureConfiner(CinemachineVirtualCamera vcam)
    {
        if (vcam == null || frameBounds == null)
            return;

        CinemachineConfiner2D confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
            confiner = vcam.gameObject.AddComponent<CinemachineConfiner2D>();

        confiner.m_BoundingShape2D = frameBounds;
        confiner.m_Damping = 0f;
        confiner.m_MaxWindowSize = -1f;
    }

    private void EnsureFollowCameraLockY()
    {
        if (followCamera == null || frameBounds == null)
            return;

        if (followCameraLockY == null)
            followCameraLockY = followCamera.GetComponent<CameraLockY>();

        if (followCameraLockY == null)
            followCameraLockY = followCamera.gameObject.AddComponent<CameraLockY>();

        followCameraLockY.vCam = followCamera;
        followCameraLockY.fixedYPosition = frameBounds.bounds.center.y;
    }

    private static void RefreshConfiner(CinemachineVirtualCamera vcam)
    {
        if (vcam == null)
            return;

        CinemachineConfiner2D confiner = vcam.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
            return;

        confiner.InvalidateCache();
    }

    private static CinemachineVirtualCamera FindVirtualCamera(string objectName)
    {
        GameObject cameraObject = FindSceneObjectIncludingInactive(objectName);
        return cameraObject != null ? cameraObject.GetComponent<CinemachineVirtualCamera>() : null;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindActivePlayerRoot()
    {
        if (GameState.mainScenePlayer != null)
        {
            GameObject resolvedFromState = ResolvePlayerRoot(GameState.mainScenePlayer);
            if (resolvedFromState != null && resolvedFromState.activeInHierarchy)
                return resolvedFromState;
        }

        PlayerController[] controllers = FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].gameObject.activeInHierarchy)
                return controllers[i].gameObject;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return ResolvePlayerRoot(tagged);
    }

    private static GameObject ResolvePlayerRoot(GameObject candidate)
    {
        if (candidate == null)
            return null;

        PlayerController controller = candidate.GetComponent<PlayerController>();
        if (controller != null)
            return controller.gameObject;

        controller = candidate.GetComponentInParent<PlayerController>(true);
        if (controller != null)
            return controller.gameObject;

        PlayerSpawn spawn = candidate.GetComponent<PlayerSpawn>();
        if (spawn != null)
            return spawn.transform.root.gameObject;

        spawn = candidate.GetComponentInParent<PlayerSpawn>(true);
        if (spawn != null)
            return spawn.transform.root.gameObject;

        Transform root = candidate.transform.root;
        return root != null ? root.gameObject : candidate;
    }

    private static GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SetBrainBlendTime(float blendTime)
    {
        if (cinemachineBrain == null)
            return;

        cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(
            cinemachineBrain.m_DefaultBlend.m_Style,
            Mathf.Max(0f, blendTime));
    }
}

public static class BossfightIntroSequenceBootstrap
{
    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        if (registered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        registered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "bossfight")
            return;

        GameObject triggerObject = GameObject.Find("TargetCamera");
        if (triggerObject == null)
            return;

        if (triggerObject.GetComponent<BossfightIntroSequence>() == null)
            triggerObject.AddComponent<BossfightIntroSequence>();
    }
}
