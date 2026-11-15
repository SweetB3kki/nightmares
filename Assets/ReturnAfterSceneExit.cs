using UnityEngine;

public class ReturnAfterSceneExit : MonoBehaviour
{
    public Transform targetPosition;
    private bool shouldTeleport = false;

    public void TeleportNow()
    {
        if (targetPosition == null)
            return;

        transform.position = targetPosition.position;
        shouldTeleport = false;
    }

    public void TeleportNextFrame()
    {
        shouldTeleport = true;
    }

    private void LateUpdate()
    {
        if (shouldTeleport)
            TeleportNow();
    }
}
