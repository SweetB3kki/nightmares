using UnityEngine;
using UnityEngine.UI;

public class TakeItem : MonoBehaviour
{
    public Text text;
    public int money;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Destroy(collision.gameObject);
            money++;
            text.text = money.ToString();
        }
    }
}
