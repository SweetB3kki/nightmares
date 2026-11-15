using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BossEncounterOverlayUtility
{
    private const string CenterRevealShaderName = "UI/Center Reveal Dissolve";
    private static Texture2D generatedRevealNoise;

    public sealed class VignetteHandle
    {
        public GameObject canvasObject;
        public RectTransform[] cornerRects;
        public Image[] cornerImages;
    }

    public sealed class FlashHandle
    {
        public GameObject canvasObject;
        public CanvasGroup canvasGroup;
        public Image fillImage;
        public RectTransform burstRect;
        public Image burstImage;
        public Material revealMaterial;
    }

    public static VignetteHandle CreateRuntimeVignetteOverlay(int sortingOrder = 9500)
    {
        GameObject canvasObject = CreateCanvasRoot("RuntimeBossEncounterVignette", sortingOrder, out _);

        RectTransform[] cornerRects = new RectTransform[4];
        Image[] cornerImages = new Image[4];

        CreateCorner(canvasObject.transform, "BottomRight", 0, new Vector3(-1f, -1f, 1f), out cornerRects[0], out cornerImages[0]);
        CreateCorner(canvasObject.transform, "BottomLeft", 1, new Vector3(1f, -1f, 1f), out cornerRects[1], out cornerImages[1]);
        CreateCorner(canvasObject.transform, "TopLeft", 2, new Vector3(1f, 1f, 1f), out cornerRects[2], out cornerImages[2]);
        CreateCorner(canvasObject.transform, "TopRight", 3, new Vector3(-1f, 1f, 1f), out cornerRects[3], out cornerImages[3]);

        VignetteHandle handle = new VignetteHandle
        {
            canvasObject = canvasObject,
            cornerRects = cornerRects,
            cornerImages = cornerImages
        };

        SetVignetteColor(handle, Color.black);
        SetVignetteStretch(handle, false);
        SetVignetteScale(handle, 0f);
        return handle;
    }

    public static void SetVignetteScale(VignetteHandle handle, float scale)
    {
        if (handle == null || handle.cornerRects == null || handle.cornerRects.Length < 4)
            return;

        float clampedScale = Mathf.Clamp(scale, 0f, 2f);

        handle.cornerRects[0].anchorMin = new Vector2(1f - clampedScale, 0f);
        handle.cornerRects[0].anchorMax = new Vector2(1f, clampedScale);
        handle.cornerRects[1].anchorMin = new Vector2(0f, 0f);
        handle.cornerRects[1].anchorMax = new Vector2(clampedScale, clampedScale);
        handle.cornerRects[2].anchorMin = new Vector2(0f, 1f - clampedScale);
        handle.cornerRects[2].anchorMax = new Vector2(clampedScale, 1f);
        handle.cornerRects[3].anchorMin = new Vector2(1f - clampedScale, 1f - clampedScale);
        handle.cornerRects[3].anchorMax = new Vector2(1f, 1f);
    }

    public static void SetVignetteColor(VignetteHandle handle, Color color)
    {
        if (handle == null || handle.cornerImages == null)
            return;

        for (int i = 0; i < handle.cornerImages.Length; i++)
        {
            if (handle.cornerImages[i] != null)
                handle.cornerImages[i].color = color;
        }
    }

    public static void SetVignetteStretch(VignetteHandle handle, bool stretchToScreenRatio)
    {
        if (handle == null || handle.cornerImages == null)
            return;

        for (int i = 0; i < handle.cornerImages.Length; i++)
        {
            if (handle.cornerImages[i] != null)
                handle.cornerImages[i].preserveAspect = !stretchToScreenRatio;
        }
    }

    public static void DestroyVignette(VignetteHandle handle)
    {
        if (handle?.canvasObject != null)
            Object.Destroy(handle.canvasObject);
    }

    public static FlashHandle CreateWhiteFlashOverlay(Sprite flashSprite = null, int sortingOrder = 10000)
    {
        GameObject canvasObject = CreateCanvasRoot("RuntimeBossEncounterFlash", sortingOrder, out CanvasGroup canvasGroup);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(canvasObject.transform, false);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0f);

        GameObject burstObject = new GameObject("Burst");
        burstObject.transform.SetParent(canvasObject.transform, false);
        RectTransform burstRect = burstObject.AddComponent<RectTransform>();
        burstRect.anchorMin = new Vector2(0.5f, 0.5f);
        burstRect.anchorMax = new Vector2(0.5f, 0.5f);
        burstRect.pivot = new Vector2(0.5f, 0.5f);
        burstRect.sizeDelta = new Vector2(512f, 512f);
        burstRect.localScale = Vector3.one * 0.01f;

        Image burstImage = burstObject.AddComponent<Image>();
        burstImage.sprite = flashSprite != null ? flashSprite : CreateRadialBurstSprite();
        burstImage.color = Color.white;
        burstImage.preserveAspect = true;

        return new FlashHandle
        {
            canvasObject = canvasObject,
            canvasGroup = canvasGroup,
            fillImage = fillImage,
            burstRect = burstRect,
            burstImage = burstImage
        };
    }

    public static IEnumerator PlayCenterFlashToSolid(FlashHandle handle, float duration)
    {
        if (handle == null || handle.burstRect == null || handle.fillImage == null)
            yield break;

        float safeDuration = Mathf.Max(0.01f, duration);
        float targetScale = Mathf.Max(Screen.width, Screen.height) / 256f * 2.6f;
        float timer = 0f;

        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / safeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            handle.burstRect.localScale = Vector3.one * Mathf.Lerp(0.01f, targetScale, eased);

            Color fillColor = handle.fillImage.color;
            fillColor.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress));
            handle.fillImage.color = fillColor;

            yield return null;
        }

        handle.burstRect.localScale = Vector3.one * targetScale;
        handle.fillImage.color = Color.white;
    }

    public static void PersistFlashUntilSceneLoaded(
        FlashHandle handle,
        string targetSceneName,
        float holdDelay,
        float revealDuration,
        float unlockControlAtProgress = 0.5f,
        float edgeSoftness = 0.045f,
        float noiseStrength = 0.03f,
        float noiseScale = 5f)
    {
        if (handle == null || handle.canvasObject == null || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        Object.DontDestroyOnLoad(handle.canvasObject);

        PersistentFlashSceneWatcher watcher = handle.canvasObject.GetComponent<PersistentFlashSceneWatcher>();
        if (watcher == null)
            watcher = handle.canvasObject.AddComponent<PersistentFlashSceneWatcher>();

        watcher.Configure(
            targetSceneName,
            holdDelay,
            revealDuration,
            unlockControlAtProgress,
            handle.canvasGroup,
            handle.fillImage,
            handle.burstImage,
            CreateCenterRevealMaterial(edgeSoftness, noiseStrength, noiseScale));
    }

    public static void PersistFlashUntilSceneLoadedWithSceneDissolve(
        FlashHandle handle,
        string targetSceneName,
        float holdDelay,
        float sceneDissolveDuration,
        float unlockControlAtProgress,
        string sceneDissolveObjectName = "dissolveintroscene")
    {
        if (handle == null || handle.canvasObject == null || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        Object.DontDestroyOnLoad(handle.canvasObject);

        PersistentSolidFlashSceneWatcher watcher = handle.canvasObject.GetComponent<PersistentSolidFlashSceneWatcher>();
        if (watcher == null)
            watcher = handle.canvasObject.AddComponent<PersistentSolidFlashSceneWatcher>();

        watcher.Configure(
            targetSceneName,
            Mathf.Max(0f, holdDelay),
            Mathf.Max(0.01f, sceneDissolveDuration),
            Mathf.Clamp01(unlockControlAtProgress),
            sceneDissolveObjectName,
            handle.canvasGroup,
            handle.fillImage,
            handle.burstRect,
            handle.burstImage);
    }

    public static void PersistBossfightReturnToLabyrinth(
        FlashHandle handle,
        string labyrinthSceneName,
        Vector3 labyrinthReturnPosition,
        float flashDuration,
        float dissolveRestoreDuration,
        float dissolveStartProgress,
        string labyrinthPlayerObjectName = "PLAYERL",
        string encounterObjectName = "BossEncounterTrigger")
    {
        if (handle == null || handle.canvasObject == null || string.IsNullOrWhiteSpace(labyrinthSceneName))
            return;

        Object.DontDestroyOnLoad(handle.canvasObject);

        PersistentLabyrinthReturnWatcher watcher = handle.canvasObject.GetComponent<PersistentLabyrinthReturnWatcher>();
        if (watcher == null)
            watcher = handle.canvasObject.AddComponent<PersistentLabyrinthReturnWatcher>();

        watcher.Configure(
            labyrinthSceneName,
            labyrinthPlayerObjectName,
            encounterObjectName,
            labyrinthReturnPosition,
            Mathf.Max(0.01f, flashDuration),
            Mathf.Max(0.01f, dissolveRestoreDuration),
            Mathf.Clamp01(dissolveStartProgress),
            handle.canvasGroup,
            handle.fillImage,
            handle.burstRect,
            handle.burstImage);
    }

    private static Material CreateCenterRevealMaterial(float edgeSoftness, float noiseStrength, float noiseScale)
    {
        Shader shader = FindRevealShader();
        if (shader == null)
        {
            Debug.LogWarning($"BossEncounterOverlayUtility could not find shader '{CenterRevealShaderName}'. Falling back to alpha fade.");
            return null;
        }

        Material material = new Material(shader);
        material.SetColor("_Color", Color.white);
        material.SetVector("_Center", new Vector4(0.5f, 0.5f, 0f, 0f));
        material.SetFloat("_Progress", 0f);
        material.SetFloat("_EdgeSoftness", Mathf.Max(0.001f, edgeSoftness));
        material.SetFloat("_NoiseStrength", Mathf.Max(0f, noiseStrength));
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, noiseScale));
        material.SetTexture("_NoiseTex", GetOrCreateRevealNoise());
        return material;
    }

    private static Shader FindRevealShader()
    {
        Shader shader = Shader.Find(CenterRevealShaderName);
        if (shader != null)
            return shader;

        Shader[] loadedShaders = Resources.FindObjectsOfTypeAll<Shader>();
        for (int i = 0; i < loadedShaders.Length; i++)
        {
            if (loadedShaders[i] != null && loadedShaders[i].name == CenterRevealShaderName)
                return loadedShaders[i];
        }

        return null;
    }

    private static Texture2D GetOrCreateRevealNoise()
    {
        if (generatedRevealNoise != null)
            return generatedRevealNoise;

        const int size = 128;
        Texture2D noise = new Texture2D(size, size, TextureFormat.R8, false);
        noise.wrapMode = TextureWrapMode.Repeat;
        noise.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sampleX = x / 18f;
                float sampleY = y / 18f;
                float layerA = Mathf.PerlinNoise(sampleX, sampleY);
                float layerB = Mathf.PerlinNoise(sampleX * 2.17f + 7.3f, sampleY * 2.17f + 11.8f);
                float value = Mathf.Lerp(layerA, layerB, 0.35f);
                noise.SetPixel(x, y, new Color(value, value, value, 1f));
            }
        }

        noise.Apply();
        generatedRevealNoise = noise;
        return generatedRevealNoise;
    }

    private static GameObject CreateCanvasRoot(string name, int sortingOrder, out CanvasGroup canvasGroup)
    {
        GameObject canvasObject = new GameObject(name);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    private static void CreateCorner(Transform parent, string name, int index, Vector3 imageScale, out RectTransform rootRect, out Image image)
    {
        GameObject rootObject = new GameObject(name);
        rootObject.transform.SetParent(parent, false);

        rootRect = rootObject.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject imageObject = new GameObject("Image");
        imageObject.transform.SetParent(rootObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageRect.localScale = imageScale;

        image = imageObject.AddComponent<Image>();
        image.sprite = CreateCornerSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private static Sprite CreateCornerSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float uvX = x / (float)(size - 1);
                float uvY = y / (float)(size - 1);
                float radial = 1f - Mathf.Clamp01(Mathf.Sqrt(uvX * uvX + uvY * uvY));
                float alpha = Mathf.SmoothStep(0f, 1f, radial);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 0f), 100f);
    }

    private static Sprite CreateRadialBurstSprite()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

