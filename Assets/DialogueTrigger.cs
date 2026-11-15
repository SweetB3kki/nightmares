using UnityEngine;

public class DialogueTriggerScript : MonoBehaviour
{
    public Animator dialogueAnimator;
    [Header("Rules")]
    public bool skipIfAllGearsInserted = false;
    public bool triggerOnce = false;
    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        if (GameState.isSceneTransitioning)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (skipIfAllGearsInserted && GameState.allGearsInserted)
            return;

        if (dialogueAnimator == null)
        {
            Debug.LogError("DialogueTriggerScript: dialogueAnimator is not assigned.");
            return;
        }

        hasTriggered = true;
        Debug.Log("Player entered dialogue trigger zone.");
        dialogueAnimator.SetTrigger("StartDialogue");
    }
}
