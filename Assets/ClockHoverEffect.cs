using UnityEngine;

public class ClockHoverEffect : MonoBehaviour
{
    public Sprite normalSprite;  // Спрайт без обводки
    public Sprite highlightedSprite;  // Спрайт с обводкой

    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();  // Получаем компонент SpriteRenderer
    }

    private void OnMouseEnter()
    {
        // Меняем спрайт на тот, который с обводкой, когда мышь наводится
        spriteRenderer.sprite = highlightedSprite;
    }

    private void OnMouseExit()
    {
        // Возвращаем спрайт на исходный, когда мышь уходит
        spriteRenderer.sprite = normalSprite;
    }
}
