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

/// <summary>Экран смерти игрока: как пауза, сверху «Возвратиться», снизу «Выйти».</summary>
[DefaultExecutionOrder(10050)]
public class GameDeathController : MonoBehaviour
{
    const string MenuBackgroundResourcePath = "Menu/main";
    const string ReturnButtonResourcePath = "Menu/return-menu";
    const string ExitButtonResourcePath = "Menu/button-exit";
#if UNITY_EDITOR
    const string EditorMenuBackgroundPath = "Assets/FightGame/prefabs/Menu/main.png";
    const string EditorReturnButtonPath = "Assets/FightGame/prefabs/Menu/return-menu.png";
    const string EditorExitButtonPath = "Assets/FightGame/prefabs/Menu/button-exit.png";
#endif

    const string MainMenuSceneName = "StartMenu";

    static GameDeathController _instance;
    static bool _sceneHookRegistered;
    static float _blockGameplayInputUntilUnscaled;

    [SerializeField] Sprite menuBackgroundSprite;
    [SerializeField] Sprite returnButtonSprite;
    [SerializeField] Sprite exitButtonSprite;
    [SerializeField] float buttonWidth = 560f;
    [SerializeField] float buttonBottomMargin = 320f;
    [SerializeField] float buttonSpacing = 4f;
    [SerializeField] float blockInputAfterUiClickSeconds = 0.2f;
    [SerializeField] Vector2 returnButtonHitboxPadding = new Vector2(56f, 32f);

    GameObject _deathRoot;
    Image _deathBackground;
    float _timeScaleBeforeDeath = 1f;
    bool _deathActive;

    public static bool IsShowing => _instance != null && _instance._deathActive;

