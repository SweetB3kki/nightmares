using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static string sceneToLoad = "SampleScene";

    [SerializeField] private float minimumLoaderScreenTime = 0.15f;

    private void Start()
    {
        StartCoroutine(LoadTargetSceneAsync());
    }

    private IEnumerator LoadTargetSceneAsync()
    {
        float loadStartedAt = Time.unscaledTime;
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        if (asyncLoad == null)
            yield break;

        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
            yield return null;

        float elapsed = Time.unscaledTime - loadStartedAt;
        if (elapsed < minimumLoaderScreenTime)
            yield return new WaitForSecondsRealtime(minimumLoaderScreenTime - elapsed);

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
            yield return null;

        GameState.isSceneTransitioning = false;
    }

    public static void LoadScene(string targetScene)
    {
        GameState.isSceneTransitioning = true;
        sceneToLoad = targetScene;
        SceneManager.LoadScene("LoadScene", LoadSceneMode.Single);
    }
}
