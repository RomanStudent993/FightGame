using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Пауза по Esc: фон main.png и кнопки как в главном меню.</summary>
[DefaultExecutionOrder(10000)]
public class GamePauseController : MonoBehaviour
{
    const string ContinueButtonResourcePath = "Menu/button-continue";
    const string ExitButtonResourcePath = "Menu/button-exit";
#if UNITY_EDITOR
    const string EditorContinueButtonPath = "Assets/FightGame/prefabs/Menu/button-continue.png";
    const string EditorExitButtonPath = "Assets/FightGame/prefabs/Menu/button-exit.png";
#endif

    const string MainMenuSceneName = "StartMenu";

    static GamePauseController _instance;
    static bool _sceneHookRegistered;

    [SerializeField] Sprite menuBackgroundSprite;
    [SerializeField] Sprite continueButtonSprite;
    [SerializeField] Sprite exitButtonSprite;
    [SerializeField] float buttonWidth = 560f;
    [SerializeField] float buttonBottomMargin = 320f;
    [SerializeField] float buttonSpacing = 12f;
    [SerializeField] float blockInputAfterUiClickSeconds = 0.2f;

    GameObject _pauseRoot;
    Image _pauseBackground;
    float _timeScaleBeforePause = 1f;
    static float _blockGameplayInputUntilUnscaled;

    public static bool IsPaused => _instance != null && _instance._pauseRoot != null && _instance._pauseRoot.activeSelf;

    public static bool BlocksGameplayInput =>
        IsPaused || GameDeathController.BlocksGameplayInput || Time.unscaledTime < _blockGameplayInputUntilUnscaled || GameFinaleController.IsPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        if (_sceneHookRegistered)
            return;

