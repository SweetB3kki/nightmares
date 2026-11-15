using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneDoor : MonoBehaviour
{
    public GameObject objectToDisable;

    [Header("Temporary Test Override")]
    public bool useTestSceneTransition = false;
    public string testSceneName = "bossfight";
    public string testSpawnPointName = "SpawnPoint";

    [Header("Labyrinth")]
    public string sceneToLoad = "labirint";
    public string returnPointName = "ReturnPoint";
    public GameObject player;
    public float fillDuration = 0.6f;
    public float revealDuration = 1.0f;
    public float holdDarkDuration = 0.15f;

    private bool isLoading = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isLoading || GameState.isSceneTransitioning || GameState.isInLabirint || !other.CompareTag("Player"))
            return;

        isLoading = true;

        if (useTestSceneTransition)
        {
            GameState.isSceneTransitioning = true;
            StartCoroutine(LoadTestSceneRoutine(other.gameObject));
            return;
        }

        GameState.isSceneTransitioning = true;
        GameState.isInLabirint = true;
        GameState.mainScenePlayer = player;

        if (objectToDisable != null)
            objectToDisable.SetActive(false);

        GameObject returnPoint = GameObject.Find(returnPointName);
        if (returnPoint != null && player != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.simulated = false;
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockAllControl(fillDuration + revealDuration + holdDarkDuration + 0.25f);

            ReturnAfterSceneExit teleporter = player.GetComponent<ReturnAfterSceneExit>();
            if (teleporter != null)
                teleporter.targetPosition = returnPoint.transform;
        }

        StartCoroutine(LoadLabyrinthRoutine());
    }

    private System.Collections.IEnumerator LoadLabyrinthRoutine()
    {
        ScreenFadeUtility.OverlayHandle overlay = ScreenFadeUtility.CreateVignetteOverlay();
        SpriteRenderer[] mainPlayerRenderers = GetCharacterRenderers(player);
        Color[] mainPlayerColors = CaptureRendererColors(mainPlayerRenderers);
        ReturnAfterSceneExit mainSceneTeleporter = player != null ? player.GetComponent<ReturnAfterSceneExit>() : null;

        yield return PlayEntryFade(overlay, mainPlayerRenderers, mainPlayerColors, fillDuration);

        if (holdDarkDuration > 0f)
            yield return new WaitForSeconds(holdDarkDuration);

        RestoreRendererColors(mainPlayerRenderers, mainPlayerColors);

        if (player != null)
            player.SetActive(false);

        yield return LocalScenePreloader.Instance.OpenSceneAsync(sceneToLoad);

        Scene scene = SceneManager.GetSceneByName(sceneToLoad);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(scene);
            ActivateSceneCameras(scene);
        }

        // Move the hidden player in SampleScene immediately so returning from the labyrinth
        // does not re-enter the old door trigger or drag the camera across the scene.
        if (mainSceneTeleporter != null)
            mainSceneTeleporter.TeleportNow();

        // Let the new scene and camera settle while the screen is still fully covered.
        yield return null;
        yield return null;

        if (scene.IsValid() && scene.isLoaded)
            ActivateSceneCameras(scene);

        GameObject labyrinthPlayer = FindPlayerInScene(scene);
        SpriteRenderer[] labyrinthRenderers = GetCharacterRenderers(labyrinthPlayer);
        Color[] labyrinthColors = CaptureRendererColors(labyrinthRenderers);
        SetRendererAlphaMultiplier(labyrinthRenderers, labyrinthColors, 0f);

        if (holdDarkDuration > 0f)
            yield return new WaitForSeconds(holdDarkDuration);

        yield return PlayRevealFade(overlay, labyrinthRenderers, labyrinthColors, revealDuration);

        GameState.isSceneTransitioning = false;
        isLoading = false;
    }

    private System.Collections.IEnumerator LoadTestSceneRoutine(GameObject triggerPlayer)
    {
        Rigidbody2D rb = triggerPlayer != null ? triggerPlayer.GetComponent<Rigidbody2D>() : null;
        if (rb != null)
            rb.velocity = Vector2.zero;

        PlayerController controller = triggerPlayer != null ? triggerPlayer.GetComponent<PlayerController>() : null;
        if (controller != null)
            controller.LockAllControl(fillDuration + holdDarkDuration + 0.35f);

        ScreenFadeUtility.OverlayHandle overlay = ScreenFadeUtility.CreateVignetteOverlay();
        SpriteRenderer[] playerRenderers = GetCharacterRenderers(triggerPlayer);
        Color[] playerColors = CaptureRendererColors(playerRenderers);

        yield return PlayEntryFade(overlay, playerRenderers, playerColors, fillDuration);

        if (holdDarkDuration > 0f)
            yield return new WaitForSeconds(holdDarkDuration);

        ScreenFadeUtility.PersistOverlayUntilSceneLoaded(overlay, testSceneName, 0.12f);
        RestoreRendererColors(playerRenderers, playerColors);

        PlayerSpawn.spawnPointName = testSpawnPointName;
        SceneLoader.LoadScene(testSceneName);
    }

    private static SpriteRenderer[] GetCharacterRenderers(GameObject target)
    {
        return target == null ? new SpriteRenderer[0] : target.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private static Color[] CaptureRendererColors(SpriteRenderer[] renderers)
    {
        Color[] colors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            colors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        return colors;
    }

    private static void RestoreRendererColors(SpriteRenderer[] renderers, Color[] colors)
    {
        for (int i = 0; i < renderers.Length && i < colors.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = colors[i];
        }
    }

    private static void SetRendererAlphaMultiplier(SpriteRenderer[] renderers, Color[] baseColors, float multiplier)
    {
        for (int i = 0; i < renderers.Length && i < baseColors.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = baseColors[i];
            color.a *= Mathf.Clamp01(multiplier);
            renderers[i].color = color;
        }
    }

    private static GameObject FindPlayerInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.CompareTag("Player"))
                return root;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag("Player"))
                    return child.gameObject;
            }
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

    private static System.Collections.IEnumerator PlayEntryFade(
        ScreenFadeUtility.OverlayHandle overlay,
        SpriteRenderer[] renderers,
        Color[] baseColors,
        float duration)
    {
        if (overlay == null || overlay.fillImage == null || duration <= 0f)
        {
            if (overlay != null && overlay.fillImage != null)
            {
                Color color = overlay.fillImage.color;
                color.a = 1f;
                overlay.fillImage.color = color;
            }

            SetRendererAlphaMultiplier(renderers, baseColors, 0f);
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            Color fillColor = overlay.fillImage.color;
            fillColor.a = progress;
            overlay.fillImage.color = fillColor;

            SetRendererAlphaMultiplier(renderers, baseColors, 1f - progress);
            yield return null;
        }

        Color finalFillColor = overlay.fillImage.color;
        finalFillColor.a = 1f;
        overlay.fillImage.color = finalFillColor;
        SetRendererAlphaMultiplier(renderers, baseColors, 0f);
    }

    private static System.Collections.IEnumerator PlayRevealFade(
        ScreenFadeUtility.OverlayHandle overlay,
        SpriteRenderer[] renderers,
        Color[] baseColors,
        float duration)
    {
        if (overlay == null || overlay.canvasGroup == null || duration <= 0f)
        {
            SetRendererAlphaMultiplier(renderers, baseColors, 1f);

            if (overlay != null && overlay.canvasObject != null)
                Object.Destroy(overlay.canvasObject);

            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            overlay.canvasGroup.alpha = 1f - progress;
            SetRendererAlphaMultiplier(renderers, baseColors, progress);
            yield return null;
        }

        overlay.canvasGroup.alpha = 0f;
        RestoreRendererColors(renderers, baseColors);

        if (overlay.canvasObject != null)
            Object.Destroy(overlay.canvasObject);
    }
}
