using UnityEngine;
using Cinemachine;

public class CameraFollowOffsetSwitcher : MonoBehaviour
{
    [SerializeField] private Transform player; // игрок
    [SerializeField] private float switchY = -5f; // граница переключенияы
    [SerializeField] private float upperOffset = 10f;
    [SerializeField] private float lowerOffset = 0f;

    private CinemachineFramingTransposer transposer;

    private void Start()
    {
        TryAutoBindPlayer();
        var vcam = GetComponent<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            Debug.LogError("Скрипт должен быть на Virtual Camera!");
            return;
        }

        transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (transposer == null)
        {
            Debug.LogError("Virtual Camera должна использовать Framing Transposer!");
        }
    }

    private void Update()
    {
        TryAutoBindPlayer();
        if (player == null || transposer == null) return;

        float playerY = player.position.y;

        // Меняем вертикальное смещение
        transposer.m_TrackedObjectOffset = new Vector3(
            transposer.m_TrackedObjectOffset.x,
            playerY < switchY ? lowerOffset : upperOffset,
            transposer.m_TrackedObjectOffset.z
        );
    }

    public void BindPlayer(Transform target)
    {
        player = target;
    }

    private void TryAutoBindPlayer()
    {
        if (player != null)
            return;

        GameObject resolved = PlayerReferenceUtility.FindActivePlayerRoot();
        if (resolved != null)
            player = resolved.transform;
    }
}
