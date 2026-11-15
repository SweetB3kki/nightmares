using UnityEngine;

public class HideZone : MonoBehaviour
{
    private Collider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (other.TryGetComponent<PlayerStealth>(out var stealth))
            stealth.NotifyHideZoneEnter(zoneCollider != null ? zoneCollider : other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (other.TryGetComponent<PlayerStealth>(out var stealth))
            stealth.NotifyHideZoneExit(zoneCollider != null ? zoneCollider : other);
    }
}