public sealed class PersistentSolidFlashSceneWatcher : MonoBehaviour
{
    private string targetSceneName;
    private string dialogueObjectName = "диалог";
    private float dialogueStartDelay = 0.5f;
    private float holdDelay;
    private float sceneDissolveDuration;
    private float unlockControlAtProgress;
    private string sceneDissolveObjectName;
    private bool handled;
    private bool releasedControl;
    private bool dialogueTriggered;
    private Animator dialogueAnimator;
    private CanvasGroup canvasGroup;
    private Image fillImage;
    private RectTransform burstRect;
    private Image burstImage;

    public void Configure(
        string targetScene,
        float delay,
        float dissolveDuration,
        float unlockProgress,
        string dissolveObjectName,
        CanvasGroup group,
        Image fill,
        RectTransform burstTransform,
        Image burst)
    {
        targetSceneName = targetScene;
        holdDelay = delay;
        sceneDissolveDuration = dissolveDuration;
        unlockControlAtProgress = unlockProgress;
        sceneDissolveObjectName = dissolveObjectName;
        canvasGroup = group;
        fillImage = fill;
        burstRect = burstTransform;
        burstImage = burst;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (handled || string.IsNullOrWhiteSpace(targetSceneName) || scene.name != targetSceneName)
            return;

        handled = true;
        dialogueAnimator = FindSceneAnimator(scene, dialogueObjectName);
        dialogueTriggered = false;
        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        if (burstImage != null)
            burstImage.enabled = false;

        if (fillImage != null)
            fillImage.color = Color.white;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (holdDelay > 0f)
            yield return new WaitForSecondsRealtime(holdDelay);

        SpriteDissolveController sceneDissolve = FindSceneDissolveController(sceneDissolveObjectName);
        if (sceneDissolve != null)
            sceneDissolve.gameObject.SetActive(false);

        GameObject player = FindActivePlayerRoot();
        if (player != null)
            ApplyInitialControlLock(player, sceneDissolveDuration * unlockControlAtProgress);

        if (fillImage != null)
        {
            fillImage.enabled = false;
        }

        if (burstImage != null && burstRect != null)
        {
            burstImage.enabled = true;
            burstImage.color = Color.white;

            float maxScreen = Mathf.Max(Screen.width, Screen.height);
            float startScale = maxScreen / 256f * 2.6f;
            float endScale = 0.01f;
            burstRect.localScale = Vector3.one * startScale;

            float timer = 0f;
            while (timer < sceneDissolveDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / sceneDissolveDuration);
                float eased = 1f - Mathf.SmoothStep(0f, 1f, progress);
                burstRect.localScale = Vector3.one * Mathf.Lerp(endScale, startScale, eased);

                if (!releasedControl && progress >= unlockControlAtProgress)
                {
                    releasedControl = true;
                    GameState.isSceneTransitioning = false;
                }

                yield return null;
            }

            burstRect.localScale = Vector3.one * endScale;
            burstImage.enabled = false;
        }
        else
        {
            GameState.isSceneTransitioning = false;
        }

