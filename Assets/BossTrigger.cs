using UnityEngine;

public class BossTrigger : MonoBehaviour
{
    public Animator dialogueAnimator;
    private bool alreadyTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player") || GameState.bossCompleted || alreadyTriggered)
            return;

        if (dialogueAnimator == null)
        {
            Debug.LogError("BossTrigger: dialogueAnimator is not assigned.");
            return;
        }

        alreadyTriggered = true;
        GameState.bossFightStarted = true;
        Debug.Log("Player entered boss trigger zone. Starting dialogue.");
        dialogueAnimator.SetTrigger("StartDialogue");
    }

    public void ResetTriggerState()
    {
        alreadyTriggered = false;
        GameState.bossFightStarted = false;

        if (dialogueAnimator != null)
            dialogueAnimator.ResetTrigger("StartDialogue");
    }
}
