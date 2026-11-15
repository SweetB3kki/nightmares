using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Scenes : MonoBehaviour
{
    [SerializeField] private Button resumeButton;

    private void Start()
    {
        BindResumeButton();
        RefreshResumeButtonState();
    }

    public void ChangeScenes(int numberScenes)
    {
        GameState.isPaused = false;
        PlayerReferenceUtility.DestroyPersistentPlayerForFreshStart();
        SaveSystem.ClearSave();
        SceneLoader.LoadScene("SampleScene");
    }

    public void Resume()
    {
        if (!SaveSystem.HasSave())
        {
            RefreshResumeButtonState();
            return;
        }

        SaveSystem.BeginResumeLoad();
    }

    public void Exit()
    {
        #if UNITY_EDITOR
        EditorApplication.isPlaying = false; // Для остановки игры в редакторе
        #else
        Application.Quit(); // Для выхода из приложения в сборке
        #endif
    }

    private void BindResumeButton()
    {
        if (resumeButton == null)
        {
            GameObject buttonObject = GameObject.Find("resumeButton");
            if (buttonObject != null)
                resumeButton = buttonObject.GetComponent<Button>();
        }

        if (resumeButton == null)
            return;

        resumeButton.onClick.RemoveListener(Resume);
        resumeButton.onClick.AddListener(Resume);
    }

    private void RefreshResumeButtonState()
    {
        if (resumeButton == null)
            return;

        resumeButton.interactable = SaveSystem.HasSave();
    }
}

