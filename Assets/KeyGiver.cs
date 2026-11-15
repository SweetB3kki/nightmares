using UnityEngine;

public class KeyGiver : MonoBehaviour
{
    public GameObject keyObject;
    private Collider2D cachedCollider;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.RestoreFullHealth();

        if (PlayerState.hasKey)
            return;

        PlayerState.hasKey = true;

        if (keyObject != null)
            keyObject.SetActive(true);

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        SaveSystem.NotifySampleSceneProgressChanged();

        Debug.Log("Key issued.");
    }

    public void ApplySavedState(bool hasKey, bool chestOpened)
    {
        bool shouldRemainAvailable = !hasKey && !chestOpened;

        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider2D>();

        if (cachedCollider != null)
            cachedCollider.enabled = shouldRemainAvailable;

        gameObject.SetActive(shouldRemainAvailable);

        if (keyObject != null)
            keyObject.SetActive(hasKey && !chestOpened);
    }
}
