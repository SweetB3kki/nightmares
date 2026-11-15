using System.Collections.Generic;
using UnityEngine;

public class PlayerStealth : MonoBehaviour
{
    public bool isHiding = false;
    private bool isDead = false;
    private bool wasHidingLastFrame = false;
    public static PlayerStealth Instance;

    [Header("Fail safety")]
    public float respawnSafeDuration = 1.5f;

    private readonly HashSet<int> hideZoneIds = new HashSet<int>();
    private int hideZoneCount = 0;
    private float safeUntilTime = -1f;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        bool failInProgress = FailManager.Instance != null && FailManager.Instance.IsFailInProgress;
        bool canTriggerFail = !failInProgress;
        bool justLeftHideZone = false;

        if (isHiding && !wasHidingLastFrame)
            BossVisionController.Instance?.SetVignetteVisible(false);

        if (!isHiding && wasHidingLastFrame)
        {
            if (BossVisionController.isLooking)
            {
                BossVisionController.Instance?.SetVignetteVisible(true);
                safeUntilTime = Mathf.Max(safeUntilTime, Time.time + 0.05f);
            }

            justLeftHideZone = true;
        }

        // Evaluate lethality only after hide/unhide transitions are processed in this frame.
        bool isCurrentlySafe = Time.time < safeUntilTime || justLeftHideZone;
        if (!isHiding && BossVisionController.IsLethalNow() && !isDead && !GameState.bossCompleted && canTriggerFail && !isCurrentlySafe)
        {
            if (FailManager.Instance != null && FailManager.Instance.TryTriggerFail())
                isDead = true;
        }

        wasHidingLastFrame = isHiding;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HideZone"))
            NotifyHideZoneEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("HideZone"))
            NotifyHideZoneExit(other);
    }

    private void OnDisable()
    {
        ResetHideState();
    }

    public void NotifyHideZoneEnter(Collider2D zoneCollider)
    {
        if (zoneCollider == null)
            return;

        if (hideZoneIds.Add(zoneCollider.GetInstanceID()))
        {
            hideZoneCount++;
            isHiding = hideZoneCount > 0;
        }
    }

    public void NotifyHideZoneExit(Collider2D zoneCollider)
    {
        if (zoneCollider == null)
            return;

        if (hideZoneIds.Remove(zoneCollider.GetInstanceID()))
        {
            hideZoneCount = Mathf.Max(0, hideZoneCount - 1);
            isHiding = hideZoneCount > 0;
        }
    }

    public void NotifyRespawn(float duration)
    {
        ResetHideState();
        isDead = false;
        safeUntilTime = Mathf.Max(safeUntilTime, Time.time + duration);
        wasHidingLastFrame = false;

        BossVisionController.Instance?.SetVignetteVisible(false);
        BossVisionController.ForceResetVision();
    }

    private void ResetHideState()
    {
        hideZoneIds.Clear();
        hideZoneCount = 0;
        isHiding = false;
    }

    public void ResetDeath()
    {
        isDead = false;
        safeUntilTime = Mathf.Max(safeUntilTime, Time.time + respawnSafeDuration);
    }
}
