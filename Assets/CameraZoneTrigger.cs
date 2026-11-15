using UnityEngine;
using Cinemachine;

public class CameraZoneTrigger : MonoBehaviour
{
    public CinemachineVirtualCamera topCamera;
    public CinemachineVirtualCamera bottomCamera;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("EnterLowerZone"))
        {
            topCamera.Priority = 5;
            bottomCamera.Priority = 10;
            Debug.Log("Камера: вниз");
        }
        else if (other.CompareTag("EnterUpperZone"))
        {
            topCamera.Priority = 10;
            bottomCamera.Priority = 5;
            Debug.Log("Камера: вверх");
        }
    }
}
