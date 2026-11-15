using UnityEngine;

public class SavePointTrigger : MonoBehaviour
{
    public Transform teleportHere;
    public string sceneToPreloadOnEnter;
    [Min(-1)]
    public int progressionOrder = -1;

    public string SavePointId => SaveIdentityUtility.GetObjectId(this);
    public int EffectiveProgressionOrder => progressionOrder >= 0 ? progressionOrder : ResolveDefaultProgressionOrder();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (teleportHere != null)
        {
            if (FailManager.Instance != null)
                FailManager.Instance.UpdateCheckpoint(teleportHere.position);
            else
                Debug.LogWarning("SavePointTrigger: FailManager instance not found.");

            SaveSystem.SaveAtCheckpoint(gameObject.scene.name, SavePointId, EffectiveProgressionOrder, teleportHere.position);
        }

        if (!string.IsNullOrWhiteSpace(sceneToPreloadOnEnter))
        {
            LocalScenePreloader.Instance.RequestPreloadScene(sceneToPreloadOnEnter, 0.75f);
            return;
        }

        if (gameObject.name == "SavePointTrigger1")
            LocalScenePreloader.Instance.RequestPreloadScene("labirint", 0.75f);
    }

    private int ResolveDefaultProgressionOrder()
    {
        string objectName = gameObject.name;
        if (string.Equals(objectName, "SavePointTrigger1", System.StringComparison.OrdinalIgnoreCase))
            return 10;

        if (string.Equals(objectName, "SavePointTrigger1 boss", System.StringComparison.OrdinalIgnoreCase))
            return 20;

        if (string.Equals(objectName, "SavePointTriggerW", System.StringComparison.OrdinalIgnoreCase))
            return 30;

        if (string.Equals(objectName, "SavePointParkur", System.StringComparison.OrdinalIgnoreCase))
            return 40;

        return -1;
    }
}
