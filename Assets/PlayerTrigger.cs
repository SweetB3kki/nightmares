using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;

public class PlayerTrigger : MonoBehaviour
{
    public GameObject fadeImage;
    public float delayBeforeUnload = 2f;
    public float postReturnFadeDuration = 1f;
    public float postReturnControlLockDuration = 1f;

    [Header("Main scene object")]
    public string objectToEnableName = "Square (1)";

    [Header("Future direct scene transition")]
    public bool useDirectSceneAfterLabyrinth = true;
    public string directSceneAfterLabyrinth = "fiol_location";
    public string directSceneSpawnPointName = "ReturnPoint";

    private bool triggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Cat"))
            return;

        triggered = true;
        GameState.isSceneTransitioning = true;

        if (fadeImage != null)
            fadeImage.SetActive(true);

        GameObject player = GameState.mainScenePlayer;
        if (player != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.velocity = Vector2.zero;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockAllControl(delayBeforeUnload + postReturnControlLockDuration + 0.2f);
        }

        Invoke(nameof(CloseLabirintScene), delayBeforeUnload);
    }

    private void CloseLabirintScene()
    {
        if (useDirectSceneAfterLabyrinth && !string.IsNullOrWhiteSpace(directSceneAfterLabyrinth))
        {
            if (Application.CanStreamedLevelBeLoaded(directSceneAfterLabyrinth))
            {
                if (GameState.mainScenePlayer != null)
                {
                    PlayerReferenceUtility.PreparePlayerForGameplay(GameState.mainScenePlayer);
                }

                ScreenFadeUtility.OverlayHandle directArrivalOverlay = ScreenFadeUtility.CreateBlackOverlay();
                DontDestroyOnLoad(directArrivalOverlay.canvasObject);
                CreateDirectSceneArrivalWatcher(directArrivalOverlay);
                SceneManager.LoadScene(directSceneAfterLabyrinth, LoadSceneMode.Single);
                return;
            }

            Debug.LogWarning($"PlayerTrigger: scene '{directSceneAfterLabyrinth}' is not available yet. Falling back to current labyrinth exit flow.");
        }

        Scene mainScene = SceneManager.GetSceneByName("SampleScene");
        if (!mainScene.IsValid() || !mainScene.isLoaded)
        {
            Debug.LogWarning("PlayerTrigger: SampleScene is not loaded and direct after_labirint scene is unavailable. Current close request is ignored as a placeholder until after_labirint is added.");
            triggered = false;
            GameState.isSceneTransitioning = false;

            if (fadeImage != null)
                fadeImage.SetActive(false);

            return;
        }

        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private void OnSceneUnloaded(Scene unloadedScene)
    {
        Scene mainScene = SceneManager.GetSceneByName("SampleScene");
        if (mainScene.IsValid())
        {
            SceneManager.SetActiveScene(mainScene);
            ActivateSceneCameras(mainScene);

            foreach (GameObject obj in mainScene.GetRootGameObjects())
            {
                if (obj.name == objectToEnableName)
                {
                    obj.SetActive(true);
                    break;
                }
            }
        }

        GameObject player = GameState.mainScenePlayer;
        if (player != null)
        {
            ReturnAfterSceneExit teleporter = player.GetComponent<ReturnAfterSceneExit>();
            if (teleporter != null)
                teleporter.TeleportNow();

            PlayerReferenceUtility.PreparePlayerForGameplay(player);

            Vector3 checkpointPosition = teleporter != null && teleporter.targetPosition != null
                ? teleporter.targetPosition.position
                : player.transform.position;

            if (FailManager.Instance != null)
                FailManager.Instance.UpdateCheckpoint(checkpointPosition);

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockAllControl(postReturnControlLockDuration);
        }

        GameState.mainScenePlayer = null;
        GameState.isInLabirint = false;

        if (FailManager.Instance != null)
            FailManager.Instance.StartCoroutine(FadeBackInAfterLabyrinth());
        else
            GameState.isSceneTransitioning = false;

        SceneManager.sceneUnloaded -= OnSceneUnloaded;
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

            listener.enabled = listener.gameObject.scene == targetScene;
        }
    }

    private IEnumerator FadeBackInAfterLabyrinth()
    {
        yield return ScreenFadeUtility.FadeFromBlack(postReturnFadeDuration);
        GameState.isSceneTransitioning = false;
    }

    private void CreateDirectSceneArrivalWatcher(ScreenFadeUtility.OverlayHandle overlayHandle)
    {
        GameObject watcherObject = new GameObject("DirectSceneAfterLabyrinthWatcher");
        DontDestroyOnLoad(watcherObject);

        DirectSceneAfterLabyrinthWatcher watcher = watcherObject.AddComponent<DirectSceneAfterLabyrinthWatcher>();
        watcher.Configure(
            directSceneAfterLabyrinth,
            directSceneSpawnPointName,
            postReturnFadeDuration,
            postReturnControlLockDuration,
            overlayHandle);
    }
}

