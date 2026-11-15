using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawn : MonoBehaviour
{
    public static string spawnPointName;
    private static PlayerSpawn instance;
    private Transform persistentRoot;

    private void Awake()
    {
        persistentRoot = transform.root;

        if (instance != null && instance != this)
        {
            if (persistentRoot != null)
                Destroy(persistentRoot.gameObject);
            else
                Destroy(gameObject);
            return;
        }

        instance = this;
        if (persistentRoot != null)
            DontDestroyOnLoad(persistentRoot.gameObject);
        else
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(spawnPointName))
            return;

        GameObject point = GameObject.Find(spawnPointName);
        if (point == null)
            return;

        if (persistentRoot != null)
            persistentRoot.position = point.transform.position;
        else
            transform.position = point.transform.position;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
            instance = null;
    }
}
