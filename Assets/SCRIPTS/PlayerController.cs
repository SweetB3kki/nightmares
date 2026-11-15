using UnityEngine;

public class PlayerController : MonoBehaviour
{

    public float speed = 5f;
    public float jumpForce = 10f;
    public Rigidbody2D rb;
    public Animator anim;

    private float moveInput;
    private bool facingRight = true;

    [Header("Ground Check")]
    public Transform groundCheckOrigin;
    public LayerMask whatIsGround;
    public float rayLength = 0.2f;
    public int rayCount = 3;
    public float raySpacing = 0.1f;
    public float maxSlopeAngle = 60f;
    public Vector2 groundCastSize = new Vector2(0.45f, 0.12f);
    public float groundCastDistance = 0.08f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float visualAirborneDistance = 0.22f;
    public float airBoolEnterDelay = 0.06f;
    public float runningVelocityThreshold = 0.05f;
    public float wallCheckDistance = 0.08f;
    public float wallCheckInset = 0.04f;
    public float visualAirborneGrace = 0.05f;

    [Header("Shadow")]
    public Transform shadowTransform;
    public float shadowRayDistance = 7f;
    public float shadowGroundOffset = 4.5f;
    public float shadowHeightForMinScale = 2f;
    public float groundedShadowScale = 1f;
    public float airborneShadowScale = 0.1f;
    public float groundedShadowAlpha = 1f;
    public float airborneShadowAlpha = 0.01f;
    public float shadowFollowSpeed = 20f;

    private bool isGrounded;
    private bool isVisuallyAirborne;
    private bool airTriggerArmed;
    private bool wallTriggerArmed;
    private bool isBlockedByWallNow;
    private bool airborneFromJump;
    private float controlLockedUntil = -1f;
    private float forcedHorizontalVelocityUntil = -1f;
    private float forcedHorizontalVelocity = 0f;
    private bool lockControlUntilGrounded = false;
    private bool presentationRunningOverride = false;
    private SpriteRenderer shadowRenderer;
    private Vector3 shadowBaseScale = Vector3.one;
    private float shadowBaseLocalZ = 0f;
    private float lastGroundedTime = -999f;
    private float lastGroundNearbyTime = -999f;
    private float jumpPressedTime = -999f;
    private bool jumpConsumed;
    private Collider2D[] ownColliders;
    private static PhysicsMaterial2D noStuckMaterial;
    private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[12];

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
        ownColliders = GetComponents<Collider2D>();
        if (shadowTransform == null)
        {
            Transform foundShadow = transform.Find("ntym");
            if (foundShadow != null)
                shadowTransform = foundShadow;
        }

        if (shadowTransform != null)
        {
            shadowRenderer = shadowTransform.GetComponent<SpriteRenderer>();
            shadowBaseScale = shadowTransform.localScale;
            shadowBaseLocalZ = shadowTransform.localPosition.z;
        }