        if (dialogueStartDelay > 0f)
            yield return new WaitForSecondsRealtime(dialogueStartDelay);

        TryActivateDialogueAnimator();

        Destroy(gameObject);
    }

    private void TryActivateDialogueAnimator()
    {
        if (dialogueTriggered)
            return;

        dialogueTriggered = true;

        if (dialogueAnimator == null)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
                dialogueAnimator = FindSceneAnimator(activeScene, dialogueObjectName);
        }

        if (dialogueAnimator == null)
            return;

        GameObject dialogueObject = dialogueAnimator.gameObject;
        if (dialogueObject != null && !dialogueObject.activeSelf)
            dialogueObject.SetActive(true);

        dialogueAnimator.enabled = false;
        dialogueAnimator.enabled = true;
        dialogueAnimator.Rebind();
        dialogueAnimator.Update(0f);

        if (dialogueAnimator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = dialogueAnimator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0 && clips[0] != null)
            {
                dialogueAnimator.Play(clips[0].name, 0, 0f);
                dialogueAnimator.Update(0f);
            }
        }
    }

    private static Animator FindSceneAnimator(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform == null || transform.gameObject.scene != scene)
                continue;

            string currentName = transform.gameObject.name ?? string.Empty;
            bool exactMatch = string.Equals(currentName, objectName, System.StringComparison.OrdinalIgnoreCase);
            bool containsMatch = !exactMatch && currentName.IndexOf(objectName, System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!exactMatch && !containsMatch)
                continue;

            Animator animator = transform.GetComponent<Animator>();
            if (animator == null)
                animator = transform.GetComponentInChildren<Animator>(true);

            if (animator != null)
                return animator;
        }

        return null;
    }

    private static SpriteDissolveController FindSceneDissolveController(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            SpriteDissolveController controller = FindInHierarchy(root.transform, objectName);
            if (controller != null)
                return controller;
        }

        return null;
    }

    private static SpriteDissolveController FindInHierarchy(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root.GetComponent<SpriteDissolveController>();

        for (int i = 0; i < root.childCount; i++)
        {
            SpriteDissolveController found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void ApplyInitialControlLock(GameObject player, float duration)
    {
        if (player == null)
            return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
            playerController.LockAllControl(duration + 0.02f);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;

        mazemove mazeMove = player.GetComponent<mazemove>();
        if (mazeMove != null)
            mazeMove.SetMovementLocked(true);
    }

    private static GameObject FindActivePlayerRoot()
    {
        if (GameState.mainScenePlayer != null)
        {
            GameObject resolvedFromState = ResolvePlayerRoot(GameState.mainScenePlayer);
            if (resolvedFromState != null && resolvedFromState.activeInHierarchy)
                return resolvedFromState;
        }

        PlayerController[] controllers = Object.FindObjectsOfType<PlayerController>(true);
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
}

public sealed class PersistentLabyrinthReturnWatcher : MonoBehaviour
{
    private string labyrinthSceneName;
    private string labyrinthPlayerObjectName;
    private string encounterObjectName;
    private Vector3 labyrinthReturnPosition;
    private float flashDuration;
    private float dissolveRestoreDuration;
    private float dissolveStartProgress;
    private CanvasGroup canvasGroup;
    private Image fillImage;
    private RectTransform burstRect;
    private Image burstImage;
    private bool started;

    public void Configure(
        string sceneName,
        string playerObjectName,
        string bossEncounterObjectName,
        Vector3 returnPosition,
        float flashRevealDuration,
        float dissolveDuration,
        float dissolveDelayProgress,
        CanvasGroup group,
        Image fill,
        RectTransform burstTransform,
        Image burst)
    {
        labyrinthSceneName = sceneName;
        labyrinthPlayerObjectName = playerObjectName;
        encounterObjectName = bossEncounterObjectName;
        labyrinthReturnPosition = returnPosition;
        flashDuration = flashRevealDuration;
        dissolveRestoreDuration = dissolveDuration;
        dissolveStartProgress = dissolveDelayProgress;
        canvasGroup = group;
        fillImage = fill;
        burstRect = burstTransform;
        burstImage = burst;
    }

    private void Update()
    {
        if (started)
            return;

        started = true;
        StartCoroutine(RunRoutine());
    }

    private IEnumerator RunRoutine()
    {
        GameObject persistentPlayer = GameState.mainScenePlayer;
        if (persistentPlayer != null)
        {
            persistentPlayer.transform.position = labyrinthReturnPosition;
            persistentPlayer.SetActive(false);

            Rigidbody2D persistentRb = persistentPlayer.GetComponent<Rigidbody2D>();
            if (persistentRb != null)
            {
                persistentRb.simulated = false;
                persistentRb.velocity = Vector2.zero;
            }
        }

        Scene labyrinthScene = SceneManager.GetSceneByName(labyrinthSceneName);
        while (!labyrinthScene.IsValid() || !labyrinthScene.isLoaded)
        {
            yield return null;
            labyrinthScene = SceneManager.GetSceneByName(labyrinthSceneName);
        }

        SceneManager.SetActiveScene(labyrinthScene);
        ActivateSceneCameras(labyrinthScene);

        GameObject labyrinthPlayer = FindSceneObjectIncludingInactive(labyrinthScene, labyrinthPlayerObjectName);
        if (labyrinthPlayer != null)
        {
            labyrinthPlayer.SetActive(true);
            labyrinthPlayer.transform.position = labyrinthReturnPosition;

            Rigidbody2D rb = labyrinthPlayer.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
            }
        }

        LabyrinthBossEncounter encounter = null;
        GameObject encounterObject = FindSceneObjectIncludingInactive(labyrinthScene, encounterObjectName);
        if (encounterObject != null)
            encounter = encounterObject.GetComponent<LabyrinthBossEncounter>();

        if (encounter != null)
            encounter.PrepareBossReturnReveal();

        float activeDissolveDuration = Mathf.Max(0.01f, dissolveRestoreDuration);

        yield return null;
        yield return null;

        if (fillImage != null)
            fillImage.enabled = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (burstImage != null && burstRect != null)
        {
            burstImage.enabled = true;
            burstImage.color = Color.white;

            float maxScreen = Mathf.Max(Screen.width, Screen.height);
            float startScale = maxScreen / 256f * 2.6f;
            float endScale = 0.01f;
            burstRect.localScale = Vector3.one * startScale;

            float timer = 0f;
            float dissolveElapsed = 0f;
            bool dissolveStarted = false;
            while (timer < flashDuration || dissolveElapsed < activeDissolveDuration)
            {
                if (timer < flashDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(timer / flashDuration);
                    float eased = 1f - Mathf.SmoothStep(0f, 1f, progress);
                    burstRect.localScale = Vector3.one * Mathf.Lerp(endScale, startScale, eased);

                    if (progress >= dissolveStartProgress)
                    {
                        dissolveStarted = true;
                        if (burstImage.enabled)
                            burstImage.enabled = false;
                    }

                    if (progress >= 1f && burstImage.enabled)
                        burstImage.enabled = false;
                }
                else
                {
                    burstRect.localScale = Vector3.one * endScale;
                    if (burstImage.enabled)
                        burstImage.enabled = false;
                }

                if (dissolveStarted && dissolveElapsed < activeDissolveDuration)
                {
                    dissolveElapsed += Time.unscaledDeltaTime;
                    float dissolveProgress = Mathf.Clamp01(dissolveElapsed / activeDissolveDuration);
                    encounter?.SetBossReturnRevealProgress(dissolveProgress);
                }

                yield return null;
            }

            burstRect.localScale = Vector3.one * endScale;
            burstImage.enabled = false;
        }
        else
        {
            float timer = 0f;
            while (timer < activeDissolveDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / activeDissolveDuration);
                encounter?.SetBossReturnRevealProgress(progress);
                yield return null;
            }
        }

        encounter?.FinishBossReturnReveal();
        Destroy(gameObject);
    }

    private static void ActivateSceneCameras(Scene targetScene)
    {
        if (!targetScene.IsValid() || !targetScene.isLoaded)
            return;

        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            bool shouldEnable = camera.gameObject.scene == targetScene;
            if (camera.gameObject.activeSelf != shouldEnable)
                camera.gameObject.SetActive(shouldEnable);

            camera.enabled = shouldEnable;
            camera.tag = shouldEnable ? "MainCamera" : "Untagged";
        }

        AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
                continue;

            listener.enabled = listener.gameObject.scene == targetScene;
        }
    }

    private static GameObject FindSceneObjectIncludingInactive(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(objectName))
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
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
}

