using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneCameraActivationUtility
{
    public static void ActivateSceneCameras(Scene targetScene)
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
            camera.enabled = shouldEnable;
        }

        AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
                continue;

            bool shouldEnable = listener.gameObject.scene == targetScene;
            listener.enabled = shouldEnable;
        }
    }
}
