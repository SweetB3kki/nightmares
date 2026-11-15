using UnityEngine;

public class ChestInteraction : MonoBehaviour
{
    public GameObject gear1;
    public GameObject gear2;
    public GameObject keyObject;
    public Transform player;
    public float maxInteractDistance = 3f;
    public Vector2 interactionPointOffset = new Vector2(0f, -0.8f);
    public Sprite openedChestSprite;

    private bool opened = false;
    private SpriteRenderer spriteRenderer;

    public bool IsOpened => opened;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ResolvePlayer();
    }

    private void OnMouseDown()
    {
        if (GameState.isPaused || GameState.isInLabirint || GameState.isSceneTransitioning)
            return;

        if (opened)
            return;

        if (!PlayerState.hasKey)
        {
            Debug.Log("Chest is locked. Key is required.");
            return;
        }

        if (!IsPlayerInRange())
        {
            Debug.Log("Player is too far from the chest.");
            return;
        }

        opened = true;
        PlayerState.hasKey = false;

        if (gear1 != null)
            gear1.SetActive(true);

        if (gear2 != null)
            gear2.SetActive(true);

        GameState.hasGear1 = true;
        GameState.hasGear2 = true;

        if (keyObject != null)
            keyObject.SetActive(false);

        if (spriteRenderer != null && openedChestSprite != null)
            spriteRenderer.sprite = openedChestSprite;

        LocalScenePreloader.Instance.RequestPreloadScene("Puzzle", 0.9f);
        SaveSystem.NotifySampleSceneProgressChanged();

        Debug.Log("Chest opened.");
    }

    public void ApplySavedState(bool isOpened)
    {
        opened = isOpened;

        if (gear1 != null)
            gear1.SetActive(GameState.hasGear1);

        if (gear2 != null)
            gear2.SetActive(GameState.hasGear2);

        if (keyObject != null)
            keyObject.SetActive(PlayerState.hasKey && !opened);

        if (spriteRenderer != null && opened && openedChestSprite != null)
            spriteRenderer.sprite = openedChestSprite;
    }

    private bool IsPlayerInRange()
    {
        ResolvePlayer();

        if (player == null)
            return false;

        return Vector2.Distance(player.position, GetInteractionPoint()) <= maxInteractDistance;
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    private Vector3 GetInteractionPoint()
    {
        return transform.TransformPoint(interactionPointOffset);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.55f, 0.8f);
        Gizmos.DrawWireSphere(GetInteractionPoint(), maxInteractDistance);
    }
}
