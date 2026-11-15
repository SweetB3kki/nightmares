using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalScenePreloader : MonoBehaviour
{
    private sealed class PreloadedSceneState
    {
        public bool isLoading;
        public bool rootsHidden;
        public readonly Dictionary<GameObject, bool> rootActiveStates = new Dictionary<GameObject, bool>();
    }

    private static LocalScenePreloader instance;

    [SerializeField] private float initialClocksPreloadDelay = 2f;
    [SerializeField] private string clocksSceneName = "clocks";

    private readonly Dictionary<string, PreloadedSceneState> sceneStates = new Dictionary<string, PreloadedSceneState>();
    private readonly Dictionary<string, Coroutine> scheduledPreloads = new Dictionary<string, Coroutine>();
    private Coroutine startupPreloadRoutine;

    public static LocalScenePreloader Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LocalScenePreloader");
                instance = go.AddComponent<LocalScenePreloader>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Start()
    {
        TryScheduleInitialClocksPreload();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }

    public void PreloadSceneIfNeeded(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (sceneStates.TryGetValue(sceneName, out PreloadedSceneState existingState))
        {
            if (existingState.isLoading)
                return;

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                return;
        }

        StartCoroutine(PreloadSceneRoutine(sceneName));
    }

    public void RequestPreloadScene(string sceneName, float delay)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (scheduledPreloads.TryGetValue(sceneName, out Coroutine scheduledCoroutine) && scheduledCoroutine != null)
            return;

        if (sceneStates.TryGetValue(sceneName, out PreloadedSceneState existingState))
        {
            if (existingState.isLoading)
                return;

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                return;
        }

        scheduledPreloads[sceneName] = StartCoroutine(RequestedPreloadRoutine(sceneName, delay));
    }

    public IEnumerator OpenSceneAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            yield break;

        PreloadedSceneState state = GetOrCreateState(sceneName);

        while (state.isLoading)
            yield return null;

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;

            loadedScene = SceneManager.GetSceneByName(sceneName);
        }

        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            yield break;

        RestoreSceneRoots(state);
        SceneManager.SetActiveScene(loadedScene);
    }

    private IEnumerator PreloadSceneRoutine(string sceneName)
    {
        PreloadedSceneState state = GetOrCreateState(sceneName);
        if (state.isLoading)
            yield break;

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            if (!state.rootsHidden)
                CaptureAndHideSceneRoots(state, loadedScene);
            yield break;
        }

        state.isLoading = true;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (loadOperation != null && !loadOperation.isDone)
            yield return null;

        loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
            CaptureAndHideSceneRoots(state, loadedScene);

        state.isLoading = false;
    }

    private void CaptureAndHideSceneRoots(PreloadedSceneState state, Scene scene)
    {
        state.rootActiveStates.Clear();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            state.rootActiveStates[root] = root.activeSelf;
            if (root.activeSelf)
                root.SetActive(false);
        }

        state.rootsHidden = true;
    }

    private void RestoreSceneRoots(PreloadedSceneState state)
    {
        if (!state.rootsHidden)
            return;

        foreach (KeyValuePair<GameObject, bool> pair in state.rootActiveStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        state.rootsHidden = false;
    }

    private PreloadedSceneState GetOrCreateState(string sceneName)
    {
        if (!sceneStates.TryGetValue(sceneName, out PreloadedSceneState state))
        {
            state = new PreloadedSceneState();
            sceneStates[sceneName] = state;
        }

        return state;
    }

    private void OnSceneUnloaded(Scene unloadedScene)
    {
        if (sceneStates.ContainsKey(unloadedScene.name))
            sceneStates.Remove(unloadedScene.name);
    }

    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        if (loadedScene.name == "SampleScene")
        {
            GameState.isSceneTransitioning = false;
            TryScheduleInitialClocksPreload();
        }

        if (loadedScene.name == "menu")
        {
            PlayerReferenceUtility.HidePersistentPlayerForMenu();
            GameState.isSceneTransitioning = false;
        }
    }

    private void TryScheduleInitialClocksPreload()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene")
            return;

        if (startupPreloadRoutine != null)
            StopCoroutine(startupPreloadRoutine);

        startupPreloadRoutine = StartCoroutine(InitialClocksPreloadRoutine());
    }

    private IEnumerator InitialClocksPreloadRoutine()
    {
        if (initialClocksPreloadDelay > 0f)
            yield return new WaitForSeconds(initialClocksPreloadDelay);

        PreloadSceneIfNeeded(clocksSceneName);
    }

    private IEnumerator RequestedPreloadRoutine(string sceneName, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        while (GameState.isSceneTransitioning || (FailManager.Instance != null && FailManager.Instance.IsFailInProgress))
            yield return null;

        scheduledPreloads.Remove(sceneName);
        PreloadSceneIfNeeded(sceneName);
    }
}
