using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class LabyrinthBossEncounter : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string playerObjectName = "PLAYER";
    [SerializeField] private string dissolveTargetsName = "LabyrinthDissolveTargets";
    [SerializeField] private string bossSceneName = "bossfight";
    [SerializeField] private string bossSpawnPointName = "SpawnPoint";
    [SerializeField] private string vignetteObjectName = "Q_Vignette_Single";
    [SerializeField] private Camera sceneCameraOverride;
    [SerializeField] private Collider2D wallHitboxCollider;
    [SerializeField] private Vector2 sceneFrameSize = new Vector2(19.2f, 10.8f);
    [SerializeField] private Vector2 sceneFrameCenterOffset = Vector2.zero;

    [Header("Progress")]
    [SerializeField] private float distanceToTransfer = 8f;
    [SerializeField] private float dissolveSpreadDuration = 20f;
    [SerializeField] private float worldGoneProgress = 0.9f;
    [SerializeField] private float finalSequenceProgress = 0.95f;
    [SerializeField] private float cameraNearMaxProgress = 0.7f;
    [SerializeField] private float effectStartProgress = 0.8f;
    [SerializeField] private float effectFadeInDuration = 0.5f;

    [Header("Movement")]
    [SerializeField] private float minSpeedMultiplier = 0.12f;
    [SerializeField] private bool clampMovementToDissolveFront = true;
    [SerializeField] private float movementBoundaryPadding = 0.15f;
    [SerializeField] private float movementClampStartProgress = 0.18f;

    [Header("Vignette")]
    [SerializeField] private int vignetteCornerType = 1;
    [SerializeField] private Color vignetteColor = Color.black;
    [SerializeField] private bool vignetteStretchToScreenRatio = true;
    [SerializeField] private float vignetteMaxScale = 2f;
    [SerializeField] private float vignetteNearMaxProgress = 0.7f;
    [SerializeField] private float vignettePulseAmplitude = 0.08f;
    [SerializeField] private float vignettePulseFrequencyMin = 1.2f;
    [SerializeField] private float vignettePulseFrequencyMax = 8f;

    [Header("Dissolve")]
    [SerializeField] private bool overrideDissolveCenterWithPlayer = false;
    [SerializeField] private bool useGlobalDissolveAlongsideRadial = true;
    [SerializeField] private bool disableWallHitboxOnEncounter = true;
    [SerializeField] private float radialRadiusPadding = 1.25f;
    [SerializeField] private float fallbackRadialRadius = 12f;
    [SerializeField] private float targetRadialProgress = 1f;
    [SerializeField] private float targetDissolveAmount = 0.08f;

    [Header("Camera")]
    [SerializeField] private float zoomedOrthographicSize = 1.2f;
    [SerializeField] private Vector2 cameraZoomOffset = new Vector2(-1.16f, -5f);
    [SerializeField] private float cameraPositionLerp = 8f;
    [SerializeField] private float cameraSizeLerp = 3.77f;
    [SerializeField] private float snapbackDuration = 0.12f;

    [Header("Shake")]
    [SerializeField] private float maxShakeAmplitude = 0.03f;
    [SerializeField] private float shakeFrequency = 16f;

    [Header("Flash")]
    [SerializeField] private float flashDuration = 0.18f;
    [SerializeField] private float flashHoldDuration = 0.06f;
    [SerializeField] private float bossRevealStartDelay = 1f;
    [SerializeField] private float bossRevealFadeDuration = 0.18f;
    [SerializeField] private float bossRevealUnlockProgress = 0.5f;
    [SerializeField] private float bossRevealEdgeSoftness = 0.045f;
    [SerializeField] private float bossRevealNoiseStrength = 0.03f;
    [SerializeField] private float bossRevealNoiseScale = 5f;
    [SerializeField] private bool allowStandalonePreview = true;
    [SerializeField] private float standaloneFlashFadeDuration = 0.2f;

    [Header("Return Placeholder")]
    [SerializeField] private string bossReturnDialogueObjectName = "диалог";
    [SerializeField] private string bossReturnDialogueTriggerName = "StartDialoge";
    [SerializeField] private UnityEvent onBossReturnSequenceFinished;

    private BoxCollider2D triggerCollider;
    private GameObject labyrinthPlayerObject;
    private mazemove labyrinthMove;
    private Rigidbody2D labyrinthRigidbody;
    private PlayerTrigger labyrinthExitTrigger;
    private SpriteDissolveController dissolveController;
    private Camera sceneCamera;
    private Q_Vignette_Single sceneVignette;
    private Q_Vignette_Single vignetteTemplate;
    private bool ownsSpawnedVignette;
    private bool bossReturnRevealActive;

    private bool armed;
    private bool finalizing;
    private float travelledDistance;
    private float dissolveElapsed;
    private Vector3 lastPlayerPosition;
    private Vector3 defaultCameraPosition;
    private float defaultOrthographicSize;
    private Bounds cameraClampBounds;
    private bool hasCameraClampBounds;
    private float currentVignetteBaseScale;
    private float vignettePulseTime;
    private bool originalWallHitboxEnabled;
    private Vector3 returnRevealCameraStartPosition;
    private float returnRevealStartOrthographicSize;

    private float originalVignetteScale;
    private int originalVignetteCornerType;
    private Color originalVignetteColor;
    private bool originalVignetteStretch;
    private bool vignetteStateCaptured;

    private float initialDissolveAmount;
    private float initialRadialProgress;
    private float initialRadialRadius;
    private bool initialUseRadialMask;
    private bool initialInvertRadialMask;
    private Transform initialRadialCenterTarget;
    private Vector3 initialRadialCenterOffset;
    private bool dissolveStateCaptured;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        ResolveReferences();
        CaptureDissolveStateIfNeeded();
        ApplyDormantDissolveState();
    }

    private void Update()
    {
        if (!armed && !finalizing && !bossReturnRevealActive)
            ApplyDormantDissolveState();

        if (!armed || finalizing || labyrinthPlayerObject == null || labyrinthMove == null)
            return;

        Vector3 currentPosition = labyrinthPlayerObject.transform.position;
        travelledDistance += Vector3.Distance(currentPosition, lastPlayerPosition);
        lastPlayerPosition = currentPosition;
        dissolveElapsed += Time.unscaledDeltaTime;

        float progress = GetEncounterProgress();
        ApplyEncounterState(progress);

        if (progress >= finalSequenceProgress)
            StartCoroutine(FinalizeEncounterRoutine());
    }

    private void FixedUpdate()
    {
        if (!armed || finalizing || !disableWallHitboxOnEncounter || !clampMovementToDissolveFront)
            return;

        ApplyMovementBoundary();
    }

    private void LateUpdate()
    {
        if (!armed || finalizing || sceneCamera == null || labyrinthPlayerObject == null)
            return;

        ActivateEncounterSceneCameras();

        float progress = GetEncounterProgress();
        float dissolveProgress = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        float cameraT = Mathf.Clamp01(Mathf.InverseLerp(0f, cameraNearMaxProgress, dissolveProgress));
        float shakeT = Mathf.Clamp01(Mathf.InverseLerp(effectStartProgress, worldGoneProgress, progress));
        float desiredSize = Mathf.Lerp(defaultOrthographicSize, zoomedOrthographicSize, cameraT);

        Vector3 followPosition = new Vector3(
            labyrinthPlayerObject.transform.position.x + cameraZoomOffset.x,
            labyrinthPlayerObject.transform.position.y + cameraZoomOffset.y,
            defaultCameraPosition.z);

        Vector3 desiredPosition = Vector3.Lerp(defaultCameraPosition, followPosition, cameraT);
        Vector2 shakeOffset = EvaluateShakeOffset(shakeT);
        desiredPosition.x += shakeOffset.x;
        desiredPosition.y += shakeOffset.y;
        desiredPosition = ClampCameraPosition(desiredPosition, desiredSize);

        sceneCamera.transform.position = desiredPosition;
        sceneCamera.orthographicSize = Mathf.Lerp(sceneCamera.orthographicSize, desiredSize, Time.deltaTime * cameraSizeLerp);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (armed || finalizing)
            return;

        ResolveReferences();
        if (!TryBindEncounterPlayer(other))
            return;

        armed = true;
        travelledDistance = 0f;
        dissolveElapsed = 0f;
        lastPlayerPosition = labyrinthPlayerObject.transform.position;
        defaultCameraPosition = sceneCamera != null ? sceneCamera.transform.position : Vector3.zero;
        defaultOrthographicSize = sceneCamera != null ? sceneCamera.orthographicSize : 5f;
        currentVignetteBaseScale = 0f;
        vignettePulseTime = 0f;
        originalWallHitboxEnabled = wallHitboxCollider != null && wallHitboxCollider.enabled;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (labyrinthExitTrigger != null)
            labyrinthExitTrigger.enabled = false;

        if (disableWallHitboxOnEncounter && wallHitboxCollider != null)
            wallHitboxCollider.enabled = false;

        ActivateEncounterSceneCameras();

        PrepareSceneVignette();
        PrepareDissolveController();
    }

    private bool TryBindEncounterPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        GameObject candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.transform.root.gameObject;

        if (candidate == null || !candidate.CompareTag("Player"))
            return false;

        labyrinthPlayerObject = candidate;
        labyrinthMove = labyrinthPlayerObject.GetComponent<mazemove>();
        labyrinthRigidbody = labyrinthPlayerObject.GetComponent<Rigidbody2D>();
        labyrinthExitTrigger = labyrinthPlayerObject.GetComponent<PlayerTrigger>();
        return labyrinthMove != null || labyrinthRigidbody != null;
    }

    private void ResolveReferences()
    {
        if (sceneCamera == null)
            sceneCamera = FindLabyrinthCamera();

        if (dissolveController == null)
        {
            GameObject dissolveObject = GameObject.Find(dissolveTargetsName);
            if (dissolveObject != null)
                dissolveController = dissolveObject.GetComponent<SpriteDissolveController>();
        }

        if (wallHitboxCollider == null)
        {
            GameObject wallObject = GameObject.Find("СТЕНЫ HITBOX");
            if (wallObject != null)
                wallHitboxCollider = wallObject.GetComponent<Collider2D>();
        }

        if (labyrinthPlayerObject == null)
            labyrinthPlayerObject = GameObject.Find(playerObjectName);

        if (labyrinthPlayerObject == null)
            return;

        RebindLabyrinthPlayerComponents();

        if (vignetteTemplate == null)
        {
            Q_Vignette_Single[] vignettes = FindObjectsOfType<Q_Vignette_Single>(true);
            for (int i = 0; i < vignettes.Length; i++)
            {
                if (vignettes[i] != null && vignettes[i].gameObject.name == vignetteObjectName)
                {
                    vignetteTemplate = vignettes[i];
                    break;
                }
            }
        }

#if UNITY_EDITOR
        if (vignetteTemplate == null)
        {
            GameObject vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Q Vignette/Prefabs/Q_Vignette_Single.prefab");
            if (vignettePrefab != null)
                vignetteTemplate = vignettePrefab.GetComponent<Q_Vignette_Single>();
        }
#endif
    }

    private void PrepareSceneVignette()
    {
        if (sceneVignette == null)
            sceneVignette = CreateEncounterVignetteInstance();

        if (sceneVignette == null)
            return;

        if (!vignetteStateCaptured)
        {
            originalVignetteScale = sceneVignette.mainScale;
            originalVignetteCornerType = sceneVignette.mainCornerType;
            originalVignetteColor = sceneVignette.mainColor;
            originalVignetteStretch = sceneVignette.stretchMainToScreenRatio;
            vignetteStateCaptured = true;
        }

        sceneVignette.mainCornerType = vignetteCornerType;
        sceneVignette.mainColor = vignetteColor;
        sceneVignette.stretchMainToScreenRatio = vignetteStretchToScreenRatio;
        sceneVignette.mainScale = 0f;
        sceneVignette.UpdateVignette();
    }

    private Q_Vignette_Single CreateEncounterVignetteInstance()
    {
        Canvas targetCanvas = FindLabyrinthCanvas();
        if (targetCanvas == null || vignetteTemplate == null)
            return null;

        Q_Vignette_Single[] existingVignettes = targetCanvas.GetComponentsInChildren<Q_Vignette_Single>(true);
        for (int i = 0; i < existingVignettes.Length; i++)
        {
            if (existingVignettes[i] != null && existingVignettes[i].gameObject.name == "LabyrinthEncounterVignette")
                return existingVignettes[i];
        }

        GameObject clone = Object.Instantiate(vignetteTemplate.gameObject, targetCanvas.transform, false);
        clone.name = "LabyrinthEncounterVignette";
        clone.SetActive(true);
        clone.transform.SetAsLastSibling();
        ownsSpawnedVignette = true;
        return clone.GetComponent<Q_Vignette_Single>();
    }

    private void CaptureDissolveStateIfNeeded()
    {
        if (dissolveController == null || dissolveStateCaptured)
            return;

        initialUseRadialMask = dissolveController.UseRadialMask;
        initialInvertRadialMask = dissolveController.InvertRadialMask;
        initialDissolveAmount = dissolveController.DissolveAmount;
        initialRadialProgress = dissolveController.RadialProgress;
        initialRadialRadius = dissolveController.RadialRadius;
        initialRadialCenterTarget = dissolveController.RadialCenterTarget;
        initialRadialCenterOffset = dissolveController.RadialCenterOffset;
        dissolveStateCaptured = true;
    }

    private void ApplyDormantDissolveState()
    {
        if (dissolveController == null)
            return;

        dissolveController.CollectTargets();
        dissolveController.SetVisible();
        dissolveController.DissolveAmount = 0f;
        dissolveController.RadialProgress = 0f;
        dissolveController.UseRadialMask = false;
        dissolveController.InvertRadialMask = false;
        dissolveController.RadialRadius = 0f;
        dissolveController.RadialCenterTarget = null;
        dissolveController.RadialCenterOffset = Vector3.zero;
    }
    private void PrepareDissolveController()
    {
        if (dissolveController == null)
            return;

        if (!dissolveStateCaptured)
        {
            initialUseRadialMask = dissolveController.UseRadialMask;
            initialInvertRadialMask = dissolveController.InvertRadialMask;
            initialDissolveAmount = dissolveController.DissolveAmount;
            initialRadialProgress = dissolveController.RadialProgress;
            initialRadialRadius = dissolveController.RadialRadius;
            initialRadialCenterTarget = dissolveController.RadialCenterTarget;
            initialRadialCenterOffset = dissolveController.RadialCenterOffset;
            dissolveStateCaptured = true;
        }

        dissolveController.UseRadialMask = true;
        dissolveController.InvertRadialMask = true;
        dissolveController.RadialProgress = initialRadialProgress;
        dissolveController.DissolveAmount = initialDissolveAmount;
        dissolveController.RadialCenterTarget = overrideDissolveCenterWithPlayer && labyrinthPlayerObject != null
            ? labyrinthPlayerObject.transform
            : transform;
        dissolveController.RadialCenterOffset = overrideDissolveCenterWithPlayer ? initialRadialCenterOffset : Vector3.zero;

        Bounds targetBounds = dissolveController.CalculateTargetBounds();
        if (targetBounds.size != Vector3.zero)
        {
            Vector3 center = (overrideDissolveCenterWithPlayer && labyrinthPlayerObject != null
                ? labyrinthPlayerObject.transform.position
                : transform.position) + dissolveController.RadialCenterOffset;
            Vector3 min = targetBounds.min;
            Vector3 max = targetBounds.max;
            float computedRadius = Mathf.Max(
                Vector2.Distance(center, new Vector2(min.x, min.y)),
                Vector2.Distance(center, new Vector2(min.x, max.y)),
                Vector2.Distance(center, new Vector2(max.x, min.y)),
                Vector2.Distance(center, new Vector2(max.x, max.y))) + radialRadiusPadding;
            dissolveController.RadialRadius = Mathf.Max(fallbackRadialRadius, computedRadius);
        }
        else
        {
            dissolveController.RadialRadius = Mathf.Max(fallbackRadialRadius, initialRadialRadius);
        }

        Vector3 frameCenter = new Vector3(
            defaultCameraPosition.x + sceneFrameCenterOffset.x,
            defaultCameraPosition.y + sceneFrameCenterOffset.y,
            0f);
        cameraClampBounds = new Bounds(frameCenter, new Vector3(sceneFrameSize.x, sceneFrameSize.y, 0f));
        hasCameraClampBounds = sceneFrameSize.x > 0.01f && sceneFrameSize.y > 0.01f;
    }

    private float GetEncounterProgress()
    {
        float distanceProgress = Mathf.Clamp01(travelledDistance / Mathf.Max(0.01f, distanceToTransfer));
        float dissolveProgress = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        return Mathf.Max(distanceProgress, dissolveProgress);
    }

    private void ApplyEncounterState(float progress)
    {
        if (labyrinthMove != null)
        {
            float speedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, Mathf.SmoothStep(0f, 1f, progress));
            labyrinthMove.SetExternalSpeedMultiplier(speedMultiplier);
        }

        float fadeInT = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, effectFadeInDuration));
        float dissolveProgress = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        float vignetteT = Mathf.Clamp01(Mathf.InverseLerp(0f, vignetteNearMaxProgress, dissolveProgress)) * fadeInT;
        currentVignetteBaseScale = Mathf.Lerp(0f, vignetteMaxScale, Mathf.SmoothStep(0f, 1f, vignetteT));
        UpdateSceneVignette(vignetteT);
        UpdateDissolveState();
    }

    private void UpdateDissolveState()
    {
        if (dissolveController == null)
            return;

        float spreadT = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        float fadeInT = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, effectFadeInDuration));
        float dissolveProgress = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        spreadT *= fadeInT;
        dissolveController.RadialProgress = Mathf.Lerp(initialRadialProgress, targetRadialProgress, spreadT);

        if (useGlobalDissolveAlongsideRadial)
            dissolveController.DissolveAmount = Mathf.Lerp(initialDissolveAmount, targetDissolveAmount, spreadT);
        else
            dissolveController.DissolveAmount = initialDissolveAmount;
    }

    private void UpdateSceneVignette(float effectT)
    {
        if (sceneVignette == null)
            return;

        float pulseFrequency = Mathf.Lerp(vignettePulseFrequencyMin, vignettePulseFrequencyMax, effectT);
        vignettePulseTime += Time.unscaledDeltaTime * pulseFrequency;
        float pulse = Mathf.Sin(vignettePulseTime) * vignettePulseAmplitude * effectT;
        float zoomRatio = sceneCamera != null && defaultOrthographicSize > 0.01f
            ? sceneCamera.orthographicSize / defaultOrthographicSize
            : 1f;
        sceneVignette.mainScale = Mathf.Clamp((currentVignetteBaseScale + pulse) * zoomRatio, 0f, 5f);
        sceneVignette.UpdateVignette();
    }

    private IEnumerator FinalizeEncounterRoutine()
    {
        if (finalizing)
            yield break;

        finalizing = true;
        GameState.isSceneTransitioning = true;

        if (labyrinthMove != null)
            labyrinthMove.SetMovementLocked(true);

        if (labyrinthRigidbody != null)
            labyrinthRigidbody.velocity = Vector2.zero;

        GameState.hasLabyrinthReturnPosition = true;
        GameState.labyrinthReturnPosition = labyrinthPlayerObject != null
            ? labyrinthPlayerObject.transform.position
            : Vector3.zero;

        yield return SnapCameraAndVignetteBack();

        BossEncounterOverlayUtility.FlashHandle flashOverlay = BossEncounterOverlayUtility.CreateWhiteFlashOverlay();
        yield return BossEncounterOverlayUtility.PlayCenterFlashToSolid(flashOverlay, flashDuration);

        if (flashHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(flashHoldDuration);

        GameObject transferPlayer = ResolveTransferPlayer();
        if (transferPlayer == null)
        {
            if (allowStandalonePreview)
            {
                yield return StandalonePreviewOutro(flashOverlay);
                yield break;
            }

            Debug.LogError("LabyrinthBossEncounter could not find a persistent transfer player. Enter the labyrinth from SampleScene.");
            GameState.isSceneTransitioning = false;
            yield break;
        }

        ActivateTransferPlayer(transferPlayer);
        PlayerSpawn.spawnPointName = bossSpawnPointName;
        GameState.mainScenePlayer = transferPlayer;
        GameState.isInLabirint = false;

        BossEncounterOverlayUtility.PersistFlashUntilSceneLoadedWithSceneDissolve(
            flashOverlay,
            bossSceneName,
            bossRevealStartDelay,
            bossRevealFadeDuration,
            bossRevealUnlockProgress);

        SceneLoader.LoadScene(bossSceneName);
    }

    private IEnumerator StandalonePreviewOutro(BossEncounterOverlayUtility.FlashHandle flashOverlay)
    {
        if (flashOverlay != null && flashOverlay.canvasGroup != null)
        {
            float duration = Mathf.Max(0.01f, standaloneFlashFadeDuration);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                flashOverlay.canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            Object.Destroy(flashOverlay.canvasObject);
        }

        RestoreStandaloneState();
    }

    private IEnumerator SnapCameraAndVignetteBack()
    {
        float duration = Mathf.Max(0.01f, snapbackDuration);
        float timer = 0f;

        Vector3 startCameraPosition = sceneCamera != null ? sceneCamera.transform.position : Vector3.zero;
        float startOrtho = sceneCamera != null ? sceneCamera.orthographicSize : 5f;
        float startVignetteScale = sceneVignette != null ? sceneVignette.mainScale : 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);

            if (sceneCamera != null)
            {
                sceneCamera.transform.position = Vector3.Lerp(startCameraPosition, defaultCameraPosition, t);
                sceneCamera.orthographicSize = Mathf.Lerp(startOrtho, defaultOrthographicSize, t);
            }

            if (sceneVignette != null)
            {
                sceneVignette.mainScale = Mathf.Lerp(startVignetteScale, 0f, t);
                sceneVignette.UpdateVignette();
            }

            yield return null;
        }

        if (sceneCamera != null)
        {
            sceneCamera.transform.position = defaultCameraPosition;
            sceneCamera.orthographicSize = defaultOrthographicSize;
        }

        if (sceneVignette != null)
        {
            sceneVignette.mainScale = 0f;
            sceneVignette.UpdateVignette();
        }

    }

    private GameObject ResolveTransferPlayer()
    {
        if (GameState.mainScenePlayer != null)
            return GameState.mainScenePlayer;

        PlayerSpawn[] spawns = Resources.FindObjectsOfTypeAll<PlayerSpawn>();
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i] == null)
                continue;

            GameObject root = spawns[i].transform.root.gameObject;
            if (root == null)
                continue;

            if (root.scene.name == "DontDestroyOnLoad" || root.scene.name == "SampleScene")
                return root;
        }

        return null;
    }

    private static void ActivateTransferPlayer(GameObject transferPlayer)
    {
        if (transferPlayer == null)
            return;

        transferPlayer.SetActive(true);

        Rigidbody2D rb = transferPlayer.GetComponentInChildren<Rigidbody2D>(true);
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
        }
    }

    
    private void ApplyMovementBoundary()
    {
        if (labyrinthRigidbody == null || dissolveController == null || !dissolveController.UseRadialMask)
            return;

        float spreadProgress = Mathf.Clamp01(dissolveElapsed / Mathf.Max(0.01f, dissolveSpreadDuration));
        if (spreadProgress < movementClampStartProgress)
            return;

        float allowedRadius = Mathf.Max(0.05f, dissolveController.RadialRadius * dissolveController.RadialProgress - movementBoundaryPadding);
        Vector2 center = GetCurrentEncounterCenter();
        Vector2 currentPosition = labyrinthRigidbody.position;
        Vector2 delta = currentPosition - center;

        if (delta.sqrMagnitude <= allowedRadius * allowedRadius)
            return;

        Vector2 clampedPosition = center + delta.normalized * allowedRadius;
        labyrinthRigidbody.position = clampedPosition;
        labyrinthRigidbody.velocity = Vector2.zero;

        if (labyrinthPlayerObject != null)
            labyrinthPlayerObject.transform.position = new Vector3(clampedPosition.x, clampedPosition.y, labyrinthPlayerObject.transform.position.z);
    }

    private Vector2 GetCurrentEncounterCenter()
    {
        Transform centerTarget = dissolveController != null && dissolveController.RadialCenterTarget != null
            ? dissolveController.RadialCenterTarget
            : transform;

        Vector3 center = centerTarget.position + (dissolveController != null ? dissolveController.RadialCenterOffset : Vector3.zero);
        return new Vector2(center.x, center.y);
    }
    private Vector2 EvaluateShakeOffset(float shakeT)
    {
        if (shakeT <= 0f)
            return Vector2.zero;

        float amplitude = maxShakeAmplitude * shakeT;
        float time = Time.unscaledTime * shakeFrequency;
        float x = (Mathf.PerlinNoise(time, 0.13f) - 0.5f) * 2f * amplitude;
        float y = (Mathf.PerlinNoise(0.71f, time) - 0.5f) * 2f * amplitude;
        return new Vector2(x, y);
    }

    private Vector3 ClampCameraPosition(Vector3 desiredPosition, float orthographicSize)
    {
        if (!hasCameraClampBounds || sceneCamera == null || !sceneCamera.orthographic)
            return desiredPosition;

        float halfHeight = Mathf.Max(0.01f, orthographicSize);
        float halfWidth = halfHeight * sceneCamera.aspect;

        float minX = cameraClampBounds.min.x + halfWidth;
        float maxX = cameraClampBounds.max.x - halfWidth;
        float minY = cameraClampBounds.min.y + halfHeight;
        float maxY = cameraClampBounds.max.y - halfHeight;

        if (minX > maxX)
        {
            float centerX = cameraClampBounds.center.x;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = cameraClampBounds.center.y;
            minY = centerY;
            maxY = centerY;
        }

        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        desiredPosition.z = defaultCameraPosition.z;
        return desiredPosition;
    }

    private void OnDisable()
    {
        RestoreSceneVignette();
        RestoreDissolveState();
    }

    private void OnDestroy()
    {
        RestoreSceneVignette();
        RestoreDissolveState();
    }

    private void RestoreSceneVignette()
    {
        if (sceneVignette == null || !vignetteStateCaptured)
            return;

        sceneVignette.mainScale = originalVignetteScale;
        sceneVignette.mainCornerType = originalVignetteCornerType;
        sceneVignette.mainColor = originalVignetteColor;
        sceneVignette.stretchMainToScreenRatio = originalVignetteStretch;
        sceneVignette.UpdateVignette();
        vignetteStateCaptured = false;

        if (ownsSpawnedVignette && sceneVignette != null)
        {
            Object.Destroy(sceneVignette.gameObject);
            sceneVignette = null;
            ownsSpawnedVignette = false;
        }
    }

    private void RestoreDissolveState()
    {
        if (dissolveController == null || !dissolveStateCaptured)
            return;

        dissolveController.UseRadialMask = initialUseRadialMask;
        dissolveController.InvertRadialMask = initialInvertRadialMask;
        dissolveController.DissolveAmount = initialDissolveAmount;
        dissolveController.RadialProgress = initialRadialProgress;
        dissolveController.RadialRadius = initialRadialRadius;
        dissolveController.RadialCenterTarget = initialRadialCenterTarget;
        dissolveController.RadialCenterOffset = initialRadialCenterOffset;
        dissolveStateCaptured = false;
    }

    private void RestoreStandaloneState()
    {
        finalizing = false;
        armed = false;
        travelledDistance = 0f;
        dissolveElapsed = 0f;
        currentVignetteBaseScale = 0f;
        vignettePulseTime = 0f;

        if (labyrinthMove != null)
            labyrinthMove.ResetMovementState();

        if (labyrinthRigidbody != null)
            labyrinthRigidbody.velocity = Vector2.zero;

        if (sceneCamera != null)
        {
            sceneCamera.transform.position = defaultCameraPosition;
            sceneCamera.orthographicSize = defaultOrthographicSize;
        }

        if (sceneVignette != null)
        {
            sceneVignette.mainScale = 0f;
            sceneVignette.UpdateVignette();
        }

        ApplyDormantDissolveState();

        if (disableWallHitboxOnEncounter && wallHitboxCollider != null)
            wallHitboxCollider.enabled = originalWallHitboxEnabled;

        if (triggerCollider != null)
            triggerCollider.enabled = true;

        if (labyrinthExitTrigger != null)
            labyrinthExitTrigger.enabled = true;

        GameState.isSceneTransitioning = false;
    }

    private Camera FindLabyrinthCamera()
    {
        if (sceneCameraOverride != null)
            return sceneCameraOverride;

        Scene labyrinthScene = gameObject.scene;
        if (labyrinthScene.IsValid() && labyrinthScene.isLoaded)
        {
            foreach (GameObject root in labyrinthScene.GetRootGameObjects())
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);
                if (camera != null && camera.gameObject.activeInHierarchy)
                    return camera;
            }
        }

        return null;
    }

    private void ActivateEncounterSceneCameras()
    {
        Scene encounterScene = gameObject.scene;
        if (!encounterScene.IsValid() || !encounterScene.isLoaded)
            return;

        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            bool shouldEnable = camera.gameObject.scene == encounterScene;
            if (camera.gameObject.activeSelf != shouldEnable)
                camera.gameObject.SetActive(shouldEnable);

            if (camera != null)
            {
                camera.enabled = shouldEnable;
                camera.tag = shouldEnable ? "MainCamera" : "Untagged";
            }
        }

        AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
                continue;

            listener.enabled = listener.gameObject.scene == encounterScene;
        }
    }


    public void PrepareBossReturnReveal()
    {
        ResolveReferences();
        labyrinthPlayerObject = FindSceneObjectInCurrentScene(playerObjectName);
        if (labyrinthPlayerObject == null)
            labyrinthPlayerObject = FindFirstTaggedPlayerInCurrentScene();
        RebindLabyrinthPlayerComponents();
        CaptureDissolveStateIfNeeded();
        ActivateEncounterSceneCameras();

        if (sceneCamera != null)
        {
            defaultCameraPosition = sceneCamera.transform.position;
            defaultOrthographicSize = sceneCamera.orthographicSize;
        }

        if (labyrinthMove != null)
            labyrinthMove.SetMovementLocked(true);

        if (labyrinthRigidbody != null)
        {
            labyrinthRigidbody.simulated = true;
            labyrinthRigidbody.velocity = Vector2.zero;
        }

        PrepareSceneVignette();
        PrepareDissolveController();

        if (sceneCamera != null)
        {
            Vector3 desiredPosition;
            if (labyrinthPlayerObject != null)
            {
                Vector3 playerPosition = labyrinthPlayerObject.transform.position;
                desiredPosition = new Vector3(playerPosition.x, playerPosition.y, defaultCameraPosition.z);
            }
            else
            {
                desiredPosition = new Vector3(
                    defaultCameraPosition.x + sceneFrameCenterOffset.x,
                    defaultCameraPosition.y + sceneFrameCenterOffset.y,
                    defaultCameraPosition.z);
            }

            returnRevealStartOrthographicSize = zoomedOrthographicSize;
            returnRevealCameraStartPosition = desiredPosition;
            sceneCamera.transform.position = returnRevealCameraStartPosition;
            sceneCamera.orthographicSize = returnRevealStartOrthographicSize;
        }

        if (sceneVignette != null)
        {
            sceneVignette.mainScale = 0f;
            sceneVignette.UpdateVignette();
        }

        if (dissolveController != null)
        {
            dissolveController.UseRadialMask = true;
            dissolveController.InvertRadialMask = true;
            dissolveController.RadialCenterTarget = overrideDissolveCenterWithPlayer && labyrinthPlayerObject != null ? labyrinthPlayerObject.transform : transform;
            dissolveController.RadialCenterOffset = overrideDissolveCenterWithPlayer ? initialRadialCenterOffset : Vector3.zero;
            dissolveController.RadialProgress = targetRadialProgress;
            dissolveController.DissolveAmount = useGlobalDissolveAlongsideRadial ? targetDissolveAmount : initialDissolveAmount;
        }

        if (wallHitboxCollider != null)
            wallHitboxCollider.enabled = true;

        bossReturnRevealActive = true;
        armed = false;
        finalizing = false;
    }

    public float GetReturnDissolveDuration()
    {
        return Mathf.Max(0.01f, dissolveSpreadDuration);
    }

    public void SetBossReturnRevealProgress(float progress)
    {
        if (!bossReturnRevealActive)
            return;

        float t = Mathf.Clamp01(progress);

        if (dissolveController != null)
        {
            dissolveController.RadialProgress = Mathf.Lerp(targetRadialProgress, initialRadialProgress, t);
            dissolveController.DissolveAmount = Mathf.Lerp(useGlobalDissolveAlongsideRadial ? targetDissolveAmount : initialDissolveAmount, initialDissolveAmount, t);
        }

        if (sceneCamera != null)
        {
            sceneCamera.transform.position = Vector3.Lerp(returnRevealCameraStartPosition, defaultCameraPosition, t);
            sceneCamera.orthographicSize = Mathf.Lerp(returnRevealStartOrthographicSize, defaultOrthographicSize, t);
        }

        if (sceneVignette != null)
        {
            sceneVignette.mainScale = 0f;
            sceneVignette.UpdateVignette();
        }
    }

    public void FinishBossReturnReveal()
    {
        if (!bossReturnRevealActive)
            return;

        bossReturnRevealActive = false;
        ApplyDormantDissolveState();

        if (sceneVignette != null)
        {
            sceneVignette.mainScale = 0f;
            sceneVignette.UpdateVignette();
        }

        if (sceneCamera != null)
        {
            sceneCamera.transform.position = defaultCameraPosition;
            sceneCamera.orthographicSize = defaultOrthographicSize;
        }

        if (labyrinthMove != null)
            labyrinthMove.ResetMovementState();

        if (labyrinthRigidbody != null)
            labyrinthRigidbody.velocity = Vector2.zero;

        if (wallHitboxCollider != null)
            wallHitboxCollider.enabled = true;

        if (labyrinthExitTrigger != null)
            labyrinthExitTrigger.enabled = true;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        GameState.isSceneTransitioning = false;
        RefreshLabyrinthSparkCollectors();
        TryActivateBossReturnDialogue();
        onBossReturnSequenceFinished?.Invoke();
    }

    private void RefreshLabyrinthSparkCollectors()
    {
        Scene currentScene = gameObject.scene;
        if (!currentScene.IsValid() || !currentScene.isLoaded)
            return;

        SparkCollector[] sparkCollectors = FindObjectsOfType<SparkCollector>(true);
        for (int i = 0; i < sparkCollectors.Length; i++)
        {
            SparkCollector collector = sparkCollectors[i];
            if (collector == null || collector.gameObject.scene != currentScene)
                continue;

            collector.ResetForSceneReuse();
        }
    }

    private void TryActivateBossReturnDialogue()
    {
        if (string.IsNullOrWhiteSpace(bossReturnDialogueObjectName))
            return;

        GameObject dialogueObject = FindSceneObjectInCurrentScene(bossReturnDialogueObjectName);
        if (dialogueObject == null)
            return;

        if (!dialogueObject.activeSelf)
            dialogueObject.SetActive(true);

        Animator dialogueAnimator = dialogueObject.GetComponent<Animator>();
        if (dialogueAnimator == null)
            dialogueAnimator = dialogueObject.GetComponentInChildren<Animator>(true);

        if (dialogueAnimator == null)
            return;

        string triggerName = string.IsNullOrWhiteSpace(bossReturnDialogueTriggerName)
            ? "StartDialoge"
            : bossReturnDialogueTriggerName;

        dialogueAnimator.ResetTrigger(triggerName);
        dialogueAnimator.SetTrigger(triggerName);
    }

    private GameObject FindSceneObjectInCurrentScene(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Scene currentScene = gameObject.scene;
        if (!currentScene.IsValid() || !currentScene.isLoaded)
            return null;

        foreach (GameObject root in currentScene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private GameObject FindFirstTaggedPlayerInCurrentScene()
    {
        Scene currentScene = gameObject.scene;
        if (!currentScene.IsValid() || !currentScene.isLoaded)
            return null;

        foreach (GameObject root in currentScene.GetRootGameObjects())
        {
            GameObject found = FindTaggedPlayerInHierarchy(root.transform);
            if (found != null)
                return found;
        }

        return null;
    }

    private GameObject FindTaggedPlayerInHierarchy(Transform root)
    {
        if (root == null)
            return null;

        if (root.CompareTag("Player"))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindTaggedPlayerInHierarchy(root.GetChild(i));
            if (found != null)
                return found;
        }

        return null;
    }

    private void RebindLabyrinthPlayerComponents()
    {
        labyrinthMove = null;
        labyrinthRigidbody = null;
        labyrinthExitTrigger = null;

        if (labyrinthPlayerObject == null)
            return;

        labyrinthMove = labyrinthPlayerObject.GetComponent<mazemove>();
        labyrinthRigidbody = labyrinthPlayerObject.GetComponent<Rigidbody2D>();
        labyrinthExitTrigger = labyrinthPlayerObject.GetComponent<PlayerTrigger>();
    }

    private static GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
    private Canvas FindLabyrinthCanvas()
    {
        Scene labyrinthScene = gameObject.scene;
        if (labyrinthScene.IsValid() && labyrinthScene.isLoaded)
        {
            foreach (GameObject root in labyrinthScene.GetRootGameObjects())
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                    return canvas;
            }
        }

        return null;
    }
}

public static class LabyrinthBossEncounterBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "labirint")
            return;

        GameObject triggerObject = GameObject.Find("BossEncounterTrigger");
        if (triggerObject == null)
            return;

        if (triggerObject.GetComponent<LabyrinthBossEncounter>() == null)
            triggerObject.AddComponent<LabyrinthBossEncounter>();
    }
}



