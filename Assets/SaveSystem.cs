using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    private const string SaveFileName = "savegame.json";
    private const string SampleSceneName = "SampleScene";
    private const string FiolSceneName = "fiol_location";

    private static SaveData cachedData;
    private static bool cacheLoaded;

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool HasSave()
    {
        if (!File.Exists(SavePath))
            return false;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            return data != null && !string.IsNullOrWhiteSpace(data.currentSceneName);
        }
        catch
        {
            return false;
        }
    }

    public static void ClearSave()
    {
        cacheLoaded = true;
        cachedData = CreateEmptyData();

        if (File.Exists(SavePath))
            File.Delete(SavePath);

        ResetRuntimeState();
    }

    public static bool TryLoad(out SaveData data)
    {
        if (!File.Exists(SavePath))
        {
            data = null;
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null || string.IsNullOrWhiteSpace(data.currentSceneName))
            {
                data = null;
                return false;
            }

            EnsureData(data);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError("SaveSystem: failed to load save file. " + exception);
            data = null;
            return false;
        }
    }

    public static void SaveAtCheckpoint(string sceneName, string savePointId, Vector3 spawnPosition)
    {
        SaveAtCheckpoint(sceneName, savePointId, -1, spawnPosition);
    }

    public static void SaveAtCheckpoint(string sceneName, string savePointId, int savePointOrder, Vector3 spawnPosition)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        SaveData data = GetOrCreateCachedData();
        if (string.Equals(data.currentSceneName, sceneName, System.StringComparison.OrdinalIgnoreCase) &&
            data.currentSavePointOrder >= 0 &&
            savePointOrder >= 0 &&
            savePointOrder < data.currentSavePointOrder)
        {
            return;
        }

        data.currentSceneName = sceneName;
        data.currentSavePointId = savePointId ?? string.Empty;
        data.currentSavePointOrder = savePointOrder;
        data.spawnPosition = SerializableVector3.FromVector3(spawnPosition);

        CaptureGlobalState(data);
        CaptureSceneState(data, sceneName);
        WriteData(data);
    }

    public static void SaveLightweightScene(string sceneName, string savePointId, Vector3 spawnPosition)
    {
        SaveLightweightScene(sceneName, savePointId, -1, spawnPosition);
    }

    public static void SaveLightweightScene(string sceneName, string savePointId, int savePointOrder, Vector3 spawnPosition)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        SaveData data = GetOrCreateCachedData();
        data.currentSceneName = sceneName;
        data.currentSavePointId = savePointId ?? string.Empty;
        data.currentSavePointOrder = savePointOrder;
        data.spawnPosition = SerializableVector3.FromVector3(spawnPosition);

        CaptureGlobalState(data);
        WriteData(data);
    }

    public static void NotifySampleSceneProgressChanged()
    {
        SaveData data = GetOrCreateCachedData();
        if (!string.Equals(data.currentSceneName, SampleSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        CaptureGlobalState(data);
        CaptureSampleSceneSnapshot(data);
        WriteData(data);
    }

    public static void MarkSparkCollected(SparkCollector collector)
    {
        if (collector == null || collector.gameObject.scene.name != SampleSceneName)
            return;

        SaveData data = GetOrCreateCachedData();
        AddUnique(data.sampleScene.collectedSparkIds, SaveIdentityUtility.GetObjectId(collector));
        NotifySampleSceneProgressChanged();
    }

    public static void MarkUnderCollapseTriggered(UnderCollapseDestructionController controller)
    {
        if (controller == null || controller.gameObject.scene.name != SampleSceneName)
            return;

        SaveData data = GetOrCreateCachedData();
        data.sampleScene.underCollapseTriggered = true;
        NotifySampleSceneProgressChanged();
    }

    public static bool BeginResumeLoad()
    {
        if (!TryLoad(out SaveData data))
            return false;

        GameObject loaderObject = new GameObject("SaveResumeLoader");
        Object.DontDestroyOnLoad(loaderObject);
        SaveResumeLoader loader = loaderObject.AddComponent<SaveResumeLoader>();
        loader.Begin(data);
        return true;
    }

    internal static void ApplyLoadedStateToCurrentScene(SaveData data)
    {
        if (data == null)
            return;

        EnsureData(data);
        cacheLoaded = true;
        cachedData = data;

        ApplyGlobalState(data);

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject player = ResolvePlayerForLoadedScene();
        if (player != null)
        {
            PlayerReferenceUtility.PreparePlayerForGameplay(player);
            player.transform.position = data.spawnPosition.ToVector3();

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.LockAllControl(0.15f);

            GameState.mainScenePlayer = player;
        }

        ScenePlayerBindingUtility.RefreshSceneBindings(activeScene, player);

        FailManager failManager = ScenePlayerBindingUtility.FindSceneComponent<FailManager>(activeScene);
        if (failManager != null)
        {
            if (player != null)
                failManager.BindPlayerReference(player);

            failManager.UpdateCheckpoint(data.spawnPosition.ToVector3());
        }

        if (activeScene.name == SampleSceneName)
            ApplySampleSceneState(activeScene, data);
        else if (activeScene.name == FiolSceneName && player != null)
            SaveLightweightScene(activeScene.name, data.currentSavePointId, player.transform.position);

        SparkManager.Instance?.UpdateUI();

        InventoryDisplay[] inventoryDisplays = Object.FindObjectsOfType<InventoryDisplay>(true);
        for (int i = 0; i < inventoryDisplays.Length; i++)
        {
            if (inventoryDisplays[i] != null)
                inventoryDisplays[i].Refresh();
        }
    }

    private static void ApplySampleSceneState(Scene scene, SaveData data)
    {
        HashSet<string> collectedSparkIds = new HashSet<string>(data.sampleScene.collectedSparkIds ?? new List<string>());

        SparkCollector[] sparkCollectors = Object.FindObjectsOfType<SparkCollector>(true);
        for (int i = 0; i < sparkCollectors.Length; i++)
        {
            SparkCollector collector = sparkCollectors[i];
            if (collector == null || collector.gameObject.scene != scene)
                continue;

            string collectorId = SaveIdentityUtility.GetObjectId(collector);
            bool isCollected = collectedSparkIds.Contains(collectorId);

            bool shouldBeActive = !isCollected;
            if (collector.name == "ClockSpark")
                shouldBeActive = data.allGearsInserted && !isCollected;
            else if (collector.name == "spark_pazzle")
                shouldBeActive = data.picturePuzzleCompleted && !isCollected;

            collector.gameObject.SetActive(shouldBeActive);
        }

        ChestInteraction[] chests = Object.FindObjectsOfType<ChestInteraction>(true);
        for (int i = 0; i < chests.Length; i++)
        {
            ChestInteraction chest = chests[i];
            if (chest == null || chest.gameObject.scene != scene)
                continue;

            chest.ApplySavedState(data.sampleScene.chestOpened);
        }

        KeyGiver[] keyGivers = Object.FindObjectsOfType<KeyGiver>(true);
        for (int i = 0; i < keyGivers.Length; i++)
        {
            KeyGiver keyGiver = keyGivers[i];
            if (keyGiver == null || keyGiver.gameObject.scene != scene)
                continue;

            keyGiver.ApplySavedState(data.hasKey, data.sampleScene.chestOpened);
        }

        GearSlot[] gearSlots = Object.FindObjectsOfType<GearSlot>(true);
        for (int i = 0; i < gearSlots.Length; i++)
        {
            GearSlot slot = gearSlots[i];
            if (slot == null || slot.gameObject.scene != scene)
                continue;

            slot.RefreshState();
        }

        UnderCollapseDestructionController[] collapses = Object.FindObjectsOfType<UnderCollapseDestructionController>(true);
        for (int i = 0; i < collapses.Length; i++)
        {
            UnderCollapseDestructionController collapse = collapses[i];
            if (collapse == null || collapse.gameObject.scene != scene)
                continue;

            if (data.sampleScene.underCollapseTriggered)
                collapse.ApplyCollapsedStateImmediate();
        }
    }

    private static void CaptureSceneState(SaveData data, string sceneName)
    {
        if (sceneName == SampleSceneName)
            CaptureSampleSceneSnapshot(data);
    }

    private static void CaptureSampleSceneSnapshot(SaveData data)
    {
        EnsureData(data);

        ChestInteraction chest = Object.FindObjectOfType<ChestInteraction>(true);
        if (chest != null && chest.gameObject.scene.name == SampleSceneName)
            data.sampleScene.chestOpened = chest.IsOpened;

        UnderCollapseDestructionController underCollapse = Object.FindObjectOfType<UnderCollapseDestructionController>(true);
        if (underCollapse != null && underCollapse.gameObject.scene.name == SampleSceneName)
            data.sampleScene.underCollapseTriggered = data.sampleScene.underCollapseTriggered || underCollapse.IsCollapsed;
    }

    private static void CaptureGlobalState(SaveData data)
    {
        EnsureData(data);

        data.sparkCount = SparkManager.Instance != null ? SparkManager.Instance.sparkCount : data.sparkCount;
        data.hasKey = PlayerState.hasKey;
        data.hasGear1 = GameState.hasGear1;
        data.hasGear2 = GameState.hasGear2;
        data.gear1Inserted = GameState.gear1Inserted;
        data.gear2Inserted = GameState.gear2Inserted;
        data.allGearsInserted = GameState.allGearsInserted;
        data.clocksPuzzleCompleted = GameState.clocksPuzzleCompleted;
        data.picturePuzzleCompleted = GameState.picturePuzzleCompleted;
        data.clocksCursorUnlocked = GameState.clocksCursorUnlocked;
        data.pictureCursorUnlocked = GameState.pictureCursorUnlocked;
    }

    private static void ApplyGlobalState(SaveData data)
    {
        PlayerState.hasKey = data.hasKey;
        GameState.hasGear1 = data.hasGear1;
        GameState.hasGear2 = data.hasGear2;
        GameState.gear1Inserted = data.gear1Inserted;
        GameState.gear2Inserted = data.gear2Inserted;
        GameState.allGearsInserted = data.allGearsInserted;
        GameState.clocksPuzzleCompleted = data.clocksPuzzleCompleted;
        GameState.picturePuzzleCompleted = data.picturePuzzleCompleted;
        GameState.clocksCursorUnlocked = data.clocksCursorUnlocked;
        GameState.pictureCursorUnlocked = data.pictureCursorUnlocked;
        GameState.isPaused = false;
        GameState.isInLabirint = false;
        GameState.bossFightStarted = false;
        GameState.hasLabyrinthReturnPosition = false;
        GameState.ClearLabyrinthSparkState();

        if (SparkManager.Instance != null)
            SparkManager.Instance.SetSparkCount(data.sparkCount);
    }

    private static GameObject ResolvePlayerForLoadedScene()
    {
        GameObject player = GameState.mainScenePlayer;
        if (player == null)
            player = PlayerReferenceUtility.FindActivePlayerRoot();

        player = PlayerReferenceUtility.ResolvePlayerRoot(player);
        if (player != null)
            return player;

        PlayerController[] controllers = Object.FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] == null)
                continue;

            return controllers[i].gameObject;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return PlayerReferenceUtility.ResolvePlayerRoot(tagged);
    }

    private static void WriteData(SaveData data)
    {
        EnsureData(data);
        cacheLoaded = true;
        cachedData = data;

        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    private static SaveData GetOrCreateCachedData()
    {
        if (cacheLoaded)
        {
            EnsureData(cachedData);
            return cachedData;
        }

        if (TryLoad(out SaveData loaded))
        {
            cacheLoaded = true;
            cachedData = loaded;
            return cachedData;
        }

        cacheLoaded = true;
        cachedData = CreateEmptyData();
        return cachedData;
    }

    private static SaveData CreateEmptyData()
    {
        return new SaveData
        {
            sampleScene = new SampleSceneSaveData()
        };
    }

    private static void EnsureData(SaveData data)
    {
        if (data == null)
            return;

        if (data.sampleScene == null)
            data.sampleScene = new SampleSceneSaveData();
        if (data.sampleScene.collectedSparkIds == null)
            data.sampleScene.collectedSparkIds = new List<string>();
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value) || values.Contains(value))
            return;

        values.Add(value);
    }

    private static void ResetRuntimeState()
    {
        PlayerState.hasKey = false;
        GameState.mainScenePlayer = null;
        GameState.isInLabirint = false;
        GameState.isSceneTransitioning = false;
        GameState.hasLabyrinthReturnPosition = false;
        GameState.labyrinthReturnPosition = Vector3.zero;
        GameState.bossCompleted = false;
        GameState.bossFightStarted = false;
        GameState.hasGear1 = false;
        GameState.hasGear2 = false;
        GameState.allGearsInserted = false;
        GameState.gear1Inserted = false;
        GameState.gear2Inserted = false;
        GameState.clocksPuzzleCompleted = false;
        GameState.picturePuzzleCompleted = false;
        GameState.clocksCursorUnlocked = false;
        GameState.pictureCursorUnlocked = false;
        GameState.isPaused = false;
        GameState.ClearLabyrinthSparkState();

        if (SparkManager.Instance != null)
            SparkManager.Instance.SetSparkCount(0);
    }
}

public sealed class SaveResumeLoader : MonoBehaviour
{
    private SaveData saveData;
    private bool applied;

    public void Begin(SaveData data)
    {
        saveData = data;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameState.isSceneTransitioning = true;
        SceneLoader.LoadScene(data.currentSceneName);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (applied || saveData == null)
            return;

        if (scene.name != saveData.currentSceneName)
            return;

        applied = true;
        StartCoroutine(ApplyAfterLoad());
    }

    private IEnumerator ApplyAfterLoad()
    {
        ScreenFadeUtility.OverlayHandle overlay = ScreenFadeUtility.CreateBlackOverlay();
        Object.DontDestroyOnLoad(overlay.canvasObject);

        yield return null;
        yield return null;

        SaveSystem.ApplyLoadedStateToCurrentScene(saveData);

        if (overlay != null && overlay.canvasObject != null)
            yield return ScreenFadeUtility.RevealAndDestroyOverlay(overlay, 1.1f);

        GameState.isSceneTransitioning = false;
        Destroy(gameObject);
    }
}