        ApplyNoStuckPhysicsMaterial();
        UpdateGroundedState();
    }

    void FixedUpdate()
    {
        bool controlsLocked = Time.time < controlLockedUntil || lockControlUntilGrounded || GameState.isPaused;
        bool forcedHorizontalActive = Time.time < forcedHorizontalVelocityUntil;
        moveInput = controlsLocked ? 0f : Input.GetAxis("Horizontal");

        if (forcedHorizontalActive)
            rb.velocity = new Vector2(forcedHorizontalVelocity, rb.velocity.y);
        else if (!controlsLocked)
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
        else
            rb.velocity = new Vector2(0f, rb.velocity.y);

        if (!forcedHorizontalActive && !facingRight && moveInput > 0)
            Flip();
        else if (!forcedHorizontalActive && facingRight && moveInput < 0)
            Flip();

        float horizontalSpeed = rb != null ? Mathf.Abs(rb.velocity.x) : 0f;
        bool isActuallyMovingHorizontally = horizontalSpeed > runningVelocityThreshold;
        bool blockedByWall = !controlsLocked && Mathf.Abs(moveInput) > 0.01f && IsBlockedByWall(Mathf.Sign(moveInput));
        isBlockedByWallNow = blockedByWall;
        anim.SetBool("isRunning", presentationRunningOverride || (!controlsLocked && Mathf.Abs(moveInput) > 0.01f && isActuallyMovingHorizontally && !blockedByWall));
    }

    void Update()
    {
        if (GameState.isPaused)
            return;

        UpdateGroundedState();

        if (lockControlUntilGrounded && Time.time >= controlLockedUntil && isGrounded)
            lockControlUntilGrounded = false;

        bool controlsLocked = Time.time < controlLockedUntil || lockControlUntilGrounded;

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressedTime = Time.time;

        if (!controlsLocked && CanExecuteBufferedJump())
            ExecuteJump();

        isVisuallyAirborne = EvaluateVisualAirborne();
        anim.SetBool("isJumping", isVisuallyAirborne);
        UpdateAirTriggerState();
        UpdateWalledTriggerState(controlsLocked);
    }

    void LateUpdate()
    {
        UpdateShadow();
    }

    public void BlockHorizontalControl(float duration)
    {
        controlLockedUntil = Mathf.Max(controlLockedUntil, Time.time + duration);
    }

    public void ApplyExternalKnockback(float horizontalVelocity, float duration, bool keepLockedUntilGrounded = false)
    {
        controlLockedUntil = Mathf.Max(controlLockedUntil, Time.time + duration);
        lockControlUntilGrounded = lockControlUntilGrounded || keepLockedUntilGrounded;
        forcedHorizontalVelocity = horizontalVelocity;
        forcedHorizontalVelocityUntil = Mathf.Max(forcedHorizontalVelocityUntil, Time.time + duration);

        if (rb != null)
            rb.velocity = new Vector2(horizontalVelocity, rb.velocity.y);
    }

    public void LockAllControl(float duration)
    {
        controlLockedUntil = Mathf.Max(controlLockedUntil, Time.time + duration);
        lockControlUntilGrounded = false;
        forcedHorizontalVelocity = 0f;
        forcedHorizontalVelocityUntil = Time.time - 0.01f;

        if (rb != null)
            rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    public void RefreshPresentation(bool rebindAnimator = false)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();
        ownColliders = GetComponents<Collider2D>();
        if (shadowTransform == null)
        {
            Transform foundShadow = transform.Find("ntym");
            if (foundShadow != null)
                shadowTransform = foundShadow;
        }

        if (shadowTransform != null && shadowRenderer == null)
        {
            shadowRenderer = shadowTransform.GetComponent<SpriteRenderer>();
            shadowBaseScale = shadowTransform.localScale;
            shadowBaseLocalZ = shadowTransform.localPosition.z;
        }

        moveInput = 0f;
        jumpPressedTime = -999f;
        jumpConsumed = false;
        airborneFromJump = false;
        airTriggerArmed = false;
        wallTriggerArmed = false;
        isBlockedByWallNow = false;
        lastGroundedTime = -999f;

        if (anim != null)
        {
            if (rebindAnimator)
            {
                anim.Rebind();
                anim.Update(0f);
            }

            anim.SetBool("isRunning", presentationRunningOverride);
            isGrounded = EvaluateGroundContact();
            isVisuallyAirborne = EvaluateVisualAirborne();
            anim.SetBool("isJumping", isVisuallyAirborne);
            anim.ResetTrigger("isAir0");
            anim.ResetTrigger("isWalled");
        }

        if (rb != null)
            rb.velocity = Vector2.zero;

        ApplyNoStuckPhysicsMaterial();
        UpdateGroundedState();
        UpdateShadow();
    }

    public void SetPresentationRunningOverride(bool enabled)
    {
        presentationRunningOverride = enabled;

        if (anim != null)
            anim.SetBool("isRunning", enabled);
    }

    public bool IsGrounded()
    {
        return isGrounded || EvaluateGroundContact();
    }

    private void UpdateGroundedState()
    {
        bool groundedNow = EvaluateGroundContact();
        bool landedFromAir = !isGrounded && groundedNow;
        isGrounded = groundedNow;
        isVisuallyAirborne = EvaluateVisualAirborne();

        if (groundedNow)
        {
            lastGroundedTime = Time.time;
            lastGroundNearbyTime = Time.time;
            jumpConsumed = false;
            airTriggerArmed = false;
            wallTriggerArmed = false;

            bool descendingOrStill = rb == null || rb.velocity.y <= 0.05f;
            if (landedFromAir && descendingOrStill)
                airborneFromJump = false;
        }
    }

    private bool CanExecuteBufferedJump()
    {
        if (jumpConsumed)
            return false;

        bool jumpBuffered = Time.time - jumpPressedTime <= jumpBufferTime;
        bool recentlyGrounded = Time.time - lastGroundedTime <= coyoteTime;
        return jumpBuffered && recentlyGrounded;
    }

    private void ExecuteJump()
    {
        jumpConsumed = true;
        airborneFromJump = true;
        jumpPressedTime = -999f;
        isGrounded = false;
        airTriggerArmed = false;
        lastGroundedTime = -999f;

        if (rb != null)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

        if (anim != null)
            anim.SetTrigger("takeOf");
    }

    private bool EvaluateGroundContact()
    {
        if (groundCheckOrigin == null)
            return false;

        if (HasGroundContactFromCollision())
            return true;

        Vector2 origin = groundCheckOrigin.position;
        RaycastHit2D boxHit = Physics2D.BoxCast(origin, groundCastSize, 0f, Vector2.down, groundCastDistance, whatIsGround);
        Debug.DrawRay(origin + new Vector2(-groundCastSize.x * 0.5f, 0f), Vector2.down * groundCastDistance, boxHit.collider != null ? Color.cyan : Color.gray);
        Debug.DrawRay(origin + new Vector2(groundCastSize.x * 0.5f, 0f), Vector2.down * groundCastDistance, boxHit.collider != null ? Color.cyan : Color.gray);

        if (boxHit.collider != null)
        {
            float boxAngle = Vector2.Angle(boxHit.normal, Vector2.up);
            if (boxAngle <= maxSlopeAngle)
                return true;
        }

        float totalWidth = raySpacing * (rayCount - 1);
        for (int i = 0; i < rayCount; i++)
        {
            float xOffset = -totalWidth / 2f + i * raySpacing;
            Vector2 rayOrigin = origin + new Vector2(xOffset, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, whatIsGround);

            Debug.DrawRay(rayOrigin, Vector2.down * rayLength, hit.collider != null ? Color.green : Color.red);

            if (hit.collider == null)
                continue;

            float angle = Vector2.Angle(hit.normal, Vector2.up);
            if (angle <= maxSlopeAngle && hit.normal.y > 0.2f)
                return true;
        }

        return false;
    }

    private bool HasGroundContactFromCollision()
    {
        if (rb == null)
            return false;

        int contactCount = rb.GetContacts(contactBuffer);
        if (contactCount <= 0)
            return false;

        float feetY = groundCheckOrigin != null ? groundCheckOrigin.position.y : transform.position.y;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contactBuffer[i];
            if (contact.collider == null)
                continue;

            if (((1 << contact.collider.gameObject.layer) & whatIsGround.value) == 0)
                continue;

            float angle = Vector2.Angle(contact.normal, Vector2.up);
            if (angle > maxSlopeAngle || contact.normal.y <= 0.2f)
                continue;

            if (contact.point.y <= feetY + 0.2f)
                return true;
        }

        return false;
    }

    private bool EvaluateVisualAirborne()
    {
        if (isGrounded)
            return false;

        if (groundCheckOrigin == null)
            return true;

        if (HasGroundWithinDistance(visualAirborneDistance))
        {
            lastGroundNearbyTime = Time.time;
            return false;
        }

        return Time.time - lastGroundNearbyTime > visualAirborneGrace;
    }

    private bool ShouldTriggerAirAnimation()
    {
        if (isGrounded)
            return false;

        if (airborneFromJump || jumpConsumed)
            return false;

        if (groundCheckOrigin == null)
            return true;

        if (HasGroundWithinDistance(visualAirborneDistance))
        {
            lastGroundNearbyTime = Time.time;
            return false;
        }

        return Time.time - lastGroundNearbyTime > airBoolEnterDelay;
    }

    private void UpdateAirTriggerState()
    {
        if (anim == null)
            return;

        if (ShouldTriggerAirAnimation())
        {
            if (airTriggerArmed)
                return;

            airTriggerArmed = true;
            anim.ResetTrigger("isAir0");
            anim.SetTrigger("isAir0");
            return;
        }

        if (isGrounded)
        {
            airTriggerArmed = false;
            anim.ResetTrigger("isAir0");
        }
    }

    private bool HasGroundWithinDistance(float distance)
    {
        Vector2 origin = groundCheckOrigin.position;
        RaycastHit2D boxHit = Physics2D.BoxCast(origin, groundCastSize, 0f, Vector2.down, distance, whatIsGround);
        if (boxHit.collider != null)
        {
            float angle = Vector2.Angle(boxHit.normal, Vector2.up);
            if (angle <= maxSlopeAngle && boxHit.normal.y > 0.2f)
                return true;
        }

        float totalWidth = raySpacing * (rayCount - 1);
        for (int i = 0; i < rayCount; i++)
        {
            float xOffset = -totalWidth / 2f + i * raySpacing;
            Vector2 rayOrigin = origin + new Vector2(xOffset, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, distance, whatIsGround);
            if (hit.collider == null)
                continue;

            float angle = Vector2.Angle(hit.normal, Vector2.up);
            if (angle <= maxSlopeAngle && hit.normal.y > 0.2f)
                return true;
        }

        return false;
    }

    private bool IsBlockedByWall(float directionSign)
    {
        if (Mathf.Abs(directionSign) < 0.01f)
            return false;

        Collider2D referenceCollider = null;
        if (ownColliders != null)
        {
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] != null && ownColliders[i].enabled)
                {
                    referenceCollider = ownColliders[i];
                    break;
                }
            }
        }

        if (referenceCollider == null)
            return false;

        Bounds bounds = referenceCollider.bounds;
        Vector2 boxSize = new Vector2(Mathf.Max(0.02f, bounds.size.x * 0.18f), Mathf.Max(0.02f, bounds.size.y - wallCheckInset * 2f));
        Vector2 origin = bounds.center + new Vector3(directionSign * (bounds.extents.x - boxSize.x * 0.5f), 0f, 0f);
        if (HasWallBlockingContact(directionSign, bounds))
            return true;

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, new Vector2(directionSign, 0f), wallCheckDistance, whatIsGround);
        Debug.DrawRay(origin + Vector2.up * (boxSize.y * 0.5f), Vector2.right * directionSign * wallCheckDistance, hit.collider != null ? Color.yellow : Color.blue);
        Debug.DrawRay(origin - Vector2.up * (boxSize.y * 0.5f), Vector2.right * directionSign * wallCheckDistance, hit.collider != null ? Color.yellow : Color.blue);

        if (hit.collider == null)
            return false;

        if (Mathf.Sign(hit.normal.x) != -Mathf.Sign(directionSign))
            return false;

        if (Mathf.Abs(hit.normal.x) > 0.45f)
            return true;

        return hit.normal.y < 0.82f;
    }

    private bool HasWallBlockingContact(float directionSign, Bounds referenceBounds)
    {
        if (rb == null)
            return false;

        int contactCount = rb.GetContacts(contactBuffer);
        if (contactCount <= 0)
            return false;

        float lowerContactLimit = referenceBounds.min.y - 0.02f;
        float upperContactLimit = referenceBounds.max.y - 0.04f;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contactBuffer[i];
            if (contact.collider == null)
                continue;

            if (((1 << contact.collider.gameObject.layer) & whatIsGround.value) == 0)
                continue;

            if (contact.point.y < lowerContactLimit || contact.point.y > upperContactLimit)
                continue;

            if (Mathf.Sign(contact.normal.x) != -Mathf.Sign(directionSign))
                continue;

            if (Mathf.Abs(contact.normal.x) > 0.3f)
                return true;

            if (Mathf.Abs(contact.normal.x) > 0.12f && contact.normal.y < 0.9f)
                return true;
        }

        return false;
    }

    private void UpdateWalledTriggerState(bool controlsLocked)
    {
        if (anim == null)
            return;

        bool movementIntent = !controlsLocked && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f;
        bool shouldTriggerWall = isGrounded && movementIntent && isBlockedByWallNow;

        if (shouldTriggerWall)
        {
            if (!wallTriggerArmed)
            {
                wallTriggerArmed = true;
                anim.ResetTrigger("isWalled");
                anim.SetTrigger("isWalled");
            }

            return;
        }

        wallTriggerArmed = false;
        anim.ResetTrigger("isWalled");
    }

    private void ApplyNoStuckPhysicsMaterial()
    {
        if (ownColliders == null || ownColliders.Length == 0)
            ownColliders = GetComponents<Collider2D>();

        if (noStuckMaterial == null)
        {
            noStuckMaterial = new PhysicsMaterial2D("RuntimeNoStuck")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D collider = ownColliders[i];
            if (collider != null)
                collider.sharedMaterial = noStuckMaterial;
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        transform.Rotate(Vector3.up * 180);
    }

    private void UpdateShadow()
    {
        if (shadowTransform == null || shadowRenderer == null)
            return;

        Vector3 rayOrigin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, shadowRayDistance, whatIsGround);

        if (hit.collider == null)
        {
            Color hiddenColor = shadowRenderer.color;
            hiddenColor.a = Mathf.Lerp(hiddenColor.a, 0f, Time.deltaTime * shadowFollowSpeed);
            shadowRenderer.color = hiddenColor;
            return;
        }

        float heightAboveGround = Mathf.Max(0f, rayOrigin.y - hit.point.y);
        float normalizedHeight = Mathf.Clamp01(heightAboveGround / Mathf.Max(0.01f, shadowHeightForMinScale));

        Vector3 targetWorldPosition = new Vector3(transform.position.x, hit.point.y + shadowGroundOffset, transform.position.z);
        Vector3 targetLocalPosition = transform.InverseTransformPoint(targetWorldPosition);
        targetLocalPosition.z = shadowBaseLocalZ;
        shadowTransform.localPosition = Vector3.Lerp(
            shadowTransform.localPosition,
            targetLocalPosition,
            Time.deltaTime * shadowFollowSpeed);

        float targetScaleValue = Mathf.Lerp(groundedShadowScale, airborneShadowScale, normalizedHeight);
        Vector3 targetScale = new Vector3(
            shadowBaseScale.x * targetScaleValue,
            shadowBaseScale.y * targetScaleValue,
            shadowBaseScale.z);
        shadowTransform.localScale = Vector3.Lerp(
            shadowTransform.localScale,
            targetScale,
            Time.deltaTime * shadowFollowSpeed);

        Color targetColor = shadowRenderer.color;
        targetColor.a = Mathf.Lerp(groundedShadowAlpha, airborneShadowAlpha, normalizedHeight);
        shadowRenderer.color = Color.Lerp(shadowRenderer.color, targetColor, Time.deltaTime * shadowFollowSpeed);
    }
}
