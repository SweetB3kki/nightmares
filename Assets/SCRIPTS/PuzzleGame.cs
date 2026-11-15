using UnityEngine;
using UnityEngine.SceneManagement;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Snap settings")]
    public Transform targetObject;
    public float snapThreshold = 1f;
    public float uiSnapThreshold = 1f;

    [Header("Input")]
    public Camera interactionCamera;

    private bool isDragging = false;
    private Vector2 offset;
    private bool snapReported = false;

    private bool TryResolveCamera(out Camera cam)
    {
        if (interactionCamera != null && interactionCamera.isActiveAndEnabled)
        {
            cam = interactionCamera;
            return true;
        }

        Scene currentScene = gameObject.scene;
        foreach (Camera cameraInScene in Object.FindObjectsOfType<Camera>())
        {
            if (cameraInScene.isActiveAndEnabled && cameraInScene.gameObject.scene == currentScene)
            {
                interactionCamera = cameraInScene;
                cam = interactionCamera;
                return true;
            }
        }

        interactionCamera = Camera.main;
        cam = interactionCamera;
        return cam != null;
    }

    private void OnMouseDown()
    {
        if (!TryResolveCamera(out Camera cam))
            return;

        isDragging = true;
        offset = (Vector2)transform.position - (Vector2)cam.ScreenToWorldPoint(Input.mousePosition);
    }

    private void OnMouseDrag()
    {
        if (!isDragging)
            return;

        if (!TryResolveCamera(out Camera cam))
            return;

        Vector2 mousePosition = cam.ScreenToWorldPoint(Input.mousePosition);
        transform.position = mousePosition + offset;
    }

    private void OnMouseUp()
    {
        isDragging = false;

        if (targetObject == null)
            return;

        if (TrySnapRectTransform())
            return;

        float worldDistance = Vector2.Distance(transform.position, targetObject.position);
        if (worldDistance > snapThreshold)
            return;

        CompleteSnap();
    }

    private bool TrySnapRectTransform()
    {
        RectTransform pieceRect = transform as RectTransform;
        RectTransform targetRect = targetObject as RectTransform;
        if (pieceRect == null || targetRect == null)
            return false;

        Vector2 piecePosition = pieceRect.anchoredPosition;
        Vector2 targetPosition = targetRect.anchoredPosition;
        float rectDistance = Vector2.Distance(piecePosition, targetPosition);
        if (rectDistance > uiSnapThreshold)
            return false;

        pieceRect.anchoredPosition = targetPosition;
        pieceRect.localRotation = targetRect.localRotation;
        CompleteSnap();
        return true;
    }

    private void CompleteSnap()
    {
        transform.position = targetObject.position;
        transform.rotation = targetObject.rotation;

        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.enabled = false;

        if (!snapReported)
        {
            snapReported = true;
            PuzzleEscHandler puzzleHandler = FindObjectOfType<PuzzleEscHandler>();
            if (puzzleHandler != null)
                puzzleHandler.NotifyPieceSnapped();
        }

        enabled = false;
    }
}
