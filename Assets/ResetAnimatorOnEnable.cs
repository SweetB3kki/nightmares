using UnityEngine;

public class ResetAnimatorOnEnable : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        animator.Rebind();      // сбрасывает все значения Animator к текущему состоянию
        animator.Update(0f);    // обновляет его на этом кадре
    }
}
