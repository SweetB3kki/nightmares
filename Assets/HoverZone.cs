using UnityEngine;
using UnityEngine.EventSystems;

public class HoverZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Animator imageAnimator;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (imageAnimator == null)
            return;

        imageAnimator.SetTrigger("HoverIn");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (imageAnimator == null)
            return;

        imageAnimator.SetTrigger("HoverOut");
    }
}