public sealed class DirectSceneAfterLabyrinthWatcher : MonoBehaviour
{
    private string targetSceneName;
    private string targetPointName;
    private float fadeDuration;
    private float controlLockDuration;
    private ScreenFadeUtility.OverlayHandle overlayHandle;
    private bool handled;

    public void Configure(string sceneName, string pointName, float fadeTime, float lockTime, ScreenFadeUtility.OverlayHandle overlay)
    {
        targetSceneName = sceneName;
        targetPointName = pointName;
        fadeDuration = Mathf.Max(0f, fadeTime);
        controlLockDuration = Mathf.Max(0f, lockTime);
        overlayHandle = overlay;
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
        StartCoroutine(HandleArrival(scene));
    }

    private IEnumerator HandleArrival(Scene scene)
    {
        yield return null;

        ActivateSceneCameras(scene);

        GameObject player = GameState.mainScenePlayer;
        if (player == null)
            player = PlayerReferenceUtility.FindActivePlayerRoot();

        player = PlayerReferenceUtility.ResolvePlayerRoot(player);
        if (player != null)
        {
            PlayerReferenceUtility.PreparePlayerForGameplay(player);

            GameObject point = FindSceneObject(scene, targetPointName);
            if (point != null)
                player.transform.position = point.transform.position;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockAllControl(controlLockDuration + 0.02f);

            BindSceneCameraTargets(scene, player.transform);

            float groundedWaitTimeout = 2.5f;
            float groundedElapsed = 0f;
            while (controller != null && !controller.IsGrounded() && groundedElapsed < groundedWaitTimeout)
            {
                groundedElapsed += Time.deltaTime;
                yield return null;
            }
        }

        FailManager failManager = FindSceneComponent<FailManager>(scene);
        if (failManager != null && player != null)
        {
            failManager.BindPlayerReference(player);

            DamageSystemBootstrap bootstrap = failManager.GetComponent<DamageSystemBootstrap>();
            if (bootstrap != null)
                bootstrap.TrySetup();
        }

        SparkManager.Instance?.UpdateUI();
        GameState.mainScenePlayer = player;
        GameState.isInLabirint = false;

        if (player != null && string.Equals(scene.name, "fiol_location", System.StringComparison.OrdinalIgnoreCase))
            SaveSystem.SaveLightweightScene(scene.name, targetPointName, 40, player.transform.position);

        if (overlayHandle != null && overlayHandle.canvasObject != null)
        {
            if (fadeDuration > 0f)
                yield return ScreenFadeUtility.RevealAndDestroyOverlay(overlayHandle, fadeDuration);
            else
                Object.Destroy(overlayHandle.canvasObject);
        }
        else if (fadeDuration > 0f)
        {
            yield return ScreenFadeUtility.FadeFromBlack(fadeDuration);
        }

        GameState.isSceneTransitioning = false;
        Destroy(gameObject);
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInHierarchy(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInHierarchy(Transform current, string objectName)
    {
        if (current == null)
            return null;

        if (string.Equals(current.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            return current.gameObject;

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindInHierarchy(current.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
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

    private static void BindSceneCameraTargets(Scene scene, Transform player)
    {
        if (!scene.IsValid() || !scene.isLoaded || player == null)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            CinemachineVirtualCamera[] virtualCameras = roots[i].GetComponentsInChildren<CinemachineVirtualCamera>(true);
            for (int cameraIndex = 0; cameraIndex < virtualCameras.Length; cameraIndex++)
            {
                CinemachineVirtualCamera virtualCamera = virtualCameras[cameraIndex];
                if (virtualCamera == null)
                    continue;

                virtualCamera.Follow = player;
                virtualCamera.LookAt = scene.name == "fiol_location" ? null : player;
                virtualCamera.PreviousStateIsValid = false;

                if (scene.name == "fiol_location")
                {
                    CinemachineFramingTransposer framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
                    if (framingTransposer != null)
                    {
                        framingTransposer.m_YDamping = 1f;
                        framingTransposer.m_DeadZoneHeight = 2f;
                        framingTransposer.m_SoftZoneHeight = 2f;
                    }

                    CinemachineConfiner2D confiner2D = virtualCamera.GetComponent<CinemachineConfiner2D>();
                    if (confiner2D != null)
                        confiner2D.InvalidateCache();
                }
            }

            CameraSwitcher[] switchers = roots[i].GetComponentsInChildren<CameraSwitcher>(true);
            for (int switcherIndex = 0; switcherIndex < switchers.Length; switcherIndex++)
            {
                CameraSwitcher switcher = switchers[switcherIndex];
                if (switcher == null)
                    continue;

                switcher.BindPlayer(player);
                switcher.ForceEvaluate();
            }

            CameraFollowOffsetSwitcher[] offsetSwitchers = roots[i].GetComponentsInChildren<CameraFollowOffsetSwitcher>(true);
            for (int offsetIndex = 0; offsetIndex < offsetSwitchers.Length; offsetIndex++)
            {
                CameraFollowOffsetSwitcher offsetSwitcher = offsetSwitchers[offsetIndex];
                if (offsetSwitcher == null)
                    continue;

                offsetSwitcher.BindPlayer(player);
            }
        }
    }
}

