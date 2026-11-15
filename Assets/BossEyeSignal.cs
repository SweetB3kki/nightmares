using UnityEngine;

public class BossEyeSignal : MonoBehaviour
{
    public void BossEyeStateFromInt(int value)
    {
        if (BossVisionController.Instance == null)
            return;

        // Ignore stray animation events when boss fight is not active.
        bool looking = value == 1 && GameState.bossFightStarted && !GameState.bossCompleted;
        BossVisionController.Instance.BossEyeState(looking);
    }

    public void SetBossDefeatedFromInt(int value)
    {
        if (value == 1)
        {
            GameState.bossCompleted = true;
            Debug.Log("Босс считается пройденным");
        }
    }

    public void ResetBossState()
    {
        GameState.bossFightStarted = false;

        Animator animator = GetComponent<Animator>();
        if (animator == null)
            return;

        animator.ResetTrigger("StartDialogue");
        animator.Rebind();
        animator.Update(0f);
        animator.Play("Idle", 0, 0f);
        animator.speed = 1f;

        BossVisionController.ForceResetVision();
    }
}
