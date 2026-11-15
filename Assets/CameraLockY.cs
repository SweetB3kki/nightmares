using Cinemachine;
using UnityEngine;

public class CameraLockY : MonoBehaviour
{
    public CinemachineVirtualCamera vCam;
    public float fixedYPosition = 0.6f;

    private void LateUpdate()
    {
        if (vCam != null && vCam.Follow != null)
        {
            Vector3 currentPos = vCam.transform.position;
            currentPos.y = fixedYPosition;
            vCam.transform.position = currentPos;
        }
    }
}
