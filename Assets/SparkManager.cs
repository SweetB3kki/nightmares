using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SparkManager : MonoBehaviour
{
    public static SparkManager Instance;
    private const string SparkCounterObjectName = "SparkCounterText";

    public int sparkCount = 0;
    public TMP_Text sparkText;

    private readonly List<TMP_Text> sparkTexts = new List<TMP_Text>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        RefreshSparkTexts();
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    public void AddSpark()
    {
        sparkCount++;
        UpdateUI();
    }

    public void SetSparkCount(int count)
    {
        sparkCount = Mathf.Max(0, count);
        UpdateUI();
    }

    public void UpdateUI()
    {
        RefreshSparkTexts();

        for (int i = sparkTexts.Count - 1; i >= 0; i--)
        {
            TMP_Text text = sparkTexts[i];
            if (text == null)
            {
                sparkTexts.RemoveAt(i);
                continue;
            }

            text.text = sparkCount + "/30";
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSparkTexts();
        UpdateUI();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RefreshSparkTexts();
    }

    private void RefreshSparkTexts()
    {
        sparkTexts.Clear();
        AddSparkTextIfValid(sparkText);

        int loadedSceneCount = SceneManager.sceneCount;
        for (int i = 0; i < loadedSceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject sparkCounterObject = FindInHierarchy(roots[rootIndex].transform, SparkCounterObjectName);
                if (sparkCounterObject == null)
                    continue;

                TMP_Text text = sparkCounterObject.GetComponent<TMP_Text>();
                if (text == null)
                    text = sparkCounterObject.GetComponentInChildren<TMP_Text>(true);

                AddSparkTextIfValid(text);
            }
        }

        sparkText = sparkTexts.Count > 0 ? sparkTexts[0] : null;
    }

    private void AddSparkTextIfValid(TMP_Text text)
    {
        if (text == null || sparkTexts.Contains(text))
            return;

        sparkTexts.Add(text);
    }

    private static GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
