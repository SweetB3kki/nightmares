using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button resumeButton;

    private void Start()
    {
        BindResumeButton();
        RefreshResumeButtonState();
    }

    public void OnPlayClicked()
    {
        SaveSystem.ClearSave();
        SceneLoader.LoadScene("SampleScene");
    }

    public void OnResumeClicked()
    {
        if (!SaveSystem.HasSave())
        {
            RefreshResumeButtonState();
            return;
        }

        SaveSystem.BeginResumeLoad();
    }

    private void BindResumeButton()
    {
        if (resumeButton == null)
            resumeButton = FindButton("resumeButton") ?? FindButton("loadingButton");

        if (resumeButton == null)
            return;

        resumeButton.onClick.RemoveAllListeners();
        resumeButton.onClick.AddListener(OnResumeClicked);
    }

    private void RefreshResumeButtonState()
    {
        if (resumeButton == null)
            return;

        resumeButton.interactable = SaveSystem.HasSave();
    }

    private static Button FindButton(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }
}
