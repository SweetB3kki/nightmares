using UnityEngine;

public class PlayerStatModifierZone : MonoBehaviour
{
    public float newSpeed = 25f;
    public float newJumpForce = 27f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.speed = newSpeed;
                player.jumpForce = newJumpForce;
            }
        }
    }
}