public sealed class PersistentFlashSceneWatcher : MonoBehaviour
{
    private string targetSceneName;
    private float holdDelay;
    private float revealDuration;
    private float unlockControlAtProgress;
    private bool handled;
    private bool releasedControl;
    private bool initialControlLockApplied;
    private Transform revealCenterTarget;
    private CanvasGroup canvasGroup;
    private Image fillImage;
    private Image burstImage;
    private Material revealMaterial;

    public void Configure(
        string targetScene,
        float hold,
        float reveal,
        float unlockProgress,
        CanvasGroup group,
        Image fill,
        Image burst,
        Material material)
    {
        targetSceneName = targetScene;
        holdDelay = Mathf.Max(0f, hold);
        revealDuration = Mathf.Max(0.01f, reveal);
        unlockControlAtProgress = Mathf.Clamp01(unlockProgress);
        canvasGroup = group;
        fillImage = fill;
        burstImage = burst;
        revealMaterial = material;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (handled || string.IsNullOrWhiteSpace(targetSceneName) || scene.name != targetSceneName)
            return;

        handled = true;
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        GameState.isSceneTransitioning = true;

        if (burstImage != null)
            burstImage.enabled = false;

        if (holdDelay > 0f)
            yield return new WaitForSecondsRealtime(holdDelay);

        if (fillImage != null)
        {
            fillImage.color = Color.white;
            if (revealMaterial != null)
                fillImage.material = revealMaterial;
        }

        if (fillImage != null && revealMaterial != null)
        {
            float timer = 0f;
            while (timer < revealDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / revealDuration);
                TryLockPlayerForInitialReveal();
                UpdateRevealCenterFromPlayer();
                revealMaterial.SetFloat("_Progress", progress);

                if (!releasedControl && progress >= unlockControlAtProgress)
                {
                    releasedControl = true;
                    GameState.isSceneTransitioning = false;
                }

                yield return null;
            }
        }
        else if (canvasGroup != null)
        {
            float timer = 0f;
            float startAlpha = canvasGroup.alpha;
            while (timer < revealDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / revealDuration);
                TryLockPlayerForInitialReveal();
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);

