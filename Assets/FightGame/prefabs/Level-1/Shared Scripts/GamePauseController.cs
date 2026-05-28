using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Пауза по Esc: остановка игры и фон main.png из главного меню.</summary>
[DefaultExecutionOrder(10000)]
public class GamePauseController : MonoBehaviour
{
    const string MenuBackgroundResourcePath = "Menu/main";
#if UNITY_EDITOR
    const string EditorMenuBackgroundPath = "Assets/FightGame/prefabs/Menu/main.png";
#endif

    static GamePauseController _instance;
    static bool _sceneHookRegistered;

    [SerializeField] Sprite menuBackgroundSprite;

    GameObject _pauseRoot;
    Image _pauseBackground;
    float _timeScaleBeforePause = 1f;

    public static bool IsPaused => _instance != null && _instance._pauseRoot != null && _instance._pauseRoot.activeSelf;

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

        return sceneName == "StartMenu"
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
        pauseCanvas.sortingOrder = 500;
        _pauseRoot.AddComponent<GraphicRaycaster>();

        GameObject bgGo = new GameObject("MenuBackground", typeof(RectTransform));
        bgGo.transform.SetParent(_pauseRoot.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());

        _pauseBackground = bgGo.AddComponent<Image>();
        _pauseBackground.raycastTarget = true;
        _pauseBackground.type = Image.Type.Simple;
        _pauseBackground.preserveAspect = false;
        _pauseBackground.color = Color.white;

        Sprite bg = ResolveMenuBackground();
        if (bg != null)
        {
            _pauseBackground.sprite = bg;
            if (bg.texture != null)
                bg.texture.filterMode = FilterMode.Point;
        }
        else
            Debug.LogWarning("GamePauseController: не найден main.png (Resources/Menu/main или prefabs/Menu/main.png).");

        _pauseRoot.SetActive(false);
    }

    static Sprite ResolveMenuBackground()
    {
        if (_instance != null && _instance.menuBackgroundSprite != null)
            return _instance.menuBackgroundSprite;

        Sprite sprite = Resources.Load<Sprite>(MenuBackgroundResourcePath);
        if (sprite != null)
            return CacheBackground(sprite);

        Sprite[] sprites = Resources.LoadAll<Sprite>(MenuBackgroundResourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && (sprites[i].name == "main_0" || sprites[i].name == "main"))
                    return CacheBackground(sprites[i]);
            }

            return CacheBackground(sprites[0]);
        }

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorMenuBackgroundPath);
        if (assets != null)
        {
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite editorSprite)
                    return CacheBackground(editorSprite);
            }
        }
#endif

        return null;
    }

    static Sprite CacheBackground(Sprite sprite)
    {
        if (_instance != null)
            _instance.menuBackgroundSprite = sprite;
        return sprite;
    }

    void Pause()
    {
        if (_pauseRoot == null)
            BuildPauseUi();

        if (_pauseBackground != null && _pauseBackground.sprite == null)
        {
            Sprite bg = ResolveMenuBackground();
            if (bg != null)
            {
                _pauseBackground.sprite = bg;
                if (bg.texture != null)
                    bg.texture.filterMode = FilterMode.Point;
            }
        }

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
