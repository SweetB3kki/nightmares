using UnityEngine;
using Cinemachine;

public class CameraVerticalLimiter : MonoBehaviour
{
    public Transform player; // игрок
    public float maxY = 2.5f; // максимальное значение Y, до которого можно опускать камеру

    private CinemachineVirtualCamera cam;
    private Vector3 camOffset;

    private void Start()
    {
        cam = GetComponent<CinemachineVirtualCamera>();
    }

    private void LateUpdate()
    {
        if (cam != null && player != null)
        {
            Vector3 pos = cam.transform.position;
            float targetY = player.position.y;

            // Ограничиваем Y, если игрок выше maxY
            if (player.position.y > maxY)
                targetY = maxY;

            // Обновляем позицию камеры, но только по Y
            cam.transform.position = new Vector3(pos.x, targetY, pos.z);
        }
    }
}
