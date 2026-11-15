using UnityEngine;

public class DeadZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (FailManager.Instance != null)
            FailManager.Instance.TryTriggerFail();
    }
}