        _sceneHookRegistered = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstanceForScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsMenuScene(scene.name))
        {
            if (_instance != null)
                Destroy(_instance.gameObject);
            return;
        }

        if (_instance != null)
        {
            if (IsPaused)
                _instance.Resume();
            return;
        }

        EnsureInstanceForScene(scene);
    }

    static void EnsureInstanceForScene(Scene scene)
    {
        if (!scene.IsValid() || IsMenuScene(scene.name))
            return;

        if (_instance != null)
            return;

        var go = new GameObject(nameof(GamePauseController));
        DontDestroyOnLoad(go);
        go.AddComponent<GamePauseController>();
    }

    static bool IsMenuScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        return sceneName == MainMenuSceneName
            || sceneName == "Menu"
            || sceneName.Contains("MenuDemo");
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureEventSystem();
        BuildPauseUi();
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            if (IsPaused)
                Resume();
            _instance = null;
        }
    }

    void Update()
    {
        if (GameFinaleController.IsPlaying || GameDeathController.IsShowing)
            return;

        if (!WasEscapePressed())
            return;

        if (IsPaused)
            Resume();
        else
            Pause();
    }

    void BuildPauseUi()
    {
        if (_pauseRoot != null)
            return;

        _pauseRoot = new GameObject("PauseOverlay");
        _pauseRoot.transform.SetParent(transform, false);

        Canvas pauseCanvas = _pauseRoot.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 1700;

        CanvasScaler scaler = _pauseRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _pauseRoot.AddComponent<GraphicRaycaster>();

        GameObject bgGo = new GameObject("MenuBackground", typeof(RectTransform));
        bgGo.transform.SetParent(_pauseRoot.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());

        _pauseBackground = bgGo.AddComponent<Image>();
        _pauseBackground.raycastTarget = true;
        _pauseBackground.type = Image.Type.Simple;
        _pauseBackground.preserveAspect = false;
        _pauseBackground.color = Color.white;

        ApplyMenuBackground(_pauseBackground);

        GameObject buttonsRoot = new GameObject("PauseButtons", typeof(RectTransform));
        buttonsRoot.transform.SetParent(_pauseRoot.transform, false);
        StretchFull(buttonsRoot.GetComponent<RectTransform>());
        BuildPauseButtons(buttonsRoot.transform);

        _pauseRoot.SetActive(false);
    }

    void BuildPauseButtons(Transform parent)
    {
        Sprite continueSprite = ResolveContinueButton();
        Sprite exitSprite = ResolveExitButton();
        float bottom = buttonBottomMargin;

        if (exitSprite != null)
        {
            AddSpriteButton(parent, "Exit", exitSprite, bottom, ExitToMainMenu);
            bottom += ButtonHeight(exitSprite) + buttonSpacing;
        }

        if (continueSprite != null)
            AddSpriteButton(parent, "Continue", continueSprite, bottom, OnContinueClicked);
    }

    void OnContinueClicked()
    {
        BlockGameplayInputBriefly();
        Resume();
    }

    Button AddSpriteButton(Transform parent, string objectName, Sprite sprite, float bottomOffset, UnityEngine.Events.UnityAction onClick)
    {
        Vector2 size = new Vector2(buttonWidth, ButtonHeight(sprite));

        GameObject btnGo = new GameObject(objectName, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, bottomOffset);

        Image img = btnGo.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        if (sprite.texture != null)
            sprite.texture.filterMode = FilterMode.Point;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        return btn;
    }

    float ButtonHeight(Sprite sprite)
    {
        if (sprite == null) return 0f;
        float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
        return buttonWidth * aspect;
    }

    void Pause()
    {
        if (_pauseRoot == null)
            BuildPauseUi();

        if (_pauseBackground != null && _pauseBackground.sprite == null)
            ApplyMenuBackground(_pauseBackground);

        if (IsPaused)
            return;

        _timeScaleBeforePause = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _pauseRoot.SetActive(true);
    }

    void Resume()
    {
        if (_pauseRoot == null || !_pauseRoot.activeSelf)
            return;

        _pauseRoot.SetActive(false);
        Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        AudioListener.pause = false;
    }

    public static void ForceResume()
    {
        if (_instance != null)
            _instance.Resume();
    }

    void ExitToMainMenu()
    {
        _blockGameplayInputUntilUnscaled = 0f;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (_pauseRoot != null)
            _pauseRoot.SetActive(false);

        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            SceneManager.LoadScene(MainMenuSceneName);
        else
            Debug.LogWarning($"GamePauseController: сцена «{MainMenuSceneName}» недоступна.");
    }

    void BlockGameplayInputBriefly()
    {
        float duration = Mathf.Max(0.05f, blockInputAfterUiClickSeconds);
        _blockGameplayInputUntilUnscaled = Time.unscaledTime + duration;
    }

    static void ApplyMenuBackground(Image target)
    {
        if (target == null)
            return;

        Sprite bg = ResolveMenuBackground();
        if (bg == null)
        {
            Debug.LogWarning("GamePauseController: не найден main.png (Resources/Menu/main).");
            return;
        }

        target.sprite = bg;
        if (bg.texture != null)
            bg.texture.filterMode = FilterMode.Point;
    }

    static Sprite ResolveMenuBackground()
    {
        if (_instance != null && _instance.menuBackgroundSprite != null)
            return _instance.menuBackgroundSprite;

        Sprite sprite = MenuUiAssets.GetMainBackground();
        return CacheBackground(sprite);
    }

    Sprite ResolveContinueButton()
    {
        if (continueButtonSprite != null)
            return continueButtonSprite;

        continueButtonSprite = LoadButtonSprite(ContinueButtonResourcePath
#if UNITY_EDITOR
            , EditorContinueButtonPath
#endif
        );
        return continueButtonSprite;
    }

    Sprite ResolveExitButton()
    {
        if (exitButtonSprite != null)
            return exitButtonSprite;

        exitButtonSprite = LoadButtonSprite(ExitButtonResourcePath
#if UNITY_EDITOR
            , EditorExitButtonPath
#endif
        );
        return exitButtonSprite;
    }

    static Sprite LoadButtonSprite(string resourcePath
#if UNITY_EDITOR
        , string editorAssetPath
#endif
    )
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null && sprites.Length > 0)
            return sprites[0];

#if UNITY_EDITOR
        return LoadSpriteFromAssetPath(editorAssetPath);
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    static Sprite LoadSpriteFromAssetPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null)
            return null;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite editorSprite)
                return editorSprite;
        }

        return null;
    }
#endif

    static Sprite CacheBackground(Sprite sprite)
    {
        if (_instance != null && sprite != null)
            _instance.menuBackgroundSprite = sprite;
        return sprite;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    static bool WasEscapePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
