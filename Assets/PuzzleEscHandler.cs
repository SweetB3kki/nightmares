using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PuzzleEscHandler : MonoBehaviour
{
    public string[] objectsToEnable = { "Square (1)", "Square (5)" };
    public string mainSceneName = "SampleScene";
    public string puzzleSceneName = "Puzzle";
    public string sparkObjectName = "spark_pazzle";

    [Header("Auto close")]
    public float completeDelay = 1f;

    [Header("Fade")]
    public float fadeDuration = 1.2f;
    public float holdBlackDuration = 0.8f;

    private int totalPieceCount;
    private int snappedPieceCount;
    private bool isReturning;

    private void Start()
    {
        totalPieceCount = FindObjectsOfType<PuzzlePiece>().Length;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ReturnToMainSceneImmediate();
    }

    public void NotifyPieceSnapped()
    {
        if (isReturning)
            return;

        snappedPieceCount++;

        if (snappedPieceCount < totalPieceCount || totalPieceCount <= 0)
            return;

        if (!GameState.picturePuzzleCompleted)
            GameState.picturePuzzleCompleted = true;

        SaveSystem.NotifySampleSceneProgressChanged();

        StartCoroutine(ReturnRoutine(useFade: true, activateSpark: true));
    }

    public void ReturnToMainSceneImmediate()
    {
        if (isReturning)
            return;

        StartCoroutine(ReturnRoutine(useFade: false, activateSpark: GameState.picturePuzzleCompleted));
    }

    private IEnumerator ReturnRoutine(bool useFade, bool activateSpark)
    {
        isReturning = true;

        if (useFade)
        {
            if (completeDelay > 0f)
                yield return new WaitForSeconds(completeDelay);

            yield return StartCoroutine(PlayFadeOut());

            if (holdBlackDuration > 0f)
                yield return new WaitForSeconds(holdBlackDuration);
        }

        Scene mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (mainScene.IsValid())
        {
            for (int i = 0; i < objectsToEnable.Length; i++)
            {
                GameObject obj = FindInSceneByName(mainScene, objectsToEnable[i]);
                if (obj != null)
                    obj.SetActive(true);
            }

            if (activateSpark)
            {
                GameObject sparkObj = FindInSceneByName(mainScene, sparkObjectName);
                if (sparkObj != null)
                    sparkObj.SetActive(true);
                else
                    Debug.LogWarning($"PuzzleEscHandler: could not find '{sparkObjectName}' in scene '{mainSceneName}'.");
            }
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync(puzzleSceneName);
        if (unload != null)
            yield return unload;

        if (mainScene.IsValid())
            SceneManager.SetActiveScene(mainScene);

        isReturning = false;
    }

    private IEnumerator PlayFadeOut()
    {
        GameObject fadeCanvasObj = new GameObject("PuzzleReturnFadeCanvas");
        Canvas fadeCanvas = fadeCanvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;
        fadeCanvasObj.AddComponent<CanvasScaler>();
        fadeCanvasObj.AddComponent<GraphicRaycaster>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeCanvasObj.transform, false);

        RectTransform rect = imageObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / fadeDuration);
            image.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        image.color = new Color(0f, 0f, 0f, 1f);
    }

    private GameObject FindInSceneByName(Scene scene, string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private GameObject FindInHierarchy(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        if (parent.name == targetName)
            return parent.gameObject;

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject found = FindInHierarchy(parent.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
