using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClickableClock : MonoBehaviour
{
    public GameObject objectToDisable;
    public string[] additionalObjectsToDisableNames = { "Square (1)" };

    private Collider2D ownCollider;
    private Camera cachedCamera;
    private bool isLoading = false;

    private void Awake()
    {
        ownCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (GameState.isPaused)
            return;

        if (Input.GetMouseButtonDown(0))
            TryHandleClick();
    }

    private void OnMouseDown()
    {
        TryHandleClick();
    }

    private void TryHandleClick()
    {
        if (GameState.isPaused || GameState.isInLabirint || GameState.isSceneTransitioning || isLoading || ownCollider == null)
            return;

        if (!TryGetCamera(out Camera cam))
            return;

        Vector2 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
        if (!ownCollider.OverlapPoint(worldPoint))
            return;

        StartCoroutine(LoadClocksSceneAndSetActive());
    }

    private bool TryGetCamera(out Camera cam)
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            cam = cachedCamera;
            return true;
        }

        Scene currentScene = gameObject.scene;
        foreach (Camera cameraInScene in Object.FindObjectsOfType<Camera>())
        {
            if (cameraInScene.isActiveAndEnabled && cameraInScene.gameObject.scene == currentScene)
            {
                cachedCamera = cameraInScene;
                cam = cachedCamera;
                return true;
            }
        }

        cachedCamera = Camera.main;
        cam = cachedCamera;
        return cam != null;
    }

    private IEnumerator LoadClocksSceneAndSetActive()
    {
        isLoading = true;

        yield return LocalScenePreloader.Instance.OpenSceneAsync("clocks");

        DisableClockBlockers();

        Scene clocksScene = SceneManager.GetSceneByName("clocks");

        if (clocksScene.IsValid() && clocksScene.isLoaded)
            SceneManager.SetActiveScene(clocksScene);
        else
            Debug.LogWarning("Не удалось активировать сцену 'clocks'");

        isLoading = false;
    }

    private void DisableClockBlockers()
    {
        if (objectToDisable != null)
            objectToDisable.SetActive(false);

        for (int i = 0; i < additionalObjectsToDisableNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(additionalObjectsToDisableNames[i]))
                continue;

            GameObject extraObject = GameObject.Find(additionalObjectsToDisableNames[i]);
            if (extraObject != null)
                extraObject.SetActive(false);
        }
    }
}
