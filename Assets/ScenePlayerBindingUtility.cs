using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScenePlayerBindingUtility
{
    public static void RefreshSceneBindings(Scene scene, GameObject player)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        ActivateSceneCameras(scene);

        if (player != null)
        {
            BindSceneCameraTargets(scene, player.transform);

            FailManager failManager = FindSceneComponent<FailManager>(scene);
            if (failManager != null)
            {
                failManager.BindPlayerReference(player);

                DamageSystemBootstrap bootstrap = failManager.GetComponent<DamageSystemBootstrap>();
                if (bootstrap != null)
                    bootstrap.TrySetup();
            }
        }

        SparkManager.Instance?.UpdateUI();
    }

    public static T FindSceneComponent<T>(Scene scene) where T : Component
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

    public static GameObject FindSceneObject(Scene scene, string objectName)
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

    private static void ActivateSceneCameras(Scene targetScene)
    {
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
}
