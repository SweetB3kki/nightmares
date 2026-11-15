using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneReturner : MonoBehaviour
{
    public string objectToEnableName = "Square (1)";
    public string[] additionalObjectsToEnableNames = { "Square (1)" };
    public string mainSceneName = "SampleScene";
    public string sparkObjectName = "ClockSpark";

    [Header("Fade")]
    public float fadeDuration = 1.2f;
    public float holdBlackDuration = 0.8f;

    private bool isReturning = false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ReturnToMainSceneImmediate();
    }

    public void ReturnToMainSceneWithFade()
    {
        if (isReturning)
            return;

        StartCoroutine(ReturnRoutine(useFade: true));
    }

    public void ReturnToMainSceneImmediate()
    {
        if (isReturning)
            return;

        StartCoroutine(ReturnRoutine(useFade: false));
    }

    private IEnumerator ReturnRoutine(bool useFade)
    {
        isReturning = true;

        if (useFade)
        {
            yield return StartCoroutine(PlayFadeOut());
            if (holdBlackDuration > 0f)
                yield return new WaitForSeconds(holdBlackDuration);
        }

        InventoryDisplay inv = FindObjectOfType<InventoryDisplay>();
        if (inv != null)
            inv.Refresh();

        Scene mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (mainScene.IsValid())
        {
            GameObject objToEnable = FindInSceneByName(mainScene, objectToEnableName);
            if (objToEnable != null)
                objToEnable.SetActive(true);

            for (int i = 0; i < additionalObjectsToEnableNames.Length; i++)
            {
                GameObject extraObject = FindInSceneByName(mainScene, additionalObjectsToEnableNames[i]);
                if (extraObject != null)
                    extraObject.SetActive(true);
            }

            if (GameState.allGearsInserted)
            {
                GameObject sparkObj = FindInSceneByName(mainScene, sparkObjectName);
                if (sparkObj != null)
                    sparkObj.SetActive(true);
                else
                    Debug.LogWarning($"SceneReturner: could not find '{sparkObjectName}' in scene '{mainSceneName}'.");
            }
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync("clocks");
        if (unload != null)
            yield return unload;

        if (mainScene.IsValid())
            SceneManager.SetActiveScene(mainScene);

        isReturning = false;
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

    private IEnumerator PlayFadeOut()
    {
        GameObject fadeCanvasObj = new GameObject("ClockReturnFadeCanvas");
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
}
