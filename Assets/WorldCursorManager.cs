using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class WorldCursorManager : MonoBehaviour
{
    private const string SampleSceneName = "SampleScene";
    private const string ClocksSceneName = "clocks";
    private const string PuzzleSceneName = "Puzzle";

    private const string NormalCursorPath = "Cursors/normal_select.png";
    private const string HelpCursorPath = "Cursors/help_select.png";
    private const string LinkCursorPath = "Cursors/link_select.png";

    private const int LinkFrameColumns = 5;
    private const int LinkFrameRows = 2;
    private const int LinkFrameCount = 9;
    private const float LinkFrameDuration = 0.06f;

    private static WorldCursorManager instance;

    private readonly Dictionary<HoverTarget, Collider2D> interactiveColliders = new Dictionary<HoverTarget, Collider2D>();

    private Canvas overlayCanvas;
    private RectTransform cursorRect;
    private Image cursorImage;

    private Sprite normalCursor;
    private Sprite helpCursor;
    private readonly List<Sprite> linkCursorFrames = new List<Sprite>();

    private float linkAnimationTimer;
    private int linkAnimationIndex;
    private CursorVisualState currentVisualState = CursorVisualState.None;
    private HoverTarget lastHoverTarget = HoverTarget.None;
    private bool clockUnlockPending;
    private bool pictureUnlockPending;

    private enum CursorVisualState
    {
        None,
        Normal,
        Help,
        Link
    }

    private enum HoverTarget
    {
        None,
        Clock,
        Picture,
        Chest
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject managerObject = new GameObject("WorldCursorManager");
        instance = managerObject.AddComponent<WorldCursorManager>();
        DontDestroyOnLoad(managerObject);
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
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        CreateOverlay();
        LoadCursorAssets();
        ResolveSceneTargets();
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        Cursor.visible = true;
    }

    private void OnApplicationQuit()
    {
        Cursor.visible = true;
    }

    private void Update()
    {
        if (cursorRect == null || cursorImage == null)
            return;

        UpdateCursorPosition();
        UpdateCursorState();
        TrackFirstClickUnlock();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveSceneTargets();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        ResolveSceneTargets();
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("CursorOverlayCanvas");
        DontDestroyOnLoad(canvasObject);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject cursorObject = new GameObject("WorldCursor");
        cursorObject.transform.SetParent(canvasObject.transform, false);
        cursorRect = cursorObject.AddComponent<RectTransform>();
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.pivot = new Vector2(0f, 1f);
        cursorRect.sizeDelta = new Vector2(64f, 64f);

        cursorImage = cursorObject.AddComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
    }

    private void LoadCursorAssets()
    {
        normalCursor = CreateSpriteFromStreamingAsset(NormalCursorPath, 64f);
        helpCursor = CreateSpriteFromStreamingAsset(HelpCursorPath, 64f);
        CreateLinkCursorFrames();

        if (normalCursor != null)
            ApplyCursorVisual(normalCursor, CursorVisualState.Normal);
    }

    private void CreateLinkCursorFrames()
    {
        Texture2D texture = LoadTextureFromStreamingAsset(LinkCursorPath);
        if (texture == null)
            return;

        int frameWidth = texture.width / LinkFrameColumns;
        int frameHeight = texture.height / LinkFrameRows;

        for (int index = 0; index < LinkFrameCount; index++)
        {
            int column = index % LinkFrameColumns;
            int row = index / LinkFrameColumns;
            int x = column * frameWidth;
            int y = texture.height - ((row + 1) * frameHeight);
            Rect rect = new Rect(x, y, frameWidth, frameHeight);
            Sprite frame = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), frameWidth);
            frame.name = $"link_cursor_{index}";
            linkCursorFrames.Add(frame);
        }
    }

    private void ResolveSceneTargets()
    {
        interactiveColliders.Clear();

        Scene sampleScene = SceneManager.GetSceneByName(SampleSceneName);
        if (!sampleScene.IsValid() || !sampleScene.isLoaded)
            return;

        CacheColliderForComponent<ClickableClock>(sampleScene, HoverTarget.Clock);
        CacheColliderForComponent<PuzzleTrigger>(sampleScene, HoverTarget.Picture);
        CacheColliderForComponent<ChestInteraction>(sampleScene, HoverTarget.Chest);
    }

    private void CacheColliderForComponent<T>(Scene scene, HoverTarget hoverTarget) where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject.scene != scene)
                continue;

            Collider2D collider = component.GetComponent<Collider2D>();
            if (collider == null)
                collider = component.GetComponentInChildren<Collider2D>(true);

            if (collider == null)
                continue;

            interactiveColliders[hoverTarget] = collider;
            return;
        }
    }

    private void UpdateCursorPosition()
    {
        cursorRect.position = (Vector2)Input.mousePosition + GetVisualOffset(currentVisualState);
    }

    private void UpdateCursorState()
    {
        HoverTarget hoverTarget = ResolveHoverTarget();
        FinalizePendingUnlocks(hoverTarget);

        switch (hoverTarget)
        {
            case HoverTarget.Clock:
                if (GameState.clocksCursorUnlocked)
                    ApplyCursorVisual(GetCurrentLinkFrame(), CursorVisualState.Link);
                else
                    ApplyCursorVisual(helpCursor, CursorVisualState.Help);
                break;

            case HoverTarget.Picture:
                if (GameState.pictureCursorUnlocked)
                    ApplyCursorVisual(GetCurrentLinkFrame(), CursorVisualState.Link);
                else
                    ApplyCursorVisual(helpCursor, CursorVisualState.Help);
                break;

            case HoverTarget.Chest:
                ApplyCursorVisual(GetCurrentLinkFrame(), CursorVisualState.Link);
                break;

            default:
                ApplyCursorVisual(normalCursor, CursorVisualState.Normal);
                break;
        }

        lastHoverTarget = hoverTarget;
    }

    private void TrackFirstClickUnlock()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        HoverTarget hoverTarget = ResolveHoverTarget();
        if (hoverTarget == HoverTarget.Clock && !GameState.clocksCursorUnlocked)
            clockUnlockPending = true;
        else if (hoverTarget == HoverTarget.Picture && !GameState.pictureCursorUnlocked)
            pictureUnlockPending = true;
    }

    private void FinalizePendingUnlocks(HoverTarget hoverTarget)
    {
        if (clockUnlockPending && lastHoverTarget == HoverTarget.Clock && hoverTarget != HoverTarget.Clock)
        {
            GameState.clocksCursorUnlocked = true;
            clockUnlockPending = false;
        }

        if (pictureUnlockPending && lastHoverTarget == HoverTarget.Picture && hoverTarget != HoverTarget.Picture)
        {
            GameState.pictureCursorUnlocked = true;
            pictureUnlockPending = false;
        }
    }

    private HoverTarget ResolveHoverTarget()
    {
        if (GameState.isPaused || GameState.isInLabirint || GameState.isSceneTransitioning)
            return HoverTarget.None;

        Scene sampleScene = SceneManager.GetSceneByName(SampleSceneName);
        if (!sampleScene.IsValid() || !sampleScene.isLoaded)
            return HoverTarget.None;

        if (IsOverlaySceneVisible(ClocksSceneName) || IsOverlaySceneVisible(PuzzleSceneName))
            return HoverTarget.None;

        if (interactiveColliders.Count == 0)
            ResolveSceneTargets();

        Camera camera = Camera.main;
        if (camera == null)
            return HoverTarget.None;

        Vector2 worldPoint = camera.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
        if (hits == null || hits.Length == 0)
            return HoverTarget.None;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject.scene != sampleScene)
                continue;

            if (IsTargetHit(HoverTarget.Clock, hit))
                return HoverTarget.Clock;

            if (IsTargetHit(HoverTarget.Picture, hit))
                return HoverTarget.Picture;

            if (CanUseChestCursor() && IsTargetHit(HoverTarget.Chest, hit))
                return HoverTarget.Chest;
        }

        return HoverTarget.None;
    }

    private bool IsTargetHit(HoverTarget hoverTarget, Collider2D hit)
    {
        if (!interactiveColliders.TryGetValue(hoverTarget, out Collider2D targetCollider) || targetCollider == null)
            return false;

        Transform hitTransform = hit.transform;
        Transform targetTransform = targetCollider.transform;
        return hit == targetCollider || hitTransform.IsChildOf(targetTransform) || targetTransform.IsChildOf(hitTransform);
    }

    private void ApplyCursorVisual(Sprite sprite, CursorVisualState visualState)
    {
        if (sprite == null || cursorImage == null)
            return;

        currentVisualState = visualState;
        cursorImage.sprite = sprite;
        cursorRect.sizeDelta = GetDisplaySize(visualState);
        cursorRect.position = (Vector2)Input.mousePosition + GetVisualOffset(visualState);
    }

    private Sprite GetCurrentLinkFrame()
    {
        if (linkCursorFrames.Count == 0)
            return normalCursor;

        linkAnimationTimer += Time.unscaledDeltaTime;
        while (linkAnimationTimer >= LinkFrameDuration)
        {
            linkAnimationTimer -= LinkFrameDuration;
            linkAnimationIndex = (linkAnimationIndex + 1) % linkCursorFrames.Count;
        }

        return linkCursorFrames[linkAnimationIndex];
    }

    private static Vector2 GetVisualOffset(CursorVisualState visualState)
    {
        switch (visualState)
        {
            case CursorVisualState.Help:
                return new Vector2(-8f, 8f);
            case CursorVisualState.Link:
                return new Vector2(-30f, 8f);
            case CursorVisualState.Normal:
            default:
                return new Vector2(-8f, 8f);
        }
    }

    private static Vector2 GetDisplaySize(CursorVisualState visualState)
    {
        switch (visualState)
        {
            case CursorVisualState.Link:
                return new Vector2(72f, 72f);
            case CursorVisualState.Help:
            case CursorVisualState.Normal:
            default:
                return new Vector2(64f, 64f);
        }
    }

    private static Sprite CreateSpriteFromStreamingAsset(string relativePath, float pixelsPerUnit)
    {
        Texture2D texture = LoadTextureFromStreamingAsset(relativePath);
        if (texture == null)
            return null;

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = Path.GetFileNameWithoutExtension(relativePath);
        return sprite;
    }

    private static Texture2D LoadTextureFromStreamingAsset(string relativePath)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] data = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        texture.name = Path.GetFileNameWithoutExtension(relativePath);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.LoadImage(data, false);
        return texture;
    }

    private static bool IsOverlaySceneVisible(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].activeInHierarchy)
                return true;
        }

        return false;
    }

    private static bool CanUseChestCursor()
    {
        if (!PlayerState.hasKey)
            return false;

        if (GameState.hasGear1 || GameState.hasGear2)
            return false;

        return true;
    }
}
