using UnityEngine;

public class GearSlot : MonoBehaviour
{
    public int gearNumber = 1;
    public GameObject visualGear;
    public GameObject target;

    private void Start()
    {
        RefreshState();
    }

    private void OnMouseDown()
    {
        bool inserted = false;

        if (gearNumber == 1 && GameState.hasGear1 && !GameState.gear1Inserted)
        {
            GameState.gear1Inserted = true;
            GameState.hasGear1 = false;
            inserted = true;
        }
        else if (gearNumber == 2 && GameState.hasGear2 && !GameState.gear2Inserted)
        {
            GameState.gear2Inserted = true;
            GameState.hasGear2 = false;
            inserted = true;
        }

        if (!inserted)
        {
            Debug.Log("Нельзя вставить: шестерёнка не получена или уже вставлена.");
            return;
        }

        if (visualGear != null)
            visualGear.SetActive(true);

        if (GameState.gear1Inserted && GameState.gear2Inserted)
        {
            GameState.allGearsInserted = true;
            SaveSystem.NotifySampleSceneProgressChanged();

            if (!GameState.clocksPuzzleCompleted)
            {
                GameState.clocksPuzzleCompleted = true;
                SaveSystem.NotifySampleSceneProgressChanged();
                SceneReturner returner = FindObjectOfType<SceneReturner>();
                if (returner != null)
                    returner.ReturnToMainSceneWithFade();
            }
        }
        else
        {
            SaveSystem.NotifySampleSceneProgressChanged();
        }
    }

    public void RefreshState()
    {
        bool hasMatchingGear = (gearNumber == 1 && GameState.hasGear1) || (gearNumber == 2 && GameState.hasGear2);
        if (hasMatchingGear && gameObject.activeInHierarchy && target != null)
            target.SetActive(false);

        bool inserted = (gearNumber == 1 && GameState.gear1Inserted) ||
                        (gearNumber == 2 && GameState.gear2Inserted);

        if (visualGear != null)
            visualGear.SetActive(inserted);
    }
}