    public static bool BlocksGameplayInput =>
        IsShowing || Time.unscaledTime < _blockGameplayInputUntilUnscaled;

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
            _instance.HideDeathScreen();
        else
            EnsureInstanceForScene(scene);
    }

    static void EnsureInstanceForScene(Scene scene)
    {
        if (!scene.IsValid() || IsMenuScene(scene.name))
            return;

        if (_instance != null)
            return;

        var go = new GameObject(nameof(GameDeathController));
        DontDestroyOnLoad(go);
        go.AddComponent<GameDeathController>();
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
        BuildDeathUi();
    }

    void OnEnable()
    {
        SimpleHealth.Died += OnCharacterDied;
    }

    void OnDisable()
    {
        SimpleHealth.Died -= OnCharacterDied;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            if (_deathActive)
                HideDeathScreen();
            _instance = null;
        }
    }

    void OnCharacterDied(GameObject who)
    {
        if (_deathActive || who == null || GameFinaleController.IsPlaying)
            return;

        if (!IsPlayerDeath(who))
            return;

        ShowDeathScreen();
    }

    static bool IsPlayerDeath(GameObject who)
    {
        Transform root = who.transform.root;
        if (root.CompareTag("Player"))
            return true;

        return root.GetComponentInChildren<HeroKnight>(true) != null;
    }

    void ShowDeathScreen()
    {
        if (_deathRoot == null)
            BuildDeathUi();

        if (_deathActive)
            return;

        if (GamePauseController.IsPaused)
            GamePauseController.ForceResume();

        EnsureEventSystem();
        EnsureDeathBackground();

        _deathActive = true;
        _timeScaleBeforeDeath = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _deathRoot.SetActive(true);
    }

    void EnsureDeathBackground()
    {
        if (_deathBackground == null)
            return;

        if (_deathBackground.sprite != null)
            return;

        Sprite bg = ResolveMenuBackground();
        if (bg == null)
        {
            Debug.LogWarning("GameDeathController: не найден main.png (Resources/Menu/main или prefabs/Menu/main.png).");
            return;
        }

        _deathBackground.sprite = bg;
        if (bg.texture != null)
            bg.texture.filterMode = FilterMode.Point;
    }

    void HideDeathScreen()
    {
        _deathActive = false;
        _blockGameplayInputUntilUnscaled = 0f;

        if (_deathRoot != null)
            _deathRoot.SetActive(false);

        if (Time.timeScale <= 0f)
        {
            Time.timeScale = _timeScaleBeforeDeath > 0f ? _timeScaleBeforeDeath : 1f;
            AudioListener.pause = false;
        }
    }

    void BuildDeathUi()
    {
        if (_deathRoot != null)
            return;

        _deathRoot = new GameObject("DeathOverlay");
        _deathRoot.transform.SetParent(transform, false);

        Canvas canvas = _deathRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;

        CanvasScaler scaler = _deathRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _deathRoot.AddComponent<GraphicRaycaster>();

        GameObject bgGo = new GameObject("MenuBackground", typeof(RectTransform));
        bgGo.transform.SetParent(_deathRoot.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());

        _deathBackground = bgGo.AddComponent<Image>();
        _deathBackground.raycastTarget = false;
        _deathBackground.type = Image.Type.Simple;
        _deathBackground.preserveAspect = false;
        _deathBackground.color = Color.white;

        Sprite bg = ResolveMenuBackground();
        if (bg != null)
        {
            _deathBackground.sprite = bg;
            if (bg.texture != null)
                bg.texture.filterMode = FilterMode.Point;
        }

        GameObject buttonsRoot = new GameObject("DeathButtons", typeof(RectTransform));
        buttonsRoot.transform.SetParent(_deathRoot.transform, false);
        StretchFull(buttonsRoot.GetComponent<RectTransform>());
        BuildDeathButtons(buttonsRoot.transform);

        _deathRoot.SetActive(false);
    }

    void BuildDeathButtons(Transform parent)
    {
        Sprite returnSprite = ResolveReturnButton();
        Sprite exitSprite = ResolveExitButton();
        float bottom = buttonBottomMargin;
        Vector2 buttonSize = exitSprite != null
            ? new Vector2(buttonWidth, ButtonHeight(exitSprite))
            : new Vector2(buttonWidth, 140f);

        if (exitSprite != null)
        {
            AddSpriteButton(parent, "Exit", exitSprite, bottom, buttonSize, ExitToMainMenu);
            bottom += buttonSize.y + buttonSpacing - returnButtonHitboxPadding.y;
        }

        if (returnSprite != null)
            AddSpriteButtonWithHitbox(parent, "Return", returnSprite, bottom, buttonSize, returnButtonHitboxPadding, OnReturnClicked);
    }

    void OnReturnClicked()
    {
        BlockGameplayInputBriefly();
        ReturnToCurrentLevel();
    }

    void ReturnToCurrentLevel()
    {
        _deathActive = false;
        _blockGameplayInputUntilUnscaled = 0f;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (_deathRoot != null)
            _deathRoot.SetActive(false);

        BattleIntroController.ResetForLevelRestart();

        ContinuePrompt.EnsureSceneLoadedHook();
        ContinuePrompt.ShowBlockingOverlay(ResolveMenuBackground());

        Scene scene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(scene.path))
            SceneManager.LoadScene(scene.path, LoadSceneMode.Single);
        else if (!string.IsNullOrEmpty(scene.name))
            SceneManager.LoadScene(scene.name, LoadSceneMode.Single);
        else
            Debug.LogWarning("GameDeathController: не удалось перезагрузить уровень.");
    }

    void ExitToMainMenu()
    {
        _deathActive = false;
        _blockGameplayInputUntilUnscaled = 0f;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (_deathRoot != null)
            _deathRoot.SetActive(false);

        ContinuePrompt.EnsureSceneLoadedHook();
        ContinuePrompt.ShowBlockingOverlay(ResolveMenuBackground());

        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            SceneManager.LoadScene(MainMenuSceneName);
        else
            Debug.LogWarning($"GameDeathController: сцена «{MainMenuSceneName}» недоступна.");
    }

    void BlockGameplayInputBriefly()
    {
        float duration = Mathf.Max(0.05f, blockInputAfterUiClickSeconds);
        _blockGameplayInputUntilUnscaled = Time.unscaledTime + duration;
    }

    Button AddSpriteButton(
        Transform parent,
        string objectName,
        Sprite sprite,
        float bottomOffset,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick,
        bool preserveAspect = true)
    {
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
        img.preserveAspect = preserveAspect;
        if (sprite != null && sprite.texture != null)
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

    Button AddSpriteButtonWithHitbox(
        Transform parent,
        string objectName,
        Sprite sprite,
        float bottomOffset,
        Vector2 visualSize,
        Vector2 hitboxPadding,
        UnityEngine.Events.UnityAction onClick)
    {
        Vector2 hitSize = visualSize + hitboxPadding * 2f;

        GameObject btnGo = new GameObject(objectName, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = hitSize;
        rt.anchoredPosition = new Vector2(0f, bottomOffset);

        Image hitImage = btnGo.AddComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0f);
        hitImage.raycastTarget = true;

        GameObject visualGo = new GameObject("Visual", typeof(RectTransform));
        visualGo.transform.SetParent(btnGo.transform, false);
        RectTransform visualRt = visualGo.GetComponent<RectTransform>();
        visualRt.anchorMin = new Vector2(0.5f, 0.5f);
        visualRt.anchorMax = new Vector2(0.5f, 0.5f);
        visualRt.pivot = new Vector2(0.5f, 0.5f);
        visualRt.sizeDelta = visualSize;
        visualRt.anchoredPosition = Vector2.zero;

        Image visualImage = visualGo.AddComponent<Image>();
        visualImage.sprite = sprite;
        visualImage.type = Image.Type.Simple;
        visualImage.preserveAspect = true;
        visualImage.raycastTarget = false;
        if (sprite != null && sprite.texture != null)
            sprite.texture.filterMode = FilterMode.Point;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = hitImage;
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

    Sprite ResolveMenuBackground()
    {
        if (menuBackgroundSprite != null)
            return menuBackgroundSprite;

        return menuBackgroundSprite = MenuUiAssets.GetMainBackground();
    }

    Sprite ResolveReturnButton()
    {
        if (returnButtonSprite != null)
            return returnButtonSprite;

        returnButtonSprite = LoadButtonSprite(ReturnButtonResourcePath
#if UNITY_EDITOR
            , EditorReturnButtonPath
#endif
        );
        return returnButtonSprite;
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
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name.EndsWith("_0"))
                    return sprites[i];
            }

            return sprites[0];
        }

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

    static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            DontDestroyOnLoad(esGo);
            eventSystem = esGo.AddComponent<EventSystem>();
        }
        else
        {
            DontDestroyOnLoad(eventSystem.gameObject);
        }

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
            Object.Destroy(legacyModule);

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
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
