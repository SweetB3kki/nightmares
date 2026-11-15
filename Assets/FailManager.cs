using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FailManager : MonoBehaviour
{
    public static FailManager Instance;
    private const string SparkCounterObjectName = "SparkCounterText";

    [Header("Fade & Loading")]
    public Image fadeImage;
    public GameObject loadingImage;
    public float fadeDuration = 1f;
    public float loadingDuration = 3f;
    public GameObject bossGameObject;

    [Header("Player")]
    public GameObject player;

    [Header("UI Elements")]
    public GameObject sparkCounterText;

    [Header("Respawn")]
    public float respawnInvulnerabilityDuration = 1.5f;

    public bool IsFailInProgress => isFailInProgress;

    private CanvasGroup sparkGroup;
    private Vector3 lastCheckpoint;
    private bool isFailInProgress = false;
    private float failBlockedUntilTime = -1f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _ = LocalScenePreloader.Instance;

        if (player != null)
            lastCheckpoint = player.transform.position;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        RefreshSparkCounterBinding();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    public void UpdateCheckpoint(Vector3 newPosition)
    {
        lastCheckpoint = newPosition;
    }

    public void BindPlayerReference(GameObject playerObject)
    {
        GameObject resolved = PlayerReferenceUtility.ResolvePlayerRoot(playerObject);
        if (resolved == null)
            return;

        player = resolved;
        lastCheckpoint = resolved.transform.position;
    }

    public bool TryTriggerFail()
    {
        if (isFailInProgress)
            return false;

        if (Time.time < failBlockedUntilTime)
            return false;

        StartCoroutine(HandleFail());
        return true;
    }

    public void TriggerFail()
    {
        TryTriggerFail();
    }

    private IEnumerator HandleFail()
    {
        isFailInProgress = true;

        if (player == null || fadeImage == null)
        {
            Debug.LogError("FailManager is missing required references (player or fadeImage).");
            isFailInProgress = false;
            yield break;
        }

        failBlockedUntilTime = Mathf.Max(
            failBlockedUntilTime,
            Time.time + fadeDuration + loadingDuration + respawnInvulnerabilityDuration
        );

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        StartCoroutine(FadeOutSparkCounter(2f));
        yield return StartCoroutine(Fade(0f, 1f));

        yield return new WaitForSeconds(0.2f);

        if (loadingImage != null)
            loadingImage.SetActive(true);

        player.transform.position = lastCheckpoint;

        CameraSwitcher[] cameraSwitchers = FindObjectsOfType<CameraSwitcher>(true);
        foreach (CameraSwitcher switcher in cameraSwitchers)
            switcher.ForceEvaluate();

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.ResetAfterRespawn();

        PlayerStealth stealth = player.GetComponent<PlayerStealth>();
        if (stealth != null)
            stealth.NotifyRespawn(respawnInvulnerabilityDuration);

        GameState.bossFightStarted = false;
        BossVisionController.ForceResetVision();
        BossfightIntroSequence.Instance?.ResetEncounterState();

        yield return new WaitForSeconds(loadingDuration);

        if (controller != null)
            controller.enabled = true;

        if (loadingImage != null)
            loadingImage.SetActive(false);

        if (bossGameObject != null)
            bossGameObject.GetComponent<BossEyeSignal>()?.ResetBossState();

        foreach (BossTrigger trigger in FindObjectsOfType<BossTrigger>(true))
            trigger.ResetTriggerState();

        if (sparkGroup != null)
            sparkGroup.alpha = 1f;

        yield return StartCoroutine(Fade(1f, 0f));

        failBlockedUntilTime = Mathf.Max(failBlockedUntilTime, Time.time + respawnInvulnerabilityDuration);
        GameState.isSceneTransitioning = false;
        isFailInProgress = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            float t = timer / fadeDuration;
            color.a = Mathf.Lerp(from, to, t);
            fadeImage.color = color;
            timer += Time.deltaTime;
            yield return null;
        }

        color.a = to;
        fadeImage.color = color;
    }

    private IEnumerator FadeOutSparkCounter(float duration)
    {
        if (sparkGroup == null)
            yield break;

        float startAlpha = sparkGroup.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            sparkGroup.alpha = Mathf.Lerp(startAlpha, 0f, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }

        sparkGroup.alpha = 0f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoBindPlayerReference();
        RefreshSparkCounterBinding();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RefreshSparkCounterBinding();
    }

    private void RefreshSparkCounterBinding()
    {
        if (sparkCounterText == null || !sparkCounterText.scene.IsValid() || !sparkCounterText.scene.isLoaded)
            sparkCounterText = FindSparkCounterObject();

        sparkGroup = sparkCounterText != null ? sparkCounterText.GetComponent<CanvasGroup>() : null;
    }

    private void TryAutoBindPlayerReference()
    {
        if (player != null)
        {
            GameObject resolvedExisting = PlayerReferenceUtility.ResolvePlayerRoot(player);
            if (resolvedExisting != null)
            {
                player = resolvedExisting;
                return;
            }
        }

        GameObject resolved = PlayerReferenceUtility.FindActivePlayerRoot();
        if (resolved != null)
            BindPlayerReference(resolved);
    }

    private static GameObject FindSparkCounterObject()
    {
        int loadedSceneCount = SceneManager.sceneCount;
        for (int i = 0; i < loadedSceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject found = FindInHierarchy(roots[rootIndex].transform, SparkCounterObjectName);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root == null)
            return null;

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
