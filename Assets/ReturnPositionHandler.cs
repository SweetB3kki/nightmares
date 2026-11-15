using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnPositionHandler : MonoBehaviour
{
    public string labirintSceneName = "labirint"; // <- Только из этой сцены сработает

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene unloadedScene)
    {
        if (unloadedScene.name != labirintSceneName) return; //выходим, если сцена — не лабиринт
    }
}
