using UnityEngine;

public class InventoryDisplay : MonoBehaviour
{
    public GameObject gear1Icon;
    public GameObject gear2Icon;

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        Debug.Log("Обновление инвентаря: hasGear1=" + GameState.hasGear1 + ", hasGear2=" + GameState.hasGear2);

        if (gear1Icon != null)
            gear1Icon.SetActive(GameState.hasGear1); // ← обновлено

        if (gear2Icon != null)
            gear2Icon.SetActive(GameState.hasGear2);
    }
}