                if (!releasedControl && progress >= unlockControlAtProgress)
                {
                    releasedControl = true;
                    GameState.isSceneTransitioning = false;
                }

                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        if (revealMaterial != null)
            Destroy(revealMaterial);

        GameState.isSceneTransitioning = false;

        Destroy(gameObject);
    }

    private void TryLockPlayerForInitialReveal()
    {
        if (initialControlLockApplied)
            return;

        GameObject player = FindActivePlayerRoot();
        if (player == null)
            return;

        player = ResolvePlayerRoot(player);
        if (player == null)
            return;

        revealCenterTarget = player.transform;

        float controlLockDuration = revealDuration * Mathf.Clamp01(unlockControlAtProgress);

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
            playerController.LockAllControl(controlLockDuration + 0.02f);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;

        mazemove mazeMove = player.GetComponent<mazemove>();
        if (mazeMove != null)
            StartCoroutine(UnlockMazeMovementAfterDelay(mazeMove, controlLockDuration));

        initialControlLockApplied = true;
    }

    private void UpdateRevealCenterFromPlayer()
    {
        if (revealMaterial == null || revealCenterTarget == null)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        Vector3 viewportPosition = activeCamera.WorldToViewportPoint(revealCenterTarget.position);
        if (viewportPosition.z <= 0f)
            return;

        revealMaterial.SetVector("_Center", new Vector4(viewportPosition.x, viewportPosition.y, 0f, 0f));
    }

    private IEnumerator UnlockMazeMovementAfterDelay(mazemove mazeMovement, float delay)
    {
        mazeMovement.SetMovementLocked(true);
        yield return new WaitForSecondsRealtime(delay);
        if (mazeMovement != null)
            mazeMovement.SetMovementLocked(false);
    }

    private static GameObject FindActivePlayerRoot()
    {
        if (GameState.mainScenePlayer != null)
        {
            GameObject resolvedFromState = ResolvePlayerRoot(GameState.mainScenePlayer);
            if (resolvedFromState != null && resolvedFromState.activeInHierarchy)
                return resolvedFromState;
        }

        PlayerController[] controllers = Object.FindObjectsOfType<PlayerController>(true);
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
}
