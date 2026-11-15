using UnityEngine;
using UnityEngine.SceneManagement;

public class PuzzleTrigger : MonoBehaviour
{
    private readonly string[] objectsToDisable = { "Square (1)", "Square (5)" };

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

        foreach (string objName in objectsToDisable)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null)
                obj.SetActive(false);
        }

        isLoading = true;
        StartCoroutine(OpenPuzzleRoutine());
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

    private System.Collections.IEnumerator OpenPuzzleRoutine()
    {
        yield return LocalScenePreloader.Instance.OpenSceneAsync("Puzzle");

        Scene puzzleScene = SceneManager.GetSceneByName("Puzzle");
        if (puzzleScene.IsValid() && puzzleScene.isLoaded)
            SceneManager.SetActiveScene(puzzleScene);

        isLoading = false;
    }
}
