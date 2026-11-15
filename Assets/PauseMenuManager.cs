using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuManager : MonoBehaviour
{
    private const string MenuSceneName = "menu";
    private const string LoadingSceneName = "LoadScene";
    private const string ClocksSceneName = "clocks";
    private const string PuzzleSceneName = "Puzzle";

    private static PauseMenuManager instance;

    private Canvas canvas;
    private GameObject overlayRoot;
    private GameObject panelRoot;
    private Button continueButton;
    private Button settingsButton;
    private Button menuButton;
    private Font runtimeFont;
    private bool isOpen;
    private Sprite panelSprite;
    private Sprite buttonSprite;
    private Sprite continueCustomSprite;
    private Sprite settingsCustomSprite;
    private Sprite menuCustomSprite;
    private RectTransform panelRect;
    private VerticalLayoutGroup panelLayoutGroup;
    private RectTransform titleRect;
    private PauseMenuSceneLayout activeLayoutConfig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("PauseMenuManager");
        instance = go.AddComponent<PauseMenuManager>();
        DontDestroyOnLoad(go);
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
        BuildUi();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameState.isPaused = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!ShouldAllowPause(SceneManager.GetActiveScene()))
            return;

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        Toggle();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
            PlayerReferenceUtility.HidePersistentPlayerForMenu();

        RefreshCustomButtonSprites();
        ApplyCustomSpritesToExistingButtons();
        ResolveActiveLayoutConfig(scene);
        ApplyActiveLayoutConfig();

        if (isOpen || overlayRoot == null)
            return;

        overlayRoot.SetActive(false);
        GameState.isPaused = false;
        Time.timeScale = 1f;
    }

    private void Toggle()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        EnsureEventSystem();
        GameState.isPaused = true;
        Input.ResetInputAxes();
        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        Time.timeScale = 0f;
        isOpen = true;
        ClearSelectedUiObject();
    }

    private void Close()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        GameState.isPaused = false;
        Input.ResetInputAxes();
        ClearSelectedUiObject();
        Time.timeScale = 1f;
        isOpen = false;
    }

    private void OnContinuePressed()
    {
        Close();
    }

    private void OnSettingsPressed()
    {
        // Placeholder: settings screen is not implemented yet.
    }

    private void OnMenuPressed()
    {
        Close();
        PlayerReferenceUtility.HidePersistentPlayerForMenu();
        SceneLoader.LoadScene(MenuSceneName);
    }

    private void BuildUi()
    {
        runtimeFont = CreateRuntimeFont();
        panelSprite = CreateDissolvePanelSprite(900, 1280, 64f, 56f, 0.88f, 0.9f);
        buttonSprite = CreateDustRoundedSprite(480, 128, 30f, 22f, 0.96f, 0.5f);
        continueCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/continue");
        settingsCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/settings");
        menuCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/menu");

        GameObject canvasObject = new GameObject("PauseMenuCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);

        overlayRoot = new GameObject("PauseOverlay");
        overlayRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayRoot.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.1f);
        overlayImage.raycastTarget = true;

        panelRoot = new GameObject("PausePanel");
        panelRoot.transform.SetParent(overlayRoot.transform, false);
        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 397.1725f);
        panelRect.anchoredPosition = new Vector2(0f, -0.2792f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = new Color(0.02f, 0.02f, 0.02f, 0.68f);

        panelLayoutGroup = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelLayoutGroup.padding = new RectOffset(32, 32, 26, 24);
        panelLayoutGroup.spacing = -18f;
        panelLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        panelLayoutGroup.childControlHeight = false;
        panelLayoutGroup.childControlWidth = false;
        panelLayoutGroup.childForceExpandHeight = false;
        panelLayoutGroup.childForceExpandWidth = false;

        CreateTitle("Пауза");
        continueButton = CreateButton("Продолжить", continueCustomSprite, OnContinuePressed);
        settingsButton = CreateButton("Настройки", settingsCustomSprite, OnSettingsPressed);
        menuButton = CreateButton("В меню", menuCustomSprite, OnMenuPressed);

        ResolveActiveLayoutConfig(SceneManager.GetActiveScene());
        ApplyActiveLayoutConfig();

        overlayRoot.SetActive(false);
    }

    private void RefreshCustomButtonSprites()
    {
        continueCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/continue");
        settingsCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/settings");
        menuCustomSprite = Resources.Load<Sprite>("PauseMenuButtons/menu");
    }

    private void ApplyCustomSpritesToExistingButtons()
    {
        ApplyCustomSpriteToButton(continueButton, continueCustomSprite);
        ApplyCustomSpriteToButton(settingsButton, settingsCustomSprite);
        ApplyCustomSpriteToButton(menuButton, menuCustomSprite);
    }

    private void ApplyCustomSpriteToButton(Button button, Sprite customSprite)
    {
        if (button == null || customSprite == null)
            return;

        Transform slotTransform = button.transform.parent;
        Transform visualTransform = slotTransform != null ? slotTransform.Find("Visual") : null;
        Image visualImage = visualTransform != null ? visualTransform.GetComponent<Image>() : null;
        if (visualImage != null)
        {
            visualImage.sprite = customSprite;
            visualImage.type = Image.Type.Simple;
            visualImage.preserveAspect = true;
            visualImage.color = Color.white;
        }

        button.targetGraphic = visualImage;

        Transform labelTransform = button.transform.Find("Label");
        if (labelTransform != null)
            labelTransform.gameObject.SetActive(false);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;

        if (slotTransform != null)
        {
            string label = slotTransform.name.Replace("Slot", string.Empty);
            ApplyCustomButtonLayout(slotTransform, GetButtonLayout(label));
        }
    }

    private void CreateTitle(string title)
    {
        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(panelRoot.transform, false);
        titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(425f, 38.0023f);

        Text text = titleObject.AddComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = title;
    }

    private Button CreateButton(string label, Sprite customSprite, UnityEngine.Events.UnityAction action)
    {
        bool hasCustomSprite = customSprite != null;
        if (hasCustomSprite)
        {
            string slotName = GetSlotName(label);
            GameObject slotObject = new GameObject(slotName);
            slotObject.transform.SetParent(panelRoot.transform, false);

            RectTransform slotRect = slotObject.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(430f, 92f);

            LayoutElement slotLayout = slotObject.AddComponent<LayoutElement>();
            slotLayout.preferredWidth = 430f;
            slotLayout.preferredHeight = 92f;
            slotLayout.minWidth = 430f;
            slotLayout.minHeight = 92f;

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(slotObject.transform, false);
            RectTransform visualRect = visualObject.AddComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.sizeDelta = new Vector2(430f, 242f);
            visualRect.anchoredPosition = Vector2.zero;

            Image visualImage = visualObject.AddComponent<Image>();
            visualImage.sprite = customSprite;
            visualImage.type = Image.Type.Simple;
            visualImage.preserveAspect = true;
            visualImage.color = Color.white;
            visualImage.raycastTarget = false;

            GameObject hitObject = new GameObject("HitArea");
            hitObject.transform.SetParent(slotObject.transform, false);
            RectTransform hitRect = hitObject.AddComponent<RectTransform>();
            hitRect.anchorMin = new Vector2(0.5f, 0.5f);
            hitRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitRect.pivot = new Vector2(0.5f, 0.5f);
            hitRect.anchoredPosition = Vector2.zero;

            Image hitImage = hitObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0.003f);
            hitImage.raycastTarget = true;

            Button hitButton = hitObject.AddComponent<Button>();
            hitButton.targetGraphic = visualImage;
            ColorBlock hitColors = hitButton.colors;
            hitColors.normalColor = Color.white;
            hitColors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            hitColors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            hitColors.selectedColor = hitColors.highlightedColor;
            hitColors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            hitButton.colors = hitColors;
            hitButton.onClick.AddListener(action);

            ApplyCustomButtonLayout(slotObject.transform, GetButtonLayout(label));
            return hitButton;
        }

        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(panelRoot.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(340f, 82f);

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        colors.pressedColor = new Color(0.04f, 0.04f, 0.04f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        button.colors = colors;
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;

        return button;
    }

    private static Font CreateRuntimeFont()
    {
        string[] preferredFonts =
        {
            "Comic Sans MS",
            "Comic Sans MS Bold",
            "Arial"
        };

        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 32);
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static string GetSlotName(string label)
    {
        switch (label)
        {
            case "Продолжить":
                return "ПродолжитьSlot";
            case "Настройки":
                return "НастройкиSlot";
            case "В меню":
                return "В менюSlot";
            default:
                return label + "Slot";
        }
    }

    private PauseMenuSceneLayout.ButtonLayout GetButtonLayout(string label)
    {
        PauseMenuSceneLayout layout = activeLayoutConfig != null ? activeLayoutConfig : null;
        if (layout == null)
            return CreateDefaultButtonLayout(label);

        switch (label)
        {
            case "Продолжить":
                return layout.continueButton;
            case "Настройки":
                return layout.settingsButton;
            case "В меню":
                return layout.menuButton;
            default:
                return CreateDefaultButtonLayout(label);
        }
    }

    private PauseMenuSceneLayout.ButtonLayout CreateDefaultButtonLayout(string label)
    {
        switch (label)
        {
            case "Продолжить":
                return new PauseMenuSceneLayout.ButtonLayout(
                    new PauseMenuSceneLayout.ElementLayout(430f, 242f, 5.800003f, -19.7f),
                    new PauseMenuSceneLayout.ElementLayout(240.0782f, 47.7533f, 1.8004f, 46.93992f));
            case "Настройки":
                return new PauseMenuSceneLayout.ButtonLayout(
                    new PauseMenuSceneLayout.ElementLayout(430f, 242f, 2.5198f, 101.6f),
                    new PauseMenuSceneLayout.ElementLayout(219.9139f, 42.769f, 2.5198f, 121.986f));
            case "В меню":
                return new PauseMenuSceneLayout.ButtonLayout(
                    new PauseMenuSceneLayout.ElementLayout(430f, 242f, 1.8612f, 203.4f),
                    new PauseMenuSceneLayout.ElementLayout(223.5972f, 41.5636f, 1.8612f, 133.9892f));
            default:
                return new PauseMenuSceneLayout.ButtonLayout(
                    new PauseMenuSceneLayout.ElementLayout(430f, 242f, 0f, 0f),
                    new PauseMenuSceneLayout.ElementLayout(230f, 46f, 0f, 0f));
        }
    }

    private void ApplyCustomButtonLayout(Transform slotTransform, PauseMenuSceneLayout.ButtonLayout layout)
    {
        if (slotTransform == null || layout == null)
            return;

        RectTransform slotRect = slotTransform.GetComponent<RectTransform>();
        Transform visualTransform = slotTransform.Find("Visual");
        Transform hitTransform = slotTransform.Find("HitArea");
        RectTransform visualRect = visualTransform != null ? visualTransform.GetComponent<RectTransform>() : null;
        RectTransform hitRect = hitTransform != null ? hitTransform.GetComponent<RectTransform>() : null;

        if (slotRect != null)
            slotRect.sizeDelta = new Vector2(layout.visual.size.x, layout.slotHeight);

        LayoutElement slotLayout = slotTransform.GetComponent<LayoutElement>();
        if (slotLayout != null)
        {
            slotLayout.preferredWidth = layout.visual.size.x;
            slotLayout.preferredHeight = layout.slotHeight;
            slotLayout.minWidth = layout.visual.size.x;
            slotLayout.minHeight = layout.slotHeight;
        }

        if (visualRect == null || hitRect == null)
            return;

        visualRect.sizeDelta = layout.visual.size;
        visualRect.anchoredPosition = layout.visual.position;
        hitRect.sizeDelta = layout.hitArea.size;
        hitRect.anchoredPosition = layout.hitArea.position;
    }

    private void ResolveActiveLayoutConfig(Scene scene)
    {
        activeLayoutConfig = null;

        PauseMenuSceneLayout[] layouts = Object.FindObjectsOfType<PauseMenuSceneLayout>(true);
        foreach (PauseMenuSceneLayout layout in layouts)
        {
            if (layout == null)
                continue;

            if (layout.gameObject.scene == scene)
            {
                activeLayoutConfig = layout;
                break;
            }
        }
    }

    private void ApplyActiveLayoutConfig()
    {
        if (panelRect == null || panelLayoutGroup == null)
            return;

        PauseMenuSceneLayout layout = activeLayoutConfig;
        if (layout == null)
            return;

        panelRect.sizeDelta = layout.panel.size;
        panelRect.anchoredPosition = layout.panel.position;
        panelLayoutGroup.padding = layout.panel.padding.ToRectOffset();
        panelLayoutGroup.spacing = layout.panel.spacing;
        panelLayoutGroup.childControlWidth = layout.panel.childControlWidth;
        panelLayoutGroup.childForceExpandWidth = layout.panel.childForceExpandWidth;

        if (titleRect != null)
            titleRect.sizeDelta = layout.title.size;

        ApplyCustomButtonLayout(continueButton != null ? continueButton.transform.parent : null, layout.continueButton);
        ApplyCustomButtonLayout(settingsButton != null ? settingsButton.transform.parent : null, layout.settingsButton);
        ApplyCustomButtonLayout(menuButton != null ? menuButton.transform.parent : null, layout.menuButton);
    }

    private static Sprite CreateDustRoundedSprite(int width, int height, float radius, float dustWidth, float alpha, float noiseStrength)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 half = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 innerHalf = new Vector2(half.x - radius, half.y - radius);
        float noiseScaleA = 0.045f;
        float noiseScaleB = 0.11f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f - half.x;
                float py = y + 0.5f - half.y;
                float qx = Mathf.Abs(px) - innerHalf.x;
                float qy = Mathf.Abs(py) - innerHalf.y;
                float outsideX = Mathf.Max(qx, 0f);
                float outsideY = Mathf.Max(qy, 0f);
                float signedDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float localNoise =
                    Mathf.PerlinNoise((x + 13.7f) * noiseScaleA, (y + 51.2f) * noiseScaleA) * 0.65f +
                    Mathf.PerlinNoise((x + 101.4f) * noiseScaleB, (y + 7.3f) * noiseScaleB) * 0.35f;
                localNoise = (localNoise - 0.5f) * 2f;

                float effectiveDustWidth = Mathf.Max(1f, dustWidth * (1f + localNoise * noiseStrength));
                float core = Mathf.SmoothStep(effectiveDustWidth, 0f, signedDistance);
                float edgeGlow = Mathf.SmoothStep(effectiveDustWidth * 1.45f, 0f, signedDistance) - core;

                float finalAlpha = Mathf.Clamp01(core * alpha + edgeGlow * alpha * 0.34f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, finalAlpha));
            }
        }

        texture.Apply();
        int border = Mathf.Clamp(Mathf.RoundToInt(radius), 8, Mathf.Min(width, height) / 3);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static Sprite CreateDustCloudSprite(int width, int height, float alpha, float noiseStrength)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f - center.x) / Mathf.Max(1f, halfWidth);
                float ny = (y + 0.5f - center.y) / Mathf.Max(1f, halfHeight);

                float radial = Mathf.Sqrt(nx * nx + ny * ny);
                float verticalSoftness = Mathf.Abs(ny);

                float noiseA = Mathf.PerlinNoise((x + 17f) * 0.018f, (y + 43f) * 0.018f);
                float noiseB = Mathf.PerlinNoise((x + 91f) * 0.051f, (y + 7f) * 0.051f);
                float noise = ((noiseA * 0.68f) + (noiseB * 0.32f) - 0.5f) * 2f;

                float edge = radial + verticalSoftness * 0.22f;
                float dissolve = Mathf.SmoothStep(1.06f, 0.62f, edge + noise * 0.18f * noiseStrength);
                float dust = Mathf.SmoothStep(1.22f, 0.72f, edge + noise * 0.32f * noiseStrength) - dissolve;
                float finalAlpha = Mathf.Clamp01(dissolve * alpha + dust * alpha * 0.36f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, finalAlpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateDissolvePanelSprite(int width, int height, float radius, float edgeWidth, float alpha, float noiseStrength)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 half = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 innerHalf = new Vector2(half.x - radius, half.y - radius);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f - half.x;
                float py = y + 0.5f - half.y;
                float qx = Mathf.Abs(px) - innerHalf.x;
                float qy = Mathf.Abs(py) - innerHalf.y;
                float outsideX = Mathf.Max(qx, 0f);
                float outsideY = Mathf.Max(qy, 0f);
                float signedDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float noiseA = Mathf.PerlinNoise((x + 21f) * 0.021f, (y + 47f) * 0.021f);
                float noiseB = Mathf.PerlinNoise((x + 103f) * 0.063f, (y + 9f) * 0.063f);
                float noise = ((noiseA * 0.7f) + (noiseB * 0.3f) - 0.5f) * 2f;

                float verticalFade = Mathf.InverseLerp(1.12f, 0.38f, Mathf.Abs(py) / half.y);
                float edgeNoise = signedDistance + noise * edgeWidth * 0.42f * noiseStrength;

                float core = Mathf.SmoothStep(edgeWidth, 0f, edgeNoise);
                float dust = Mathf.SmoothStep(edgeWidth * 2.1f, 0f, edgeNoise) - core;
                float speckleMask = Mathf.SmoothStep(0.58f, 0.94f, noiseA);
                float speckles = dust * speckleMask * 0.85f;

                float finalAlpha = Mathf.Clamp01((core * alpha + speckles * alpha * 0.55f) * verticalFade);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, finalAlpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool ShouldAllowPause(Scene activeScene)
    {
        if (!activeScene.IsValid())
            return false;

        string sceneName = activeScene.name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (sceneName == MenuSceneName || sceneName == LoadingSceneName)
            return false;

        if (sceneName == ClocksSceneName || sceneName == PuzzleSceneName)
            return false;

        return true;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("PauseMenuEventSystem");
        Object.DontDestroyOnLoad(eventSystemObject);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void ClearSelectedUiObject()
    {
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
            return;

        currentEventSystem.SetSelectedGameObject(null);
    }
}
