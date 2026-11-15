using UnityEngine;

public class SceneTransition : MonoBehaviour
{
    public string targetSceneName = "bossfight";
    public string targetSpawnPointName = "SpawnPoint";
    public float controlLockDuration = 0.25f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller != null)
            controller.LockAllControl(controlLockDuration);

        PlayerSpawn.spawnPointName = targetSpawnPointName;
        SceneLoader.LoadScene(targetSceneName);
    }
}
