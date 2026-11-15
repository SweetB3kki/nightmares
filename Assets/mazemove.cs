using UnityEngine;

public class mazemove : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementLocked;
    private float externalSpeedMultiplier = 1f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (movementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput.Normalize();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        rb.velocity = movementLocked
            ? Vector2.zero
            : moveInput * (moveSpeed * Mathf.Max(0f, externalSpeedMultiplier));
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            if (rb != null)
                rb.velocity = Vector2.zero;
        }
    }

    public void ResetMovementState()
    {
        externalSpeedMultiplier = 1f;
        SetMovementLocked(false);
    }
}
