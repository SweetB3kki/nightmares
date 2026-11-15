using UnityEngine;

public static class PlayerReferenceUtility
{
    public static GameObject ResolvePlayerRoot(GameObject candidate)
    {
        if (candidate == null)
            return null;

        PlayerController controller = candidate.GetComponent<PlayerController>();
        if (controller != null)
            return controller.gameObject;

        controller = candidate.GetComponentInParent<PlayerController>(true);
        if (controller != null)
            return controller.gameObject;

        PlayerSpawn spawn = candidate.GetComponent<PlayerSpawn>();
        if (spawn != null)
            return spawn.transform.root.gameObject;

        spawn = candidate.GetComponentInParent<PlayerSpawn>(true);
        if (spawn != null)
            return spawn.transform.root.gameObject;

        Transform root = candidate.transform.root;
        return root != null ? root.gameObject : candidate;
    }

    public static GameObject FindActivePlayerRoot()
    {
        if (GameState.mainScenePlayer != null)
        {
            GameObject resolved = ResolvePlayerRoot(GameState.mainScenePlayer);
            if (resolved != null && resolved.activeInHierarchy)
                return resolved;
        }

        PlayerController[] controllers = Object.FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] == null)
                continue;

            GameObject controllerObject = controllers[i].gameObject;
            if (controllerObject.activeInHierarchy)
                return controllerObject;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return ResolvePlayerRoot(tagged);
    }

    public static void HidePersistentPlayerForMenu()
    {
        GameObject player = ResolvePlayerRoot(GameState.mainScenePlayer);
        if (player == null)
            player = ResolvePlayerRoot(GameObject.FindGameObjectWithTag("Player"));

        if (player == null)
            return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        player.SetActive(false);
        GameState.mainScenePlayer = player;
    }

    public static void PreparePlayerForGameplay(GameObject playerObject, bool refreshAnimator = true)
    {
        GameObject player = ResolvePlayerRoot(playerObject);
        if (player == null)
            return;

        player.SetActive(true);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = true;
            controller.RefreshPresentation(refreshAnimator);
        }

        GameState.mainScenePlayer = player;
    }

    public static void DestroyPersistentPlayerForFreshStart()
    {
        GameObject player = ResolvePlayerRoot(GameState.mainScenePlayer);
        if (player == null)
            player = ResolvePlayerRoot(GameObject.FindGameObjectWithTag("Player"));

        if (player != null)
            Object.Destroy(player);

        PlayerSpawn[] spawns = Object.FindObjectsOfType<PlayerSpawn>(true);
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i] == null)
                continue;

            GameObject root = spawns[i].transform.root != null ? spawns[i].transform.root.gameObject : spawns[i].gameObject;
            if (root != null)
                Object.Destroy(root);
        }

        GameState.mainScenePlayer = null;
        PlayerSpawn.spawnPointName = null;
    }
}
