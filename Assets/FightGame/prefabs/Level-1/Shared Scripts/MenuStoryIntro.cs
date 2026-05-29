using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Экран перед туториалом: фон + текст по буквам (Aventura).</summary>
public class MenuStoryIntro : MonoBehaviour
{
    static readonly Color StoryColor = new Color(0.94f, 0.86f, 0.62f, 1f);

    const string DefaultStory =
        "Рассвет ещё не занялся, когда к стенам Альтариса подошёл Гниющий Король. С ним - армия, которой не должно быть на свете: рыцари без знамён, воины из выжженных земель, маги с выгоревшими глазами, а небо затянули крылья гарпий.\n\n" +
        "Замок пал за час. Стража легла у ворот. Совет магов пропал в зелёном пламени.\n\n" +
        "Король прошёл через тронный зал, не глядя на престол. Ему не нужна была власть. Только она.\n\n" +
        "Теперь принцесса в его руках. Что ждёт её дальше - никто не скажет. Но ты знаешь: стоять нельзя.\n\n" +
        "Стены ещё дымятся. Она ждёт. А впереди - только враги.";

    [Header("Оформление")]
    [SerializeField] Sprite loadingBackground;
    [SerializeField] int storyFontSize = 32;
    [SerializeField] float lineSpacing = 0.92f;
    [SerializeField] Color storyTextColor = StoryColor;

    [Header("Тайминг")]
    [SerializeField] float charsPerSecond = 32f;
    [SerializeField] float holdSpaceSpeedMultiplier = 4f;
    [SerializeField] float pauseAfterPeriod = 0.28f;
    [SerializeField] float pauseAfterParagraph = 0.55f;
    [SerializeField] float pauseBeforeSceneLoad = 2.5f;
    [SerializeField] float skipDelaySeconds = 0.4f;
    [SerializeField] bool allowSkip = true;

    GameObject _panel;
    Canvas _storyCanvas;
    Image _backgroundImage;
    Text _storyUi;
    ScrollRect _scroll;
    RectTransform _scrollContent;
    Coroutine _routine;
    string _targetScene;
    bool _isPlaying;
    bool _forceSkipRequested;
    float _playStartTime;
    AsyncOperation _sceneLoad;
    string _preloadedSceneName;

    public bool IsPlaying => _isPlaying;

    public Sprite LoadingBackground => loadingBackground;

    public void EnsureLoadingBackground(Sprite sprite)
    {
        if (loadingBackground == null && sprite != null)
            loadingBackground = sprite;
    }

    public void PreloadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (_sceneLoad != null && _preloadedSceneName == sceneName)
            return;

        _preloadedSceneName = sceneName;
        _targetScene = sceneName;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
            return;

        _sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (_sceneLoad == null)
            return;

