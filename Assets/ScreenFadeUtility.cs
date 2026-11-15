using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class ScreenFadeUtility
{
    public sealed class OverlayHandle
    {
        public GameObject canvasObject;
        public CanvasGroup canvasGroup;
        public Image fillImage;
    }

    public static OverlayHandle CreateBlackOverlay(int sortingOrder = 9999)
    {
        OverlayHandle overlay = CreatePlainBlackOverlay("RuntimePersistentBlackOverlay", sortingOrder);
        overlay.fillImage.color = Color.black;
        overlay.canvasGroup.alpha = 1f;
        return overlay;
    }

    public static IEnumerator FadeFromBlack(float duration, int sortingOrder = 9999)
    {
        OverlayHandle overlay = CreatePlainBlackOverlay("RuntimeScreenFadeCanvas", sortingOrder);
        overlay.fillImage.color = Color.black;

        if (duration <= 0f)
        {
            Object.Destroy(overlay.canvasObject);
            yield break;
        }

        yield return FadeCanvasGroup(overlay.canvasGroup, 1f, 0f, duration);

        Object.Destroy(overlay.canvasObject);
    }

    public static OverlayHandle CreateVignetteOverlay(int sortingOrder = 9999)
    {
        OverlayHandle overlay = CreateOverlayCanvas("RuntimeVignetteTransitionCanvas", sortingOrder);
        overlay.fillImage.color = new Color(0f, 0f, 0f, 0f);
        overlay.canvasGroup.alpha = 1f;

        CreateCorner(overlay.canvasObject.transform, "TopLeft", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector3.one);
        CreateCorner(overlay.canvasObject.transform, "TopRight", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector3(-1f, 1f, 1f));
        CreateCorner(overlay.canvasObject.transform, "BottomLeft", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector3(1f, -1f, 1f));
        CreateCorner(overlay.canvasObject.transform, "BottomRight", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector3(-1f, -1f, 1f));

        return overlay;
    }

    public static IEnumerator FillOverlayToBlack(OverlayHandle overlay, float duration)
    {
        if (overlay == null || overlay.fillImage == null)
            yield break;

        yield return FadeImageAlpha(overlay.fillImage, overlay.fillImage.color.a, 1f, duration);
    }

    public static IEnumerator RevealAndDestroyOverlay(OverlayHandle overlay, float duration)
    {
        if (overlay == null || overlay.canvasObject == null || overlay.canvasGroup == null)
            yield break;

        yield return FadeCanvasGroup(overlay.canvasGroup, overlay.canvasGroup.alpha, 0f, duration);
        Object.Destroy(overlay.canvasObject);
    }

    public static void PersistOverlayUntilSceneLoaded(OverlayHandle overlay, string targetSceneName, float destroyDelay = 0.1f)
    {
        if (overlay == null || overlay.canvasObject == null || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        Object.DontDestroyOnLoad(overlay.canvasObject);

        PersistentOverlaySceneWatcher watcher = overlay.canvasObject.GetComponent<PersistentOverlaySceneWatcher>();
        if (watcher == null)
            watcher = overlay.canvasObject.AddComponent<PersistentOverlaySceneWatcher>();

        watcher.Configure(targetSceneName, destroyDelay);
    }

    private static void CreateCorner(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector3 scale)
    {
        GameObject cornerObject = new GameObject(name);
        cornerObject.transform.SetParent(parent, false);

        RectTransform rect = cornerObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(900f, 900f);
        rect.localScale = scale;

        Image image = cornerObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.95f);
        image.sprite = CreateCornerSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private static OverlayHandle CreatePlainBlackOverlay(string name, int sortingOrder)
    {
        OverlayHandle overlay = CreateOverlayCanvas(name, sortingOrder);
        overlay.canvasGroup.alpha = 1f;
        return overlay;
    }

    private static OverlayHandle CreateOverlayCanvas(string name, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(name);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(canvasObject.transform, false);

        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.AddComponent<Image>();
        return new OverlayHandle
        {
            canvasObject = canvasObject,
            canvasGroup = canvasGroup,
            fillImage = fillImage
        };
    }

    private static IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            Color instantColor = image.color;
            instantColor.a = to;
            image.color = instantColor;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / duration));
            Color color = image.color;
            color.a = alpha;
            image.color = color;
            yield return null;
        }

        Color finalColor = image.color;
        finalColor.a = to;
        image.color = finalColor;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private static Sprite CreateCornerSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float uvX = x / (float)(size - 1);
                float uvY = y / (float)(size - 1);
                float radial = 1f - Mathf.Clamp01(Mathf.Sqrt(uvX * uvX + uvY * uvY));
                float alpha = Mathf.SmoothStep(0f, 1f, radial);
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 0f), 100f);
    }
}

public sealed class PersistentOverlaySceneWatcher : MonoBehaviour
{
    private string targetSceneName;
    private float destroyDelay;
    private bool sceneHandled;

    public void Configure(string targetScene, float delay)
    {
        targetSceneName = targetScene;
        destroyDelay = Mathf.Max(0f, delay);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (sceneHandled || string.IsNullOrWhiteSpace(targetSceneName) || scene.name != targetSceneName)
            return;

        sceneHandled = true;
        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        if (destroyDelay > 0f)
            yield return new WaitForSecondsRealtime(destroyDelay);

        Destroy(gameObject);
    }
}
