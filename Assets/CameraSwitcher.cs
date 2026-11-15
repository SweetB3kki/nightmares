using UnityEngine;
using Cinemachine;

public class CameraSwitcher : MonoBehaviour
{
    public Transform player;
    public float switchY = -50f;

    public CinemachineVirtualCamera topCamera;
    public CinemachineVirtualCamera bottomCamera;

    private bool isTopActive = true;

    private void Start()
    {
        TryAutoBindPlayer();
        ForceEvaluate();
    }

    private void Update()
    {
        TryAutoBindPlayer();
        EvaluateCameraState();
    }

    public void BindPlayer(Transform target)
    {
        player = target;
    }

    public void ForceEvaluate()
    {
        EvaluateCameraState();
    }

    private void EvaluateCameraState()
    {
        if (player == null || topCamera == null || bottomCamera == null)
            return;

        bool shouldUseTopCamera = player.position.y > switchY;

        topCamera.Priority = shouldUseTopCamera ? 10 : 5;
        bottomCamera.Priority = shouldUseTopCamera ? 5 : 10;
        isTopActive = shouldUseTopCamera;
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