        _sceneLoad.allowSceneActivation = false;
    }

    public void Build(Transform canvasParent)
    {
        if (_panel != null) return;

        _panel = new GameObject("StoryIntro", typeof(RectTransform));
        _panel.transform.SetParent(canvasParent, false);
        StretchFull(_panel.GetComponent<RectTransform>());

        _storyCanvas = _panel.AddComponent<Canvas>();
        _storyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _storyCanvas.overrideSorting = true;
        _storyCanvas.sortingOrder = 200;
        _panel.AddComponent<GraphicRaycaster>();

        GameObject bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(_panel.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        _backgroundImage = bgGo.AddComponent<Image>();
        _backgroundImage.raycastTarget = false;
        _backgroundImage.type = Image.Type.Simple;
        _backgroundImage.preserveAspect = false;

        GameObject scrollGo = new GameObject("StoryScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(_panel.transform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(36f, 28f);
        scrollRt.offsetMax = new Vector2(-36f, -28f);

        _scroll = scrollGo.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 24f;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        StretchFull(viewportRt);
        viewportGo.AddComponent<RectMask2D>();
        _scroll.viewport = viewportRt;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        _scrollContent = contentGo.GetComponent<RectTransform>();
        _scrollContent.anchorMin = new Vector2(0f, 1f);
        _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = new Vector2(0f, 400f);
        _scroll.content = _scrollContent;

        GameObject textGo = new GameObject("StoryText", typeof(RectTransform));
        textGo.transform.SetParent(contentGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(-8f, 0f);

        _storyUi = textGo.AddComponent<Text>();
        _storyUi.text = "";
        _storyUi.font = ResolveStoryFont(DefaultStory);
        _storyUi.fontSize = storyFontSize;
        _storyUi.lineSpacing = lineSpacing;
        _storyUi.fontStyle = FontStyle.Normal;
        _storyUi.color = storyTextColor;
        _storyUi.alignment = TextAnchor.UpperLeft;
        _storyUi.horizontalOverflow = HorizontalWrapMode.Wrap;
        _storyUi.verticalOverflow = VerticalWrapMode.Overflow;
        _storyUi.raycastTarget = false;
        _storyUi.supportRichText = false;

        ContentSizeFitter textFitter = textGo.AddComponent<ContentSizeFitter>();
        textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _panel.SetActive(false);
    }

    void ShowBackground()
    {
        if (_backgroundImage == null || loadingBackground == null) return;

        if (loadingBackground.texture != null)
            loadingBackground.texture.filterMode = FilterMode.Point;

        _backgroundImage.sprite = loadingBackground;
        _backgroundImage.enabled = true;
    }

    public void Play(string sceneName)
    {
        if (_panel == null) return;

        _targetScene = sceneName;
        _playStartTime = Time.unscaledTime;
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);
        ShowBackground();

        string full = GetFullStory();
        Font font = ResolveStoryFont(full);
        _storyUi.font = font;
        _storyUi.text = "";
        PrepareFontForStory(font, full, storyFontSize);

        if (_routine != null)
            StopCoroutine(_routine);

        PreloadScene(sceneName);
        _routine = StartCoroutine(PlayRoutine(full));
    }

    void Update()
    {
        if (!_isPlaying)
            return;

        // Ctrl+Shift+Пробел во время текста — скип истории (в меню без текста — удаление сохранений).
        if (WasForceSkipScenePressed())
            _forceSkipRequested = true;
    }

    bool ShouldForceSkipScene() => _forceSkipRequested || WasForceSkipScenePressed();

    IEnumerator PlayRoutine(string full)
    {
        _isPlaying = true;
        _forceSkipRequested = false;

        yield return null;
        ShowStoryText("");

        var shown = new StringBuilder(full.Length);

        for (int i = 0; i < full.Length; i++)
        {
            if (ShouldForceSkipScene())
            {
                ActivateSceneNow();
                yield break;
            }

            if (allowSkip && Time.unscaledTime - _playStartTime >= skipDelaySeconds && WasShowAllTextPressed())
            {
                ShowStoryText(full);
                break;
            }

            char c = full[i];
            shown.Append(c);
            ShowStoryText(shown.ToString());

            yield return WaitStoryDelay(GetCharDelay(c, full, i));
        }

        if (ShouldForceSkipScene())
        {
            ActivateSceneNow();
            yield break;
        }

        ShowStoryText(full);

        float pauseEnd = Time.unscaledTime + Mathf.Max(0f, pauseBeforeSceneLoad);
        while (Time.unscaledTime < pauseEnd)
        {
            if (ShouldForceSkipScene())
            {
                ActivateSceneNow();
                yield break;
            }

            if (allowSkip && WasShowAllTextPressed())
                break;

            yield return null;
        }

        ActivateSceneNow();
    }

    IEnumerator WaitStoryDelay(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
        {
            if (ShouldForceSkipScene())
                yield break;

            yield return null;
        }
    }

    void ShowStoryText(string text)
    {
        _storyUi.text = text;
        UpdateContentHeight();
        ScrollToLatestLine();
    }

    void ActivateSceneNow()
    {
        if (!_isPlaying)
            return;

        _isPlaying = false;

        if (_sceneLoad != null && _sceneLoad.progress >= 0.9f)
        {
            _sceneLoad.allowSceneActivation = true;
            return;
        }

        LoadTargetSceneSync();
    }

    void LoadTargetSceneSync()
    {
        if (!string.IsNullOrEmpty(_targetScene) && Application.CanStreamedLevelBeLoaded(_targetScene))
            SceneManager.LoadScene(_targetScene);
        else
            Debug.LogWarning($"MenuStoryIntro: сцена «{_targetScene}» недоступна.");
    }

    string GetFullStory() => CompressStorySpacing(DefaultStory);

    static string CompressStorySpacing(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw.Replace("\r\n", "\n").Replace("\n\n\n", "\n\n");
    }

    Font ResolveStoryFont(string full)
    {
        GameFont.Reload();
        Font font = GameFont.ResolveForText(full, storyFontSize);
        if (font != null)
            return font;

        font = GameFont.BuiltInFallback;
        if (font == null)
            Debug.LogError("MenuStoryIntro: не найден шрифт для текста истории.");
        return font;
    }

    static void PrepareFontForStory(Font font, string full, int fontSize)
    {
        if (font == null || string.IsNullOrEmpty(full)) return;
        GameFont.RequestGlyphs(full, fontSize, FontStyle.Normal);
    }

    void UpdateContentHeight()
    {
        if (_storyUi == null || _scrollContent == null) return;

        float height = _storyUi.preferredHeight + 24f;
        _scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(height, 200f));
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        Canvas.ForceUpdateCanvases();
    }

    void ScrollToLatestLine()
    {
        if (_scroll == null) return;
        _scroll.verticalNormalizedPosition = 0f;
    }

    float GetCharDelay(char c, string full, int index)
    {
        if (index >= full.Length - 1)
            return 0f;

        float delay = DelayFor(c, full, index);
        if (IsSpaceHeld())
            delay /= Mathf.Max(1f, holdSpaceSpeedMultiplier);
        return delay;
    }

    float DelayFor(char c, string full, int index)
    {
        float baseDelay = 1f / Mathf.Max(1f, charsPerSecond);

        if (c == '\n')
        {
            bool paragraphBreak = index > 0 && full[index - 1] == '\n';
            return paragraphBreak ? pauseAfterParagraph : baseDelay * 0.5f;
        }

        if (c == '.' || c == '!' || c == '?')
            return baseDelay + pauseAfterPeriod;

        if (c == ',' || c == ';' || c == ':')
            return baseDelay + pauseAfterPeriod * 0.35f;

        if (c == '-')
            return baseDelay + pauseAfterPeriod * 0.2f;

        return baseDelay;
    }

    static bool IsSpaceHeld()
    {
        if (Input.GetKey(KeyCode.Space))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            return true;
#endif
        return false;
    }

    static bool WasShowAllTextPressed()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    static bool WasForceSkipScenePressed()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl && shift && Input.GetKeyDown(KeyCode.Space))
            return true;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            bool ctrlNew = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            bool shiftNew = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            if (ctrlNew && shiftNew && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
        }
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
