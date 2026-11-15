using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScaleAndMove : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform target; // image1
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(0.95f, 0.95f, 0.95f);

    public float normalY = 200f;
    public float hoverY = 193.8f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target == null)
            return;

        target.localScale = hoverScale;
        Vector3 pos = target.anchoredPosition;
        pos.y = hoverY;
        target.anchoredPosition = pos;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (target == null)
            return;

        target.localScale = normalScale;
        Vector3 pos = target.anchoredPosition;
        pos.y = normalY;
        target.anchoredPosition = pos;
    }
}
