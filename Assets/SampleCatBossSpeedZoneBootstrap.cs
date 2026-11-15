using UnityEngine;
using UnityEngine.SceneManagement;

public static class SampleCatBossSpeedZoneBootstrap
{
    private const string SceneName = "SampleScene";
    private const string EnterZoneName = "CatBossZoneIn";
    private const string ExitZoneName = "CatBossZoneOut";
    private const float DefaultSpeed = 20f;
    private const float CatBossSpeed = 12f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName)
            return;

        TryAttachZone(EnterZoneName, CatBossSpeed);
        TryAttachZone(ExitZoneName, DefaultSpeed);
    }

    private static void TryAttachZone(string objectName, float targetSpeed)
    {
        GameObject zoneObject = FindSceneObjectIncludingInactive(objectName);
        if (zoneObject == null)
            return;

        SampleCatBossSpeedZone zone = zoneObject.GetComponent<SampleCatBossSpeedZone>();
        if (zone == null)
            zone = zoneObject.AddComponent<SampleCatBossSpeedZone>();

        zone.targetSpeed = targetSpeed;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInHierarchy(Transform root, string objectName)
    {
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

public sealed class SampleCatBossSpeedZone : MonoBehaviour
{
    public float targetSpeed = 20f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        ApplyIfPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ApplyIfPlayer(other);
    }

    private void ApplyIfPlayer(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        player.speed = targetSpeed;
    }
}
