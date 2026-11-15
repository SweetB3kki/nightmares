using UnityEngine;

public class HeadCollider : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Проверяем, что столкновение произошло с объектом ног игрока (объект с тегом "PlayerFoot")
        if (collision.CompareTag("PlayerFoot"))
        {
            Debug.Log("Игрок соприкоснулся с головой призрака сверху!");
            
            // Находим родительский объект (монстр) и уничтожаем его
            Destroy(transform.parent.gameObject);
        }
    }
}
