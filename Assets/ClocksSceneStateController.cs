using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClocksSceneStateController : MonoBehaviour
{
    private const string ClocksSceneName = "clocks";
    private const string IntroDialogueControllerName = "GameObject 2";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HandleInitialScene()
    {
        ApplyToScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene(scene);
    }

    private static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != ClocksSceneName)
            return;

        if (!GameState.allGearsInserted && !GameState.clocksPuzzleCompleted)
            return;

        Animator[] animators = Object.FindObjectsOfType<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.gameObject.scene != scene)
                continue;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null || controller.name != IntroDialogueControllerName)
                continue;

            animator.enabled = false;
            animator.gameObject.SetActive(false);
        }
    }
}
